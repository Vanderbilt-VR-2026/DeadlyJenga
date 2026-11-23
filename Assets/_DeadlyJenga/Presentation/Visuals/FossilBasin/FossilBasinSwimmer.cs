using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    public sealed class FossilBasinSwimmer : MonoBehaviour
    {
        static readonly int Stroke = Shader.PropertyToID("_Stroke");
        MeshRenderer skin;
        MaterialPropertyBlock motion;
        float elapsed;
        IWorldQuery world;
        Transform map;
        Matrix4x4 mapMatrix;
        Bounds bounds;

        internal void Initialize(Mesh model, Material material)
        {
            motion = new MaterialPropertyBlock();
            gameObject.AddComponent<MeshFilter>().sharedMesh = model;
            skin = gameObject.AddComponent<MeshRenderer>();
            skin.sharedMaterial = material;
            map = transform.parent;
            world = map.GetComponent<IWorldQuery>();
            skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            SamplePose(0f);
        }

        void LateUpdate()
        {
            elapsed += Time.deltaTime;
            SamplePose(elapsed);
        }

        // +X is the muzzle, +Y is dorsal. One surfacing sequence spans 32 seconds.
        public void SamplePose(float seconds)
        {
            if (world == null || !skin) return;
            float phase = Mathf.Repeat(seconds, 32f);
            float head = Envelope(phase, 7.25f, 16.75f, .14f);
            float breachTime = Mathf.Clamp01(Mathf.Repeat(seconds - 23.25f, 32f) / 9.5f);
            float arc = Mathf.Sin(Mathf.PI * breachTime);
            float leap = arc * arc;

            // The wider orbit and the shorter surfacing cycle put successive breaches on different shores.
            float angle = seconds * (2f * Mathf.PI / 53f) - 1.1f;
            float size = map.lossyScale.x;
            if (mapMatrix != map.localToWorldMatrix)
            {
                mapMatrix = map.localToWorldMatrix;
                bounds = world.PlayableBounds;
            }
            float rx = bounds.extents.x + 13f * size;
            float rz = bounds.extents.z + 14f * size;
            var position = new Vector3(bounds.center.x + rx * Mathf.Cos(angle), 0,
                bounds.center.z + rz * Mathf.Sin(angle));
            const float modelScale = 1.65f;
            float lift = -1.38f + head * .91f + leap * 6.2f + .045f * Mathf.Sin(seconds * 2f);
            position.y = world.WaterHeight(position) + lift * modelScale * size;
            var tangent = new Vector3(-rx * Mathf.Sin(angle), 0, rz * Mathf.Cos(angle)).normalized;
            float pitch = 18f * head + 34f * leap * Mathf.Cos(breachTime * Mathf.PI);
            var facing = Quaternion.LookRotation(Vector3.Cross(tangent, Vector3.up), Vector3.up);
            transform.SetPositionAndRotation(position, facing * Quaternion.AngleAxis(pitch, Vector3.forward));
            transform.localScale = Vector3.one * modelScale;
            motion.SetVector(Stroke, new Vector4(seconds * 3.1f, head, leap, 0));
            skin.SetPropertyBlock(motion);
        }

        static float Envelope(float phase, float start, float end, float ramp)
        {
            float t = Mathf.InverseLerp(start, end, phase);
            return Mathf.SmoothStep(0, 1, Mathf.Min(t, 1f - t) / ramp);
        }
    }
}
