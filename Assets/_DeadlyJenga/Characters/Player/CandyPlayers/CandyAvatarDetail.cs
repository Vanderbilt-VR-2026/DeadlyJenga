using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyPlayers
{
    // Only distant external bodies change detail. Tracking, skeletons and local hands stay exact.
    [DefaultExecutionOrder(450)]
    public sealed class CandyAvatarDetail : MonoBehaviour
    {
        static readonly Dictionary<(Mesh, int), Mesh> meshes = new Dictionary<(Mesh, int), Mesh>();
        static Camera[] cameras = new Camera[8];
        static int cameraFrame = -1, cameraCount, users;
        CandyPlayerRig rig;
        Mesh fullBody, fullHead;
        SkinnedMeshRenderer[] bodies = new SkinnedMeshRenderer[3], heads = new SkinnedMeshRenderer[3];
        MaterialPropertyBlock properties;
        float nextRefresh;
        int detail;
        public int Detail => detail;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            foreach (var mesh in meshes.Values) if (mesh) { if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh); }
            meshes.Clear(); cameraFrame = -1; cameraCount = 0; users = 0;
        }
        void Awake()
        {
            properties = new MaterialPropertyBlock();
            users++;rig = GetComponent<CandyPlayerRig>();
            fullBody = rig.bodyRenderer.sharedMesh; fullHead = rig.headRenderer.sharedMesh;
            bodies[0] = rig.bodyRenderer; heads[0] = rig.headRenderer;
        }
        void LateUpdate()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .2f + (GetInstanceID() & 15) * .005f;
            Refresh();
        }
        public void Refresh()
        {
            if (!rig || !rig.enabled) return;
            int level = 0;
            if (!rig.ownerCamera)
            {
                if (cameraFrame != Time.frameCount)
                {
                    cameraFrame = Time.frameCount;
                    int count = Camera.allCamerasCount;
                    if (cameras.Length < count) cameras = new Camera[Mathf.NextPowerOfTwo(count)];
                    cameraCount = Camera.GetAllCameras(cameras);
                }
                float distance = float.PositiveInfinity;
                for (int i = 0; i < cameraCount; i++)
                {
                    var camera = cameras[i];
                    if (!camera || !camera.isActiveAndEnabled || camera.cameraType != CameraType.Game) continue;
                    if (camera.orthographic) { distance = 0; break; }
                    float projected = Vector3.Distance(camera.transform.position, rig.transform.position) / Mathf.Max(.1f, rig.transform.lossyScale.y);
                    projected *= Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) / .57735f;
                    distance = Mathf.Min(distance, projected);
                }
                if (!float.IsPositiveInfinity(distance))
                    level = distance < (detail == 0 ? 8 : 7) ? 0 : distance < (detail == 2 ? 18 : 21) ? 1 : 2;
            }
            if (level == detail) return;
            rig.bodyRenderer = Select(bodies, rig.bodyRenderer, fullBody, level);
            rig.headRenderer = Select(heads, rig.headRenderer, fullHead, level);
            detail = level;
        }
        SkinnedMeshRenderer Select(SkinnedMeshRenderer[] levels, SkinnedMeshRenderer current, Mesh source, int level)
        {
            var next = levels[level];
            if (!next)
            {
                var original = levels[0];
                var child = new GameObject(original.name + " detail " + level);
                child.layer = original.gameObject.layer;
                child.transform.SetParent(original.transform.parent, false);
                child.transform.localPosition = original.transform.localPosition;
                child.transform.localRotation = original.transform.localRotation;
                child.transform.localScale = original.transform.localScale;
                next = child.AddComponent<SkinnedMeshRenderer>();
                next.enabled = false;
                next.sharedMesh = Reduced(source, level);
                next.bones = original.bones;
                next.rootBone = original.rootBone;
                next.localBounds = original.localBounds;
                next.quality = original.quality;
                next.updateWhenOffscreen = original.updateWhenOffscreen;
                next.skinnedMotionVectors = original.skinnedMotionVectors;
                levels[level] = next;
            }
            if (next == current) return current;
            // Each renderer keeps one vertex layout for its entire lifetime.
            current.GetPropertyBlock(properties);
            next.SetPropertyBlock(properties);
            next.sharedMaterials = current.sharedMaterials;
            next.shadowCastingMode = current.shadowCastingMode;
            next.receiveShadows = current.receiveShadows;
            next.renderingLayerMask = current.renderingLayerMask;
            next.lightProbeUsage = current.lightProbeUsage;
            next.reflectionProbeUsage = current.reflectionProbeUsage;
            next.motionVectorGenerationMode = current.motionVectorGenerationMode;
            next.forceRenderingOff = current.forceRenderingOff;
            next.enabled = current.enabled;
            current.enabled = false;
            return next;
        }
        public static Mesh Reduced(Mesh source, int level)
        {
            if (!source || level == 0 || !source.isReadable) return source;
            if (meshes.TryGetValue((source, level), out var found) && found) return found;
            // Skin partitions stop neighbouring fingers or differently driven joints welding together.
            var positions = source.vertices; var normals = source.normals; var uv = source.uv; var weights = source.boneWeights;
            if (weights.Length != positions.Length || normals.Length != positions.Length || uv.Length != positions.Length) return source;
            float size = level == 1 ? .009f : .022f;
            var groups = new Dictionary<(Vector3Int, int, int, int, int, int, int), int>();
            var vertices = new List<Vector3>(); var ns = new List<Vector3>(); var tex = new List<Vector2>(); var skin = new List<BoneWeight>();
            var counts = new List<int>(); var mapping = new int[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var w = weights[i]; var p = positions[i];
                var key = (Vector3Int.RoundToInt(p / size), w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3, Mathf.RoundToInt(w.weight0 * 8), Mathf.RoundToInt(w.weight1 * 8));
                if (!groups.TryGetValue(key, out int index))
                {
                    index = vertices.Count; groups.Add(key, index); vertices.Add(p); ns.Add(normals[i]); tex.Add(uv[i]); skin.Add(w); counts.Add(1);
                }
                else { vertices[index] += p; ns[index] += normals[i]; tex[index] += uv[i]; counts[index]++; }
                mapping[i] = index;
            }
            for (int i = 0; i < vertices.Count; i++) { vertices[i] /= counts[i]; ns[i] = ns[i].normalized; tex[i] /= counts[i]; }
            var mesh = new Mesh { name = source.name + " external detail " + level, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetNormals(ns); mesh.SetUVs(0, tex); mesh.boneWeights = skin.ToArray(); mesh.bindposes = source.bindposes;
            mesh.subMeshCount = source.subMeshCount;
            var triangles = new List<int>();
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                triangles.Clear(); var indices = source.GetTriangles(sub);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int a = mapping[indices[i]], b = mapping[indices[i + 1]], c = mapping[indices[i + 2]];
                    if (a == b || a == c || b == c) continue;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                }
                mesh.SetTriangles(triangles, sub, false);
            }
            mesh.bounds = source.bounds;
            meshes[(source, level)] = mesh; return mesh;
        }
        void OnDestroy() { if(--users<=0)Reset(); }
        void OnDisable()
        {
            if (!rig || !rig.enabled) return;
            rig.bodyRenderer = Select(bodies, rig.bodyRenderer, fullBody, 0);
            rig.headRenderer = Select(heads, rig.headRenderer, fullHead, 0);
            detail = 0;
        }
    }
}
