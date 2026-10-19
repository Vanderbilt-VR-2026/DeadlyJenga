using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    [DisallowMultipleComponent]
    public sealed class CandyVaultGlow : MonoBehaviour
    {
        [SerializeField] Shader glowShader;
        [Header("Candy appearance")]
        [ColorUsage(true, true)] public Color tint = new Color(.08f, .48f, 1f, .75f);
        [Range(0, 3)] public float brightness = 2.3f;
        [Min(.02f)] public float edgeSpan = 1.1f;
        [Min(.005f)] public float topDepth = .09f;
        [Min(.005f)] public float faceDepth = .07f;
        [Min(.001f)] public float surfaceOffset = .008f;
        [Min(.01f)] public float fadeSeconds = .12f;
        [Range(0, .3f)] public float shimmer = .06f;

        IVaultTargetProvider provider;
        IMovementState owner;
        GameObject visual;
        Mesh mesh;
        Material material;
        MeshRenderer meshRenderer;
        Collider lastSurface;
        float opacity;
        static readonly Vector3[] Axes = { Vector3.right, Vector3.up, Vector3.forward };
        readonly Vector3[] corners = new Vector3[4];
        readonly float[] lengths = new float[4];
        readonly List<float> cuts = new List<float>(40);
        readonly List<Vector3> vertices = new List<Vector3>(120);
        readonly List<Vector2> uv = new List<Vector2>(120);
        readonly List<int> triangles = new List<int>(480);

        void Awake()
        {
            if(tint==new Color(.25f,.75f,1,.32f)&&Mathf.Approximately(brightness,1.2f)){tint=new Color(.08f,.48f,1,.75f);brightness=2.3f;}
            provider = GetComponent<IVaultTargetProvider>(); owner = GetComponent<IMovementState>();
            if (!glowShader || provider == null || owner == null) { enabled = false; return; }
            visual = new GameObject("Visual - vault ledge glow");
            mesh = new Mesh { name = "Candy vault rim" }; mesh.MarkDynamic();
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = visual.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off; meshRenderer.receiveShadows = false;
            material = new Material(glowShader); meshRenderer.sharedMaterial = material;
            meshRenderer.enabled = false;
        }

        void LateUpdate()
        {
            if (!visual) return;
            if(provider==null||owner==null){provider=GetComponent<IVaultTargetProvider>();owner=GetComponent<IMovementState>();}
            if(provider==null||owner==null)return;
            Collider surface = null; Vector3 edge = default;
            bool visible = owner.IsLocal && !owner.InputPaused && !(Services.Session?.MenuOpen ?? false)
                && provider.TryGetVaultTarget(out surface, out edge, out _);
            // Keep the last mesh attached to its surface while it fades away.
            if (visible)
            {
                visible = surface is BoxCollider box && BuildRim(box, edge);
                if (visible) lastSurface = surface;
            }
            if (!lastSurface || !lastSurface.enabled || !lastSurface.gameObject.activeInHierarchy || owner.InputPaused || !owner.IsLocal)
                opacity = 0;
            else opacity = Mathf.MoveTowards(opacity, visible ? 1 : 0, Time.unscaledDeltaTime / Mathf.Max(.01f, fadeSeconds));
            meshRenderer.enabled = opacity > .001f;
            if (lastSurface)
            {
                visual.transform.SetPositionAndRotation(lastSurface.transform.position, lastSurface.transform.rotation);
                visual.transform.localScale = lastSurface.transform.lossyScale;
            }
            Color color = tint; color.a *= opacity;
            material.SetColor("_Tint", color);
            material.SetFloat("_Brightness", Mathf.Max(0, brightness) * (1 + shimmer * Mathf.Sin(Time.unscaledTime * 2.5f)));
        }

        bool BuildRim(BoxCollider box, Vector3 edge)
        {
            Transform t = box.transform;
            int upAxis = 0;
            for (int i = 1; i < 3; i++)
                if (Mathf.Abs(Vector3.Dot(t.TransformDirection(Axes[i]), Vector3.up)) > Mathf.Abs(Vector3.Dot(t.TransformDirection(Axes[upAxis]), Vector3.up))) upAxis = i;
            Vector3 normal = t.TransformDirection(Axes[upAxis]).normalized;
            float sign = Vector3.Dot(normal, Vector3.up) >= 0 ? 1 : -1; normal *= sign;
            int a = (upAxis + 1) % 3, b = (upAxis + 2) % 3;
            Vector3 half = box.size * .5f;
            Vector3 center = t.TransformPoint(box.center + Axes[upAxis] * half[upAxis] * sign);
            float offset = Mathf.Max(surfaceOffset, Vector3.Dot(edge - center, normal) + surfaceOffset);
            center += normal * offset;
            Vector3 along = t.TransformVector(Axes[a] * half[a]);
            Vector3 across = t.TransformVector(Axes[b] * half[b]);
            corners[0] = center - along - across; corners[1] = center + along - across;
            corners[2] = center + along + across; corners[3] = center - along + across;
            float perimeter = 0, nearest = 0, best = float.PositiveInfinity;
            for (int i = 0; i < 4; i++)
            {
                Vector3 delta = corners[(i + 1) % 4] - corners[i];
                lengths[i] = delta.magnitude;
                if (lengths[i] < .001f) return false;
                float fraction = Mathf.Clamp01(Vector3.Dot(edge - corners[i], delta) / delta.sqrMagnitude);
                float distance = (edge - corners[i] - delta * fraction).sqrMagnitude;
                if (distance < best) { best = distance; nearest = perimeter + lengths[i] * fraction; }
                perimeter += lengths[i];
            }
            float span = Mathf.Clamp(edgeSpan, .02f, perimeter), start = nearest - span * .5f;
            cuts.Clear(); cuts.Add(0); cuts.Add(span);
            float cornerDistance = 0;
            for (int i = 0; i < 4; i++)
            {
                float distance = Mathf.Repeat(cornerDistance - start, perimeter);
                if (distance > .0001f && distance < span - .0001f) cuts.Add(distance);
                cornerDistance += lengths[i];
            }
            for (int i = 1; i < 24; i++) cuts.Add(span * i / 24f);
            cuts.Sort(); vertices.Clear(); uv.Clear(); triangles.Clear();
            float inset = Mathf.Min(topDepth, Mathf.Min(along.magnitude, across.magnitude) * .8f);
            foreach (float distance in cuts)
            {
                float s = Mathf.Repeat(start + distance, perimeter); int segment = 0;
                while (segment < 3 && s > lengths[segment]) { s -= lengths[segment]; segment++; }
                int next = (segment + 1) % 4; float f = s / lengths[segment];
                Vector3 rim = Vector3.Lerp(corners[segment], corners[next], f);
                Vector3 Inner(int index)
                {
                    Vector3 relative = corners[index] - center;
                    return corners[index] - along.normalized * Mathf.Sign(Vector3.Dot(relative, along)) * inset
                        - across.normalized * Mathf.Sign(Vector3.Dot(relative, across)) * inset;
                }
                Vector3 inner = Vector3.Lerp(Inner(segment), Inner(next), f);
                // A shared rim keeps both ribbons joined through right-angle corners.
                Vector3 outer = rim + (rim - inner).normalized * surfaceOffset;
                vertices.Add(t.InverseTransformPoint(inner)); vertices.Add(t.InverseTransformPoint(outer));
                vertices.Add(t.InverseTransformPoint(outer - normal * Mathf.Max(.005f, faceDepth)));
                float u = distance / span;
                uv.Add(new Vector2(u, 0)); uv.Add(new Vector2(u, .5f)); uv.Add(new Vector2(u, 1));
            }
            for (int i = 0; i < cuts.Count - 1; i++)
                for (int strip = 0; strip < 2; strip++)
                {
                    int v = i * 3 + strip;
                    triangles.Add(v); triangles.Add(v + 3); triangles.Add(v + 1);
                    triangles.Add(v + 1); triangles.Add(v + 3); triangles.Add(v + 4);
                }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return true;
        }

        void OnDisable() { opacity = 0; if (meshRenderer) meshRenderer.enabled = false; }
        void OnDestroy()
        {
            if (visual) Destroy(visual);
            if (mesh) Destroy(mesh);
            if (material) Destroy(material);
        }
    }
}
