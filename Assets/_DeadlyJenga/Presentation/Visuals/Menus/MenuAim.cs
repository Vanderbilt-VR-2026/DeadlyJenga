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
            var selected=pressed??preferred??fallback;
            var rig=eye.GetComponentInParent<XROrigin>();
            var space=rig&&rig.CameraFloorOffsetObject?rig.CameraFloorOffsetObject.transform:fallbackSpace;
            if(selected==null||!space)return new Ray(eye.transform.position,eye.transform.forward);
            left=false;foreach(var usage in selected.usages)left|=usage==CommonUsages.LeftHand;
            var position=selected.TryGetChildControl<Vector3Control>("pointerPosition")??selected.devicePosition;
            var rotation=selected.TryGetChildControl<QuaternionControl>("pointerRotation")??selected.deviceRotation;
            controller=true;return new Ray(space.TransformPoint(position.ReadValue()),space.TransformDirection(rotation.ReadValue()*Vector3.forward));
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
