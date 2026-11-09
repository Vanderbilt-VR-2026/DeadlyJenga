using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using Protocol = DeadlyJenga.CastlePrototype.CastleLanProtocol;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleLanSession
    {
        struct BlockMotion
        {
            public int Tick;
            public bool Queued;
            public Protocol.Pose From, To;
            public Vector3 Velocity;
            public float Arrived, Duration, Delay;
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
            bool hadSample = sample.Duration > 0;
            if (hadSample && !IsLaterTick(tick, sample.Tick)) return;
            var from = new Protocol.Pose(body.position, body.rotation);
            float dt = hadSample ? unchecked(tick - sample.Tick) * tickSeconds : .07f;
            Vector3 delta = target.position - (hadSample ? sample.To.position : from.position);
            bool discontinuity = delta.sqrMagnitude > Mathf.Pow(Mathf.Max(3, Mathf.Min(dt, .2f) * 40), 2);
            float jitter = hadSample ? Mathf.Abs(arrival - sample.Arrived - dt) : 0;
            float delay = Mathf.Clamp(.04f + jitter, .04f, .10f);
            if (!sample.Queued) nativeRenderingBlocks.Add(index);
            sample = new BlockMotion
            {
                Tick = tick,
                Queued = true,
                Arrived = arrival,
                Duration = discontinuity ? .001f : Mathf.Clamp(dt, .025f, .12f),
                Delay = discontinuity ? 0 : delay,
                From = discontinuity ? target : from,
                To = target,
                Velocity = hadSample && !discontinuity && dt >= .015f && dt <= .3f
                    ? Vector3.ClampMagnitude(delta / dt, 30) : Vector3.zero
            };
        }

        bool HasNewerNativeBlock(int index) => index < nativeBlocks.Length && nativeBlocks[index].Duration > 0 &&
            (!nativeWorld || IsLaterTick(nativeBlocks[index].Tick, nativeWorldTick));

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
            float ratio = Mathf.Clamp01(elapsed / sample.Duration);
            Vector3 position = Vector3.Lerp(sample.From.position, sample.To.position, ratio);
            float lead = Mathf.Clamp(elapsed - sample.Duration, 0, .04f);
            // Fade extrapolation back to the authoritative endpoint when updates stop.
            lead *= 1f - Mathf.Clamp01((elapsed - sample.Duration - .04f) / .08f);
            position += Vector3.ClampMagnitude(sample.Velocity * lead, .5f);
            var rotation = Quaternion.Slerp(sample.From.rotation, sample.To.rotation, ratio);
            if ((body.position - position).sqrMagnitude > .0000001f) body.position = position;
            if (Quaternion.Dot(body.rotation, rotation) < .9999999f) body.rotation = rotation;
            return elapsed < sample.Duration + .12f;
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
