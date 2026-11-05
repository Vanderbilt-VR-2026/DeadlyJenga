using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CandyPhysicsHands;
using FlyingHand;

namespace DeadlyJenga.CastlePrototype
{
    // Two ordinary physical-hand prefabs, controlled from one stationary observer.
    public sealed class SeaSpiritController : MonoBehaviour, ISeaSpiritView
    {
        public float reach = 25, orbitDegreesPerSecond = 35;
        public float orbitAngle, minimumReach = 8, maximumReach = 80;
        [Min(.2f)] public float entranceSeconds=3.8f;
        public bool Entering=>Time.unscaledTime-entranceAt<entranceSeconds;
        public bool InputSuspended { get; set; }
        public Transform BustAnchor => spiritAnchor;
        public CastleHandActor LeftHand => left;
        public CastleHandActor RightHand => right;
        public Transform LeftShoulder => leftShoulder;
        public Transform RightShoulder => rightShoulder;
        public Transform LeftHandAnchor => HandAnchor(left);
        public Transform RightHandAnchor => HandAnchor(right);
        public bool Active => enabled && observer && left && right;
        Transform leftShoulder,rightShoulder;
        CastleHandActor left, right;
        CandyTrackingSource source;
        Transform observer;
        Transform spiritAnchor;
        Pose entranceStart;
        Vector3 cameraOffset;
        Quaternion cameraRotation;
        float entranceAt;
        bool entranceSkipArmed;
        Camera view;
        ThirdPersonVRFollow originalFollow;
        bool followWasEnabled;
        Pose observerStart;
        CastleHandActor owner;
        Vector3 observerStartScale;
        bool observerCaptured;
        float orbitDistance,minimumOrbitDistance,retreatLimit;
        public float OrbitDistance=>orbitDistance;
        public float MinimumOrbitDistance=>minimumOrbitDistance;
        public bool ControlsBlocked=>InputSuspended||(owner&&owner.InputSuspended)||Entering||Services.Session?.MenuOpen==true;
        readonly Collider[] entryClearance=new Collider[32];
        CandyFlyingHandBridge parentBridge,leftBridge,rightBridge;
        bool templateBridgePreview=>parentBridge&&parentBridge.NetworkPreview;
        readonly Vector2[] desktop = { new Vector2(-.3f,0), new Vector2(.3f,0) };
        readonly PoseSource[] inputs = { new PoseSource(), new PoseSource() };
        static Transform HandAnchor(CastleHandActor actor)
        {
            if(!actor)return null;var bridge=actor.GetComponent<CandyFlyingHandBridge>();
            return bridge&&bridge.PhysicalHand&&bridge.PhysicalHand.bodies!=null&&bridge.PhysicalHand.bodies.Length>0?bridge.PhysicalHand.bodies[0].transform:actor.Anchor;
        }
        Transform staging;
        bool shutdown;

        sealed class PoseSource : ICandyHandPoseSource
        {
            public CandyHandPose pose;
            public bool TryGetPose(out CandyHandPose value) { value=pose; return value.tracked; }
        }

        public void Initialize(CastleHandActor template, Camera camera, CandyTrackingSource tracking)
        {
            view=camera;source=tracking;
            originalFollow=camera?camera.GetComponentInParent<ThirdPersonVRFollow>():null;
            observer=originalFollow?originalFollow.transform:camera?camera.transform.parent:null;
            owner=template;
            followWasEnabled=originalFollow&&originalFollow.enabled;
            if(!template||!view||!observer||Services.World==null) { enabled=false; return; }
            observerStart=new Pose(observer.position,observer.rotation);observerStartScale=observer.localScale;observerCaptured=true;
            entranceStart=new Pose(view.transform.position,view.transform.rotation);
            var world=Services.World;
            var bearing=Vector3.ProjectOnPlane(entranceStart.position-world.Center,Vector3.up);
            if(bearing.sqrMagnitude<.0001f)bearing=-Vector3.ProjectOnPlane(view.transform.forward,Vector3.up);
            if(bearing.sqrMagnitude<.0001f)bearing=Vector3.back;
            bearing.Normalize();orbitAngle=Vector3.SignedAngle(Vector3.forward,bearing,Vector3.up);
            if(!RefreshApproach(world,bearing,true)||!FindClearOceanStart(world,bearing)){enabled=false;return;}
            if(originalFollow)originalFollow.enabled=false;
            cameraOffset=observer.InverseTransformPoint(view.transform.position);cameraRotation=Quaternion.Inverse(observer.rotation)*view.transform.rotation;
            entranceAt=Time.unscaledTime;
            spiritAnchor=new GameObject("Sea spirit bust anchor").transform;spiritAnchor.SetParent(transform,false);
            leftShoulder=new GameObject("Left spirit shoulder").transform;leftShoulder.SetParent(transform,false);
            rightShoulder=new GameObject("Right spirit shoulder").transform;rightShoulder.SetParent(transform,false);
            staging=new GameObject("Sea spirit hand assembly").transform;staging.gameObject.SetActive(false);
            parentBridge=template.GetComponent<CandyFlyingHandBridge>();
            left=Create(template,true,0);right=Create(template,false,1);
            if(left){left.transform.SetParent(transform,true);leftBridge=left.GetComponent<CandyFlyingHandBridge>();}
            if(right){right.transform.SetParent(transform,true);rightBridge=right.GetComponent<CandyFlyingHandBridge>();}
        }

