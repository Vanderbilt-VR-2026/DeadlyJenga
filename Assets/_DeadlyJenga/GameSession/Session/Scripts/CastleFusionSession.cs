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
    // One outstanding world/pose per peer bounds reliable-stream backpressure.
    public sealed partial class CastleFusionSession : MonoBehaviour
    {
        const int Channel = 0x43415354, SocialChannel=0x534F434C;
        readonly Dictionary<PlayerRef,CastleSocialChannel> socialPeers=new Dictionary<PlayerRef,CastleSocialChannel>();
        readonly CastleSocialChannel socialServer=new CastleSocialChannel();
        const int Ready = 1, Welcome = 2, World = 3, Pose = 4, Ack = 5, WelcomeAck = 6, Rules=7, Command=8, Admin=9;
        sealed class Peer { public bool ready; public string rules; public byte[] welcome; public int waiting; public float lastPose, sentAt, lastCommand; public string admin; public byte[] disasters; }
        readonly Dictionary<PlayerRef, Peer> peers = new Dictionary<PlayerRef, Peer>();
        NetworkRunner runner;
        NetworkDelegates callbacks;
        CastleLanSession castle;
        CastlePlaySession session;
        int sequence, waitingPose;
        bool welcomed, closing;
        float nextSend, lastWorld;

        [Serializable] public sealed class DemoState
        {public DeadlyJenga.Core.WaveFrame wave;public bool shared,autoFill;public int survivors,hands,capacity,target,survivorPreset,handPreset,handLimit;public string survivorName,handName;}
        public DemoState Demo {get;private set;}=new DemoState();
        public bool CanUseDemoControls=>runner&&runner.IsRunning&&(runner.IsServer||MatchSetup.ModeId=="demo"&&Demo.shared);
        public bool IsHost=>runner&&runner.IsRunning&&runner.IsServer;
        public void SetSharedControls(bool value){var room=IslandRoomConnection.Instance;if(IsHost&&room)room.SetSharedControls(value);}
        CastleDemoCommand pendingStamina;float nextStamina;string authoritativeRules;
        public void Request(CastleDemoCommand command)
        {
            if(!CanUseDemoControls)return;
            if(IsHost)CastleDemoCommands.Execute(session,command);
            else if(command.action==CastleDemoAction.Stamina){pendingStamina=command;if(authoritativeRules!=null)MatchSetup.ApplyRules(session.Player,authoritativeRules);}
            else Send(default,Command,System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(command)));
        }
        public void PauseForMigration(){ResetDisasters();closing=true;pendingStamina=null;if(runner&&callbacks!=null)runner.RemoveCallbacks(callbacks);castle.FusionFreeze();}
        public void ResumeAfterMigration(NetworkRunner next)
        {
            peers.Clear();socialPeers.Clear();socialServer.Clear();waitingPose=0;welcomed=false;closing=false;nextSend=0;
            runner=next;if(next.IsServer){if(Demo.target>0)session.Bots.targetPopulation=Mathf.Max(1,Demo.target);if(Demo.handLimit>0)session.HandBots.maximumHands=0;session.Bots.autoFill=Demo.autoFill;}castle.FusionRebind(next.LocalPlayer.ToString(),!next.IsServer,Demo.survivors,Demo.hands,Demo.survivorPreset,Demo.handPreset);
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
            runner = networkRunner; session = owner; castle = state;
            castle.InitializeFusion(owner, runner.LocalPlayer.ToString(), !runner.IsServer);
            AttachCallbacks();
            lastWorld = Time.unscaledTime;
            UpdatePopulation();
        }

        void Send(PlayerRef player, int kind, byte[] bytes, int serial = 0)
        {
            var key = ReliableKey.FromInts(Channel, kind, serial, 0);
            if (runner.IsServer) runner.SendReliableDataToPlayer(player, key, bytes);
            else runner.SendReliableDataToServer(key, bytes);
        }

        void SendSocialPacket(PlayerRef player,int kind,int serial,byte[] data)
        {
            var key=ReliableKey.FromInts(SocialChannel,kind,serial,0);
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
            key.GetInts(out int channel, out int kind, out int serial, out _);
            if (closing) return;
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
                        if(kind==Command&&peer.ready&&MatchSetup.ModeId=="demo"&&IslandRoomConnection.Instance&&IslandRoomConnection.Instance.SharedDemoControls&&payload.Length<=8192&&Time.unscaledTime-peer.lastCommand>=.15f)
                        {peer.lastCommand=Time.unscaledTime;CastleDemoCommands.Execute(session,JsonUtility.FromJson<CastleDemoCommand>(System.Text.Encoding.UTF8.GetString(payload.ToArray())));}
                        else if (kind == WelcomeAck) peer.ready = true;
                        else if (kind == Pose && peer.ready)
                        {
                            castle.FusionReceivePlayer(player.ToString(), payload.ToArray());
                            peer.lastPose = Time.unscaledTime;
                            Send(player, Ack, Array.Empty<byte>(), serial);
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
                    else if(kind==Admin&&welcomed&&payload.Length<2048)
                    {Demo=JsonUtility.FromJson<DemoState>(System.Text.Encoding.UTF8.GetString(payload.ToArray()));ReceiveWave(Demo.wave);IslandRoomConnection.Instance?.ReceiveSharedControls(Demo.shared);}
                    else if(kind==DisasterImpact&&welcomed&&payload.Length<512)ReceiveDisasterImpact(payload.ToArray());
                    else if(kind==DisasterVisuals&&welcomed&&payload.Length<256*1024)ReceiveDisasters(payload.ToArray());
                    else if(kind==Rules&&welcomed&&payload.Length<4096){authoritativeRules=System.Text.Encoding.UTF8.GetString(payload.ToArray());MatchSetup.ApplyRules(session.Player,authoritativeRules);}
                    else if (kind == Ack && waitingPose == serial) waitingPose = 0;
                }
            }
            catch (Exception error)
            {
                Debug.LogError("Castle synchronization rejected a message: " + error.Message, this);
                if (runner.IsServer) runner.Disconnect(player);
                else Leave();
            }
        }

        void LateUpdate()
        {
            if (closing || runner == null || !runner.IsRunning) return;
            castle.FusionRender();
            float now = Time.unscaledTime;
            if(pendingStamina!=null&&now>=nextStamina){if(CanUseDemoControls)Send(default,Command,System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(pendingStamina)));pendingStamina=null;nextStamina=now+.2f;}
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
                CaptureDisasters();
                Demo=new DemoState{wave=CaptureWave(),shared=IslandRoomConnection.Instance&&IslandRoomConnection.Instance.SharedDemoControls,survivors=session.Bots.Count,hands=session.HandBots.Count,
                    autoFill=session.Bots.autoFill,handLimit=session.HandBots.maximumHands,capacity=session.Bots.Capacity,target=session.Bots.targetPopulation,survivorPreset=session.Bots.SelectedPreset,handPreset=session.HandBots.SelectedPreset,survivorName=session.Bots.SelectedName,handName=session.HandBots.SelectedName};
                string admin=JsonUtility.ToJson(Demo);
                byte[] world = null;
                foreach (var pair in peers)
                {
                    if (!pair.Value.ready || pair.Value.waiting != 0) continue;
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
                nextSend = now + (welcomed ? 1f / 30 : 1f);
                if (!welcomed) Send(default, Ready, Array.Empty<byte>());
                else if (!castle.WaitingForWorld && waitingPose == 0)
                {
                    waitingPose = ++sequence;
                    Send(default, Pose, castle.FusionCapturePlayer(), waitingPose);
                }
                if (now - lastWorld > (welcomed ? 20 : 60)) Leave();
            }
        }

        void UpdatePopulation()
        {
            if (runner.IsServer) session.Bots.SetHumanPlayerCount(runner.ActivePlayers.Count());
        }

        void RemovePlayer(PlayerRef player)
        {
            peers.Remove(player); socialPeers.Remove(player); castle.FusionRemovePlayer(player.ToString());
            if (runner.IsServer) session.Bots.SetHumanPlayerCount(runner.ActivePlayers.Count(p => p != player));
        }

        public void Leave()
        {
            if (closing) return;
            ResetDisasters();
            var island=IslandRoomConnection.Instance;if(island&&island.Owns(runner))_=island.Leave();else _=runner.Shutdown();
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
            ResetDisasters();
            if (runner != null && callbacks != null) runner.RemoveCallbacks(callbacks);
        }
    }
}
