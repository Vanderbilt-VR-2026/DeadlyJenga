using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPhysicsHands;

namespace DeadlyJenga.CandyPlayers
{
    [DefaultExecutionOrder(-50)]
    public sealed class CandyTrackingSource : MonoBehaviour, IFingerTips
    {
        public Transform trackingOrigin;
        // Controller roots identify the matching UI interactors; they are aim poses, not palms.
        public Transform leftController, rightController;
        public CandyPlayerStyle style;
        public ICandyHandPoseSource LeftPoseSource {get;set;}
        public ICandyHandPoseSource RightPoseSource {get;set;}
        public bool RightPinchPressed{get;private set;}
        public bool RightPinching{get;private set;}
        public bool LeftMenuPressed{get;private set;}
        public bool LeftPinching{get;private set;}
        public bool HasHandTracking=>left.skeletonTracked||right.skeletonTracked;
        public bool HasTrackedHands=>left.tracked||right.tracked;
        public bool IsHybrid=>HybridXRInput.SingleController&&GestureHand.skeletonTracked;
        public CandyHandPose GestureHand=>WorldHand(HybridXRInput.SingleController?!HybridXRInput.SingleControllerLeft:left.skeletonTracked&&!right.skeletonTracked);
        public bool TryControllerAim(bool isLeft,out Pose aim)
        {
            aim=default;
            if(!HybridXRInput.ControllerTracked(isLeft))return false;
            var rotation=HybridXRInput.Controller(isLeft)?.TryGetChildControl<UnityEngine.InputSystem.Controls.QuaternionControl>("pointerRotation");
            Quaternion q=rotation!=null?rotation.ReadValue():default;
            var hand=WorldHand(isLeft);
            if(q!=default){aim=new Pose(hand.position,(trackingOrigin?trackingOrigin.rotation:Quaternion.identity)*q);return true;}
            var pointer=isLeft?leftController:rightController;
            aim=new Pose(hand.position,pointer?pointer.rotation:hand.rotation*Quaternion.Euler(35,0,0));return true;
        }
        readonly List<XRHandSubsystem> subsystems=new List<XRHandSubsystem>();
        XRHandSubsystem subsystem;
        CandyHandPose left,right;
        bool previousRight,previousMenu,previousSelect;
        public bool MenuSelectHeld=>RightPinching||LeftPinching;
        public bool MenuSelectPressed{get;private set;}
        static readonly XRHandJointID[] JointIds={XRHandJointID.ThumbMetacarpal,XRHandJointID.ThumbProximal,XRHandJointID.ThumbDistal,XRHandJointID.ThumbTip,XRHandJointID.IndexProximal,XRHandJointID.IndexIntermediate,XRHandJointID.IndexDistal,XRHandJointID.IndexTip,XRHandJointID.MiddleProximal,XRHandJointID.MiddleIntermediate,XRHandJointID.MiddleDistal,XRHandJointID.MiddleTip,XRHandJointID.RingProximal,XRHandJointID.RingIntermediate,XRHandJointID.RingDistal,XRHandJointID.RingTip,XRHandJointID.LittleProximal,XRHandJointID.LittleIntermediate,XRHandJointID.LittleDistal,XRHandJointID.LittleTip};
        void OnEnable()=>Application.onBeforeRender+=ReadLatest;
        void OnDisable()
        {
            Application.onBeforeRender-=ReadLatest;left=right=default;
            RightPinchPressed=RightPinching=LeftMenuPressed=LeftPinching=previousRight=previousMenu=previousSelect=MenuSelectPressed=false;
        }
        void Update()
        {
            ReadLatest();
            bool rightPinch=right.skeletonTracked&&Vector3.Distance(right.skeleton.Joint(3),right.skeleton.Joint(7))<.023f;
            LeftPinching=left.skeletonTracked&&Vector3.Distance(left.skeleton.Joint(3),left.skeleton.Joint(7))<.023f;
            bool menu=left.skeletonTracked&&Vector3.Distance(left.skeleton.Joint(3),left.skeleton.Joint(11))<.023f&&!LeftPinching;
            RightPinchPressed=rightPinch&&!previousRight;LeftMenuPressed=menu&&!previousMenu;
            RightPinching=rightPinch;
            MenuSelectPressed=MenuSelectHeld&&!previousSelect;previousSelect=MenuSelectHeld;
            previousRight=rightPinch;previousMenu=menu;
        }
        void ReadLatest()
        {
            if(subsystem==null||!subsystem.running)
            {SubsystemManager.GetSubsystems(subsystems);subsystem=subsystems.Find(s=>s.running);}
            left=LeftPoseSource!=null?(LeftPoseSource.TryGetPose(out var l)?l:default):Read(true);
            right=RightPoseSource!=null?(RightPoseSource.TryGetPose(out var r)?r:default):Read(false);
        }
        CandyHandPose Read(bool isLeft)
        {
            var result=new CandyHandPose{rotation=Quaternion.identity,skeleton=CandyHandSkeleton.Rest(isLeft)};
            var controller=HybridXRInput.Controller(isLeft);
            if(controller!=null)
            {
                result.tracked=true;result.position=controller.devicePosition.ReadValue();result.rotation=controller.deviceRotation.ReadValue();
                if(result.rotation==default)result.rotation=Quaternion.identity;
                if(style){result.position+=result.rotation*style.controllerPalmOffset;result.rotation*=Quaternion.Euler(isLeft?style.leftControllerRotation:style.rightControllerRotation);}
                float Analog(string name)=>controller.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>(name)?.ReadValue()??0;
                result.skeleton=CandyHandSkeleton.ControllerGrip(isLeft,Analog("grip"),Analog("trigger"));
                return result;
            }
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
                    if(result.skeletonTracked&&CandyHandTransition.IsValidPose(result))
                    {
                        var forward=result.skeleton.Joint(8).normalized;
                        var across=(result.skeleton.Joint(4)-result.skeleton.Joint(16))*(isLeft?1:-1);
                        var up=Vector3.Cross(forward,across);
                        if(up.sqrMagnitude>.000001f)
                        {
                            var basis=Quaternion.LookRotation(forward,up);
                            for(int i=0;i<20;i++)result.skeleton.SetJoint(i,Quaternion.Inverse(basis)*result.skeleton.Joint(i));
                            result.rotation=wrist.rotation*basis;
                        }
                        return result;
                    }
                    // An incomplete skeleton must not become a stale active physics target.
                    result=new CandyHandPose{rotation=Quaternion.identity,skeleton=CandyHandSkeleton.Rest(isLeft)};
                }
            }
            return result;
        }
        public bool TryIndexTip(bool isLeft,out Vector3 tip)
        {
            var hand=WorldHand(isLeft);tip=default;if(!hand.tracked)return false;
            float scale=trackingOrigin?trackingOrigin.lossyScale.x:1;
            tip=hand.position+hand.rotation*(hand.skeletonTracked?hand.skeleton.Joint(7)*scale:Vector3.forward*.06f*scale);return true;
        }
        public CandyHandPose WorldHand(bool isLeft)
        {
            var hand=isActiveAndEnabled?(isLeft?left:right):default;
            // The XR rig's controller transforms use pointer/aim poses for UI rays.
            // Render the palm from devicePosition/deviceRotation (grip pose) instead;
            // aim rotation has a device-specific tilt and is not the wrist orientation.
            if(trackingOrigin){hand.position=trackingOrigin.TransformPoint(hand.position);hand.rotation=trackingOrigin.rotation*hand.rotation;}
            return hand;
        }
    }
}
