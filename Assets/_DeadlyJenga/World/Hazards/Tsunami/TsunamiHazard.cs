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
        [Min(0)] public float offshoreDistance = 160;
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
        [Header("Obstacles")]
        [Tooltip("Fraction of the push removed directly behind an intact obstacle (a tower or anchored block). 0 = off.")]
        [Range(0, 1)] public float obstacleShelter = .6f;
        [Tooltip("Metres behind an obstacle over which the water around refills the gap and the shelter fades.")]
        [Min(.5f)] public float shelterRefill = 7;

        readonly Dictionary<object, (Target target, float submerged)> wetTargets = new();
        // Shelter grid in wave space (across x along); water flows toward +along. Per wet receiver, the union of
        // all its wet colliders: along span, across span (metres) and its lowest point.
        const int ShelterX = 128, ShelterY = 48;
        readonly float[] shelterWeight = new float[ShelterX * ShelterY], shelterTop = new float[ShelterX * ShelterY];
        struct WetPlace { public float alongMin, alongMax, acrossMin, acrossMax, bottom; }
        readonly Dictionary<object, WetPlace> wetPlaces = new();
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
        Vector3 start, direction, across, alongExtents, acrossExtents;
        Quaternion queryRotation;
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
            across = Vector3.Cross(Vector3.up, direction);
            queryRotation = Quaternion.LookRotation(direction);
            alongExtents = new Vector3(Mathf.Abs(direction.x), 0, Mathf.Abs(direction.z));
            acrossExtents = new Vector3(Mathf.Abs(across.x), 0, Mathf.Abs(across.z));
            float reach = Mathf.Abs(direction.x) * bounds.extents.x + Mathf.Abs(direction.z) * bounds.extents.z;
            width = widthOverride > 0 ? widthOverride : (2 * Vector3.Dot(acrossExtents, bounds.extents) + widthMargin * 2) / .85f;
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
            LastTargetCount = 0;
            if (Services.Session is IWorldAuthority authority && !authority.HasWorldAuthority) { End(); return; }
            age += Time.fixedDeltaTime;
            if (age >= lifetime) { End(); return; }
            RefreshFrame();
            if (Wave.height < .05f || forceScale <= 0 || pushAcceleration <= 0 && liftAcceleration <= 0 && damagePerSecond <= 0) return;
            var center = Wave.center + Vector3.up * (Wave.height * .5f - 1);
            var extents = new Vector3(width * .5f, Wave.height * .5f + 1, depth * .5f);
            int mask = (affectBlocks ? 1 << Layers.Block : 0) | (affectPlayers ? 1 << Layers.Survivor : 0);
            if (mask == 0) return;
            int count;
            do
            {
                count = Physics.OverlapBoxNonAlloc(center, extents, hits, queryRotation, mask, QueryTriggerInteraction.Collide);
                if (count < hits.Length || hits.Length >= 32768) break;
                System.Array.Resize(ref hits, hits.Length * 2);
            } while (true);
            wetTargets.Clear(); wetPlaces.Clear();
            bool shelterOn = obstacleShelter > 0;
            if (shelterOn) { System.Array.Clear(shelterWeight, 0, shelterWeight.Length); System.Array.Clear(shelterTop, 0, shelterTop.Length); }
            float crest = depth * TsunamiProfile.CrestAlong;
            float flatWidth = width * .425f;
            float maximumSurface = water + Wave.height;
            for (int i = 0; i < count; i++)
            {
                var collider = hits[i];
                hits[i] = null;
                var bounds = collider.bounds;
                if (bounds.min.y >= maximumSurface) continue;
                if (!targets.TryGetValue(collider, out var target))
                {
                    target = new Target { push = HitAbilities.Get<IPushable>(collider), damage = HitAbilities.Get<IDamageable>(collider),
                        motion = HitAbilities.Get<IHazardMotion>(collider), resistance = HitAbilities.Get<IWaterResistance>(collider), body = HitAbilities.Get<Rigidbody>(collider) };
                    targets.Add(collider, target);
                }
                object receiver = (object)target.push ?? target.damage;
                if (receiver == null || target.body && target.body.isKinematic && target.motion == null) continue;
                bool alreadyWet = wetTargets.TryGetValue(receiver, out var previous);
                if (alreadyWet && previous.submerged >= 1f) continue;
                Vector3 point = bounds.center;
                var relative = point - Wave.center;
                float along = Vector3.Dot(relative, direction), lateral = Vector3.Dot(relative, across);
                float surface = SurfaceHeight(Wave, along, lateral);
                if (surface < bounds.max.y && collider.gameObject.layer == Layers.Block && collider is BoxCollider box)
                {
                    // Projected AABB intervals bound the best surface any point on this collider can reach.
                    float halfAlong = Vector3.Dot(bounds.extents, alongExtents);
                    float bestAlong = Mathf.Clamp(crest, along - halfAlong, along + halfAlong);
                    float bestLateral = Mathf.Sign(lateral) * Mathf.Max(0, Mathf.Abs(lateral) - Vector3.Dot(bounds.extents, acrossExtents));
                    if (SurfaceHeight(Wave, bestAlong, bestLateral) > Mathf.Max(surface, bounds.min.y))
                    {
                        var sample = point + direction * (crest - along) + across * (Mathf.Clamp(lateral, -flatWidth, flatWidth) - lateral);
                        var nearest = box.ClosestPoint(sample);
                        var offset = nearest - Wave.center;
                        float edgeSurface = SurfaceHeight(Wave, Vector3.Dot(offset, direction), Vector3.Dot(offset, across));
                        if (edgeSurface > surface) { point = nearest; surface = edgeSurface; }
                    }
                }
                float submerged = Mathf.Clamp01((surface - bounds.min.y) / Mathf.Max(.1f, bounds.size.y));
                if (submerged <= 0 || respectShelter && world.IsSheltered(point)) continue;
                if (!alreadyWet || submerged > previous.submerged)
                    wetTargets[receiver] = (target, submerged);
                if (shelterOn)
                {
                    float hA = Vector3.Dot(bounds.extents, alongExtents), hX = Vector3.Dot(bounds.extents, acrossExtents);
                    var place = new WetPlace { alongMin = along - hA, alongMax = along + hA, acrossMin = lateral - hX, acrossMax = lateral + hX, bottom = bounds.min.y };
                    if (wetPlaces.TryGetValue(receiver, out var known))
                    {
                        place.alongMin = Mathf.Min(place.alongMin, known.alongMin); place.alongMax = Mathf.Max(place.alongMax, known.alongMax);
                        place.acrossMin = Mathf.Min(place.acrossMin, known.acrossMin); place.acrossMax = Mathf.Max(place.acrossMax, known.acrossMax);
                        place.bottom = Mathf.Min(place.bottom, known.bottom);
                    }
                    wetPlaces[receiver] = place;
                    // Anchored blocks shelter what is behind them; blocks already riding the wave do not.
                    var body = target.body;
                    if (body && collider.gameObject.layer == Layers.Block)
                    {
                        float carried = body.isKinematic ? 0 : Mathf.Clamp01(Vector3.Dot(body.linearVelocity, direction) / Mathf.Max(1, Wave.speed));
                        float weight = 1 - carried;
                        if (weight > .15f) MarkShelter(along, lateral, Vector3.Dot(bounds.extents, alongExtents), Vector3.Dot(bounds.extents, acrossExtents), weight, bounds.max.y);
                    }
                }
            }
            LastTargetCount = wetTargets.Count;
            foreach (var (target, submerged) in wetTargets.Values)
            {
                var body = target.body;
                float resistance = target.resistance?.WaterResistance ??
                    (body ? body.mass / blockReferenceMass : 1);
                resistance = Mathf.Max(.1f, resistance);
                Vector3 velocity = target.motion?.HazardVelocity ?? (body ? body.linearVelocity : Vector3.zero);
                float force = forceScale * submerged * Time.fixedDeltaTime / resistance;
                if (shelterOn) force *= 1 - obstacleShelter * Shelter(target.push != null ? target.push : target.damage);
                float carried = new Vector2(Mathf.Max(0, Vector3.Dot(velocity, direction)), Mathf.Max(0, velocity.y)).magnitude;
                var push = Vector3.ClampMagnitude((direction * pushAcceleration + Vector3.up * liftAcceleration) * force,
                    Mathf.Max(0, maximumCarriedSpeed - carried));
                if (push.sqrMagnitude > .000001f) target.push?.Push(push, gameObject);
                if (damagePerSecond > 0) target.damage?.Damage(damagePerSecond * forceScale * submerged * Time.fixedDeltaTime, gameObject);
            }
        }

        void MarkShelter(float along, float lateral, float halfAlong, float halfAcross, float weight, float top)
        {
            int y0 = Mathf.Max(0, Mathf.FloorToInt(((along - halfAlong) / depth + .5f) * ShelterY)), y1 = Mathf.Min(ShelterY - 1, Mathf.FloorToInt(((along + halfAlong) / depth + .5f) * ShelterY));
            int x0 = Mathf.Max(0, Mathf.FloorToInt(((lateral - halfAcross) / width + .5f) * ShelterX)), x1 = Mathf.Min(ShelterX - 1, Mathf.FloorToInt(((lateral + halfAcross) / width + .5f) * ShelterX));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * ShelterX + x;
                    shelterWeight[i] = Mathf.Max(shelterWeight[i], weight);
                    shelterTop[i] = Mathf.Max(shelterTop[i], top);
                }
        }

        // 0..1: how much an intact obstacle upstream (and at least as tall) blocks the water reaching this receiver.
        float Shelter(object receiver)
        {
            if (receiver == null || !wetPlaces.TryGetValue(receiver, out var place)) return 0;
            // Only cells strictly upstream of the receiver's own front face, and only across its own width.
            int front = Mathf.FloorToInt((place.alongMin / depth + .5f) * ShelterY) - 1;
            int reach = Mathf.CeilToInt(shelterRefill / depth * ShelterY);
            float inset = width / ShelterX * .5f;   // ignore cells it merely grazes at its sides
            int x0 = Mathf.Max(0, Mathf.FloorToInt(((place.acrossMin + inset) / width + .5f) * ShelterX));
            int x1 = Mathf.Min(ShelterX - 1, Mathf.FloorToInt(((place.acrossMax - inset) / width + .5f) * ShelterX));
            if (x1 < x0) x0 = x1 = Mathf.Clamp(Mathf.FloorToInt(((place.acrossMin + place.acrossMax) * .5f / width + .5f) * ShelterX), 0, ShelterX - 1);
            float best = 0;
            for (int k = 0; k < reach; k++)
            {
                int y = front - k;
                if (y < 0) break;
                float fade = 1 - k / (float)(reach + 1);
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * ShelterX + x;
                    if (shelterWeight[i] > 0 && shelterTop[i] > place.bottom + .3f) best = Mathf.Max(best, shelterWeight[i] * fade);
                }
            }
            return best;
        }

        public static float SurfaceHeight(WaveFrame wave, Vector3 point)
        {
            Vector3 relative = point - wave.center;
            return SurfaceHeight(wave, Vector3.Dot(relative, wave.direction),
                Vector3.Dot(relative, Vector3.Cross(Vector3.up, wave.direction)));
        }

        static float SurfaceHeight(WaveFrame wave, float along, float lateral)
        {
            float halfWidth = wave.width * .5f, x = Mathf.Abs(lateral);
            float t = along / wave.depth + .5f;
            if (t <= 0 || t >= 1 || x >= halfWidth) return float.NegativeInfinity;
            if (TsunamiProfile.Available)
                return wave.waterHeight + wave.height * TsunamiProfile.Envelope(along / wave.depth, lateral / wave.width);
            float shape = t < .72f ? Mathf.SmoothStep(0, 1, t / .72f) : 1 - Mathf.SmoothStep(0, 1, (t - .72f) / .28f);
            float edge = x <= halfWidth * .85f ? 1 : 1 - Mathf.SmoothStep(0, 1, (x / halfWidth - .85f) / .15f);
            return wave.waterHeight + wave.height * shape * edge;
        }

        protected override void EndHazard()
        {
            Tracked.Waves.Remove(this); Wave = default; wetTargets.Clear(); targets.Clear();
        }
    }
}
