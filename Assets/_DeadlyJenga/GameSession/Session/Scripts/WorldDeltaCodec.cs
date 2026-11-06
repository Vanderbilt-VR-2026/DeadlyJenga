using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

namespace DeadlyJenga.CastlePrototype
{
    // Lossless raw snapshot bytes only. Sender anchors advance on applied-frame ACKs, never on send.
    internal static class WorldDeltaCodec
    {
        internal const int MaxBytes=2*1024*1024,CacheBytes=256*1024,HeaderBytes=136,TokenBytes=60;
        const uint Magic=0x3144574a,TokenMagic=0x3141574a;
        internal enum Status{Ready,Duplicate,Stale,MissingBase}
        internal sealed class Frame
        {
            internal Guid generation;internal int serial,length;internal byte[] hash,raw,packetHash;
            internal Status status;internal bool delta;
            internal byte[] Token()=>WorldDeltaCodec.Token(generation,serial,length,hash);
        }
        static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
        static byte[] Hash(byte[] bytes){using var hash=SHA256.Create();return hash.ComputeHash(bytes);}
        static byte[] EnvelopeHash(byte[] bytes){var copy=(byte[])bytes.Clone();Array.Clear(copy,104,32);return Hash(copy);}
        static bool Equal(byte[] a,byte[] b)=>a!=null&&b!=null&&a.SequenceEqual(b);
        static bool Newer(int value,int previous)=>value!=0&&unchecked((int)((uint)value-(uint)previous))>0;
        static byte[] Zip(byte[] raw)
        {using var output=new MemoryStream();using(var zip=new DeflateStream(output,CompressionLevel.Fastest,true))zip.Write(raw,0,raw.Length);return output.ToArray();}
        static byte[] Unzip(byte[] packet,int length)
        {
            Require(length>0&&length<=MaxBytes,"World expansion bound invalid");var raw=new byte[length];
            using var input=new MemoryStream(packet,false);using var zip=new DeflateStream(input,CompressionMode.Decompress);
            int at=0,n;while(at<length&&(n=zip.Read(raw,at,length-at))>0)at+=n;
            Require(at==length&&zip.ReadByte()==-1,"World expansion differs from exact target length");return raw;
        }
        static byte[] Token(Guid generation,int serial,int length,byte[] hash)
        {using var output=new MemoryStream();using var w=new BinaryWriter(output);w.Write(TokenMagic);w.Write(generation.ToByteArray());w.Write(serial);w.Write(length);w.Write(hash);return output.ToArray();}
        static bool Match(byte[] token,Guid generation,int serial,int length,byte[] hash)
        {return token!=null&&token.Length==TokenBytes&&Equal(token,Token(generation,serial,length,hash));}
        internal sealed class Sender
        {
            Guid generation=Guid.NewGuid();string context;
            byte[] anchor,anchorHash,pending,pendingHash;int anchorSerial,pendingSerial,pendingLength;bool invalidated;
            internal int PendingSerial=>pendingSerial;
            internal int LastFullPacketBytes{get;private set;}
            internal int CachedBytes=>(anchor?.Length??0)+(pending?.Length??0);
            internal long FullFrames,DeltaFrames;
            internal void Reset(){generation=Guid.NewGuid();context=null;anchor=pending=anchorHash=pendingHash=null;anchorSerial=pendingSerial=pendingLength=0;invalidated=false;}
            internal void Invalidate()
            {if(pendingSerial==0){Reset();return;}anchor=pending=anchorHash=null;anchorSerial=0;context=null;invalidated=true;}
            internal byte[] Prepare(byte[] raw,string nextContext,int serial)
            {
                Require(pendingSerial==0,"Only one world frame may be in flight");
                Require(raw!=null&&raw.Length>0&&raw.Length<=MaxBytes&&serial!=0&&nextContext!=null,"World target invalid");
                if(nextContext!=context){Reset();context=nextContext;}
                var full=Zip(raw);LastFullPacketBytes=full.Length+HeaderBytes;byte[] body=full;bool delta=false;
                if(anchor!=null&&raw.Length<=CacheBytes)
                {
                    var xor=new byte[raw.Length];for(int i=0;i<xor.Length;i++)xor[i]=(byte)(raw[i]^(i<anchor.Length?anchor[i]:0));
                    var compressed=Zip(xor);if(compressed.Length<full.Length){body=compressed;delta=true;}
                }
                Require(body.Length<=MaxBytes-HeaderBytes,"World envelope exceeds transfer limit");
                var targetHash=Hash(raw);using var output=new MemoryStream();
                using(var w=new BinaryWriter(output,System.Text.Encoding.UTF8,true))
                {
                    w.Write(Magic);w.Write((byte)1);w.Write((byte)(delta?1:0));w.Write((ushort)0);w.Write(generation.ToByteArray());
                    w.Write(serial);w.Write(delta?anchorSerial:0);w.Write(delta?anchor.Length:0);w.Write(raw.Length);
                    w.Write(delta?anchorHash:new byte[32]);w.Write(targetHash);w.Write(new byte[32]);w.Write(body);
                }
                pendingSerial=serial;pendingLength=raw.Length;pendingHash=targetHash;
                pending=raw.Length<=CacheBytes?(byte[])raw.Clone():null;
                var packet=output.ToArray();Array.Copy(EnvelopeHash(packet),0,packet,104,32);
                if(delta)DeltaFrames++;else FullFrames++;return packet;
            }
            internal bool Acknowledge(int serial,byte[] token)
            {
                if(serial==0||serial!=pendingSerial||!Match(token,generation,pendingSerial,pendingLength,pendingHash))return false;
                if(invalidated){Reset();return true;}
                anchor=pending;anchorHash=pendingHash;anchorSerial=pendingSerial;
                pending=null;pendingHash=null;pendingSerial=pendingLength=0;return true;
            }
            internal bool Resynchronize(int serial,byte[] token)
            {if(serial==0||serial!=pendingSerial||!Match(token,generation,pendingSerial,pendingLength,pendingHash))return false;Reset();return true;}
            internal void Cancel(){pending=null;pendingHash=null;pendingSerial=pendingLength=0;}
            internal bool MatchesPending(int serial,byte[] token)=>serial!=0&&serial==pendingSerial&&Match(token,generation,pendingSerial,pendingLength,pendingHash);
        }
        internal sealed class Receiver
        {
            Guid generation;int serial,length;bool seen;byte[] anchor,hash,lastPacketHash;
            internal int CachedBytes=>anchor?.Length??0;
            internal void Reset(){generation=Guid.Empty;serial=length=0;seen=false;anchor=hash=lastPacketHash=null;}
            internal Frame Decode(byte[] packet,int expectedSerial)
            {
                Require(packet!=null&&packet.Length>HeaderBytes&&packet.Length<=MaxBytes,"World envelope size invalid");
                using var r=new BinaryReader(new MemoryStream(packet,false));
                Require(r.ReadUInt32()==Magic&&r.ReadByte()==1,"World protocol mismatch");
                byte mode=r.ReadByte();Require(mode<=1&&r.ReadUInt16()==0,"World mode invalid");
                var nextGeneration=new Guid(r.ReadBytes(16));int nextSerial=r.ReadInt32(),baseSerial=r.ReadInt32(),baseLength=r.ReadInt32(),targetLength=r.ReadInt32();
                var baseHash=r.ReadBytes(32);var targetHash=r.ReadBytes(32);var bodyHash=r.ReadBytes(32);
                Require(nextGeneration!=Guid.Empty&&nextSerial!=0&&nextSerial==expectedSerial&&targetLength>0&&targetLength<=MaxBytes,"World target header invalid");
                Require(mode==0?baseSerial==0&&baseLength==0&&baseHash.All(x=>x==0):baseSerial!=0&&baseLength>0&&baseLength<=CacheBytes&&targetLength<=CacheBytes,"World base header invalid");
                var packetHash=Hash(packet);var body=r.ReadBytes(packet.Length-HeaderBytes);Require(Equal(EnvelopeHash(packet),bodyHash),"World envelope integrity failed");
                var frame=new Frame{generation=nextGeneration,serial=nextSerial,length=targetLength,hash=targetHash,packetHash=packetHash,delta=mode==1};
                if(seen&&nextGeneration==generation&&nextSerial==serial)
                {Require(targetLength==length&&Equal(targetHash,hash)&&Equal(packetHash,lastPacketHash),"Conflicting duplicate world");frame.status=Status.Duplicate;return frame;}
                if(seen&&!Newer(nextSerial,serial)){frame.status=Status.Stale;return frame;}
                if(mode==1&&(nextGeneration!=generation||anchor==null||baseSerial!=serial||baseLength!=anchor.Length||!Equal(baseHash,hash)))
                {frame.status=Status.MissingBase;return frame;}
                var raw=Unzip(body,targetLength);if(mode==1)for(int i=0;i<raw.Length;i++)raw[i]^=i<anchor.Length?anchor[i]:(byte)0;
                Require(Equal(Hash(raw),targetHash),"World target integrity failed");frame.raw=raw;frame.status=Status.Ready;return frame;
            }
            // Call only after the existing complete validated world application returns normally.
            internal void Applied(Frame frame)
            {
                Require(frame!=null&&frame.status==Status.Ready&&frame.raw!=null&&frame.raw.Length==frame.length&&Equal(Hash(frame.raw),frame.hash),"Unapplied world cannot become baseline");
                Require(!seen||Newer(frame.serial,serial),"Stale world cannot become baseline");
                generation=frame.generation;serial=frame.serial;length=frame.length;hash=frame.hash;lastPacketHash=frame.packetHash;seen=true;
                anchor=frame.raw.Length<=CacheBytes?(byte[])frame.raw.Clone():null;
            }
        }
    }
}
