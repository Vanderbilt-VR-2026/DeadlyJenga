using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CastleBots;
using DeadlyJenga.Core;
using DeadlyJenga.CharacterNetwork;
using System.IO;

namespace DeadlyJenga.CandyPlayers
{
    [DefaultExecutionOrder(100)]
    public sealed partial class CandyPlayerAvatar : MonoBehaviour, INetworkActor
    {
        public CandyPlayerRig visualPrefab;
        public CastleSurvivor survivor;
        public Camera viewCamera;
        public Transform trackingOrigin;
        public CandyTrackingSource tracking;
        public CandyPlayerRig Rig{get;private set;}
        public CandyAvatarFrame LastFrame{get;private set;}
        internal CandyAvatarFrame RenderedFrame { get; private set; }
        public bool remoteDriven;
        public int ColorIndex{get;private set;}
        NearFarInteractor[] nearRays;
        XRRayInteractor[] farRays;
        bool overridePose,wasVaulting;
        float handStride, handSwing, handSprintBlend;
        int handMotionFrame = -1;
        CandyAvatarFrame overridden;
        CandyHandPose vaultStartLeft,vaultStartRight;
        bool wasClimbing;
        float climbRelease=1;
        CandyHandPose climbStartLeft,climbStartRight,climbLastLeft,climbLastRight;
        void Awake()
        {
            if(!survivor)survivor=GetComponent<CastleSurvivor>();
            var origin=GetComponent<XROrigin>();
            if(!viewCamera)viewCamera=survivor?survivor.viewCamera:origin?origin.Camera:null;
            if(!trackingOrigin)trackingOrigin=survivor?survivor.cameraOffset:origin&&origin.CameraFloorOffsetObject?origin.CameraFloorOffsetObject.transform:transform;
            if(!visualPrefab)visualPrefab=Resources.Load<CandyPlayerRig>("CandyPlayerVisual");
            if(!visualPrefab){enabled=false;Debug.LogError("Build candy player assets first.",this);return;}
            Rig=Instantiate(visualPrefab,transform);Rig.name="Candy player body (shared views)";Rig.ownerCamera=remoteDriven?null:viewCamera;
            if(!GetComponent<CandyEquipment>())gameObject.AddComponent<CandyEquipment>();
            ColorIndex=Random.Range(0,Rig.style.colors.Length);Rig.SetColor(ColorIndex);LoadAppearance();
            if(!remoteDriven)
            {
                if(survivor&&!survivor.externallyDriven&&!GetComponent<CandyBlaster>())gameObject.AddComponent<CandyBlaster>();
                tracking=GetComponent<CandyTrackingSource>();if(!tracking)tracking=gameObject.AddComponent<CandyTrackingSource>();
                tracking.trackingOrigin=trackingOrigin;tracking.style=Rig.style;
                nearRays=GetComponentsInChildren<NearFarInteractor>(true);
                farRays=GetComponentsInChildren<XRRayInteractor>(true);
            }
        }
        void OnEnable(){Application.onBeforeRender+=BeforeRender;if(survivor)survivor.PunchRequested+=BeginPunch;if(!remoteDriven)NetworkActors.Local.Add(this);}
        void OnDisable(){Application.onBeforeRender-=BeforeRender;if(survivor)survivor.PunchRequested-=BeginPunch;ResetPunch();wasVaulting=false;NetworkActors.Local.Remove(this);}
        // Emotes and the elimination burst live on SurvivorMoments (looks come from the avatar appearance system).
        void Start(){if(!GetComponent<SurvivorMoments>())gameObject.AddComponent<SurvivorMoments>();}
        SurvivorMoments moments;
        SurvivorMoments Moments=>moments?moments:moments=GetComponent<SurvivorMoments>();

