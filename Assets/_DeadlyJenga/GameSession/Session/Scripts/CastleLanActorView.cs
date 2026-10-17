using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CandyPhysicsHands;
using P = DeadlyJenga.CastlePrototype.CastleLanProtocol;

namespace DeadlyJenga.CastlePrototype
{
    // Remote avatars have no cameras/input. Remote hand contacts exist only on
    // the host; all other devices display the host's actual palm/finger poses.
    public sealed class CastleLanActorView : MonoBehaviour, IPushable, IDamageable, IHazardMotion
    {
        public CandyPhysicalHand Hand { get; private set; }
        CandyPlayerRig rig;
        CandyHandSettings handSettings;
        CastlePlaySession session;
        bool authority, initialized, suspended;
        P.Actor target;
        public Vector3 WorldPosition=>target==null?transform.position:target.hand?target.target.position:target.avatar.head;
        CandyAvatarFrame frame;
        CapsuleCollider hazardTarget;
        Vector3 previousFeet, velocity;
        float previousPoseTime;
        public Vector3 HazardVelocity => velocity;
        public float Health => 100;
        public float MaxHealth => 100;
        public void Push(Vector3 force, GameObject source)
        {
            if (authority && !suspended && target != null && !target.hand && target.active)
                session.GetComponent<CastleFusionSession>()?.QueueDisasterImpact(target.id, force, 0);
        }
        public void Damage(float amount, GameObject source)
        {
            if (authority && !suspended && target != null && !target.hand && target.active)
                session.GetComponent<CastleFusionSession>()?.QueueDisasterImpact(target.id, Vector3.zero, amount);
        }
        public void Eliminate(GameObject source) => Damage(10000, source);
        public void Initialize(CastlePlaySession owner, bool host) { session = owner; authority = host; }
        public void Receive(P.Actor actor)
        {
            suspended=false;
            if (target != null && (target.hand != actor.hand || actor.hand && (target.left != actor.left || Mathf.Abs(target.size - actor.size) > .01f))) Clear();
            target = actor;
            if (authority && !actor.hand)
            {
                if (!hazardTarget)
                {
                    var hit = new GameObject("Remote survivor hazard target"); hit.layer = Layers.Survivor;
                    hit.transform.SetParent(transform, false); hazardTarget = hit.AddComponent<CapsuleCollider>(); hazardTarget.isTrigger = true;
                }
                float dt = Time.time - previousPoseTime;
                velocity = previousPoseTime > 0 && dt > .001f ? Vector3.ClampMagnitude((actor.avatar.feet - previousFeet) / dt, 60) : Vector3.zero;
                previousFeet = actor.avatar.feet; previousPoseTime = Time.time;
                hazardTarget.transform.position = actor.avatar.feet;
                hazardTarget.height = Mathf.Max(.2f, 1.7f * actor.avatar.scale);
                hazardTarget.radius = Mathf.Max(.05f, .24f * actor.avatar.scale);
                hazardTarget.center = Vector3.up * hazardTarget.height * .5f;
                hazardTarget.enabled = actor.active;
            }
            else if (hazardTarget) hazardTarget.enabled = false;
            if (actor.hand)
            {
                if (!Hand)
                {
                    var bridge = session.Hand.GetComponent<CandyFlyingHandBridge>();
                    if (!bridge || !bridge.prefab) return;
                    handSettings = Instantiate(bridge.settings); handSettings.size = actor.size;
                    Hand = Instantiate(bridge.prefab, transform); Hand.settings = handSettings; Hand.left = actor.left; Hand.Build(actor.target);
                }
                Hand.gameObject.SetActive(actor.active);
                Hand.SetTarget(actor.target, authority && actor.active ? 1 : 0);
                if (authority) Hand.SetInteractive(actor.active);
            }
            else
            {
                if (!rig)
                {
                    var avatar = session.Player.GetComponent<CandyPlayerAvatar>();
                    if (!avatar || !avatar.visualPrefab) return;
                    rig = Instantiate(avatar.visualPrefab, transform); rig.ownerCamera = null;
                }
                rig.gameObject.SetActive(actor.active); rig.SetColor(actor.color);
                if (!initialized) { frame = actor.avatar; initialized = true; }
            }
        }
        public void Suspend() { suspended=true;if(hazardTarget)hazardTarget.enabled=false;if (Hand) { Hand.SetTarget(Hand.Target, 0); Hand.SetInteractive(false); } }
        public void Render(float dt)
        {
            if (target == null || !target.active) return;
            float t = 1 - Mathf.Exp(-20 * dt);
            if (target.hand)
            {
                if (!Hand) return;
                if (authority) { if(!suspended)Hand.SetInteractive(true);return; }
                if (target.bones.Length != 16) return;
                for (int i = 0; i < 16; i++)
                {
                    var body = Hand.bodies[i]; var p = target.bones[i];
                    body.position = Vector3.Lerp(body.position, p.position, t);
                    body.rotation = Quaternion.Slerp(body.rotation, p.rotation, t);
                }
                Hand.skin.enabled = true;
            }
            else if (rig)
            {
                var next = target.avatar;
                frame.feet = Vector3.Lerp(frame.feet, next.feet, t); frame.head = Vector3.Lerp(frame.head, next.head, t);
                frame.headRotation = Quaternion.Slerp(frame.headRotation, next.headRotation, t); frame.scale = next.scale;
                frame.left = Blend(frame.left, next.left, t); frame.right = Blend(frame.right, next.right, t);
                rig.ApplyFrame(frame, true, dt);
            }
        }
        static CandyHandPose Blend(CandyHandPose from, CandyHandPose to, float t)
        {
            to.position = Vector3.Lerp(from.position, to.position, t); to.rotation = Quaternion.Slerp(from.rotation, to.rotation, t); return to;
        }
        void Clear()
        {
            if (Hand) { Hand.gameObject.SetActive(false); Destroy(Hand.gameObject); Hand = null; }
            if (rig) { Destroy(rig.gameObject); rig = null; }
            if (handSettings) { Destroy(handSettings); handSettings = null; }
            initialized = false;
        }
        void OnDestroy() { Clear(); }
    }
}
