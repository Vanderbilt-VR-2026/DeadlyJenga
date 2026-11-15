using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    // An independent surface pass leaves skin materials and their property blocks intact.
    sealed class CandySugarSurface
    {
        readonly Renderer source, overlay;
        readonly Material material;
        readonly Dictionary<Mesh, Mesh> meshes = new();
        readonly float span;
        readonly MaterialPropertyBlock properties = new();
        Mesh currentMesh;
        readonly List<Vector3> sampleVertices = new();
        Mesh sampledMesh;

        public CandySugarSurface(Renderer source, Material material, float bottom, float top)
        {
            this.source = source;
            this.material = material;
            span = Mathf.Max(.05f, top - bottom);
            var go = new GameObject("Sticky sugar surface");
            go.transform.SetParent(source.transform, false);
            go.layer = source.gameObject.layer;
            if (source is SkinnedMeshRenderer skin)
            {
                var copy = go.AddComponent<SkinnedMeshRenderer>();
                copy.bones = skin.bones;
                copy.rootBone = skin.rootBone;
                copy.quality = skin.quality;
                overlay = copy;
            }
            else
            {
                go.AddComponent<MeshFilter>();
                overlay = go.AddComponent<MeshRenderer>();
            }
            overlay.shadowCastingMode = ShadowCastingMode.Off;
            overlay.receiveShadows = true;
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            Sync();
        }

        void BeforeCamera(ScriptableRenderContext context, Camera camera) => Sync();

        // Avatar owners select their camera-specific mesh before this subscriber runs.
        void Sync()
        {
            if (!source || !overlay) return;
            overlay.enabled = source.enabled && !source.forceRenderingOff &&
                source.shadowCastingMode != ShadowCastingMode.ShadowsOnly;
            overlay.gameObject.layer = source.gameObject.layer;
            var skin = source as SkinnedMeshRenderer;
            var mesh = skin ? skin.sharedMesh : source.GetComponent<MeshFilter>()?.sharedMesh;
            if (!mesh) { overlay.enabled = false; return; }
            if (!meshes.TryGetValue(mesh, out var surface))
            {
                surface = Object.Instantiate(mesh);
                surface.name = mesh.name + " sugar surface";
                if (mesh.isReadable)
                {
                    var rest = new List<Vector4>(mesh.vertexCount);
                    foreach (var vertex in mesh.vertices)
                    {
                        var p = source.transform.TransformPoint(vertex);
                        var scaled = Vector3.Scale(vertex, source.transform.lossyScale);
                        rest.Add(new Vector4(scaled.x, scaled.y, scaled.z, (p.y - material.GetFloat("_CoatBottom")) / span));
                    }
                    surface.SetUVs(3, rest);
                }
                meshes.Add(mesh, surface);
            }
            if (currentMesh != mesh)
            {
                properties.SetFloat("_HasRest", mesh.isReadable ? 1 : 0);
                overlay.SetPropertyBlock(properties);
                currentMesh = mesh;
            }
            if (overlay is SkinnedMeshRenderer target)
            {
                target.sharedMesh = surface;
                target.localBounds = skin.localBounds;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                    target.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
            }
            else overlay.GetComponent<MeshFilter>().sharedMesh = surface;
            if (overlay.sharedMaterials.Length != mesh.subMeshCount)
            {
                var materials = new Material[mesh.subMeshCount];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                overlay.sharedMaterials = materials;
            }
        }

        public bool TrySample(out Vector3 position)
        {
            position = default;
            if (!source || !source.enabled || !source.gameObject.activeInHierarchy) return false;
            Mesh mesh;
            if (source is SkinnedMeshRenderer skin)
            {
                if (!sampledMesh) sampledMesh = new Mesh();
                skin.BakeMesh(sampledMesh);
                mesh = sampledMesh;
            }
            else mesh = source.GetComponent<MeshFilter>()?.sharedMesh;
            if (!mesh || !mesh.isReadable) return false;
            mesh.GetVertices(sampleVertices);
            if (sampleVertices.Count == 0) return false;
            position = source.transform.TransformPoint(sampleVertices[Random.Range(0, sampleVertices.Count)]);
            return true;
        }

        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            if (overlay) Object.Destroy(overlay.gameObject);
            foreach (var mesh in meshes.Values) Object.Destroy(mesh);
            if (sampledMesh) Object.Destroy(sampledMesh);
        }
    }
}
