using System;
using System.Collections.Generic;
using System.IO;
using DeadlyJenga.Core;
using Fusion;
using UnityEngine;
using Protocol = DeadlyJenga.CastlePrototype.CastleLanProtocol;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleFusionSession
    {
        const int HumanLanes = 5;
        const int BotLanes = 2;
        const int BlockLanes = 2;
        const int BlockSamples = 36;
        const int BlockScanBudget = 240;
        const byte ActorPage = 1, BlockPage = 2;
        internal const string MotionProtocol = "/native-motion-1";
        readonly CastleMotionPage[] motionPages = new CastleMotionPage[HumanLanes + BotLanes + BlockLanes];
        readonly List<INetworkActor> motionBots = new List<INetworkActor>(64);
        readonly HashSet<INetworkActor> motionBotSet = new HashSet<INetworkActor>();
        readonly List<Vector3> motionAnchors = new List<Vector3>(10);
        readonly MotionBlockCandidate[] nearBlocks = new MotionBlockCandidate[BlockSamples];
        readonly MotionBlockCandidate[] otherBlocks = new MotionBlockCandidate[BlockSamples];
        readonly MemoryStream motionStream = new MemoryStream(CastleMotionPage.ByteCapacity);
        BinaryWriter motionWriter;
        CastleMotionProvider motionProvider;
        CastlePoseInputProbe latestInput;
        int motionStep, inputSequence, blockCursor, botCursor, fastBotCursor, motionRevision = -1;
        float nextInputCapture, nextMotionFrame, nextHumanFrame, nextBotRoster, nextMotionWorld;
        bool[] movingBlocks = Array.Empty<bool>();
        bool inputFallback;
        bool UsesNativeMotion => motionProvider;

        struct MotionBlockCandidate
        {
            public int Index;
            public Rigidbody Body;
            public bool Moving;
        }

        void BindMotion()
        {
            motionProvider = runner.GetComponent<CastleMotionProvider>();
            if (!motionProvider) return;
            motionProvider.Session = this;
            runner.ProvideInput = !runner.IsServer;
            motionWriter ??= new BinaryWriter(motionStream);
            nextMotionFrame = nextHumanFrame = nextInputCapture = nextBotRoster = nextMotionWorld = 0;
            motionStep = inputSequence = blockCursor = botCursor = fastBotCursor = 0;
            latestInput = default;
            inputFallback = false;
            motionRevision = -1;
        }

        void UnbindMotion(bool despawn)
        {
            if (!motionProvider) return;
            if (motionProvider.Session == this)
            {
                motionProvider.Session = null;
                if (runner && runner.IsRunning) runner.ProvideInput = false;
            }
            if (despawn && runner && runner.IsRunning && runner.IsServer)
                foreach (var page in motionPages)
                    if (page && page.Object && page.Object.IsValid) runner.Despawn(page.Object);
            Array.Clear(motionPages, 0, motionPages.Length);
            motionProvider = null;
        }

        internal bool CanReadMotion(NetworkRunner source, int epoch, int revision) =>
            UsesNativeMotion && runner == source && !closing && IsCurrentScene && epoch == sceneTag &&
            castle.AcceptsMotionRevision(revision);

        internal void ReadMotionPage(int tick, float tickSeconds, byte[] data, int length)
        {
            using var reader = new BinaryReader(new MemoryStream(data, 0, length, false));
            switch (reader.ReadByte())
            {
                case ActorPage:
                    int actorCount = reader.ReadByte();
                    for (int i = 0; i < actorCount; i++) castle.ApplyNativeActor(Protocol.ReadActor(reader), tick);
                    FusionMotionWireTrace.Record(FusionMotionWireTrace.Phase.Applied, 32, tick, length);
                    break;
                case BlockPage:
                    int blockCount = reader.ReadByte();
                    float arrival = Time.unscaledTime;
                    for (int i = 0; i < blockCount; i++)
                    {
                        int index = reader.ReadUInt16();
                        castle.ApplyNativeBlock(index, Protocol.ReadPose(reader), tick, tickSeconds, arrival);
                    }
                    FusionMotionWireTrace.Record(FusionMotionWireTrace.Phase.Applied, 33, tick, length);
                    break;
            }
        }

        void CollectMotionInput(NetworkRunner source, NetworkInput input)
        {
            if (source != runner || !UsesNativeMotion || closing || runner.IsServer || !IsCurrentScene ||
                !welcomed || castle.WaitingForWorld) return;
            float now = Time.unscaledTime;
            if (now >= nextInputCapture)
            {
                nextInputCapture = now + 1f / 30;
                byte[] raw = castle.FusionCapturePlayer();
                var payload = LivePoseCodec.Pack(raw, MaxPoseSize, Array.Empty<int>(), out _);
                if (CastlePoseInputProbe.TryPack(payload, ++inputSequence, out var sample))
                {
                    sample.SceneEpoch = sceneTag;
                    latestInput = sample;
                    inputFallback = false;
                }
                else
                {
                    latestInput = default;
                    inputFallback = true;
                }
            }
            if (latestInput.Length != 0) input.Set(latestInput);
        }

        internal void ReadMotionInput(NetworkRunner source)
        {
            if (source != runner || !UsesNativeMotion || closing || !IsCurrentScene) return;
            foreach (var player in runner.ActivePlayers)
            {
                if (!peers.TryGetValue(player, out var peer) || !peer.ready ||
                    !runner.TryGetInputForPlayer<CastlePoseInputProbe>(player, out var input) ||
                    input.SceneEpoch != sceneTag || !LivePoseWindow.Newer(input.Sequence, peer.lastNativeSequence, peer.hasNativeSequence)) continue;
                try
                {
                    var payload = CastlePoseInputProbe.Unpack(ref input);
                    if (payload == null) continue;
                    var raw = LivePoseCodec.Unpack(payload, MaxPoseSize, out _);
                    long applyStarted = FusionMotionWireTrace.Now;
                    castle.FusionReceivePlayer(player.ToString(), raw);
                    peer.lastNativeSequence = input.Sequence;
                    peer.hasNativeSequence = true;
                    peer.lastNativePose = peer.lastPose = Time.unscaledTime;
                    FusionMotionWireTrace.Record(FusionMotionWireTrace.Phase.Applied, 31, input.Sequence, payload.Length, applyStarted);
                }
                catch (InvalidDataException) { }
                catch (IOException) { }
                catch (ArgumentException) { }
            }
        }

        void PumpNativeMotion(float now)
        {
            if (!UsesNativeMotion || !runner.IsServer || now < nextMotionFrame ||
                peers.Count == 0 || DeadlyJenga.Core.Services.World is not ISyncedWorld synced) return;

            nextMotionFrame = now + .03f;
            int tick = runner.Tick.Raw;
            if (motionRevision != synced.Revision)
            {
                motionRevision = synced.Revision;
                movingBlocks = new bool[Services.World.Blocks.Count];
                blockCursor = 0;
            }
            int phase = motionStep++;
            if (now >= nextHumanFrame)
            {
                nextHumanFrame = now + .045f;
                PublishHumanPages(tick);
            }

            if (now >= nextBotRoster)
            {
                nextBotRoster = now + .2f;
                castle.CollectMotionBots(motionBots, motionBotSet);
                castle.CollectMotionAnchors(motionAnchors);
            }
            PublishBotPage(phase, tick);
            PublishBlockPage(phase, tick);
        }

        void PublishHumanPages(int tick)
        {
            for (int lane = 0; lane < HumanLanes; lane++)
            {
                StartPage(ActorPage);
                int count = lane == 0 && TryWriteActor(castle.CaptureMotionHuman()) ? 1 : 0;
                foreach (var player in runner.ActivePlayers)
                    if (player != runner.LocalPlayer && player.PlayerId % HumanLanes == lane &&
                        castle.TryCaptureMotionHuman(player.ToString(), out var actor) && TryWriteActor(actor)) count++;
                PublishPage(lane, tick, count);
            }
        }

        void StartPage(byte kind)
        {
            motionStream.Position = 0;
            motionWriter.Write(kind);
            motionWriter.Write((byte)0);
        }

        bool TryWriteActor(Protocol.Actor actor)
        {
            long start = motionStream.Position;
            // Four UTF-8 bytes per UTF-16 character is a safe upper bound.
            if (start + actor.state.Length + 4 * (actor.id.Length + actor.kind.Length) + 15 > CastleMotionPage.ByteCapacity)
                return false;
            Protocol.Write(motionWriter, actor);
            return true;
        }

        void PublishPage(int lane, int tick, int count)
        {
            if (count == 0) return;
            if (!motionPages[lane])
            {
                var obj = runner.Spawn(NetworkPrefabId.FromRaw(CastleMotionPage.PrefabNumber),
                    onBeforeSpawned: (_, instance) => instance.GetComponent<CastleMotionPage>().Lane = lane);
                if (!obj) return;
                motionPages[lane] = obj.GetComponent<CastleMotionPage>();
            }
            var buffer = motionStream.GetBuffer();
            buffer[1] = (byte)count;
            int bytes = (int)motionStream.Position;
            motionPages[lane].Publish(sceneTag, motionRevision, tick, buffer, bytes);
            FusionMotionWireTrace.Record(FusionMotionWireTrace.Phase.Submitted, buffer[0] == ActorPage ? 32 : 33, tick, bytes);
        }

        bool IsNearPlayer(Vector3 point)
        {
            foreach (var anchor in motionAnchors)
                if ((point - anchor).sqrMagnitude <= 24 * 24) return true;
            return false;
        }

        void PublishBotPage(int phase, int tick)
        {
            if (motionBots.Count == 0) return;
            StartPage(ActorPage);
            int count = 0;
            bool preferNear = phase % 2 == 0;
            for (int inspected = 0; inspected < motionBots.Count && count < 3; inspected++)
            {
                int index = preferNear ? fastBotCursor++ : botCursor++;
                var bot = motionBots[(index & int.MaxValue) % motionBots.Count];
                if (!NetworkActors.Alive(bot) || preferNear && !IsNearPlayer(bot.NetworkPosition)) continue;
                if (!TryWriteActor(castle.CaptureMotionBot(bot))) break;
                count++;
            }
            PublishPage(HumanLanes + phase % BotLanes, tick, count);
        }

        void PublishBlockPage(int phase, int tick)
        {
            var blocks = Services.World.Blocks;
            int scan = Mathf.Min(BlockScanBudget, blocks.Count);
            int near = 0, other = 0;
            for (int n = 0; n < scan && (near < BlockSamples || other < BlockSamples); n++)
            {
                int index = blockCursor;
                if (++blockCursor >= blocks.Count) blockCursor = 0;
                var block = blocks[index];
                if (!block || !block.gameObject.activeSelf) continue;
                bool moving = !block.isKinematic && !block.IsSleeping() &&
                    (block.linearVelocity.sqrMagnitude > .0004f || block.angularVelocity.sqrMagnitude > .0025f);
                if (!moving && !movingBlocks[index]) continue;
                var candidate = new MotionBlockCandidate { Index = index, Body = block, Moving = moving };
                if (IsNearPlayer(block.position) && near < nearBlocks.Length) nearBlocks[near++] = candidate;
                else if (other < otherBlocks.Length) otherBlocks[other++] = candidate;
            }
            if (near + other == 0) return;
            StartPage(BlockPage);
            int fastCount = Mathf.Min(near, BlockSamples - Mathf.Min(other, BlockSamples / 3));
            int slowCount = Mathf.Min(other, BlockSamples - fastCount);
            for (int i = 0; i < fastCount; i++) WriteBlock(nearBlocks[i]);
            for (int i = 0; i < slowCount; i++) WriteBlock(otherBlocks[i]);
            PublishPage(HumanLanes + BotLanes + phase % BlockLanes, tick, fastCount + slowCount);
        }

        void WriteBlock(MotionBlockCandidate sample)
        {
            movingBlocks[sample.Index] = sample.Moving;
            motionWriter.Write((ushort)sample.Index);
            Protocol.Write(motionWriter, new Protocol.Pose(sample.Body.position, sample.Body.rotation));
        }
    }
}
