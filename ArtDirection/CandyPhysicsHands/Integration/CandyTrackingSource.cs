using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;

namespace DeadlyJenga.CandyPlayers
{
    [DefaultExecutionOrder(-50)]
    public sealed class CandyTrackingSource : MonoBehaviour
    {
        public Transform trackingOrigin;
        public CandyPlayerStyle style;
        public bool RightPinchPressed{get;private set;}
        public bool LeftMenuPressed{get;private set;}
        public bool LeftPinching{get;private set;}
        public bool HasHandTracking=>left.skeletonTracked||right.skeletonTracked;
        public bool HasTrackedHands=>left.tracked||right.tracked;
        InputAction lp,lr,lt,rp,rr,rt;
        readonly List<XRHandSubsystem> subsystems=new List<XRHandSubsystem>();
        XRHandSubsystem subsystem;
        CandyHandPose left,right;
        bool previousRight,previousMenu;
        static readonly XRHandJointID[] JointIds={XRHandJointID.ThumbMetacarpal,XRHandJointID.ThumbProximal,XRHandJointID.ThumbDistal,XRHandJointID.ThumbTip,XRHandJointID.IndexProximal,XRHandJointID.IndexIntermediate,XRHandJointID.IndexDistal,XRHandJointID.IndexTip,XRHandJointID.MiddleProximal,XRHandJointID.MiddleIntermediate,XRHandJointID.MiddleDistal,XRHandJointID.MiddleTip,XRHandJointID.RingProximal,XRHandJointID.RingIntermediate,XRHandJointID.RingDistal,XRHandJointID.RingTip,XRHandJointID.LittleProximal,XRHandJointID.LittleIntermediate,XRHandJointID.LittleDistal,XRHandJointID.LittleTip};
        void Awake()
        {
            InputAction Action(string path)=>new InputAction(type:InputActionType.Value,binding:path);
            lp=Action("<XRController>{LeftHand}/devicePosition");lr=Action("<XRController>{LeftHand}/deviceRotation");lt=Action("<XRController>{LeftHand}/isTracked");
            rp=Action("<XRController>{RightHand}/devicePosition");rr=Action("<XRController>{RightHand}/deviceRotation");rt=Action("<XRController>{RightHand}/isTracked");
        }
        void OnEnable(){lp.Enable();lr.Enable();lt.Enable();rp.Enable();rr.Enable();rt.Enable();Application.onBeforeRender+=ReadLatest;}
        void OnDisable(){lp.Disable();lr.Disable();lt.Disable();rp.Disable();rr.Disable();rt.Disable();Application.onBeforeRender-=ReadLatest;}
        void OnDestroy(){lp.Dispose();lr.Dispose();lt.Dispose();rp.Dispose();rr.Dispose();rt.Dispose();}
        void Update()
        {
            ReadLatest();
            bool rightPinch=right.skeletonTracked&&Vector3.Distance(right.skeleton.Joint(3),right.skeleton.Joint(7))<.023f;
            LeftPinching=left.skeletonTracked&&Vector3.Distance(left.skeleton.Joint(3),left.skeleton.Joint(7))<.023f;
            bool menu=left.skeletonTracked&&Vector3.Distance(left.skeleton.Joint(3),left.skeleton.Joint(11))<.023f&&!LeftPinching;
            RightPinchPressed=rightPinch&&!previousRight;LeftMenuPressed=menu&&!previousMenu;
            previousRight=rightPinch;previousMenu=menu;
        }
        void ReadLatest()
        {
            if(subsystem==null||!subsystem.running)
            {SubsystemManager.GetSubsystems(subsystems);subsystem=subsystems.Find(s=>s.running);}
            left=Read(true,lp,lr,lt);right=Read(false,rp,rr,rt);
        }
        CandyHandPose Read(bool isLeft,InputAction position,InputAction rotation,InputAction tracked)
        {
            var result=new CandyHandPose{rotation=Quaternion.identity,skeleton=CandyHandSkeleton.Rest(isLeft)};
            if(subsystem!=null&&subsystem.running)
            {
                var hand=isLeft?subsystem.leftHand:subsystem.rightHand;
                if(hand.isTracked&&hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wrist))
                {
                    result.position=wrist.position;result.rotation=wrist.rotation;result.tracked=true;result.skeletonTracked=true;
                    for(int i=0;i<20;i++)
                    {
                        if(!hand.GetJoint(JointIds[i]).TryGetPose(out var joint)){result.skeletonTracked=false;break;}
                        result.skeleton.SetJoint(i,Quaternion.Inverse(wrist.rotation)*(joint.position-wrist.position));
                    }
                    if(result.skeletonTracked)return result;
                    // An incomplete skeleton must not become a stale active physics target.
                    result=new CandyHandPose{rotation=Quaternion.identity,skeleton=CandyHandSkeleton.Rest(isLeft)};
                }
            }
            if(position.activeControl!=null&&tracked.ReadValue<float>()>.5f)
            {
                result.tracked=true;result.position=position.ReadValue<Vector3>();result.rotation=rotation.ReadValue<Quaternion>();
                if(result.rotation==default)result.rotation=Quaternion.identity;
                if(style){result.position+=result.rotation*style.controllerPalmOffset;result.rotation*=Quaternion.Euler(isLeft?style.leftControllerRotation:style.rightControllerRotation);}
            }
            return result;
        }
        public CandyHandPose WorldHand(bool isLeft)
        {
            var hand=isLeft?left:right;
            if(trackingOrigin){hand.position=trackingOrigin.TransformPoint(hand.position);hand.rotation=trackingOrigin.rotation*hand.rotation;}
            return hand;
        }
    }
}
