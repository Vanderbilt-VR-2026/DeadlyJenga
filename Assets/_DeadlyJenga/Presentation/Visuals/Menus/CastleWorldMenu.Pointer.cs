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
        int tabletPointerDevice=-1;
        bool tabletDominantLeft,tabletWasHeld;
        Image tabletFrame;
        Mesh tabletCaseMesh;
        Material tabletCaseMaterial;

        public void Show(bool show,Camera camera)
        {
            roomLeave.Clear();
            if(!show)RestoreEquipmentWheelLayout();
            gameObject.SetActive(show&&camera);
            if(!show||!camera)return;
            var eye=camera.transform;
            var flat=Vector3.ProjectOnPlane(eye.forward,Vector3.up);
            if(flat.sqrMagnitude<.015f)flat=Vector3.Cross(eye.right,Vector3.up);
            flat=flat.sqrMagnitude>.001f?flat.normalized:Vector3.forward;
            transform.SetPositionAndRotation(eye.position+flat*MenuDistance,Quaternion.LookRotation(flat,Vector3.up));
            startedAim=false;requireRelease=true;focus=pressedTarget=null;tabletPointerDevice=-1;tabletWasHeld=false;
            if(pointerDot)pointerDot.gameObject.SetActive(false);
            if(pointerBeam)pointerBeam.enabled=false;
            if(focusOutline)focusOutline.gameObject.SetActive(false);
        }

        public void Point(Camera camera,bool pressed,bool held=false)
        {
            if(!camera||!isActiveAndEnabled||DeadlyJenga.Presentation.MenuAim.InputConsumed)return;
            if(!giveEquipmentPage)RestoreEquipmentWheelLayout();
            focus=null;
            foreach(var button in buttons)if(button)button.image.color=CandyHudVisuals.Card;
            ((RectTransform)transform).sizeDelta=new Vector2(600,1080);
            help.rectTransform.anchoredPosition=new Vector2(0,-480);
            bool controller=TabletInput(camera,ref pressed,ref held,out pointerRay);
            PlaceTablet(camera);
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

        bool DominantLeft=>session.Player?.gameObject.GetComponent<DeadlyJenga.Core.IBlasterControls>()?.LeftHanded
            ??(session as DeadlyJenga.Core.IBlasterControls)?.LeftHanded??false;

        bool ControllerPose(Camera camera,bool left,out Pose pose,out XRController selected)
            =>DeadlyJenga.Presentation.MenuAim.PhysicalPose(camera,left,out pose,out selected);
        bool TabletInput(Camera camera,ref bool pressed,ref bool held,out Ray ray)
        {
            bool left=DominantLeft;
            bool physical=ControllerPose(camera,left,out var pose,out var device);
            if(!physical)physical=ControllerPose(camera,!left,out pose,out device);
            int identity=physical?device.deviceId:-1;
            if(tabletDominantLeft!=left||tabletPointerDevice!=identity)
            {
                tabletDominantLeft=left;tabletPointerDevice=identity;tabletWasHeld=false;
                requireRelease=true;focus=pressedTarget=null;
            }
            if(physical)
            {
                // Aim and click come from the same chosen controller. The holding hand cannot steal them.
                var trigger=device.TryGetChildControl<ButtonControl>("triggerPressed");
                var axis=device.TryGetChildControl<AxisControl>("trigger");
                bool down=trigger!=null?trigger.isPressed:axis!=null&&axis.ReadValue()>.65f;
                held=down;pressed=down&&!tabletWasHeld;tabletWasHeld=down;
                ray=new Ray(pose.position,pose.rotation*Vector3.forward);return true;
            }
            tabletWasHeld=held;
            ray=new Ray(camera.transform.position,camera.transform.forward);return false;
        }
        void PlaceTablet(Camera camera)
        {
            DeadlyJenga.Presentation.MenuAim.PlaceTablet((RectTransform)transform,camera,session,DominantLeft);
            if(!tabletFrame)
            {
                tabletFrame=PointerImage("Tablet lavender bezel",new Vector2(640,1120));
                tabletFrame.type=Image.Type.Sliced;tabletFrame.fillCenter=false;tabletFrame.color=CandyHudVisuals.Lavender;
                tabletFrame.transform.SetAsFirstSibling();
                BuildTabletCase();
            }
        }

        void BuildTabletCase()
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)return;
            tabletCaseMaterial=new Material(shader){name="Tablet candy shell",hideFlags=HideFlags.DontSave};
            tabletCaseMaterial.SetColor("_BaseColor",CandyHudVisuals.Lavender);
            tabletCaseMaterial.SetFloat("_Smoothness",.8f);tabletCaseMaterial.SetFloat("_Metallic",.18f);
            const int count=32;
            var vertices=new Vector3[count*2+2];var triangles=new System.Collections.Generic.List<int>();
            for(int ring=0;ring<2;ring++)for(int i=0;i<count;i++)
            {
                int corner=i/8;float angle=(corner*90+i%8*90f/7)*Mathf.Deg2Rad;
                float x=(corner==0||corner==3?285:-285)+Mathf.Cos(angle)*35;
                float y=(corner<2?525:-525)+Mathf.Sin(angle)*35;
                vertices[ring*count+i]=new Vector3(x,y,ring==0?10:36);
            }
            vertices[count*2]=new Vector3(0,0,10);vertices[count*2+1]=new Vector3(0,0,36);
            for(int i=0;i<count;i++)
            {
                int next=(i+1)%count;
                triangles.AddRange(new[]{count*2,next,i,count*2+1,count+i,count+next,i,next,count+i,next,count+next,count+i});
            }
            tabletCaseMesh=new Mesh{name="Rounded tablet shell",hideFlags=HideFlags.DontSave};
            tabletCaseMesh.vertices=vertices;tabletCaseMesh.triangles=triangles.ToArray();tabletCaseMesh.RecalculateNormals();tabletCaseMesh.RecalculateBounds();
            var body=new GameObject("Visual - glossy tablet shell",typeof(MeshFilter),typeof(MeshRenderer));
            body.transform.SetParent(transform,false);body.layer=gameObject.layer;
            body.GetComponent<MeshFilter>().sharedMesh=tabletCaseMesh;
            var renderer=body.GetComponent<MeshRenderer>();renderer.sharedMaterial=tabletCaseMaterial;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        void OnDestroy()
        {
            if(tabletCaseMesh)Destroy(tabletCaseMesh);
            if(tabletCaseMaterial)Destroy(tabletCaseMaterial);
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
                    var beam=new GameObject("Dominant controller tablet ray");beam.transform.SetParent(transform,false);
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
