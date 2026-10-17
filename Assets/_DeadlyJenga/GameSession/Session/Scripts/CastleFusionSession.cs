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
    public sealed class CastleFusionSession : MonoBehaviour
    {
        const int Channel = 0x43415354, SocialChannel=0x534F434C;
        readonly Dictionary<PlayerRef,CastleSocialChannel> socialPeers=new Dictionary<PlayerRef,CastleSocialChannel>();
        readonly CastleSocialChannel socialServer=new CastleSocialChannel();
        const int Ready = 1, Welcome = 2, World = 3, Pose = 4, Ack = 5, WelcomeAck = 6;
        sealed class Peer { public bool ready; public byte[] welcome; public int waiting; public float lastPose, sentAt; }
        readonly Dictionary<PlayerRef, Peer> peers = new Dictionary<PlayerRef, Peer>();
        NetworkRunner runner;
        NetworkDelegates callbacks;
        CastleLanSession castle;
        CastlePlaySession session;
        int sequence, waitingPose;
        bool welcomed, closing;
        float nextSend, lastWorld;

        public void Initialize(NetworkRunner networkRunner, CastlePlaySession owner, CastleLanSession state)
        {
            runner = networkRunner; session = owner; castle = state;
            castle.InitializeFusion(owner, runner.LocalPlayer.ToString(), !runner.IsServer);
            callbacks = new NetworkDelegates();
            callbacks.OnReliableDataReceived = Receive;
            callbacks.OnPlayerLeft = (_, player) => RemovePlayer(player);
            callbacks.OnPlayerJoined = (_, player) => UpdatePopulation();
            callbacks.OnShutdown = (_, reason) => ReturnToLobby();
            runner.AddCallbacks(callbacks);
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
                        if (kind == WelcomeAck) peer.ready = true;
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
                        if (!welcomed) { castle.FusionReceiveWelcome(payload.ToArray()); welcomed = true; }
                        Send(default, WelcomeAck, Array.Empty<byte>());
                    }
                    else if (kind == World && welcomed)
                    {
                        castle.FusionApplyWorld(CastleFusionWire.Decompress(payload.ToArray())); lastWorld = Time.unscaledTime;
                        Send(default, Ack, Array.Empty<byte>(), serial);
                    }
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
                byte[] world = null;
                foreach (var pair in peers)
                {
                    if (!pair.Value.ready || pair.Value.waiting != 0) continue;
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
            runner.Shutdown();
        }

        void ReturnToLobby()
        {
            if (closing) return;
            closing = true;
            // The prototype also owns persistent templates and needs its reset path.
            var bootstrap = FindFirstObjectByType<FusionBootstrap>();
            if (bootstrap != null) bootstrap.Shutdown();
            else SceneManager.LoadScene("Lobby");
        }

        void OnDestroy()
        {
            if (runner != null && callbacks != null) runner.RemoveCallbacks(callbacks);
        }
    }
}
