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
        const int Channel = 0x43415354;
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

        void Receive(NetworkRunner source, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> payload)
        {
            key.GetInts(out int channel, out int kind, out int serial, out _);
            if (channel != Channel || closing) return;
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
            peers.Remove(player); castle.FusionRemovePlayer(player.ToString());
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
