using UnityEngine;
using FlyingHand;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
namespace DeadlyJenga.CandyPhysicsHands
{
    [DefaultExecutionOrder(50)]
    public sealed class CandyFlyingHandBridge : MonoBehaviour, ITrackedFlightInputSource
    {
        public CastleHandActor actor;
        public CandyPhysicalHand prefab;
        public CandyHandSettings settings;
        public CandyPlayerStyle trackingStyle;
        public bool useLeftHand;
        public CandyHandMode Mode=>transition.Mode;
        public CandyPhysicalHand PhysicalHand=>physical;
        // A joining headset predicts its own hand visuals; only the host makes
        // contact with the shared castle. Set every frame to cover rig rebuilds.
        public bool NetworkPreview {get;set;}
        CandyHandTransition transition=new CandyHandTransition();
        public ICandyHandPoseSource PoseSource {get;set;}
        CandyTrackingSource source;
        CandyPhysicalHand physical;
        Collider[] pointerColliders;
        Renderer[] pointerRenderers;
        bool[] colliderEnabled,rendererEnabled;
        Vector3 trackingAnchor;
        bool anchored;
        bool trackedGeometry;
        readonly CandyHandFlightGesture flightGesture=new CandyHandFlightGesture();
        void Awake()
        {
            if(!actor.motor.externallyDriven){source=gameObject.AddComponent<CandyTrackingSource>();source.style=trackingStyle;actor.motor.TrackedInputDriver=this;}
            var visuals=actor.motor.transform.Find("HandVisual");pointerRenderers=visuals?visuals.GetComponentsInChildren<Renderer>(true):new Renderer[0];
            pointerColliders=actor.motor.GetComponentsInChildren<Collider>(true);
            colliderEnabled=new bool[pointerColliders.Length];rendererEnabled=new bool[pointerRenderers.Length];
            for(int i=0;i<colliderEnabled.Length;i++)colliderEnabled[i]=pointerColliders[i].enabled;
            for(int i=0;i<rendererEnabled.Length;i++)rendererEnabled[i]=pointerRenderers[i].enabled;
        }
        void Pointer(bool value)
        {
            for(int i=0;i<pointerColliders.Length;i++)pointerColliders[i].enabled=value&&colliderEnabled[i];
            for(int i=0;i<pointerRenderers.Length;i++)pointerRenderers[i].enabled=value&&rendererEnabled[i];
        }
        public bool TryReadFlightInput(out FlyingHandCommand command)
        {
            command=default;
            if(!source||!source.HasHandTracking){flightGesture.Reset();return false;}
            var steering=source.WorldHand(!useLeftHand);
            if(!steering.tracked||!steering.skeletonTracked){flightGesture.Reset();return false;}
            command=flightGesture.Read(steering,actor.follow.transform.rotation,actor.motor.InputSuspended);
            return true;
        }
        public void ResetFlightInput()=>flightGesture.Reset();
        void Update()
        {
            bool desktop=actor.motor.ActiveControlMode==ControlMode.Desktop;
            Pointer(false);
            bool external=actor.motor.externallyDriven;
            var raw=source?source.WorldHand(useLeftHand):default;
            if(PoseSource!=null){if(!PoseSource.TryGetPose(out raw))raw=default;}
            else if(external)raw=default;
            if(!CandyHandTransition.IsValidPose(raw))raw.tracked=false;
            CandyHandMode mode=raw.tracked?(raw.skeletonTracked?CandyHandMode.HandTracking:CandyHandMode.Controller):CandyHandMode.Inactive;
            if(desktop&&!external)
            {
                raw=new CandyHandPose{tracked=true,position=actor.motor.transform.position,rotation=actor.motor.transform.rotation,skeleton=CandyHandSkeleton.Rest(useLeftHand,.85f)};
                mode=CandyHandMode.Desktop;anchored=false;
            }
            if(raw.tracked)
            {
                if(!desktop&&!external)
                {
                    if(!anchored){trackingAnchor=raw.position;anchored=true;}
                    raw.position=actor.motor.transform.position+actor.motor.transform.rotation*((raw.position-trackingAnchor)*settings.size);
                    raw.rotation=actor.motor.transform.rotation*raw.rotation;
                }
                if(!raw.skeletonTracked)
                {
                    var pointing=CandyHandSkeleton.Rest(useLeftHand,.85f);var open=CandyHandSkeleton.Rest(useLeftHand);
                    for(int j=0;j<4;j++)pointing.SetJoint(4+j,open.Joint(4+j));raw.skeleton=pointing;
                }
            }
            transition.Step(raw,mode,useLeftHand,Time.unscaledDeltaTime,settings);
            if(!transition.Initialized)return;
            if(transition.Mode==CandyHandMode.Inactive&&transition.Strength<=0)
            {if(physical&&physical.gameObject.activeSelf){physical.SetInteractive(false);physical.gameObject.SetActive(false);}return;}
            bool needsTrackedGeometry=transition.Mode==CandyHandMode.HandTracking;
            if(!physical){physical=Instantiate(prefab);physical.name="Contact driven candy hand";physical.settings=settings;physical.left=useLeftHand;physical.JointDrivenFingers=external;physical.Build(transition.Pose);trackedGeometry=needsTrackedGeometry;}
            // Wrist movement must stay visible relative to the observer.
            actor.follow.target=actor.motor.transform.Find("CameraFollowTarget");
            actor.follow.positionTarget=null;actor.follow.compactHandView=true;
            if(trackedGeometry!=needsTrackedGeometry&&raw.tracked&&transition.Mode==mode)
            {
                physical.SetInteractive(false);
                var calibration=transition.Pose;calibration.skeleton=raw.skeleton;
                physical.RebuildInactive(calibration);trackedGeometry=needsTrackedGeometry;
            }
            physical.gameObject.SetActive(true);physical.SetTarget(transition.Pose,transition.Strength);
            physical.NetworkPreview=NetworkPreview;
            if(!physical.IsInteractive&&Mathf.Abs(physical.GeometrySize-settings.size)>.001f)physical.RebuildInactive(transition.Pose);
            // No reset on reacquisition: the dynamic hand returns through bounded drives.
            physical.SetInteractive(transition.Strength>0&&!actor.motor.InputSuspended);
            physical.skin.enabled=physical.IsInteractive||(actor.motor.InputSuspended&&transition.Strength>0);
            if(transition.Mode==CandyHandMode.Inactive){physical.gameObject.SetActive(false);}
        }
        // Respawn/rebuild only: release contacts before discarding the old rig. Never moves a held block.
        public void ResetExternalRig()
        {
            if(!actor.motor.externallyDriven)return;
            ResetTrackingRig();
        }
        public void ResetTrackingRig()
        {
            flightGesture.Reset();
            if(physical){physical.SetInteractive(false);physical.gameObject.SetActive(false);Destroy(physical.gameObject);physical=null;}
            transition=new CandyHandTransition();trackedGeometry=false;anchored=false;
        }
        void OnDisable(){if(physical){physical.SetInteractive(false);physical.gameObject.SetActive(false);}if(pointerColliders!=null)Pointer(true);if(actor&&actor.follow){actor.follow.target=actor.motor.transform.Find("CameraFollowTarget");actor.follow.positionTarget=null;}anchored=false;}
        void OnDestroy(){if(physical)Destroy(physical.gameObject);}
    }
}
