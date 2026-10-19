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
        public bool InputSuspended { get; set; }
        public Transform BustAnchor => observer;
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
            if(!observer)return transform.position;
            return observer.position + observer.right*(isLeft?-1:1)*Mathf.Max(1,reach*.1f)-Vector3.up*Mathf.Max(1,reach*.07f);
        }

        void Update()
        {
            if(!view||!observer||Services.World==null)return;
            bool vr=source&&source.HasTrackedHands;
            if(!InputSuspended)
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
            observer.SetPositionAndRotation(eye,Quaternion.LookRotation(-outward,Vector3.up));
            leftShoulder.position=Shoulder(true);rightShoulder.position=Shoulder(false);
            for(int i=0;i<2;i++)
            {
                bool isLeft=i==0;
                var actor=isLeft?left:right;if(!actor)continue;
                actor.GetComponent<CandyFlyingHandBridge>().NetworkPreview=templateBridgePreview;
                CandyHandPose pose=vr?source.WorldHand(isLeft):default;
                if(vr&&pose.tracked)
                {
                    Vector3 offset=observer.InverseTransformVector(pose.position-view.transform.position);
                    pose.position=eye+observer.forward*(radius*.72f)+observer.rotation*(offset*reach);
                }
                else if(!vr)
                {
                    float curl=Mouse.current!=null&&(isLeft?Mouse.current.rightButton.isPressed:Mouse.current.leftButton.isPressed)?1:0;
                    pose=new CandyHandPose{tracked=true,skeletonTracked=true,position=eye+observer.forward*(radius*.72f)+observer.right*(desktop[i].x*reach*.6f)+Vector3.up*(desktop[i].y*reach*.6f),rotation=observer.rotation*Quaternion.Euler(-65,0,0),skeleton=DesktopHandPoses.Build(isLeft,DesktopHandPose.Cup,curl)};
                }
                if(pose.tracked)
                {
                    var position=pose.position;position.y=Mathf.Max(position.y,world.WaterHeight(position)-.35f);pose.position=position;
                    actor.motor.transform.SetPositionAndRotation(position,pose.rotation);
                }
                inputs[i].pose=pose;actor.InputSuspended=InputSuspended;
            }
        }

        public void Shutdown()
        {
            if(shutdown)return;shutdown=true;
            if(left){left.transform.SetParent(null,true);left.gameObject.SetActive(false);Destroy(left.gameObject);}
            if(right){right.transform.SetParent(null,true);right.gameObject.SetActive(false);Destroy(right.gameObject);}
            if(originalFollow){originalFollow.enabled=followWasEnabled;if(observer)observer.SetPositionAndRotation(observerStart.position,observerStart.rotation);}
            if(staging)Destroy(staging.gameObject);
            if(leftShoulder)Destroy(leftShoulder.gameObject);if(rightShoulder)Destroy(rightShoulder.gameObject);
        }
        void OnDestroy()=>Shutdown();
    }
}
