using System.IO;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;

namespace DeadlyJenga.CharacterNetwork
{
    // Someone else's survivor (a player or bot) on this machine. No camera, no input.
    public sealed class SurvivorNetworkView : MonoBehaviour, INetworkActorView
    {
        NetworkContext context;
        CandyPlayerRig rig;
        CandyAvatarFrame target, frame;
        int color;
        bool active, hasTarget, initialized;

        public SurvivorNetworkView Init(NetworkContext networkContext) { context = networkContext; return this; }

        public string NetworkKind => NetworkKinds.Survivor;
        public bool NetworkActive => active;
        public Vector3 NetworkPosition => target.feet;
        public void WriteState(BinaryWriter w) => CharacterWire.WriteSurvivor(w, target, color);

        public void ReadState(BinaryReader r, bool isActive)
        {
            CharacterWire.ReadSurvivor(r, out target, out color);
            active = isActive; hasTarget = true;
            if (!rig)
            {
                var avatar = context.localSurvivor ? context.localSurvivor.GetComponent<CandyPlayerAvatar>() : null;
                if (!avatar || !avatar.visualPrefab) return;
                rig = Instantiate(avatar.visualPrefab, transform); rig.ownerCamera = null;
            }
            rig.gameObject.SetActive(active); rig.SetColor(color);
            if (!initialized) { frame = target; initialized = true; }
        }

        public void Render(float dt)
        {
            if (!hasTarget || !active || !rig) return;
            float t = 1 - Mathf.Exp(-20 * dt);
            frame.feet = Vector3.Lerp(frame.feet, target.feet, t); frame.head = Vector3.Lerp(frame.head, target.head, t);
            frame.headRotation = Quaternion.Slerp(frame.headRotation, target.headRotation, t); frame.scale = target.scale;
            frame.left = Blend(frame.left, target.left, t); frame.right = Blend(frame.right, target.right, t);
            rig.ApplyFrame(frame, true, dt);
        }

        public void Suspend() { }

        static CandyHandPose Blend(CandyHandPose from, CandyHandPose to, float t)
        {
            to.position = Vector3.Lerp(from.position, to.position, t); to.rotation = Quaternion.Slerp(from.rotation, to.rotation, t); return to;
        }

        void OnDestroy() { if (rig) Destroy(rig.gameObject); }
    }
}
