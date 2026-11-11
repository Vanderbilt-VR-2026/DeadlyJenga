using System.IO;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;

namespace DeadlyJenga.CharacterNetwork
{
    // Someone else's survivor (a player or bot) on this machine. No camera, no input.
    // On the host it can also be hit by hazards: pushes and damage go to its owner through the network.
    public sealed class SurvivorNetworkView : MonoBehaviour, INetworkActorView, IPushable, IDamageable, IHazardMotion, IModeActor, IEnemyActor, IEnemyPose
    {
        NetworkContext context;
        CandyPlayerRig rig;
        CandyAvatarFrame target, frame;
        int color;
        AvatarAppearance appearance;
        EnemySpawnSettings enemy;
        byte blaster;Transform blasterModel;CandyWeaponVisual weaponVisual;
        public bool IsEnemy=>enemy!=null;
        public bool PreparingMelee=>enemy?.preparingMelee==true;
        public string ArchetypeId=>enemy?.archetypeId;
        public float HitDamage=>enemy?.hitDamage??0;
        public void Despawn() { }
        bool active, hasTarget, initialized, suspended;
        string emote; float emoteStart, emoteLength; int lastEmoteBits;
        Quaternion emoteFacing;
        CapsuleCollider hazardTarget;
        Vector3 previousFeet, velocity;
        float previousPoseTime;
        string assignedId, displayName;
        int team = -1;
        Color assignedColor = UnityEngine.Color.white;
        bool identityAssigned;
        RaycastHit standingHit;
        float groundProbeAt=-1;
        public string ActorId => string.IsNullOrEmpty(assignedId) ? context.actorId : assignedId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;
        public int Team => team;
        public Color Color => identityAssigned ? assignedColor : rig&&rig.style?rig.style.ColorAt(color & 0xFF):UnityEngine.Color.white;
        public bool Alive => active && !suspended;
        public bool IsBot => IsEnemy||context.actorId!=null&&context.actorId.StartsWith("bot",System.StringComparison.OrdinalIgnoreCase);
        public bool IsLocal => false;
        public Transform Visual => rig ? rig.transform : null;
        public Pose ViewPose => initialized ? new Pose(frame.head,frame.headRotation) : new Pose(target.head,target.headRotation);
        public Pose HandPose(bool left)
        {
            var f=initialized?frame:target;var hand=left?f.left:f.right;
            return new Pose(hand.position,hand.rotation);
        }
        public Rigidbody StandingBody { get { ProbeStanding();return standingHit.collider&&Climbables.TryGet(standingHit.collider,out var owner)?owner.Body:standingHit.rigidbody; } }
        public Vector3 StandingNormal { get { ProbeStanding();return standingHit.collider?standingHit.normal:Vector3.zero; } }
        public bool Grounded { get { ProbeStanding();return standingHit.collider&&standingHit.normal.y>.25f; } }
        void ProbeStanding()
        {
            if(Time.time<groundProbeAt)return;groundProbeAt=Time.time+.1f;
            Physics.Raycast(target.feet+Vector3.up*.08f,Vector3.down,out standingHit,.2f,(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore);
        }
        public void SetIdentity(string id,string label,int assignedTeam,Color tint)
        { assignedId=id;displayName=label;team=assignedTeam;assignedColor=tint;identityAssigned=true; }
        public void ApplyModeRules(ModeRules rules) { }

        public Vector3 HazardVelocity => velocity;
        public float Health => (enemy?.healthRatio??1)*100;
        public float MaxHealth => 100;
        bool CanBeHit => context.host && !suspended && active && context.sendImpact != null && context.actorId != null;
        public void Push(Vector3 force, GameObject source) { if (CanBeHit) context.sendImpact(context.actorId, force, 0); }
        public void Damage(float amount, GameObject source) { if (CanBeHit) context.sendImpact(context.actorId, Vector3.zero, amount); }
        public void Eliminate(GameObject source) => Damage(10000, source);

        public SurvivorNetworkView Init(NetworkContext networkContext) { context = networkContext; return this; }

        public string NetworkKind => NetworkKinds.Survivor;
        public bool NetworkActive => active;
        public Vector3 NetworkPosition => target.feet;
        public void WriteState(BinaryWriter w) => CharacterWire.WriteSurvivor(w, target, color,appearance,this,null,blaster);

        public void ReadState(BinaryReader r, bool isActive)
        {
            CharacterWire.ReadSurvivor(r, out target, out color,out appearance,out enemy,out blaster);
            // A connected human cannot grant themselves NPC rules through their visual payload.
            if(context.host)enemy=null;
            if (active && !isActive && rig) Burst();
            active = isActive; hasTarget = true;if(!active)weaponVisual?.Hide();
            if(!active&&blasterModel)blasterModel.gameObject.SetActive(false);
            transform.SetPositionAndRotation(target.feet, Quaternion.Euler(0,target.headRotation.eulerAngles.y,0));
            int bits = (color >> 16) & 0xFF;
            if (!active || suspended || bits == 0 || bits > Emotes.Survivor.Length) emote = null;
            else if (bits != lastEmoteBits)
            {
                emote = Emotes.Survivor[bits - 1];
                emoteStart = Time.time;
                emoteLength = CandyEmotePoses.Loops(emote) ? float.PositiveInfinity : CandyEmotePoses.Duration(emote);
                emoteFacing = Quaternion.Euler(0, target.headRotation.eulerAngles.y, 0);
                GameEvents.RaiseEmoted(rig ? rig.gameObject : gameObject, emote);
            }
            lastEmoteBits = active && !suspended ? bits : 0;
            if (!rig)
            {
                var avatar = context.localSurvivor ? context.localSurvivor.GetComponent<CandyPlayerAvatar>() : null;
                if (!avatar || !avatar.visualPrefab) return;
                rig = Instantiate(avatar.visualPrefab, transform); rig.ownerCamera = null;
            }
            rig.gameObject.SetActive(active); rig.SetColor(color & 0xFF);rig.SetAppearance(appearance);
            if(IsEnemy&&(ArchetypeId=="longhand"||ArchetypeId=="gum-brute")&&!GetComponent<DeadlyJenga.CastleBots.CandyMayhemEnemyVisual>())gameObject.AddComponent<DeadlyJenga.CastleBots.CandyMayhemEnemyVisual>();
            PushSample(target);
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
            var wanted = Sampled();
            float t = 1 - Mathf.Exp(-30 * dt);
            frame.feet = Vector3.Lerp(frame.feet, wanted.feet, t); frame.head = Vector3.Lerp(frame.head, wanted.head, t);
            frame.headRotation = Quaternion.Slerp(frame.headRotation, wanted.headRotation, t); frame.scale = wanted.scale; frame.seated = wanted.seated;
            frame.left = Blend(frame.left, wanted.left, t); frame.right = Blend(frame.right, wanted.right, t);
            rig.ApplyFrame(frame, true, dt);
            var equipment=(MatchRuntime.Current as IEquipmentRuntime)?.EquipmentFor(ActorId)??new EquipmentState();
            bool held = equipment.drawn && equipment.kind >= EquipmentKind.Blaster && equipment.kind != EquipmentKind.PoweredArmor;
            bool twoHanded = held && EquipmentSpecifications.Get(equipment.kind).twoHanded;
            var rendered = frame;
            if (emote != null)
            {
                float age = Time.time - emoteStart;
                if (age >= emoteLength) emote = null;
                else
                {
                    rendered.headRotation = emoteFacing;
                    rendered = CandyEmotePoses.Pose(rendered, emote, age, emoteLength);
                    if (held && !twoHanded)
                    {
                        bool paired = equipment.kind is EquipmentKind.DualUzi or EquipmentKind.LaserClaws;
                        if (equipment.leftHanded || paired) SurvivorMoments.KeepGrip(ref rendered.left, frame.left);
                        if (!equipment.leftHanded || paired) SurvivorMoments.KeepGrip(ref rendered.right, frame.right);
                    }
                    rig.ApplyFrame(rendered, true, 0);
                }
            }
            if(IsEnemy)rig.SetTint(new Color(.035f,.045f,.065f));
            weaponVisual??=new CandyWeaponVisual(transform,rig.style?rig.style.material:null);
            weaponVisual.Present(equipment,rendered,active&&!suspended,emote!=null&&twoHanded);
        }

        public void Suspend() { emote = null; lastEmoteBits = 0; weaponVisual?.Hide();suspended = true; if (hazardTarget) hazardTarget.enabled = false; }

        // Someone else's survivor just disappeared (eliminated): let the effects burst it.
        void Burst()
        {
            var colors = rig.style ? rig.style.colors : null;
            var c = colors != null && colors.Length > 0 ? colors[(color & 0xFF) % colors.Length] : Color.white;
            GameEvents.RaiseBodyBurst(rig.gameObject, frame.feet, c, "A player", false);
            emote = null;
        }

        // Poses arrive unevenly: bots ride the world stream (a few per second), players the 30 Hz pose lane.
        // The view plays a short adaptive delay behind the newest pose and interpolates between buffered
        // samples, so movement stays continuous instead of catching up and stopping at each update.
        const int Samples = 8;
        readonly CandyAvatarFrame[] sampleFrames = new CandyAvatarFrame[Samples];
        readonly float[] sampleTimes = new float[Samples];
        int sampleCount;
        float sampleInterval = .1f, sampleJitter;
        void PushSample(CandyAvatarFrame pose)
        {
            float now = Time.unscaledTime;
            if (sampleCount > 0)
            {
                int last = sampleCount - 1;
                float gap = now - sampleTimes[last];
                if (gap < .001f) { sampleFrames[last] = pose; return; }
                if (gap > 1.5f || (pose.feet - sampleFrames[last].feet).sqrMagnitude > 9) sampleCount = 0;   // long silence or a teleport/respawn: start fresh
                else { sampleJitter = Mathf.Lerp(sampleJitter, Mathf.Abs(gap - sampleInterval), .2f); sampleInterval = Mathf.Lerp(sampleInterval, gap, .2f); }
            }
            if (sampleCount == Samples)
            {
                System.Array.Copy(sampleFrames, 1, sampleFrames, 0, Samples - 1);
                System.Array.Copy(sampleTimes, 1, sampleTimes, 0, Samples - 1);
                sampleCount--;
            }
            sampleFrames[sampleCount] = pose; sampleTimes[sampleCount] = now; sampleCount++;
            if (sampleCount == 1) frame = pose;
        }
        CandyAvatarFrame Sampled()
        {
            if (sampleCount == 0) return target;
            float delay = Mathf.Clamp(sampleInterval * 1.15f + sampleJitter * 2f, .04f, .4f);
            float at = Time.unscaledTime - delay;
            int i = sampleCount - 1;
            if (at >= sampleTimes[i]) return sampleFrames[i];
            while (i > 0 && sampleTimes[i - 1] > at) i--;
            if (i == 0) return sampleFrames[0];
            var a = sampleFrames[i - 1]; var b = sampleFrames[i];
            float u = Mathf.InverseLerp(sampleTimes[i - 1], sampleTimes[i], at);
            b.feet = Vector3.Lerp(a.feet, b.feet, u); b.head = Vector3.Lerp(a.head, b.head, u);
            b.headRotation = Quaternion.Slerp(a.headRotation, b.headRotation, u);
            b.left = Blend(a.left, b.left, u); b.right = Blend(a.right, b.right, u);
            return b;
        }

        static CandyHandPose Blend(CandyHandPose from, CandyHandPose to, float t)
        {
            to.position = Vector3.Lerp(from.position, to.position, t); to.rotation = Quaternion.Slerp(from.rotation, to.rotation, t); return to;
        }

        void OnDestroy() { if (rig) Destroy(rig.gameObject); }
    }
}
