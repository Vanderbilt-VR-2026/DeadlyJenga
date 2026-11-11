using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.Presentation;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        const float MenuDistance=1.68f;
        Ray pointerRay;
        BoxCollider focus,pressedTarget,previousFocus;
        Image pointerDot,focusOutline;
        float pressedUntil;
        bool requireRelease,startedAim,usingController;

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
            startedAim=false;requireRelease=true;focus=pressedTarget=null;
            if(pointerDot)pointerDot.gameObject.SetActive(false);
            if(focusOutline)focusOutline.gameObject.SetActive(false);
        }

        public void Point(Camera camera,bool pressed,bool held=false)
        {
            if(!camera||!isActiveAndEnabled)return;
            previousFocus=focus;focus=null;
            bool homePage=!DeadlyJenga.Presentation.MenuAccess.GuestPause(session)&&AtHome&&!hostTools&&!wildlifePage;
            if(!homePage)LayoutDefault();
            foreach(var button in buttons)if(button)button.image.color=CandyHudVisuals.Card;
            bool guest=DeadlyJenga.Presentation.MenuAccess.GuestPause(session);
            ((RectTransform)transform).sizeDelta=new Vector2(600,guest?590:1080);
            heading.rectTransform.anchoredPosition=new Vector2(0,guest?171:355);
            help.rectTransform.anchoredPosition=new Vector2(0,-480);
            bool controller = session.IsVR;
            if (controller)
            {
                var trackingCamera = MenuPointer.TrackingCamera(session, camera);
                bool aimed = DeadlyJenga.Presentation.MenuPointer.AimAt(camera, trackingCamera,
                    session.Player?.gameObject.GetComponent<DeadlyJenga.Core.IBlasterControls>()?.LeftHanded==true,
                    ray => DeadlyJenga.Presentation.MenuPointer.PanelHit(ray, (RectTransform)transform), out var input);
                pointerRay=input.Ray;
                pressed=aimed&&input.Pressed;
                held=aimed&&input.Held;
            }
            else pointerRay=new Ray(camera.transform.position,camera.transform.forward);
            if(startedAim&&usingController!=controller)requireRelease=true;
            usingController=controller;startedAim=true;
            if(requireRelease)
            {
                if(!pressed&&!held)requireRelease=false;
                pressed=held=false;
            }
            if(guest)PointGuestPause(pressed);
            else PointPage(camera,pressed,held);
            if(!isActiveAndEnabled)return;
            if(pressed&&focus){pressedTarget=focus;pressedUntil=Time.unscaledTime+.13f;}
            ApplyLabelContrast();PresentPointer();
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
            if(!Inside(target,ray,target==previousFocus?7:1,out point))return false;
            // Rows sit 2 units apart: the row hovered last frame keeps a small margin so the highlight does not flicker between neighbours.
            if(previousFocus&&target!=previousFocus&&Inside(previousFocus,ray,7,out _))return false;
            focus=target;return true;
        }
        bool Inside(BoxCollider target,Ray ray,float padY,out Vector3 point)
        {
            point=default;
            if(!target||!target.enabled||!target.gameObject.activeInHierarchy||
                !(target.transform is RectTransform rect)||!Project(ray,rect,out point))return false;
            var local=rect.InverseTransformPoint(point);
            var bounds=rect.rect;
            float pad=bounds.width<150?1.8f:14;
            return !(local.x<bounds.xMin-pad||local.x>bounds.xMax+pad||local.y<bounds.yMin-padY||local.y>bounds.yMax+padY);
        }

        void PresentPointer()
        {
            if(!pointerDot)
            {
                pointerDot=PointerImage("Pointer dot",new Vector2(15,15));
                var edge=pointerDot.gameObject.AddComponent<Outline>();edge.effectColor=CandyHudVisuals.Ink;edge.effectDistance=new Vector2(2,-2);
                focusOutline=PointerImage("Pointed control focus bar",Vector2.zero);

            }
            var panel=(RectTransform)transform;
            bool intersects=Project(pointerRay,panel,out var at);
            bool actionable=focus&&focus.gameObject.activeInHierarchy&&focus.enabled;
            bool dot=intersects&&(panel.rect.Contains(panel.InverseTransformPoint(at))||actionable);
            bool confirming=actionable&&pressedTarget==focus&&Time.unscaledTime<pressedUntil;
            Color color=confirming?CandyHudVisuals.Peach:actionable?CandyHudVisuals.Mint:CandyHudVisuals.Lavender;
            if(usingController){if(actionable)DeadlyJenga.Presentation.MenuLaser.MarkHover(confirming);dot=false;}   // VR uses the laser's own hit cursor
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
        }

        Image PointerImage(string name,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));
            go.transform.SetParent(transform,false);var image=go.GetComponent<Image>();
            image.sprite=CandyHudVisuals.Pill;image.raycastTarget=false;image.rectTransform.sizeDelta=size;return image;
        }
    }
}
