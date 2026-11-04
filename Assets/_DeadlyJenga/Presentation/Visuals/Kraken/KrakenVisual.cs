using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using DeadlyJenga.Core;
using DeadlyJenga.CandyArt;

namespace DeadlyJenga.Presentation
{
    // Draws the Kraken from its DisasterFrame: a glossy gummy-candy octopus. Domed head with cartoon eyes that
    // watch the tower, tapered tentacles that rise out of the sea and curl at the tip, pink suckers on the
    // inner side, foam rings where the arms break the water. Built from real objects so it can be previewed.
    public sealed class KrakenVisual : MonoBehaviour
    {
        const int Rings = 30, Sides = 14, SuckersPerArm = 14;
        public static readonly Color Body = new Color(1f, .45f, .12f), Spot = new Color(1f, .72f, .32f), Belly = new Color(1f, .86f, .66f),
            EyeWhite = new Color(1f, .98f, .95f), Pupil = new Color(.05f, .03f, .1f), Foam = new Color(1f, 1f, 1f, .75f);

        sealed class Arm
        {
            public GameObject go; public Mesh mesh; public Vector3[] verts; public Vector3[] normals;
            public Transform[] suckers; public Transform foam; public Vector3 tip, basePoint; public bool init;
        }

        Material body, spot, belly, white, pupil, foam;
        Transform head, headFoam;
        Transform[] eyes = new Transform[2], pupils = new Transform[2], shines = new Transform[2], brows = new Transform[2];
        readonly List<Arm> arms = new List<Arm>();
        Mesh sphere, cylinder, capsule;
        Vector3 smoothedHead;
        bool built;

