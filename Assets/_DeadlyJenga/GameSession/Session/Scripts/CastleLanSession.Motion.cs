using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using Protocol = DeadlyJenga.CastlePrototype.CastleLanProtocol;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleLanSession
    {
        struct BlockFrame
        {
            public int Tick;
            public Protocol.Pose Pose;
        }

        struct BlockMotion
        {
            public BlockFrame Oldest, Older, Previous, Latest;
            public int Count;
            public bool Queued;
            public float Arrived, TickSeconds, Delay;
        }

        readonly Dictionary<string, int> nativeActorTicks = new Dictionary<string, int>();
        readonly List<int> nativeRenderingBlocks = new List<int>();
        readonly List<int> reliableRenderingBlocks = new List<int>();
        BlockMotion[] nativeBlocks = Array.Empty<BlockMotion>();
        bool nativeWorld;
        int nativeWorldTick;

        internal bool AcceptsMotionRevision(int revision) => IsClient && receivedWorld && hostRevision == revision;

        static bool IsLaterTick(int sample, int baseline) => unchecked(sample - baseline) > 0;

        void ResetNativeMotion()
        {
            nativeWorld = false;
            nativeActorTicks.Clear();
            nativeRenderingBlocks.Clear();
            reliableRenderingBlocks.Clear();
            nativeBlocks = Array.Empty<BlockMotion>();
        }

        bool NewerNativeActor(string actorId, int tick) =>
            nativeActorTicks.TryGetValue(actorId, out int previous) && IsLaterTick(previous, tick);

        internal void ApplyNativeActor(Protocol.Actor actor, int tick)
        {
            if (nativeWorld && !IsLaterTick(tick, nativeWorldTick)) return;
            if (nativeActorTicks.TryGetValue(actor.id, out int previous) && !IsLaterTick(tick, previous)) return;
            if (actor.id == id)
                ApplyOwnHandFeedback(actor);
            else
                ShowActor(actor, false);
            nativeActorTicks[actor.id] = tick;
        }

        internal void ApplyNativeBlock(int index, Protocol.Pose target, int tick, float tickSeconds, float arrival)
        {
            if (index >= nativeBlocks.Length || nativeWorld && !IsLaterTick(tick, nativeWorldTick)) return;
            var body = World.Blocks[index];
            if (!body || !body.gameObject.activeSelf) return;
            ref var sample = ref nativeBlocks[index];
            bool hadSample = sample.Count > 0;
            if (hadSample && !IsLaterTick(tick, sample.Latest.Tick)) return;
            float dt = hadSample ? unchecked(tick - sample.Latest.Tick) * tickSeconds : .067f;
            var from = new Protocol.Pose(body.position, body.rotation);
            Vector3 delta = target.position - (hadSample ? sample.Latest.Pose.position : from.position);
            float teleportDistance = Mathf.Max(3, Mathf.Min(dt, .2f) * 40);
            bool discontinuity = delta.sqrMagnitude > teleportDistance * teleportDistance;
            float jitter = hadSample ? Mathf.Abs(arrival - sample.Arrived - dt) : 0;
            if (!sample.Queued) nativeRenderingBlocks.Add(index);
            sample.Queued = true;
            sample.Arrived = arrival;
            sample.TickSeconds = tickSeconds;
            sample.Delay = discontinuity ? 0 : Mathf.Clamp(.04f + jitter, .04f, .10f);
            if (discontinuity)
            {
                sample.Count = 1;
            }
            else if (!hadSample)
            {
                sample.Count = 2;
                sample.Previous = new BlockFrame
                {
                    Tick = unchecked(tick - Mathf.Max(1, Mathf.RoundToInt(.067f / tickSeconds))),
                    Pose = from
                };
            }
            else
            {
                sample.Oldest = sample.Older;
                sample.Older = sample.Previous;
                sample.Previous = sample.Latest;
                sample.Count = Mathf.Min(4, sample.Count + 1);
            }
            sample.Latest = new BlockFrame { Tick = tick, Pose = target };
        }

        bool HasNewerNativeBlock(int index) => index < nativeBlocks.Length && nativeBlocks[index].Count > 0 &&
            (!nativeWorld || IsLaterTick(nativeBlocks[index].Latest.Tick, nativeWorldTick));

        void RenderNativeBlocks(float now)
        {
            for (int i = nativeRenderingBlocks.Count - 1; i >= 0; i--)
            {
                int index = nativeRenderingBlocks[i];
                if (RenderNativeBlock(index, World.Blocks[index], now)) continue;
                nativeBlocks[index].Queued = false;
                int last = nativeRenderingBlocks.Count - 1;
                nativeRenderingBlocks[i] = nativeRenderingBlocks[last];
                nativeRenderingBlocks.RemoveAt(last);
            }
        }

        bool RenderNativeBlock(int index, Rigidbody body, float now)
        {
            if (!HasNewerNativeBlock(index) || !body || !body.gameObject.activeSelf) return false;
            ref var sample = ref nativeBlocks[index];

            float elapsed = now - sample.Arrived - sample.Delay;
            var position = sample.Latest.Pose.position;
            var rotation = sample.Latest.Pose.rotation;
            if (sample.Count > 1)
            {
                float beforeLatest = -elapsed / sample.TickSeconds;
                var left = sample.Previous;
                var right = sample.Latest;
                if (sample.Count >= 3 && beforeLatest > unchecked(sample.Latest.Tick - left.Tick))
                {
                    right = left;
                    left = sample.Older;
                }
                if (sample.Count == 4 && beforeLatest > unchecked(sample.Latest.Tick - left.Tick))
                {
                    right = left;
                    left = sample.Oldest;
                }

                int leftAge = unchecked(sample.Latest.Tick - left.Tick);
                int rightAge = unchecked(sample.Latest.Tick - right.Tick);
                float ratio = Mathf.Clamp01((leftAge - beforeLatest) / Mathf.Max(1, leftAge - rightAge));
                position = Vector3.Lerp(left.Pose.position, right.Pose.position, ratio);
                rotation = Quaternion.Slerp(left.Pose.rotation, right.Pose.rotation, ratio);
                float spacing = unchecked(sample.Latest.Tick - sample.Previous.Tick) * sample.TickSeconds;
                if (elapsed > 0 && spacing >= .015f && spacing <= .3f)
                {
                    float lead = Mathf.Min(elapsed, .04f) * (1f - Mathf.Clamp01((elapsed - .04f) / .08f));
                    var velocity = (sample.Latest.Pose.position - sample.Previous.Pose.position) / spacing;
                    position += Vector3.ClampMagnitude(Vector3.ClampMagnitude(velocity, 30) * lead, .5f);
                }
            }
            if ((body.position - position).sqrMagnitude > .0000001f) body.position = position;
            if (Mathf.Abs(Quaternion.Dot(body.rotation, rotation)) < .9999999f) body.rotation = rotation;
            return elapsed < .12f;
        }

        internal Protocol.Actor CaptureMotionHuman() => LocalActor();

        internal bool TryCaptureMotionHuman(string actorId, out Protocol.Actor actor)
        {
            if (!fusionActors.TryGetValue(actorId, out actor)) return false;
            actor = Rebroadcast(actorId, actor);
            return true;
        }

        internal void CollectMotionAnchors(List<Vector3> anchors)
        {
            anchors.Clear();
            if (session.Player is Component local && local) anchors.Add(local.transform.position);
            foreach (var pair in fusionActors)
                if (views.TryGetValue(pair.Key, out var view) && NetworkActors.Alive(view) && view.NetworkActive)
                    anchors.Add(view.NetworkPosition);
        }

        internal void CollectMotionBots(List<INetworkActor> result, HashSet<INetworkActor> seen)
        {
            result.Clear();
            seen.Clear();
            foreach (var actor in NetworkActors.Local)
                if (NetworkActors.Alive(actor) && !IsOwnCharacter(actor) && seen.Add(actor)) result.Add(actor);
            if (session.SurvivorBots == null) return;
            foreach (var member in session.SurvivorBots.Members)
            {
                if (!member) continue;
                var actor = member.GetComponent<INetworkActor>();
                if (NetworkActors.Alive(actor) && seen.Add(actor)) result.Add(actor);
            }
        }

        internal Protocol.Actor CaptureMotionBot(INetworkActor actor) =>
            Protocol.Capture(LocalActorId((Component)actor), actor);
    }
}