        CastleHandActor Create(CastleHandActor template,bool isLeft,int index)
        {
            var actor=Instantiate(template,staging);
            foreach(var inherited in actor.GetComponents<SeaSpiritController>())Destroy(inherited);
            actor.name=isLeft?"Sea spirit left hand":"Sea spirit right hand";
            actor.motor.externallyDriven=true;actor.motor.captureDesktopCursor=false;
            actor.follow.enabled=false;
            foreach(var camera in actor.GetComponentsInChildren<Camera>(true)){camera.enabled=false;camera.tag="Untagged";}
            foreach(var listener in actor.GetComponentsInChildren<AudioListener>(true))listener.enabled=false;
            foreach(var driver in actor.GetComponentsInChildren<TrackedPoseDriver>(true))driver.enabled=false;
            foreach(var origin in actor.GetComponentsInChildren<Unity.XR.CoreUtils.XROrigin>(true))origin.enabled=false;
            var bridge=actor.GetComponent<CandyFlyingHandBridge>();
            if(!bridge){Destroy(actor.gameObject);return null;}
            var templateBridge=template.GetComponent<CandyFlyingHandBridge>();
            bridge.PhysicsProfile=templateBridge?templateBridge.PhysicsProfile:null;
            bridge.SuppressNetworkRegistration=true;bridge.ExternalUsesJointDrives=false;
            NetworkActors.Local.Remove(bridge);bridge.enabled=true;
            bridge.useLeftHand=isLeft;bridge.PoseSource=inputs[index];
            actor.gameObject.SetActive(true);actor.transform.SetParent(null,true);
            actor.motor.enabled=false;actor.motor.Body.isKinematic=true;
            return actor;
        }

        Vector3 Shoulder(bool isLeft)
        {
            if(!spiritAnchor)return transform.position;
            return spiritAnchor.position + spiritAnchor.right*(isLeft?-1:1)*Mathf.Max(1,reach*.1f)-Vector3.up*Mathf.Max(1,reach*.07f);
        }

