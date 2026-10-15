using Fusion;
using UnityEngine;

namespace DeadlyJenga.Avatars
{
    /// <summary>Lobby-only presentation of the existing Fusion pose transforms.</summary>
    public sealed class LobbyNetworkAvatar : NetworkBehaviour
    {
        public Transform headPose;
        public Transform leftHandPose;
        public Transform rightHandPose;
        SimpleAvatarView view;

        public override void Spawned()
        {
            var lobby = FindFirstObjectByType<LobbyAvatarScene>();
            if (lobby == null || lobby.visualPrefab == null) return;
            if (Object.HasInputAuthority)
            {
                lobby.SetLocalPlayer(Object.InputAuthority.PlayerId);
                return;
            }

            view = Instantiate(lobby.visualPrefab, transform);
            view.name = "Remote Avatar";
            view.headTarget = headPose;
            view.leftHandTarget = leftHandPose;
            view.rightHandTarget = rightHandPose;
            view.SetLocalView(false);
            view.SetPlayerColor(lobby.ColorForPlayer(Object.InputAuthority.PlayerId));
            view.ApplyTrackingPose();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (view != null) Destroy(view.gameObject);
            view = null;
        }
    }
}
