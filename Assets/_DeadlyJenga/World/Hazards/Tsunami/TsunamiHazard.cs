using Unity.Profiling;
using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.World.Hazards
{
    public sealed class TsunamiHazard : MapHazard, IWaveState, IWaveTuning, IDemoHazard
    {
        static readonly ProfilerMarker DJTsunamiMarker = new ProfilerMarker("DJ.Tsunami");
        [Header("Shape (metres)")]
        [Min(.1f)] public float height = 22;
        [Min(1)] public float depth = 28;
        [Min(0)] public float widthOverride;
        [Min(0)] public float widthMargin = 12;
        [Header("Approach")]
        public bool randomDirection = true;
        [Tooltip("Coming FROM this heading: 0 = north (+Z), 90 = east (+X).")]
        public float approachHeading;
        [Min(0)] public float offshoreDistance = 45;
        [Min(0)] public float warningTime = 5;
        [Min(.1f)] public float riseTime = 4;
        [Min(.1f)] public float approachSpeed = 8;
        [Min(0)] public float recessionDistance = 20;
        [Min(.1f)] public float fallTime = 4;
        [Tooltip("Base Duration is an optional safety timeout. Route timing normally determines lifetime.")]
        public bool useDurationTimeout;
        [Header("Forces")]
        [Min(0)] public float pushAcceleration = 9;
        [Min(0)] public float liftAcceleration = 5;
        [Min(.1f)] public float maximumCarriedSpeed = 12;
        [Min(.01f)] public float blockReferenceMass = 40;
        public bool affectPlayers = true, affectBlocks = true;
        public bool respectShelter;
        [Min(0)] public float damagePerSecond;
        [Header("Intensity multipliers")]
        [Range(0, 1)] public float heightIntensity = .5f;
        [Range(0, 1)] public float forceIntensity = 1;

        readonly HashSet<object> touched = new HashSet<object>();
        sealed class Target
        {
            public IPushable push;
            public IDamageable damage;
            public IHazardMotion motion;
            public IWaterResistance resistance;
            public Rigidbody body;
        }
        readonly Dictionary<Collider, Target> targets = new Dictionary<Collider, Target>();
        Collider[] hits = new Collider[2048];
        IWorldQuery world;
        Vector3 start, direction;
        float age, width, peak, fallStart, lifetime, water, forceScale;
        public int LastTargetCount { get; private set; }
        public WaveFrame Wave { get; private set; }
        public bool Toggle => false;
        public IReadOnlyList<DisasterParameter> Parameters => new[] {
            new DisasterParameter("speed","Wave speed",.5f,30,approachSpeed,"m/s"),
            new DisasterParameter("force","Push / lift force",0,5,1,"×"),
            new DisasterParameter("height","Wave height",1,100,height,"m"),
            new DisasterParameter("width","Wave width (0 = fit map)",0,600,widthOverride,"m"),
            new DisasterParameter("warning","Warning time",0,15,warningTime,"s") };
        public void Configure(float[] values)
        {
            if(Active||values==null||values.Length!=Parameters.Count)throw new System.ArgumentException("Invalid tsunami settings.");
            foreach(float value in values)if(float.IsNaN(value)||float.IsInfinity(value))throw new System.ArgumentException("Settings must be finite.");
            Configure(values[0],values[1]);height=Mathf.Clamp(values[2],1,100);widthOverride=Mathf.Clamp(values[3],0,600);warningTime=Mathf.Clamp(values[4],0,15);
        }

        bool tuned;
        float originalPush, originalLift;
        public void Configure(float speed, float forceMultiplier)
        {
            if (Active || float.IsNaN(speed) || float.IsInfinity(speed) ||
                float.IsNaN(forceMultiplier) || float.IsInfinity(forceMultiplier)) return;
            if (!tuned) { originalPush = pushAcceleration; originalLift = liftAcceleration; tuned = true; }
            approachSpeed = Mathf.Clamp(speed, .5f, 30);
            float multiplier = Mathf.Clamp(forceMultiplier, 0, 5);
            pushAcceleration = originalPush * multiplier;
            liftAcceleration = originalLift * multiplier;
        }

        protected override bool ValidateSettings(out string error)
        {
            error = null;
            float[] values = { height, depth, widthOverride, widthMargin, offshoreDistance, warningTime,
                riseTime, approachSpeed, recessionDistance, fallTime, pushAcceleration, liftAcceleration,
                maximumCarriedSpeed, blockReferenceMass, damagePerSecond, heightIntensity, forceIntensity };
            foreach (float value in values)
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0) error = "Tsunami settings must be finite and nonnegative.";
            if (float.IsNaN(approachHeading) || float.IsInfinity(approachHeading) || height <= 0 || depth <= 0 || riseTime <= 0 || approachSpeed <= 0 ||
                fallTime <= 0 || maximumCarriedSpeed <= 0 || blockReferenceMass <= 0 || heightIntensity > 1 || forceIntensity > 1)
                error = "Tsunami dimensions, speeds and phase times must be positive; intensity multipliers must be 0..1.";
            return error == null;
        }

        protected override void BeginHazard(IWorldQuery map, Bounds bounds, float strength, float seconds)
        {
            world = map; age = 0;
            float angle = (randomDirection ? Random.Range(0f, 360f) : approachHeading) * Mathf.Deg2Rad;
            direction = -new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            var side = Vector3.Cross(Vector3.up, direction);
            float reach = Mathf.Abs(direction.x) * bounds.extents.x + Mathf.Abs(direction.z) * bounds.extents.z;
            width = widthOverride > 0 ? widthOverride : (2 * (Mathf.Abs(side.x) * bounds.extents.x + Mathf.Abs(side.z) * bounds.extents.z) + widthMargin * 2) / .85f;
            water = map.WaterHeight(bounds.center);
            start = bounds.center - direction * (reach + offshoreDistance + depth * .5f);
            start.y = water;
            peak = height * Mathf.Lerp(1, strength, heightIntensity);
            forceScale = Mathf.Lerp(1, strength, forceIntensity);
            fallStart = warningTime + Mathf.Max(riseTime, (2 * reach + offshoreDistance + depth + recessionDistance) / approachSpeed);
            lifetime = fallStart + fallTime;
            if (useDurationTimeout) lifetime = Mathf.Min(lifetime, seconds);
            Tracked.Waves.Add(this);
            RefreshFrame();
        }

        void RefreshFrame()
        {
            float travelAge = Mathf.Max(0, age - warningTime);
            float envelope = Mathf.SmoothStep(0, 1, travelAge / riseTime) * (1 - Mathf.SmoothStep(0, 1, (age - fallStart) / fallTime));
            float nextAge = age + .02f;
            float nextEnvelope = Mathf.SmoothStep(0, 1, Mathf.Max(0, nextAge - warningTime) / riseTime) *
                (1 - Mathf.SmoothStep(0, 1, (nextAge - fallStart) / fallTime));
            Wave = new WaveFrame { active = true, center = start + direction * (approachSpeed * travelAge), direction = direction,
                width = width, depth = depth, height = peak * envelope, waterHeight = water, carriedSpeedLimit = maximumCarriedSpeed,
                speed = age >= warningTime ? approachSpeed : 0, heightRate = peak * (nextEnvelope - envelope) / .02f,
                warningRemaining = Mathf.Max(0, warningTime - age), remaining = Mathf.Max(0, lifetime - age) };
            transform.position = Wave.center;
        }

        void FixedUpdate()
        {
            using var measurement = DJTsunamiMarker.Auto();
            if (!Active) return;
            if (Services.Session is IWorldAuthority authority && !authority.HasWorldAuthority) { End(); return; }
            age += Time.fixedDeltaTime;
            if (age >= lifetime) { End(); return; }
            RefreshFrame();
            if (Wave.height < .05f) return;
            var center = Wave.center + Vector3.up * (Wave.height * .5f - 1);
            var extents = new Vector3(width * .5f, Wave.height * .5f + 1, depth * .5f);
            var rotation = Quaternion.LookRotation(direction);
            int mask = (affectBlocks ? 1 << Layers.Block : 0) | (affectPlayers ? 1 << Layers.Survivor : 0);
            int count;
            do
            {
                count = Physics.OverlapBoxNonAlloc(center, extents, hits, rotation, mask, QueryTriggerInteraction.Collide);
                if (count < hits.Length || hits.Length >= 32768) break;
                System.Array.Resize(ref hits, hits.Length * 2);
            } while (true);
            touched.Clear(); LastTargetCount = 0;
            for (int i = 0; i < count; i++)
            {
                var collider = hits[i];
                hits[i] = null;
                if (!targets.TryGetValue(collider, out var target))
                {
                    target = new Target { push = HitAbilities.Get<IPushable>(collider), damage = HitAbilities.Get<IDamageable>(collider),
                        motion = HitAbilities.Get<IHazardMotion>(collider), resistance = HitAbilities.Get<IWaterResistance>(collider), body = HitAbilities.Get<Rigidbody>(collider) };
                    targets.Add(collider, target);
                }
                var pushable = target.push;
                object receiver = (object)pushable ?? target.damage;
                if (receiver == null || touched.Contains(receiver)) continue;
                var bounds = collider.bounds;
                Vector3 point = bounds.center;
                float surface = SurfaceHeight(Wave, point);
                float submerged = Mathf.Clamp01((surface - bounds.min.y) / Mathf.Max(.1f, bounds.size.y));
                if (submerged <= 0 || respectShelter && world.IsSheltered(point)) continue;
                touched.Add(receiver); LastTargetCount++;
                var body = target.body;
                if (body && body.isKinematic && target.motion == null) continue;
                float resistance = target.resistance?.WaterResistance ??
                    (body ? body.mass / blockReferenceMass : 1);
                resistance = Mathf.Max(.1f, resistance);
                Vector3 velocity = target.motion?.HazardVelocity ?? (body ? body.linearVelocity : Vector3.zero);
                float force = forceScale * submerged * Time.fixedDeltaTime / resistance;
                float carried = new Vector2(Mathf.Max(0, Vector3.Dot(velocity, direction)), Mathf.Max(0, velocity.y)).magnitude;
                var push = Vector3.ClampMagnitude((direction * pushAcceleration + Vector3.up * liftAcceleration) * force,
                    Mathf.Max(0, maximumCarriedSpeed - carried));
                if (push.sqrMagnitude > .000001f) pushable?.Push(push, gameObject);
                if (damagePerSecond > 0) target.damage?.Damage(damagePerSecond * forceScale * submerged * Time.fixedDeltaTime, gameObject);
            }
        }

        public static float SurfaceHeight(WaveFrame wave, Vector3 point)
        {
            Vector3 relative = point - wave.center;
            float x = Vector3.Dot(relative, Vector3.Cross(Vector3.up, wave.direction));
            float t = Vector3.Dot(relative, wave.direction) / wave.depth + .5f;
            if (t <= 0 || t >= 1 || Mathf.Abs(x) >= wave.width * .5f) return float.NegativeInfinity;
            float shape = t < .72f ? Mathf.SmoothStep(0, 1, t / .72f) : 1 - Mathf.SmoothStep(0, 1, (t - .72f) / .28f);
            float edge = 1 - Mathf.SmoothStep(0, 1, (Mathf.Abs(x) / (wave.width * .5f) - .85f) / .15f);
            return wave.waterHeight + wave.height * shape * edge;
        }

        protected override void EndHazard()
        {
            Tracked.Waves.Remove(this); Wave = default; touched.Clear(); targets.Clear();
        }
    }
}
