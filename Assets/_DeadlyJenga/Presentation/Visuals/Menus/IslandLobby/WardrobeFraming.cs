using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public static class WardrobeFraming
    {
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
