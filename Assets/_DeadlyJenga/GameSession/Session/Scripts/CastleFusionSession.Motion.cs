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
        const int HumanLanes = 10;
        const int BotLanes = 2;
        const int BlockLanes = 2;
        const int NearBlockSamples = 24, FarBlockSamples = 16;
        const int BlockScanBudget = 120;
        const byte ActorPage = 1, BlockPage = 2;
        internal const string MotionProtocol = "/native-motion-1";
        readonly CastleMotionPage[] motionPages = new CastleMotionPage[HumanLanes + BotLanes + BlockLanes];
        readonly List<INetworkActor> motionBots = new List<INetworkActor>(64);
        readonly HashSet<INetworkActor> motionBotSet = new HashSet<INetworkActor>();
        readonly List<Vector3> motionAnchors = new List<Vector3>(10);
        readonly MemoryStream motionStream = new MemoryStream(CastleMotionPage.ByteCapacity);
        BinaryWriter motionWriter;
        CastleMotionProvider motionProvider;
        CastlePoseInputProbe latestInput;
        int motionStep, inputSequence, nearBlockCursor, blockCursor, botCursor, fastBotCursor, motionRevision = -1;
        float nextInputCapture, nextMotionFrame, nextHumanFrame, nextBotRoster, nextMotionWorld;
        bool[] movingBlocks = Array.Empty<bool>();
        bool inputFallback;
        bool UsesNativeMotion => motionProvider;

        int AssignMotionLane()
        {
            Span<int> population = stackalloc int[HumanLanes];
            population.Clear();
            population[0] = 1;
            foreach (var peer in peers.Values) population[peer.motionLane]++;
            int lane = 0;
            for (int i = 1; i < population.Length; i++)
                if (population[i] < population[lane]) lane = i;
            return lane;
        }

        void BindMotion()
        {
            motionProvider = runner.GetComponent<CastleMotionProvider>();
            if (!motionProvider) return;
            motionProvider.Session = this;
            runner.ProvideInput = !runner.IsServer;
            motionWriter ??= new BinaryWriter(motionStream);
            nextMotionFrame = nextHumanFrame = nextInputCapture = nextBotRoster = nextMotionWorld = 0;
            motionStep = inputSequence = nearBlockCursor = blockCursor = botCursor = fastBotCursor = 0;
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
                ++inputSequence;
                if (CastlePoseInputProbe.TryPack(raw, inputSequence, out var sample) ||
                    raw.Length <= MaxPoseSize && CastlePoseInputProbe.TryPack(
                        LivePoseCodec.Pack(raw, MaxPoseSize), inputSequence, out sample))
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
                    var raw = payload[0] == Protocol.Player ? payload : LivePoseCodec.Unpack(payload, MaxPoseSize);
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
                nearBlockCursor = blockCursor = 0;
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
                foreach (var pair in peers)
                    if (pair.Value.ready && pair.Value.motionLane == lane &&
                        castle.TryCaptureMotionHuman(pair.Key.ToString(), out var actor) && TryWriteActor(actor)) count++;
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
                if (IsDistantMotionLane(lane))
                    foreach (var player in peers.Keys) obj.SetPriority(player, 2);
            }
            var buffer = motionStream.GetBuffer();
            buffer[1] = (byte)count;
            int bytes = (int)motionStream.Position;
            if (motionPages[lane].Publish(sceneTag, motionRevision, tick, buffer, bytes))
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

        static bool IsDistantMotionLane(int lane) =>
            lane == HumanLanes + 1 || lane == HumanLanes + BotLanes + 1;

        void SetDistantMotionPriority(PlayerRef player)
        {
            for (int lane = HumanLanes; lane < motionPages.Length; lane++)
                if (IsDistantMotionLane(lane) && motionPages[lane]) motionPages[lane].Object.SetPriority(player, 2);
        }

        void PublishBlockPage(int phase, int tick)
        {
            StartPage(BlockPage);
            int lane = phase % BlockLanes;
            int count = lane == 0
                ? WriteBlocks(ref nearBlockCursor, NearBlockSamples, true)
                : WriteBlocks(ref blockCursor, FarBlockSamples, false);
            PublishPage(HumanLanes + BotLanes + lane, tick, count);
        }

        int WriteBlocks(ref int cursor, int capacity, bool near)
        {
            var blocks = Services.World.Blocks;
            int scan = Mathf.Min(BlockScanBudget, blocks.Count);
            int written = 0;
            for (int n = 0; n < scan && written < capacity; n++)
            {
                int index = cursor;
                if (++cursor >= blocks.Count) cursor = 0;
                var block = blocks[index];
                if (!block || !block.gameObject.activeSelf) continue;
                bool moving = !block.isKinematic && !block.IsSleeping() &&
                    (block.linearVelocity.sqrMagnitude > .0004f || block.angularVelocity.sqrMagnitude > .0025f);
                if (!moving && !movingBlocks[index]) continue;
                var position = block.position;
                if (near != IsNearPlayer(position)) continue;
                movingBlocks[index] = moving;
                motionWriter.Write((ushort)index);
                Protocol.Write(motionWriter, new Protocol.Pose(position, block.rotation));
                written++;
            }
            return written;
        }
    }
}
