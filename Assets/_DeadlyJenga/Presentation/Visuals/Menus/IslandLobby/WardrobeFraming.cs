using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public static class WardrobeFraming
    {
        // Constructing this sampler allocates no Unity objects. Refresh runs on the main thread.
        public sealed class GeometrySampler : System.IDisposable
        {
            struct Sample { public Mesh source; public Bounds local; }
            readonly Dictionary<SkinnedMeshRenderer, Sample> samples = new Dictionary<SkinnedMeshRenderer, Sample>();
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<int> indices = new List<int>();
            Mesh scratch;
            bool disposed;

            public void Refresh(List<Renderer> renderers)
            {
                if (disposed) return;
                samples.Clear();
                foreach (var renderer in renderers)
                {
                    if (!Visible(renderer) || !(renderer is SkinnedMeshRenderer skinned) || !skinned.sharedMesh) continue;
                    if (!scratch) scratch = new Mesh { name = "Wardrobe geometry measurement" };
                    scratch.Clear();
                    skinned.BakeMesh(scratch, false);
                    scratch.GetVertices(vertices);
                    bool found = false, valid = true;
                    Bounds local = default;
                    for (int submesh = 0; submesh < scratch.subMeshCount; submesh++)
                    {
                        scratch.GetTriangles(indices, submesh, true);
                        foreach (int index in indices)
                        {
                            if (index < 0 || index >= vertices.Count || !Finite(vertices[index])) { valid = false; break; }
                            if (!found) { local = new Bounds(vertices[index], Vector3.zero); found = true; }
                            else local.Encapsulate(vertices[index]);
                        }
                        if (!valid) break;
                    }
                    if (valid && found) samples[skinned] = new Sample { source = skinned.sharedMesh, local = local };
                }
            }

            public bool TryBounds(List<Renderer> renderers, out Bounds bounds, out bool conservativeFallback)
            {
                bounds = default;
                conservativeFallback = false;
                bool found = false;
                foreach (var renderer in renderers)
                {
                    if (!Visible(renderer)) continue;
                    Bounds next;
                    if (renderer is SkinnedMeshRenderer skinned)
                    {
                        if (!disposed && samples.TryGetValue(skinned, out var sample) && sample.source == skinned.sharedMesh)
                        {
                            if (!WorldBounds(sample.local, skinned.transform.localToWorldMatrix, out next))
                            { next = renderer.bounds; conservativeFallback = true; }
                        }
                        else { next = renderer.bounds; conservativeFallback = true; }
                    }
                    else next = renderer.bounds;
                    if (!Finite(next.center) || !Finite(next.extents) || next.size.sqrMagnitude < .000001f) continue;
                    if (!found) { bounds = next; found = true; }
                    else bounds.Encapsulate(next);
                }
                return found;
            }

            static bool WorldBounds(Bounds local, Matrix4x4 matrix, out Bounds world)
            {
                world = default;
                for (int corner = 0; corner < 8; corner++)
                {
                    var signs = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                    var point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, signs));
                    if (!Finite(point)) return false;
                    if (corner == 0) world = new Bounds(point, Vector3.zero);
                    else world.Encapsulate(point);
                }
                return true;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                samples.Clear(); vertices.Clear(); indices.Clear();
                if (scratch)
                {
                    if (Application.isPlaying) Object.Destroy(scratch);
                    else Object.DestroyImmediate(scratch);
                    scratch = null;
                }
            }
        }

        static bool Visible(Renderer renderer) => renderer && renderer.enabled && !renderer.forceRenderingOff &&
            renderer.gameObject.activeInHierarchy && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer);

        public static bool TryBounds(List<Renderer> renderers, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var renderer in renderers)
            {
                if (!renderer || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy) continue;
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                var next = renderer.bounds;
                if (!Finite(next.center) || !Finite(next.extents) || next.size.sqrMagnitude < .000001f) continue;
                if (!found) { bounds = next; found = true; }
                else bounds.Encapsulate(next);
            }
            return found;
        }

        public static float RequiredDistance(Bounds bounds, Vector3 pivot, Quaternion rotation,
            float verticalFov, float aspect, float nearClip, out float nearDistance)
        {
            float tangent = Mathf.Tan(Mathf.Clamp(verticalFov, 1, 179) * Mathf.Deg2Rad * .5f);
            float horizontal = tangent * Mathf.Max(.01f, aspect);
            float distance = 0;
            nearDistance = 0;
            var inverse = Quaternion.Inverse(rotation);
            for (int corner = 0; corner < 8; corner++)
            {
                var signs = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                var point = inverse * (bounds.center + Vector3.Scale(bounds.extents, signs) - pivot);
                nearDistance = Mathf.Max(nearDistance, nearClip + .05f - point.z);
                float width = Mathf.Abs(point.x) / (.90f * horizontal) - point.z;
                float height = Mathf.Abs(point.y) / (.90f * tangent) - point.z;
                distance = Mathf.Max(distance, Mathf.Max(width, height));
            }
            return Mathf.Max(distance, nearDistance);
        }

        static bool Finite(Vector3 value) => Valid(value.x) && Valid(value.y) && Valid(value.z);
        static bool Valid(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
