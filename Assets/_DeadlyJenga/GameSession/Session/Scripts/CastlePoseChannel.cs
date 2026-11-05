using System;
using System.Collections.Generic;
using System.IO;
using Fusion;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    internal sealed class CastlePoseReassembly
    {
        internal const int ChunkBytes=384,MaxBytes=4096;
        int sequence,length,mask;
        float began;
        byte[] buffer;
        bool seen,finished;
        internal bool Push(int serial,int total,int index,byte[] part,float now,out byte[] complete)
        {
            complete=null;
            if(serial==0||total<1||total>MaxBytes||part==null||index<0||index>=(total+ChunkBytes-1)/ChunkBytes||
                part.Length!=Math.Min(ChunkBytes,total-index*ChunkBytes))return false;
            if(!seen||serial!=sequence)
            {
                if(seen&&unchecked((int)((uint)serial-(uint)sequence))<=0)return false;
                seen=true;sequence=serial;length=total;mask=0;began=now;finished=false;buffer=new byte[total];
            }
            if(finished||length!=total)return false;
            if(now-began>.25f){finished=true;buffer=null;return false;}
            int bit=1<<index;
            if((mask&bit)!=0)return false;
            Array.Copy(part,0,buffer,index*ChunkBytes,part.Length);mask|=bit;
            if(mask!=(1<<((length+ChunkBytes-1)/ChunkBytes))-1)return true;
            complete=buffer;buffer=null;finished=true;return true;
        }
    }

    public struct CastlePoseWords : INetworkStruct
    {
        public ulong a,b,c,d,e,f,g,h;
        internal static CastlePoseWords Pack(byte[] bytes,int at)=>new CastlePoseWords{a=Read(bytes,at),b=Read(bytes,at+8),c=Read(bytes,at+16),d=Read(bytes,at+24),e=Read(bytes,at+32),f=Read(bytes,at+40),g=Read(bytes,at+48),h=Read(bytes,at+56)};
        static ulong Read(byte[] bytes,int at){ulong value=0;for(int i=0;i<8&&at+i<bytes.Length;i++)value|=(ulong)bytes[at+i]<<(i*8);return value;}
        static void Write(ulong value,byte[] bytes,int at){for(int i=0;i<8&&at+i<bytes.Length;i++)bytes[at+i]=(byte)(value>>(i*8));}
        internal void CopyTo(byte[] bytes,int at){Write(a,bytes,at);Write(b,bytes,at+8);Write(c,bytes,at+16);Write(d,bytes,at+24);Write(e,bytes,at+32);Write(f,bytes,at+40);Write(g,bytes,at+48);Write(h,bytes,at+56);}
    }
    public struct CastlePoseChunk : INetworkStruct
    {
        public CastlePoseWords a,b,c,d,e,f;
        internal static CastlePoseChunk Pack(byte[] bytes,int at)=>new CastlePoseChunk{a=CastlePoseWords.Pack(bytes,at),b=CastlePoseWords.Pack(bytes,at+64),c=CastlePoseWords.Pack(bytes,at+128),d=CastlePoseWords.Pack(bytes,at+192),e=CastlePoseWords.Pack(bytes,at+256),f=CastlePoseWords.Pack(bytes,at+320)};
        internal byte[] Unpack(int length)
        {
            if(length<1||length>CastlePoseReassembly.ChunkBytes)throw new ArgumentOutOfRangeException(nameof(length));
            var bytes=new byte[length];a.CopyTo(bytes,0);b.CopyTo(bytes,64);c.CopyTo(bytes,128);d.CopyTo(bytes,192);e.CopyTo(bytes,256);f.CopyTo(bytes,320);return bytes;
        }
    }

    // Static RPCs are registered by Fusion's weaver; no scene NetworkObject is needed.
    public sealed class CastlePoseChannel : SimulationBehaviour
    {
        internal const int Game=1,Lobby=2,MaxActors=256;
        internal const string VersionSuffix="/pose-unreliable-1";
        internal static byte[] Encode(CastleLanProtocol.Actor actor)
        {
            if(actor==null||actor.id==null||actor.id.Length>256||actor.kind==null||actor.kind.Length>32||actor.state==null||actor.state.Length>CastlePoseReassembly.MaxBytes)return null;
            var data=CastleLanProtocol.Encode(CastleLanProtocol.Player,w=>CastleLanProtocol.Write(w,actor));
            return data.Length<=CastlePoseReassembly.MaxBytes?data:null;
        }
        internal static CastleLanProtocol.Actor Decode(byte[] data)
        {
            using var reader=CastleLanProtocol.Reader(data,CastleLanProtocol.Player);
            var actor=new CastleLanProtocol.Actor{id=CastleLanProtocol.Text(reader),kind=CastleLanProtocol.Text(reader,32),active=reader.ReadBoolean()};
            int length=CastleLanProtocol.Count(reader,CastlePoseReassembly.MaxBytes);
            if(length!=reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException("Invalid live actor length");
            actor.state=reader.ReadBytes(length);return actor;
        }
        public sealed class Stats
        {
            public long SubmittedFrames,SubmittedChunks,SubmittedPayloadBytes,SubmittedRpcPayloadBytes,ReceivedChunks,CompletedFrames,RejectedChunks;
            public float LastCompleteAt;
            public sealed class ActorSamples {public long Applied,Intervals;public float LastAt,LastInterval,MaxInterval;public double IntervalSeconds;}
            readonly Dictionary<string,ActorSamples> actors=new Dictionary<string,ActorSamples>();
            public IReadOnlyDictionary<string,ActorSamples> Actors=>actors;
            public long DroppedActorMetrics;
            internal void Applied(string id,float now)
            {
                if(!actors.TryGetValue(id,out var sample))
                {if(actors.Count>=MaxActors){DroppedActorMetrics++;return;}actors.Add(id,sample=new ActorSamples());}
                if(sample.Applied>0){sample.LastInterval=now-sample.LastAt;sample.MaxInterval=Math.Max(sample.MaxInterval,sample.LastInterval);sample.IntervalSeconds+=sample.LastInterval;sample.Intervals++;}
                sample.Applied++;sample.LastAt=now;
            }
        }
        internal sealed class Binding
        {
            internal readonly object owner;
            internal readonly int epoch;
            internal readonly Func<PlayerRef,int,bool> accepts;
            internal readonly Action<PlayerRef,int,byte[]> receive;
            internal readonly Dictionary<(PlayerRef,int),CastlePoseReassembly> incoming=new Dictionary<(PlayerRef,int),CastlePoseReassembly>();
            internal readonly Dictionary<PlayerRef,(float began,int count)> rate=new Dictionary<PlayerRef,(float,int)>();
            internal readonly Stats stats=new Stats();
            internal Binding(object owner,int epoch,Func<PlayerRef,int,bool> accepts,Action<PlayerRef,int,byte[]> receive)
            {this.owner=owner;this.epoch=epoch;this.accepts=accepts;this.receive=receive;}
        }
        static readonly Dictionary<NetworkRunner,Dictionary<int,Binding>> bindings=new Dictionary<NetworkRunner,Dictionary<int,Binding>>();
        internal static Binding Bind(NetworkRunner runner,int channel,int epoch,object owner,Func<PlayerRef,int,bool> accepts,Action<PlayerRef,int,byte[]> receive)
        {
            if(!bindings.TryGetValue(runner,out var channels))bindings.Add(runner,channels=new Dictionary<int,Binding>());
            if(channels.TryGetValue(channel,out var previous)&&!ReferenceEquals(previous.owner,owner))throw new InvalidOperationException("Pose channel already has an owner");
            var value=new Binding(owner,epoch,accepts,receive);channels[channel]=value;return value;
        }
        internal static void Unbind(NetworkRunner runner,int channel,object owner)
        {
            if(ReferenceEquals(runner,null)||!bindings.TryGetValue(runner,out var channels))return;
            if(channels.TryGetValue(channel,out var binding)&&ReferenceEquals(binding.owner,owner))channels.Remove(channel);
            if(channels.Count==0)bindings.Remove(runner);
        }
        internal static void Forget(Binding binding,PlayerRef source)
        {
            if(binding==null)return;
            var keys=new List<(PlayerRef,int)>();
            foreach(var key in binding.incoming.Keys)if(key.Item1==source)keys.Add(key);
            foreach(var key in keys)binding.incoming.Remove(key);
            binding.rate.Remove(source);
        }
        internal static void Send(NetworkRunner runner,int channel,Binding binding,PlayerRef target,int slot,int serial,byte[] payload)
        {
            if(binding==null||serial==0||slot<0||slot>=MaxActors||payload==null||payload.Length<1||payload.Length>CastlePoseReassembly.MaxBytes)return;
            binding.stats.SubmittedFrames++;binding.stats.SubmittedPayloadBytes+=payload.Length;
            for(int offset=0;offset<payload.Length;offset+=CastlePoseReassembly.ChunkBytes)
            {
                var part=CastlePoseChunk.Pack(payload,offset);
                var sent=RPC_PoseChunk(runner,target,channel,binding.epoch,slot,serial,payload.Length,offset/CastlePoseReassembly.ChunkBytes,part);
                binding.stats.SubmittedChunks++;binding.stats.SubmittedRpcPayloadBytes+=sent.PayloadSize;
            }
        }
        [Rpc(Channel=RpcChannel.Unreliable,TickAligned=false,InvokeLocal=false,HostMode=RpcHostMode.SourceIsServer)]
        public static RpcInvokeInfo RPC_PoseChunk(NetworkRunner runner,[RpcTarget] PlayerRef target,int channel,int epoch,int slot,int serial,int total,int index,CastlePoseChunk data,RpcInfo info=default)
        {
            if(!bindings.TryGetValue(runner,out var channels)||!channels.TryGetValue(channel,out var binding))return default;
            binding.stats.ReceivedChunks++;
            if(info.Channel!=RpcChannel.Unreliable||epoch!=binding.epoch||slot<0||slot>=MaxActors||!binding.accepts(info.Source,slot))
            {binding.stats.RejectedChunks++;return default;}
            float now=Time.unscaledTime;
            binding.rate.TryGetValue(info.Source,out var rate);
            rate=now-rate.began>=.1f?(now,1):(rate.began,rate.count+1);binding.rate[info.Source]=rate;
            if(rate.count>(runner.IsServer?48:512)){binding.stats.RejectedChunks++;return default;}
            // Validate before creating an assembler or allocating its bounded frame.
            if(serial==0||total<1||total>CastlePoseReassembly.MaxBytes||index<0||index>=(total+CastlePoseReassembly.ChunkBytes-1)/CastlePoseReassembly.ChunkBytes)
            {binding.stats.RejectedChunks++;return default;}
            var key=(info.Source,slot);
            if(!binding.incoming.TryGetValue(key,out var assembly))binding.incoming.Add(key,assembly=new CastlePoseReassembly());
            if(!assembly.Push(serial,total,index,data.Unpack(Math.Min(CastlePoseReassembly.ChunkBytes,total-index*CastlePoseReassembly.ChunkBytes)),now,out var payload)){binding.stats.RejectedChunks++;return default;}
            if(payload!=null)
            {
                binding.stats.CompletedFrames++;binding.stats.LastCompleteAt=Time.unscaledTime;
                binding.receive(info.Source,slot,payload);
            }
            return default;
        }
    }
}
