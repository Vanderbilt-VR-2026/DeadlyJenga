using System;
using System.Collections.Generic;
using System.Linq;
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
        const int LivePose = 20, LivePoses = 21, PoseReceiptGroup = 23;
        const int PoseWindow = 6, MaxPoseSize = 4096, MaxLiveBatchSize=64*1024;
        internal static class LivePoseCodec
        {
            internal const int HeaderBytes=17;
            const uint Magic=0x3150534c;
            static readonly uint[] CrcTable=BuildCrcTable();
            static uint[] BuildCrcTable()
            {
                var table=new uint[256];
                for(int i=0;i<table.Length;i++){uint value=(uint)i;for(int bit=0;bit<8;bit++)value=(value>>1)^((value&1)!=0?0xedb88320u:0);table[i]=value;}
                return table;
            }
            static uint Crc(byte[] data)
            {uint crc=uint.MaxValue;foreach(byte value in data)crc=(crc>>8)^CrcTable[(crc^value)&255];return ~crc;}
            internal static byte[] Pack(byte[] raw,int maximum)
            {
                if(raw==null||raw.Length<1||raw.Length>maximum)throw new System.IO.InvalidDataException("Live pose size is invalid");
                byte[] body;
                using(var zipped=new System.IO.MemoryStream())
                {using(var zip=new System.IO.Compression.GZipStream(zipped,System.IO.Compression.CompressionLevel.Fastest,true))zip.Write(raw,0,raw.Length);body=zipped.ToArray();}
                bool compressed=body.Length<raw.Length;if(!compressed)body=raw;
                using var output=new System.IO.MemoryStream();using var writer=new System.IO.BinaryWriter(output);
                writer.Write(Magic);writer.Write((byte)(compressed?1:0));writer.Write(raw.Length);writer.Write(body.Length);writer.Write(Crc(raw));writer.Write(body);return output.ToArray();
            }
            internal static byte[] Unpack(byte[] packet,int maximum)
            {
                if(packet==null||packet.Length<HeaderBytes||packet.Length>maximum+HeaderBytes)throw new System.IO.InvalidDataException("Live pose envelope size is invalid");
                using var input=new System.IO.MemoryStream(packet,false);using var reader=new System.IO.BinaryReader(input);
                if(reader.ReadUInt32()!=Magic)throw new System.IO.InvalidDataException("Live pose protocol differs");
                byte mode=reader.ReadByte();int length=reader.ReadInt32(),stored=reader.ReadInt32();uint crc=reader.ReadUInt32();
                if(mode>1||length<1||length>maximum||stored<1||stored!=packet.Length-HeaderBytes||stored>length||mode==0&&stored!=length||mode==1&&stored>=length)
                    throw new System.IO.InvalidDataException("Live pose declared lengths are invalid");
                var raw=new byte[length];
                if(mode==0)System.Buffer.BlockCopy(packet,HeaderBytes,raw,0,length);
                else
                {
                    int at=HeaderBytes,end=packet.Length;
                    if(stored<18||packet[at]!=31||packet[at+1]!=139||packet[at+2]!=8||packet[at+3]!=0||
                        BitConverter.ToUInt32(packet,end-8)!=crc||BitConverter.ToUInt32(packet,end-4)!=(uint)length)
                        throw new System.IO.InvalidDataException("Live pose gzip framing is invalid");
                    // Buffered gzip consumption is bounded by the envelope and exact decoded size.
                    using var compressed=new System.IO.MemoryStream(packet,at,stored,false);
                    using var zip=new System.IO.Compression.GZipStream(compressed,System.IO.Compression.CompressionMode.Decompress);
                    int count=0,read;
                    while(count<length&&(read=zip.Read(raw,count,length-count))>0)count+=read;
                    if(count!=length||zip.ReadByte()!=-1)throw new System.IO.InvalidDataException("Live pose expansion differs from its bound");
                }
                if(Crc(raw)!=crc)throw new System.IO.InvalidDataException("Live pose checksum differs");return raw;
            }
        }
        internal sealed class LivePoseReceipts
        {
            internal const int Capacity=64;
            readonly HashSet<int> serials=new HashSet<int>();
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
            internal int Flush(Action<byte[]> send)
            {
                if(serials.Count==0)return 0;var values=serials.ToArray();var packet=Encode(values);send(packet);
                foreach(int value in values)serials.Remove(value);return values.Length;
            }
            internal void Clear()=>serials.Clear();
        }
        public sealed class LivePoseTraffic
        {
            public long RawSubmittedBytes{get;internal set;}public long RawReceivedBytes{get;internal set;}public long CompressedSubmittedFrames{get;internal set;}
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

        sealed class Peer { public readonly LivePoseWindow posesInFlight = new LivePoseWindow(PoseWindow,384*1024,MaxLiveBatchSize+LivePoseCodec.HeaderBytes);public readonly LivePoseReceipts receipts=new LivePoseReceipts(); public int lastPoseSequence;public bool hasPoseSequence; public bool ready; public string rules; public byte[] welcome; public int waiting; public float lastPose, sentAt, lastCommand; public string admin; public byte[] disasters; public byte[] match; public float lastModeCommand; public int mysteryRound=-1,trailCount; }
        readonly Dictionary<PlayerRef, Peer> peers = new Dictionary<PlayerRef, Peer>();
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
        bool IsCurrentScene=>sceneTag==SceneTag(runner);
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
                {foreach(var pair in peers.ToArray()){int count=pair.Value.receipts.Flush(packet=>Send(pair.Key,PoseReceiptGroup,packet));if(count>0){pair.Value.posesInFlight.Traffic.ReceiptGroupsSubmitted++;pair.Value.posesInFlight.Traffic.ReceiptSerialsSubmitted+=count;}pair.Value.posesInFlight.Traffic.PendingReceiptCount=pair.Value.receipts.Count;}}
                else{int count=poseReceipts.Flush(packet=>Send(default,PoseReceiptGroup,packet));if(count>0){posesInFlight.Traffic.ReceiptGroupsSubmitted++;posesInFlight.Traffic.ReceiptSerialsSubmitted+=count;}posesInFlight.Traffic.PendingReceiptCount=poseReceipts.Count;}
            }
            catch(Exception){Fail("Pose receipt send failed. Rejoin with the same game build.");poseSendFailed=true;}
        }
        static void ApplyPoseReceipts(LivePoseWindow window,byte[] packet,float now)
        {var values=LivePoseReceipts.Decode(packet);window.Traffic.ReceiptGroupsReceived++;window.Traffic.ReceiptSerialsReceived+=values.Length;foreach(int value in values)window.Ack(value,now);}
        public LivePoseTraffic LocalPoseTraffic=>posesInFlight.Traffic;
        public LivePoseTraffic[] PeerPoseTraffic=>peers.Values.Select(peer=>peer.posesInFlight.Traffic).ToArray();
        public long WorldTransfersSubmitted{get;private set;}
        public long WorldPayloadBytesSubmitted{get;private set;}
        void ResetPoseWindows()
        {posesInFlight.Clear();poseReceipts.Clear();posesInFlight.Traffic.PendingReceiptCount=0;nextReceiptFlush=0;foreach(var peer in peers.Values){peer.posesInFlight.Clear();peer.receipts.Clear();peer.posesInFlight.Traffic.PendingReceiptCount=0;}hasPoseBatch=false;lastPoseBatch=0;nextPoseSend=0;poseSendFailed=false;}
        bool SendPose(PlayerRef target,int kind,LivePoseWindow window,byte[] packet,float now)
        {
            if(closing||poseSendFailed||runner==null||!runner.IsRunning||!IsCurrentScene)return false;
            int serial=LivePoseWindow.NextSequence(ref poseSequence);
            int rawLength=packet?.Length??0;
            if(packet==null||packet.Length<1||packet.Length>(kind==LivePose?MaxPoseSize:MaxLiveBatchSize))
            {window.Reserve(serial,packet?.Length??0,now);Fail("Live pose payload exceeds the supported bound. Rejoin with the same game build.");poseSendFailed=true;return false;}
            try{packet=LivePoseCodec.Pack(packet,kind==LivePose?MaxPoseSize:MaxLiveBatchSize);}
            catch(Exception){Fail("Live pose encoding failed. Rejoin with the same game build.");poseSendFailed=true;return false;}
            if(!window.Reserve(serial,packet.Length,now))return false;
            try{Send(target,kind,packet,serial);window.Submitted(packet.Length);window.Traffic.RawSubmittedBytes+=rawLength;if(packet[4]==1)window.Traffic.CompressedSubmittedFrames++;return true;}
            catch(Exception){window.Cancel(serial);Fail("Live pose send failed. Rejoin with the same game build.");poseSendFailed=true;return false;}
        }

        void PumpPoses(float now)
        {
            posesInFlight.Observe(now);foreach(var peer in peers.Values)peer.posesInFlight.Observe(now);
            if(now<nextPoseSend)return;nextPoseSend=now+1f/30;
            if(!runner.IsServer)
            {
                if(welcomed&&!castle.WaitingForWorld&&posesInFlight.CanCapture())SendPose(default,LivePose,posesInFlight,castle.FusionCapturePlayer(),now);
                return;
            }
            CastleLanProtocol.Actor[] actors=null;
            foreach(var pair in peers)
            {
                if(!pair.Value.ready||!pair.Value.posesInFlight.CanCapture())continue;
                actors??=castle.FusionCaptureLiveActors();
                var batch=CastleLanSession.FusionEncodeLivePosesFor(actors,pair.Key.ToString());
                if(!SendPose(pair.Key,LivePoses,pair.Value.posesInFlight,batch,now)&&poseSendFailed)return;
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
        public void PauseForMigration(){ResetPoseWindows();ResetDisasters();closing=true;pendingStamina=null;if(runner&&callbacks!=null)runner.RemoveCallbacks(callbacks);castle.FusionFreeze();}
        public void ResumeAfterMigration(NetworkRunner next)
        {
            ResetPoseWindows();peers.Clear();socialPeers.Clear();socialServer.Clear();welcomed=false;closing=false;nextSend=0;
            runner=next;posesInFlight.Traffic.PeerId=next.LocalPlayer.ToString();sceneTag=SceneTag(next);if(next.IsServer&&session.SurvivorBots!=null){if(Demo.target>0)session.SurvivorBots.TargetPopulation=Mathf.Max(1,Demo.target);session.SurvivorBots.AutoFill=Demo.autoFill;}castle.FusionRebind(next.LocalPlayer.ToString(),!next.IsServer,Demo.survivors,Demo.hands,Demo.survivorPreset,Demo.handPreset);
            if(next.IsServer&&session.Modes){session.Modes.ResumeAuthorityFromFrame();session.Modes.ReconcileHumanRoster(next.ActivePlayers.Select(p=>p.ToString()));}
            AttachCallbacks();lastWorld=Time.unscaledTime;UpdatePopulation();
        }
        void AttachCallbacks()
        {
            callbacks=new NetworkDelegates();callbacks.OnReliableDataReceived=Receive;
            callbacks.OnPlayerLeft=(_,player)=>RemovePlayer(player);callbacks.OnPlayerJoined=(_,player)=>UpdatePopulation();
            callbacks.OnShutdown=(_,reason)=>{if(reason!=ShutdownReason.HostMigration&&!(IslandRoomConnection.Instance&&IslandRoomConnection.Instance.Migrating))ReturnToLobby();};
            runner.AddCallbacks(callbacks);
        }
        public void Initialize(NetworkRunner networkRunner, CastlePlaySession owner, CastleLanSession state)
        {
            ResetPoseWindows();runner = networkRunner; session = owner; castle = state;
            posesInFlight.Traffic.PeerId=networkRunner.LocalPlayer.ToString();
            sceneTag = SceneTag(runner);
            // Fusion disables Unity's automatic physics when this component starts.
            // The castle's flight motor and contact hands still require 3D simulation.
            var physics = runner.GetComponent<Fusion.Addons.Physics.RunnerSimulatePhysics>();
            if (physics) physics.Update3DPhysicsScene = true;
            castle.InitializeFusion(owner, runner.LocalPlayer.ToString(), !runner.IsServer);
            AttachCallbacks();
            lastWorld = Time.unscaledTime;
            UpdatePopulation();
        }

        void Send(PlayerRef player, int kind, byte[] bytes, int serial = 0)
        {
            if(!IsCurrentScene)return;
            var key = ReliableKey.FromInts(Channel, kind, serial, sceneTag);
            if (runner.IsServer) runner.SendReliableDataToPlayer(player, key, bytes);
            else runner.SendReliableDataToServer(key, bytes);
            if(kind==World){WorldTransfersSubmitted++;WorldPayloadBytesSubmitted+=bytes.Length;}
        }

        void SendSocialPacket(PlayerRef player,int kind,int serial,byte[] data)
        {
            if(!IsCurrentScene)return;
            var key=ReliableKey.FromInts(SocialChannel,kind,serial,sceneTag);
            if(runner.IsServer)runner.SendReliableDataToPlayer(player,key,data);else runner.SendReliableDataToServer(key,data);
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
            if (closing || source != runner || !IsCurrentScene || (receivedTag != 0 && receivedTag != sceneTag)) return;
            if (runner.IsServer && !source.ActivePlayers.Contains(player)) return;
            if(channel==SocialChannel){ReceiveSocial(player,kind,serial,payload);return;}
            if(channel!=Channel)return;
            if (runner.IsServer && !source.ActivePlayers.Contains(player)) return;
            // Castle snapshots are bounded by the protocol; reject oversized messages before copying.
            if (payload.Length > 2 * 1024 * 1024) return;
            try
            {
                if (runner.IsServer)
                {
                    if (kind == Ready)
                    {
                        if (!peers.ContainsKey(player))
                        {
                            var added=new Peer {lastPose=Time.unscaledTime,welcome=castle.FusionWelcome(player.ToString())};
                            added.posesInFlight.Traffic.PeerId=player.ToString();peers.Add(player,added);
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
                            var rawPose=LivePoseCodec.Unpack(payload.ToArray(),MaxPoseSize);peer.posesInFlight.Traffic.RawReceivedBytes+=rawPose.Length;
                            if (LivePoseWindow.Newer(serial,peer.lastPoseSequence,peer.hasPoseSequence))
                            {
                                castle.FusionReceivePlayer(player.ToString(),rawPose);
                                peer.lastPoseSequence = serial;peer.hasPoseSequence=true;
                                peer.lastPose = Time.unscaledTime;
                                peer.posesInFlight.Applied(payload.Length,Time.unscaledTime);
                            }
                            else peer.posesInFlight.Skipped();
                            peer.posesInFlight.Received(payload.Length,Time.unscaledTime);
                            peer.receipts.Add(serial);peer.posesInFlight.Traffic.PendingReceiptCount=peer.receipts.Count;
                        }
                        else if (kind == PoseReceiptGroup&&payload.Length>=7&&payload.Length<=3+LivePoseReceipts.Capacity*4) ApplyPoseReceipts(peer.posesInFlight,payload.ToArray(),Time.unscaledTime);
                        else if (kind == Ack && peer.waiting == serial) peer.waiting = 0;
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
                        castle.FusionApplyWorld(CastleFusionWire.Decompress(payload.ToArray())); lastWorld = Time.unscaledTime;
                        Send(default, Ack, Array.Empty<byte>(), serial);
                    }
                    else if (kind == LivePoses && welcomed && payload.Length <= MaxLiveBatchSize+LivePoseCodec.HeaderBytes)
                    {
                        var rawPoses=LivePoseCodec.Unpack(payload.ToArray(),MaxLiveBatchSize);posesInFlight.Traffic.RawReceivedBytes+=rawPoses.Length;
                        if (LivePoseWindow.Newer(serial,lastPoseBatch,hasPoseBatch) && !castle.WaitingForWorld)
                        {
                            castle.FusionApplyLivePoses(rawPoses, runner.ActivePlayers.Select(p => p.ToString()));
                            lastPoseBatch = serial;hasPoseBatch=true;posesInFlight.Applied(payload.Length,Time.unscaledTime);
                        }
                        else posesInFlight.Skipped();
                        posesInFlight.Received(payload.Length,Time.unscaledTime);
                        poseReceipts.Add(serial);posesInFlight.Traffic.PendingReceiptCount=poseReceipts.Count;
                    }
                    else if (kind == PoseReceiptGroup&&payload.Length>=7&&payload.Length<=3+LivePoseReceipts.Capacity*4) ApplyPoseReceipts(posesInFlight,payload.ToArray(),Time.unscaledTime);
                    else if(kind==MatchState&&welcomed&&payload.Length<=512000)ReceiveMatch(payload);
                    else if(kind==ModeOutcome&&welcomed&&payload.Length<=1024)ReceiveOutcome(payload);
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
                Debug.LogError("Castle synchronization rejected a message: " + error.Message, this);
                if (runner.IsServer) runner.Disconnect(player);
                else Fail("Game synchronization failed. Make sure everyone has the same build, then rejoin.");
            }
        }

        void LateUpdate()
        {
            if (closing || poseSendFailed || runner == null || !runner.IsRunning || !IsCurrentScene) return;
            castle.FusionRender();ConnectModes();
            float now = Time.unscaledTime;
            FlushPoseReceipts(now);if(poseSendFailed)return;
            PumpPoses(now);if(poseSendFailed)return;
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
                if (now < nextSend) return;
                nextSend = now + .1f;
                FlushDisasterImpacts();
                CaptureDisasters();CaptureMatch();
                var bots=session.SurvivorBots;var hands=session.HandBots;
                Demo=new DemoState{leader=IslandRoomConnection.Instance?IslandRoomConnection.Instance.LeaderId:"",wave=CaptureWave(),shared=IslandRoomConnection.Instance&&IslandRoomConnection.Instance.SharedDemoControls,survivors=bots?.Count??0,hands=hands?.Count??0,
                    autoFill=bots?.AutoFill??false,handLimit=hands?.Capacity??0,capacity=bots?.Capacity??0,target=bots?.TargetPopulation??0,survivorPreset=bots?.SelectedPreset??0,handPreset=hands?.SelectedPreset??0,survivorName=bots?.SelectedName??"",handName=hands?.SelectedName??""};
                var room=IslandRoomConnection.Instance;
                Demo.roomNotice=room&&room.Owns(runner)?room.MatchNotice:"";
                castle.FusionRoomNotice(Demo.roomNotice);
                string admin=JsonUtility.ToJson(Demo);
                byte[] world = null;
                foreach (var pair in peers)
                {
                    if (!pair.Value.ready || pair.Value.waiting != 0) continue;
                    if(matchPacket!=null&&!ReferenceEquals(pair.Value.match,matchPacket)){Send(pair.Key,MatchState,matchPacket);pair.Value.match=matchPacket;}
                    if(disasterVisualPacket!=null&&!ReferenceEquals(pair.Value.disasters,disasterVisualPacket)){Send(pair.Key,DisasterVisuals,disasterVisualPacket);pair.Value.disasters=disasterVisualPacket;}
                    if(pair.Value.admin!=admin){Send(pair.Key,Admin,System.Text.Encoding.UTF8.GetBytes(admin));pair.Value.admin=admin;}
                    string rules=MatchSetup.CaptureRules(session.Player);if(pair.Value.rules!=rules){Send(pair.Key,Rules,System.Text.Encoding.UTF8.GetBytes(rules));pair.Value.rules=rules;}
                    world ??= CastleFusionWire.Compress(castle.FusionCaptureWorld());
                    pair.Value.waiting = ++sequence; pair.Value.sentAt = now;
                    Send(pair.Key, World, world, pair.Value.waiting);
                }
            }
            else if (now >= nextSend)
            {
                nextSend = now + 1;
                if (!welcomed) Send(default, Ready, Array.Empty<byte>());
                if(posesInFlight.Expired(now)){Fail("Live pose acknowledgements timed out. Rejoin the room.");return;}
                if (now - lastWorld > (welcomed ? 20 : 60)) Fail("The host stopped responding. Check your connection and rejoin the room.");
            }
        }

        void UpdatePopulation()
        {
            if (runner.IsServer) session.SurvivorBots?.SetHumanPlayerCount(runner.ActivePlayers.Count());
        }

        void RemovePlayer(PlayerRef player)
        {
            if(peers.TryGetValue(player,out var departing)){departing.posesInFlight.Clear();departing.receipts.Clear();}
            modeRuntime?.UnregisterRemote(player.ToString());peers.Remove(player); socialPeers.Remove(player); castle.FusionRemovePlayer(player.ToString());
            if (runner.IsServer) session.SurvivorBots?.SetHumanPlayerCount(runner.ActivePlayers.Count(p => p != player));
        }

        public void Leave()
        {
            if (closing) return;
            ResetPoseWindows();ResetDisasters();
            var island=IslandRoomConnection.Instance;if(island&&island.Owns(runner))_=island.Leave();else _=runner.Shutdown();
        }

        void Fail(string message)
        {
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
            DisconnectModes();ResetPoseWindows();
            ResetDisasters();
            if (runner != null && callbacks != null) runner.RemoveCallbacks(callbacks);
        }
    }
}
