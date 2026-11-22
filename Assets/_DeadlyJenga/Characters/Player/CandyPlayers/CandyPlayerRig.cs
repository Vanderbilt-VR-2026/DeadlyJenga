using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyPlayerRig : MonoBehaviour, IMysteryBodySource, IAvatarSurfacePalette
    {
        public CandyPlayerStyle style;
        public SkinnedMeshRenderer bodyRenderer,headRenderer;
        public Camera ownerCamera;
        // Retained for compatibility. Comfort mode does not change the avatar stance.
        public bool SeatedVisual {get;set;}
        public CandyAvatarCalibration calibration;

        internal static CandyAvatarFrame UprightTrackingPose(CandyAvatarFrame frame)
        {
            // The headset can change height independently of the avatar's standing proportions.
            frame.head.y = frame.feet.y + 1.6f * Mathf.Max(.1f, frame.scale);
            return frame;
        }
        static float LandingWeight(CastleSurvivor motor)
        {
            if (!motor || !motor.Grounded) return 0;
            float elapsed = Time.time - motor.LandedAt;
            const float absorb = .07f;
            float strength = Mathf.InverseLerp(2.25f, 10f, motor.LandingSpeed);
            float recover = Mathf.Lerp(.20f, .36f, strength);
            if (elapsed >= recover || elapsed < 0) return 0;
            float bend = elapsed < absorb
                ? Mathf.SmoothStep(0, 1, elapsed / absorb)
                : Mathf.SmoothStep(1, 0, (elapsed - absorb) / (recover - absorb));
            return bend * Mathf.Lerp(.18f, 1f, strength);
        }
        [SerializeField] Transform[] bones;
        [SerializeField] float[] lengths;
        MaterialPropertyBlock colorProperties;
        Material currentMaterial;
        Color currentColor;
        [SerializeField] bool hasTint;
        [SerializeField] Color tint;
        float currentSmoothness;
        int colorIndex=int.MinValue;
        Vector3 previousFeet, previousSupportFeet;
        Transform previousGaitSupport;
        bool gaitReady;
        float walkPhase, sprintBlend;
        Vector3 strideDirection = Vector3.forward;
        float locomotionSpeed;
        // Everything the walk cycle remembers between frames. Replay tools save and restore it,
        // so a recorded walk looks the same whether it is played, scrubbed or rendered.
        public struct GaitState
        {
            public Vector3 previousFeet, strideDirection;
            public float walkPhase, sprintBlend, locomotionSpeed;
            public bool ready;
        }
        public GaitState Gait
        {
            get => new GaitState { previousFeet = previousFeet, strideDirection = strideDirection, walkPhase = walkPhase,
                sprintBlend = sprintBlend, locomotionSpeed = locomotionSpeed, ready = gaitReady };
            set
            {
                previousFeet = value.previousFeet; previousGaitSupport = null; gaitReady = value.ready;
                strideDirection = value.strideDirection.sqrMagnitude > .0001f ? value.strideDirection : Vector3.forward;
                walkPhase = value.walkPhase; sprintBlend = value.sprintBlend; locomotionSpeed = value.locomotionSpeed;
            }
        }
        // One step of the walk cycle on its own, with no avatar and no side effects. Replay tools use it to
        // work out the walk state for every frame of a recording. ApplyFrame uses the same sum (AdvanceGait).
        public static GaitState StepGait(GaitState gait, CandyAvatarFrame frame, float deltaTime)
        {
            if (deltaTime <= 0f) return gait;
            Quaternion facing = Quaternion.Euler(0f, frame.headRotation.eulerAngles.y, 0f);   // the body faces where the head looks
            if (gait.strideDirection.sqrMagnitude < .0001f) gait.strideDirection = Vector3.forward;
            AdvanceGait(ref gait, frame, Vector3.zero, facing, null, frame.sprinting && !frame.airborne, deltaTime);
            return gait;
        }

        // The walk-cycle sum. carry = how far a moving floor moved the feet (it is not a step).
        // The stride direction is measured in facingSpace when given, otherwise in the facing rotation.
        static void AdvanceGait(ref GaitState gait, in CandyAvatarFrame frame, Vector3 carry, Quaternion facing,
            Transform facingSpace, bool mayRun, float deltaTime)
        {
            float scale = Mathf.Max(.1f, frame.scale);
            Vector3 displacement = frame.feet - gait.previousFeet;
            float teleportDistance = BodyTeleportDistance * scale;
            bool reset = !gait.ready || displacement.sqrMagnitude > teleportDistance * teleportDistance;
            Vector3 travel = reset ? Vector3.zero : Vector3.ProjectOnPlane(displacement - carry, Vector3.up);
            gait.previousFeet = frame.feet; gait.ready = true;
            if (reset) { gait.sprintBlend = 0f; gait.strideDirection = Vector3.forward; }
            gait.locomotionSpeed = travel.magnitude / (deltaTime * scale);
            float runTarget = mayRun ? Mathf.InverseLerp(.3f, 2.4f, gait.locomotionSpeed) : 0f;
            gait.sprintBlend = Mathf.MoveTowards(gait.sprintBlend, runTarget, deltaTime * 6f);
            if (travel.sqrMagnitude > .00001f)
            {
                Vector3 targetDirection = facingSpace ? facingSpace.InverseTransformDirection(travel.normalized) : Quaternion.Inverse(facing) * travel.normalized;
                // Interpolate the heading so opposite directions cannot cancel or stall.
                float oldHeading = Mathf.Atan2(gait.strideDirection.x, gait.strideDirection.z) * Mathf.Rad2Deg;
                float targetHeading = Mathf.Atan2(targetDirection.x, targetDirection.z) * Mathf.Rad2Deg;
                float heading = Mathf.LerpAngle(oldHeading, targetHeading, 1f - Mathf.Exp(-12f * deltaTime)) * Mathf.Deg2Rad;
                gait.strideDirection = new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading));
            }
            float cadence = Mathf.Lerp(6f, 11f, Mathf.InverseLerp(.12f, 4f, gait.locomotionSpeed)) + gait.sprintBlend * 3.8f;
            gait.walkPhase = frame.hasGaitPhase ? frame.gaitPhase : Mathf.Repeat(gait.walkPhase + cadence * deltaTime, 2f * Mathf.PI);
        }
        public Transform LeftPalm=>bones[4];
        public Transform HeadAnchor=>bones[1];
        public Transform BodyAnchor=>bones[0];
        CandyAvatarAttachments attachments;
        Vector3 wardrobeHeadScale = Vector3.one;
        internal void SetWardrobeHeadScale(Vector3 scale)
        {
            wardrobeHeadScale = scale;
            HeadAnchor.localScale = scale * (calibration?.headSize ?? 1f);
        }
        public void SetAppearance(AvatarAppearance appearance)
        {
            if(!attachments)attachments=GetComponent<CandyAvatarAttachments>();
            if(!attachments)attachments=gameObject.AddComponent<CandyAvatarAttachments>();
            attachments.Apply(this,appearance);
        }
        public void SetTint(Color color)
        {
            hasTint=true;tint=color;
            if(currentColor==color)return;
            colorProperties??=new MaterialPropertyBlock();colorProperties.SetColor("_BaseColor",color);colorProperties.SetFloat("_Smoothness",style?style.smoothness:.78f);
            if (attachments?.UsesApprovedSkin != true)
            {
                bodyRenderer.SetPropertyBlock(colorProperties);
                headRenderer.SetPropertyBlock(colorProperties);
            }
            currentColor=color;
            if(attachments)attachments.Tint(this);
        }
        public Transform RightPalm=>bones[7];
        public Transform AppearanceBone(int index)=>bones[index];
        public Color BaseAppearanceColor=>hasTint?tint:style?style.ColorAt(colorIndex):Color.white;
        public void ApplyBodyPalette(Color color){colorProperties??=new MaterialPropertyBlock();bodyRenderer.GetPropertyBlock(colorProperties);colorProperties.SetColor("_BaseColor",color);bodyRenderer.SetPropertyBlock(colorProperties);headRenderer.SetPropertyBlock(colorProperties);}
        public int ColorIndex=>colorIndex;
        public int BoneCount=>bones?.Length??0;
        public IMysteryBody CreateMysteryBody()
        {
            var copy=Instantiate(this);copy.name="Candy mystery body";copy.ownerCamera=null;copy.enabled=false;copy.SnapSpineToTorso();
            copy.bodyRenderer.enabled=copy.headRenderer.enabled=true;copy.bodyRenderer.SetPropertyBlock(colorProperties);copy.headRenderer.SetPropertyBlock(colorProperties);
            var copiedAttachments=copy.GetComponent<CandyAvatarAttachments>();if(copiedAttachments){copiedAttachments.Tint(copy);if((copiedAttachments.Appearance.accessories&CandyHeadgearMeshes.SculptedHeads)!=0)copy.headRenderer.enabled=false;}
            var corpse=copy.gameObject.AddComponent<CandyMysteryBody>();corpse.Initialize(copy.bones,copy.lengths);copy.gameObject.SetActive(true);return corpse;
        }
        void Awake()
        {
            if(!bodyRenderer||!bodyRenderer.sharedMesh)BuildModel();
            if(bodyRenderer)
            {
                var mesh=bodyRenderer.sharedMesh;
                if(Application.isPlaying)mesh=CandySmoothHandMesh.SeparateDigitWeights(mesh,14,10);
                bodyRenderer.sharedMesh=BlendWristMesh(mesh);
                EnsureSpine();
            }
            if(Application.isPlaying&&!GetComponent<CandyAvatarDetail>())gameObject.AddComponent<CandyAvatarDetail>();
        }
        void OnEnable(){RenderPipelineManager.beginCameraRendering+=BeforeCamera;RenderPipelineManager.endCameraRendering+=AfterCamera;}
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering-=BeforeCamera;
            RenderPipelineManager.endCameraRendering-=AfterCamera;
            gaitReady=false;
            previousGaitSupport=null;
            if(headRenderer)headRenderer.shadowCastingMode=ShadowCastingMode.On;
            RestoreFirstPersonBody();
            if(attachments)attachments.FirstPerson(this,false);
        }
        bool firstPersonOverride;
        void BeforeCamera(ScriptableRenderContext context,Camera camera)
        {
            var spectator=Services.Session?.Demo as ISpectatorControls;
            var watched=spectator?.WatchedRoot(camera);
            bool firstPerson=camera==ownerCamera||(watched&&transform.IsChildOf(watched));
            if(!firstPerson&&!firstPersonOverride)return;
            firstPersonOverride=firstPerson;
            if(headRenderer)headRenderer.shadowCastingMode=firstPerson?ShadowCastingMode.ShadowsOnly:ShadowCastingMode.On;
            SetFirstPersonBody(firstPerson,camera.transform.forward.y<-.91f);
            if(attachments)attachments.FirstPerson(this,firstPerson);
        }
        void AfterCamera(ScriptableRenderContext context,Camera camera){if(!firstPersonOverride)return;firstPersonOverride=false;RestoreFirstPersonBody();if(headRenderer)headRenderer.shadowCastingMode=ShadowCastingMode.On;if(attachments)attachments.FirstPerson(this,false);}
        public void SetColor(int index)
        {
            if(!style)return;var color=hasTint?tint:style.ColorAt(index);
            if(index==colorIndex&&currentMaterial==style.material&&currentColor==color&&currentSmoothness==style.smoothness)return;
            colorProperties??=new MaterialPropertyBlock();
            colorProperties.SetColor("_BaseColor",color);colorProperties.SetFloat("_Smoothness",style.smoothness);
            if (attachments?.UsesApprovedSkin != true)
            {
                bodyRenderer.sharedMaterial = style.material;
                headRenderer.sharedMaterial = style.material;
                bodyRenderer.SetPropertyBlock(colorProperties);
                headRenderer.SetPropertyBlock(colorProperties);
            }
            colorIndex=index;currentMaterial=style.material;currentColor=color;currentSmoothness=style.smoothness;
            if(attachments)attachments.Tint(this);
        }
        bool composingPose,pendingPose,pendingGrounded;
        float pendingDelta;
        CandyAvatarFrame pendingFrame;
        public void BeginPoseComposition(){composingPose=true;pendingPose=false;pendingDelta=0;}
        public void EndPoseComposition()
        {
            composingPose=false;
            if(!pendingPose)return;
            pendingPose=false;ApplyFrame(pendingFrame,pendingGrounded,pendingDelta);
        }
        static readonly Unity.Profiling.ProfilerMarker AvatarPoseMarker = new("DJ.Avatar.Pose");
        public void ApplyFrame(CandyAvatarFrame frame,bool grounded=true,float deltaTime=0)
        {
            if(composingPose){pendingFrame=frame;pendingGrounded=grounded;pendingDelta+=deltaTime;pendingPose=true;return;}
            using var measurement = AvatarPoseMarker.Auto();
            if(!bodyRenderer)BuildModel();
            if(colorIndex!=int.MinValue)SetColor(colorIndex);
            float scale=Mathf.Max(.1f,frame.scale);
            var actor=GetComponentInParent<IModeActor>();bool downed=actor!=null&&MatchRuntime.Current?.RulesFor(actor.gameObject).downed==true;
            if(downed)
            {
                frame.head=frame.feet+Vector3.up*.62f*scale+frame.headRotation*Vector3.forward*.3f*scale;
                frame.left.position=frame.feet+transform.forward*.5f*scale-transform.right*.23f*scale+Vector3.up*.12f*scale;
                frame.right.position=frame.feet+transform.forward*.5f*scale+transform.right*.23f*scale+Vector3.up*.12f*scale;
            }
            var fit=calibration;
            // Tracking and equipment already provide the shared hand anchors used by weapons and remote views.
            Vector3 forward=Vector3.ProjectOnPlane(frame.headRotation*Vector3.forward,Vector3.up).normalized;
            float bodyYaw=UpdateBodyYaw(frame,forward);
            transform.SetPositionAndRotation(frame.feet,Quaternion.Euler(0f,bodyYaw,0f));var parentScale=transform.parent?transform.parent.lossyScale:Vector3.one;
            transform.localScale=new Vector3(scale/Mathf.Max(.001f,Mathf.Abs(parentScale.x)),scale/Mathf.Max(.001f,Mathf.Abs(parentScale.y)),scale/Mathf.Max(.001f,Mathf.Abs(parentScale.z)));
            var motor = actor as CastleSurvivor;
            float dance = downed ? 0 : frame.danceWeight;
            float headY=transform.InverseTransformPoint(frame.head).y;
            float crouch=Mathf.Clamp(headY/1.6f,.4f,1.25f)*(fit?.bodyHeight??1f);
            float width=fit?.shoulderWidth??1f;
            Vector3 torsoOffset=fit?.bodyOffset??Vector3.zero;
            if(deltaTime>0f)
            {
                float teleportDistance = BodyTeleportDistance * scale;
                Transform support = grounded && motor && motor.StandingSurface ? motor.StandingSurface.transform : null;
                Vector3 carry = Vector3.zero;
                if (gaitReady && support && support == previousGaitSupport)
                {
                    // The floor can carry a planted survivor without taking a step.
                    carry = support.TransformPoint(previousSupportFeet) - previousFeet;
                    if (carry.sqrMagnitude >= teleportDistance * teleportDistance) carry = Vector3.zero;
                }
                previousGaitSupport = support;
                if (support) previousSupportFeet = support.InverseTransformPoint(frame.feet);
                // The body turns with the rig itself, so the stride is measured in the rig's own space.
                var gait = Gait;
                AdvanceGait(ref gait, frame, carry, Quaternion.identity, transform, frame.sprinting && grounded && !downed && dance <= .01f, deltaTime);
                previousFeet = gait.previousFeet; gaitReady = gait.ready; strideDirection = gait.strideDirection;
                walkPhase = gait.walkPhase; sprintBlend = gait.sprintBlend; locomotionSpeed = gait.locomotionSpeed;
            }
            float run = grounded && !downed && dance <= .01f ? sprintBlend : 0f;
            float landing = grounded && !downed && dance <= .01f ? LandingWeight(motor) : 0f;
            float counterSwing = Mathf.Cos(walkPhase);
            torsoOffset.y -= run * (.038f - .017f * Mathf.Abs(counterSwing));
            float stride=grounded?Mathf.Clamp01(locomotionSpeed/2)*.13f:0;
            float dropTarget=PlanFeet(grounded&&!downed&&dance<=.01f,scale,stride,run);
            groundDrop=Application.isPlaying?Mathf.Lerp(groundDrop,dropTarget,1f-Mathf.Exp(-14f*Time.deltaTime)):dropTarget;
            torsoOffset.y+=groundDrop/scale;
            torsoOffset.y -= landing * .16f;
            torsoOffset.z -= landing * .055f;
            PoseTorso(frame, torsoOffset, crouch, width, run, dance, grounded && !downed, landing);
            var headOffset=frame.headRotation*((style?style.headCenterOffset:new Vector3(0,0,-.08f))+(fit?.headOffset??Vector3.zero))+Vector3.down*.14f;
            bones[1].SetPositionAndRotation(frame.head+headOffset*scale,frame.headRotation);
            bones[1].localScale=wardrobeHeadScale*(fit?.headSize??1f);
            if(attachments)attachments.RefreshPalette(this);
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1;
                Vector3 hip=transform.TransformPoint(pelvisPosition+pelvisRotation*new Vector3(sign*.105f*width,.67f*crouch,0));
                Vector3 footLocal = feetLocal[side];
                float footPitch = feetPitch[side];
                Vector3 foot=transform.TransformPoint(footLocal);
                if(!grounded)foot=transform.TransformPoint(new Vector3(sign*.13f,.13f,.015f));
                if (dance > 0)
                    foot = Vector3.Lerp(foot, transform.TransformPoint(side == 0 ? frame.danceLeftFoot : frame.danceRightFoot), dance);
                if (feetWeight[side] > 0f)
                    foot = Vector3.Lerp(foot, feetTarget[side], feetWeight[side]);
                int thigh=side==0?8:11;
                Vector3 knee=SolveJoint(hip,foot,.31f*scale,.31f*scale,transform.forward+Vector3.down*.1f);
                Segment(thigh,hip,knee);Segment(thigh+1,knee,foot);
                bones[thigh+2].SetPositionAndRotation(foot,feetTilt[side]*transform.rotation*Quaternion.Euler(footPitch,0f,0f));bones[thigh+2].localScale=Vector3.one;
                var hand=side==0?frame.left:frame.right;
                Quaternion torsoRotation=chestRotation;
                Vector3 handLocal=Quaternion.Inverse(torsoRotation)*(transform.InverseTransformPoint(hand.position)-chestPosition);
                float across=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.12f*width,.18f*width,-sign*handLocal.x));
                float ahead=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.03f,.25f,handLocal.z));
                // The shoulder rolls forward on a reach across the chest. The tracked wrist stays fixed.
                Vector3 shoulder=transform.TransformPoint(chestPosition+torsoRotation*new Vector3(sign*.21f*width,1.255f*crouch,.12f*across*ahead));
                // Seat the forearm inside the back of the palm, without moving the tracked grip.
                Vector3 wrist=hand.position-hand.rotation*Vector3.forward*(.022f*scale);
                Vector3 elbowPole=ElbowPole(wrist-shoulder,transform.TransformDirection(torsoRotation*(Vector3.right*sign)),transform.TransformDirection(torsoRotation*Vector3.down));
                Vector3 elbow=SolveJoint(shoulder,wrist,.29f*scale,.27f*scale,elbowPole);
                int upper=side==0?2:5;
                Segment(upper,shoulder,elbow);
                // Carry the arm frame through the bend. Hand roll must not wring the elbow shut.
                var bendRotation=Quaternion.FromToRotation(elbow-shoulder,wrist-elbow);
                Segment(upper+1,elbow,wrist,bendRotation*bones[upper].forward);
                bones[upper+2].SetPositionAndRotation(hand.position,hand.rotation);bones[upper+2].localScale=Vector3.one;
                PoseFingers(side, hand, scale);
            }
            bodyRenderer.localBounds=new Bounds(new Vector3(0,1,0),new Vector3(6,5,6));
            if(downed){bones[0].localRotation=Quaternion.Euler(65,0,0);bones[0].localPosition=new Vector3(0,.05f,.28f);PoseSpineRigid();}
        }
        // The body turns after the head: it waits while the head stays within HeadYawLead degrees,
        // then turns at BodyTurnSpeed. Once the head has been still for a moment, the body catches up.
        const float HeadYawLead=35f,BodyTurnSpeed=180f,BodySettleSpeed=90f,BodySettleDelay=.6f,BodyTeleportDistance=1.5f;
        float bodyYaw,lastHeadYaw,settleTime;
        bool bodyYawReady;
        int bodyYawFrame=-1;
        Vector3 bodyYawFeet;
        float UpdateBodyYaw(CandyAvatarFrame frame,Vector3 forward)
        {
            float headYaw=forward.sqrMagnitude<.01f?bodyYaw:Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
            // Teleports and recentering snap the body so it never swings through the old direction.
            if(!bodyYawReady||Vector3.Distance(frame.feet,bodyYawFeet)>BodyTeleportDistance*Mathf.Max(.1f,frame.scale))
            {
                bodyYaw=lastHeadYaw=headYaw;settleTime=0;bodyYawReady=true;
            }
            bodyYawFeet=frame.feet;
            // Several pose calls can run in one frame; only the first one moves the body.
            if(bodyYawFrame==Time.frameCount)return bodyYaw;
            bodyYawFrame=Time.frameCount;
            float dt=Mathf.Min(Time.deltaTime,.05f);
            settleTime=Mathf.Abs(Mathf.DeltaAngle(lastHeadYaw,headYaw))>.5f?0f:settleTime+dt;
            lastHeadYaw=headYaw;
            float lead=Mathf.DeltaAngle(bodyYaw,headYaw);
            float target=headYaw-Mathf.Clamp(lead,-HeadYawLead,HeadYawLead);
            float speed=BodyTurnSpeed;
            if(settleTime>BodySettleDelay){target=headYaw;speed=BodySettleSpeed;}
            bodyYaw=Mathf.MoveTowardsAngle(bodyYaw,target,speed*dt);
            return bodyYaw;
        }
        static Quaternion Orientation(Vector3 delta,Vector3 normal)
        {
            if(delta.sqrMagnitude<.0000001f)return Quaternion.identity;
            var up=delta.normalized;var forward=Vector3.ProjectOnPlane(normal,up).normalized;
            if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(Vector3.up,up).normalized;
            if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(Vector3.right,up).normalized;
            return Quaternion.LookRotation(forward,up);
        }
        void Segment(int index,Vector3 a,Vector3 b,Vector3? normal=null)
        {
            Vector3 delta=b-a;
            bones[index].SetPositionAndRotation(a,delta.sqrMagnitude>.0000001f?Orientation(delta,normal??transform.forward):transform.rotation);
            float scale=Mathf.Max(.001f,transform.lossyScale.y);
            bones[index].localScale=new Vector3(1,Mathf.Max(.001f,delta.magnitude)/(Mathf.Max(.001f,lengths[index])*scale),1);
        }
        // The elbow points down and out from the torso. It stays that way when the hand is high or low.
        // If the reach lines up with the pole, fall back to other body directions so the elbow never flips.
        static Vector3 ElbowPole(Vector3 reach,Vector3 outward,Vector3 down)
        {
            Vector3 direction=reach.normalized;
            Vector3 pole=Vector3.ProjectOnPlane(outward+down*.8f,direction);
            if(pole.sqrMagnitude<.04f)pole=Vector3.ProjectOnPlane(down,direction);
            if(pole.sqrMagnitude<.04f)pole=Vector3.ProjectOnPlane(outward,direction);
            return pole.normalized;
        }
        static Vector3 SolveJoint(Vector3 a,Vector3 b,float l1,float l2,Vector3 pole)
        {
            Vector3 delta=b-a;float distance=Mathf.Max(.0001f,delta.magnitude);Vector3 direction=delta/distance;
            // A hand almost on the shoulder would fold the arm into a wire. Keep a small minimum reach.
            distance=Mathf.Max(distance,Mathf.Abs(l1-l2)+.02f);
            float stretch=Mathf.Max(1,distance/(l1+l2)*1.001f);l1*=stretch;l2*=stretch;
            float along=Mathf.Clamp((l1*l1-l2*l2+distance*distance)/(2*distance),0,l1);
            float bend=Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
            Vector3 perpendicular=Vector3.ProjectOnPlane(pole,direction).normalized;
            if(perpendicular.sqrMagnitude<.01f)perpendicular=Vector3.Cross(direction,Vector3.right).normalized;
            return a+direction*along+perpendicular*bend;
        }
        public void BuildModel()
        {
            bones=new Transform[44];lengths=new float[44];
            Transform Bone(int index,string name,Vector3 start,Vector3 end,Vector3? normal=null)
            {
                var bone=new GameObject(name).transform;bone.SetParent(transform,false);bone.localPosition=start;
                Vector3 delta=end-start;bone.localRotation=delta.sqrMagnitude>.000001f?Orientation(delta,normal??Vector3.forward):Quaternion.identity;
                lengths[index]=Mathf.Max(.001f,delta.magnitude);bones[index]=bone;return bone;
            }
            Bone(0,"Torso",Vector3.zero,Vector3.zero);Bone(1,"Head",new Vector3(0,1.49f,0),new Vector3(0,1.49f,0));
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1;string label=side==0?"Left":"Right";int upper=side==0?2:5,thigh=side==0?8:11;
                var shoulder=new Vector3(sign*.21f,1.255f,0);var elbow=new Vector3(sign*.29f,.99f,.035f);var wrist=new Vector3(sign*.3f,.75f,.09f);
                Bone(upper,label+" upper arm",shoulder,elbow);Bone(upper+1,label+" forearm",elbow,wrist);Bone(upper+2,label+" palm",wrist,wrist);
                var hip=new Vector3(sign*.105f,.67f,0);var knee=new Vector3(sign*.115f,.37f,.018f);var ankle=new Vector3(sign*.12f,.075f,.025f);
                Bone(thigh,label+" thigh",hip,knee);Bone(thigh+1,label+" shin",knee,ankle);Bone(thigh+2,label+" foot",ankle,ankle);
                var rest=CandyHandSkeleton.Rest(side==0);
                for(int finger=0;finger<5;finger++)for(int joint=0;joint<3;joint++)
                    Bone(14+side*15+finger*3+joint,label+" finger "+finger+" segment "+joint,wrist+rest.Joint(finger*4+joint),wrist+rest.Joint(finger*4+joint+1),Vector3.up);
            }
            var body=new SkinBuilder(transform,bones);var head=new SkinBuilder(transform,bones);
            body.Torso(0);head.Sphere(1,Vector3.zero,new Vector3(.22f,.22f,.215f),24,40);
            for(int side=0;side<2;side++)
            {
                int upper=side==0?2:5,thigh=side==0?8:11;
                body.Limb(new[]{upper,upper+1,upper+2},new[]{.067f,.051f,.035f});
                body.Append(CandySmoothHandMesh.Build(transform,bones,side));
                body.Limb(new[]{thigh,thigh+1,thigh+2},new[]{.083f,.066f,.055f});
                body.Sphere(thigh+2,new Vector3(0,0,.04f),new Vector3(.092f,.075f,.155f),14,24);
            }
            SkinnedMeshRenderer Renderer(string name,Mesh mesh)
            {
                var go=new GameObject(name);go.transform.SetParent(transform,false);var renderer=go.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh=mesh;renderer.bones=bones;renderer.rootBone=transform;renderer.updateWhenOffscreen=true;renderer.sharedMaterial=style?style.material:null;return renderer;
            }
            bodyRenderer=Renderer("Candy body and shared hands",body.Mesh("Candy body"));headRenderer=Renderer("Candy spherical head",head.Mesh("Candy head"));
        }
        sealed class SkinBuilder
        {
            readonly Transform root;readonly Transform[] bones;
            readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();readonly List<BoneWeight> weights=new List<BoneWeight>();readonly List<int> triangles=new List<int>();
            public SkinBuilder(Transform root,Transform[] bones){this.root=root;this.bones=bones;}
            void Vertex(int bone,Vector3 point,Vector3 normal,Vector2 texture)
            {
                Matrix4x4 m=root.worldToLocalMatrix*bones[bone].localToWorldMatrix;
                vertices.Add(m.MultiplyPoint3x4(point));normals.Add(m.MultiplyVector(normal).normalized);uv.Add(texture);
                weights.Add(new BoneWeight{boneIndex0=bone,weight0=1});
            }
            void Grid(int start,int rings,int sides)
            {
                for(int y=0;y<rings;y++)for(int x=0;x<sides;x++)
                {int a=start+y*(sides+1)+x,b=a+1,c=a+sides+1,d=c+1;triangles.Add(a);triangles.Add(c);triangles.Add(b);triangles.Add(b);triangles.Add(c);triangles.Add(d);}
            }
            public void Sphere(int bone,Vector3 center,Vector3 radii,int rings,int sides)
            {
                int start=vertices.Count;
                for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++)
                {
                    float theta=Mathf.PI*y/rings,angle=2*Mathf.PI*x/sides;
                    Vector3 n=new Vector3(Mathf.Sin(theta)*Mathf.Cos(angle),-Mathf.Cos(theta),Mathf.Sin(theta)*Mathf.Sin(angle));
                    Vertex(bone,center+Vector3.Scale(n,radii),new Vector3(n.x/radii.x,n.y/radii.y,n.z/radii.z).normalized,new Vector2((float)x/sides,(float)y/rings));
                }
                Grid(start,rings,sides);
            }
            public void Capsule(int bone,float length,float radius,int rings=12,int sides=20)
            {
                int start=vertices.Count;
                for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++)
                {
                    float theta=Mathf.PI*y/rings,angle=2*Mathf.PI*x/sides;
                    Vector3 n=new Vector3(Mathf.Sin(theta)*Mathf.Cos(angle),-Mathf.Cos(theta),Mathf.Sin(theta)*Mathf.Sin(angle));
                    Vector3 point=n*radius+Vector3.up*(y<=rings/2?0:length);
                    Vertex(bone,point,n,new Vector2((float)x/sides,(float)y/rings));
                }
                Grid(start,rings,sides);
            }
            public void Append(Mesh mesh)
            {
                int start=vertices.Count;vertices.AddRange(mesh.vertices);normals.AddRange(mesh.normals);uv.AddRange(mesh.uv);weights.AddRange(mesh.boneWeights);
                foreach(int index in mesh.triangles)triangles.Add(start+index);
                if(Application.isPlaying)Object.Destroy(mesh);else Object.DestroyImmediate(mesh);
            }
            public void Limb(int[] chain,float[] radii)
            {
                const int rings=24,sides=24;int start=vertices.Count;
                var points=new Vector3[chain.Length];for(int i=0;i<chain.Length;i++)points[i]=root.InverseTransformPoint(bones[chain[i]].position);
                for(int ring=0;ring<=rings;ring++)
                {
                    float t=(float)ring/rings*(chain.Length-1);int segment=Mathf.Min(chain.Length-2,(int)t);float f=t-segment;
                    Vector3 a=points[segment],b=points[segment+1],tangent=(b-a).normalized;
                    if(segment>0&&f<.25f)tangent=Vector3.Lerp((points[segment+1]-points[segment-1]).normalized,tangent,f/.25f).normalized;
                    if(segment<chain.Length-2&&f>.75f)tangent=Vector3.Lerp(tangent,(points[segment+2]-points[segment]).normalized,(f-.75f)/.25f).normalized;
                    Vector3 axis=Vector3.Cross(tangent,Vector3.forward).normalized;if(axis.sqrMagnitude<.1f)axis=Vector3.Cross(tangent,Vector3.right).normalized;
                    Vector3 other=Vector3.Cross(tangent,axis).normalized;
                    float radius=Mathf.Lerp(radii[segment],radii[segment+1],f);
                    radius*=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.12f))*Mathf.SmoothStep(0,1,Mathf.Clamp01((chain.Length-1-t)/.08f));
                    Vector3 center=Vector3.Lerp(a,b,f);
                    // Blending across the joint keeps one continuous tube instead of capsule end caps.
                    float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,f));
                    if(segment>0&&f<.35f)blend=Mathf.SmoothStep(.5f,0,f/.35f);
                    int first=segment>0&&f<.35f?chain[segment-1]:chain[segment];
                    int second=segment>0&&f<.35f?chain[segment]:chain[segment+1];
                    float secondWeight=segment>0&&f<.35f?1-blend:blend*.5f;
                    if(ring==rings){first=chain[chain.Length-1];second=first;secondWeight=0;}
                    for(int x=0;x<=sides;x++)
                    {
                        float angle=2*Mathf.PI*x/sides;Vector3 n=axis*Mathf.Cos(angle)+other*Mathf.Sin(angle);
                        vertices.Add(center+n*radius);normals.Add(n);uv.Add(new Vector2((float)x/sides,(float)ring/rings));
                        weights.Add(new BoneWeight{boneIndex0=first,weight0=1-secondWeight,boneIndex1=second,weight1=secondWeight});
                    }
                }
                // Tube parameterization has the opposite angular orientation to the lathe.
                int indexStart=triangles.Count;Grid(start,rings,sides);
                for(int i=indexStart;i<triangles.Count;i+=3){int swap=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=swap;}
            }
            public void Torso(int bone)
            {
                float[] ys={.53f,.58f,.66f,.79f,.94f,1.08f,1.2f,1.29f,1.34f};
                float[] rs={0,.12f,.18f,.17f,.155f,.2f,.21f,.16f,0};
                const int rings=48,sides=40;int start=vertices.Count;
                float Radius(float t)
                {
                    float f=Mathf.Clamp(t,0,1)*(rs.Length-1);int i=Mathf.Min(rs.Length-2,(int)f);float u=f-i;
                    float p0=rs[Mathf.Max(0,i-1)],p1=rs[i],p2=rs[i+1],p3=rs[Mathf.Min(rs.Length-1,i+2)];
                    return Mathf.Max(0,.5f*((2*p1)+(-p0+p2)*u+(2*p0-5*p1+4*p2-p3)*u*u+(-p0+3*p1-3*p2+p3)*u*u*u));
                }
                for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++)
                {
                    float t=(float)y/rings,a=2*Mathf.PI*x/sides,f=t*(ys.Length-1);int i=Mathf.Min(ys.Length-2,(int)f);
                    float height=Mathf.Lerp(ys[i],ys[i+1],f-i),r=Radius(t),slope=(Radius(t+.005f)-Radius(t-.005f))/.0081f;
                    Vector3 point=new Vector3(Mathf.Cos(a)*r,height,Mathf.Sin(a)*r*.72f);
                    Vertex(bone,point,new Vector3(Mathf.Cos(a),-slope,Mathf.Sin(a)/.72f).normalized,new Vector2((float)x/sides,t));
                }
                Grid(start,rings,sides);
            }
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.boneWeights=weights.ToArray();
                var bindings=new Matrix4x4[bones.Length];for(int i=0;i<bones.Length;i++)bindings[i]=bones[i].worldToLocalMatrix*root.localToWorldMatrix;
                mesh.bindposes=bindings;mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
