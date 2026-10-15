using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using System.Collections.Generic;
using DeadlyJenga.CandyPlayers;
namespace DeadlyJenga.CandyPhysicsHands
{
    // Standalone one-block bench. Desktop simulation is input only, not a recognized grab.
    public sealed class CandyHandLab : MonoBehaviour
    {
        public CandyPhysicalHand prefab;
        public CandyPhysicalHand trackedPrefab;
        public CandyHandSettings settings;
        public Camera view;
        public Transform trackingOrigin;
        public TrackedPoseDriver poseDriver;
        public Unity.XR.CoreUtils.XROrigin xrOrigin;
        public Rigidbody block;
        public CandyPhysicalHand Hand {get;private set;}
        public bool automaticTest;
        public float closure;
        public Vector3 wrist=new Vector3(0,.35f,0);
        CandyTrackingSource source;
        readonly CandyHandTransition transition=new CandyHandTransition();
        readonly List<UnityEngine.XR.XRDisplaySubsystem> displays=new List<UnityEngine.XR.XRDisplaySubsystem>();
        Vector3 desktopViewPosition;Quaternion desktopViewRotation;
        bool trackingGeometry;
        void Start(){desktopViewPosition=view.transform.localPosition;desktopViewRotation=view.transform.localRotation;source=gameObject.AddComponent<CandyTrackingSource>();source.trackingOrigin=trackingOrigin;Hand=Instantiate(prefab);Hand.settings=settings;Hand.Build(Pose(wrist,closure));Hand.SetInteractive(false);}
        public static CandyHandPose Pose(Vector3 wrist,float closure)
        {
            var pose=new CandyHandPose{tracked=true,skeletonTracked=true,position=wrist,rotation=Quaternion.identity,skeleton=CandyHandSkeleton.Rest(false)};
            // Bench pinch: opposing thumb/index pads close around a 22mm block; other fingers remain open.
            float x=Mathf.Lerp(.04f,.003f,closure);
            var folded=CandyHandSkeleton.Rest(false,.95f);
            for(int f=2;f<5;f++)for(int j=0;j<4;j++){var p=folded.Joint(f*4+j);p.y=-p.y+.016f;pose.skeleton.SetJoint(f*4+j,p);}
            for(int finger=0;finger<2;finger++)
            {
                float sign=finger==0?-1:1,length=finger==0?.03f:.04f;
                var start=new Vector3(finger==0?-.035f:-.028f,-.004f,finger==0?.014f:.050f);
                var pad=new Vector3(sign*x,-.004f,.060f);var delta=pad-start;
                var outside=new Vector3(delta.z,0,-delta.x).normalized*sign;
                var middle=(start+pad)*.5f+outside*Mathf.Sqrt(Mathf.Max(0,length*length-delta.sqrMagnitude*.25f));
                pose.skeleton.SetJoint(finger*4,start);pose.skeleton.SetJoint(finger*4+1,middle);pose.skeleton.SetJoint(finger*4+2,pad);pose.skeleton.SetJoint(finger*4+3,pad+Vector3.forward*.030f);
            }
            return pose;
        }
        void Update()
        {
            if(!Hand||automaticTest)return;
            SubsystemManager.GetSubsystems(displays);bool xr=displays.Exists(d=>d.running);
            if(poseDriver)
            {
                bool changed=poseDriver.enabled!=xr;poseDriver.enabled=xr;if(xrOrigin)xrOrigin.enabled=xr;
                if(changed&&!xr){view.transform.localPosition=desktopViewPosition;view.transform.localRotation=desktopViewRotation;}
            }
            var raw=source.WorldHand(false);CandyHandMode mode;
            if(raw.tracked)
            {
                mode=raw.skeletonTracked?CandyHandMode.HandTracking:CandyHandMode.Controller;
                if(!raw.skeletonTracked){var open=CandyHandSkeleton.Rest(false);raw.skeleton=CandyHandSkeleton.Rest(false,.85f);for(int j=0;j<4;j++)raw.skeleton.SetJoint(j+4,open.Joint(j+4));}
            }
            else if(xr)mode=CandyHandMode.Inactive;
            else
            {
                mode=CandyHandMode.Desktop;var k=Keyboard.current;
                if(k!=null){float dt=Time.deltaTime*.15f;wrist+=new Vector3((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.eKey.isPressed?1:0)-(k.qKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0))*dt;closure=Mathf.MoveTowards(closure,k.spaceKey.isPressed?1:0,Time.deltaTime*2);}
                raw=Pose(wrist,closure);
            }
            transition.Step(raw,mode,false,Time.unscaledDeltaTime,settings);
            bool liveGeometry=mode==CandyHandMode.HandTracking||mode==CandyHandMode.Controller;
            if(transition.Initialized&&transition.Mode==mode&&transition.Strength>0&&trackingGeometry!=liveGeometry)
            {
                Hand.SetInteractive(false);var visual=liveGeometry&&trackedPrefab?trackedPrefab:prefab;
                Hand.skin.sharedMesh=visual.skin.sharedMesh;Hand.visualReferenceLengths=visual.visualReferenceLengths;
                var calibration=raw;calibration.position=transition.Pose.position;calibration.rotation=transition.Pose.rotation;Hand.RebuildInactive(calibration);trackingGeometry=liveGeometry;
            }
            Hand.SetTarget(transition.Pose,transition.Strength);Hand.SetInteractive(transition.Initialized&&transition.Strength>0);
            Hand.skin.enabled=Hand.IsInteractive;
        }
        void OnGUI(){GUI.Box(new Rect(15,15,530,65),"CANDY CONTACT HAND LAB — no grab helper\nWASD move · Q/E lower/lift · hold Space close thumb/index\nXR Hands: actual skeleton · sliders live on Hand Settings asset");}
        void OnDestroy(){if(Hand)Destroy(Hand.gameObject);}
    }
}
