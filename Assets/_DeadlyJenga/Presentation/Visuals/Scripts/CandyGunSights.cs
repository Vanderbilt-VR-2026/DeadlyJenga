using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadlyJenga.Presentation
{
    // Zoom is local presentation. The host still resolves every shot from the actual muzzle.
    [DefaultExecutionOrder(420)]
    public sealed class CandyGunSights:MonoBehaviour
    {
        Camera eye,scopeCamera;RenderTexture texture;Material lensMaterial;GameObject lens;
        Transform optic,owner;EquipmentKind lastKind;bool lastShotgun;
        InputAction grip;float originalFov;bool zoomed,visible,aiming,vr;float nextRender,findAfter;
        EquipmentState state;
        public bool Aiming=>aiming;
        public float Magnification=>state?.kind==EquipmentKind.DualRifle?(state.shotgun?1:4):state?.kind==EquipmentKind.Blaster?1.5f:2;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=go=>{if(!go.GetComponent<CandyGunSights>())go.AddComponent<CandyGunSights>();};
        void Awake(){grip=new InputAction("Aim / scope",InputActionType.Button);grip.AddBinding("<XRController>{LeftHand}/grip");grip.AddBinding("<XRController>{RightHand}/grip");grip.Enable();}
        void LateUpdate()
        {
            var session=Services.Session;var player=session?.Player;var camera=session?.ActiveCamera;
            if(camera!=eye){RestoreFov();eye=camera;originalFov=eye?eye.fieldOfView:60;}
            vr=session?.IsVR==true;state=player?.gameObject.GetComponent<IEquipmentControls>()?.Equipment;
            visible=eye&&player!=null&&player.gameObject.GetComponent<IModeActor>()?.Alive!=false&&!player.InputSuspended&&session.MenuOpen==false&&state?.drawn==true&&
                (state.kind==EquipmentKind.Blaster||state.kind==EquipmentKind.DualRifle||state.kind==EquipmentKind.SheriffHammer);
            aiming=visible&&(vr?grip.IsPressed():Mouse.current?.rightButton.isPressed==true);
            if(!vr&&aiming){if(!zoomed)originalFov=eye.fieldOfView;zoomed=true;eye.fieldOfView=Mathf.Lerp(eye.fieldOfView,originalFov/Magnification,1-Mathf.Exp(-20*Time.unscaledDeltaTime));}
            else RestoreFov();
            if(!visible||!vr){if(lens)lens.SetActive(false);return;}
            if(owner!=player.transform||lastKind!=state.kind||lastShotgun!=state.shotgun||!optic)
            {
                if(Time.unscaledTime<findAfter)return;findAfter=Time.unscaledTime+.2f;
                owner=player.transform;lastKind=state.kind;lastShotgun=state.shotgun;optic=null;
                string name=state.kind==EquipmentKind.Blaster?"Blaster optic":state.kind==EquipmentKind.SheriffHammer?"Revolver optic":state.shotgun?"Shotgun optic":"Rifle optic";
                foreach(var t in owner.GetComponentsInChildren<Transform>())if(t.name==name){optic=t;break;}
                ReleaseLens();
            }
            if(!optic)return;
            if(state.kind==EquipmentKind.DualRifle&&state.shotgun){if(lens)lens.SetActive(false);return;}
            // A magnifying lens only renders while close to and aligned with an eye; XR projection is untouched.
            Vector3 delta=optic.position-eye.transform.position;
            bool aligned=aiming&&delta.magnitude<.65f&&Vector3.Dot(eye.transform.forward,optic.forward)>.94f&&Vector3.Dot(delta,optic.forward)>0;
            if(!aligned){if(lens)lens.SetActive(false);return;}
            EnsureLens();lens.SetActive(true);
            if(Time.unscaledTime>=nextRender)
            {
                nextRender=Time.unscaledTime+1f/30;
                scopeCamera.transform.SetPositionAndRotation(optic.position+optic.forward*.02f,optic.rotation);
                scopeCamera.fieldOfView=50/Magnification;scopeCamera.cullingMask=eye.cullingMask&~(1<<2);
                scopeCamera.Render();
            }
        }
        void EnsureLens()
        {
            if(!scopeCamera)
            {
                var go=new GameObject("Local scope camera");go.transform.SetParent(transform,false);scopeCamera=go.AddComponent<Camera>();scopeCamera.enabled=false;
                scopeCamera.stereoTargetEye=StereoTargetEyeMask.None;scopeCamera.nearClipPlane=.06f;scopeCamera.farClipPlane=500;
                scopeCamera.allowHDR=false;scopeCamera.allowMSAA=false;
                texture=new RenderTexture(384,384,16){name="Local scope view"};texture.Create();scopeCamera.targetTexture=texture;
                lensMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));lensMaterial.SetTexture("_BaseMap",texture);lensMaterial.SetFloat("_Cull",0);
            }
            if(lens)return;
            lens=new GameObject("Local magnifying lens",typeof(MeshFilter),typeof(MeshRenderer));lens.layer=2;lens.transform.SetParent(optic,false);lens.transform.localPosition=new Vector3(0,0,.01f);
            float radius=state.kind==EquipmentKind.DualRifle&&!state.shotgun?.030f:.018f;
            const int segments=48;var vertices=new Vector3[segments+1];var uv=new Vector2[segments+1];var triangles=new int[segments*3];uv[0]=new Vector2(.5f,.5f);
            for(int i=0;i<segments;i++){float a=i*Mathf.PI*2/segments;vertices[i+1]=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius;uv[i+1]=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.5f+Vector2.one*.5f;triangles[i*3]=0;triangles[i*3+1]=(i+1)%segments+1;triangles[i*3+2]=i+1;}
            var mesh=new Mesh{name="Scope lens disc",vertices=vertices,uv=uv,triangles=triangles};mesh.RecalculateNormals();lens.GetComponent<MeshFilter>().sharedMesh=mesh;lens.GetComponent<MeshRenderer>().sharedMaterial=lensMaterial;
        }
        void OnGUI()
        {
            if(!visible||vr)return;
            float x=Screen.width*.5f,y=Screen.height*.5f;bool rifle=state.kind==EquipmentKind.DualRifle&&!state.shotgun;
            var old=GUI.color;GUI.color=state.shotgun?Color.red:Color.cyan;
            if(aiming&&rifle)
            {
                float r=Screen.height*.42f;GUI.color=Color.black;
                // Raster strips form a circular scope mask without an extra full-screen render pass.
                for(int i=0;i<64;i++){float dy=(i+.5f)*Screen.height/64-y;float half=Mathf.Abs(dy)<r?Mathf.Sqrt(r*r-dy*dy):0;float h=Screen.height/64f+1;GUI.DrawTexture(new Rect(0,i*Screen.height/64f,x-half,h),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(x+half,i*Screen.height/64f,Screen.width-x-half,h),Texture2D.whiteTexture);}
                GUI.color=Color.white;Line(x-r,y,r*2,1);Line(x,y-r,1,r*2);
                for(int i=-3;i<=3;i++)if(i!=0){Line(x-4,y+i*25,9,1);Line(x+i*25,y-4,1,9);}
            }
            else if(state.shotgun){GUI.DrawTexture(new Rect(x-2,y-2,4,4),Texture2D.whiteTexture);}
            else {Line(x-11,y,8,1);Line(x+4,y,8,1);Line(x,y-11,1,8);Line(x,y+4,1,8);}
            GUI.color=old;
        }
        static void Line(float x,float y,float width,float height)=>GUI.DrawTexture(new Rect(x,y,width,height),Texture2D.whiteTexture);
        void RestoreFov(){if(zoomed&&eye)eye.fieldOfView=originalFov;zoomed=false;}
        void OnDisable(){RestoreFov();aiming=visible=false;if(lens)lens.SetActive(false);grip?.Disable();}
        void OnEnable()=>grip?.Enable();
        void ReleaseLens(){if(lens){Destroy(lens.GetComponent<MeshFilter>().sharedMesh);Destroy(lens);}lens=null;}
        void OnDestroy(){RestoreFov();grip?.Dispose();ReleaseLens();if(scopeCamera)Destroy(scopeCamera.gameObject);if(texture){texture.Release();Destroy(texture);}if(lensMaterial)Destroy(lensMaterial);}
    }
}
