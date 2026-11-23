using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    public sealed partial class FantasyMapArt
    {
        // Index triangles once per shared sculpt. Near a camera, test just its column
        // in mesh space so rotation, bobbing, body shape and island scale all follow the art.
        sealed class CloudDepth
        {
            readonly Vector3[] vertices;
            readonly int[] indices;
            readonly List<int>[] columns;
            readonly Bounds bounds;
            readonly int size;

            public CloudDepth(Mesh mesh, int size)
            {
                this.size = size;
                bounds = mesh.bounds;
                vertices = mesh.vertices;
                indices = mesh.triangles;
                columns = new List<int>[size * size];
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                    var lo = Vector3.Min(a, Vector3.Min(b, c));
                    var hi = Vector3.Max(a, Vector3.Max(b, c));
                    for (int z = Z(lo.z); z <= Z(hi.z); z++)
                        for (int x = X(lo.x); x <= X(hi.x); x++)
                            (columns[z * size + x] ??= new List<int>()).Add(i);
                }
            }

            int X(float x) => Mathf.Clamp((int)((x - bounds.min.x) / bounds.size.x * size), 0, size - 1);
            int Z(float z) => Mathf.Clamp((int)((z - bounds.min.z) / bounds.size.z * size), 0, size - 1);

            bool Inside(Vector3 p)
            {
                if (!bounds.Contains(p)) return false;
                var candidates = columns[Z(p.z) * size + X(p.x)];
                if (candidates == null) return false;
                int winding = 0;
                foreach (int i in candidates)
                {
                    Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]] - a, c = vertices[indices[i + 2]] - a;
                    Vector3 d = p - a;
                    float det = b.x * c.z - b.z * c.x;
                    if (Mathf.Abs(det) < 1e-12f) continue;
                    float u = (d.x * c.z - d.z * c.x) / det, v = (b.x * d.z - b.z * d.x) / det;
                    if (u < 0 || v < 0 || u + v > 1 || a.y + b.y * u + c.y * v < p.y) continue;
                    winding += det > 0 ? 1 : -1;
                }
                return winding != 0;
            }

            public float Density(Vector3 p)
            {
                // Fully obscure the interior. Soften entry just outside the surface.
                if (Inside(p)) return 1;
                var delta = p - bounds.center;
                if (!Inside(bounds.center + delta * .95f)) return 0;
                float lo = .95f, hi = 1;
                for (int i = 0; i < 4; i++)
                {
                    float mid = (lo + hi) * .5f;
                    if (Inside(bounds.center + delta * mid)) lo = mid;
                    else hi = mid;
                }
                return Mathf.SmoothStep(0, 1, (lo - .95f) * 20);
            }
        }

        CloudDepth islandDepth;
        Transform cloudBank;
        MeshRenderer mistCurtain;
        Material mistMaterial;
        static readonly int MistTint = Shader.PropertyToID("_MistTint");

        void BuildCloudMist(MeshFilter bank)
        {
            islandDepth = new CloudDepth(bank.sharedMesh, 96);
            cloudBank = bank.transform;
            var shader = Resources.Load<Shader>("FantasyMaps/CloudInterior");
            mistMaterial = new Material(shader);
            var go = new GameObject("Cloud interior mist", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            var mesh = new Mesh { name = "Cloud interior screen triangle" };
            mesh.vertices = new[] { new Vector3(-1,-1), new Vector3(-1,3), new Vector3(3,-1) };
            mesh.triangles = new[] { 0,1,2 };
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            mistCurtain = go.GetComponent<MeshRenderer>();
            mistCurtain.sharedMaterial = mistMaterial;
            mistCurtain.shadowCastingMode = ShadowCastingMode.Off;
            mistCurtain.allowOcclusionWhenDynamic = false;
            mistCurtain.enabled = false;
            ownedObjects.Add(go);
            ownedMeshes.Add(mesh);
            RenderPipelineManager.beginCameraRendering += ShowCloudMist;
            RenderPipelineManager.endCameraRendering += HideCloudMist;
        }

        void ShowCloudMist(ScriptableRenderContext context, Camera camera)
        {
            if (!mistCurtain || !cloudBank || camera.cameraType != CameraType.Game) return;
            var eye = camera.transform.position;
            var color = Color.white;
            float density = islandDepth.Density(cloudBank.InverseTransformPoint(eye));
            if (density < 1)
                foreach (var resident in residents)
                {
                    float amount = resident.shape.Density(resident.body.InverseTransformPoint(eye));
                    if (amount <= density) continue;
                    density = amount;
                    color = resident.mistTint;
                    if (density >= 1) break;
                }
            mistCurtain.enabled = density > 0;
            if (!mistCurtain.enabled) return;
            color.a = density;
            mistMaterial.SetVector(MistTint, color);
            mistCurtain.transform.position = eye + camera.transform.forward * (camera.nearClipPlane + 1);
        }

        void HideCloudMist(ScriptableRenderContext context, Camera camera)
        {
            if (mistCurtain) mistCurtain.enabled = false;
        }

        void ClearCloudMist()
        {
            RenderPipelineManager.beginCameraRendering -= ShowCloudMist;
            RenderPipelineManager.endCameraRendering -= HideCloudMist;
            Release(mistMaterial);
            islandDepth = null;
            cloudBank = null;
        }
    }
}
