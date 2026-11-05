using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    internal static class MenuAim
    {
        static int consumedFrame=-1;
        public static bool InputConsumed=>consumedFrame==Time.frameCount;
        public static void Consume()=>consumedFrame=Time.frameCount;
        public static Ray Resolve(Camera eye,Transform fallbackSpace,bool preferredLeft,bool leftPressed,bool rightPressed,out bool controller,out bool left)
        {
            controller=false;left=preferredLeft;
            XRController preferred=null,fallback=null,pressed=null;
            foreach(var device in InputSystem.devices)
            {
                if(!(device is XRController candidate)||!candidate.isTracked.isPressed||candidate.layout.IndexOf("HandInteraction",StringComparison.OrdinalIgnoreCase)>=0)continue;
                bool isLeft=false,isRight=false;
                foreach(var usage in candidate.usages){isLeft|=usage==CommonUsages.LeftHand;isRight|=usage==CommonUsages.RightHand;}
                if(!isLeft&&!isRight)continue;
                fallback??=candidate;
                if(isLeft==preferredLeft)preferred=candidate;
                if(leftPressed!=rightPressed&&(isLeft&&leftPressed||isRight&&rightPressed))pressed=candidate;
            }
            var selected=preferred??pressed??fallback;
            var rig=eye.GetComponentInParent<XROrigin>();
            var space=rig&&rig.CameraFloorOffsetObject?rig.CameraFloorOffsetObject.transform:fallbackSpace;
            if(selected==null||!space)return new Ray(eye.transform.position,eye.transform.forward);
            left=false;foreach(var usage in selected.usages)left|=usage==CommonUsages.LeftHand;
            var position=selected.TryGetChildControl<Vector3Control>("pointerPosition")??selected.devicePosition;
            var rotation=selected.TryGetChildControl<QuaternionControl>("pointerRotation")??selected.deviceRotation;
            controller=true;return new Ray(space.TransformPoint(position.ReadValue()),space.TransformDirection(rotation.ReadValue()*Vector3.forward));
        }
        public static bool PhysicalPose(Camera eye,bool left,out Pose pose,out XRController selected)
        {
            pose=default;selected=null;
            var rig=eye.GetComponentInParent<XROrigin>();
            var space=rig&&rig.CameraFloorOffsetObject?rig.CameraFloorOffsetObject.transform:eye.transform.parent;
            if(!space)return false;
            foreach(var device in InputSystem.devices)
            {
                if(!(device is XRController controller)||!controller.isTracked.isPressed||
                    device.layout.IndexOf("HandInteraction",StringComparison.OrdinalIgnoreCase)>=0)continue;
                bool matches=false;
                foreach(var usage in device.usages)matches|=usage==(left?CommonUsages.LeftHand:CommonUsages.RightHand);
                if(!matches)continue;
                var position=device.TryGetChildControl<Vector3Control>("pointerPosition")??controller.devicePosition;
                var rotation=device.TryGetChildControl<QuaternionControl>("pointerRotation")??controller.deviceRotation;
                var origin=space.TransformPoint(position.ReadValue());var orientation=space.rotation*rotation.ReadValue();
                float norm=Quaternion.Dot(orientation,orientation);
                if(!((origin-eye.transform.position).sqrMagnitude<9)||!(norm>.5f&&norm<1.5f))continue;
                pose=new Pose(origin,orientation.normalized);selected=controller;return true;
            }
            return false;
        }
        public static int PointerIdentity(Camera eye,bool preferredLeft)
        {
            if(PhysicalPose(eye,preferredLeft,out _,out var device)||PhysicalPose(eye,!preferredLeft,out _,out device))return device.deviceId;
            return -1;
        }
        public static void PlaceTablet(RectTransform panel,Camera eye,ISessionInfo session,bool dominantLeft)
        {
            bool holderLeft=!dominantLeft;
            bool held=PhysicalPose(eye,holderLeft,out var anchor,out _);
            var tips=session?.Player?.gameObject.GetComponent<IFingerTips>();
            if(!held&&session?.Player?.HasHandTracking==true&&tips!=null&&tips.TryIndexTip(holderLeft,out _)&&session.Player is IModeActor actor)
            {
                anchor=actor.HandPose(holderLeft);
                float distance=(anchor.position-eye.transform.position).sqrMagnitude;
                held=distance>.01f&&distance<3;
            }
            bool dual=PhysicalPose(eye,dominantLeft,out _,out _)&&held;
            Vector3 position=dual?anchor.position+eye.transform.right*(holderLeft ? .12f : -.12f)+eye.transform.up*.18f
                :eye.transform.position+eye.transform.forward*.62f-eye.transform.up*.06f;
            Vector3 forward=position-eye.transform.position;
            if(forward.sqrMagnitude<.01f)return;
            float scale=.00055f*Mathf.Min(1,600/Mathf.Max(1,panel.rect.width));
            float blend=Mathf.Abs(panel.localScale.x-scale)<.00001f?1-Mathf.Exp(-20*Time.unscaledDeltaTime):1;
            panel.SetPositionAndRotation(Vector3.Lerp(panel.position,position,blend),Quaternion.Slerp(panel.rotation,Quaternion.LookRotation(forward.normalized,eye.transform.up),blend));
            panel.localScale=Vector3.one*scale;
        }
        public static bool TryPoint(Ray ray,RectTransform target,float range,out Vector2 local,out Vector3 hit)
        {
            local=default;hit=default;
            if(Vector3.Dot(ray.direction,target.forward)<=.01f||!new Plane(target.forward,target.position).Raycast(ray,out float distance)||distance>range)return false;
            hit=ray.GetPoint(distance);local=target.InverseTransformPoint(hit);return target.rect.Contains(local);
        }
        public static Image CreateReticle(Transform parent)
        {
            var image=new GameObject("Menu aim reticle",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent,false);image.rectTransform.sizeDelta=new Vector2(14,14);
            image.sprite=CandyHudVisuals.Pill;image.raycastTarget=false;image.gameObject.SetActive(false);return image;
        }
        public static void Present(Image dot,Vector3 point,bool shown,bool actionable)
        {
            if(!dot)return;dot.gameObject.SetActive(shown);if(!shown)return;
            dot.transform.position=point-dot.transform.forward*.0005f;dot.color=actionable?CandyHudVisuals.Mint:Color.white;
        }
    }

}
