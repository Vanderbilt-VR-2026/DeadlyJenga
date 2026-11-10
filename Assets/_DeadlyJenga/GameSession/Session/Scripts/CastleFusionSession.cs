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
        const int LivePose = 20, LivePoses = 21, PoseReceipt = 22;
        const int PoseWindow = 6, MaxPoseSize = 4096;
        sealed class Peer { public readonly HashSet<int> posesInFlight = new HashSet<int>(); public int lastPoseSequence; public bool ready; public string rules; public byte[] welcome; public int waiting; public float lastPose, sentAt, lastCommand; public string admin; public byte[] disasters; public byte[] match; public float lastModeCommand; public int mysteryRound=-1,trailCount; public string secret; public readonly CastleLanProtocol.WorldBaseline baseline=new CastleLanProtocol.WorldBaseline(); }
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
        readonly HashSet<int> posesInFlight = new HashSet<int>();

        // The reliable copy is only a fallback for lost fast packets; at 5 Hz it crowded the stream that carries world and match updates.
        const float FastPoseInterval = 1f / 30, ReliablePoseInterval = 1f;
        float nextReliablePose;
        readonly List<byte[]> poseParts = new List<byte[]>();
        static CastleFusionSession fastLane;
        void PumpPoses(float now)
        {
            if (now < nextPoseSend) return;
            nextPoseSend = now + FastPoseInterval;
            bool reliableDue = now >= nextReliablePose;
            if (reliableDue) nextReliablePose = now + ReliablePoseInterval;
            if (!runner.IsServer)
            {
                if (!welcomed || castle.WaitingForWorld) return;
                byte[] packet = castle.FusionCapturePlayer();
                if (packet.Length > MaxPoseSize) return;
                int serial = ++poseSequence;
                SendFast(PlayerRef.None, serial, packet);
                if (reliableDue && posesInFlight.Count < PoseWindow)
                {
                    posesInFlight.Add(serial);
                    Send(default, LivePose, packet, serial);
                }
                return;
            }

            bool anyReady = false;
            foreach (var pair in peers) if (pair.Value.ready) { anyReady = true; break; }
            if (!anyReady) return;
            castle.FusionCaptureLivePoseParts(poseParts);
            foreach (var pair in peers)
            {
                if (!pair.Value.ready) continue;
                foreach (var part in poseParts) SendFast(pair.Key, ++poseSequence, part);
            }
            if (!reliableDue) return;
            byte[] batch = null;
            foreach (var pair in peers)
            {
                if (!pair.Value.ready || pair.Value.posesInFlight.Count >= PoseWindow) continue;
                batch ??= castle.FusionCaptureLivePoses();
                int serial = ++poseSequence;
                pair.Value.posesInFlight.Add(serial);
                Send(pair.Key, LivePoses, batch, serial);
            }
        }
        // [serial][scene tag][chunk index][chunk count][bytes]. Characters larger than one unreliable
        // message go out in a few chunks; a pose missing any chunk is dropped and the next one replaces it.
        const int FastHeader = 10, FastChunk = CastleFusionFastLane.MaxPayload - FastHeader, MaxFastChunks = 8;
        void SendFast(PlayerRef target, int serial, byte[] pose)
        {
            if (!IsCurrentScene) return;
            int chunks = (pose.Length + FastChunk - 1) / FastChunk;
            if (chunks < 1 || chunks > MaxFastChunks) return;
            try
            {
                for (int c = 0; c < chunks; c++)
                {
                    int offset = c * FastChunk, length = Mathf.Min(FastChunk, pose.Length - offset);
                    var data = new byte[FastHeader + length];
                    BitConverter.TryWriteBytes(new Span<byte>(data, 0, 4), serial);
                    BitConverter.TryWriteBytes(new Span<byte>(data, 4, 4), sceneTag);
                    data[8] = (byte)c; data[9] = (byte)chunks;
                    Buffer.BlockCopy(pose, offset, data, FastHeader, length);
                    CastleFusionFastLane.RpcLivePose(runner, target, data);
                }
            }
            catch (Exception error) { Debug.LogWarning("Fast pose lane unavailable: " + error.Message); }
        }
        sealed class FastAssembly { public int serial; public byte[][] parts; public int received; }
        readonly Dictionary<int, FastAssembly> fastAssemblies = new Dictionary<int, FastAssembly>();
        byte[] AssembleFast(PlayerRef player, byte[] data, out int serial)
        {
            serial = BitConverter.ToInt32(data, 0);
            int index = data[8], count = data[9];
            if (count < 1 || count > MaxFastChunks || index >= count) return null;
            var chunk = new byte[data.Length - FastHeader];
            Buffer.BlockCopy(data, FastHeader, chunk, 0, chunk.Length);
            if (count == 1) return chunk;
            int sender = player.PlayerId;
            if (!fastAssemblies.TryGetValue(sender, out var a)) fastAssemblies[sender] = a = new FastAssembly { serial = int.MinValue };
            if (serial < a.serial) return null;
            if (serial != a.serial || a.parts == null || a.parts.Length != count) { a.serial = serial; a.parts = new byte[count][]; a.received = 0; }
            if (a.parts[index] != null) return null;
            a.parts[index] = chunk; a.received++;
            if (a.received < count) return null;
            int total = 0; foreach (var part in a.parts) total += part.Length;
            var pose = new byte[total]; int at = 0;
            foreach (var part in a.parts) { Buffer.BlockCopy(part, 0, pose, at, part.Length); at += part.Length; }
            a.parts = null; a.received = 0;
            return pose;
        }
        internal static void ReceiveFastPose(NetworkRunner source, PlayerRef player, byte[] data)
        {
            var self = fastLane;
            if (!self || self.closing || source != self.runner || data == null || data.Length <= FastHeader || data.Length > CastleFusionFastLane.MaxPayload || !self.IsCurrentScene) return;
            int tag = BitConverter.ToInt32(data, 4);
            if (tag != 0 && tag != self.sceneTag) return;
            var pose = self.AssembleFast(player, data, out int serial);
            if (pose == null) return;
            try
            {
                if (self.runner.IsServer)
                {
                    if (player == PlayerRef.None || !self.peers.TryGetValue(player, out var peer) || !peer.ready || serial <= peer.lastPoseSequence) return;
                    self.castle.FusionReceivePlayer(player.ToString(), pose);
                    peer.lastPoseSequence = serial; peer.lastPose = Time.unscaledTime;
                }
                else
                {
                    if (!self.welcomed || self.castle.WaitingForWorld || serial <= self.lastPoseBatch) return;
                    self.castle.FusionApplyLivePoses(pose, self.runner.ActivePlayers.Select(p => p.ToString()));
                    self.lastPoseBatch = serial;
                }
            }
            catch (Exception error) { Debug.LogWarning("Dropped a live pose: " + error.Message); }
        }
        bool welcomed, closing, leavingIntentionally;
        float nextSend, lastWorld;

        [Serializable] public sealed class DemoState
        {public DeadlyJenga.Core.WaveFrame wave;public bool shared,autoFill;public int survivors,hands,capacity,target,survivorPreset,handPreset,handLimit;public string survivorName,handName,leader,roomNotice;}
        public DemoState Demo {get;private set;}=new DemoState();
        public bool CanUseDemoControls=>SessionDevelopmentPolicy.DemoEnabled(session)&&runner&&runner.IsRunning&&(IslandRoomConnection.Instance?IslandRoomConnection.Instance.HasDemoPermission(runner.LocalPlayer.ToString()):runner.IsServer);
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
        public void PauseForMigration(){ResetDisasters();closing=true;pendingStamina=null;if(runner&&callbacks!=null)runner.RemoveCallbacks(callbacks);castle.FusionFreeze();}
        public void ResumeAfterMigration(NetworkRunner next)
        {
            peers.Clear();socialPeers.Clear();socialServer.Clear();posesInFlight.Clear();fastAssemblies.Clear();lastPoseBatch=0;nextPoseSend=0;welcomed=false;closing=false;nextSend=0;
            runner=next;sceneTag=SceneTag(next);fastLane=this;ConfigureTransport(next);if(next.IsServer&&session.SurvivorBots!=null){if(Demo.target>0)session.SurvivorBots.TargetPopulation=Mathf.Max(1,Demo.target);session.SurvivorBots.AutoFill=Demo.autoFill;}castle.FusionRebind(next.LocalPlayer.ToString(),!next.IsServer,Demo.survivors,Demo.hands,Demo.survivorPreset,Demo.handPreset);
            if(next.IsServer&&session.Modes){session.Modes.ResumeAuthorityFromFrame();session.Modes.ReconcileHumanRoster(next.ActivePlayers.Select(p=>p.ToString()));}
            AttachCallbacks();lastWorld=Time.unscaledTime;UpdatePopulation();
        }
        // Reliable data defaults to a low send rate; world and match updates queue up behind it.
        static void ConfigureTransport(NetworkRunner target)
        {
            if (!target) return;
            for (int hz = 64; hz >= 8; hz /= 2)
            {
                try { target.ReliableDataSendRate = hz; return; }
                catch (Exception) { }
            }
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
            runner = networkRunner; session = owner; castle = state; fastLane = this;
            ConfigureTransport(runner);
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
                            peers.Add(player, new Peer { lastPose = Time.unscaledTime, welcome = castle.FusionWelcome(player.ToString()) });
                        if (!peers[player].ready) { peers[player].baseline.Reset(); Send(player, Welcome, peers[player].welcome); }
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
                        else if (kind == LivePose && peer.ready && payload.Length <= MaxPoseSize)
                        {
                            if (serial > peer.lastPoseSequence)
                            {
                                castle.FusionReceivePlayer(player.ToString(), payload.ToArray());
                                peer.lastPoseSequence = serial;
                                peer.lastPose = Time.unscaledTime;
                            }
                            Send(player, PoseReceipt, Array.Empty<byte>(), serial);
                        }
                        else if (kind == PoseReceipt) peer.posesInFlight.Remove(serial);
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
                    else if (kind == LivePoses && welcomed && payload.Length <= MaxPoseSize * 64)
                    {
                        if (serial > lastPoseBatch && !castle.WaitingForWorld)
                        {
                            castle.FusionApplyLivePoses(payload.ToArray(), runner.ActivePlayers.Select(p => p.ToString()));
                            lastPoseBatch = serial;
                        }
                        Send(default, PoseReceipt, Array.Empty<byte>(), serial);
                    }
                    else if (kind == PoseReceipt) posesInFlight.Remove(serial);
                    else if(kind==MatchState&&welcomed&&payload.Length<=512000)ReceiveMatch(payload);
                    else if(kind==ModeOutcome&&welcomed&&payload.Length<=1024)ReceiveOutcome(payload);
                    else if(kind==MysterySecret&&welcomed&&payload.Length<=8192){ConnectModes();modeRuntime?.ReceiveSecret(System.Text.Encoding.UTF8.GetString(payload.ToArray()));}
                    else if(kind==MysteryTrails&&welcomed&&payload.Length<=64000){ConnectModes();modeRuntime?.ReceiveTrails(System.Text.Encoding.UTF8.GetString(payload.ToArray()));}
                    else if(kind==Admin&&welcomed&&payload.Length<2048)
                    {Demo=JsonUtility.FromJson<DemoState>(System.Text.Encoding.UTF8.GetString(payload.ToArray()));ReceiveWave(Demo.wave);var room=IslandRoomConnection.Instance;if(room&&room.Owns(runner))room.ReceiveMatchPermissions(Demo.leader,Demo.shared);castle.FusionRoomNotice(Demo.roomNotice);}
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
            if (closing || runner == null || !runner.IsRunning || !IsCurrentScene) return;
            castle.FusionRender();ConnectModes();
            float now = Time.unscaledTime;
            PumpPoses(now);
            if(pendingStamina!=null&&now>=nextStamina){if(SessionDevelopmentPolicy.CanRequestLocal(session))Send(default,Command,System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(pendingStamina)));pendingStamina=null;nextStamina=now+.2f;}
            if(runner.IsServer){foreach(var pair in socialPeers)if(peers.TryGetValue(pair.Key,out var ready)&&ready.ready)pair.Value.Pump(now,(serial,data)=>SendSocialPacket(pair.Key,1,serial,data));}
            else if(welcomed)socialServer.Pump(now,(serial,data)=>SendSocialPacket(default,1,serial,data));
            if (runner.IsServer)
            {
                foreach (var pair in peers)
                    if (now - pair.Value.lastPose > 1) castle.FusionSuspendPlayer(pair.Key.ToString());
                foreach (var pair in peers.ToArray())
                    if (now - pair.Value.lastPose > 20 || pair.Value.waiting != 0 && now - pair.Value.sentAt > 20)
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
                CastleLanProtocol.Snapshot worldState = null;
                foreach (var pair in peers)
                {
                    if (!pair.Value.ready || pair.Value.waiting != 0) continue;
                    if(matchPacket!=null&&!ReferenceEquals(pair.Value.match,matchPacket)){Send(pair.Key,MatchState,matchPacket);pair.Value.match=matchPacket;}
                    if(disasterVisualPacket!=null&&!ReferenceEquals(pair.Value.disasters,disasterVisualPacket)){Send(pair.Key,DisasterVisuals,disasterVisualPacket);pair.Value.disasters=disasterVisualPacket;}
                    if(pair.Value.admin!=admin){Send(pair.Key,Admin,System.Text.Encoding.UTF8.GetBytes(admin));pair.Value.admin=admin;}
                    string rules=MatchSetup.CaptureRules(session.Player);if(pair.Value.rules!=rules){Send(pair.Key,Rules,System.Text.Encoding.UTF8.GetBytes(rules));pair.Value.rules=rules;}
                    worldState ??= castle.FusionCaptureSnapshot();
                    var world = CastleFusionWire.Compress(castle.FusionEncodeWorldFor(worldState, pair.Value.baseline));
                    pair.Value.waiting = ++sequence; pair.Value.sentAt = now;
                    Send(pair.Key, World, world, pair.Value.waiting);
                }
            }
            else if (now >= nextSend)
            {
                nextSend = now + 1;
                if (!welcomed) Send(default, Ready, Array.Empty<byte>());
                if (now - lastWorld > (welcomed ? 20 : 60)) Fail("The host stopped responding. Check your connection and rejoin the room.");
            }
        }

        void UpdatePopulation()
        {
            if (runner.IsServer) session.SurvivorBots?.SetHumanPlayerCount(runner.ActivePlayers.Count());
        }

        void RemovePlayer(PlayerRef player)
        {
            modeRuntime?.UnregisterRemote(player.ToString());peers.Remove(player); socialPeers.Remove(player); castle.FusionRemovePlayer(player.ToString());
            if (runner.IsServer) session.SurvivorBots?.SetHumanPlayerCount(runner.ActivePlayers.Count(p => p != player));
        }

        public void Leave()
        {
            if (closing) return;
            ResetDisasters();
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
            closing = true;
            var island=IslandRoomConnection.Instance;
            if(island&&island.Owns(runner))return;
            // The prototype also owns persistent templates and needs its reset path.
            var bootstrap = FindFirstObjectByType<FusionBootstrap>();
            if (bootstrap != null) bootstrap.Shutdown();
            else SceneManager.LoadScene("Lobby");
        }

        void OnDestroy()
        {
            if (fastLane == this) fastLane = null;
            DisconnectModes();
            ResetDisasters();
            if (runner != null && callbacks != null) runner.RemoveCallbacks(callbacks);
        }
    }
}
