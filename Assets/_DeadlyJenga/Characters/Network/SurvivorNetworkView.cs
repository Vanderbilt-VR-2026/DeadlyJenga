using System.IO;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;

namespace DeadlyJenga.CharacterNetwork
{
    // Someone else's survivor (a player or bot) on this machine. No camera, no input.
    // On the host it can also be hit by hazards: pushes and damage go to its owner through the network.
    public sealed class SurvivorNetworkView : MonoBehaviour, INetworkActorView, IPushable, IDamageable, IHazardMotion
    {
        NetworkContext context;
        CandyPlayerRig rig;
        CandyAvatarFrame target, frame;
        int color;
        bool active, hasTarget, initialized, suspended;
        string emote; float emoteStart, emoteLength; int lastEmoteBits;
        CapsuleCollider hazardTarget;
        Vector3 previousFeet, velocity;
        float previousPoseTime;

        public Vector3 HazardVelocity => velocity;
        public float Health => 100;
        public float MaxHealth => 100;
        bool CanBeHit => context.host && !suspended && active && context.sendImpact != null && context.actorId != null;
        public void Push(Vector3 force, GameObject source) { if (CanBeHit) context.sendImpact(context.actorId, force, 0); }
        public void Damage(float amount, GameObject source) { if (CanBeHit) context.sendImpact(context.actorId, Vector3.zero, amount); }
        public void Eliminate(GameObject source) => Damage(10000, source);

        public SurvivorNetworkView Init(NetworkContext networkContext) { context = networkContext; return this; }

        public string NetworkKind => NetworkKinds.Survivor;
        public bool NetworkActive => active;
        public Vector3 NetworkPosition => target.feet;
        public void WriteState(BinaryWriter w) => CharacterWire.WriteSurvivor(w, target, color);

        public void ReadState(BinaryReader r, bool isActive)
        {
            CharacterWire.ReadSurvivor(r, out target, out color);
            if (active && !isActive && rig) Burst();
            active = isActive; hasTarget = true;
            int bits = (color >> 16) & 0xFF;
            if (bits != lastEmoteBits && bits > 0 && bits <= Emotes.Survivor.Length)
            {
                emote = Emotes.Survivor[bits - 1]; emoteStart = Time.time; emoteLength = CandyEmotePoses.Duration(emote);
                GameEvents.RaiseEmoted(rig ? rig.gameObject : gameObject, emote);
            }
            lastEmoteBits = bits;
            if (!rig)
            {
                var avatar = context.localSurvivor ? context.localSurvivor.GetComponent<CandyPlayerAvatar>() : null;
                if (!avatar || !avatar.visualPrefab) return;
                rig = Instantiate(avatar.visualPrefab, transform); rig.ownerCamera = null;
            }
            rig.gameObject.SetActive(active); rig.SetColor(color & 0xFF); CandyCharacterLook.For(rig).Apply(((color >> 8) & 0xFF) - 1);
            if (!initialized) { frame = target; initialized = true; }
            UpdateHazardTarget();
        }

        // Host: a trigger capsule where the remote survivor stands, so hazards can find and hit it.
        void UpdateHazardTarget()
        {
            if (!context.host) { if (hazardTarget) hazardTarget.enabled = false; return; }
            if (!hazardTarget)
            {
                var hit = new GameObject("Remote survivor hazard target"); hit.layer = Layers.Survivor;
                hit.transform.SetParent(transform, false); hazardTarget = hit.AddComponent<CapsuleCollider>(); hazardTarget.isTrigger = true;
            }
            float dt = Time.time - previousPoseTime;
            velocity = previousPoseTime > 0 && dt > .001f ? Vector3.ClampMagnitude((target.feet - previousFeet) / dt, 60) : Vector3.zero;
            previousFeet = target.feet; previousPoseTime = Time.time;
            hazardTarget.transform.position = target.feet;
            hazardTarget.height = Mathf.Max(.2f, 1.7f * target.scale);
            hazardTarget.radius = Mathf.Max(.05f, .24f * target.scale);
            hazardTarget.center = Vector3.up * hazardTarget.height * .5f;
            hazardTarget.enabled = active && !suspended;
        }

        public void Render(float dt)
        {
            if (!hasTarget || !active || !rig) return;
            float t = 1 - Mathf.Exp(-20 * dt);
            frame.feet = Vector3.Lerp(frame.feet, target.feet, t); frame.head = Vector3.Lerp(frame.head, target.head, t);
            frame.headRotation = Quaternion.Slerp(frame.headRotation, target.headRotation, t); frame.scale = target.scale;
            frame.left = Blend(frame.left, target.left, t); frame.right = Blend(frame.right, target.right, t);
            rig.ApplyFrame(frame, true, dt);
            if (emote != null)
            {
                float age = Time.time - emoteStart;
                if (age >= emoteLength) emote = null;
                else rig.ApplyFrame(CandyEmotePoses.Pose(frame, emote, age, emoteLength), true, 0);
            }
        }

        public void Suspend() { suspended = true; if (hazardTarget) hazardTarget.enabled = false; }

        // Someone else's survivor just disappeared (eliminated): let the effects burst it.
        void Burst()
        {
            var colors = rig.style ? rig.style.colors : null;
            var c = colors != null && colors.Length > 0 ? colors[(color & 0xFF) % colors.Length] : Color.white;
            GameEvents.RaiseBodyBurst(rig.gameObject, frame.feet, c, "A player", false);
            emote = null;
        }

        static CandyHandPose Blend(CandyHandPose from, CandyHandPose to, float t)
        {
            to.position = Vector3.Lerp(from.position, to.position, t); to.rotation = Quaternion.Slerp(from.rotation, to.rotation, t); return to;
        }

        void OnDestroy() { if (rig) Destroy(rig.gameObject); }
    }
}
