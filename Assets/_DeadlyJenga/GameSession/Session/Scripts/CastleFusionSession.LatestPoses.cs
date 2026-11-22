using System;
using System.Collections.Generic;
using System.IO;
using Fusion;
using UnityEngine;
using P = DeadlyJenga.CastlePrototype.CastleLanProtocol;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleFusionSession
    {
        internal const string LatestPoseProtocol = "/latest-game-pose-1";
        readonly Dictionary<string, CastleLatestPosePage> latestPages = new Dictionary<string, CastleLatestPosePage>();
        readonly Dictionary<string, int> latestActorSequences = new Dictionary<string, int>();
        CastleLatestPoseProvider latestProvider;
        CastleLatestPoseInput latestInput;
        int latestReadTick;
        bool readLatestTick, latestDownlinkSeen;

        void BindLatestPoses()
        {
            latestProvider = runner.GetComponent<CastleLatestPoseProvider>();
            if (latestProvider) latestProvider.Session = this;
        }

        void UnbindLatestPoses(bool despawn)
        {
            if (latestProvider && latestProvider.Session == this)
            {
                latestProvider.Session = null;
                if (runner && runner.IsRunning) runner.ProvideInput = false;
            }
            if (despawn && runner && runner.IsRunning && runner.IsServer)
                foreach (var page in latestPages.Values)
                    if (page && page.Object && page.Object.IsValid) runner.Despawn(page.Object);
            latestPages.Clear();
            latestActorSequences.Clear();
            latestInput = default;
            readLatestTick = false;
            latestDownlinkSeen = false;
            latestProvider = null;
        }

        void RemoveLatestPose(string actorId)
        {
            if (latestPages.TryGetValue(actorId, out var page))
            {
                latestPages.Remove(actorId);
                if (page && page.Object && page.Object.IsValid && runner && runner.IsRunning && runner.IsServer)
                    runner.Despawn(page.Object);
            }
            latestActorSequences.Remove(actorId);
        }

        void CollectLatestInput(NetworkRunner source, NetworkInput input)
        {
            if (source == runner && latestProvider && !closing && !outboundFailed && IsCurrentScene &&
                !runner.IsServer && welcomed && !castle.WaitingForWorld && latestInput.Length > 0)
                input.Set(latestInput);
        }

        bool SendLatestInput(byte[] raw)
        {
            if (!latestProvider || !latestDownlinkSeen) return false;
            var packed = LivePoseCodec.Pack(raw, MaxPoseSize, Array.Empty<int>(), out _);
            int serial = LivePoseWindow.NextSequence(ref poseSequence);
            if (!CastleLatestPoseInput.TryPack(packed, serial, out latestInput)) return false;
            latestInput.SceneEpoch = sceneTag;
            // Stay off during lobby, loading and the first world handshake.
            runner.ProvideInput = true;
            posesInFlight.Submitted(packed.Length);
            posesInFlight.Traffic.RawSubmittedBytes += raw.Length;
            return true;
        }

        internal void ReadLatestInputs(NetworkRunner source)
        {
            if (!latestProvider || source != runner || closing || outboundFailed || !IsCurrentScene) return;
            int tick = runner.Tick.Raw;
            if (readLatestTick && tick == latestReadTick) return;
            readLatestTick = true;
            latestReadTick = tick;
            float now = Time.unscaledTime;
            foreach (var player in runner.ActivePlayers)
            {
                if (!peers.TryGetValue(player, out var peer) || !peer.ready || !peer.worldAcknowledged ||
                    !runner.TryGetInputForPlayer<CastleLatestPoseInput>(player, out var input) || input.SceneEpoch != sceneTag ||
                    !LivePoseWindow.Newer(input.Sequence, peer.lastPoseSequence, peer.hasPoseSequence))
                    continue;
                try
                {
                    var payload = CastleLatestPoseInput.Unpack(ref input);
                    if (payload == null) continue;
                    var raw = LivePoseCodec.Unpack(payload, MaxPoseSize, out _);
                    castle.FusionReceivePlayer(player.ToString(), raw);
                    peer.lastPoseSequence = input.Sequence;
                    peer.hasPoseSequence = true;
                    peer.lastPose = now;
                    peer.posesInFlight.Traffic.RawReceivedBytes += raw.Length;
                    peer.posesInFlight.Received(payload.Length, now);
                    peer.posesInFlight.Applied(payload.Length, now);
                }
                catch (InvalidDataException error) { Debug.LogWarning("Rejected latest player pose: " + error.Message, this); }
                catch (EndOfStreamException error) { Debug.LogWarning("Rejected incomplete latest player pose: " + error.Message, this); }
            }
        }

        bool PublishLatestPose(P.Actor actor)
        {
            if (!latestProvider) return false;
            byte[] raw = P.Encode(P.Player, w => P.Write(w, actor));
            var packed = LivePoseCodec.Pack(raw, MaxLiveBatchSize, Array.Empty<int>(), out _);
            if (packed.Length > CastleLatestPosePage.ByteCapacity) return false;
            if (!latestPages.TryGetValue(actor.id, out var page) || !page)
            {
                var instance = runner.Spawn(NetworkPrefabId.FromRaw(CastleLatestPosePage.PrefabNumber));
                if (!instance) return false;
                page = latestPages[actor.id] = instance.GetComponent<CastleLatestPosePage>();
            }
            page.Publish(sceneTag, LivePoseWindow.NextSequence(ref poseSequence), packed);
            foreach (var pair in peers)
            {
                if (!pair.Value.ready || !pair.Value.worldAcknowledged ||
                    pair.Value.posesInFlight.Traffic.PeerId == actor.id && actor.kind == DeadlyJenga.Core.NetworkKinds.Survivor)
                    continue;
                pair.Value.posesInFlight.Submitted(packed.Length);
                pair.Value.posesInFlight.Traffic.RawSubmittedBytes += raw.Length;
            }
            return true;
        }

        internal bool AcceptLatestPage(NetworkRunner source, int scene) =>
            latestProvider && source == runner && !closing && !outboundFailed && !runner.IsServer &&
            scene == sceneTag && IsCurrentScene && welcomed && !castle.WaitingForWorld;

        bool AcceptLatestActor(string id, int sequence)
        {
            if (!latestProvider) return true;
            bool seen = latestActorSequences.TryGetValue(id, out int last);
            if (!LivePoseWindow.Newer(sequence, last, seen)) return false;
            latestActorSequences[id] = sequence;
            return true;
        }

        internal void ReadLatestPage(byte[] payload, int sequence)
        {
            try
            {
                var raw = LivePoseCodec.Unpack(payload, MaxLiveBatchSize, out _);
                using var reader = P.Reader(raw, P.Player);
                var actor = P.ReadActor(reader);
                foreach (var player in runner.ActivePlayers)
                {
                    if (player.ToString() != actor.id || !AcceptLatestActor(actor.id, sequence)) continue;
                    if (!castle.FusionApplyLiveActor(actor)) return;
                    latestDownlinkSeen = true;
                    lastHostPose = Time.unscaledTime;
                    posesInFlight.Traffic.RawReceivedBytes += raw.Length;
                    posesInFlight.Received(payload.Length, lastHostPose);
                    posesInFlight.Applied(payload.Length, lastHostPose);
                    return;
                }
            }
            catch (InvalidDataException error) { Debug.LogWarning("Rejected latest host pose: " + error.Message, this); }
            catch (EndOfStreamException error) { Debug.LogWarning("Rejected incomplete latest host pose: " + error.Message, this); }
        }
    }
}
