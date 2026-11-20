using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    // Makes the Deck Ship look like it is sailing forward while it actually stays put (cheap, and the cargo
    // physics stays simple): the sea's waves and ripples flow toward the stern, islands drift past and loop
    // around, and the hull gets a wake, propeller churn, foam where it meets the water and spray at the bow.
    // Art only: no colliders, nothing gameplay reads. Sits on the Deck Ship map prefab's root.
    // The hull is found through Core: the map's first block (IWorldQuery.Blocks[0]) is its kinematic hull.
    public sealed class DeckShipSeaArt : MonoBehaviour
    {
        static readonly int ScrollId = Shader.PropertyToID("_CandyOceanScroll");

        [Tooltip("How fast the ship seems to sail (m/s). Keep equal to the map's cruise speed.")]
        public float cruiseSpeed = 7;
        public bool wake = true, propellers = true, hullFoam = true, bowSpray = true, islands = true;
        [Range(0, 8)] public int islandCount = 4;
        [Tooltip("Particle amount: lower for Quest if needed.")]
        [Range(.2f, 2)] public float foamAmount = 1;

        Rigidbody hull;
        Bounds hullBounds;          // hull art in hull space
        float travel, water;
        ParticleSystem foam, spray;
        readonly List<Transform> props = new List<Transform>();
        readonly List<Transform> isles = new List<Transform>();
        readonly List<Object> owned = new List<Object>();
        readonly Dictionary<string, float> emitDebt = new Dictionary<string, float>();
        Material foamMaterial, sprayMaterial;
        float lastBowY;
        readonly RaycastHit[] hits = new RaycastHit[16];
        System.Random random = new System.Random(5);

        void OnDisable()
        {
            Shader.SetGlobalVector(ScrollId, Vector4.zero);
            foreach (var p in props) if (p) Destroy(p.gameObject);
            props.Clear();
            hull = null;
        }

        void OnDestroy() { foreach (var o in owned) if (o) Destroy(o); }

        void LateUpdate()
        {
            var world = Services.World;
            if (!(world is ISyncedWorld synced) || synced.Root != transform || world.Blocks == null || world.Blocks.Count == 0) { Shader.SetGlobalVector(ScrollId, Vector4.zero); return; }
            var body = world.Blocks[0];
            if (!body || !body.isKinematic) return;
            if (body != hull) Attach(body);
            water = world.WaterHeight(transform.position);
            float dt = Time.deltaTime;
            if (dt <= 0) return;

            // The sea flows past: sample the wave patterns further along the course every frame.
            travel += cruiseSpeed * dt;
            if (travel > 100000) travel -= 100000;
            var forward = Flat(transform.forward);
            Shader.SetGlobalVector(ScrollId, new Vector4(forward.x * travel, forward.z * travel, 0, 0));

            if (islands) MoveIslands(dt, forward);
            if (propellers) foreach (var p in props) if (p) p.Rotate(0, 0, cruiseSpeed * 70 * dt, Space.Self);
            Emit(dt, forward);
        }

        void Attach(Rigidbody body)
        {
            hull = body;
            hullBounds = new Bounds();
            bool first = true;
            foreach (var r in hull.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = hull.transform.InverseTransformPoint(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                    if (first) { hullBounds = new Bounds(c, Vector3.zero); first = false; } else hullBounds.Encapsulate(c);
                }
            }
            if (!foamMaterial) MakeEffects();
            if (propellers) BuildPropellers();
            if (islands && isles.Count == 0) for (int i = 0; i < islandCount; i++) isles.Add(BuildIsland(i));
        }

        // ---------- Wake, churn, hull foam, bow spray ----------

        void Emit(float dt, Vector3 forward)
        {
            var right = Vector3.Cross(Vector3.up, forward);
            var sea = -forward * cruiseSpeed;
            float stern = hullBounds.min.z, beam = Mathf.Max(1, hullBounds.extents.x);
            float propX = beam * .35f;

            if (propellers && wake)
                for (int side = -1; side <= 1; side += 2)
                    Repeat("churn" + side, 34 * foamAmount, dt, () =>
                    {
                        var p = OnWater(new Vector3(side * propX, 0, stern - .6f)) + Jitter(.8f);
                        EmitFoam(p, sea - forward * 2.5f + right * Range(-.7f, .7f), Range(1.4f, 2.6f), Range(3.5f, 5.5f));
                    });

            if (wake)
                for (int side = -1; side <= 1; side += 2)
                    Repeat("arm" + side, 22 * foamAmount, dt, () =>
                    {
                        // The two arms of the V: foam pushed sideways at a third of the ship's speed (about 19 degrees).
                        var p = OnWater(new Vector3(side * beam * .85f, 0, stern + Range(0, 3))) + Jitter(.5f);
                        EmitFoam(p, sea + right * side * cruiseSpeed * Range(.28f, .38f), Range(1.2f, 2), Range(6, 8));
                    });

            if (hullFoam)
                for (int side = -1; side <= 1; side += 2)
                    Repeat("side" + side, 70 * foamAmount, dt, () =>
                    {
                        if (!HullSide(side, Range(hullBounds.min.z + 1, hullBounds.max.z - 1), out var p, out var outward)) return;
                        EmitFoam(p + outward * .15f, sea * .9f + outward * Range(.5f, 1.1f), Range(.6f, 1.2f), Range(1.6f, 2.6f));
                    });

            if (bowSpray && Bow(out var bow, out float bowY))
            {
                // Spray grows when the bow drops into the water.
                float plunge = Mathf.Clamp01((lastBowY - bowY) / dt * .6f);
                lastBowY = bowY;
                Repeat("bowfoam", 26 * foamAmount, dt, () =>
                {
                    int side = random.Next(2) * 2 - 1;
                    EmitFoam(bow + right * side * Range(.2f, 1), sea * .6f + right * side * Range(1.5f, 3), Range(.8f, 1.6f), Range(1.5f, 2.5f));
                });
                Repeat("spray", (18 + plunge * 120) * foamAmount, dt, () =>
                {
                    int side = random.Next(2) * 2 - 1;
                    var v = Vector3.up * Range(1.5f, 3.5f + plunge * 4) + right * side * Range(1, 3.5f) + sea * .25f;
                    spray.Emit(new ParticleSystem.EmitParams { position = bow + Jitter(.4f) + Vector3.up * .2f, velocity = v, startSize = Range(.25f, .7f), startLifetime = Range(.9f, 1.5f), startColor = new Color(1, 1, 1, .85f) }, 1);
                });
            }
        }

        void EmitFoam(Vector3 position, Vector3 velocity, float size, float life)
        {
            foam.Emit(new ParticleSystem.EmitParams
            {
                position = position, velocity = velocity, startSize = size, startLifetime = life,
                rotation = Range(0, 360), startColor = new Color(1, 1, 1, Range(.55f, .9f))
            }, 1);
        }

        // A point on the water surface under a hull-space point.
        Vector3 OnWater(Vector3 local)
        {
            var p = hull.transform.TransformPoint(local);
            p.y = water + .06f;
            return p;
        }

        // Where the hull side meets the water at this hull-space z (works for any hull shape).
        bool HullSide(int side, float z, out Vector3 point, out Vector3 outward)
        {
            var t = hull.transform;
            var from = t.TransformPoint(new Vector3(side * (hullBounds.extents.x + 4), 0, z)); from.y = water + .1f;
            var dir = -Flat(t.right) * side;
            outward = -dir; point = default;
            int n = Physics.RaycastNonAlloc(from, dir, hits, hullBounds.extents.x + 4, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (hits[i].rigidbody == hull) { point = hits[i].point; point.y = water + .06f; return true; }
            return false;
        }

        bool Bow(out Vector3 point, out float bowY)
        {
            var t = hull.transform;
            var from = t.TransformPoint(new Vector3(0, 0, hullBounds.max.z + 4)); from.y = water + .15f;
            point = default; bowY = t.TransformPoint(new Vector3(0, 0, hullBounds.max.z)).y;
            int n = Physics.RaycastNonAlloc(from, -Flat(t.forward), hits, 8, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (hits[i].rigidbody == hull) { point = hits[i].point; point.y = water + .08f; return true; }
            return false;
        }

        void Repeat(string key, float perSecond, float dt, System.Action emit)
        {
            emitDebt.TryGetValue(key, out float debt);
            debt += perSecond * dt;
            int n = Mathf.Min((int)debt, 12);
            for (int i = 0; i < n; i++) emit();
            emitDebt[key] = debt - (int)debt;
        }

        void MakeEffects()
        {
            var blob = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "Deck Ship foam", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dx = (x - 31.5f) / 31.5f, dy = (y - 31.5f) / 31.5f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float lumps = Mathf.PerlinNoise(x * .16f, y * .16f) * .55f + Mathf.PerlinNoise(x * .45f + 7, y * .45f + 3) * .25f;
                    float a = Mathf.Clamp01((1 - d) * 1.7f) * Mathf.Clamp01(.35f + lumps);
                    blob.SetPixel(x, y, new Color(1, 1, 1, a * a));
                }
            blob.Apply();
            owned.Add(blob);
            foamMaterial = ParticleMaterial("Deck Ship foam", blob);
            sprayMaterial = ParticleMaterial("Deck Ship spray", blob);
            foam = MakeSystem("Sea foam (art)", foamMaterial, 2400, ParticleSystemRenderMode.HorizontalBillboard, 0, true);
            spray = MakeSystem("Bow spray (art)", sprayMaterial, 600, ParticleSystemRenderMode.Billboard, 1, false);
        }

        Material ParticleMaterial(string name, Texture texture)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader) { name = name };
            m.SetTexture("_BaseMap", texture); m.mainTexture = texture;
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent + 5;
            owned.Add(m);
            return m;
        }

        ParticleSystem MakeSystem(string name, Material material, int max, ParticleSystemRenderMode mode, float gravity, bool grow)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max; main.playOnAwake = false; main.loop = true;
            main.gravityModifier = gravity; main.startSpeed = 0;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, grow ? AnimationCurve.EaseInOut(0, .6f, 1, 2.6f) : AnimationCurve.Linear(0, 1, 1, .4f));
            var color = ps.colorOverLifetime; color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(.92f, .97f, 1), 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .08f), new GradientAlphaKey(.55f, .55f), new GradientAlphaKey(0, 1) });
            color.color = fade;
            var drag = ps.limitVelocityOverLifetime; drag.enabled = grow; drag.drag = .25f; drag.multiplyDragByParticleSize = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = mode; r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.alignment = mode == ParticleSystemRenderMode.HorizontalBillboard ? ParticleSystemRenderSpace.World : ParticleSystemRenderSpace.View;
            r.sortMode = ParticleSystemSortMode.None;
            ps.Play();
            return ps;
        }

        // ---------- Propellers ----------

        void BuildPropellers()
        {
            foreach (var p in props) if (p) Destroy(p.gameObject);
            props.Clear();
            var brass = Lit("Deck Ship brass", new Color(1, .74f, .25f), .85f);
            var dark = Lit("Deck Ship rudder", new Color(.75f, .12f, .18f), .5f);
            var t = hull.transform;
            float waterLocal = t.InverseTransformPoint(new Vector3(0, water, 0)).y;
            float y = Mathf.Max(hullBounds.min.y + 1.1f, waterLocal - 1.6f);
            float z = hullBounds.min.z - .5f, x = Mathf.Max(1, hullBounds.extents.x) * .35f;
            for (int side = -1; side <= 1; side += 2)
            {
                var prop = new GameObject("Propeller (art)").transform;
                prop.SetParent(t, false); prop.localPosition = new Vector3(side * x, y, z);
                Part(prop, PrimitiveType.Cylinder, Vector3.zero, new Vector3(.55f, .35f, .55f), Quaternion.Euler(90, 0, 0), brass);
                for (int b = 0; b < 3; b++)
                    Part(prop, PrimitiveType.Cube, Quaternion.Euler(0, 0, b * 120) * new Vector3(0, .75f, 0), new Vector3(.55f, 1.4f, .08f), Quaternion.Euler(0, 25, b * 120), brass);
                Part(t, PrimitiveType.Cylinder, new Vector3(side * x, y, z + 1.3f), new Vector3(.18f, 1.2f, .18f), Quaternion.Euler(90, 0, 0), brass).name = "Propeller shaft (art)";
                props.Add(prop);
            }
            Part(t, PrimitiveType.Cube, new Vector3(0, y + .2f, z - .4f), new Vector3(.18f, 2.6f, 1.8f), Quaternion.identity, dark).name = "Rudder (art)";
        }

        // ---------- Islands drifting past ----------

        Transform BuildIsland(int index)
        {
            var root = new GameObject("Passing island (art)").transform;
            root.SetParent(transform, false);
            var sand = Lit("Deck Ship island sand", new Color(.98f, .86f, .6f), .15f);
            var grass = Lit("Deck Ship island grass", new Color(.36f, .82f, .35f), .2f);
            var trunk = Lit("Deck Ship palm trunk", new Color(.62f, .42f, .25f), .1f);
            var leaf = Lit("Deck Ship palm leaf", new Color(.2f, .7f, .3f), .3f);
            var rock = Lit("Deck Ship island rock", new Color(.58f, .6f, .66f), .25f);
            float r = Range(10, 26);
            Part(root, PrimitiveType.Sphere, new Vector3(0, -r * .06f, 0), new Vector3(r * 2, r * .32f, r * 1.6f), Quaternion.identity, sand);
            Part(root, PrimitiveType.Sphere, new Vector3(r * .1f, r * .06f, 0), new Vector3(r * 1.3f, r * .26f, r * 1.05f), Quaternion.identity, grass);
            int palms = 2 + random.Next(4);
            for (int i = 0; i < palms; i++)
            {
                var at = new Vector3(Range(-r * .5f, r * .5f), r * .12f, Range(-r * .4f, r * .4f));
                float h = Range(5, 9), lean = Range(-14, 14);
                var palm = new GameObject("Palm").transform; palm.SetParent(root, false); palm.localPosition = at; palm.localRotation = Quaternion.Euler(lean, Range(0, 360), 0);
                Part(palm, PrimitiveType.Cylinder, new Vector3(0, h * .5f, 0), new Vector3(.55f, h * .5f, .55f), Quaternion.identity, trunk);
                for (int l = 0; l < 6; l++)
                    Part(palm, PrimitiveType.Cube, new Vector3(0, h, 0) + Quaternion.Euler(0, l * 60, 0) * new Vector3(0, -.3f, 1.6f), new Vector3(1.1f, .12f, 3.4f), Quaternion.Euler(18, l * 60, 0), leaf);
            }
            for (int i = 0; i < 3; i++)
                Part(root, PrimitiveType.Sphere, new Vector3(Range(-r, r) * .8f, 0, Range(-r, r) * .6f), Vector3.one * Range(2, 5), Quaternion.Euler(Range(0, 40), Range(0, 360), 0), rock);
            Place(root, index * 260 - 400 - (index > 1 ? hullBounds.extents.z : 0));
            return root;
        }

        void Place(Transform island, float z)
        {
            int side = random.Next(2) * 2 - 1;
            // Outside the hull, whatever its size.
            float clear = Mathf.Max(20, hullBounds.extents.x);
            island.localPosition = new Vector3(side * (clear + Range(55, 170)), water - transform.position.y, z);
            island.localRotation = Quaternion.Euler(0, Range(0, 360), 0);
            float s = Range(.7f, 1.3f); island.localScale = new Vector3(s, s * Range(.8f, 1.2f), s);
        }

        void MoveIslands(float dt, Vector3 forward)
        {
            foreach (var island in isles)
            {
                if (!island) continue;
                var p = island.localPosition;
                p.z -= cruiseSpeed * dt;
                island.localPosition = p;
                float reach = Mathf.Max(600, hullBounds.extents.z * 3);
                if (p.z < -reach) Place(island, reach + Range(0, 200));
            }
        }

        // ---------- Helpers ----------

        Transform Part(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localRotation = rotation; go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        readonly Dictionary<string, Material> lit = new Dictionary<string, Material>();
        Material Lit(string name, Color color, float smoothness)
        {
            if (lit.TryGetValue(name, out var m) && m) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { name = name, enableInstancing = true };
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            lit[name] = m; owned.Add(m);
            return m;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }
        float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
        Vector3 Jitter(float r) => new Vector3(Range(-r, r), 0, Range(-r, r));
    }
}
