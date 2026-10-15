using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace DeadlyJenga.Avatars
{
    /// <summary>Exists only in Lobby. Owns immediate local hands, including before joining.</summary>
    public sealed class LobbyAvatarScene : MonoBehaviour
    {
        public SimpleAvatarView visualPrefab;
        public NetworkObject networkPrefab;
        public Transform headTarget;
        public Transform leftHandTarget;
        public Transform rightHandTarget;
        public Color[] playerColors = {
            new Color(.12f, .65f, .95f), new Color(.98f, .28f, .20f),
            new Color(.35f, .85f, .20f), new Color(.85f, .20f, .92f),
            new Color(1f, .80f, .10f), new Color(.10f, .85f, .75f)
        };

        SimpleAvatarView localView;
        readonly Dictionary<Renderer, bool> hiddenControllers = new();

        void OnEnable()
        {
            if (visualPrefab == null || headTarget == null || leftHandTarget == null || rightHandTarget == null)
            {
                Debug.LogError("Lobby avatar is missing its prefab or tracking targets.", this);
                return;
            }
            localView = Instantiate(visualPrefab, transform);
            localView.name = "Local Avatar";
            localView.headTarget = headTarget;
            localView.leftHandTarget = leftHandTarget;
            localView.rightHandTarget = rightHandTarget;
            localView.SetLocalView(true);
            localView.SetPlayerColor(ColorForPlayer(1));
            localView.ApplyTrackingPose();
            HideController(leftHandTarget);
            HideController(rightHandTarget);
        }

        void HideController(Transform target)
        {
            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                // Keep interaction rays and all colliders/input components intact.
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                hiddenControllers.TryAdd(renderer, renderer.forceRenderingOff);
                renderer.forceRenderingOff = true;
            }
        }

        public Color ColorForPlayer(int playerId)
        {
            if (playerColors == null || playerColors.Length == 0) return Color.cyan;
            return playerColors[Mathf.Max(0, playerId - 1) % playerColors.Length];
        }

        public void SetLocalPlayer(int playerId)
        {
            if (localView != null) localView.SetPlayerColor(ColorForPlayer(playerId));
        }

        void OnDisable()
        {
            foreach (var pair in hiddenControllers)
                if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            hiddenControllers.Clear();
            if (localView != null) Destroy(localView.gameObject);
            localView = null;
        }
    }
}