        void Update()
        {
            if(!view||!observer||Services.World==null)return;
            bool vr=Services.Session?.IsVR==true;
            bool skipHeld = vr ? owner && owner.SelectHeld : Mouse.current?.leftButton.isPressed == true;
            bool skipPressed = vr ? owner && owner.SelectPressed : Mouse.current?.leftButton.wasPressedThisFrame == true;
            bool canSkip = !InputSuspended && !(owner && owner.InputSuspended) && Services.Session?.MenuOpen != true;
            if (!canSkip) entranceSkipArmed = false;
            else if (!skipHeld) entranceSkipArmed = true;
            else if (Entering && entranceSkipArmed && skipPressed)
            {
                entranceAt = Time.unscaledTime - Mathf.Max(.2f, entranceSeconds);
                entranceSkipArmed = false;
            }
            bool suspended=ControlsBlocked;
            if(!suspended)
            {
                var keys=Keyboard.current;
                float turn=keys==null?0:(keys.aKey.isPressed?1:0)-(keys.dKey.isPressed?1:0);
                float approach=keys==null?0:(keys.wKey.isPressed?1:0)-(keys.sKey.isPressed?1:0);
                if(vr)
                {
                    bool dominantLeft=PlayerPrefs.GetInt("CandyBlaster.LeftHanded",0)!=0;
                    var move=HybridXRInput.MovementController(dominantLeft);
                    var stick=move?.TryGetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("primary2DAxis")?.ReadValue()??Vector2.zero;
                    float deadzone=owner&&owner.motor?owner.motor.stickDeadzone:0;
                    stick=stick.normalized*Mathf.InverseLerp(deadzone,1,stick.magnitude);
                    turn-=stick.x;approach+=stick.y;
                }
                float speed=MovementSpeed(Services.World);
                float angular=Finite(orbitDegreesPerSecond)?Mathf.Max(0,orbitDegreesPerSecond):0;
                angular=Mathf.Min(angular,speed/Mathf.Max(.1f,orbitDistance)*Mathf.Rad2Deg);
                orbitAngle+=Mathf.Clamp(turn,-1,1)*angular*Time.deltaTime;
                orbitDistance-=Mathf.Clamp(approach,-1,1)*speed*Time.deltaTime;
                if(!vr&&Cursor.lockState==CursorLockMode.Locked&&Mouse.current!=null)
                {
                    int active=Mouse.current.rightButton.isPressed?0:1;
                    desktop[active]+=Mouse.current.delta.ReadValue()*.002f;
                    desktop[active]=Vector2.ClampMagnitude(desktop[active],1.5f);
                }
            }
            var world=Services.World;
            var outward=Quaternion.Euler(0,orbitAngle,0)*Vector3.forward;
            if(!RefreshApproach(world,outward,false))return;
            float radius=orbitDistance;
            Vector3 eye=EyePoint(world,outward);
            Quaternion facing=Quaternion.LookRotation(-outward,Vector3.up);
            float progress=Mathf.Clamp01((Time.unscaledTime-entranceAt)/Mathf.Max(.2f,entranceSeconds));
            float rise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.65f,progress));
            float depth=Mathf.Max(12,eye.y-world.WaterHeight(eye)+8);
            spiritAnchor.SetPositionAndRotation(eye-Vector3.up*depth*(1-rise),facing);
            var cameraPose=!vr&&Entering?EntrancePose(progress,eye,facing,radius):new Pose(eye,facing);
            observer.rotation=cameraPose.rotation*Quaternion.Inverse(cameraRotation);
            observer.position=cameraPose.position-observer.TransformVector(cameraOffset);
            leftShoulder.position=Shoulder(true);rightShoulder.position=Shoulder(false);
            for(int i=0;i<2;i++)
            {
                bool isLeft=i==0;
                var actor=isLeft?left:right;if(!actor)continue;
                (isLeft?leftBridge:rightBridge).NetworkPreview=templateBridgePreview;
                CandyHandPose pose=vr&&source?source.WorldHand(isLeft):default;
                if(vr&&source)pose=ResolveTrackingPose(pose,source.trackingOrigin,observer);
                if(vr&&pose.tracked)
                {
                    Vector3 offset=observer.InverseTransformVector(pose.position-view.transform.position);
                    if(!Finite(offset))pose.tracked=false;
                    else pose.position=ReachTarget(eye,spiritAnchor.rotation,offset,reach);
                }
                else if(!vr)
                {
                    float curl=Entering?1:Mouse.current!=null&&(isLeft?Mouse.current.rightButton.isPressed:Mouse.current.leftButton.isPressed)?1:0;
                    pose=new CandyHandPose{tracked=true,skeletonTracked=true,position=ReachTarget(eye,spiritAnchor.rotation,new Vector3(desktop[i].x*.6f,desktop[i].y*.6f,0),reach),rotation=spiritAnchor.rotation*Quaternion.Euler(-65,0,0),skeleton=DesktopHandPoses.Build(isLeft,Entering?DesktopHandPose.Fist:DesktopHandPose.Cup,curl)};
                }
                if(pose.tracked)
                {
                    var position=pose.position;position.y=Mathf.Max(position.y,world.WaterHeight(position)-.35f);pose.position=position;
                    if(Entering&&!vr)pose.position=Vector3.Lerp(Shoulder(isLeft),pose.position,rise);
                    actor.motor.transform.SetPositionAndRotation(pose.position,pose.rotation);
                }
                inputs[i].pose=pose;actor.InputSuspended=suspended;
            }
        }


        public static bool TryApproachRange(Bounds area,Vector3 center,Vector3 outward,float worldRadius,float handReach,float stepHeight,out float minimum,out float start,out float maximum)
        {
            minimum=start=maximum=0;
            if(!Finite(area.center)||!Finite(area.size)||!Finite(center)||!Finite(outward)||
                !Finite(worldRadius)||!Finite(handReach)||!Finite(stepHeight)||
                area.size.x<=0||area.size.z<=0||handReach<=0||stepHeight<=0)return false;
            outward=Vector3.ProjectOnPlane(outward,Vector3.up);
            if(outward.sqrMagnitude<.0001f)return false;
            outward.Normalize();
            var delta=Vector3.ProjectOnPlane(area.center-center,Vector3.up);
            float projected=Mathf.Abs(outward.x)*area.extents.x+Mathf.Abs(outward.z)*area.extents.z;
            float shore=Mathf.Max(0,Vector3.Dot(delta,outward)+projected);
            float body=Mathf.Max(handReach*.2f,stepHeight);
            float envelope=new Vector2(area.extents.x,area.extents.z).magnitude+delta.magnitude;
            minimum=shore+body;
            start=Mathf.Max(minimum+handReach*2,Mathf.Max(worldRadius,envelope)*1.7f);
            maximum=start+Mathf.Max(handReach*2,projected*2);
            return Finite(minimum)&&Finite(start)&&Finite(maximum);
        }
        public static Vector3 ReachTarget(Vector3 eye,Quaternion facing,Vector3 localOffset,float handReach)
        {
            var target=Vector3.forward*(handReach*.4f)+localOffset*handReach;
            return eye+facing*Vector3.ClampMagnitude(target,handReach);
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        bool RefreshApproach(IWorldQuery world,Vector3 outward,bool initialize)
        {
            if(!TryApproachRange(world.PlayableBounds,world.Center,outward,world.Radius,reach,world.StepHeight,
                out var minimum,out var start,out var maximum))return false;
            minimumOrbitDistance=minimum;
            retreatLimit=initialize?maximum:Mathf.Max(retreatLimit,maximum);
            orbitDistance=initialize?start:Mathf.Clamp(orbitDistance,minimum,retreatLimit);
            return true;
        }
        float MovementSpeed(IWorldQuery world)
        {
            float speed=Mathf.Max(reach*.4f,world.StepHeight*2);
            return Mathf.Max(0,Mathf.Min(speed,Mathf.Min(HandMovementLimit(leftBridge),HandMovementLimit(rightBridge))));
        }
        static float HandMovementLimit(CandyFlyingHandBridge bridge)
        {
            var settings=bridge?bridge.settings:null;
            if(!settings)return float.PositiveInfinity;
            if(!Finite(settings.maxSpeed)||!Finite(settings.maxTargetSpeed))return 0;
            return Mathf.Max(0,Mathf.Min(settings.maxSpeed,settings.maxTargetSpeed)*.5f);
        }
        Vector3 EyePoint(IWorldQuery world,Vector3 outward)
        {
            Vector3 eye=world.Center+outward*orbitDistance;
            eye.y=Mathf.Max(world.WaterHeight(eye)+2,world.TopHeight*.55f+world.Center.y*.45f);
            return eye;
        }
        bool FindClearOceanStart(IWorldQuery world,Vector3 outward)
        {
            float clearance=Mathf.Max(reach*.2f,world.StepHeight);
            var physics=gameObject.scene.GetPhysicsScene();
            if(!physics.IsValid())return false;
            for(int attempt=0;attempt<8;attempt++)
            {
                var eye=EyePoint(world,outward);
                float water=world.WaterHeight(eye);
                if(!Finite(eye)||!Finite(water))return false;
                var lower=new Vector3(eye.x,water+clearance,eye.z);
                int count=physics.OverlapCapsule(lower,eye,clearance,entryClearance,
                    (1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore);
                if(count==0){retreatLimit=Mathf.Max(retreatLimit,orbitDistance+reach*2);return true;}
                orbitDistance+=Mathf.Max(reach,world.StepHeight*2);
            }
            return false;
        }

        public static CandyHandPose ResolveTrackingPose(CandyHandPose pose,Transform trackingOrigin,Transform observer)
        {
            if(pose.tracked&&!trackingOrigin&&observer)
            {pose.position=observer.TransformPoint(pose.position);pose.rotation=observer.rotation*pose.rotation;}
            return pose;
        }
        Pose EntrancePose(float progress,Vector3 eye,Quaternion facing,float radius)
        {
            var world=Services.World;
            Vector3 high=Vector3.Lerp(entranceStart.position,eye,.42f);
            high.y=Mathf.Max(entranceStart.position.y+12,world.TopHeight+Mathf.Max(12,radius*.65f));
            Quaternion reveal=Quaternion.LookRotation(eye+Vector3.up*2-high,Vector3.up);
            float launch=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.42f,progress));
            if(progress<.55f)return new Pose(Vector3.Lerp(entranceStart.position,high,launch),Quaternion.Slerp(entranceStart.rotation,reveal,launch));
            float enter=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1,progress));
            return new Pose(Vector3.Lerp(high,eye,enter),Quaternion.Slerp(reveal,facing,enter));
        }

        public void Shutdown()
        {
            if(shutdown)return;shutdown=true;
            if(left){left.transform.SetParent(null,true);left.gameObject.SetActive(false);Destroy(left.gameObject);}
            if(right){right.transform.SetParent(null,true);right.gameObject.SetActive(false);Destroy(right.gameObject);}
            if(observer&&observerCaptured)
            {
                observer.localScale=observerStartScale;observer.SetPositionAndRotation(observerStart.position,observerStart.rotation);
            }
            if(originalFollow)originalFollow.enabled=followWasEnabled;
            if(staging)Destroy(staging.gameObject);
            if(leftShoulder)Destroy(leftShoulder.gameObject);if(rightShoulder)Destroy(rightShoulder.gameObject);
            if(spiritAnchor)Destroy(spiritAnchor.gameObject);
        }
        void OnDestroy()=>Shutdown();
    }
}
