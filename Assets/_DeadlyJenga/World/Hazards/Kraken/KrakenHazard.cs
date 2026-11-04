using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.World.Hazards
{
    // The Kraken: a giant candy octopus rises out of the sea beside the tower. Its tentacles surface all
    // around the tower and take turns to
    //  - GRAB: curl up to a block on the outside of the tower, wrap it, and drag it down into the sea, or
    //  - SLAM: rear up high and sweep across one level of the tower, knocking players and blocks flying.
    // Host only. Everyone sees it through the DisasterFrame (tentacle tips, bases, phases, head height),
    // which GameSession already sends to other players; Presentation draws it.
    public sealed class KrakenHazard : MapHazard, IDemoHazard, IDisasterState
    {
        public const string KindName = "Kraken";
        public enum Arm { Resting, Reaching, Holding, Dragging, Rearing, Slamming, Sinking }

        sealed class Tentacle
        {
            public Vector3 basePoint, tip, from, to, slamEnd;
            public Arm state = Arm.Sinking;
            public float t, wait, length;
            public Rigidbody held;
        }

        static readonly DisasterParameter[] Schema =
        {
            new DisasterParameter("arms", "Tentacles", 2, 8, 5, "", true),
            new DisasterParameter("grab", "Grab every (per tentacle)", 2, 20, 6, "s"),
            new DisasterParameter("slam", "Slam force", 0, 40, 16, "m/s"),
            new DisasterParameter("warning", "Warning time", 0, 15, 4, "s"),
            new DisasterParameter("duration", "Duration", 10, 180, 45, "s"),
        };
        float[] values;
        public IReadOnlyList<DisasterParameter> Parameters => Schema;
        public bool Toggle => false;
        float V(int i) { if (values == null) { values = new float[Schema.Length]; for (int k = 0; k < Schema.Length; k++) values[k] = Schema[k].initial; } return values[i]; }

        public void Configure(float[] input)
        {
            if (Active || input == null || input.Length != Schema.Length) throw new System.ArgumentException("Disaster settings do not match the parameter schema.");
            values = new float[input.Length];
            for (int i = 0; i < input.Length; i++) values[i] = Schema[i].Clamp(input[i]);
        }

        IWorldQuery world;
        Bounds area;
        float age, strength, water, towerRadius, top;
        Vector3 center, head, headDir;
        readonly List<Tentacle> arms = new List<Tentacle>();
        readonly HashSet<Rigidbody> claimed = new HashSet<Rigidbody>();
        readonly Collider[] hits = new Collider[256];
        DisasterFrame cached; int capturedAt = -1;

        float Warning => V(3);
        public float HeadRise => Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / Mathf.Max(.5f, Warning + 1.5f))) * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((age - Warning - V(4)) / 3f)));

        protected override void BeginHazard(IWorldQuery map, Bounds bounds, float intensity, float seconds)
        {
            world = map; area = bounds; strength = intensity; age = 0; capturedAt = -1; cached = null;
            center = new Vector3(map.Center.x, 0, map.Center.z);
            water = map.WaterHeight(map.Center);
            towerRadius = Mathf.Max(4, map.Radius);
            top = Mathf.Max(water + 8, map.TopHeight);
            float angle = Random.Range(0f, 360f);
            headDir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            head = center + headDir * (towerRadius * 1.3f + 26) + Vector3.up * water;
            arms.Clear(); claimed.Clear();
            int count = Mathf.RoundToInt(V(0));
            for (int i = 0; i < count; i++)
            {
                // Bases fan around the tower, centred on the side the head is on.
                float a = angle + (i - (count - 1) * .5f) * (300f / Mathf.Max(1, count));
                var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                var b = center + dir * (towerRadius + 10) + Vector3.up * water;
                var arm = new Tentacle { basePoint = b, tip = b + Vector3.down * 6, from = b + Vector3.down * 6, state = Arm.Sinking, t = 1, wait = Warning + .6f * i + Random.Range(0, 1.5f) };
                arm.length = (top - water) * 1.15f + towerRadius;
                arms.Add(arm);
            }
            DisasterStates.Active.Add(this);
        }

        protected override void EndHazard()
        {
            DisasterStates.Active.Remove(this);
            arms.Clear(); claimed.Clear();
        }

        void FixedUpdate()
        {
            if (!Active) return;
            capturedAt = -1;
            if (Services.Session is IWorldAuthority authority && !authority.HasWorldAuthority) { End(); return; }
            float dt = Time.fixedDeltaTime;
            age += dt;
            if (age >= Warning + V(4) + 3) { End(); return; }
            bool leaving = age >= Warning + V(4);
            foreach (var arm in arms) Step(arm, dt, leaving);
        }

        static float Ease(float t) => t * t * (3 - 2 * t);

        Vector3 Rest(Tentacle arm)
        {
            var outward = Vector3.ProjectOnPlane(arm.basePoint - center, Vector3.up).normalized;
            float sway = Mathf.Sin(age * 1.3f + arm.basePoint.x) * 2.5f;
            return arm.basePoint + Vector3.up * 22 + outward * 7 + Vector3.Cross(Vector3.up, outward) * sway * 2;
        }

        void Go(Tentacle arm, Arm state, Vector3 to) { arm.state = state; arm.from = arm.tip; arm.to = to; arm.t = 0; }

        void Step(Tentacle arm, float dt, bool leaving)
        {
            switch (arm.state)
            {
                case Arm.Sinking:
                    arm.t += dt / 1.2f;
                    arm.tip = Vector3.Lerp(arm.from, arm.basePoint + Vector3.down * 6, Ease(Mathf.Clamp01(arm.t)));
                    arm.wait -= dt;
                    if (arm.t >= 1 && arm.wait <= 0 && !leaving) { Go(arm, Arm.Resting, Rest(arm)); arm.wait = Random.Range(1f, 2.5f); }
                    break;

                case Arm.Resting:
                    arm.t += dt / 1.4f;
                    arm.tip = Vector3.Lerp(arm.from, Rest(arm), Ease(Mathf.Clamp01(arm.t)));
                    arm.wait -= dt;
                    if (leaving) { Go(arm, Arm.Sinking, Vector3.zero); arm.wait = 99; break; }
                    if (arm.t >= 1 && arm.wait <= 0) Choose(arm);
                    break;

                case Arm.Reaching:
                    arm.t += dt / 1.6f;
                    if (arm.held) arm.to = arm.held.position;
                    arm.tip = Bend(arm.from, arm.to, Ease(Mathf.Clamp01(arm.t)), 6);
                    if (arm.t >= 1) { arm.state = Arm.Holding; arm.t = 0; }
                    if (!Alive(arm.held)) Release(arm);
                    break;

                case Arm.Holding:
                    arm.t += dt / .45f;
                    if (!Alive(arm.held)) { Release(arm); break; }
                    arm.tip = arm.held.position;
                    if (arm.t >= 1) { Go(arm, Arm.Dragging, arm.basePoint + Vector3.down * 10); }
                    break;

                case Arm.Dragging:
                    arm.t += dt / 2.6f;
                    arm.tip = Bend(arm.from, arm.to, Ease(Mathf.Clamp01(arm.t)), 4);
                    if (Alive(arm.held))
                    {
                        // Pull the block hard toward the tentacle tip: it gets ripped out and dragged under.
                        var b = arm.held;
                        b.linearVelocity = Vector3.Lerp(b.linearVelocity, (arm.tip - b.position) * 7 * strength, .6f);
                        b.angularVelocity = Vector3.Lerp(b.angularVelocity, Random.insideUnitSphere * 3, .1f);
                        if (b.position.y < water - 2.5f) { b.GetComponent<IBreakable>()?.Break(gameObject); Release(arm); }
                    }
                    if (arm.t >= 1) { Release(arm); Go(arm, Arm.Sinking, Vector3.zero); arm.from = arm.tip; arm.wait = V(1) * Random.Range(.6f, 1.2f); }
                    break;

                case Arm.Rearing:
                    arm.t += dt / 1.3f;
                    arm.tip = Bend(arm.from, arm.to, Ease(Mathf.Clamp01(arm.t)), 8);
                    if (arm.t >= 1) { Go(arm, Arm.Slamming, arm.slamEnd); }
                    break;

                case Arm.Slamming:
                    arm.t += dt / .9f;
                    float k = Mathf.Clamp01(arm.t);
                    arm.tip = Bend(arm.from, arm.to, k * k, -5);
                    Smash(arm, dt);
                    if (arm.t >= 1) { Go(arm, Arm.Sinking, Vector3.zero); arm.from = arm.tip; arm.wait = V(1) * Random.Range(.8f, 1.4f); }
                    break;
            }
        }

        // A curved path: an arc that bulges sideways by `bulge` metres along the way.
        static Vector3 Bend(Vector3 a, Vector3 b, float t, float bulge)
        {
            var side = Vector3.up * bulge;
            return Vector3.Lerp(a, b, t) + side * Mathf.Sin(t * Mathf.PI);
        }

        static bool Alive(Rigidbody b) => b && !b.isKinematic && !(b.GetComponent<IBreakable>()?.Broken ?? false);

        void Release(Tentacle arm)
        {
            if (arm.held) claimed.Remove(arm.held);
            arm.held = null;
            if (arm.state == Arm.Reaching || arm.state == Arm.Holding) { Go(arm, Arm.Sinking, Vector3.zero); arm.wait = 1.5f; }
        }

        void Choose(Tentacle arm)
        {
            bool slam = Random.value < .3f && AnyoneOnTower();
            if (!slam)
            {
                var target = PickBlock(arm);
                if (target) { arm.held = target; claimed.Add(target); Go(arm, Arm.Reaching, target.position); return; }
            }
            // Slam: rear up on this side, then sweep across a level of the tower to the far side.
            var outward = Vector3.ProjectOnPlane(arm.basePoint - center, Vector3.up).normalized;
            float level = Mathf.Lerp(water + 6, top, Random.Range(.35f, .85f));
            var across = Vector3.Cross(Vector3.up, outward) * (Random.value < .5f ? 1 : -1);
            arm.slamEnd = center + new Vector3(0, level, 0) - outward * (towerRadius + 4) + across * towerRadius * .5f;
            Go(arm, Arm.Rearing, center + outward * (towerRadius + 2) + Vector3.up * (level + 12) - across * towerRadius * .6f);
        }

        bool AnyoneOnTower()
        {
            int n = Physics.OverlapBoxNonAlloc(area.center, area.extents, hits, Quaternion.identity, 1 << Layers.Survivor, QueryTriggerInteraction.Collide);
            return n > 0;
        }

        Rigidbody PickBlock(Tentacle arm)
        {
            var outward = Vector3.ProjectOnPlane(arm.basePoint - center, Vector3.up).normalized;
            Rigidbody best = null; float bestScore = float.MaxValue;
            foreach (var b in world.Blocks)
            {
                if (!Alive(b) || claimed.Contains(b)) continue;
                var p = b.position;
                if (p.y < water + 1.5f || p.y > Mathf.Lerp(water, top, .75f)) continue;
                var flat = Vector3.ProjectOnPlane(p - center, Vector3.up);
                if (flat.magnitude < towerRadius * .8f) continue;   // only the outside of the tower, so you can see it happen
                float facing = 1 - Vector3.Dot(flat.normalized, outward);   // 0 = right in front of this arm
                float score = facing * 30 + Vector3.Distance(p, arm.basePoint) * .2f + Random.value * 4;
                if (score < bestScore) { bestScore = score; best = b; }
            }
            return best;
        }

        void Smash(Tentacle arm, float dt)
        {
            int n = Physics.OverlapSphereNonAlloc(arm.tip, 4.5f, hits, (1 << Layers.Survivor) | (1 << Layers.Block), QueryTriggerInteraction.Collide);
            var sweep = (arm.to - arm.from).normalized;
            for (int i = 0; i < n; i++)
            {
                var col = hits[i];
                var push = col.GetComponentInParent<IPushable>();
                var body = col.attachedRigidbody;
                var dir = (Vector3.ProjectOnPlane(sweep, Vector3.up).normalized + Vector3.up * .45f).normalized;
                var impulse = dir * V(2) * strength;
                if (col.gameObject.layer == Layers.Survivor) { push?.Push(impulse * dt * 6, gameObject); continue; }
                if (body && !body.isKinematic) body.AddForce(impulse * .35f, ForceMode.VelocityChange);
            }
        }

        // Network/visual state: one point per tentacle (position = tip, velocity = base, size = state, life = progress).
        public DisasterFrame Capture()
        {
            if (!Active) return null;
            if (capturedAt == Time.frameCount && cached != null) return cached;
            capturedAt = Time.frameCount;
            var points = new DisasterPoint[arms.Count];
            for (int i = 0; i < arms.Count; i++)
                points[i] = new DisasterPoint { id = i, position = arms[i].tip, velocity = arms[i].basePoint, size = (int)arms[i].state, life = Mathf.Clamp01(arms[i].t) };
            return cached = new DisasterFrame
            {
                sampledAt = Time.time, kind = KindName, label = Type ? Type.name : KindName,
                center = head, size = area.size, age = age,
                warning = Mathf.Max(0, Warning - age), remaining = Mathf.Max(0, Warning + V(4) - age),
                values = new[] { arms.Count, HeadRise, towerRadius, top, water, center.x, center.z },
                points = points
            };
        }
    }
}
