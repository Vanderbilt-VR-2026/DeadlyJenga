using System.IO;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyPhysicsHands
{
    [DisallowMultipleComponent]
    public sealed class HandDriveProfile : MonoBehaviour
    {
        public const float DefaultForceNewtons = 1200;
        public const float DefaultFriction = 2f;
        public const float MaximumForceNewtons = 10000;
        public const float MaximumFriction = 2;
        public float ForceNewtons { get; private set; } = DefaultForceNewtons;
        public float Friction { get; private set; } = DefaultFriction;
        public bool Confirmed { get; private set; }

        public static HandDriveProfile For(GameObject player)
        {
            var profile = player.GetComponent<HandDriveProfile>();
            return profile ? profile : player.AddComponent<HandDriveProfile>();
        }

        public HandPhysicsRules CopyRules() => new HandPhysicsRules
        {
            forceNewtons = ForceNewtons,
            friction = Friction
        };

        public void Apply(HandPhysicsRules requested)
        {
            float force = requested?.forceNewtons ?? DefaultForceNewtons;
            float friction = requested?.friction ?? DefaultFriction;
            if (float.IsNaN(force) || float.IsInfinity(force) ||
                float.IsNaN(friction) || float.IsInfinity(friction))
                throw new InvalidDataException("Invalid hand physics rules");

            ForceNewtons = Mathf.Clamp(force, 0, MaximumForceNewtons);
            Friction = Mathf.Clamp(friction, 0, MaximumFriction);
            Confirmed = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()
        {
            GameEvents.SessionStarted += Bind;
            GameEvents.LobbyStarted += Bind;
        }

        static void Bind(GameObject root)
        {
            var session = root ? root.GetComponent<ISessionInfo>() : null;
            if (session?.Player == null) return;
            var profile = For(session.Player.gameObject);
            if (session.Multiplayer?.IsClient != true && !profile.Confirmed) profile.Apply(null);
            var bridge = session.Hand?.gameObject.GetComponent<CandyFlyingHandBridge>();
            if (bridge) bridge.PhysicsProfile = profile;
        }
    }
}
