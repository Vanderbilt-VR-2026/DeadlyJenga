using System.Collections.Generic;
using DeadlyJenga.CandyArt;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeadlyJenga.Presentation
{
    // Looks only. Watches blocks and characters through Core and shows:
    //  - a splash, foam ring and bubble trail when a block drops into the water,
    //  - a caving crater with sand ripples while a block sinks into the sand,
    //  - an underwater filter whenever the camera ends up below the water,
    //  - a cinnamon-sugar sand coating on characters who escape sinking sand.
    // Each client observes the locally available block poses.
    public sealed class CandyWaterSandEffects : MonoBehaviour
    {
        const int BlocksPerFrame = 96;
        const float SplashSpeed = 1.2f;
        static readonly Color SandDark = new(.62f, .47f, .30f), SandLight = new(.93f, .83f, .62f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()
        {
            GameEvents.SessionStarted -= Attach; GameEvents.SessionStarted += Attach;
            GameEvents.LobbyStarted -= Attach; GameEvents.LobbyStarted += Attach;
        }
        static void Attach(GameObject root)
        {
            if (root && !root.GetComponent<CandyWaterSandEffects>()) root.AddComponent<CandyWaterSandEffects>();
        }

        // ---------- shared particles ----------
        ParticleSystem droplets, splashMist, bubbles, ripples, dust, sugar;
        CandyBlockWhitewater whitewater;
        readonly List<Object> owned = new();

        void Awake()
        {
            var soft = SoftDot(32);
            var ring = RingTexture(64);
            droplets = Particles("Water splash droplets", SprayTexture(), 1100, 1.9f, false);
            var sprayRenderer = droplets.GetComponent<ParticleSystemRenderer>();
            sprayRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            sprayRenderer.velocityScale = .045f;
            sprayRenderer.lengthScale = 1.15f;
            splashMist = Particles("Whitewater mist", MistTexture(), 280, .48f, false);
            whitewater = new CandyBlockWhitewater(transform);
            bubbles = Particles("Underwater bubbles", soft, 300, -.25f, false);
            ripples = Particles("Sand ripples", ring, 120, 0, true);
            dust = Particles("Sand dust", soft, 300, .08f, false);
            sugar = SugarParticles();
        }

        void OnDestroy()
        {
            if (waterMaterial) Destroy(waterMaterial);
            whitewater?.Dispose();
            foreach (var coat in coats.Values) coat.Remove();
            ResetCraters();
            foreach (var o in owned) if (o) Destroy(o);
        }

        void Update()
        {
            var world = Services.World;
            if (!ReferenceEquals(world, lastWorld))
            {
                ResetCraters();
                seen.Clear();
                bubbling.Clear();
                lastWorld = world;
                cursor = 0;
                droplets.Clear();
                splashMist.Clear();
                whitewater.Clear();
                bubbles.Clear();
            }
            if (world != null) ScanBlocks(world);
            whitewater.Tick();
            UpdateBubbles(world);
            UpdateCraters();
            UpdateUnderwater(world);
            UpdateCoatings();
        }

        // ---------- blocks: water entry and sand sinking ----------
        struct Seen
        {
            public Vector3 center;
            public float gap, time, downSpeed, splashAt;
            public bool armed;
        }
        readonly Dictionary<Rigidbody, Seen> seen = new();
        IWorldQuery lastWorld;
        int cursor;
        float nextPrune;
        readonly List<Rigidbody> staleBodies = new();

        void ScanBlocks(IWorldQuery world)
        {
            if (Time.time >= nextPrune)
            {
                nextPrune = Time.time + 1f;
                staleBodies.Clear();
                foreach (var entry in seen)
                    if (!entry.Key || !entry.Key.gameObject.activeInHierarchy) staleBodies.Add(entry.Key);
                foreach (var body in staleBodies) seen.Remove(body);
            }

            var blocks = world.Blocks;
            if (blocks == null || blocks.Count == 0) return;
            // At slower frame rates a fixed per-frame budget can miss the entire entry.
            int scans = Mathf.Clamp(Mathf.CeilToInt(Time.unscaledDeltaTime * 35), 1, 8);
            int count = Mathf.Min(BlocksPerFrame * scans, blocks.Count);
            for (int n = 0; n < count; n++)
            {
                var body = blocks[cursor++ % blocks.Count];
                if (!body || !body.gameObject.activeInHierarchy)
                {
                    if (!ReferenceEquals(body, null)) seen.Remove(body);
                    continue;
                }
                var shape = body.GetComponent<Collider>();
                if (!shape) continue;
                var bounds = shape.bounds;
                float water = world.WaterHeight(bounds.center);
                float gap = bounds.min.y - water;
                bool observed = seen.TryGetValue(body, out var before);
                float now = Time.time;
                float downSpeed = body.isKinematic ? 0 : Mathf.Max(0, -body.linearVelocity.y);
                float splashAt = observed ? before.splashAt : float.NegativeInfinity;
                bool armed = before.armed || (gap > .08f && now - splashAt >= .45f);
                if (observed)
                {
                    float dt = Mathf.Max(.001f, now - before.time);
                    float descent = (before.center.y - bounds.center.y) / dt;
                    float impact = Mathf.Max(before.downSpeed, descent);
                    if (armed && before.gap >= 0 && gap < 0)
                    {
                        // Interpolate the last dry pose to the water plane at the crossing.
                        float t = before.gap / (before.gap - gap);
                        var point = Vector3.Lerp(before.center, bounds.center, t);
                        point.y = world.WaterHeight(point);
                        if (impact >= SplashSpeed && OpenWater(point, point.y))
                        {
                            var drift = new Vector2(bounds.center.x - before.center.x, bounds.center.z - before.center.z);
                            Splash(point, bounds.size, impact, Vector2.ClampMagnitude(drift / (dt * 8), 1), body);
                            splashAt = now;
                        }
                        armed = false;
                    }
                    else if (body.isKinematic && !shape.isTrigger && descent > .04f && descent < 1.5f)
                        CheckSand(body, shape, bounds, water);
                }
                seen[body] = new Seen { gap = gap, center = bounds.center, time = now, downSpeed = downSpeed, armed = armed, splashAt = splashAt };
            }
            if (cursor > 100000000) cursor = 0;
        }

        void Splash(Vector3 at, Vector3 size, float speed, Vector2 drift, Rigidbody block)
        {
            float footprint = Mathf.Clamp(new Vector2(size.x, size.z).magnitude * .5f, .32f, 2.4f);
            float energy = Mathf.Clamp(speed / 10f, .15f, 1.6f);
            whitewater.Emit(at, footprint, energy, drift);

            var emit = new ParticleSystem.EmitParams();
            int sprays = Mathf.RoundToInt(45 + 78 * energy * Mathf.Clamp(footprint / .78f, .65f, 1.35f));
            float seed = Random.Range(0, 20f);
            for (int i = 0; i < sprays; i++)
            {
                float angle = Random.Range(0, 2f * Mathf.PI);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float side = Vector2.Dot(direction, drift);
                float lobe = .74f + .26f * Mathf.Sin(angle * 5 + seed);
                bool jet = i % 4 == 0;
                float speedOut = Random.Range(3.8f, 7.2f) * lobe * (.68f + energy * .58f + .24f * side);
                float speedUp = Random.Range(3f, 6.1f) * lobe * (.65f + energy * .71f);
                if (jet) speedUp *= 1.52f;
                emit.position = at + new Vector3(direction.x, .08f, direction.y) * footprint * Random.Range(.36f, .85f);
                emit.velocity = new Vector3(direction.x * speedOut, speedUp, direction.y * speedOut);
                emit.startSize = Random.Range(.025f, .11f) * Mathf.Lerp(.9f, 1.55f, energy / 1.6f);
                emit.startLifetime = Random.Range(.36f, .92f);
                emit.startColor = new Color(.92f, .99f, 1f, Random.Range(.65f, .98f));
                droplets.Emit(emit, 1);
            }
            int mistCount = Mathf.RoundToInt(12 + 22 * energy);
            for (int i = 0; i < mistCount; i++)
            {
                var direction = Random.insideUnitCircle.normalized;
                float spread = Random.Range(2.2f, 4.5f) * (.60f + energy * .48f);
                emit.position = at + new Vector3(direction.x, .12f, direction.y) * footprint * .8f;
                emit.velocity = new Vector3(direction.x * spread, Random.Range(1.6f, 4.5f) * (.6f + energy * .48f), direction.y * spread);
                emit.startSize = Random.Range(.25f, .64f) * (.65f + energy * .52f);
                emit.startLifetime = Random.Range(.35f, .77f);
                emit.startColor = new Color(.90f, .97f, 1f, Random.Range(.20f, .48f));
                splashMist.Emit(emit, 1);
            }

            if (block) bubbling.Add(new Bubbler { block = block, until = Time.time + 3f, next = 0 });
        }

        // Bubbles rise from a block for a few seconds after it goes under.
        sealed class Bubbler { public Rigidbody block; public float until, next; }
        readonly List<Bubbler> bubbling = new();

        void UpdateBubbles(IWorldQuery world)
        {
            var emit = new ParticleSystem.EmitParams();
            for (int i = bubbling.Count - 1; i >= 0; i--)
            {
                var b = bubbling[i];
                if (!b.block || !b.block.gameObject.activeInHierarchy || Time.time > b.until || world == null) { bubbling.RemoveAt(i); continue; }
                if (Time.time < b.next) continue;
                b.next = Time.time + .09f;
                var p = b.block.worldCenterOfMass;
                float water = world.WaterHeight(p);
                if (p.y > water) continue;
                emit.position = p + Random.insideUnitSphere * .3f;
                emit.velocity = new Vector3(Random.Range(-.15f, .15f), Random.Range(.6f, 1.1f), Random.Range(-.15f, .15f));
                emit.startSize = Random.Range(.04f, .11f);
                emit.startLifetime = Mathf.Clamp((water - p.y) / .85f, .3f, 2.5f);
                emit.startColor = new Color(.85f, .97f, 1f, .7f);
                bubbles.Emit(emit, 1);
            }
        }

        // ---------- sand cave-in ----------
        sealed class Crater
        {
            public Collider shape;
            public CandySandbar terrain;
            public Vector3 point;
            public float footprint, started, ended = -1, nextRipple, depth;
        }

        const int MaxCraters = 24;
        readonly Dictionary<Rigidbody, Crater> craters = new();
        readonly List<Rigidbody> finished = new();
        readonly Dictionary<CandySandbar, List<Vector4>> terrainCraters = new();
        CandySandbar[] sandbars;
        float nextDeform;

        // Ground layers only: no blocks, survivors or their climb surfaces.
        static int GroundMask => ~((1 << Layers.Block) | (1 << Layers.Survivor) | (1 << Layers.Surface));

        static bool OpenWater(Vector3 at, float water) =>
            !Physics.Raycast(new Vector3(at.x, water + .05f, at.z), Vector3.down, out var hit, .55f, GroundMask, QueryTriggerInteraction.Ignore) || hit.rigidbody;

        void CheckSand(Rigidbody block, Collider shape, Bounds bounds, float water)
        {
            if (craters.Count >= MaxCraters || craters.ContainsKey(block)) return;
            var from = new Vector3(bounds.center.x, bounds.max.y + .6f, bounds.center.z);
            if (!Physics.Raycast(from, Vector3.down, out var hit, bounds.size.y + 2.5f,
                    GroundMask, QueryTriggerInteraction.Ignore) ||
                hit.rigidbody || hit.normal.y < .6f || hit.point.y < water - .3f) return;

            sandbars ??= FindObjectsByType<CandySandbar>(FindObjectsSortMode.None);
            foreach (var sandbar in sandbars)
            {
                if (!sandbar || !sandbar.TrySampleSurface(hit.point, out var surface) ||
                    Mathf.Abs(surface.y - hit.point.y) > .45f || surface.y < bounds.min.y + .04f) continue;

                craters.Add(block, new Crater
                {
                    shape = shape,
                    terrain = sandbar,
                    point = surface,
                    started = Time.time,
                    footprint = Mathf.Clamp(new Vector2(bounds.size.x, bounds.size.z).magnitude * .5f, .35f, 2f)
                });
                if (!terrainCraters.ContainsKey(sandbar)) terrainCraters.Add(sandbar, new List<Vector4>());
                break;
            }
        }

        void UpdateCraters()
        {
            if (craters.Count == 0 && terrainCraters.Count == 0) return;
            float now = Time.time;
            bool rebuild = now >= nextDeform;
            if (rebuild)
            {
                nextDeform = now + .1f;
                foreach (var areas in terrainCraters.Values) areas.Clear();
            }
            finished.Clear();
            var emit = new ParticleSystem.EmitParams();
            foreach (var pair in craters)
            {
                var c = pair.Value;
                var body = pair.Key;
                bool sinking = body && body.gameObject.activeInHierarchy && body.isKinematic;
                if (!sinking && c.ended < 0) c.ended = now;
                float fade = c.ended < 0 ? 1 : 1 - Mathf.Clamp01((now - c.ended) / 3.5f);
                if (fade <= 0 || !c.terrain) { finished.Add(body); continue; }

                if (sinking && c.shape) c.depth = Mathf.Max(c.depth, c.point.y - c.shape.bounds.min.y);
                if (rebuild)
                {
                    float grow = Mathf.Clamp01((now - c.started) / 2.5f);
                    float radius = c.footprint * Mathf.Lerp(1.65f, 2.8f, grow);
                    float depth = Mathf.Min(c.footprint * .74f, c.depth * .85f) * fade;
                    var local = c.terrain.transform.InverseTransformPoint(c.point);
                    terrainCraters[c.terrain].Add(new Vector4(local.x, local.z, radius, depth));
                }

                if (!sinking || now < c.nextRipple) continue;
                c.nextRipple = now + Random.Range(.45f, .7f);
                emit.position = c.point + Vector3.up * .03f;
                emit.velocity = Vector3.zero;
                emit.rotation3D = new Vector3(90, Random.Range(0, 360f), 0);
                emit.startSize = c.footprint * 3.9f;
                emit.startLifetime = 1.6f;
                emit.startColor = new Color(SandDark.r, SandDark.g, SandDark.b, .32f);
                ripples.Emit(emit, 1);
                emit.ResetRotation();
                for (int i = 0; i < 4; i++)
                {
                    var rim = Random.insideUnitCircle.normalized * c.footprint * Random.Range(.9f, 1.4f);
                    emit.position = c.point + new Vector3(rim.x, .05f, rim.y);
                    emit.velocity = new Vector3(rim.x * -.3f, Random.Range(.25f, .6f), rim.y * -.3f);
                    emit.startSize = Random.Range(.08f, .18f);
                    emit.startLifetime = Random.Range(.6f, 1f);
                    emit.startColor = new Color(SandLight.r, SandLight.g, SandLight.b, .8f);
                    dust.Emit(emit, 1);
                }
            }
            foreach (var body in finished) craters.Remove(body);
            if (rebuild)
                foreach (var pair in terrainCraters)
                    if (pair.Key) pair.Key.ApplyCaveIns(pair.Value);
        }

        void ResetCraters()
        {
            foreach (var terrain in terrainCraters.Keys)
                if (terrain) terrain.ApplyCaveIns(System.Array.Empty<Vector4>());
            terrainCraters.Clear();
            craters.Clear();
            sandbars = null;
            nextDeform = 0;
            if (ripples) ripples.Clear();
        }

        // ---------- underwater filter ----------
        Camera waterEye;
        IWorldQuery waterWorld;
        Material waterMaterial;
        CandyWaveImmersionPass waterPass;
        float underwater, dippedFor, hiddenDepth;
        const float UnderwaterDelay = .2f;

        void UpdateUnderwater(IWorldQuery world)
        {
            var session = Services.Session;
            var eye = session?.ActiveCamera;
            if (!eye) eye = Camera.main;
            if (eye != waterEye || !ReferenceEquals(world, waterWorld))
            {
                waterEye = eye;
                waterWorld = world;
                underwater = dippedFor = hiddenDepth = 0;
            }
            if (!eye || world == null) return;

            var point = eye.transform.position;
            float water = world.WaterHeight(point);
            float depth = water - point.y;
            var player = session?.Player;
            var sand = player != null && player.gameObject && eye == player.ViewCamera
                ? player.gameObject.GetComponent<ISandState>() : null;
            bool sandContact = sand != null && sand.InSand;
            float terrainDepth = 0;
            if (depth > .02f)
            {
                sandbars ??= FindObjectsByType<CandySandbar>(FindObjectsSortMode.None);
                foreach (var terrain in sandbars)
                    if (terrain && terrain.TrySampleSurface(point, out var surface))
                        terrainDepth = Mathf.Max(terrainDepth, surface.y - point.y);

                var above = new Vector3(point.x, water + 2, point.z);
                if (Physics.Raycast(above, Vector3.down, out var hit, depth + 2, GroundMask, QueryTriggerInteraction.Ignore) && !hit.rigidbody)
                    terrainDepth = Mathf.Max(terrainDepth, hit.point.y - point.y);
            }

            bool deep = depth >= 4 || terrainDepth >= 1.1f;
            bool dipped = depth > .02f && !sandContact && terrainDepth < .05f && OpenWater(point, water);
            float dt = Time.unscaledDeltaTime;
            dippedFor = dipped ? dippedFor + dt : 0;
            underwater = deep ? 1 : Mathf.MoveTowards(underwater, dippedFor >= UnderwaterDelay ? 1 : 0, dt * 5);
            hiddenDepth = deep ? 1 : Mathf.MoveTowards(hiddenDepth, 0, dt * 6);
        }

        void OnEnable() => RenderPipelineManager.beginCameraRendering += BeforeRender;

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeRender;
            ResetCraters();
            seen.Clear();
            bubbling.Clear();
            whitewater?.Clear();
            waterEye = null;
            underwater = dippedFor = hiddenDepth = 0;
        }

        void BeforeRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera != waterEye || underwater <= .005f ||
                (Services.Session is Component session && session.gameObject != gameObject) ||
                !(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) return;

            if (waterPass == null)
            {
                var shader = Resources.Load<Shader>("CandyScenery/CandyWaveImmersion");
                if (!shader || !shader.isSupported) return;
                waterMaterial = new Material(shader);
                waterPass = new CandyWaveImmersionPass(waterMaterial);
            }
            waterPass.State = new Vector4(underwater, 0, Time.unscaledTime, camera.stereoEnabled ? .002f : .005f);
            waterPass.SceneOcclusion = hiddenDepth;
            camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(waterPass);
        }

        // ---------- cinnamon-sugar sand coating ----------
        sealed class Coat
        {
            public Component owner; public ISandState sand;
            public float sandSeconds, coatedAt = -1, dustNext;
            public readonly List<CandySugarSurface> surfaces = new();
            public Material material;
            public float footOffset, waistOffset;

            public void Remove()
            {
                foreach (var surface in surfaces) surface.Dispose();
                surfaces.Clear();
                if (material) Object.Destroy(material);
                coatedAt = -1;
            }
        }
        const float SandSecondsToCoat = 1f, CoatSeconds = 20f, DustOffSeconds = 2.5f;
        readonly Dictionary<int, Coat> coats = new();
        readonly List<int> goneCoats = new();
        Shader coatShader;

        void UpdateCoatings()
        {
            foreach (var mover in Tracked.Movers)
            {
                if (!(mover is ISandState sand) || !(mover is Component owner) || !owner) continue;
                int id = owner.GetInstanceID();
                if (!coats.TryGetValue(id, out var coat)) coats[id] = coat = new Coat { owner = owner, sand = sand };
                if (sand.InSand) { coat.sandSeconds += Time.deltaTime; continue; }
                if (coat.sandSeconds >= SandSecondsToCoat) Apply(coat);
                coat.sandSeconds = 0;
            }
            goneCoats.Clear();
            foreach (var pair in coats)
            {
                var coat = pair.Value;
                if (!coat.owner) { coat.Remove(); goneCoats.Add(pair.Key); continue; }
                if (coat.coatedAt < 0) continue;
                float age = Time.time - coat.coatedAt;
                if (age >= CoatSeconds) { coat.Remove(); continue; }
                float feet = coat.owner.transform.position.y;
                coat.material.SetFloat("_CoatBottom", feet + coat.footOffset);
                coat.material.SetFloat("_CoatTop", feet + coat.waistOffset);
                // Dust off over the last seconds: grains drop away and fall as a light puff.
                float coverage = 1 - Mathf.Clamp01((age - (CoatSeconds - DustOffSeconds)) / DustOffSeconds);
                coat.material.SetFloat("_Coverage", coverage);
                if (coverage < 1 && Time.time >= coat.dustNext)
                {
                    coat.dustNext = Time.time + .06f;
                    if (coat.surfaces.Count == 0) continue;
                    var surface = coat.surfaces[Random.Range(0, coat.surfaces.Count)];
                    if (!surface.TrySample(out var position)) continue;
                    if (position.y > feet + coat.waistOffset || position.y < feet + coat.footOffset) continue;
                    var emit = new ParticleSystem.EmitParams
                    {
                        position = position,
                        velocity = new Vector3(Random.Range(-.08f, .08f), Random.Range(-.18f, -.04f), Random.Range(-.08f, .08f)),
                        startSize = Random.Range(.0005f, .0015f), startLifetime = Random.Range(.45f, .85f),
                        rotation3D = Random.insideUnitSphere * 180,
                        startColor = Color.Lerp(new Color(.78f, .69f, .53f), new Color(1f, .96f, .86f), Random.value)
                    };
                    sugar.Emit(emit, Random.Range(6, 11));
                    emit.startSize = Random.Range(.018f, .04f);
                    emit.startLifetime = Random.Range(.25f, .45f);
                    emit.startColor = new Color(.93f, .88f, .76f, .10f);
                    emit.velocity *= .35f;
                    dust.Emit(emit, 1);

                }
            }
            foreach (int id in goneCoats) coats.Remove(id);
        }

        void Apply(Coat coat)
        {
            coat.Remove();
            var visual = coat.owner.GetComponent<IModeActor>()?.Visual;
            if (!visual) return;
            if (!coatShader) coatShader = Resources.Load<Shader>("CandySandCoating");
            if (!coatShader) return;
            var renderers = visual.GetComponentsInChildren<Renderer>(false);
            var bodies = new List<Renderer>();
            float low = float.MaxValue, high = float.MinValue;
            foreach (var r in renderers)
            {
                if (!(r is SkinnedMeshRenderer || r is MeshRenderer) || r.name == "Sticky sugar surface") continue;
                bodies.Add(r);
                low = Mathf.Min(low, r.bounds.min.y); high = Mathf.Max(high, r.bounds.max.y);
            }
            if (bodies.Count == 0) return;
            coat.material = new Material(coatShader) { name = "Sticky sugar coating" };
            coat.material.SetFloat("_Seed", Random.value * 100);
            coat.material.SetTexture("_CrystalMap", Resources.Load<Texture2D>("CandyFineSugar"));
            float feet = coat.owner.transform.position.y;
            coat.footOffset = low - feet - .05f;
            coat.waistOffset = low - feet + (high - low) * Random.Range(.44f, .52f);
            coat.material.SetFloat("_CoatBottom", feet + coat.footOffset);
            coat.material.SetFloat("_CoatTop", feet + coat.waistOffset);
            coat.material.SetFloat("_Coverage", 1);
            foreach (var r in bodies)
                coat.surfaces.Add(new CandySugarSurface(r, coat.material, feet + coat.footOffset, feet + coat.waistOffset));
            coat.coatedAt = Time.time;
        }

        ParticleSystem SugarParticles()
        {
            var ps = Particles("Falling sugar crystals", null, 600, .65f, false);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Lit"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", .48f);
            owned.Add(material);
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            var points = new[] { Vector3.up * .6f, Vector3.down * .6f, Vector3.left * .4f,
                Vector3.forward * .4f, Vector3.right * .4f, Vector3.back * .4f };
            var faces = new[] { 0,3,2, 0,4,3, 0,5,4, 0,2,5, 1,2,3, 1,3,4, 1,4,5, 1,5,2 };
            var vertices = new Vector3[faces.Length];
            var indices = new int[faces.Length];
            for (int i = 0; i < faces.Length; i++) { vertices[i] = points[faces[i]]; indices[i] = i; }
            var mesh = new Mesh { name = "Sugar crystal facets", vertices = vertices, triangles = indices };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            owned.Add(mesh); renderer.mesh = mesh;
            var main = ps.main; main.startRotation3D = true;
            var rotation = ps.rotationOverLifetime; rotation.enabled = true; rotation.separateAxes = true;
            rotation.x = new ParticleSystem.MinMaxCurve(-4, 4);
            rotation.y = new ParticleSystem.MinMaxCurve(-3, 3);
            rotation.z = new ParticleSystem.MinMaxCurve(-5, 5);
            return ps;
        }

        // ---------- helpers ----------
        ParticleSystem Particles(string name, Texture2D texture, int max, float gravity, bool flat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var sizeLife = ps.sizeOverLifetime; sizeLife.enabled = true;
            sizeLife.size = new ParticleSystem.MinMaxCurve(1, flat ? AnimationCurve.EaseInOut(0, .35f, 1, 1.25f) : AnimationCurve.Linear(0, 1, 1, .55f));
            var colorLife = ps.colorOverLifetime; colorLife.enabled = true;
            var fadeOut = new Gradient();
            fadeOut.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.8f, .55f), new GradientAlphaKey(0, 1) });
            colorLife.color = fadeOut;
            if (flat) { main.startRotation3D = true; }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Transparent(name, texture);
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.alignment = flat ? ParticleSystemRenderSpace.World : ParticleSystemRenderSpace.View;
            ps.Play();
            return ps;
        }

        Material Transparent(string name, Texture2D texture)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader) { name = name };
            if (texture) { m.SetTexture("_BaseMap", texture); m.mainTexture = texture; }
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0);
            m.renderQueue = (int)RenderQueue.Transparent;
            owned.Add(m);
            return m;
        }

        Texture2D SprayTexture()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Tapered water spray",
                wrapMode = TextureWrapMode.Clamp
            };
            var colors = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float v = (y + .5f) / size;
                float width = .23f + .65f * Mathf.Sin(v * Mathf.PI);
                float u = Mathf.Abs((x + .5f) / size * 2 - 1) / width;
                float alpha = (1 - Mathf.SmoothStep(.12f, .98f, Mathf.Clamp01(u))) *
                    Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .22f, v)) *
                    (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.72f, 1, v)));
                colors[y * size + x] = (Color32)new Color(.95f, .99f, 1, alpha);
            }
            texture.SetPixels32(colors);
            texture.Apply(false, true);
            owned.Add(texture);
            return texture;
        }

        Texture2D MistTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "Whitewater mist and microdroplets",
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var uv = new Vector2(x + .5f, y + .5f) * (2f / size) - Vector2.one;
                float falloff = Mathf.Clamp01(1 - uv.sqrMagnitude);
                float cloud = Mathf.PerlinNoise(x * .16f + 21, y * .18f + 7);
                float flecks = Mathf.PerlinNoise(x * .67f + 4, y * .74f + 16);
                float opacity = falloff * falloff * Mathf.Clamp01(cloud * .42f + flecks * flecks * .86f);
                pixels[y * size + x] = (Color32)new Color(.96f, .99f, 1, opacity);
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            owned.Add(texture);
            return texture;
        }

        Texture2D SoftDot(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Soft dot", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * size * .5f) / (size * .5f);
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1 - d * d)));
                }
            t.Apply(); owned.Add(t);
            return t;
        }

        Texture2D RingTexture(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Ripple ring", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * size * .5f) / (size * .5f);
                    float ring = Mathf.Clamp01(1 - Mathf.Abs(d - .82f) / .12f);
                    t.SetPixel(x, y, new Color(1, 1, 1, ring * ring));
                }
            t.Apply(); owned.Add(t);
            return t;
        }

    }
}