        // INetworkActor: what other players see of this survivor (the player's or a bot's).
        CastleBotBrain bot;
        public string NetworkKind=>NetworkKinds.Survivor;
        public bool NetworkActive{get{if(!bot)bot=GetComponent<CastleBotBrain>();return !(bot&&bot.Respawning);}}
        public Vector3 NetworkPosition=>transform.position;
        // Third byte of the synced color value: the emote showing right now (index + 1, or 0).
        public void WriteState(BinaryWriter w)=>CharacterWire.WriteSurvivor(w,survivor&&survivor.externallyDriven&&LastFrame.scale>0?LastFrame:ReadFrame(),(ColorIndex&0xFF)|(Moments?Moments.NetworkBits<<16:0),EquippedAppearance,GetComponent<IEnemyActor>(),GetComponent<IBlasterControls>());
        void LateUpdate(){if(!remoteDriven)Present(Time.deltaTime);}
        [BeforeRenderOrder(100)] void BeforeRender(){if(!remoteDriven)Present(0);}
        public void SetColor(int index){ColorIndex=index;if(Rig)Rig.SetColor(index);}
        public void SetExactColor(Color color)
        {
            if(!Rig||!Rig.style)return;
            float distance=float.PositiveInfinity;int best=0;
            for(int i=0;i<Rig.style.colors.Length;i++)
            {
                var c=Rig.style.colors[i];float d=(new Vector3(c.r,c.g,c.b)-new Vector3(color.r,color.g,color.b)).sqrMagnitude;
                if(d<distance){distance=d;best=i;}
            }
            SetColor(best);
        }
        public void OverridePose(CandyAvatarFrame frame){overridden=frame;overridePose=true;}
        public void ClearPoseOverride(){overridePose=false;}
        public CandyAvatarFrame ReadFrame()
        {
            if (Showcasing) return ReadShowcaseFrame();
            if(overridePose){var posed=overridden;ApplyPunch(ref posed);GetComponent<CandyEquipment>()?.PoseHands(ref posed);return posed;}
            float scale=survivor?survivor.playerScale:trackingOrigin?Mathf.Abs(trackingOrigin.lossyScale.y):1;
            scale=Mathf.Max(.1f,scale);
            Transform head=viewCamera?viewCamera.transform:transform;
            var frame=new CandyAvatarFrame{feet=transform.position,head=head.position,headRotation=head.rotation,scale=scale,seated=false};
            if(!viewCamera){frame.head=frame.feet+Vector3.up*(1.6f*scale);frame.headRotation=transform.rotation;}
            // Room-scale body placement follows the head's horizontal location; locomotion feet supply height.
            frame.feet=new Vector3(frame.head.x,transform.position.y,frame.head.z);
            // Keep the torso directly beneath the tracked head, including room-scale steps.
            bool vr=survivor?survivor.IsVR:tracking&&tracking.HasTrackedHands;
            var left=vr&&tracking?tracking.WorldHand(true):default;
            var right=vr&&tracking?tracking.WorldHand(false):default;
            bool animateHands = survivor && !survivor.externallyDriven;
            if (animateHands)
            {
                UpdateHandMotion();
                frame.sprinting = survivor.Sprinting;
                frame.hasGaitPhase = true;
                frame.gaitPhase = handStride;
            }
            frame.left = LocomotionHand(frame, left, true, animateHands);
            frame.right = LocomotionHand(frame, right, false, animateHands);
            ApplyPunch(ref frame);
            GetComponent<CandyEquipment>()?.PoseHands(ref frame);
            return frame;
        }
        public void ApplyCosmeticFrame(CandyAvatarFrame frame){LastFrame=RenderedFrame=frame;if(Rig)Rig.ApplyFrame(frame,survivor?survivor.Grounded:true,0);}
        void UpdateHandMotion()
        {
            // The network and before-render paths can read the pose again in the same frame.
            if (handMotionFrame == Time.frameCount) return;
            handMotionFrame = Time.frameCount;
            Vector3 velocity = survivor.Capsule ? survivor.Capsule.velocity : Vector3.zero;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            float target = survivor.Grounded && !survivor.Climbing && !survivor.Vaulting
                ? Mathf.InverseLerp(.12f, 4f, speed) : 0f;
            handSwing = Mathf.MoveTowards(handSwing, target, Time.deltaTime * 4f);
            handSprintBlend = Mathf.MoveTowards(handSprintBlend, survivor.Sprinting ? target : 0f, Time.deltaTime * 6f);
            float cadence = Mathf.Lerp(6f, 11f, handSwing) + handSprintBlend * 3.8f;
            handStride = Mathf.Repeat(handStride + Time.deltaTime * cadence, 2f * Mathf.PI);
        }

