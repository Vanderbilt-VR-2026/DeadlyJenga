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
        Camera view;
        ThirdPersonVRFollow originalFollow;
        bool followWasEnabled;
        Pose observerStart;
        float headYaw;
        CandyFlyingHandBridge parentBridge;
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
            if(!template||!observer) { enabled=false; return; }
            observerStart=new Pose(observer.position,observer.rotation);
            entranceStart=new Pose(view.transform.position,view.transform.rotation);
            cameraOffset=observer.InverseTransformPoint(view.transform.position);cameraRotation=Quaternion.Inverse(observer.rotation)*view.transform.rotation;
            entranceAt=Time.unscaledTime;
            spiritAnchor=new GameObject("Sea spirit bust anchor").transform;spiritAnchor.SetParent(transform,false);
            leftShoulder=new GameObject("Left spirit shoulder").transform;leftShoulder.SetParent(transform,false);
            rightShoulder=new GameObject("Right spirit shoulder").transform;rightShoulder.SetParent(transform,false);
            if(originalFollow){followWasEnabled=originalFollow.enabled;originalFollow.enabled=false;}
            headYaw=view.transform.localEulerAngles.y;
            var center=Services.World!=null?Services.World.Center:Vector3.zero;
            orbitAngle=Vector3.SignedAngle(Vector3.forward,Vector3.ProjectOnPlane(observer.position-center,Vector3.up),Vector3.up);
            staging=new GameObject("Sea spirit hand assembly").transform;staging.gameObject.SetActive(false);
            parentBridge=template.GetComponent<CandyFlyingHandBridge>();
            left=Create(template,true,0);right=Create(template,false,1);
            if(left)left.transform.SetParent(transform,true);if(right)right.transform.SetParent(transform,true);
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
            bool suspended=InputSuspended||Entering||Services.Session?.MenuOpen==true;
            bool vr=source&&source.HasTrackedHands;
            if(!suspended)
            {
                var keys=Keyboard.current;
                float turn=keys==null?0:(keys.aKey.isPressed?1:0)-(keys.dKey.isPressed?1:0);
                if(vr)
                {
                    float yaw=view.transform.localEulerAngles.y;
                    orbitAngle+=Mathf.Clamp(Mathf.DeltaAngle(headYaw,yaw),-25,25)*.7f;headYaw=yaw;
                }
                orbitAngle+=turn*orbitDegreesPerSecond*Time.deltaTime;
                if(keys!=null)reach=Mathf.Clamp(reach+((keys.sKey.isPressed?1:0)-(keys.wKey.isPressed?1:0))*Time.deltaTime*10,minimumReach,maximumReach);
                if(!vr&&Cursor.lockState==CursorLockMode.Locked&&Mouse.current!=null)
                {
                    int active=Mouse.current.rightButton.isPressed?0:1;
                    desktop[active]+=Mouse.current.delta.ReadValue()*.002f;
                    desktop[active]=Vector2.ClampMagnitude(desktop[active],1.5f);
                }
            }
            var world=Services.World;
            float radius=Mathf.Max(world.Radius*1.7f,reach*.8f);
            var outward=Quaternion.Euler(0,orbitAngle,0)*Vector3.forward;
            Vector3 eye=world.Center+outward*radius;
            eye.y=Mathf.Max(world.WaterHeight(eye)+2,world.TopHeight*.55f+world.Center.y*.45f);
            Quaternion facing=Quaternion.LookRotation(-outward,Vector3.up);
            float progress=Mathf.Clamp01((Time.unscaledTime-entranceAt)/Mathf.Max(.2f,entranceSeconds));
            float rise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.65f,progress));
            float depth=Mathf.Max(12,eye.y-world.WaterHeight(eye)+8);
            spiritAnchor.SetPositionAndRotation(eye-Vector3.up*depth*(1-rise),facing);
            var cameraPose=Entering?EntrancePose(progress,eye,facing,radius):new Pose(eye,facing);
            observer.rotation=cameraPose.rotation*Quaternion.Inverse(cameraRotation);
            observer.position=cameraPose.position-observer.rotation*cameraOffset;
            leftShoulder.position=Shoulder(true);rightShoulder.position=Shoulder(false);
            for(int i=0;i<2;i++)
            {
                bool isLeft=i==0;
                var actor=isLeft?left:right;if(!actor)continue;
                actor.GetComponent<CandyFlyingHandBridge>().NetworkPreview=templateBridgePreview;
                CandyHandPose pose=vr?source.WorldHand(isLeft):default;
                if(vr&&pose.tracked&&!Entering)
                {
                    Vector3 offset=observer.InverseTransformVector(pose.position-view.transform.position);
                    pose.position=eye+spiritAnchor.forward*(radius*.72f)+spiritAnchor.rotation*(offset*reach);
                }
                else if(!vr||Entering)
                {
                    float curl=Entering?1:Mouse.current!=null&&(isLeft?Mouse.current.rightButton.isPressed:Mouse.current.leftButton.isPressed)?1:0;
                    pose=new CandyHandPose{tracked=true,skeletonTracked=true,position=eye+spiritAnchor.forward*(radius*.72f)+spiritAnchor.right*(desktop[i].x*reach*.6f)+Vector3.up*(desktop[i].y*reach*.6f),rotation=spiritAnchor.rotation*Quaternion.Euler(-65,0,0),skeleton=DesktopHandPoses.Build(isLeft,Entering?DesktopHandPose.Fist:DesktopHandPose.Cup,curl)};
                }
                if(pose.tracked)
                {
                    var position=pose.position;position.y=Mathf.Max(position.y,world.WaterHeight(position)-.35f);pose.position=position;
                    if(Entering)pose.position=Vector3.Lerp(Shoulder(isLeft),pose.position,rise);
                    actor.motor.transform.SetPositionAndRotation(position,pose.rotation);
                }
                inputs[i].pose=pose;actor.InputSuspended=suspended;
            }
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
            if(originalFollow){originalFollow.enabled=followWasEnabled;if(observer)observer.SetPositionAndRotation(observerStart.position,observerStart.rotation);}
            if(staging)Destroy(staging.gameObject);
            if(leftShoulder)Destroy(leftShoulder.gameObject);if(rightShoulder)Destroy(rightShoulder.gameObject);
            if(spiritAnchor)Destroy(spiritAnchor.gameObject);
        }
        void OnDestroy()=>Shutdown();
    }
}
