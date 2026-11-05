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
        sealed class Peer { public bool ready; public string rules; public byte[] welcome; public int waiting; public float lastPose, sentAt, lastCommand; public string admin; public byte[] disasters; public byte[] match; public float lastModeCommand; public int mysteryRound=-1,trailCount; }
        readonly Dictionary<PlayerRef, Peer> peers = new Dictionary<PlayerRef, Peer>();
        NetworkRunner runner;
        NetworkDelegates callbacks;
        CastleLanSession castle;
        CastlePlaySession session;
        int sequence, poseSequence, sceneTag,botCursor;
        // The same runner survives map reloads, so old reliable packets can reach a new session.
        // Zero is reserved for legacy senders and the standalone Fusion prototype.
        static int SceneTag(NetworkRunner source)
        {
            var room=IslandRoomConnection.Instance;
            return source&&room&&room.Owns(source)?source.SceneInfo.Version+1:0;
        }
        bool IsCurrentScene=>sceneTag==SceneTag(runner);
        float nextPoseSend,nextBotPoseSend;
        CastlePoseChannel.Binding livePoses;
        public CastlePoseChannel.Stats PoseTraffic => livePoses?.stats;
        public long LiveActorsApplied {get;private set;}
        public float LastActorAppliedAt {get;private set;}
        public long WorldPayloadBytesQueued {get;private set;}
        public long WorldTransfersQueued {get;private set;}
        void BindLivePoses()
        {
            CastlePoseChannel.Unbind(runner,CastlePoseChannel.Game,this);
            livePoses=CastlePoseChannel.Bind(runner,CastlePoseChannel.Game,sceneTag,this,
                (source,slot)=>!closing&&IsCurrentScene&&(runner.IsServer?
                    slot==0&&runner.ActivePlayers.Contains(source)&&peers.TryGetValue(source,out var peer)&&peer.ready:
                    source==PlayerRef.None&&welcomed&&!castle.WaitingForWorld),ReceiveLivePose);
        }
        void ReceiveLivePose(PlayerRef source,int slot,byte[] data)
        {
            try
            {
                var actor=CastlePoseChannel.Decode(data);
                if(runner.IsServer)
                {
                    if(actor.kind!=DeadlyJenga.Core.NetworkKinds.Survivor&&actor.kind!=DeadlyJenga.Core.NetworkKinds.Hand)throw new System.IO.InvalidDataException("Unknown player pose kind");
                    castle.FusionReceivePlayer(source.ToString(),data);peers[source].lastPose=Time.unscaledTime;
                }
                else if(!castle.FusionApplyLiveActor(actor))return;
                LiveActorsApplied++;LastActorAppliedAt=Time.unscaledTime;
                livePoses.stats.Applied(runner.IsServer?source.ToString():actor.id,LastActorAppliedAt);
            }
            catch(Exception error)
            {
                Debug.LogError("Castle live pose rejected: "+error.Message,this);
                if(runner.IsServer)runner.Disconnect(source);else Fail("Game pose synchronization failed. Use matching builds and rejoin.");
            }
        }
        int NextPoseSequence(){poseSequence=unchecked(poseSequence+1);if(poseSequence==0)poseSequence=1;return poseSequence;}
        void PumpPoses(float now)
        {
            if(now<nextPoseSend)return;
            nextPoseSend=now+1f/30;
            if(!runner.IsServer)
            {
                if(welcomed&&!castle.WaitingForWorld)CastlePoseChannel.Send(runner,CastlePoseChannel.Game,livePoses,PlayerRef.None,0,NextPoseSequence(),castle.FusionCapturePlayer());
                return;
            }
            if(!peers.Any(pair=>pair.Value.ready))return;
            bool botsDue=now>=nextBotPoseSend;if(botsDue)nextBotPoseSend=now+.1f;
            var actors=castle.FusionCaptureLiveActors(false);
            var humans=new HashSet<string>(runner.ActivePlayers.Select(player=>player.ToString()));
            for(int slot=0;slot<actors.Length&&slot<CastlePoseChannel.MaxActors;slot++)
                if(humans.Contains(actors[slot].id))SendLiveActor(actors[slot],slot);
            if(!botsDue)return;
            // Capture and queue optional bots only after the human samples are submitted.
            actors=castle.FusionCaptureLiveActors();var bots=new List<int>();
            for(int slot=0;slot<actors.Length&&slot<CastlePoseChannel.MaxActors;slot++)if(!humans.Contains(actors[slot].id))bots.Add(slot);
            int budget=16384;
            for(int count=0;botsDue&&count<bots.Count;count++)
            {
                int index=botCursor%bots.Count,slot=bots[index];
                var data=CastlePoseChannel.Encode(actors[slot]);
                if(data!=null&&data.Length>budget)break;
                botCursor=(index+1)%bots.Count;
                if(data==null)continue;
                budget-=data.Length;SendLiveData(data,slot);
            }
        }
        void SendLiveActor(CastleLanProtocol.Actor actor,int slot)
        {var data=CastlePoseChannel.Encode(actor);if(data!=null)SendLiveData(data,slot);}
        void SendLiveData(byte[] data,int slot)
        {
            int serial=NextPoseSequence();
            foreach(var pair in peers)if(pair.Value.ready)CastlePoseChannel.Send(runner,CastlePoseChannel.Game,livePoses,pair.Key,slot,serial,data);
        }
        bool welcomed, closing, leavingIntentionally;
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
        public void PauseForMigration(){CastlePoseChannel.Unbind(runner,CastlePoseChannel.Game,this);ResetDisasters();closing=true;pendingStamina=null;if(runner&&callbacks!=null)runner.RemoveCallbacks(callbacks);castle.FusionFreeze();}
        public void ResumeAfterMigration(NetworkRunner next)
        {
            peers.Clear();socialPeers.Clear();socialServer.Clear();nextPoseSend=nextBotPoseSend=0;botCursor=0;welcomed=false;closing=false;nextSend=0;
            runner=next;sceneTag=SceneTag(next);if(next.IsServer&&session.SurvivorBots!=null){if(Demo.target>0)session.SurvivorBots.TargetPopulation=Mathf.Max(1,Demo.target);session.SurvivorBots.AutoFill=Demo.autoFill;}castle.FusionRebind(next.LocalPlayer.ToString(),!next.IsServer,Demo.survivors,Demo.hands,Demo.survivorPreset,Demo.handPreset);
            if(next.IsServer&&session.Modes){session.Modes.ResumeAuthorityFromFrame();session.Modes.ReconcileHumanRoster(next.ActivePlayers.Select(p=>p.ToString()));}
            AttachCallbacks();lastWorld=Time.unscaledTime;UpdatePopulation();
        }
        void AttachCallbacks()
        {
            callbacks=new NetworkDelegates();callbacks.OnReliableDataReceived=Receive;
            callbacks.OnPlayerLeft=(_,player)=>RemovePlayer(player);callbacks.OnPlayerJoined=(_,player)=>UpdatePopulation();
            callbacks.OnShutdown=(_,reason)=>{if(reason!=ShutdownReason.HostMigration&&!(IslandRoomConnection.Instance&&IslandRoomConnection.Instance.Migrating))ReturnToLobby();};
            runner.AddCallbacks(callbacks);BindLivePoses();
        }
        public void Initialize(NetworkRunner networkRunner, CastlePlaySession owner, CastleLanSession state)
        {
            runner = networkRunner; session = owner; castle = state;
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
            if(kind==World){WorldTransfersQueued++;WorldPayloadBytesQueued+=bytes.Length;}
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
                if (now - lastWorld > (welcomed ? 20 : 60)) Fail("The host stopped responding. Check your connection and rejoin the room.");
            }
        }

        void UpdatePopulation()
        {
            if (runner.IsServer) session.SurvivorBots?.SetHumanPlayerCount(runner.ActivePlayers.Count());
        }

        void RemovePlayer(PlayerRef player)
        {
            CastlePoseChannel.Forget(livePoses,player);modeRuntime?.UnregisterRemote(player.ToString());peers.Remove(player); socialPeers.Remove(player); castle.FusionRemovePlayer(player.ToString());
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
            CastlePoseChannel.Unbind(runner,CastlePoseChannel.Game,this);
            DisconnectModes();
            ResetDisasters();
            if (runner != null && callbacks != null) runner.RemoveCallbacks(callbacks);
        }
    }
}