        CandyHandPose LocomotionHand(CandyAvatarFrame frame, CandyHandPose hand, bool isLeft, bool animate)
        {
            float side = isLeft ? -1f : 1f;
            float swing = animate ? Mathf.Sin(handStride) * handSwing * side : 0f;
            if (hand.tracked)
            {
                PointOnHover(ref hand, isLeft);
                // Keep the tracked palm position and optical pose. Only the free controller wrist gets a tiny roll.
                if (animate && !hand.skeletonTracked)
                    hand.rotation *= Quaternion.Euler(0f, 0f, swing * 1.5f);
                hand.skeletonTracked = true;
                return hand;
            }

            if (!animate)
                return new CandyHandPose {
                    position = frame.head + frame.headRotation * (new Vector3(side * .23f, -.20f, .40f) * frame.scale),
                    rotation = frame.headRotation * Quaternion.Euler(8f, -side * 8f, 0f),
                    skeleton = CandyHandSkeleton.Rest(isLeft, .12f)
                };

            Quaternion facing = Quaternion.Euler(0f, frame.headRotation.eulerAngles.y, 0f);
            float idle = Mathf.Sin(Time.time * 1.8f + side) * (1f - handSwing);
            if (!survivor.IsVR)
            {
                // Keep the wrists in the lower view with the palms facing each other.
                var desktopOffset = new Vector3(side * (.23f - swing * .012f),
                    -.22f + swing * .027f + idle * .008f, .50f + swing * .075f);
                float pump = Mathf.Cos(handStride) * side;
                var runningOffset = new Vector3(side * .21f, -.27f + pump * .086f, .47f + pump * .13f);
                Vector3 walkingAngles = new(38f - swing * 15f, -side * 5f, -side * (67f + swing * 8f + idle * 3f));
                Vector3 runningAngles = new(31f - pump * 24f, -side * (8f + 6f * pump), -side * 66f);
                return new CandyHandPose
                {
                    position = frame.head + facing * (Vector3.Lerp(desktopOffset, runningOffset, handSprintBlend) * frame.scale),
                    rotation = facing * Quaternion.Euler(Vector3.Lerp(walkingAngles, runningAngles, handSprintBlend)),
                    skeleton = CandyHandSkeleton.Rest(isLeft, Mathf.Lerp(.38f + swing * .10f, .65f, handSprintBlend)),
                    skeletonTracked = true
                };
            }
            Vector3 offset = new Vector3(side * .29f, -.51f + Mathf.Abs(swing) * .024f + idle * .006f,
                .25f + swing * .065f);
            return new CandyHandPose {
                position = frame.head + facing * (offset * frame.scale),
                rotation = facing * Quaternion.Euler(51f - swing * 10f, -side * 10f, side * 24f + idle * 2f),
                skeleton = CandyHandSkeleton.Rest(isLeft, .27f),
                skeletonTracked = true
            };
        }

