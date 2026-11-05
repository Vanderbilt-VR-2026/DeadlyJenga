using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using Unity.XR.CoreUtils;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        const float MenuDistance=1.68f;
        Ray pointerRay;
        BoxCollider focus,pressedTarget;
        Image pointerDot,focusOutline;
        LineRenderer pointerBeam;
        float pressedUntil;
        bool requireRelease,startedAim,usingController;
        Camera rigCamera;
        Transform trackingSpace;

        public void Show(bool show,Camera camera)
        {
            roomLeave.Clear();
            gameObject.SetActive(show&&camera);
            if(!show||!camera)return;
            var eye=camera.transform;
            var flat=Vector3.ProjectOnPlane(eye.forward,Vector3.up);
            if(flat.sqrMagnitude<.015f)flat=Vector3.Cross(eye.right,Vector3.up);
            flat=flat.sqrMagnitude>.001f?flat.normalized:Vector3.forward;
            transform.SetPositionAndRotation(eye.position+flat*MenuDistance,Quaternion.LookRotation(flat,Vector3.up));
            startedAim=false;requireRelease=true;focus=pressedTarget=null;rigCamera=null;
            if(pointerDot)pointerDot.gameObject.SetActive(false);
            if(pointerBeam)pointerBeam.enabled=false;
            if(focusOutline)focusOutline.gameObject.SetActive(false);
        }

        public void Point(Camera camera,bool pressed,bool held=false)
        {
            if(!camera||!isActiveAndEnabled)return;
            focus=null;
            foreach(var button in buttons)if(button)button.image.color=CandyHudVisuals.Card;
            ((RectTransform)transform).sizeDelta=new Vector2(600,1080);
            help.rectTransform.anchoredPosition=new Vector2(0,-480);
            bool controller=ControllerRay(camera,out pointerRay);
            if(!controller)pointerRay=new Ray(camera.transform.position,camera.transform.forward);
            if(startedAim&&usingController!=controller)requireRelease=true;
            usingController=controller;startedAim=true;
            if(requireRelease)
            {
                if(!pressed&&!held)requireRelease=false;
                pressed=held=false;
            }
            PointPage(camera,pressed,held);
            if(!isActiveAndEnabled)return;
            if(pressed&&focus){pressedTarget=focus;pressedUntil=Time.unscaledTime+.13f;}
            ApplyLabelContrast();PresentPointer();
        }

        bool ControllerRay(Camera camera,out Ray ray)
        {
            ray=default;
            if(rigCamera!=camera)
            {
                rigCamera=camera;
                var rig=camera.GetComponentInParent<XROrigin>();
                trackingSpace=rig&&rig.Camera==camera&&rig.CameraFloorOffsetObject?rig.CameraFloorOffsetObject.transform:null;
            }
            if(!trackingSpace)return false;
            foreach(var device in InputSystem.devices)
            {
                if(!(device is XRController controller)||!controller.isTracked.isPressed||
                    device.layout.IndexOf("HandInteraction",System.StringComparison.OrdinalIgnoreCase)>=0)continue;
                bool right=false;foreach(var usage in device.usages)right|=usage==UnityEngine.InputSystem.CommonUsages.RightHand;
                if(!right)continue;
                var position=device.TryGetChildControl<Vector3Control>("pointerPosition")??controller.devicePosition;
                var rotation=device.TryGetChildControl<QuaternionControl>("pointerRotation")??controller.deviceRotation;
                if(position==null||rotation==null)continue;
                var origin=trackingSpace.TransformPoint(position.ReadValue());
                var direction=trackingSpace.TransformDirection(rotation.ReadValue()*Vector3.forward);
                float span=(origin-camera.transform.position).sqrMagnitude;
                if(!(span<9&&direction.sqrMagnitude>.5f&&direction.sqrMagnitude<2))continue;
                ray=new Ray(origin,direction.normalized);return true;
            }
            return false;
        }

        static bool Project(Ray ray,Transform plane,out Vector3 point)
        {
            point=default;
            float dot=Vector3.Dot(ray.direction,plane.forward);
            if(dot<.008f)return false;
            float distance=Vector3.Dot(plane.position-ray.origin,plane.forward)/dot;
            if(!(distance>.04f&&distance<4))return false;
            point=ray.GetPoint(distance);return true;
        }

        bool Hit(BoxCollider target,Ray ray)=>Hit(target,ray,out _);
        bool Hit(BoxCollider target,Ray ray,out Vector3 point)
        {
            point=default;
            if(!target||!target.enabled||!target.gameObject.activeInHierarchy||
                !(target.transform is RectTransform rect)||!Project(ray,rect,out point))return false;
            var local=rect.InverseTransformPoint(point);
            var bounds=rect.rect;
            float pad=bounds.width<150?1.8f:14;
            if(local.x<bounds.xMin-pad||local.x>bounds.xMax+pad||local.y<bounds.yMin-1||local.y>bounds.yMax+1)return false;
            focus=target;return true;
        }

        void PresentPointer()
        {
            if(!pointerDot)
            {
                pointerDot=PointerImage("Pointer dot",new Vector2(15,15));
                var edge=pointerDot.gameObject.AddComponent<Outline>();edge.effectColor=CandyHudVisuals.Ink;edge.effectDistance=new Vector2(2,-2);
                focusOutline=PointerImage("Pointed control focus bar",Vector2.zero);
                var source=Resources.Load<Material>("CandyUtilityGlow");
                if(source)
                {
                    var beam=new GameObject("Right controller menu ray");beam.transform.SetParent(transform,false);
                    pointerBeam=beam.AddComponent<LineRenderer>();pointerBeam.sharedMaterial=source;pointerBeam.useWorldSpace=true;
                    pointerBeam.positionCount=2;pointerBeam.startWidth=.005f;pointerBeam.endWidth=.002f;
                    pointerBeam.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;pointerBeam.receiveShadows=false;
                }
            }
            var panel=(RectTransform)transform;
            bool intersects=Project(pointerRay,panel,out var at);
            bool actionable=focus&&focus.gameObject.activeInHierarchy&&focus.enabled;
            bool dot=intersects&&(panel.rect.Contains(panel.InverseTransformPoint(at))||actionable);
            bool confirming=actionable&&pressedTarget==focus&&Time.unscaledTime<pressedUntil;
            Color color=confirming?CandyHudVisuals.Peach:actionable?CandyHudVisuals.Mint:CandyHudVisuals.Lavender;
            pointerDot.gameObject.SetActive(dot);
            if(dot)
            {
                pointerDot.color=color;pointerDot.rectTransform.sizeDelta=Vector2.one*(actionable?17:11);
                pointerDot.transform.position=at-transform.forward*.008f;
                pointerDot.transform.SetAsLastSibling();
            }
            focusOutline.gameObject.SetActive(actionable);
            if(actionable&&focus.transform is RectTransform row)
            {
                focusOutline.color=color;focusOutline.rectTransform.sizeDelta=new Vector2(5,row.rect.height*.67f);
                focusOutline.transform.position=row.TransformPoint(new Vector3(row.rect.xMin+8,0,-3));
                focusOutline.transform.SetAsLastSibling();if(dot)pointerDot.transform.SetAsLastSibling();
            }
            if(pointerBeam)
            {
                pointerBeam.enabled=usingController;
                if(usingController)
                {
                    pointerBeam.startColor=color;pointerBeam.endColor=color;
                    pointerBeam.SetPosition(0,pointerRay.GetPoint(.025f));pointerBeam.SetPosition(1,dot?at-transform.forward*.01f:pointerRay.GetPoint(MenuDistance));
                }
            }
        }

        Image PointerImage(string name,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));
            go.transform.SetParent(transform,false);var image=go.GetComponent<Image>();
            image.sprite=CandyHudVisuals.Pill;image.raycastTarget=false;image.rectTransform.sizeDelta=size;return image;
        }
    }
}
