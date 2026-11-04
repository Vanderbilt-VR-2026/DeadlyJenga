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
        float originalFov;bool zoomed,visible,aiming,vr;float nextRender,findAfter;
        EquipmentState state;
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
            if(camera!=eye){RestoreFov();eye=camera;originalFov=eye?eye.fieldOfView:60;}
            vr=session?.IsVR==true;state=player?.gameObject.GetComponent<IEquipmentControls>()?.Equipment;
            visible=eye&&player!=null&&player.gameObject.GetComponent<IModeActor>()?.Alive!=false&&!player.InputSuspended&&!session.MenuOpen&&!session.PlayingHand&&state?.drawn==true&&SupportsAim(state);
            aiming=visible&&!vr&&Mouse.current?.rightButton.isPressed==true;
            if(!vr&&aiming){if(!zoomed)originalFov=eye.fieldOfView;zoomed=true;eye.fieldOfView=Mathf.Lerp(eye.fieldOfView,originalFov/Magnification,1-Mathf.Exp(-20*Time.unscaledDeltaTime));}
            else RestoreFov();
            if(!visible||!vr){if(lens)lens.SetActive(false);return;}
            if(state.kind!=EquipmentKind.Blaster&&state.kind!=EquipmentKind.DualRifle&&state.kind!=EquipmentKind.SheriffHammer)
            {
                if(lens)lens.SetActive(false);
                if(player.gameObject.GetComponent<IWeaponAim>() is IWeaponAim weapon)
                    aiming=OpenSightAligned(eye.transform.position,eye.transform.forward,weapon.WeaponAim);
                return;
            }
            if(owner!=player.transform||lastKind!=state.kind||lastShotgun!=state.shotgun||!optic)
            {
                if(lens)lens.SetActive(false);if(Time.unscaledTime<findAfter)return;findAfter=Time.unscaledTime+.2f;
                owner=player.transform;lastKind=state.kind;lastShotgun=state.shotgun;optic=null;
                string name=state.kind==EquipmentKind.Blaster?"Blaster optic":state.kind==EquipmentKind.SheriffHammer?"Revolver optic":state.shotgun?"Shotgun optic":"Rifle optic";
                foreach(var t in owner.GetComponentsInChildren<Transform>())if(t.name==name){optic=t;break;}
                ReleaseLens();
            }
            if(!optic)return;
            // A magnifying lens only renders while close to and aligned with an eye; XR projection is untouched.
            float scale=Mathf.Abs(optic.lossyScale.z);
            Vector3 eyePosition=eye.transform.position;
            if(eye.stereoEnabled)
            {
                var left=(Vector3)eye.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse.GetColumn(3);
                var right=(Vector3)eye.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse.GetColumn(3);
                eyePosition=EyeOffset(left,optic)<EyeOffset(right,optic)?left:right;
            }
            bool aligned=EyeBoxContains(eyePosition,eye.transform.forward,optic.position,optic.forward,scale);
            aiming=aligned;
            if(state.kind==EquipmentKind.DualRifle&&state.shotgun)return;
            if(!aligned){if(lens)lens.SetActive(false);return;}
            EnsureLens();lens.SetActive(true);
            if(Time.unscaledTime>=nextRender)
            {
                nextRender=Time.unscaledTime+1f/30;
                scopeCamera.transform.SetPositionAndRotation(optic.TransformPoint(Vector3.forward*(state.kind==EquipmentKind.DualRifle?.255f:.02f)),optic.rotation);
                scopeCamera.fieldOfView=50/Magnification;scopeCamera.cullingMask=eye.cullingMask&~(1<<2);
                scopeCamera.Render();
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
        static float EyeOffset(Vector3 eye,Transform optic)=>Vector3.ProjectOnPlane(eye-optic.position,optic.forward).sqrMagnitude;
        public static bool EyeBoxContains(Vector3 eye,Vector3 view,Vector3 rear,Vector3 forward,float scale)
        {
            Vector3 delta=rear-eye;
            float relief=Vector3.Dot(delta,forward);
            return relief>=.025f*scale&&relief<=.30f*scale&&
                (delta-forward*relief).sqrMagnitude<=.033f*.033f*scale*scale&&Vector3.Dot(view,forward)>.94f;
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
            lens=new GameObject("Local magnifying lens",typeof(MeshFilter),typeof(MeshRenderer));lens.layer=2;lens.transform.SetParent(optic,false);lens.transform.localPosition=new Vector3(0,0,-.015f);
            float radius=state.kind==EquipmentKind.DualRifle&&!state.shotgun?.027f:.018f;
            const int segments=48;var vertices=new Vector3[segments+1];var uv=new Vector2[segments+1];var triangles=new int[segments*3];uv[0]=new Vector2(.5f,.5f);
            for(int i=0;i<segments;i++){float a=i*Mathf.PI*2/segments;vertices[i+1]=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius;uv[i+1]=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.5f+Vector2.one*.5f;triangles[i*3]=0;triangles[i*3+1]=(i+1)%segments+1;triangles[i*3+2]=i+1;}
            var mesh=new Mesh{name="Scope lens disc",vertices=vertices,uv=uv,triangles=triangles};mesh.RecalculateNormals();lens.GetComponent<MeshFilter>().sharedMesh=mesh;lens.GetComponent<MeshRenderer>().sharedMaterial=lensMaterial;
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
        void OnDisable(){RestoreFov();aiming=visible=false;if(lens)lens.SetActive(false);}
        void ReleaseLens(){if(lens){Destroy(lens.GetComponent<MeshFilter>().sharedMesh);Destroy(lens);}lens=null;}
        void OnDestroy(){RestoreFov();ReleaseLens();if(scopeCamera)Destroy(scopeCamera.gameObject);if(texture){texture.Release();Destroy(texture);}if(lensMaterial)Destroy(lensMaterial);}
    }
}
