using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CastlePrototype
{
    // Reuses the castle's validated snapshot format and physical remote-hand views.
    // World transfers are acknowledged. Live poses are superseded by newer samples.
    public sealed partial class CastleFusionSession : MonoBehaviour
    {
        const int Channel = 0x43415354, SocialChannel=0x534F434C;
        readonly Dictionary<PlayerRef,CastleSocialChannel> socialPeers=new Dictionary<PlayerRef,CastleSocialChannel>();
        readonly CastleSocialChannel socialServer=new CastleSocialChannel();
        const int Ready = 1, Welcome = 2, World = 3, Ack = 5, WelcomeAck = 6, Rules=7, Command=8, Admin=9;
        const int LivePose = 20, LivePoses = 21, PoseReceiptGroup = 23, WorldResync=24, HotpathBundle=25;
        const int PoseWindow = 6, MaxPoseSize = 4096, MaxLiveBatchSize=64*1024;
        internal static class LivePoseCodec
        {
            internal const int HeaderBytes=17,ReceiptCapacity=PoseWindow;
            const uint Magic=0x3250534c;
            static readonly uint[] CrcTable=BuildCrcTable();
            static uint[] BuildCrcTable()
            {
                var table=new uint[256];
                for(int i=0;i<table.Length;i++){uint value=(uint)i;for(int bit=0;bit<8;bit++)value=(value>>1)^((value&1)!=0?0xedb88320u:0);table[i]=value;}
                return table;
            }
            static uint Crc(byte[] data)=>Crc(data,Array.Empty<int>(),0);
            static uint Crc(byte[] data,int[] receipts,int count)
            {
                uint crc=uint.MaxValue;foreach(byte value in data)crc=(crc>>8)^CrcTable[(crc^value)&255];
                for(int i=0;i<count;i++)for(int shift=0;shift<32;shift+=8){byte value=(byte)((uint)receipts[i]>>shift);crc=(crc>>8)^CrcTable[(crc^value)&255];}return ~crc;
            }
            internal static byte[] Pack(byte[] raw,int maximum)=>Pack(raw,maximum,Array.Empty<int>(),out _);
            internal static byte[] Pack(byte[] raw,int maximum,int[] receipts,out int packedReceipts)
            {
                if(raw==null||raw.Length<1||raw.Length>maximum)throw new System.IO.InvalidDataException("Live pose size is invalid");
                if(receipts==null||receipts.Length>ReceiptCapacity)throw new System.IO.InvalidDataException("Live pose receipt count invalid");
                var used=new HashSet<int>();foreach(int value in receipts)if(value==0||!used.Add(value))throw new System.IO.InvalidDataException("Live pose receipt serial invalid");
                byte[] body;
                using(var zipped=new System.IO.MemoryStream())
                {using(var zip=new System.IO.Compression.GZipStream(zipped,System.IO.Compression.CompressionLevel.Fastest,true))zip.Write(raw,0,raw.Length);body=zipped.ToArray();}
                bool compressed=body.Length<raw.Length;if(!compressed)body=raw;
                packedReceipts=Math.Min(receipts.Length,(maximum-body.Length)/4);
                using var output=new System.IO.MemoryStream();using var writer=new System.IO.BinaryWriter(output);
                writer.Write(Magic);writer.Write((byte)((compressed?1:0)|(packedReceipts<<1)));writer.Write(raw.Length);writer.Write(body.Length);writer.Write(Crc(raw,receipts,packedReceipts));
                for(int i=0;i<packedReceipts;i++)writer.Write(receipts[i]);writer.Write(body);return output.ToArray();
            }
            internal static byte[] Unpack(byte[] packet,int maximum)=>Unpack(packet,maximum,out _);
            internal static byte[] Unpack(byte[] packet,int maximum,out int[] receipts)
            {
                receipts=Array.Empty<int>();
                if(packet==null||packet.Length<HeaderBytes||packet.Length>maximum+HeaderBytes)throw new System.IO.InvalidDataException("Live pose envelope size is invalid");
                using var input=new System.IO.MemoryStream(packet,false);using var reader=new System.IO.BinaryReader(input);
                if(reader.ReadUInt32()!=Magic)throw new System.IO.InvalidDataException("Live pose protocol differs");
                byte flags=reader.ReadByte();int mode=flags&1,count=flags>>1,length=reader.ReadInt32(),stored=reader.ReadInt32();uint crc=reader.ReadUInt32();
                int at=HeaderBytes+count*4;
                if(count>ReceiptCapacity||length<1||length>maximum||stored<1||stored!=packet.Length-at||stored>length||mode==0&&stored!=length||mode==1&&stored>=length)
                    throw new System.IO.InvalidDataException("Live pose declared lengths are invalid");
                var received=new int[count];var used=new HashSet<int>();
                for(int i=0;i<count;i++){int value=reader.ReadInt32();if(value==0||!used.Add(value))throw new System.IO.InvalidDataException("Live pose receipt serial invalid");received[i]=value;}
                var raw=new byte[length];
                if(mode==0)System.Buffer.BlockCopy(packet,at,raw,0,length);
                else
                {
                    int end=packet.Length;
                    if(stored<18||packet[at]!=31||packet[at+1]!=139||packet[at+2]!=8||packet[at+3]!=0||
                        BitConverter.ToUInt32(packet,end-4)!=(uint)length)
                        throw new System.IO.InvalidDataException("Live pose gzip framing is invalid");
                    using var compressed=new System.IO.MemoryStream(packet,at,stored,false);
                    using var zip=new System.IO.Compression.GZipStream(compressed,System.IO.Compression.CompressionMode.Decompress);
                    int expanded=0,read;
                    while(expanded<length&&(read=zip.Read(raw,expanded,length-expanded))>0)expanded+=read;
                    if(expanded!=length||zip.ReadByte()!=-1)throw new System.IO.InvalidDataException("Live pose expansion differs from its bound");
                }
                if(mode==1&&BitConverter.ToUInt32(packet,packet.Length-8)!=Crc(raw)||Crc(raw,received,count)!=crc)throw new System.IO.InvalidDataException("Live pose checksum differs");receipts=received;return raw;
            }
        }
        internal sealed class LivePoseReceipts
        {
            internal const int Capacity=64;
            readonly HashSet<int> serials=new HashSet<int>(),claimed=new HashSet<int>();
            internal int Count=>serials.Count;
            internal void Add(int serial)
            {if(serial==0)throw new System.IO.InvalidDataException("Zero pose receipt");if(serials.Contains(serial))return;if(serials.Count>=Capacity)throw new System.IO.InvalidDataException("Pose receipts exceed their bound");serials.Add(serial);}
            internal static byte[] Encode(int[] values)
            {
                if(values==null||values.Length<1||values.Length>Capacity)throw new System.IO.InvalidDataException("Pose receipt count invalid");
                using var output=new System.IO.MemoryStream();using var writer=new System.IO.BinaryWriter(output);writer.Write((byte)167);writer.Write((byte)1);writer.Write((byte)values.Length);
                var used=new HashSet<int>();foreach(int value in values){if(value==0||!used.Add(value))throw new System.IO.InvalidDataException("Pose receipt serial invalid");writer.Write(value);}return output.ToArray();
            }
            internal static int[] Decode(byte[] packet)
            {
                if(packet==null||packet.Length<7||packet.Length>3+Capacity*4||packet[0]!=167||packet[1]!=1||packet[2]<1||packet[2]>Capacity||packet.Length!=3+packet[2]*4)throw new System.IO.InvalidDataException("Pose receipt group invalid");
                var result=new int[packet[2]];var used=new HashSet<int>();
                for(int i=0;i<result.Length;i++){int value=BitConverter.ToInt32(packet,3+i*4);if(value==0||!used.Add(value))throw new System.IO.InvalidDataException("Pose receipt serial invalid");result[i]=value;}return result;
            }
            internal int[] Snapshot()=>serials.Where(value=>!claimed.Contains(value)).Take(LivePoseCodec.ReceiptCapacity).ToArray();
            internal int[] Remaining()=>serials.Where(value=>!claimed.Contains(value)).ToArray();
            internal void Claim(int[] values,int count)
            {for(int i=0;i<count;i++)if(!serials.Contains(values[i])||!claimed.Add(values[i]))throw new InvalidOperationException("Receipt already claimed");}
            internal void Release(int[] values,int count){for(int i=0;i<count;i++)claimed.Remove(values[i]);}
            internal void Sent(int[] snapshot,int count)
            {if(snapshot==null||count<0||count>snapshot.Length)throw new ArgumentOutOfRangeException(nameof(count));for(int i=0;i<count;i++){serials.Remove(snapshot[i]);claimed.Remove(snapshot[i]);}}
            internal int Flush(Action<byte[]> send)
            {
                if(serials.Count==0)return 0;var values=serials.ToArray();var packet=Encode(values);send(packet);
                foreach(int value in values)serials.Remove(value);return values.Length;
            }
            internal void Clear(){serials.Clear();claimed.Clear();}
        }
        public sealed class LivePoseTraffic
        {
            public long RawSubmittedBytes{get;internal set;}public long RawReceivedBytes{get;internal set;}public long CompressedSubmittedFrames{get;internal set;}
            public long PiggybackFramesSubmitted{get;internal set;}public long PiggybackSerialsSubmitted{get;internal set;}public long PiggybackFramesReceived{get;internal set;}public long PiggybackSerialsReceived{get;internal set;}
            public long ReceiptGroupsSubmitted{get;internal set;}public long ReceiptSerialsSubmitted{get;internal set;}public long ReceiptGroupsReceived{get;internal set;}public long ReceiptSerialsReceived{get;internal set;}
            public int PendingReceiptCount{get;internal set;}
            public string PeerId{get;internal set;}="";
            public int CountLimit{get;internal set;}public int ByteLimit{get;internal set;}public int PayloadLimit{get;internal set;}
            public int PendingCount{get;internal set;}public int PendingBytes{get;internal set;}public int PeakCount{get;internal set;}public int PeakBytes{get;internal set;}
            public long DueTicks{get;internal set;}public long SuppressedTicks{get;internal set;}public long ByteBlocked{get;internal set;}public long InvalidFrames{get;internal set;}
            public long SubmittedFrames{get;internal set;}public long SubmittedBytes{get;internal set;}public int MaxSubmittedBytes{get;internal set;}public long SendFailures{get;internal set;}
            public long MatchedAcks{get;internal set;}public long UnknownAcks{get;internal set;}public float LastAckSeconds{get;internal set;}public float MaxAckSeconds{get;internal set;}public double AckSeconds{get;internal set;}
            public float OldestPendingSeconds{get;internal set;}public float MaxOldestPendingSeconds{get;internal set;}public long Resets{get;internal set;}
            public long ReceivedFrames{get;internal set;}public long ReceivedBytes{get;internal set;}public long SkippedFrames{get;internal set;}public long AppliedFrames{get;internal set;}public long AppliedBytes{get;internal set;}public long AppliedIntervals{get;internal set;}
            public float LastReceivedAt{get;internal set;}public float LastReceiveInterval{get;internal set;}public float MaxReceiveInterval{get;internal set;}public double ReceiveIntervalSeconds{get;internal set;}public long ReceiveIntervals{get;internal set;}
            public float LastAppliedAt{get;internal set;}public float LastApplyInterval{get;internal set;}public float MaxApplyInterval{get;internal set;}public double ApplyIntervalSeconds{get;internal set;}
        }
        internal sealed class LivePoseWindow
        {
            struct Pending {internal int bytes;internal float at;}
            readonly Dictionary<int,Pending> pending=new Dictionary<int,Pending>();
            bool hasApplied,hasReceived;
            public LivePoseTraffic Traffic{get;}=new LivePoseTraffic();
            internal LivePoseWindow(int countLimit,int byteLimit,int payloadLimit)
            {
                if(countLimit<1||byteLimit<1||payloadLimit<1||payloadLimit>byteLimit)throw new ArgumentOutOfRangeException();
                Traffic.CountLimit=countLimit;Traffic.ByteLimit=byteLimit;Traffic.PayloadLimit=payloadLimit;
            }
            internal bool CanCapture()
            {Traffic.DueTicks++;if(pending.Count<Traffic.CountLimit&&Traffic.PendingBytes<Traffic.ByteLimit)return true;Traffic.SuppressedTicks++;return false;}
            internal bool Reserve(int serial,int bytes,float now)
            {
                if(serial==0||bytes<1||bytes>Traffic.PayloadLimit||float.IsNaN(now)||float.IsInfinity(now)||pending.ContainsKey(serial)){Traffic.InvalidFrames++;return false;}
                if(pending.Count>=Traffic.CountLimit){Traffic.SuppressedTicks++;return false;}
                if(bytes>Traffic.ByteLimit-Traffic.PendingBytes){Traffic.ByteBlocked++;return false;}
                pending.Add(serial,new Pending{bytes=bytes,at=now});Traffic.PendingBytes+=bytes;Traffic.PendingCount=pending.Count;
                Traffic.PeakCount=Math.Max(Traffic.PeakCount,pending.Count);Traffic.PeakBytes=Math.Max(Traffic.PeakBytes,Traffic.PendingBytes);return true;
            }
            internal void SubmittedAt(int serial,float now){if(pending.TryGetValue(serial,out var value)){value.at=now;pending[serial]=value;}}
            internal void Submitted(int bytes){Traffic.SubmittedFrames++;Traffic.SubmittedBytes+=bytes;Traffic.MaxSubmittedBytes=Math.Max(Traffic.MaxSubmittedBytes,bytes);}
            internal bool Ack(int serial,float now)
            {
                if(!pending.TryGetValue(serial,out var value)){Traffic.UnknownAcks++;return false;}
                pending.Remove(serial);Traffic.PendingBytes-=value.bytes;Traffic.PendingCount=pending.Count;Traffic.MatchedAcks++;
                float elapsed=Math.Max(0,now-value.at);Traffic.LastAckSeconds=elapsed;Traffic.MaxAckSeconds=Math.Max(Traffic.MaxAckSeconds,elapsed);Traffic.AckSeconds+=elapsed;Observe(now);return true;
            }
            internal void Cancel(int serial)
            {if(pending.TryGetValue(serial,out var value)){pending.Remove(serial);Traffic.PendingBytes-=value.bytes;Traffic.PendingCount=pending.Count;Traffic.SendFailures++;}}
            internal void Observe(float now)
            {float age=0;foreach(var value in pending.Values)age=Math.Max(age,now-value.at);Traffic.OldestPendingSeconds=age;Traffic.MaxOldestPendingSeconds=Math.Max(Traffic.MaxOldestPendingSeconds,age);}
            internal bool Expired(float now){Observe(now);return Traffic.OldestPendingSeconds>20;}
            internal void Clear(){pending.Clear();Traffic.PendingCount=Traffic.PendingBytes=0;Traffic.OldestPendingSeconds=0;hasApplied=hasReceived=false;Traffic.Resets++;}
            internal void Received(int bytes,float now)
            {if(hasReceived){float gap=Math.Max(0,now-Traffic.LastReceivedAt);Traffic.LastReceiveInterval=gap;Traffic.MaxReceiveInterval=Math.Max(Traffic.MaxReceiveInterval,gap);Traffic.ReceiveIntervalSeconds+=gap;Traffic.ReceiveIntervals++;}hasReceived=true;Traffic.LastReceivedAt=now;Traffic.ReceivedFrames++;Traffic.ReceivedBytes+=bytes;}
            internal void Skipped(){Traffic.SkippedFrames++;}
            internal void Applied(int bytes,float now)
            {if(hasApplied){float gap=Math.Max(0,now-Traffic.LastAppliedAt);Traffic.LastApplyInterval=gap;Traffic.MaxApplyInterval=Math.Max(Traffic.MaxApplyInterval,gap);Traffic.ApplyIntervalSeconds+=gap;Traffic.AppliedIntervals++;}hasApplied=true;Traffic.LastAppliedAt=now;Traffic.AppliedFrames++;Traffic.AppliedBytes+=bytes;}
            internal static bool Newer(int value,int previous,bool seen)=>value!=0&&(!seen||unchecked((int)((uint)value-(uint)previous))>0);
            internal static int NextSequence(ref int sequence){sequence=unchecked(sequence+1);if(sequence==0)sequence=1;return sequence;}
        }

        sealed class Peer { public readonly WorldDeltaCodec.Sender world=new WorldDeltaCodec.Sender();public int worldResyncs;public float nextWorldResync;public byte[] pendingWorldResync; public readonly LivePoseWindow posesInFlight = new LivePoseWindow(PoseWindow,384*1024,MaxLiveBatchSize+LivePoseCodec.HeaderBytes);public readonly LivePoseReceipts receipts=new LivePoseReceipts(); public int lastPoseSequence,lastNativeSequence,motionLane;public bool hasPoseSequence,hasNativeSequence;public float lastNativePose,nextWorldSend; public bool ready; public string rules; public byte[] welcome; public int waiting; public float lastPose, sentAt, lastCommand; public string admin; public byte[] disasters; public byte[] match; public float lastModeCommand; public int mysteryRound=-1,trailCount; }
        readonly Dictionary<PlayerRef, Peer> peers = new Dictionary<PlayerRef, Peer>();
        readonly List<KeyValuePair<PlayerRef, Peer>> worldSendPeers = new List<KeyValuePair<PlayerRef, Peer>>(10);
        readonly List<KeyValuePair<PlayerRef, Peer>> worldSendTargets = new List<KeyValuePair<PlayerRef, Peer>>(10);
        int worldSendCursor;
        WorldSendWork worldWork;
        sealed class WorldSendWork
        {
            public int Revision;
            public string Context;
            public KeyValuePair<PlayerRef, Peer>[] Targets;
            public Task<WorldDeltaCodec.Prepared> Result;
        }
        NetworkRunner runner;
        NetworkDelegates callbacks;
        CastleLanSession castle;
        CastlePlaySession session;
        int sequence, poseSequence, lastPoseBatch, sceneTag;
        // The same runner survives map reloads, so old reliable packets can reach a new session.
        // Zero is reserved for legacy senders and the standalone Fusion prototype.
        static int SceneTag(NetworkRunner source)
        {
            var room=IslandRoomConnection.Instance;
            return source&&room&&room.Owns(source)?source.SceneInfo.Version+1:0;
        }
        bool IsCurrentScene=>sceneTag==SceneTag(runner)&&WorldConnectionCurrent;
        readonly WorldDeltaCodec.Receiver worldReceiver=new WorldDeltaCodec.Receiver();
        IslandRoomConnection worldRoom;bool worldHadRoom;int worldConnection;string worldScope;
        float nextWorldResync;
        byte[] pendingWorldResync;int pendingWorldResyncSerial;
        public long WorldDeltaTransfersSubmitted{get;private set;}
        public long WorldFullTransfersSubmitted{get;private set;}
        public long WorldRawBytesSubmitted{get;private set;}
        public long WorldFullAlternativeBytesSubmitted{get;private set;}
        void BindWorldConnection()
        {var room=IslandRoomConnection.Instance;worldRoom=room&&room.Owns(runner)?room:null;worldHadRoom=worldRoom;worldConnection=worldRoom?worldRoom.ConnectionGeneration:0;}
        bool WorldConnectionCurrent=>worldHadRoom?
            worldRoom&&ReferenceEquals(worldRoom,IslandRoomConnection.Instance)&&worldRoom.Owns(runner)&&worldRoom.ConnectionGeneration==worldConnection:
            !IslandRoomConnection.Instance||!IslandRoomConnection.Instance.Owns(runner);
        void ResetWorldTransfers()
        {worldWork=null;worldReceiver.Reset();worldScope=null;nextWorldResync=0;pendingWorldResync=null;pendingWorldResyncSerial=0;foreach(var peer in peers.Values){peer.world.Reset();peer.waiting=0;peer.nextWorldSend=0;peer.worldResyncs=0;peer.nextWorldResync=0;peer.pendingWorldResync=null;}}
        void PumpWorldResync(float now)
        {
            if(runner.IsServer)
            {
                foreach(var pair in peers.ToArray())
                {
                    var peer=pair.Value;var token=peer.pendingWorldResync;if(token==null||now<peer.nextWorldResync)continue;
                    peer.pendingWorldResync=null;
                    if(!peer.world.Resynchronize(peer.waiting,token))continue;
                    if(++peer.worldResyncs>3){runner.Disconnect(pair.Key);RemovePlayer(pair.Key);continue;}
                    peer.waiting=0;peer.nextWorldResync=now+1;
                }
            }
            else if(pendingWorldResync!=null&&now>=nextWorldResync)
            {
                Send(default,WorldResync,pendingWorldResync,pendingWorldResyncSerial);
                pendingWorldResync=null;pendingWorldResyncSerial=0;nextWorldResync=now+1;
            }
        }
        void InvalidateWorldSenders()
        {worldWork=null;foreach(var peer in peers.Values)peer.world.Invalidate();worldScope=null;}
        void ObserveWorldScope()
        {
            var synced=DeadlyJenga.Core.Services.World as DeadlyJenga.Core.ISyncedWorld;
            string scope=(synced?.Root?synced.Root.GetInstanceID():0)+"/"+(synced?.Revision??-1)+"/"+
                string.Join(",",runner.ActivePlayers.Select(p=>p.ToString()).OrderBy(p=>p,StringComparer.Ordinal));
            if(scope==worldScope)return;
            worldWork=null;foreach(var peer in peers.Values)peer.world.Invalidate();worldScope=scope;
        }
        static string WorldContext(CastleLanProtocol.Snapshot frame)
        {
            var identity=new CastleLanProtocol.Snapshot{revision=frame.revision,settings=frame.settings,origin=frame.origin,originScale=frame.originScale,
                blocks=Array.Empty<CastleLanProtocol.Pose>(),active=Array.Empty<bool>(),actors=frame.actors.OrderBy(a=>a.id,StringComparer.Ordinal)
                    .Select(a=>new CastleLanProtocol.Actor{id=a.id,kind=a.kind,active=a.active,state=Array.Empty<byte>()}).ToArray()};
            using var hash=System.Security.Cryptography.SHA256.Create();return Convert.ToBase64String(hash.ComputeHash(CastleLanProtocol.EncodeWorld(identity)));
        }
        void SendWorld(PlayerRef player,Peer peer,WorldDeltaCodec.Prepared capture,string context)
        {
            int serial=LivePoseWindow.NextSequence(ref sequence);long encodeStarted=FusionMotionWireTrace.Now;var packet=peer.world.Prepare(capture,context,serial);
            FusionMotionWireTrace.Record(FusionMotionWireTrace.Phase.Encode,World,serial,packet.Length,encodeStarted);
            int fullAlternativeBytes=peer.world.LastFullPacketBytes;
            peer.waiting=serial; // Admission reserves the one world slot; timeout starts after SDK return.
            try
            {
                Send(player,World,packet,serial,()=>
                {
                    peer.sentAt=Time.unscaledTime;WorldRawBytesSubmitted+=capture.Raw.Length;WorldFullAlternativeBytesSubmitted+=fullAlternativeBytes;
                    if(packet[5]==1)WorldDeltaTransfersSubmitted++;else WorldFullTransfersSubmitted++;
                },()=>{peer.world.Cancel();if(peer.waiting==serial)peer.waiting=0;});
            }
            catch{peer.world.Cancel();peer.waiting=0;throw;}
        }
        void QueueWorldSend(float now)
        {
            if (worldWork != null) return;
            worldSendPeers.Clear();
            worldSendPeers.AddRange(peers);
            worldSendTargets.Clear();
            int count = worldSendPeers.Count;
            int budget = UsesNativeMotion ? 2 : count;
            for (int inspected = 0; inspected < count && worldSendTargets.Count < budget; inspected++)
            {
                int index = worldSendCursor % count;
                worldSendCursor = (index + 1) % count;
                var pair = worldSendPeers[index];
                var peer = pair.Value;
                if (peer.ready && peer.waiting == 0 && (!UsesNativeMotion || now >= peer.nextWorldSend))
                    worldSendTargets.Add(pair);
            }
            if (worldSendTargets.Count == 0) return;

            long captureStarted = FusionMotionWireTrace.Now;
            var snapshot = castle.FusionCaptureWorldSnapshot(UsesNativeMotion ? runner.Tick.Raw : (int?)null);
            var raw = CastleLanProtocol.EncodeWorld(snapshot);
            FusionMotionWireTrace.Record(FusionMotionWireTrace.Phase.Capture,World,0,raw.Length,captureStarted);
            long contextStarted = FusionMotionWireTrace.Now;
            string context = WorldContext(snapshot);
            FusionMotionWireTrace.Record(FusionMotionWireTrace.Phase.Context,World,0,0,contextStarted);
            if (!UsesNativeMotion)
            {
                SubmitWorld(new WorldDeltaCodec.Prepared(raw), context, worldSendTargets, now);
                return;
            }

            byte[] first = worldSendTargets[0].Value.world.AnchorFor(context);
            byte[] second = worldSendTargets.Count == 2 ? worldSendTargets[1].Value.world.AnchorFor(context) : null;
            worldWork = new WorldSendWork
            {
                Revision = snapshot.revision,
                Context = context,
                Targets = worldSendTargets.ToArray(),
                // The worker receives only immutable byte arrays. It never touches
                // the runner, peer state, or a Unity object.
                Result = Task.Run(() =>
                {
                    var prepared = new WorldDeltaCodec.Prepared(raw);
                    if (raw.Length <= WorldDeltaCodec.CacheBytes)
                    {
                        if (first != null) prepared.Delta(first);
                        if (second != null) prepared.Delta(second);
                    }
                    return prepared;
                })
            };
        }

        void SubmitPreparedWorld(float now)
        {
            var work = worldWork;
            if (work == null || !work.Result.IsCompleted) return;
            worldWork = null;
            if (DeadlyJenga.Core.Services.World is not DeadlyJenga.Core.ISyncedWorld synced ||
                synced.Revision != work.Revision) return;
            SubmitWorld(work.Result.GetAwaiter().GetResult(), work.Context, work.Targets, now);
        }

        void SubmitWorld(WorldDeltaCodec.Prepared capture, string context,
            IReadOnlyList<KeyValuePair<PlayerRef, Peer>> targets, float now)
        {
            string admin = null, rules = null;
            byte[] adminBytes = null;
            foreach (var pair in targets)
            {
                var peer = pair.Value;
                if (!peers.TryGetValue(pair.Key, out var current) || !ReferenceEquals(peer, current) ||
                    !peer.ready || peer.waiting != 0) continue;
                if(matchPacket!=null&&!ReferenceEquals(peer.match,matchPacket)){var packet=matchPacket;Send(pair.Key,MatchState,packet,0,()=>peer.match=packet);}
                if(disasterVisualPacket!=null&&!ReferenceEquals(peer.disasters,disasterVisualPacket)){var packet=disasterVisualPacket;Send(pair.Key,DisasterVisuals,packet,0,()=>peer.disasters=packet);}
                admin ??= JsonUtility.ToJson(Demo);
                if(peer.admin!=admin){adminBytes??=System.Text.Encoding.UTF8.GetBytes(admin);Send(pair.Key,Admin,adminBytes,0,()=>peer.admin=admin);}
                rules ??= MatchSetup.CaptureRules(session.Player);
                if(peer.rules!=rules){Send(pair.Key,Rules,System.Text.Encoding.UTF8.GetBytes(rules),0,()=>peer.rules=rules);}
                SendWorld(pair.Key, peer, capture, context);
                peer.nextWorldSend = now + .25f;
            }
        }

        void ReceiveWorld(byte[] packet,int serial)
        {
            long decodeStarted=FusionMotionWireTrace.Now;var frame=worldReceiver.Decode(packet,serial);
            FusionMotionWireTrace.Record(FusionMotionWireTrace.Phase.Decode,World,serial,packet.Length,decodeStarted);
            if(frame.status==WorldDeltaCodec.Status.Stale)return;
            if(frame.status==WorldDeltaCodec.Status.MissingBase)
            {pendingWorldResync=frame.Token();pendingWorldResyncSerial=serial;return;}
            if(frame.status==WorldDeltaCodec.Status.Ready)
            {
                long applyStarted=FusionMotionWireTrace.Now;castle.FusionApplyWorld(frame.raw);
                FusionMotionWireTrace.Record(FusionMotionWireTrace.Phase.Applied,World,serial,frame.raw.Length,applyStarted);
                worldReceiver.Applied(frame);lastWorld=Time.unscaledTime;pendingWorldResync=null;pendingWorldResyncSerial=0;
            }
            Send(default,Ack,frame.Token(),serial);
        }

        float nextPoseSend;
        readonly LivePoseWindow posesInFlight = new LivePoseWindow(PoseWindow,24*1024,MaxPoseSize+LivePoseCodec.HeaderBytes);
        bool hasPoseBatch;
        readonly LivePoseReceipts poseReceipts=new LivePoseReceipts();
        float nextReceiptFlush;
        void FlushPoseReceipts(float now)
        {
            if(now<nextReceiptFlush)return;nextReceiptFlush=now+.1f;
            try
            {
                if(runner.IsServer)
                {
                    foreach(var pair in peers.ToArray())
                    {SendReceiptGroup(pair.Key,pair.Value.receipts,pair.Value.posesInFlight);}
                }
                else SendReceiptGroup(default,poseReceipts,posesInFlight);
            }
            catch(Exception){Fail("Pose receipt send failed. Rejoin with the same game build.");poseSendFailed=true;}
        }
        void SendReceiptGroup(PlayerRef player,LivePoseReceipts receipts,LivePoseWindow window)
        {
            var values=receipts.Remaining();if(values.Length==0)return;
            var packet=LivePoseReceipts.Encode(values);receipts.Claim(values,values.Length);
            try
            {
                Send(player,PoseReceiptGroup,packet,0,()=>
                {
                    receipts.Sent(values,values.Length);window.Traffic.ReceiptGroupsSubmitted++;
                    window.Traffic.ReceiptSerialsSubmitted+=values.Length;window.Traffic.PendingReceiptCount=receipts.Count;
                },()=>receipts.Release(values,values.Length));
            }
            catch{receipts.Release(values,values.Length);throw;}
        }
        static void ApplyPoseReceipts(LivePoseWindow window,byte[] packet,float now)
        {var values=LivePoseReceipts.Decode(packet);window.Traffic.ReceiptGroupsReceived++;window.Traffic.ReceiptSerialsReceived+=values.Length;foreach(int value in values)window.Ack(value,now);}
        static void ApplyPiggybackReceipts(LivePoseWindow window,int[] values,float now)
        {if(values.Length==0)return;window.Traffic.PiggybackFramesReceived++;window.Traffic.PiggybackSerialsReceived+=values.Length;foreach(int value in values)window.Ack(value,now);}
        public LivePoseTraffic LocalPoseTraffic=>posesInFlight.Traffic;
        public LivePoseTraffic[] PeerPoseTraffic=>peers.Values.Select(peer=>peer.posesInFlight.Traffic).ToArray();
        public long WorldTransfersSubmitted{get;private set;}
        public long WorldPayloadBytesSubmitted{get;private set;}
        void ResetPoseWindows()
        {CancelHotpathFrames();ResetWorldTransfers();posesInFlight.Clear();poseReceipts.Clear();posesInFlight.Traffic.PendingReceiptCount=0;nextReceiptFlush=0;foreach(var peer in peers.Values){peer.posesInFlight.Clear();peer.receipts.Clear();peer.posesInFlight.Traffic.PendingReceiptCount=0;}hasPoseBatch=false;lastPoseBatch=0;nextPoseSend=0;poseSendFailed=false;}
        bool SendPose(PlayerRef target,int kind,LivePoseWindow window,LivePoseReceipts receipts,byte[] packet,float now)
        {
            if(outboundFailed||closing||poseSendFailed||runner==null||!runner.IsRunning||!IsCurrentScene)return false;
            int serial=LivePoseWindow.NextSequence(ref poseSequence);
            int rawLength=packet?.Length??0;
            if(packet==null||packet.Length<1||packet.Length>(kind==LivePose?MaxPoseSize:MaxLiveBatchSize))
            {window.Reserve(serial,packet?.Length??0,now);Fail("Live pose payload exceeds the supported bound. Rejoin with the same game build.");poseSendFailed=true;return false;}
            var snapshot=receipts.Snapshot();int packedReceipts;
            try{packet=LivePoseCodec.Pack(packet,kind==LivePose?MaxPoseSize:MaxLiveBatchSize,snapshot,out packedReceipts);}
            catch(Exception){Fail("Live pose encoding failed. Rejoin with the same game build.");poseSendFailed=true;return false;}
            if(!window.Reserve(serial,packet.Length,now))return false;
            try
            {
                receipts.Claim(snapshot,packedReceipts);var submittedPacket=packet;
                Send(target,kind,submittedPacket,serial,()=>
                {
                    window.SubmittedAt(serial,Time.unscaledTime);window.Submitted(submittedPacket.Length);window.Traffic.RawSubmittedBytes+=rawLength;
                    if((submittedPacket[4]&1)!=0)window.Traffic.CompressedSubmittedFrames++;
                    receipts.Sent(snapshot,packedReceipts);window.Traffic.PendingReceiptCount=receipts.Count;
                    if(packedReceipts>0){window.Traffic.PiggybackFramesSubmitted++;window.Traffic.PiggybackSerialsSubmitted+=packedReceipts;}
                },()=>{window.Cancel(serial);receipts.Release(snapshot,packedReceipts);});
                return true;
            }
            catch(Exception){window.Cancel(serial);receipts.Release(snapshot,packedReceipts);Fail("Live pose send failed. Rejoin with the same game build.");poseSendFailed=true;return false;}
        }

        void PumpPoses(float now)
        {
            if (UsesNativeMotion && (runner.IsServer || !inputFallback)) return;
            posesInFlight.Observe(now);foreach(var peer in peers.Values)peer.posesInFlight.Observe(now);
            if(now<nextPoseSend)return;nextPoseSend=now+1f/30;
            if(!runner.IsServer)
            {
                if(welcomed&&!castle.WaitingForWorld&&posesInFlight.CanCapture())SendPose(default,LivePose,posesInFlight,poseReceipts,castle.FusionCapturePlayer(),now);
                return;
            }
            CastleLanProtocol.Actor[] actors=null;
            foreach(var pair in peers)
            {
                if(!pair.Value.ready||!pair.Value.posesInFlight.CanCapture())continue;
                actors??=castle.FusionCaptureLiveActors();
                var batch=CastleLanSession.FusionEncodeLivePosesFor(actors,pair.Key.ToString());
                if(!SendPose(pair.Key,LivePoses,pair.Value.posesInFlight,pair.Value.receipts,batch,now)&&poseSendFailed)return;
            }
        }
        bool welcomed, closing, leavingIntentionally,poseSendFailed;
        float nextSend, lastWorld;

        [Serializable] public sealed class DemoState
        {public DeadlyJenga.Core.WaveFrame wave;public bool shared,autoFill;public int survivors,hands,capacity,target,survivorPreset,handPreset,handLimit;public string survivorName,handName,leader,roomNotice;}
        public DemoState Demo {get;private set;}=new DemoState();
        public bool CanUseDemoControls=>SessionDevelopmentPolicy.DemoEnabled(session)&&runner&&runner.IsRunning&&(runner.IsServer?(!IslandRoomConnection.Instance||IslandRoomConnection.Instance.LocalIsLobbyHost||IslandRoomConnection.Instance.SharedDemoControls):(Demo.shared||Demo.leader==runner.LocalPlayer.ToString()));
        public bool IsHost=>runner&&runner.IsRunning&&runner.IsServer;
        public void SetSharedControls(bool value){var room=IslandRoomConnection.Instance;if(IsHost&&room)room.SetSharedControls(value);}
        CastleDemoCommand pendingStamina;float nextStamina;string authoritativeRules;
        public void Request(CastleDemoCommand command)
        {
            if(command==null||!SessionDevelopmentPolicy.CanRequestLocal(session))return;
            if(IsHost)CastleDemoCommands.Execute(session,command);
            else if(command.action==CastleDemoAction.Stamina){pendingStamina=command;if(authoritativeRules!=null)MatchSetup.ApplyRules(session.Player,authoritativeRules);}
            else Send(default,Command,System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(command)));
        }
        public void PauseForMigration(){UnbindMotion(false);ResetPoseWindows();ResetDisasters();closing=true;pendingStamina=null;if(runner&&callbacks!=null)runner.RemoveCallbacks(callbacks);castle.FusionFreeze();}
        public void ResumeAfterMigration(NetworkRunner next)
        {
            ResetPoseWindows();peers.Clear();socialPeers.Clear();socialServer.Clear();welcomed=false;closing=false;nextSend=0;
            runner=next;posesInFlight.Traffic.PeerId=next.LocalPlayer.ToString();sceneTag=SceneTag(next);BindWorldConnection();if(next.IsServer&&session.SurvivorBots!=null){if(Demo.target>0)session.SurvivorBots.TargetPopulation=Mathf.Max(1,Demo.target);session.SurvivorBots.AutoFill=Demo.autoFill;}castle.FusionRebind(next.LocalPlayer.ToString(),!next.IsServer,Demo.survivors,Demo.hands,Demo.survivorPreset,Demo.handPreset);
            if(next.IsServer&&session.Modes){session.Modes.ResumeAuthorityFromFrame();session.Modes.ReconcileHumanRoster(next.ActivePlayers.Select(p=>p.ToString()));}
            BindMotion();AttachCallbacks();lastWorld=Time.unscaledTime;UpdatePopulation();
        }
        void AttachCallbacks()
        {
            callbacks=new NetworkDelegates();callbacks.OnReliableDataReceived=Receive;
            if (UsesNativeMotion) callbacks.OnInput = CollectMotionInput;
            callbacks.OnPlayerLeft=(source,player)=>{if(source==runner&&IsCurrentScene)RemovePlayer(player);};callbacks.OnPlayerJoined=(source,player)=>{if(source==runner&&IsCurrentScene)UpdatePopulation();};
            callbacks.OnShutdown=(source,reason)=>{if(source==runner&&IsCurrentScene&&reason!=ShutdownReason.HostMigration&&!(IslandRoomConnection.Instance&&IslandRoomConnection.Instance.Migrating))ReturnToLobby();};
            runner.AddCallbacks(callbacks);
        }
        public void Initialize(NetworkRunner networkRunner, CastlePlaySession owner, CastleLanSession state)
        {
            ResetPoseWindows();runner = networkRunner; session = owner; castle = state;
            posesInFlight.Traffic.PeerId=networkRunner.LocalPlayer.ToString();
            sceneTag = SceneTag(runner);BindWorldConnection();
            // Fusion disables Unity's automatic physics when this component starts.
            // The castle's flight motor and contact hands still require 3D simulation.
            var physics = runner.GetComponent<Fusion.Addons.Physics.RunnerSimulatePhysics>();
            if (physics) physics.Update3DPhysicsScene = true;
            castle.InitializeFusion(owner, runner.LocalPlayer.ToString(), !runner.IsServer);
            BindMotion();
            AttachCallbacks();
            lastWorld = Time.unscaledTime;
            UpdatePopulation();
        }

        void Send(PlayerRef player,int kind,byte[] bytes,int serial=0,Action onSubmitted=null,Action onCanceled=null)
        {
            if(outboundFailed||closing||!IsCurrentScene){onCanceled?.Invoke();return;}
            FusionMotionWireTrace.Record(FusionMotionWireTrace.Phase.Queued,kind,serial,bytes.Length);
            Action submitted=()=>
            {
                FusionMotionWireTrace.Record(FusionMotionWireTrace.Phase.Submitted,kind,serial,bytes.Length);
                if(kind==World){WorldTransfersSubmitted++;WorldPayloadBytesSubmitted+=bytes.Length;}
                onSubmitted?.Invoke();
            };
            // Standalone so earlier clients can ignore the new cue without rejecting a bundle.
            if(collectingHotpath&&kind!=ModeMatchSound)
            {
                try{GetHotpathFrame(player).outbox.Add(kind,serial,sceneTag,bytes,submitted,onCanceled);}
                catch{onCanceled?.Invoke();throw;}
                return;
            }
            var key=ReliableKey.FromInts(Channel,kind,serial,sceneTag);
            try
            {
                if(runner.IsServer)runner.SendReliableDataToPlayer(player,key,bytes);else runner.SendReliableDataToServer(key,bytes);
                submitted();
            }
            catch{onCanceled?.Invoke();throw;}
        }

        void SendSocialPacket(PlayerRef player,int kind,int serial,byte[] data)
        {
            if(!IsCurrentScene)return;
            var key=ReliableKey.FromInts(SocialChannel,kind,serial,sceneTag);
            if(runner.IsServer)runner.SendReliableDataToPlayer(player,key,data);else runner.SendReliableDataToServer(key,data);
            FusionMotionWireTrace.Record(FusionMotionWireTrace.Phase.Submitted,-kind,serial,data.Length);
        }
        public void SendSocial(byte[] data,string except=null)
        {
            if(closing||runner==null||!runner.IsRunning)return;
            if(runner.IsServer)
            {
                foreach(var pair in peers)if(pair.Value.ready&&pair.Key.ToString()!=except)
                {if(!socialPeers.TryGetValue(pair.Key,out var flow))socialPeers[pair.Key]=flow=new CastleSocialChannel();flow.Enqueue(data,Time.unscaledTime);}
            }
            else if(welcomed)socialServer.Enqueue(data,Time.unscaledTime);
        }
        void ReceiveSocial(PlayerRef player,int kind,int serial,ReadOnlySpan<byte> payload)
        {
            if(runner.IsServer&&(!peers.TryGetValue(player,out var peer)||!peer.ready)||!runner.IsServer&&!welcomed)return;
            if(!socialPeers.TryGetValue(player,out var flow))socialPeers[player]=flow=new CastleSocialChannel();
            if(kind==2){(runner.IsServer?flow:socialServer).Ack(serial);return;}
            if(kind!=1||payload.Length<1||payload.Length>16000||(payload[0]!=5&&payload[0]!=6))return;
            if(payload[0]==5&&payload.Length>3500)return;
            SendSocialPacket(player,2,serial,Array.Empty<byte>());
            castle.FusionReceiveSocial(runner.IsServer?player.ToString():"host",payload.ToArray());
        }
        void Receive(NetworkRunner source, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> payload)
        {
            key.GetInts(out int channel, out int kind, out int serial, out int receivedTag);
            if (outboundFailed || closing || source != runner || !IsCurrentScene || (receivedTag != 0 && receivedTag != sceneTag)) return;
            if (runner.IsServer && !source.ActivePlayers.Contains(player)) return;
            if(channel==Channel||channel==SocialChannel) FusionMotionWireTrace.Record(FusionMotionWireTrace.Phase.Received,channel==SocialChannel?-kind:kind,serial,payload.Length);
            if(channel==SocialChannel){ReceiveSocial(player,kind,serial,payload);return;}
            if(channel!=Channel)return;
            if((kind==World||kind==Ack||kind==WorldResync)&&receivedTag!=sceneTag)return;
            if (runner.IsServer && !source.ActivePlayers.Contains(player)) return;
            // Castle snapshots are bounded by the protocol; reject oversized messages before copying.
            if (payload.Length > (kind==HotpathBundle?HotpathBundleCodec.MaxBytes:2*1024*1024)) return;
            try
            {
                if(kind==HotpathBundle){ReceiveHotpath(source,player,payload,receivedTag);return;}
                if (runner.IsServer)
                {
                    if (kind == Ready)
                    {
                        if (!peers.ContainsKey(player))
                        {
                            var added=new Peer {lastPose=Time.unscaledTime,welcome=castle.FusionWelcome(player.ToString()),motionLane=AssignMotionLane()};
                            added.posesInFlight.Traffic.PeerId=player.ToString();peers.Add(player,added);
                            if (UsesNativeMotion) SetDistantMotionPriority(player);
                        }
                        if (!peers[player].ready) Send(player, Welcome, peers[player].welcome);
                    }
                    else if (peers.TryGetValue(player, out var peer))
                    {
                        if(kind==Command&&peer.ready&&SessionDevelopmentPolicy.DemoEnabled(session)&&IslandRoomConnection.Instance&&IslandRoomConnection.Instance.HasDemoPermission(player.ToString())&&payload.Length<=8192&&Time.unscaledTime-peer.lastCommand>=.15f)
                        {peer.lastCommand=Time.unscaledTime;CastleDemoCommands.ExecuteFromPeer(session,JsonUtility.FromJson<CastleDemoCommand>(System.Text.Encoding.UTF8.GetString(payload.ToArray())),player.ToString());}
                        else if(kind==ModeCommand&&peer.ready&&payload.Length<=8192&&Time.unscaledTime-peer.lastModeCommand>=.08f)
                        {peer.lastModeCommand=Time.unscaledTime;ConnectModes();string modeRequest=System.Text.Encoding.UTF8.GetString(payload.ToArray());
                            if(modeRuntime&&SessionDevelopmentPolicy.DemoEnabled(session)&&IslandRoomConnection.Instance&&IslandRoomConnection.Instance.HasDemoPermission(player.ToString()))modeRuntime.ReceiveAdminCommand(player.ToString(),modeRequest);
                            modeRuntime?.ReceiveCommand(player.ToString(),modeRequest);}
                        else if (kind == WelcomeAck)
                        {
                            peer.ready=true;
                            var room=IslandRoomConnection.Instance;
                            if(room&&room.Owns(runner))room.NoteMatchPeer(player.ToString());
                        }
                        else if (kind == LivePose && peer.ready && payload.Length <= MaxPoseSize+LivePoseCodec.HeaderBytes)
                        {
                            var rawPose=LivePoseCodec.Unpack(payload.ToArray(),MaxPoseSize,out var receipts);ApplyPiggybackReceipts(peer.posesInFlight,receipts,Time.unscaledTime);peer.posesInFlight.Traffic.RawReceivedBytes+=rawPose.Length;
                            if ((!UsesNativeMotion || !peer.hasNativeSequence || Time.unscaledTime-peer.lastNativePose>.18f) &&
                                LivePoseWindow.Newer(serial,peer.lastPoseSequence,peer.hasPoseSequence))
                            {
                                long applyStarted=FusionMotionWireTrace.Now;castle.FusionReceivePlayer(player.ToString(),rawPose);
                                FusionMotionWireTrace.Record(FusionMotionWireTrace.Phase.Applied,LivePose,serial,rawPose.Length,applyStarted);
                                peer.lastPoseSequence = serial;peer.hasPoseSequence=true;
                                peer.lastPose = Time.unscaledTime;
                                peer.posesInFlight.Applied(payload.Length,Time.unscaledTime);
                            }
                            else peer.posesInFlight.Skipped();
                            peer.posesInFlight.Received(payload.Length,Time.unscaledTime);
                            peer.receipts.Add(serial);peer.posesInFlight.Traffic.PendingReceiptCount=peer.receipts.Count;
                        }
                        else if (kind == PoseReceiptGroup&&payload.Length>=7&&payload.Length<=3+LivePoseReceipts.Capacity*4) ApplyPoseReceipts(peer.posesInFlight,payload.ToArray(),Time.unscaledTime);
                        else if (kind == Ack && peer.waiting!=0 && peer.waiting==serial && payload.Length==WorldDeltaCodec.TokenBytes && peer.world.Acknowledge(serial,payload.ToArray()))
                        {FusionMotionWireTrace.Record(FusionMotionWireTrace.Phase.Ack,World,serial,payload.Length);peer.waiting=0;peer.worldResyncs=0;peer.pendingWorldResync=null;}
                        else if(kind==WorldResync&&peer.ready&&peer.waiting!=0&&peer.waiting==serial&&payload.Length==WorldDeltaCodec.TokenBytes)
                        {
                            var token=payload.ToArray();if(peer.world.MatchesPending(serial,token))peer.pendingWorldResync=token;
                        }
                    }
                }
                else
                {
                    // Fusion's server-to-client stream is the only source of world state.
                    if (kind == Welcome)
                    {
                        if (!welcomed) { castle.FusionReceiveWelcome(payload.ToArray()); authoritativeRules=MatchSetup.CaptureRules(session.Player); welcomed = true; }
                        Send(default, WelcomeAck, Array.Empty<byte>());
                    }
                    else if (kind == World && welcomed)
                    {
                        ReceiveWorld(payload.ToArray(),serial);
                    }
                    else if (kind == LivePoses && welcomed && payload.Length <= MaxLiveBatchSize+LivePoseCodec.HeaderBytes)
                    {
                        var rawPoses=LivePoseCodec.Unpack(payload.ToArray(),MaxLiveBatchSize,out var receipts);ApplyPiggybackReceipts(posesInFlight,receipts,Time.unscaledTime);posesInFlight.Traffic.RawReceivedBytes+=rawPoses.Length;
                        if (LivePoseWindow.Newer(serial,lastPoseBatch,hasPoseBatch) && !castle.WaitingForWorld)
                        {
                            long applyStarted=FusionMotionWireTrace.Now;castle.FusionApplyLivePoses(rawPoses, runner.ActivePlayers.Select(p => p.ToString()));
                            FusionMotionWireTrace.Record(FusionMotionWireTrace.Phase.Applied,LivePoses,serial,rawPoses.Length,applyStarted);
                            lastPoseBatch = serial;hasPoseBatch=true;posesInFlight.Applied(payload.Length,Time.unscaledTime);
                        }
                        else posesInFlight.Skipped();
                        posesInFlight.Received(payload.Length,Time.unscaledTime);
                        poseReceipts.Add(serial);posesInFlight.Traffic.PendingReceiptCount=poseReceipts.Count;
                    }
                    else if (kind == PoseReceiptGroup&&payload.Length>=7&&payload.Length<=3+LivePoseReceipts.Capacity*4) ApplyPoseReceipts(posesInFlight,payload.ToArray(),Time.unscaledTime);
                    else if(kind==MatchState&&welcomed&&payload.Length<=512000)ReceiveMatch(payload);
                    else if(kind==ModeOutcome&&welcomed&&payload.Length<=1024)ReceiveOutcome(payload);
                    else if(kind==ModeMatchSound&&welcomed&&receivedTag==sceneTag&&payload.Length==MatchSoundBytes)ReceiveMatchSound(payload);
                    else if(kind==MysterySecret&&welcomed&&payload.Length<=8192){ConnectModes();modeRuntime?.ReceiveSecret(System.Text.Encoding.UTF8.GetString(payload.ToArray()));}
                    else if(kind==MysteryTrails&&welcomed&&payload.Length<=64000){ConnectModes();modeRuntime?.ReceiveTrails(System.Text.Encoding.UTF8.GetString(payload.ToArray()));}
                    else if(kind==Admin&&welcomed&&payload.Length<2048)
                    {Demo=JsonUtility.FromJson<DemoState>(System.Text.Encoding.UTF8.GetString(payload.ToArray()));ReceiveWave(Demo.wave);IslandRoomConnection.Instance?.ReceiveSharedControls(Demo.shared);castle.FusionRoomNotice(Demo.roomNotice);}
                    else if(kind==DisasterImpact&&welcomed&&payload.Length<512)ReceiveDisasterImpact(payload.ToArray());
                    else if(kind==DisasterVisuals&&welcomed&&payload.Length<256*1024)ReceiveDisasters(payload.ToArray());
                    else if(kind==Rules&&welcomed&&payload.Length<4096){authoritativeRules=System.Text.Encoding.UTF8.GetString(payload.ToArray());MatchSetup.ApplyRules(session.Player,authoritativeRules);}
                }
            }
            catch (Exception error)
            {
                receiverBundleFailed=true;CancelHotpathPeer(player);
                Debug.LogError("Castle synchronization rejected a message: " + error.Message, this);
                if (runner.IsServer) runner.Disconnect(player);
                else Fail("Game synchronization failed. Make sure everyone has the same build, then rejoin.");
            }
        }

        void LateUpdate()
        {
            if(outboundFailed||closing||poseSendFailed||runner==null||!runner.IsRunning||!IsCurrentScene)return;
            if(!BeginHotpathFrame())return;
            try
            {

            long renderStarted=FusionMotionWireTrace.Now;castle.FusionRender();
            FusionMotionWireTrace.Record(FusionMotionWireTrace.Phase.Render,0,0,0,renderStarted);ConnectModes();
            float now = Time.unscaledTime;
            PumpNativeMotion(now);
            PumpPoses(now);if(poseSendFailed)return;
            try{PumpWorldResync(now);}catch(Exception){Fail("World resynchronization send failed. Rejoin with the same game build.");return;}
            FlushPoseReceipts(now);if(poseSendFailed)return;
            if(pendingStamina!=null&&now>=nextStamina){if(SessionDevelopmentPolicy.CanRequestLocal(session))Send(default,Command,System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(pendingStamina)));pendingStamina=null;nextStamina=now+.2f;}
            if(runner.IsServer){foreach(var pair in socialPeers)if(peers.TryGetValue(pair.Key,out var ready)&&ready.ready)pair.Value.Pump(now,(serial,data)=>SendSocialPacket(pair.Key,1,serial,data));}
            else if(welcomed)socialServer.Pump(now,(serial,data)=>SendSocialPacket(default,1,serial,data));
            if (runner.IsServer)
            {
                foreach (var pair in peers)
                    if (now - pair.Value.lastPose > 1) castle.FusionSuspendPlayer(pair.Key.ToString());
                foreach (var pair in peers.ToArray())
                    if (now - pair.Value.lastPose > 20 || pair.Value.waiting != 0 && now - pair.Value.sentAt > 20 || pair.Value.posesInFlight.Expired(now))
                    { runner.Disconnect(pair.Key); RemovePlayer(pair.Key); }
                // Check sent transfers before a prepared packet reserves a slot.
                SubmitPreparedWorld(now);
                if (now < nextSend) return;
                nextSend = now + .1f;ObserveWorldScope();
                FlushDisasterImpacts();
                CaptureDisasters();CaptureMatch();
                var bots=session.SurvivorBots;var hands=session.HandBots;
                Demo=new DemoState{leader=IslandRoomConnection.Instance?IslandRoomConnection.Instance.LeaderId:"",wave=CaptureWave(),shared=IslandRoomConnection.Instance&&IslandRoomConnection.Instance.SharedDemoControls,survivors=bots?.Count??0,hands=hands?.Count??0,
                    autoFill=bots?.AutoFill??false,handLimit=hands?.Capacity??0,capacity=bots?.Capacity??0,target=bots?.TargetPopulation??0,survivorPreset=bots?.SelectedPreset??0,handPreset=hands?.SelectedPreset??0,survivorName=bots?.SelectedName??"",handName=hands?.SelectedName??""};
                var room=IslandRoomConnection.Instance;
                Demo.roomNotice=room&&room.Owns(runner)?room.MatchNotice:"";
                castle.FusionRoomNotice(Demo.roomNotice);
                QueueWorldSend(now);
            }
            else if (now >= nextSend)
            {
                nextSend = now + 1;
                if (!welcomed) Send(default, Ready, Array.Empty<byte>());
                if(posesInFlight.Expired(now)){Fail("Live pose acknowledgements timed out. Rejoin the room.");return;}
                if (now - lastWorld > (welcomed ? 20 : 60)) Fail("The host stopped responding. Check your connection and rejoin the room.");
            }

            }
            catch(Exception){outboundFailed=true;CancelHotpathFrames();Fail("Hotpath production failed. Rejoin with the same game build.");}
            finally
            {
                if(outboundFailed||closing)CancelHotpathFrames();else FlushHotpathFrames();
            }
        }

        void UpdatePopulation()
        {
            InvalidateWorldSenders();
            if (runner.IsServer) session.SurvivorBots?.SetHumanPlayerCount(runner.ActivePlayers.Count());
        }

        void RemovePlayer(PlayerRef player)
        {
            CancelHotpathPeer(player);
            if(peers.TryGetValue(player,out var departing)){departing.world.Reset();departing.posesInFlight.Clear();departing.receipts.Clear();}
            InvalidateWorldSenders();modeRuntime?.UnregisterRemote(player.ToString());peers.Remove(player); socialPeers.Remove(player); castle.FusionRemovePlayer(player.ToString());
            if (runner.IsServer) session.SurvivorBots?.SetHumanPlayerCount(runner.ActivePlayers.Count(p => p != player));
        }

        public void Leave()
        {
            outboundFailed=true;CancelHotpathFrames();
            if (closing) return;
            ResetPoseWindows();ResetDisasters();
            var island=IslandRoomConnection.Instance;if(island&&island.Owns(runner))_=island.Leave();else _=runner.Shutdown();
        }

        void Fail(string message)
        {
            outboundFailed=true;CancelHotpathFrames();
            if (closing) return;
            var island = IslandRoomConnection.Instance;
            if (island) island.Status = message;
            Debug.LogWarning(message);
            Leave();
        }

        void ReturnToLobby()
        {
            if (closing) return;
            closing = true;ResetPoseWindows();
            var island=IslandRoomConnection.Instance;
            if(island&&island.Owns(runner))return;
            // The prototype also owns persistent templates and needs its reset path.
            var bootstrap = FindFirstObjectByType<FusionBootstrap>();
            if (bootstrap != null) bootstrap.Shutdown();
            else SceneManager.LoadScene("Lobby");
        }

        void OnDestroy()
        {
            UnbindMotion(true);
            DisconnectModes();ResetPoseWindows();
            ResetDisasters();
            if (runner != null && callbacks != null) runner.RemoveCallbacks(callbacks);
        }
        internal sealed class HotpathOutbox
        {
            readonly List<HotpathBundleCodec.Item> items=new List<HotpathBundleCodec.Item>();
            readonly List<Action> submitted=new List<Action>(),canceled=new List<Action>();
            int bytes=24;bool ended,flushing;
            internal int Count=>items.Count;
            internal int Bytes=>bytes;
            internal void Add(int kind,int serial,int tag,byte[] payload,Action success=null,Action cancel=null)
            {
                if(ended||flushing)throw new InvalidOperationException("Hotpath frame already finished or submitting");
                if(payload==null||payload.Length>HotpathBundleCodec.MaxItemBytes||items.Count>=HotpathBundleCodec.MaxItems||
                    payload.Length>HotpathBundleCodec.MaxBytes-bytes-16)throw new System.IO.InvalidDataException("Hotpath frame exceeds its bound");
                items.Add(new HotpathBundleCodec.Item(kind,serial,tag,payload));submitted.Add(success);canceled.Add(cancel);bytes+=16+payload.Length;
            }
            internal void Flush(Func<bool> current,Action<IReadOnlyList<HotpathBundleCodec.Item>> submit)
            {
                if(ended||flushing)throw new InvalidOperationException("Hotpath frame already finished or submitting");
                if(items.Count==0){ended=true;return;}
                flushing=true;
                try
                {
                    if(!current())throw new InvalidOperationException("Hotpath context changed before submission");
                    submit(items.AsReadOnly()); // Public SDK return precedes every success callback.
                    if(ended)throw new InvalidOperationException("Hotpath frame canceled during submission");
                    for(int i=0;i<submitted.Count;i++)submitted[i]?.Invoke();
                    ended=true;items.Clear();submitted.Clear();canceled.Clear();
                }
                catch{Cancel();throw;}
            }
            internal void Cancel()
            {
                if(ended)return;ended=true;
                for(int i=0;i<canceled.Count;i++)try{canceled[i]?.Invoke();}catch{}
                items.Clear();submitted.Clear();canceled.Clear();
            }
        }


        sealed class HotpathFrame
        {
            internal readonly NetworkRunner runner;internal readonly PlayerRef player;internal readonly Peer peer;
            internal readonly int tag,generation,frame;internal readonly IslandRoomConnection room;
            internal readonly HotpathOutbox outbox=new HotpathOutbox();
            internal HotpathFrame(NetworkRunner runner,PlayerRef player,Peer peer,int tag,int generation,int frame,IslandRoomConnection room)
            {this.runner=runner;this.player=player;this.peer=peer;this.tag=tag;this.generation=generation;this.frame=frame;this.room=room;}
        }
        readonly Dictionary<PlayerRef,HotpathFrame> hotpathFrames=new Dictionary<PlayerRef,HotpathFrame>();
        bool collectingHotpath,outboundFailed,receiverBundleFailed;
        int lastHotpathFrame=-1,bundleSequence;
        public long HotpathBundlesSubmitted{get;private set;}
        public long HotpathRecordsSubmitted{get;private set;}
        public long HotpathBytesSubmitted{get;private set;}
        public long HotpathFlushFailures{get;private set;}
        public long HotpathMaxRecords{get;private set;}
        public long HotpathMaxBytes{get;private set;}
        bool BeginHotpathFrame()
        {
            if(lastHotpathFrame==Time.frameCount)return false;
            if(collectingHotpath||hotpathFrames.Count!=0)
            {outboundFailed=true;CancelHotpathFrames();Fail("A hotpath frame was not completed. Rejoin the room.");return false;}
            lastHotpathFrame=Time.frameCount;collectingHotpath=true;return true;
        }
        HotpathFrame GetHotpathFrame(PlayerRef player)
        {
            if(hotpathFrames.TryGetValue(player,out var frame))return frame;
            if(!collectingHotpath||hotpathFrames.Count>=10)throw new InvalidOperationException("Hotpath peer bound exceeded");
            Peer peer=null;if(runner.IsServer&&!peers.TryGetValue(player,out peer))throw new InvalidOperationException("Hotpath peer is absent");
            frame=new HotpathFrame(runner,player,peer,sceneTag,worldConnection,Time.frameCount,worldRoom);hotpathFrames.Add(player,frame);return frame;
        }
        bool HotpathCurrent(HotpathFrame frame)
        {
            if(outboundFailed||closing||!ReferenceEquals(frame.runner,runner)||!runner||!runner.IsRunning||!IsCurrentScene||
                frame.tag!=sceneTag||frame.generation!=worldConnection||frame.frame!=Time.frameCount||!ReferenceEquals(frame.room,worldRoom))return false;
            return !runner.IsServer||runner.ActivePlayers.Contains(frame.player)&&peers.TryGetValue(frame.player,out var peer)&&ReferenceEquals(peer,frame.peer);
        }
        void CancelHotpathPeer(PlayerRef player)
        {if(hotpathFrames.TryGetValue(player,out var frame)){hotpathFrames.Remove(player);frame.outbox.Cancel();}}
        void CancelHotpathFrames()
        {
            collectingHotpath=false;var frames=hotpathFrames.Values.ToArray();hotpathFrames.Clear();
            foreach(var frame in frames)frame.outbox.Cancel();
        }
        void FlushHotpathFrames()
        {
            collectingHotpath=false;
            if(outboundFailed||closing){CancelHotpathFrames();return;}
            try
            {
                foreach(var frame in hotpathFrames.Values.ToArray())
                {
                    frame.outbox.Flush(()=>HotpathCurrent(frame),items=>
                    {
                        var packet=HotpathBundleCodec.Pack(items,runner.IsServer);
                        int bundleSerial=LivePoseWindow.NextSequence(ref bundleSequence);
                        var key=ReliableKey.FromInts(Channel,HotpathBundle,bundleSerial,sceneTag);
                        if(runner.IsServer)runner.SendReliableDataToPlayer(frame.player,key,packet);else runner.SendReliableDataToServer(key,packet);
                        FusionMotionWireTrace.Record(FusionMotionWireTrace.Phase.Submitted,HotpathBundle,bundleSerial,packet.Length);
                        HotpathBundlesSubmitted++;HotpathRecordsSubmitted+=items.Count;HotpathBytesSubmitted+=packet.Length;
                        HotpathMaxRecords=Math.Max(HotpathMaxRecords,items.Count);HotpathMaxBytes=Math.Max(HotpathMaxBytes,packet.Length);
                    });
                }
                hotpathFrames.Clear();
            }
            catch(Exception)
            {
                HotpathFlushFailures++;outboundFailed=true;CancelHotpathFrames();
                Fail("Hotpath submission failed. Rejoin with the same game build.");
            }
        }
        void ReceiveHotpath(NetworkRunner source,PlayerRef player,ReadOnlySpan<byte> payload,int tag)
        {
            if(tag!=sceneTag)throw new System.IO.InvalidDataException("Bundle scene scope differs");
            var items=HotpathBundleCodec.Decode(payload.ToArray(),!runner.IsServer);
            // Validate every original key before dispatching any item.
            foreach(var item in items)
            {
                if(item.sceneTag!=0&&item.sceneTag!=sceneTag||
                    (item.kind==World||item.kind==Ack||item.kind==WorldResync)&&item.sceneTag!=sceneTag)
                    throw new System.IO.InvalidDataException("Bundle item scene scope differs");
            }
            bool previous=receiverBundleFailed;receiverBundleFailed=false;
            try
            {
                foreach(var item in items)
                {
                    if(receiverBundleFailed||outboundFailed||closing)return;
                    if(source!=runner||!IsCurrentScene){CancelHotpathFrames();return;}
                    var bytes=item.payload;
                    Receive(source,player,ReliableKey.FromInts(Channel,item.kind,item.serial,item.sceneTag),bytes);
                }
            }
            finally{receiverBundleFailed=previous;}
        }

    }
}


