using System.Collections.Generic;
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
    // Every client runs it locally, so hosts and guests see the same effects.
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
        ParticleSystem droplets, foam, bubbles, ripples, dust;
        readonly List<Object> owned = new();

        void Awake()
        {
            var soft = SoftDot(32);
            var ring = RingTexture(64);
            droplets = Particles("Water splash droplets", soft, 600, .9f, false);
            foam = Particles("Water splash foam", soft, 400, 0, true);
            bubbles = Particles("Underwater bubbles", soft, 300, -.25f, false);
            ripples = Particles("Sand ripples", ring, 120, 0, true);
            dust = Particles("Sand dust", soft, 300, .08f, false);
        }

        void OnDestroy()
        {
            RestoreFog();
            foreach (var coat in coats.Values) coat.Remove();
            foreach (var crater in craters.Values) if (crater.disc) Destroy(crater.disc);
            foreach (var o in owned) if (o) Destroy(o);
        }

        void Update()
        {
            var world = Services.World;
            if (world != null) ScanBlocks(world);
            UpdateBubbles(world);
            UpdateCraters();
            UpdateUnderwater(world);
            UpdateCoatings();
        }

        // ---------- blocks: water entry and sand sinking ----------
        struct Seen { public float bottom; public Vector3 center; public float time; }
        readonly Dictionary<Rigidbody, Seen> seen = new();
        IWorldQuery lastWorld;
        int cursor;

        void ScanBlocks(IWorldQuery world)
        {
            if (!ReferenceEquals(world, lastWorld)) { seen.Clear(); lastWorld = world; }
            var blocks = world.Blocks;
            if (blocks == null || blocks.Count == 0) return;
            int count = Mathf.Min(BlocksPerFrame, blocks.Count);
            for (int n = 0; n < count; n++)
            {
                var body = blocks[cursor++ % blocks.Count];
                if (!body || !body.gameObject.activeInHierarchy) continue;
                var shape = body.GetComponent<Collider>();
                if (!shape) continue;
                var bounds = shape.bounds;
                if (seen.TryGetValue(body, out var before))
                {
                    float dt = Mathf.Max(.001f, Time.time - before.time);
                    var velocity = (bounds.center - before.center) / dt;
                    float water = world.WaterHeight(bounds.center);
                    if (before.bottom >= water && bounds.min.y < water && velocity.y < -SplashSpeed && OpenWater(bounds.center, water))
                        Splash(new Vector3(bounds.center.x, water, bounds.center.z), bounds.size, -velocity.y, body);
                    else if (velocity.y < -.04f && velocity.y > -1.5f)
                        CheckSand(body, bounds, water);
                }
                seen[body] = new Seen { bottom = bounds.min.y, center = bounds.center, time = Time.time };
            }
            if (cursor > 100000000) cursor = 0;
        }

        void Splash(Vector3 at, Vector3 size, float speed, Rigidbody block)
        {
            float footprint = Mathf.Max(.4f, new Vector2(size.x, size.z).magnitude * .5f);
            float strength = Mathf.Clamp01(speed / 12f) * Mathf.Clamp(footprint / .7f, .6f, 2.2f);
            var emit = new ParticleSystem.EmitParams();
            int sprays = Mathf.RoundToInt(16 + 44 * strength);
            for (int i = 0; i < sprays; i++)
            {
                var out2 = Random.insideUnitCircle.normalized * Random.Range(.3f, 1.2f);
                emit.position = at + new Vector3(out2.x, 0, out2.y) * footprint * .6f;
                emit.velocity = new Vector3(out2.x * (1.2f + strength * 2), Random.Range(2.5f, 4.5f) + strength * 4.5f, out2.y * (1.2f + strength * 2));
                emit.startSize = Random.Range(.06f, .16f) * (1 + strength);
                emit.startLifetime = Random.Range(.7f, 1.2f);
                emit.startColor = new Color(.92f, .98f, 1f, .9f);
                droplets.Emit(emit, 1);
            }
            // A crown of white foam rolling outward on the surface.
            int rings = Mathf.RoundToInt(18 + 18 * strength);
            for (int i = 0; i < rings; i++)
            {
                float a = i * Mathf.PI * 2 / rings;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                emit.position = at + dir * footprint + Vector3.up * .03f;
                emit.velocity = dir * (.6f + strength * 1.4f);
                emit.startSize = Random.Range(.35f, .6f) * footprint;
                emit.startLifetime = Random.Range(1.2f, 1.9f);
                emit.startColor = new Color(1, 1, 1, .75f);
                emit.rotation3D = new Vector3(90, Random.Range(0, 360), 0);
                foam.Emit(emit, 1);
            }
            emit.ResetRotation();
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
            public Rigidbody block; public Vector3 point; public float footprint, started, ended = -1, nextRipple;
            public GameObject disc; public Material material;
        }
        readonly Dictionary<Rigidbody, Crater> craters = new();
        readonly List<Rigidbody> finished = new();
        Mesh craterMesh;

        // Ground layers only: no blocks, survivors or their climb surfaces.
        static int GroundMask => ~((1 << Layers.Block) | (1 << Layers.Survivor) | (1 << Layers.Surface));

        // Real open water has no ground just under the surface (maps can sit their floor a little below sea level).
        static bool OpenWater(Vector3 at, float water) =>
            !Physics.Raycast(new Vector3(at.x, water + .05f, at.z), Vector3.down, out var hit, .55f, GroundMask, QueryTriggerInteraction.Ignore) || hit.rigidbody;

        void CheckSand(Rigidbody block, Bounds bounds, float water)
        {
            if (craters.ContainsKey(block)) return;
            var from = new Vector3(bounds.center.x, bounds.max.y + .6f, bounds.center.z);
            if (!Physics.Raycast(from, Vector3.down, out var hit, bounds.size.y + 2.5f, GroundMask, QueryTriggerInteraction.Ignore)) return;
            if (hit.rigidbody || hit.normal.y < .6f || hit.point.y < bounds.min.y + .04f || hit.point.y < water - .3f) return;
            var crater = new Crater
            {
                // Some maps sit their floor just under sea level; draw on whichever surface is visible.
                block = block, point = new Vector3(hit.point.x, Mathf.Max(hit.point.y, water), hit.point.z), started = Time.time,
                footprint = Mathf.Clamp(new Vector2(bounds.size.x, bounds.size.z).magnitude * .5f, .35f, 1.4f)
            };
            crater.disc = new GameObject("Sand cave-in");
            crater.disc.transform.SetParent(transform, true);
            crater.disc.transform.SetPositionAndRotation(crater.point + Vector3.up * .015f, Quaternion.Euler(0, Random.Range(0, 360f), 0));
            crater.disc.AddComponent<MeshFilter>().sharedMesh = craterMesh ? craterMesh : craterMesh = CraterMesh();
            var renderer = crater.disc.AddComponent<MeshRenderer>();
            crater.material = Transparent("Sand cave-in", null);
            renderer.sharedMaterial = crater.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            craters[block] = crater;
        }

        void UpdateCraters()
        {
            finished.Clear();
            var emit = new ParticleSystem.EmitParams();
            foreach (var pair in craters)
            {
                var c = pair.Value;
                bool sinking = c.block && c.block.gameObject.activeInHierarchy && c.block.isKinematic;
                if (!sinking && c.ended < 0) c.ended = Time.time;
                float grow = Mathf.Clamp01((Time.time - c.started) / 2.5f);
                float fade = c.ended < 0 ? 1 : 1 - Mathf.Clamp01((Time.time - c.ended) / 3.5f);
                if (fade <= 0) { finished.Add(pair.Key); continue; }
                c.disc.transform.localScale = Vector3.one * c.footprint * Mathf.Lerp(.9f, 1.9f, grow);
                c.material.SetColor("_BaseColor", new Color(1, 1, 1, fade));
                if (c.ended < 0 && Time.time >= c.nextRipple)
                {
                    // Expanding rings in the sand plus a few grains puffing up at the rim.
                    c.nextRipple = Time.time + Random.Range(.45f, .7f);
                    emit.position = c.point + Vector3.up * .02f;
                    emit.velocity = Vector3.zero;
                    emit.rotation3D = new Vector3(90, Random.Range(0, 360f), 0);
                    emit.startSize = c.footprint * 2.2f;
                    emit.startLifetime = 1.6f;
                    emit.startColor = new Color(SandDark.r, SandDark.g, SandDark.b, .55f);
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
            }
            foreach (var key in finished)
            {
                var c = craters[key];
                if (c.disc) Destroy(c.disc);
                if (c.material) Destroy(c.material);
                craters.Remove(key);
            }
        }

        // ---------- underwater filter ----------
        Volume volume;
        float underwater;
        bool fogOwned, savedFog;
        Color savedFogColor; FogMode savedFogMode; float savedFogDensity;

        void UpdateUnderwater(IWorldQuery world)
        {
            var eye = Services.Session?.ActiveCamera;
            if (!eye) eye = Camera.main;
            bool under = eye && world != null && eye.transform.position.y < world.WaterHeight(eye.transform.position) - .02f;
            underwater = Mathf.MoveTowards(underwater, under ? 1 : 0, Time.unscaledDeltaTime * 5);
            if (underwater > 0 && !volume) volume = UnderwaterVolume();
            if (volume) volume.weight = underwater;
            // Thick teal fog hides the underside of the map, so nothing looks broken down there.
            if (under && !fogOwned)
            {
                savedFog = RenderSettings.fog; savedFogColor = RenderSettings.fogColor;
                savedFogMode = RenderSettings.fogMode; savedFogDensity = RenderSettings.fogDensity;
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = new Color(.05f, .36f, .42f); RenderSettings.fogDensity = .11f;
                fogOwned = true;
            }
            else if (!under && fogOwned) RestoreFog();
        }

        void RestoreFog()
        {
            if (!fogOwned) return;
            RenderSettings.fog = savedFog; RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogMode = savedFogMode; RenderSettings.fogDensity = savedFogDensity;
            fogOwned = false;
        }

        Volume UnderwaterVolume()
        {
            var go = new GameObject("Underwater filter");
            go.transform.SetParent(transform, false);
            var v = go.AddComponent<Volume>();
            v.isGlobal = true; v.priority = 60; v.weight = 0;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            owned.Add(profile);
            v.profile = profile;
            var color = profile.Add<ColorAdjustments>();
            color.colorFilter.Override(new Color(.55f, .92f, .95f));
            color.saturation.Override(-18); color.contrast.Override(-12); color.postExposure.Override(-.25f);
            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(.38f); vignette.smoothness.Override(.8f); vignette.color.Override(new Color(.02f, .16f, .2f));
            return v;
        }

        // ---------- cinnamon-sugar sand coating ----------
        sealed class Coat
        {
            public Component owner; public ISandState sand;
            public float sandSeconds, coatedAt = -1, dustNext;
            public Renderer[] renderers; public Material material;
            public float footOffset, waistOffset;

            public void Remove()
            {
                if (renderers != null)
                    foreach (var r in renderers)
                    {
                        if (!r) continue;
                        var list = new List<Material>(r.sharedMaterials);
                        if (list.Remove(material)) r.sharedMaterials = list.ToArray();
                    }
                renderers = null;
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
                    coat.dustNext = Time.time + .08f;
                    var emit = new ParticleSystem.EmitParams
                    {
                        position = coat.owner.transform.position + Vector3.up * Random.Range(coat.footOffset, coat.waistOffset) + Random.insideUnitSphere * .18f,
                        velocity = new Vector3(Random.Range(-.2f, .2f), Random.Range(-.6f, -.1f), Random.Range(-.2f, .2f)),
                        startSize = Random.Range(.04f, .09f), startLifetime = Random.Range(.5f, .9f),
                        startColor = new Color(.82f, .62f, .40f, .85f)
                    };
                    dust.Emit(emit, 2);
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
                if (!(r is SkinnedMeshRenderer || r is MeshRenderer) || !r.enabled) continue;
                bodies.Add(r);
                low = Mathf.Min(low, r.bounds.min.y); high = Mathf.Max(high, r.bounds.max.y);
            }
            if (bodies.Count == 0) return;
            coat.material = new Material(coatShader) { name = "Cinnamon sugar coating" };
            coat.material.SetFloat("_Seed", Random.value * 100);
            float feet = coat.owner.transform.position.y;
            coat.footOffset = low - feet - .05f;
            coat.waistOffset = low - feet + (high - low) * Random.Range(.44f, .52f);
            foreach (var r in bodies)
            {
                var list = new List<Material>(r.sharedMaterials) { coat.material };
                r.sharedMaterials = list.ToArray();
            }
            coat.renderers = bodies.ToArray();
            coat.coatedAt = Time.time;
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

        // A flat disc darker toward its sunken middle, with a soft lighter rim and transparent edge.
        Mesh CraterMesh()
        {
            const int sides = 32;
            var vertices = new List<Vector3> { Vector3.down * .02f };
            var colors = new List<Color> { new Color(SandDark.r * .8f, SandDark.g * .8f, SandDark.b * .8f, .95f) };
            var triangles = new List<int>();
            float[] radii = { .55f, .85f, 1f };
            Color[] ring = { new(SandDark.r, SandDark.g, SandDark.b, .85f), new(SandLight.r, SandLight.g, SandLight.b, .6f), new(SandLight.r, SandLight.g, SandLight.b, 0) };
            for (int r = 0; r < radii.Length; r++)
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides, wobble = 1 + Mathf.Sin(a * 5 + r) * .06f;
                    vertices.Add(new Vector3(Mathf.Cos(a) * radii[r] * wobble, r == 1 ? .015f : 0, Mathf.Sin(a) * radii[r] * wobble));
                    colors.Add(ring[r]);
                }
            for (int i = 0; i < sides; i++) { triangles.Add(0); triangles.Add(1 + (i + 1) % sides); triangles.Add(1 + i); }
            for (int r = 0; r < radii.Length - 1; r++)
                for (int i = 0; i < sides; i++)
                {
                    int a = 1 + r * sides + i, b = 1 + r * sides + (i + 1) % sides, c = a + sides, d = b + sides;
                    triangles.AddRange(new[] { a, b, d, a, d, c });
                }
            var mesh = new Mesh { name = "Sand cave-in" };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            owned.Add(mesh);
            return mesh;
        }
    }
}