        public static Material Candy(Color color, bool transparent = false)
        {
            var env = FindFirstObjectByType<CandyEnvironment>();
            var template = !transparent && env && env.profile ? env.profile.red : null;
            var m = template ? new Material(template) : new Material(Shader.Find(transparent ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            m.name = "Kraken candy"; m.hideFlags = HideFlags.DontSave; m.enableInstancing = true;
            if (transparent)
            {
                m.SetFloat("_Surface", 1); m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)RenderQueue.Transparent;
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .88f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", .08f);
            return m;
        }

        static Mesh Primitive(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            return mesh;
        }

        Transform Part(string name, Transform parent, Mesh mesh, Material material, bool shadows = true)
        {
            var go = new GameObject(name); go.hideFlags = gameObject.hideFlags;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go.transform;
        }

        void Build(int count)
        {
            built = true;
            sphere = Primitive(PrimitiveType.Sphere); cylinder = Primitive(PrimitiveType.Cylinder); capsule = Primitive(PrimitiveType.Capsule);
            body = Candy(Body); spot = Candy(Spot); belly = Candy(Belly); white = Candy(EyeWhite); pupil = Candy(Pupil); foam = Candy(Foam, true);

            head = Part("Kraken head", transform, sphere, body);
            head.localScale = new Vector3(42, 36, 38);
            headFoam = Part("Head foam", transform, Primitive(PrimitiveType.Cylinder), Candy(Foam, true), false);
            // Gummy spots on the dome.
            var rng = new System.Random(5);
            for (int i = 0; i < 14; i++)
            {
                // Spread evenly over the dome (golden-angle spiral), skipping the face where the eyes are.
                float y = .15f + .75f * (i + .5f) / 14f, a = i * 2.39996f;
                var dir = new Vector3(Mathf.Cos(a) * Mathf.Sqrt(1 - y * y), y, Mathf.Sin(a) * Mathf.Sqrt(1 - y * y)).normalized;
                if (dir.z > .55f && dir.y < .55f) continue;
                var s = Part("Spot", head, sphere, spot);
                s.localPosition = dir * .49f; s.localRotation = Quaternion.LookRotation(dir);
                s.localScale = new Vector3(.12f, .12f, .02f) * (.7f + (float)rng.NextDouble() * .7f);
            }
            for (int e = 0; e < 2; e++)
            {
                float side = e == 0 ? -1 : 1;
                eyes[e] = Part("Eye", head, sphere, white);
                eyes[e].localPosition = new Vector3(side * .19f, .22f, .38f); eyes[e].localScale = new Vector3(.27f, .31f, .2f);
                pupils[e] = Part("Pupil", head, sphere, pupil);
                pupils[e].localScale = new Vector3(.13f, .16f, .08f);
                shines[e] = Part("Shine", head, sphere, white, false);
                shines[e].localScale = Vector3.one * .035f;
                brows[e] = Part("Brow", head, capsule, body);
                brows[e].localPosition = new Vector3(side * .2f, .4f, .37f);
                brows[e].localRotation = Quaternion.Euler(0, 0, 90 + side * 18);
                brows[e].localScale = new Vector3(.08f, .14f, .08f);
            }

            for (int i = 0; i < count; i++)
            {
                var arm = new Arm();
                arm.go = new GameObject("Tentacle " + i); arm.go.hideFlags = gameObject.hideFlags;
                arm.go.transform.SetParent(transform, false);
                arm.mesh = new Mesh { name = "Kraken tentacle" }; arm.mesh.MarkDynamic(); arm.mesh.hideFlags = HideFlags.DontSave;
                arm.verts = new Vector3[(Rings + 1) * (Sides + 1)]; arm.normals = new Vector3[arm.verts.Length];
                var tris = new int[Rings * Sides * 6]; int t = 0;
                for (int r = 0; r < Rings; r++)
                for (int s = 0; s < Sides; s++)
                {
                    int a = r * (Sides + 1) + s, b = a + Sides + 1;
                    tris[t++] = a; tris[t++] = b; tris[t++] = a + 1; tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
                }
                arm.mesh.vertices = arm.verts; arm.mesh.triangles = tris;
                arm.go.AddComponent<MeshFilter>().sharedMesh = arm.mesh;
                arm.go.AddComponent<MeshRenderer>().sharedMaterial = body;
                arm.suckers = new Transform[SuckersPerArm];
                for (int k = 0; k < SuckersPerArm; k++) arm.suckers[k] = Part("Sucker", arm.go.transform, sphere, belly, false);
                arm.foam = Part("Foam", transform, cylinder, foam, false);
                arms.Add(arm);
            }
        }

        // Smoothly follow the latest frame (other players get frames a few times a second).
        public void Show(DisasterFrame f, float dt)
        {
            if (f == null || f.points == null || f.values == null || f.values.Length < 7) return;
            if (!built) Build(f.points.Length);
            float rise = f.values[1], water = f.values[4];
            var tower = new Vector3(f.values[5], water, f.values[6]);
            float k = dt <= 0 ? 1 : 1 - Mathf.Exp(-10 * dt);
            float time = Application.isPlaying ? Time.time : (float)UnityEditor_Time();

            var headTarget = f.center + Vector3.up * (Mathf.Lerp(-30, 6f, rise) + Mathf.Sin(time * .9f) * .7f);
            smoothedHead = smoothedHead == default || dt <= 0 ? headTarget : Vector3.Lerp(smoothedHead, headTarget, k);
            head.position = smoothedHead;
            float wash = 1 + .06f * Mathf.Sin(time * 2.1f);
            headFoam.position = new Vector3(smoothedHead.x, water + .06f, smoothedHead.z);
            headFoam.localScale = new Vector3(48 * wash, rise > .05f ? .03f : 0, 44 * wash);
            var look = Vector3.ProjectOnPlane(tower - f.center, Vector3.up);
            head.rotation = Quaternion.LookRotation(look.sqrMagnitude > .01f ? look : Vector3.forward) * Quaternion.Euler(-8 + Mathf.Sin(time * .6f) * 3, 0, Mathf.Sin(time * .45f) * 3);

            // Pupils look at the middle of the tower, with a blink every few seconds.
            var focus = tower + Vector3.up * (f.values[3] - water) * .45f;
            bool blink = Mathf.Repeat(time, 4.3f) < .12f;
            for (int e = 0; e < 2; e++)
            {
                var eyeWorld = eyes[e].position;
                var toFocus = head.InverseTransformDirection((focus - eyeWorld).normalized);
                var basePos = eyes[e].localPosition;
                pupils[e].localPosition = basePos + new Vector3(toFocus.x * .05f, toFocus.y * .05f, .085f);
                shines[e].localPosition = pupils[e].localPosition + new Vector3(.025f, .035f, .03f);
                eyes[e].localScale = new Vector3(.27f, blink ? .035f : .31f, .2f);
                pupils[e].gameObject.SetActive(!blink); shines[e].gameObject.SetActive(!blink);
            }

            for (int i = 0; i < arms.Count && i < f.points.Length; i++)
            {
                var arm = arms[i]; var p = f.points[i];
                if (!arm.init || dt <= 0) { arm.tip = p.position; arm.basePoint = p.velocity; arm.init = true; }
                else { arm.tip = Vector3.Lerp(arm.tip, p.position, k); arm.basePoint = Vector3.Lerp(arm.basePoint, p.velocity, k); }
                Tentacle(arm, tower, water, (KrakenArmState)(int)p.size, time, i);
            }
        }

        public enum KrakenArmState { Resting, Reaching, Holding, Dragging, Rearing, Slamming, Sinking }

        void Tentacle(Arm arm, Vector3 tower, float water, KrakenArmState state, float time, int index)
        {
            var b = arm.basePoint;
            var start = b + Vector3.down * 14;
            var tip = arm.tip;
            float reach = Vector3.Distance(b, tip);
            var outward = Vector3.ProjectOnPlane(b - tower, Vector3.up).normalized;
            // Rise straight out of the sea, then arc toward the tip.
            var c1 = b + Vector3.up * Mathf.Max(4, reach * .45f) + outward * 2;
            var toTip = (tip - c1); var dirTip = toTip.sqrMagnitude > .01f ? toTip.normalized : Vector3.up;
            var c2 = tip - dirTip * reach * .35f + Vector3.up * reach * .15f;
            float curl = state == KrakenArmState.Holding || state == KrakenArmState.Dragging ? 1.6f : state == KrakenArmState.Slamming ? .2f : .9f;
            var curlAxis = Vector3.Cross(dirTip, Vector3.up).sqrMagnitude > .01f ? Vector3.Cross(dirTip, Vector3.up).normalized : Vector3.right;

            var points = new Vector3[Rings + 1];
            for (int r = 0; r <= Rings; r++)
            {
                float s = r / (float)Rings;
                var p = Bezier(start, c1, c2, tip, s);
                // Lively S-wave along the arm plus a hooked curl at the tip.
                float wave = Mathf.Sin(s * 5.5f - time * 1.8f + index) * (1 - s * .6f) * s * Mathf.Min(9, reach * .22f);
                p += curlAxis * wave;
                float tipCurl = Mathf.Max(0, (s - .72f) / .28f);
                p += (Quaternion.AngleAxis(tipCurl * 210 * curl, curlAxis) * (Vector3.down * 3.2f) - Vector3.down * 3.2f) * tipCurl;
                points[r] = p;
            }
            var up = Vector3.up;
            int v = 0;
            for (int r = 0; r <= Rings; r++)
            {
                float s = r / (float)Rings;
                var tangent = (points[Mathf.Min(r + 1, Rings)] - points[Mathf.Max(r - 1, 0)]).normalized;
                var side = Vector3.Cross(tangent, up); if (side.sqrMagnitude < .001f) side = Vector3.Cross(tangent, Vector3.forward);
                side.Normalize(); var normal = Vector3.Cross(side, tangent).normalized; up = normal;
                float radius = Mathf.Lerp(5f, .5f, Mathf.Pow(s, 1.45f)) * (1 + .05f * Mathf.Sin(s * 23 + time * 3));
                for (int q = 0; q <= Sides; q++)
                {
                    float a = q / (float)Sides * Mathf.PI * 2;
                    var n = side * Mathf.Cos(a) + normal * Mathf.Sin(a);
                    arm.verts[v] = points[r] + n * radius; arm.normals[v] = n; v++;
                }
                // Suckers run along the underside (the side facing the tower / down the curl).
                if (r >= 6 && (r - 6) % 2 == 0)
                {
                    int k = (r - 6) / 2;
                    if (k < arm.suckers.Length)
                    {
                        var underside = -normal;
                        var sk = arm.suckers[k];
                        sk.position = points[r] + underside * radius * .92f;
                        sk.rotation = Quaternion.LookRotation(underside, tangent);
                        sk.localScale = new Vector3(radius * .75f, radius * .75f, radius * .28f);
                    }
                }
            }
            arm.mesh.vertices = arm.verts; arm.mesh.normals = arm.normals; arm.mesh.RecalculateBounds();
            // Foam ring where the arm breaks the surface, pulsing.
            float pulse = 1 + .12f * Mathf.Sin(time * 3 + index);
            arm.foam.position = new Vector3(b.x, water + .05f, b.z);
            arm.foam.localScale = new Vector3(14f * pulse, .03f, 14f * pulse);
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1 - t;
            return u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
        }

        static double UnityEditor_Time()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorApplication.timeSinceStartup;
#else
            return Time.time;
#endif
        }

        void OnDestroy()
        {
            foreach (var m in new[] { body, spot, belly, white, pupil, foam }) if (m) DestroyImmediate(m);
            foreach (var a in arms) if (a.mesh) DestroyImmediate(a.mesh);
        }
    }
}