namespace DeadlyJenga.CastlePrototype
{
    // Preserves original CAST keys and payloads. Outer compression is unsupported.
    internal static class HotpathBundleCodec
    {
        internal const int MaxItems=64, MaxBytes=4*1024*1024, MaxItemBytes=2*1024*1024, HeaderBytes=24, ItemHeaderBytes=16;
        const uint Magic=0x31424a44;
        const byte Version=1;
        internal sealed class Item
        {
            internal readonly int kind,serial,sceneTag;
            readonly byte[] bytes;
            internal int Length=>bytes.Length;
            internal byte[] payload=>(byte[])bytes.Clone();
            internal Item(int kind,int serial,int sceneTag,byte[] payload)
            {
                Require(payload!=null&&payload.Length<=MaxItemBytes,"Bundle item size is invalid");
                this.kind=kind;this.serial=serial;this.sceneTag=sceneTag;bytes=(byte[])payload.Clone();
            }
        }
        static void Require(bool valid,string message)
        {if(!valid)throw new InvalidDataException(message);}
        static void Validate(int kind,int length,bool fromServer)
        {
            Require(length>=0&&length<=MaxItemBytes,"Bundle item size is invalid");
            bool allowed=fromServer?
                kind==2||kind==3||kind==7||kind==9||kind==10||kind==11||kind==12||kind==14||kind==15||kind==16||kind==21||kind==23:
                kind==1||kind==5||kind==6||kind==8||kind==13||kind==20||kind==23||kind==24;
            Require(allowed,"Bundle kind or direction is invalid");
            switch(kind)
            {
                case 1:case 6:Require(length==0,"Bundle bootstrap payload is invalid");break;
                case 5:case 24:Require(length==60,"Bundle world token size is invalid");break;
                case 7:Require(length<4096,"Bundle rules size is invalid");break;
                case 8:case 13:case 15:Require(length<=8192,"Bundle command or secret size is invalid");break;
                case 9:Require(length<2048,"Bundle admin size is invalid");break;
                case 10:Require(length<512,"Bundle impact size is invalid");break;
                case 11:Require(length<256*1024,"Bundle disaster size is invalid");break;
                case 12:Require(length<=512000,"Bundle match size is invalid");break;
                case 14:Require(length<=1024,"Bundle outcome size is invalid");break;
                case 16:Require(length<=64000,"Bundle trails size is invalid");break;
                case 20:Require(length>=18&&length<=4113,"Bundle pose size is invalid");break;
                case 21:Require(length>=18&&length<=65553,"Bundle poses size is invalid");break;
                case 23:Require(length>=7&&length<=259,"Bundle receipt size is invalid");break;
            }
        }
        internal static byte[] Pack(IReadOnlyList<Item> items,bool fromServer)
        {
            Require(items!=null&&items.Count>=1&&items.Count<=MaxItems,"Bundle count is invalid");
            int count=items.Count;var entries=new Item[count];var payloads=new byte[count][];long length=HeaderBytes;
            for(int i=0;i<count;i++)
            {
                var entry=items[i];Require(entry!=null,"Bundle item is missing");
                Validate(entry.kind,entry.Length,fromServer);entries[i]=entry;
                length=checked(length+ItemHeaderBytes+entry.Length);Require(length<=MaxBytes,"Bundle exceeds the byte limit");
                payloads[i]=entry.payload;
            }
            using var output=new MemoryStream((int)length);using var writer=new BinaryWriter(output);
            int body=(int)length-HeaderBytes;
            writer.Write(Magic);writer.Write(Version);writer.Write((byte)0);writer.Write((ushort)0);
            writer.Write(body);writer.Write(body);writer.Write(count);writer.Write((uint)0);
            for(int i=0;i<count;i++)
            {var entry=entries[i];writer.Write(entry.kind);writer.Write(entry.serial);writer.Write(entry.sceneTag);writer.Write(payloads[i].Length);writer.Write(payloads[i]);}
            var packet=output.ToArray();WriteChecksum(packet,Checksum(packet));return packet;
        }
        internal static Item[] Decode(byte[] packet,bool fromServer)
        {
            Require(packet!=null&&packet.Length>=HeaderBytes&&packet.Length<=MaxBytes,"Bundle size is invalid");
            using var input=new MemoryStream(packet,false);using var reader=new BinaryReader(input);
            Require(reader.ReadUInt32()==Magic&&reader.ReadByte()==Version,"Bundle header is invalid");
            Require(reader.ReadByte()==0&&reader.ReadUInt16()==0,"Bundle compression or flags are unsupported");
            int raw=reader.ReadInt32(),stored=reader.ReadInt32(),count=reader.ReadInt32();uint checksum=reader.ReadUInt32();
            Require(count>=1&&count<=MaxItems,"Bundle count is invalid");
            Require(raw>=ItemHeaderBytes&&raw==stored&&stored==packet.Length-HeaderBytes,"Bundle lengths are invalid");
            Require(checksum==Checksum(packet),"Bundle checksum differs");
            // Validate every entry before allocating or exposing any item for dispatch.
            var kinds=new int[count];var serials=new int[count];var tags=new int[count];var lengths=new int[count];var offsets=new int[count];
            for(int i=0;i<count;i++)
            {
                Require(input.Length-input.Position>=ItemHeaderBytes,"Bundle entry is truncated");
                kinds[i]=reader.ReadInt32();serials[i]=reader.ReadInt32();tags[i]=reader.ReadInt32();lengths[i]=reader.ReadInt32();
                Validate(kinds[i],lengths[i],fromServer);
                Require(lengths[i]<=input.Length-input.Position,"Bundle payload is truncated");
                offsets[i]=(int)input.Position;input.Position=checked(input.Position+lengths[i]);
            }
            Require(input.Position==input.Length,"Bundle has trailing data");
            var entries=new Item[count];
            for(int i=0;i<count;i++)
            {
                var payload=new byte[lengths[i]];Buffer.BlockCopy(packet,offsets[i],payload,0,payload.Length);
                entries[i]=new Item(kinds[i],serials[i],tags[i],payload);
            }
            return entries;
        }
        static readonly uint[] CrcTable=MakeCrcTable();
        static uint[] MakeCrcTable()
        {
            var table=new uint[256];for(uint i=0;i<table.Length;i++){uint value=i;for(int bit=0;bit<8;bit++)value=(value&1)!=0?0xedb88320u^(value>>1):value>>1;table[i]=value;}return table;
        }
        static uint Checksum(byte[] packet)
        {
            uint crc=uint.MaxValue;for(int i=0;i<packet.Length;i++){if(i>=20&&i<24)continue;crc=CrcTable[(crc^packet[i])&255]^(crc>>8);}return ~crc;
        }
        static void WriteChecksum(byte[] packet,uint value)
        {for(int i=0;i<4;i++)packet[20+i]=(byte)(value>>(i*8));}
    }
}