        void PointOnHover(ref CandyHandPose hand, bool isLeft)
        {
            if(!tracking || !hand.tracked || hand.skeletonTracked)return;
            var controller=isLeft?tracking.leftController:tracking.rightController;
            if(!controller)return;
            bool hovering=false;
            foreach(var ray in nearRays)
                if(ray && ray.isActiveAndEnabled && ray.transform.IsChildOf(controller) &&
                    (ray.TryGetCurrentUIRaycastResult(out _) || ray.hasHover))hovering=true;
            foreach(var ray in farRays)
                if(ray && ray.isActiveAndEnabled && ray.transform.IsChildOf(controller) &&
                    (ray.TryGetCurrentUIRaycastResult(out _) || ray.hasHover))hovering=true;
            if(!hovering)return;
            var pointing=CandyHandSkeleton.Rest(isLeft,.85f);
            var open=CandyHandSkeleton.Rest(isLeft);
            for(int joint=0;joint<4;joint++)pointing.SetJoint(4+joint,open.Joint(4+joint));
            hand.skeleton=pointing;
            hand.skeletonTracked=true; // Preserve the explicit pose through rig/network presentation.
        }
        void Present(float dt)
        {
            if(!Rig)return;
            Rig.calibration=!remoteDriven&&survivor&&survivor.IsVR&&!survivor.externallyDriven?CandyAvatarCalibration.Current:null;
            var frame=ReadFrame();
            bool climbing=survivor&&survivor.Climbing;
            if(climbing)
            {
                if(!wasClimbing){climbStartLeft=frame.left;climbStartRight=frame.right;}
                CandyHandPose ClimbHand(bool left,CandyHandPose live,CandyHandPose start)
                {
                    if(!survivor.TryGetClimbGrip(left,out var grip))return live;
                    float t=Mathf.SmoothStep(0,1,survivor.ClimbProgress);
                    return new CandyHandPose{tracked=true,skeletonTracked=true,position=Vector3.Lerp(start.position,grip.position,t),rotation=Quaternion.Slerp(start.rotation,grip.rotation,t),skeleton=CandyHandSkeleton.Rest(left,.18f)};
                }
                frame.left=ClimbHand(true,frame.left,climbStartLeft);frame.right=ClimbHand(false,frame.right,climbStartRight);
                climbLastLeft=frame.left;climbLastRight=frame.right;climbRelease=0;
            }
            else if(climbRelease<1)
            {
                climbRelease=Mathf.Min(1,climbRelease+dt/.16f);
                float t=Mathf.SmoothStep(0,1,climbRelease);
                frame.left.position=Vector3.Lerp(climbLastLeft.position,frame.left.position,t);frame.left.rotation=Quaternion.Slerp(climbLastLeft.rotation,frame.left.rotation,t);
                frame.right.position=Vector3.Lerp(climbLastRight.position,frame.right.position,t);frame.right.rotation=Quaternion.Slerp(climbLastRight.rotation,frame.right.rotation,t);
            }
            wasClimbing=climbing;
            bool vaulting=survivor&&survivor.Vaulting&&Rig.style.lockHandsDuringVault;
            if(vaulting)
            {
                if(!wasVaulting){vaultStartLeft=frame.left;vaultStartRight=frame.right;}
                float progress=survivor.VaultProgress;
                CandyHandPose Grip(bool isLeft,CandyHandPose live,CandyHandPose start)
                {
                    if(!survivor.TryGetVaultGrip(isLeft,out var grip))return live;
                    float reach=Mathf.SmoothStep(0,1,Mathf.Clamp01(progress/.18f));
                    float release=Mathf.SmoothStep(0,1,Mathf.Clamp01((progress-.82f)/.18f));
                    var contact=CandyHandSkeleton.Rest(isLeft,0);
                    for(int i=0;i<20;i++){var joint=contact.Joint(i);joint.y=-.013f;contact.SetJoint(i,joint);}
                    var hand=new CandyHandPose{tracked=true,skeletonTracked=true,position=Vector3.Lerp(start.position,grip.position,reach),rotation=Quaternion.Slerp(start.rotation,grip.rotation,reach),skeleton=contact};
                    hand.position=Vector3.Lerp(hand.position,live.position,release);hand.rotation=Quaternion.Slerp(hand.rotation,live.rotation,release);
                    return hand;
                }
                frame.left=Grip(true,frame.left,vaultStartLeft);frame.right=Grip(false,frame.right,vaultStartRight);
            }
            wasVaulting=vaulting;LastFrame=frame;
            var activeMoments = Moments;
            RenderedFrame = activeMoments ? activeMoments.Pose(frame) : frame;
            var presentationFrame = RenderedFrame;
            GetComponent<CandyEquipment>()?.PoseWeaponPresentationHands(ref presentationFrame, dt);
            Rig.ApplyFrame(presentationFrame, survivor ? survivor.Grounded : true, dt);
        }
        public void ApplyRemote(CandyAvatarFrame frame,int color)
        {
            if(!Rig)return;remoteDriven=true;Rig.ownerCamera=null;SetColor(color);LastFrame=RenderedFrame=frame;Rig.ApplyFrame(frame,true,Time.deltaTime);
        }
        public void RefreshNow(){Present(0);}
        public void RefreshExternal(float deltaTime){Present(deltaTime);}
    }
}
