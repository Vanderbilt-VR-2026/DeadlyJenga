using UnityEngine;

namespace DeadlyJenga.Avatars
{
    /// <summary>Presentation only: follows supplied local or replicated poses.</summary>
    [DisallowMultipleComponent]
    public sealed class SimpleAvatarView : MonoBehaviour
    {
        [Header("Avatar mesh pivots")]
        public Transform head;
        public Transform torso;
        public Transform leftHand;
        public Transform rightHand;

        [Header("Tracking targets — assign from the XR rig or network poses")]
        public Transform headTarget;
        public Transform leftHandTarget;
        public Transform rightHandTarget;

        [Header("Fit — meters and degrees in target-local space")]
        public Vector3 torsoOffset = new Vector3(0f, -0.45f, -0.075f);
        public Vector3 leftHandPositionOffset;
        public Vector3 rightHandPositionOffset;
        public Vector3 leftHandRotationOffset;
        public Vector3 rightHandRotationOffset;

        [Header("Appearance")]
        public Color playerColor = new Color(0.12f, 0.65f, 0.95f);
        [Tooltip("Hide the head and torso on the owning client to prevent camera clipping.")]
        public bool localView;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        Renderer[] renderers;
        Renderer[] bodyRenderers;
        MaterialPropertyBlock properties;
        Quaternion bodyYaw = Quaternion.identity;

        void Awake()
        {
            CacheRenderers();
            if (torso != null) bodyYaw = Quaternion.Euler(0f, torso.eulerAngles.y, 0f);
        }

        void OnEnable()
        {
            SetPlayerColor(playerColor);
            SetLocalView(localView);
        }

        void OnValidate()
        {
            // Refresh references when authoring or previewing the prefab.
            renderers = null;
            CacheRenderers();
            SetPlayerColor(playerColor);
            SetLocalView(localView);
        }

        void CacheRenderers()
        {
            if (renderers != null) return;
            renderers = GetComponentsInChildren<Renderer>(true);
            var body = new System.Collections.Generic.List<Renderer>();
            if (head != null) body.AddRange(head.GetComponentsInChildren<Renderer>(true));
            if (torso != null) body.AddRange(torso.GetComponentsInChildren<Renderer>(true));
            bodyRenderers = body.ToArray();
            properties = new MaterialPropertyBlock();
        }

        public void SetPlayerColor(Color color)
        {
            playerColor = color;
            CacheRenderers();
            foreach (var target in renderers)
            {
                if (target == null) continue;
                target.GetPropertyBlock(properties);
                properties.SetColor(BaseColor, color);
                properties.SetColor(ColorId, color);
                target.SetPropertyBlock(properties);
            }
        }

        public void SetLocalView(bool isLocal)
        {
            localView = isLocal;
            CacheRenderers();
            foreach (var target in bodyRenderers)
                if (target != null) target.forceRenderingOff = isLocal;
        }

        void LateUpdate() => ApplyTrackingPose();

        public void ApplyTrackingPose()
        {
            if (headTarget != null)
            {
                if (head != null)
                    head.SetPositionAndRotation(headTarget.position, headTarget.rotation);

                // Keep the torso upright; looking straight up/down retains the last yaw.
                Vector3 forward = Vector3.ProjectOnPlane(headTarget.forward, Vector3.up);
                if (forward.sqrMagnitude > 0.001f)
                    bodyYaw = Quaternion.LookRotation(forward, Vector3.up);
                if (torso != null)
                    torso.SetPositionAndRotation(headTarget.position + bodyYaw * torsoOffset, bodyYaw);
            }
            Follow(leftHand, leftHandTarget, leftHandPositionOffset, leftHandRotationOffset);
            Follow(rightHand, rightHandTarget, rightHandPositionOffset, rightHandRotationOffset);
        }

        static void Follow(Transform mesh, Transform target, Vector3 positionOffset, Vector3 rotationOffset)
        {
            if (mesh == null || target == null) return;
            mesh.SetPositionAndRotation(target.position + target.rotation * positionOffset,
                target.rotation * Quaternion.Euler(rotationOffset));
        }
    }
}
