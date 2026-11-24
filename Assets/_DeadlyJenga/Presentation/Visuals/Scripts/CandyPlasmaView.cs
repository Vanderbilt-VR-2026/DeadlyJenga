using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    [DefaultExecutionOrder(300)]
    public sealed class CandyPlasmaView : MonoBehaviour
    {
        const float ImpactLife = .4f;
        const int VisibleLimit = 24;
        static readonly Color Ice = new(.69f, 1, 1);
        static readonly Color[] Strands = { new(.145f, .847f, .976f), new(.176f, .467f, .933f), new(.49f, .42f, .933f) };
        readonly Dictionary<int, StrokeMesh> shots = new(), impacts = new();
        readonly Stack<StrokeMesh> pool = new();
        readonly HashSet<int> visible = new();
        readonly List<int> stale = new();
        readonly List<(StrokeMesh mesh, float born, float scale)> muzzle = new();
        BlasterFrame frame;
        float received;
        Material ribbon;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen() => GameEvents.SessionStarted += host =>
        {
            if (!host.GetComponent<CandyPlasmaView>()) host.AddComponent<CandyPlasmaView>();
        };

        void Awake()
        {
            var shader = Resources.Load<Shader>("CandyOrdnance");
            if (!shader) { enabled = false; return; }
            ribbon = new Material(shader) { name = "Braided plasma energy" };
            ribbon.SetFloat("_Ribbon", 1);
        }

        void Update()
        {
            var state = MatchRuntime.Current?.Frame.blaster;
            if (!ReferenceEquals(state, frame)) { frame = state; received = Time.time; }
            float elapsed = Mathf.Min(.25f, Time.time - received);
            visible.Clear();
            if (state?.enabled == true)
                foreach (var hit in state.impacts)
                {
                    if (visible.Count >= VisibleLimit) break;
                    if (hit.remaining <= elapsed || !CandyWeaponImpacts.VisibleToAnyCamera(hit.position, 55)) continue;
                    visible.Add(hit.id);
                    if (!impacts.TryGetValue(hit.id, out var effect))
                    {
                        var incoming = shots.TryGetValue(hit.id, out var shot) ? shot.Root.forward : Vector3.forward;
                        var normal = -incoming;
                        if (CandyWeaponImpacts.TryContact(hit.position, incoming, out var surface, .28f) && surface.anchor)
                            normal = (surface.anchor.localToWorldMatrix * surface.localFrame).rotation * Vector3.forward;
                        effect = Take("Visual - plasma impact radius");
                        effect.Root.SetPositionAndRotation(hit.position, Quaternion.LookRotation(normal));
                        impacts.Add(hit.id, effect);
                    }
                    DrawImpact(effect, hit.id, 1 - Mathf.Clamp01((hit.remaining - elapsed) / ImpactLife));
                }
            Release(impacts);
            visible.Clear();
            if (state?.enabled == true)
                foreach (var shot in state.shots)
                {
                    if (visible.Count >= VisibleLimit) break;
                    if (!CandyWeaponImpacts.VisibleToAnyCamera(shot.position, 55)) continue;
                    visible.Add(shot.id);
                    if (!shots.TryGetValue(shot.id, out var effect))
                    {
                        effect = Take("Visual - traveling plasma");
                        shots.Add(shot.id, effect);
                    }
                    Vector3 forward = shot.velocity.sqrMagnitude > .001f ? shot.velocity : Vector3.forward;
                    effect.Root.SetPositionAndRotation(shot.position + Vector3.ClampMagnitude(shot.velocity * elapsed, .5f),
                        Quaternion.LookRotation(forward));
                    DrawBolt(effect, Time.time + shot.id * .17f);
                }
            Release(shots);
            for (int i = muzzle.Count - 1; i >= 0; i--)
            {
                var spark = muzzle[i];
                float age = (Time.time - spark.born) / .10f;
                if (age >= 1)
                {
                    spark.mesh.Root.gameObject.SetActive(false);
                    pool.Push(spark.mesh);
                    muzzle.RemoveAt(i);
                    continue;
                }
                DrawMuzzle(spark.mesh, age, spark.scale);
            }
        }

        internal void PresentMuzzle(Vector3 origin, Quaternion direction, float scale)
        {
            if (!ribbon || muzzle.Count >= 12 || !CandyWeaponImpacts.VisibleToAnyCamera(origin, 55)) return;
            var flash = Take("Visual - plasma emitter flash");
            flash.Root.SetPositionAndRotation(origin, direction);
            muzzle.Add((flash, Time.time, scale));
            DrawMuzzle(flash, 0, scale);
        }

        static void DrawMuzzle(StrokeMesh mesh, float age, float scale)
        {
            mesh.Clear();
            float fade = 1 - age;
            mesh.Line(Vector3.zero, Vector3.forward * .29f * scale * fade, .16f * scale * fade, Fade(Ice, fade));
            for (int collar = 0; collar < 3; collar++)
            {
                float radius = (.060f + .029f * (age + collar)) * scale;
                float depth = (.04f + collar * .075f + age * .15f) * scale;
                var color = Fade(Strands[collar], fade);
                for (int segment = 0; segment < 12; segment++)
                {
                    float a = segment * .523599f;
                    float b = a + .523599f;
                    mesh.Line(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, depth),
                        new Vector3(Mathf.Cos(b) * radius, Mathf.Sin(b) * radius, depth),
                        .017f * scale * fade, color);
                }
            }
            mesh.Apply();
        }

        static void DrawBolt(StrokeMesh mesh, float time)
        {
            mesh.Clear();
            for (int band = 0; band < Strands.Length; band++)
            {
                Vector3 previous = Braid(0, band, time);
                for (int segment = 1; segment <= 22; segment++)
                {
                    float t = segment / 22f;
                    Vector3 next = Braid(t, band, time);
                    mesh.Line(previous, next, .028f * (1 - .88f * t), Fade(Strands[band], 1 - t));
                    previous = next;
                }
            }
            mesh.Line(Vector3.back * .31f, Vector3.forward * .27f, .18f, Fade(Strands[0], .66f));
            mesh.Line(Vector3.back * .15f, Vector3.forward * .24f, .071f, Ice);
            for (int ring = 0; ring < 3; ring++)
                for (int arc = 0; arc < 3; arc++)
                {
                    float phase = time * 10 + ring * .75f + arc * 2.0944f;
                    float radius = ring == 1 ? .19f : .149f;
                    float depth = (ring - 1) * .18f;
                    Vector3 Point(float angle) => new(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius,
                        depth + Mathf.Cos(angle) * .071f);
                    for (int step = 0; step < 9; step++)
                        mesh.Line(Point(phase + step * .176f), Point(phase + (step + 1) * .176f),
                            arc == 2 ? .025f : .016f, arc == 2 ? Ice : Strands[(ring + arc) % 3]);
                }
            mesh.Apply();
        }

        static Vector3 Braid(float t, int band, float time)
        {
            float phase = t * 9.4f - time * 19 + band * 2.0944f;
            float radius = (.069f + .028f * Mathf.Sin(Mathf.PI * t)) * Mathf.Pow(1 - t, .75f);
            return new Vector3(Mathf.Cos(phase) * radius, Mathf.Sin(phase) * radius, -.235f - 1.28f * t);
        }

        static void DrawImpact(StrokeMesh mesh, int seed, float age)
        {
            mesh.Clear();
            for (int ring = 0; ring < 3; ring++)
            {
                float t = Mathf.Clamp01((age - .085f * ring) / (1 - .085f * ring));
                if (t <= 0 || t >= 1) continue;
                float radius = 1.15f * Mathf.Pow(t, .58f);
                for (int arc = 0; arc < 10; arc++)
                {
                    float start = arc * .6283f + ring * .07f;
                    float span = (.56f - .27f * t) / 5;
                    for (int step = 0; step < 5; step++)
                    {
                        float a = start + step * span;
                        mesh.Line(Ring(a, radius, ring), Ring(a + span, radius, ring),
                            .027f * (1 - t) + .004f, Fade(Strands[ring], 1 - t));
                    }
                }
            }
            for (int i = 0; i < 25; i++)
            {
                float angle = (i + seed * .37f) * 2.39996f;
                float radius = (.51f + (i % 7) * .09f) * (1 - Mathf.Exp(-4 * age));
                var tail = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), i % 3 == 0 ? .72f : .16f) * radius;
                tail.y -= .18f * age * age;
                mesh.Line(tail * (.58f + age * .26f), tail, .024f * (1 - age) + .004f,
                    Fade(i % 3 == 0 ? Ice : Strands[i % 3], 1 - age));
            }
            float flash = Mathf.Exp(-9 * age);
            mesh.Line(Vector3.down * .24f * flash, Vector3.up * .24f * flash, .20f * flash, Fade(Ice, flash));
            mesh.Apply();
        }

        static Vector3 Ring(float angle, float radius, int ring)
        {
            float r = radius * (1 + .024f * Mathf.Sin(angle * 5 + ring));
            return new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, .025f);
        }

        static Color Fade(Color color, float opacity)
        {
            color.a = Mathf.Clamp01(opacity);
            return color;
        }

        StrokeMesh Take(string label)
        {
            var mesh = pool.Count > 0 ? pool.Pop() : new StrokeMesh(transform, ribbon);
            mesh.Root.name = label;
            mesh.Root.gameObject.SetActive(true);
            return mesh;
        }

        void Release(Dictionary<int, StrokeMesh> active)
        {
            stale.Clear();
            foreach (var pair in active)
            {
                if (visible.Contains(pair.Key)) continue;
                pair.Value.Root.gameObject.SetActive(false);
                pool.Push(pair.Value);
                stale.Add(pair.Key);
            }
            foreach (int id in stale) active.Remove(id);
        }

        void OnDestroy()
        {
            foreach (var item in shots.Values) Destroy(item.Geometry);
            foreach (var item in impacts.Values) Destroy(item.Geometry);
            foreach (var flash in muzzle) Destroy(flash.mesh.Geometry);
            foreach (var item in pool) Destroy(item.Geometry);
            if (ribbon) Destroy(ribbon);
        }

        sealed class StrokeMesh
        {
            public readonly Transform Root;
            public readonly Mesh Geometry;
            readonly List<Vector3> vertices = new(1536);
            readonly List<Color> colors = new(1536);
            readonly List<Vector2> uv = new(1536);
            readonly List<int> triangles = new(2304);

            public StrokeMesh(Transform parent, Material material)
            {
                var obj = new GameObject("Plasma energy", typeof(MeshFilter), typeof(MeshRenderer));
                Root = obj.transform;
                Root.SetParent(parent, false);
                Geometry = new Mesh { name = "Pooled plasma filaments" };
                Geometry.MarkDynamic();
                obj.GetComponent<MeshFilter>().sharedMesh = Geometry;
                var renderer = obj.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            public void Clear()
            {
                vertices.Clear();
                colors.Clear();
                uv.Clear();
                triangles.Clear();
            }

            public void Line(Vector3 from, Vector3 to, float width, Color tint)
            {
                Vector3 direction = to - from;
                if (direction.sqrMagnitude < .000001f || width < .001f) return;
                var axis = Vector3.Cross(direction, Vector3.up);
                if (axis.sqrMagnitude < .000001f) axis = Vector3.Cross(direction, Vector3.right);
                axis = axis.normalized * (width * .5f);
                Quad(from, to, axis, tint);
                Quad(from, to, Vector3.Cross(direction.normalized, axis), tint);
            }

            void Quad(Vector3 from, Vector3 to, Vector3 axis, Color tint)
            {
                int i = vertices.Count;
                vertices.Add(from - axis); vertices.Add(from + axis);
                vertices.Add(to - axis); vertices.Add(to + axis);
                uv.Add(Vector2.zero); uv.Add(Vector2.up); uv.Add(Vector2.right); uv.Add(Vector2.one);
                colors.Add(tint); colors.Add(tint); colors.Add(tint); colors.Add(tint);
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
                triangles.Add(i + 2); triangles.Add(i + 1); triangles.Add(i + 3);
            }

            public void Apply()
            {
                Geometry.Clear(false);
                Geometry.SetVertices(vertices);
                Geometry.SetColors(colors);
                Geometry.SetUVs(0, uv);
                Geometry.SetTriangles(triangles, 0);
            }
        }
    }
}
