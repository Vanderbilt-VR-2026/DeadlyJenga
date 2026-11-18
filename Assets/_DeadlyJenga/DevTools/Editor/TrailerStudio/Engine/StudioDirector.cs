using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Keeps track of the world direction that plays out over time during a live-physics run:
    // delayed pushes (ripples), chain reactions, guides that pull debris toward a spot,
    // disasters that end after a set time, and slow-time ranges.
    public static class StudioDirector
    {
        sealed class Later { public float at; public Action run; }
        sealed class Guide { public Vector3 point; public float radius, pull, until; }
        sealed class Watch { public Rigidbody body; public Vector3 start, lastVelocity; public bool moved; public string cueId; }
        sealed class Slow { public float from, to, factor; }

        static readonly List<Later> later = new List<Later>();
        static readonly List<Guide> guides = new List<Guide>();
        static readonly List<Watch> watches = new List<Watch>();
        static readonly List<Slow> slows = new List<Slow>();
        static float now;

        public static float Now => now;

        public static void Begin(StudioStage stage, float startTime)
        {
            later.Clear(); guides.Clear(); watches.Clear(); slows.Clear();
            now = startTime;
            foreach (var cue in stage.set.cues)
                if (cue.action == SlowTimeCue.Id && cue.duration > 0)
                    slows.Add(new Slow { from = cue.at, to = cue.at + cue.duration, factor = Mathf.Clamp(cue.strength, .05f, 1) });
        }

        public static void End() { later.Clear(); guides.Clear(); watches.Clear(); slows.Clear(); }

        // Do something at a tape time (or right away if that time has passed).
        public static void At(float time, Action action)
        {
            if (time <= now + .0001f) action();
            else later.Add(new Later { at = time, run = action });
        }

        public static void AddGuide(Vector3 point, float radius, float pull, float until) =>
            guides.Add(new Guide { point = point, radius = radius, pull = pull, until = until });

        // How much a slow-time cue slows the world at this tape time (1 = not at all).
        public static float SlowFactor(float time)
        {
            float factor = 1;
            foreach (var slow in slows) if (time >= slow.from && time < slow.to) factor = Mathf.Min(factor, slow.factor);
            return factor;
        }

        // A cue just ran: start the cues linked to it.
        public static void Triggered(StudioStage stage, StudioCue cue, List<string> log)
        {
            foreach (var linked in stage.set.cues.Where(c => c.link == cue.id))
            {
                var captured = linked;
                if (!linked.linkOnHit) { At(now + Mathf.Max(0, linked.at), () => RunLinked(stage, captured, log)); continue; }
                var body = cue.blocks.Count > 0 && cue.blocks[0] >= 0 && cue.blocks[0] < stage.blocks.Length ? stage.blocks[cue.blocks[0]] : null;
                if (!body) { log.Add($"{now:F3}s {linked.action}: not run (the cue it waits for has no block to watch)"); continue; }
                watches.Add(new Watch { body = body, start = body.position, lastVelocity = body.linearVelocity, cueId = linked.id });
            }
        }

        static void RunLinked(StudioStage stage, StudioCue cue, List<string> log)
        {
            log.Add($"{now:F3}s {StudioCues.Run(stage, cue)} (chained)");
            Triggered(stage, cue, log);
        }

        // Every frame while the world is live.
        public static void Tick(StudioStage stage, float time, List<string> log)
        {
            now = time;
            for (int i = 0; i < later.Count; i++)
            {
                if (later[i].at > time + .0001f) continue;
                var due = later[i];
                later.RemoveAt(i--);
                try { due.run(); } catch (Exception error) { log.Add($"{time:F3}s a delayed action failed: {error.Message}"); }
            }
        }

        // Every physics step while the world is live.
        public static void FixedTick(StudioStage stage, List<string> log)
        {
            // Guides: a gentle pull on loose, moving blocks nearby. Never drawn in the game.
            for (int g = guides.Count - 1; g >= 0; g--)
            {
                var guide = guides[g];
                if (now > guide.until) { guides.RemoveAt(g); continue; }
                foreach (var body in stage.blocks)
                {
                    if (!body || body.isKinematic || body.IsSleeping() || !body.gameObject.activeSelf) continue;
                    Vector3 offset = guide.point - body.worldCenterOfMass;
                    float distance = offset.magnitude;
                    if (distance > guide.radius || distance < .05f) continue;
                    // Mostly sideways: gravity still does the falling.
                    Vector3 pull = Vector3.ProjectOnPlane(offset, Vector3.up).normalized * guide.pull;
                    body.AddForce(pull, ForceMode.Acceleration);
                }
            }
            // Chain reactions: has the watched block hit something? (It moved, then its speed changed sharply.)
            for (int w = watches.Count - 1; w >= 0; w--)
            {
                var watch = watches[w];
                if (!watch.body) { watches.RemoveAt(w); continue; }
                if (!watch.moved && Vector3.Distance(watch.body.position, watch.start) > .3f) watch.moved = true;
                Vector3 velocity = watch.body.linearVelocity;
                bool hit = watch.moved && (velocity - watch.lastVelocity).magnitude > 1.5f;
                watch.lastVelocity = velocity;
                if (!hit) continue;
                watches.RemoveAt(w);
                var cue = stage.set.cues.FirstOrDefault(c => c.id == watch.cueId);
                if (cue != null) At(now + Mathf.Max(0, cue.at), () => RunLinked(stage, cue, log));
            }
        }

        // The blocks a cue acts on. With none chosen, the block nearest the cue's point.
        public static List<Rigidbody> Blocks(StudioStage stage, StudioCue cue)
        {
            var found = new List<Rigidbody>();
            foreach (int index in cue.blocks)
                if (index >= 0 && index < stage.blocks.Length && stage.blocks[index] && stage.blocks[index].gameObject.activeSelf) found.Add(stage.blocks[index]);
            if (found.Count == 0 && cue.blocks.Count == 0)
            {
                Vector3 point = stage.ToWorld(cue.point);
                var nearest = stage.blocks.Where(b => b && b.gameObject.activeSelf).OrderBy(b => (b.position - point).sqrMagnitude).FirstOrDefault();
                if (nearest) found.Add(nearest);
            }
            return found;
        }
    }

    // Push chosen blocks: direction, force, spin. With a ripple, each block goes a little later than the last.
    public sealed class PushCue : IStudioCueType
    {
        public const string Id = "push";
        public string Action => Id;
        public string Label => "Push blocks";
        public string Run(StudioStage stage, StudioCue cue)
        {
            var blocks = StudioDirector.Blocks(stage, cue);
            if (blocks.Count == 0) throw new InvalidOperationException("The chosen blocks are gone.");
            Vector3 direction = cue.direction.sqrMagnitude > .0001f ? cue.direction.normalized : Vector3.right;
            float force = Mathf.Clamp(cue.strength, 0, 60) * (1 + cue.jitter);
            Vector3 origin = blocks[0].position;
            Vector3 spinAxis = Vector3.Cross(Vector3.up, direction).normalized;
            float start = StudioDirector.Now;
            foreach (var block in blocks)
            {
                var body = block;
                float delay = cue.ripple * Vector3.Distance(origin, body.position);
                StudioDirector.At(start + delay, () =>
                {
                    if (!body || body.isKinematic) return;
                    body.WakeUp();
                    body.AddForce(direction * force, ForceMode.VelocityChange);
                    if (Mathf.Abs(cue.spin) > .001f) body.AddTorque(spinAxis * cue.spin * Mathf.PI * 2, ForceMode.VelocityChange);
                });
            }
            float last = cue.ripple * blocks.Max(b => Vector3.Distance(origin, b.position));
            return $"{blocks.Count} blocks at {force:0.#} m/s" + (cue.ripple > 0 ? $", rippling over {last:0.00} s" : "");
        }
    }

    // Weak points: loosen or remove blocks at a time, as if something pulled them, without the hand.
    public sealed class WeakenCue : IStudioCueType
    {
        public const string Id = "weaken";
        public string Action => Id;
        public string Label => "Weak point";
        public string Run(StudioStage stage, StudioCue cue)
        {
            var blocks = StudioDirector.Blocks(stage, cue);
            if (blocks.Count == 0) throw new InvalidOperationException("The chosen blocks are gone.");
            foreach (var body in blocks)
            {
                if (cue.lethal) { body.gameObject.SetActive(false); continue; }
                foreach (var joint in body.GetComponents<Joint>()) UnityEngine.Object.Destroy(joint);
                body.isKinematic = false;
                body.WakeUp();
                body.AddForce(Vector3.down * .3f, ForceMode.VelocityChange);
            }
            return (cue.lethal ? "removed " : "loosened ") + blocks.Count + " blocks";
        }
    }

    // An invisible helper that nudges falling blocks toward a spot.
    public sealed class GuideCue : IStudioCueType
    {
        public const string Id = "guide";
        public string Action => Id;
        public string Label => "Guide (pull debris to a spot)";
        public string Run(StudioStage stage, StudioCue cue)
        {
            float lasts = cue.duration > 0 ? cue.duration : 2;
            StudioDirector.AddGuide(stage.ToWorld(cue.point), Mathf.Max(.5f, cue.radius), Mathf.Clamp(cue.strength, 0, 60), StudioDirector.Now + lasts);
            return $"radius {cue.radius:0.#} m for {lasts:0.#} s";
        }
    }

    // Slow or nearly hold world time over a range while performing. The director reads these ranges itself.
    public sealed class SlowTimeCue : IStudioCueType
    {
        public const string Id = "slowTime";
        public string Action => Id;
        public string Label => "Slow time";
        public string Run(StudioStage stage, StudioCue cue) => $"to {Mathf.Clamp(cue.strength, .05f, 1):0.##}× for {cue.duration:0.#} s (applies while performing in real time)";
    }

    // Give an actor a weapon through the match's own equipment path.
    public sealed class GearCue : IStudioCueType
    {
        public string Action => "gear";
        public string Label => "Give a weapon";
        public string Run(StudioStage stage, StudioCue cue)
        {
            if (!Enum.TryParse(cue.weapon, true, out EquipmentKind kind) || kind == EquipmentKind.None) throw new InvalidOperationException("Pick a weapon.");
            if (string.IsNullOrEmpty(cue.actor))
            {
                if (!stage.session.Modes.GiveEveryoneEquipment(kind, false)) throw new InvalidOperationException("The match refused.");
                return kind + " to everyone live";
            }
            var body = stage.Live(cue.actor) ?? throw new InvalidOperationException($"{cue.actor} must be live in this run (recorded actors cannot be handed things).");
            var identity = body.GetComponent<IModeActor>();
            var grant = typeof(DeadlyJenga.Modes.MatchModeRuntime).GetMethod("GiveEquipmentTo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            bool given = identity != null && grant != null && (bool)grant.Invoke(stage.session.Modes, new object[] { identity.ActorId, kind, false, true });
            if (!given) throw new InvalidOperationException("The match refused.");
            return kind + " to " + cue.actor;
        }
    }

    // Fire an actor's weapon at a target or a point, through the real equipment command.
    public sealed class FireCue : IStudioCueType
    {
        public string Action => "fire";
        public string Label => "Fire a weapon";
        public string Run(StudioStage stage, StudioCue cue)
        {
            var shooter = stage.Live(cue.actor) ?? throw new InvalidOperationException($"{cue.actor} must be live in this run to fire.");
            var identity = shooter.GetComponent<IModeActor>();
            if (identity == null || string.IsNullOrEmpty(identity.ActorId)) throw new InvalidOperationException(cue.actor + " is not part of the match.");
            Vector3 target = stage.ToWorld(cue.point);
            if (!string.IsNullOrEmpty(cue.target))
            {
                var other = stage.Live(cue.target) ?? (stage.playback.TryGetValue(cue.target, out var shown) ? shown : null);
                if (!other) throw new InvalidOperationException("The target is not in the scene: " + cue.target);
                target = other.transform.position + Vector3.up * .85f;
            }
            Vector3 aim = target - identity.ViewPose.position;
            if (aim.sqrMagnitude < .01f) throw new InvalidOperationException("The shooter and the target are in the same place.");
            aim.Normalize();
            var modes = stage.session.Modes;
            int before = modes.Frame.equipmentSequence;
            var shot = new DeadlyJenga.Modes.MatchModeRuntime.EquipmentCommand { action = "attack", position = identity.ViewPose.position + aim * .18f, forward = aim };
            modes.ReceiveCommand(identity.ActorId, "equipment:" + JsonUtility.ToJson(shot));
            bool fired = modes.Frame.equipmentActions.Any(e => e.id > before && e.actor == identity.ActorId && e.action == EquipmentAction.Shot);
            if (!fired && !cue.optional) throw new InvalidOperationException("Gameplay refused the shot (weapon not drawn, cooling down, or the muzzle is blocked).");
            return $"{cue.actor} fired {modes.EquipmentFor(identity.ActorId).kind}: {fired}";
        }
    }
}
