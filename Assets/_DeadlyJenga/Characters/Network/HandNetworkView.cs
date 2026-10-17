using System;
using System.IO;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CandyPhysicsHands;

namespace DeadlyJenga.CharacterNetwork
{
    // Someone else's flying hand on this machine. On the host it is a real physics hand that
    // touches the castle, and its actual bones are sent on to everyone else. Elsewhere it just
    // shows the host's palm and finger poses.
    public sealed class HandNetworkView : MonoBehaviour, INetworkActorView
    {
        public CandyPhysicalHand Hand { get; private set; }
        NetworkContext context;
        CandyHandSettings handSettings;
        CandyHandPose target;
        Pose[] bones = Array.Empty<Pose>();
        bool left, active, hasTarget, suspended;
        float size;

        public HandNetworkView Init(NetworkContext networkContext) { context = networkContext; return this; }

        CandyFlyingHandBridge Template => context.localHand ? context.localHand.GetComponent<CandyFlyingHandBridge>() : null;

        public string NetworkKind => NetworkKinds.Hand;
        public bool NetworkActive => Hand ? Hand.gameObject.activeInHierarchy && Hand.skin && Hand.skin.enabled : active;
        public Vector3 NetworkPosition => Hand ? Hand.transform.position : target.position;
        public void WriteState(BinaryWriter w)
        {
            if (Hand) CharacterWire.WriteHand(w, Hand.left, Hand.GeometrySize, Hand.Target, Hand.bodies);
            else CharacterWire.WriteHand(w, left, size, target, bones);
        }

        public void ReadState(BinaryReader r, bool isActive)
        {
            CharacterWire.ReadHand(r, out bool newLeft, out float newSize, out var newTarget, out var newBones);
            var bridge = Template;
            // The host decides every hand's size and handedness.
            if (context.host && bridge) { newSize = bridge.settings.size; newLeft = bridge.useLeftHand; }
            suspended = false;
            if (Hand && (left != newLeft || Mathf.Abs(size - newSize) > .01f)) Clear();
            left = newLeft; size = newSize; target = newTarget; bones = newBones; active = isActive; hasTarget = true;
            if (!Hand)
            {
                if (!bridge || !bridge.prefab) return;
                handSettings = Instantiate(bridge.settings); handSettings.size = size;
                Hand = Instantiate(bridge.prefab, transform); Hand.settings = handSettings; Hand.left = left; Hand.Build(target);
            }
            Hand.gameObject.SetActive(active);
            Hand.SetTarget(target, context.host && active ? 1 : 0);
            if (context.host) Hand.SetInteractive(active);
        }

        public void Suspend()
        {
            suspended = true;
            if (Hand) { Hand.SetTarget(Hand.Target, 0); Hand.SetInteractive(false); }
        }

        public void Render(float dt)
        {
            if (!hasTarget || !active || !Hand) return;
            if (context.host) { if (!suspended) Hand.SetInteractive(true); return; }
            if (bones.Length != 16) return;
            float t = 1 - Mathf.Exp(-20 * dt);
            for (int i = 0; i < 16; i++)
            {
                var body = Hand.bodies[i]; var p = bones[i];
                body.position = Vector3.Lerp(body.position, p.position, t);
                body.rotation = Quaternion.Slerp(body.rotation, p.rotation, t);
            }
            Hand.skin.enabled = true;
        }

        void Clear()
        {
            if (Hand) { Hand.gameObject.SetActive(false); Destroy(Hand.gameObject); Hand = null; }
            if (handSettings) { Destroy(handSettings); handSettings = null; }
        }

        void OnDestroy() { Clear(); }
    }
}
