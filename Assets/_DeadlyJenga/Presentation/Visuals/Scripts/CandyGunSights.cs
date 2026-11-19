using DeadlyJenga.Core;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace DeadlyJenga.Presentation
{
    // Zoom is local presentation. The host still resolves every shot from the actual muzzle.
    [DefaultExecutionOrder(420)]
    public sealed class CandyGunSights:MonoBehaviour
    {
        Camera eye,scopeCamera;RenderTexture texture;Material lensMaterial;GameObject lens;
        Transform optic,aperture,owner;EquipmentKind lastKind;bool lastShotgun;
        float originalFov;bool zoomed,visible,aiming,vr;float nextRender,findAfter;
        EquipmentState state;
        Renderer[] scopeHidden;bool[] scopeWasHidden;
        Renderer lensRenderer;readonly Stack<bool> cameraVisibility=new Stack<bool>();
        bool missingShaderReported;float shaderRetry;
        void OnEnable(){RenderPipelineManager.beginCameraRendering+=BeginCamera;RenderPipelineManager.endCameraRendering+=EndCamera;}
        void BeginCamera(ScriptableRenderContext context,Camera camera)
        {
            if(!lensRenderer)return;
            cameraVisibility.Push(lensRenderer.forceRenderingOff);
            lensRenderer.forceRenderingOff=camera!=eye;
        }
        void EndCamera(ScriptableRenderContext context,Camera camera)
        {
            if(lensRenderer&&cameraVisibility.Count>0)lensRenderer.forceRenderingOff=cameraVisibility.Pop();
        }
        public const float BinocularDistance=2, BinocularRadius=.6f;
        public static float BinocularFov(float magnification)=>2*Mathf.Atan(BinocularRadius/BinocularDistance/Mathf.Max(1,magnification))*Mathf.Rad2Deg;
        public static float ApertureFov(float radius,float relief,float magnification)=>
            Mathf.Clamp(2*Mathf.Atan(Mathf.Max(.0001f,radius)/Mathf.Max(.01f,relief)/Mathf.Max(1,magnification))*Mathf.Rad2Deg,.1f,150);
        public static float CommonApertureRadius(float halfWidth,float halfHeight,float halfEyeSpacing)
        {
            float a=Mathf.Max(halfWidth,halfHeight),b=Mathf.Min(halfWidth,halfHeight),c=Mathf.Abs(halfEyeSpacing);
            if(c>=a)return 0;
            float difference=a*a-b*b;
            if(difference<.0000001f||c>=difference/a)return a-c;
            return b*Mathf.Sqrt(Mathf.Max(0,1-c*c/difference));
        }
        public bool Aiming=>aiming;
        public float Magnification=>state?.kind switch
        {
            EquipmentKind.DualRifle=>state.shotgun?1:4,
            EquipmentKind.Blaster=>1.5f,
            EquipmentKind.SheriffHammer=>2,
            _=>1.25f
        };
        public bool DrawsScopeOverlay=>visible&&aiming&&!vr&&state.kind==EquipmentKind.DualRifle&&!state.shotgun;

        public static bool SupportsAim(EquipmentState equipment)
        {
            if(equipment==null||equipment.kind==EquipmentKind.None)return false;
            var spec=EquipmentSpecifications.Get(equipment.kind);
            return !spec.power&&(!spec.melee||equipment.shotgun&&
                (equipment.kind==EquipmentKind.Chainsaw||equipment.kind==EquipmentKind.LaserAxe));
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=go=>{if(!go.GetComponent<CandyGunSights>())go.AddComponent<CandyGunSights>();};
        void LateUpdate()
        {
            var session=Services.Session;var player=session?.Player;var camera=session?.ActiveCamera;
            if(camera!=eye){RestoreFov();if(lens)lens.SetActive(false);eye=camera;originalFov=eye?eye.fieldOfView:60;}
            vr=session?.IsVR==true;state=player?.gameObject.GetComponent<IEquipmentControls>()?.Equipment;
            visible=eye&&player!=null&&player.gameObject.GetComponent<IModeActor>()?.Alive!=false&&!player.InputSuspended&&!session.MenuOpen&&!session.PlayingHand&&state?.drawn==true&&SupportsAim(state);
            aiming=visible&&!vr&&(Mouse.current?.rightButton.isPressed==true||DeadlyJenga.Presentation.MenuGamepad.Current?.leftTrigger.isPressed==true);
            if(!vr&&aiming){if(!zoomed)originalFov=eye.fieldOfView;zoomed=true;eye.fieldOfView=Mathf.Lerp(eye.fieldOfView,originalFov/Magnification,1-Mathf.Exp(-20*Time.unscaledDeltaTime));}
            else RestoreFov();
            if(!visible||!vr){if(lens)lens.SetActive(false);return;}
            if(state.kind!=EquipmentKind.Blaster&&state.kind!=EquipmentKind.DualRifle&&state.kind!=EquipmentKind.SheriffHammer)
            {
                if(lens)lens.SetActive(false);
                if(player.gameObject.GetComponent<IWeaponAim>() is IWeaponAim weapon)
                    aiming=OpenSightAligned(eye.transform.position,eye.transform.forward,weapon.WeaponAim,
                        state,player.gameObject.GetComponent<IMovementState>()?.Scale??1);
                return;
            }
            if(owner!=player.transform||lastKind!=state.kind||lastShotgun!=state.shotgun||!optic)
            {
                if(lens)lens.SetActive(false);if(Time.unscaledTime<findAfter)return;findAfter=Time.unscaledTime+.2f;
                owner=player.transform;lastKind=state.kind;lastShotgun=state.shotgun;optic=aperture=null;
                string name=state.kind==EquipmentKind.Blaster?"Blaster optic":state.kind==EquipmentKind.SheriffHammer?"Revolver optic":state.shotgun?"Shotgun optic":"Rifle optic";
                foreach(var t in owner.GetComponentsInChildren<Transform>())if(t.name==name){optic=t;break;}
                if(optic)aperture=optic.Find("Optic lens");
                ReleaseLens();scopeHidden=owner.GetComponentsInChildren<Renderer>(true);scopeWasHidden=new bool[scopeHidden.Length];
            }
            if(!optic||!aperture)return;
            float scale=Mathf.Abs(optic.lossyScale.z);
            aiming=EyeBoxContains(eye.transform.position,eye.transform.forward,optic.position,optic.forward,scale);
            if(state.kind==EquipmentKind.DualRifle&&state.shotgun){if(lens)lens.SetActive(false);return;}
            if(!aiming){if(lens)lens.SetActive(false);return;}
            // These mounts mark the center of the existing model opening, not its size.
            float radius=state.kind==EquipmentKind.DualRifle?.027f:state.kind==EquipmentKind.Blaster?.021f:.018f;
            float halfWidth=radius*Mathf.Abs(aperture.lossyScale.x),halfHeight=radius*Mathf.Abs(aperture.lossyScale.y);
            var eyeOffset=eye.transform.position-aperture.position;
            float relief=-Vector3.Dot(eyeOffset,aperture.forward);
            if(halfWidth<.001f||halfHeight<.001f||relief<=eye.nearClipPlane||
                Mathf.Abs(Vector3.Dot(eyeOffset,aperture.right))>Mathf.Max(.025f,halfWidth)||
                Mathf.Abs(Vector3.Dot(eyeOffset,aperture.up))>Mathf.Max(.025f,halfHeight))
            {aiming=false;if(lens)lens.SetActive(false);return;}
            EnsureLens();
            if(!lens)return;
            lens.transform.SetParent(aperture,false);
            lens.transform.localPosition=Vector3.zero;
            lens.transform.localRotation=Quaternion.identity;
            lens.transform.localScale=new Vector3(radius,radius,1);
            lens.SetActive(true);
            if(Time.unscaledTime>=nextRender)
            {
                nextRender=Time.unscaledTime+1f/60;
                var weapon=player.gameObject.GetComponent<IWeaponAim>();
                var muzzle=weapon!=null?weapon.WeaponAim:new Pose(optic.position,optic.rotation);
                var target=muzzle.position+muzzle.rotation*Vector3.forward*120;
                scopeCamera.transform.SetPositionAndRotation(eye.transform.position,
                    Quaternion.LookRotation(target-eye.transform.position,eye.transform.up));
                scopeCamera.aspect=halfWidth/Mathf.Max(.0001f,halfHeight);
                scopeCamera.fieldOfView=ApertureFov(halfHeight,relief,Magnification);scopeCamera.cullingMask=eye.cullingMask&~(1<<2);
                try
                {
                    for(int i=0;i<scopeHidden.Length;i++)if(scopeHidden[i])
                    {scopeWasHidden[i]=scopeHidden[i].forceRenderingOff;scopeHidden[i].forceRenderingOff=true;}
                    scopeCamera.Render();
                }
                finally
                {
                    for(int i=0;i<scopeHidden.Length;i++)if(scopeHidden[i])scopeHidden[i].forceRenderingOff=scopeWasHidden[i];
                }
            }
        }
        public static bool OpenSightAligned(Vector3 eye,Vector3 view,Pose muzzle)
        {
            Vector3 direction=muzzle.rotation*Vector3.forward;
            Vector3 offset=muzzle.position-eye;
            float depth=Vector3.Dot(offset,direction);
            return depth>.02f&&depth<1.2f&&Vector3.Dot(view,direction)>.97f&&
                (offset-direction*depth).sqrMagnitude<.18f*.18f;
        }
        public static bool OpenSightAligned(Vector3 eye,Vector3 view,Pose muzzle,EquipmentState equipment,float scale)
        {
            if(!SupportsAim(equipment))return false;
            scale=Mathf.Max(.1f,scale);
            // A launcher or rotary gun is shouldered beside the eye, not centered in it.
            var spec=EquipmentSpecifications.Get(equipment.kind);
            float radius=(spec.twoHanded||spec.melee?.38f:.24f)*scale;
            Vector3 direction=muzzle.rotation*Vector3.forward;
            Vector3 offset=muzzle.position-eye;
            float depth=Vector3.Dot(offset,direction);
            return depth>.02f*scale&&depth<1.6f*scale&&Vector3.Dot(view,direction)>.97f&&
                (offset-direction*depth).sqrMagnitude<radius*radius;
        }
        public static bool EyeBoxContains(Vector3 eye,Vector3 view,Vector3 rear,Vector3 forward,float scale)
        {
            scale=Mathf.Max(.1f,Mathf.Abs(scale));
            Vector3 delta=rear-eye;
            float relief=Vector3.Dot(delta,forward);
            // Human eye spacing does not shrink with the avatar. Allow natural wrist clearance in front of the face.
            float radius=Mathf.Max(.06f,.055f*scale);
            float minimumRelief=Mathf.Max(.04f,.025f*scale);
            float maximumRelief=Mathf.Max(.45f,.65f*scale);
            return relief>=minimumRelief&&relief<=maximumRelief&&
                (delta-forward*relief).sqrMagnitude<=radius*radius&&Vector3.Dot(view,forward)>.985f;
        }
        void EnsureLens()
        {
            if(!scopeCamera)
            {
                if(Time.unscaledTime<shaderRetry)return;
                var shader=Resources.Load<Shader>("CandyBinocularScope");
                if(!shader)
                {
                    shaderRetry=Time.unscaledTime+1;
                    if(!missingShaderReported){Debug.LogError("Binocular scope shader is missing.",this);missingShaderReported=true;}
                    return;
                }
                var go=new GameObject("Local scope camera");go.transform.SetParent(transform,false);scopeCamera=go.AddComponent<Camera>();scopeCamera.enabled=false;
                scopeCamera.stereoTargetEye=StereoTargetEyeMask.None;scopeCamera.GetUniversalAdditionalCameraData().allowXRRendering=false;scopeCamera.aspect=1;scopeCamera.nearClipPlane=.06f;scopeCamera.farClipPlane=500;
                scopeCamera.allowHDR=false;scopeCamera.allowMSAA=false;
                texture=new RenderTexture(384,384,16){name="Local scope view",wrapMode=TextureWrapMode.Clamp};texture.Create();scopeCamera.targetTexture=texture;
                lensMaterial=new Material(shader);lensMaterial.SetTexture("_BaseMap",texture);
            }
            if(lens||!lensMaterial)return;
            lens=new GameObject("Local magnifying lens",typeof(MeshFilter),typeof(MeshRenderer));lens.layer=2;
            float radius=1;
            const int segments=48;var vertices=new Vector3[segments+1];var uv=new Vector2[segments+1];var triangles=new int[segments*3];uv[0]=new Vector2(.5f,.5f);
            for(int i=0;i<segments;i++){float a=i*Mathf.PI*2/segments;vertices[i+1]=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius;uv[i+1]=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.5f+Vector2.one*.5f;triangles[i*3]=0;triangles[i*3+1]=(i+1)%segments+1;triangles[i*3+2]=i+1;}
            var mesh=new Mesh{name="Scope lens disc",vertices=vertices,uv=uv,triangles=triangles};mesh.RecalculateNormals();lens.GetComponent<MeshFilter>().sharedMesh=mesh;lensRenderer=lens.GetComponent<MeshRenderer>();lensRenderer.sharedMaterial=lensMaterial;
            lensRenderer.shadowCastingMode=ShadowCastingMode.Off;lensRenderer.receiveShadows=false;
        }
        void OnGUI()
        {
            if(!DrawsScopeOverlay)return;
            float x=Screen.width*.5f,y=Screen.height*.5f;
            var old=GUI.color;
            {
                float r=Screen.height*.42f;GUI.color=Color.black;
                // Raster strips form a circular scope mask without an extra full-screen render pass.
                for(int i=0;i<64;i++){float dy=(i+.5f)*Screen.height/64-y;float half=Mathf.Abs(dy)<r?Mathf.Sqrt(r*r-dy*dy):0;float h=Screen.height/64f+1;GUI.DrawTexture(new Rect(0,i*Screen.height/64f,x-half,h),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(x+half,i*Screen.height/64f,Screen.width-x-half,h),Texture2D.whiteTexture);}
                GUI.color=Color.white;Line(x-r,y,r*2,1);Line(x,y-r,1,r*2);
                for(int i=-3;i<=3;i++)if(i!=0){Line(x-4,y+i*25,9,1);Line(x+i*25,y-4,1,9);}
            }
            GUI.color=old;
        }
        static void Line(float x,float y,float width,float height)=>GUI.DrawTexture(new Rect(x,y,width,height),Texture2D.whiteTexture);
        void RestoreFov(){if(zoomed&&eye)eye.fieldOfView=originalFov;zoomed=false;}
        void OnDisable(){RenderPipelineManager.beginCameraRendering-=BeginCamera;RenderPipelineManager.endCameraRendering-=EndCamera;cameraVisibility.Clear();if(lensRenderer)lensRenderer.forceRenderingOff=false;RestoreFov();aiming=visible=false;if(lens)lens.SetActive(false);}
        void ReleaseLens(){if(lens){lens.SetActive(false);Destroy(lens.GetComponent<MeshFilter>().sharedMesh);Destroy(lens);}lens=null;lensRenderer=null;cameraVisibility.Clear();}
        void OnDestroy(){RestoreFov();ReleaseLens();if(scopeCamera)Destroy(scopeCamera.gameObject);if(texture){texture.Release();Destroy(texture);}if(lensMaterial)Destroy(lensMaterial);}
    }
}
