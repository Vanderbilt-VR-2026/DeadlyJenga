using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    public sealed class CandySeaSpirit : MonoBehaviour
    {
        bool bound;readonly Dictionary<Object,CandySeaSpirit> views=new Dictionary<Object,CandySeaSpirit>();readonly List<Object> expired=new List<Object>();float handScanAt;ISeaSpiritView view;float scanAt;Transform bust,head;readonly Transform[] arms=new Transform[8];Material water,armor,glow;Mesh torsoMesh,headMesh,finMesh,armMesh;Transform jaw,wakeColumn;LineRenderer wakeFoam;Light coreLight;readonly Transform[] shoulders=new Transform[2];readonly Vector3[] lastHands=new Vector3[2];readonly bool[] hasHand=new bool[2];
        readonly Dictionary<Renderer,Material[]> originals=new Dictionary<Renderer,Material[]>();
        readonly List<Renderer> observerBody = new List<Renderer>();
        sealed class ObserverMaskFrame
        {
            public Camera camera;
            public ShadowCastingMode[] saved, applied;
            public bool[] changed;
        }
        readonly List<ObserverMaskFrame> observerFrames = new List<ObserverMaskFrame>();
        int observerDepth, observerFrame = -1;
        Renderer torsoSurface, headSurface;
        void Update()
        {
            if(!bound){Discover();return;}
            if(view is Object obj&&!obj)view=null;
            bool active=view!=null&&view.Active;if(!active){if(bust)bust.parent.gameObject.SetActive(false);Restore();return;}
            if(!bust)Build();bust.parent.gameObject.SetActive(true);
            Vector3 eye=view.BustAnchor.position;float waterline=Services.World?.WaterHeight(eye)??0;
            float shoulderSpan=view.LeftShoulder&&view.RightShoulder?Vector3.Distance(view.LeftShoulder.position,view.RightShoulder.position):8;
            float torsoHeight=Mathf.Max(6,Mathf.Min((Services.World?.TopHeight??12)*.55f-waterline+3,Mathf.Max(8,shoulderSpan*1.9f)));
            bust.position=new Vector3(eye.x,eye.y-torsoHeight*.60f,eye.z);bust.rotation=Quaternion.Euler(0,view.BustAnchor.eulerAngles.y,0);bust.localScale=new Vector3(Mathf.Max(4.7f,shoulderSpan*1.08f),torsoHeight*1.2f*(1+Mathf.Sin(Time.time*.8f)*.012f),Mathf.Max(3.6f,shoulderSpan*.72f));
            float wakeHeight=Mathf.Max(0,bust.position.y-bust.localScale.y*.42f-waterline);
            wakeColumn.gameObject.SetActive(wakeHeight>.25f);wakeColumn.position=new Vector3(eye.x,waterline+wakeHeight*.5f,eye.z);wakeColumn.rotation=Quaternion.Euler(0,Time.time*9,0);wakeColumn.localScale=new Vector3(bust.localScale.x*.48f,wakeHeight+.5f,bust.localScale.z*.6f);
            for(int i=0;i<32;i++){float a=i*Mathf.PI*2/32,r=bust.localScale.x*.31f*(1+Mathf.Sin(a*5+Time.time*1.4f)*.045f);wakeFoam.SetPosition(i,new Vector3(eye.x+Mathf.Cos(a)*r,waterline+.08f+Mathf.Sin(a*4-Time.time*2)*.035f,eye.z+Mathf.Sin(a)*r));}
            float headHeight=Mathf.Clamp(shoulderSpan*.63f,3.5f,Mathf.Max(3.5f,torsoHeight*.46f));head.position=eye+Vector3.up*(headHeight*.42f);head.rotation=bust.rotation;head.localScale=new Vector3(headHeight*.86f,headHeight,headHeight*.8f);
            for(int side=0;side<2;side++)
            {
                var shoulder=side==0?view.LeftShoulder:view.RightShoulder;var hand=side==0?view.LeftHandAnchor:view.RightHandAnchor;if(!shoulder||!hand)continue;
                Vector3 start=shoulder.position,end=hand.position;
                shoulders[side].SetPositionAndRotation(start,bust.rotation*Quaternion.Euler(0,0,side==0?25:-25));shoulders[side].localScale=new Vector3(4,3,4);
                var wet=CandyWaterContactEffects.Current;
                if(wet){wet.HandSweep(hasHand[side]?lastHands[side]:end,end,1.1f);lastHands[side]=end;hasHand[side]=true;}
                for(int i=0;i<4;i++)
                {float a=i/4f,b=(i+1)/4f;Vector3 p=Vector3.Lerp(start,end,a)+Vector3.down*Mathf.Sin(a*Mathf.PI)*2,q=Vector3.Lerp(start,end,b)+Vector3.down*Mathf.Sin(b*Mathf.PI)*2;var segment=arms[side*4+i];segment.position=(p+q)*.5f;segment.rotation=Quaternion.FromToRotation(Vector3.up,q-p);float width=Mathf.Lerp(2.5f,.8f,(a+b)*.5f);segment.localScale=new Vector3(width,Vector3.Distance(p,q)*.53f+.18f,width);}
                if(Time.unscaledTime<handScanAt)continue;var visualRoot=hand.parent?hand.parent:hand;foreach(var r in visualRoot.GetComponentsInChildren<Renderer>())if(!originals.ContainsKey(r)){originals[r]=r.sharedMaterials;var values=new Material[r.sharedMaterials.Length];for(int i=0;i<values.Length;i++)values[i]=water;r.sharedMaterials=values;}
            }
            if(jaw)jaw.localRotation=Quaternion.Euler(5+Mathf.Sin(Time.time*.65f)*3,0,0);
            if(coreLight){coreLight.transform.position=bust.position+bust.forward*torsoHeight*.25f;coreLight.range=headHeight*3;}
            if(Time.unscaledTime>=handScanAt)handScanAt=Time.unscaledTime+1;
        }
        void BeforeObserverCamera(ScriptableRenderContext context, Camera camera)
        {
            if (observerBody.Count == 0) return;
            if (observerFrame != Time.frameCount)
            {
                RestoreObserverVisibility();
                observerFrame = Time.frameCount;
            }
            if (observerDepth == observerFrames.Count)
                observerFrames.Add(new ObserverMaskFrame
                {
                    saved = new ShadowCastingMode[observerBody.Count],
                    applied = new ShadowCastingMode[observerBody.Count],
                    changed = new bool[observerBody.Count]
                });
            var frame = observerFrames[observerDepth];
            frame.camera = camera;
            bool conceal = ObserverInsideBody(camera);
            for (int i = 0; i < observerBody.Count; i++)
            {
                var renderer = observerBody[i];
                if (!renderer) continue;
                var current = renderer.shadowCastingMode;
                if (observerDepth > 0 && current != observerFrames[observerDepth - 1].applied[i])
                    PreserveObserverMode(i, current, observerDepth);
                frame.saved[i] = current;
                frame.applied[i] = conceal ? ShadowCastingMode.ShadowsOnly : observerFrames[0].saved[i];
                frame.changed[i] = frame.applied[i] != current;
                if (frame.changed[i]) renderer.shadowCastingMode = frame.applied[i];
            }
            observerDepth++;
        }

        bool ObserverInsideBody(Camera camera)
        {
            var session = Services.Session;
            var actor = view as Component;
            if (!camera || !actor || !view.Active || !headSurface || !torsoSurface || session == null ||
                !NetworkActors.Alive(session.Hand) || session.Hand.gameObject != actor.gameObject ||
                camera != session.Hand.ViewCamera || camera != session.ActiveCamera) return false;
            float margin = Mathf.Max(.05f, camera.nearClipPlane);
            var headBounds = headSurface.bounds;
            var torsoBounds = torsoSurface.bounds;
            headBounds.Expand(margin * 2);
            torsoBounds.Expand(margin * 2);
            return headBounds.Contains(camera.transform.position) || torsoBounds.Contains(camera.transform.position);
        }

        void PreserveObserverMode(int index, ShadowCastingMode current, int ancestorCount)
        {
            for (int i = 0; i < ancestorCount; i++)
            {
                observerFrames[i].saved[index] = current;
                observerFrames[i].changed[index] = false;
            }
        }

        void RestoreObserverLevel()
        {
            var frame = observerFrames[--observerDepth];
            for (int i = 0; i < observerBody.Count; i++)
            {
                var renderer = observerBody[i];
                if (!renderer) continue;
                var current = renderer.shadowCastingMode;
                if (current != frame.applied[i]) PreserveObserverMode(i, current, observerDepth);
                else if (frame.changed[i]) renderer.shadowCastingMode = frame.saved[i];
            }
        }

        void AfterObserverCamera(ScriptableRenderContext context, Camera camera)
        {
            if (observerDepth == 0) return;
            if (observerFrames[observerDepth - 1].camera == camera) RestoreObserverLevel();
            else RestoreObserverVisibility();
        }

        void RestoreObserverVisibility()
        {
            while (observerDepth > 0) RestoreObserverLevel();
        }

        void Discover()
        {
            if(Time.unscaledTime<scanAt)return;scanAt=Time.unscaledTime+1;
            foreach(var c in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if(c is ISeaSpiritView candidate&&candidate.Active&&!views.ContainsKey(c))
                {var go=new GameObject("Visual - shared ocean spirit");go.transform.SetParent(transform,false);var child=go.AddComponent<CandySeaSpirit>();child.bound=true;child.view=candidate;views[c]=child;}
            expired.Clear();foreach(var pair in views)if(!pair.Key||pair.Value.view==null||!pair.Value.view.Active)expired.Add(pair.Key);
            foreach(var key in expired){if(views[key])Destroy(views[key].gameObject);views.Remove(key);}
        }
        void Build()
        {
            RestoreObserverVisibility();
            observerBody.Clear();
            observerFrames.Clear();
            var shader=Resources.Load<Shader>("CandyScenery/CandySpirit");water=new Material(shader?shader:Shader.Find("Universal Render Pipeline/Lit"));water.SetColor("_BaseColor",new Color(.025f,.65f,.8f));
            var root=new GameObject("Visual - ocean spirit bust and arms").transform;root.SetParent(transform,false);
            water.SetColor("_BaseColor",new Color(.010f,.086f,.14f));water.SetColor("_FoamColor",new Color(.19f,.76f,.94f));water.SetColor("_EmissionColor",new Color(.015f,.24f,.35f));
            armor=new Material(water);armor.SetColor("_BaseColor",new Color(.008f,.035f,.065f));armor.SetColor("_FoamColor",new Color(.12f,.41f,.53f));armor.SetFloat("_PlateStrength",.9f);
            glow=new Material(water);glow.SetColor("_BaseColor",new Color(.15f,.8f,.9f));glow.SetColor("_EmissionColor",new Color(.2f,1,1));glow.SetFloat("_PlateStrength",0);
            torsoMesh=CandyKaijuSculpt.Body(false);headMesh=CandyKaijuSculpt.Body(true);finMesh=CandyKaijuSculpt.Fin();armMesh=CandyKaijuSculpt.Arm();
            bust=Shape("Armored tidal thorax",root,torsoMesh,water);head=Shape("Crowned sea dragon skull",root,headMesh,water);
            wakeColumn=Shape("Rising tidal vortex",root,torsoMesh,water);
            var wakeObject=new GameObject("Visual - luminous waterline foam");wakeObject.transform.SetParent(root,false);wakeFoam=wakeObject.AddComponent<LineRenderer>();wakeFoam.sharedMaterial=glow;wakeFoam.useWorldSpace=true;wakeFoam.loop=true;wakeFoam.positionCount=32;wakeFoam.widthMultiplier=.16f;wakeFoam.shadowCastingMode=ShadowCastingMode.Off;
            for(int i=0;i<arms.Length;i++)arms[i]=Shape("Sculpted tidal arm",root,armMesh,water);
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1;shoulders[side]=Shape("Swept shoulder armor",root,finMesh,armor);
                Detail("Luminous eye",head,"Sphere.fbx",new Vector3(sign*.20f,.08f,.35f),new Vector3(.14f,.065f,.075f),glow);
                var brow=Detail("Heavy brow ridge",head,finMesh,new Vector3(sign*.22f,.16f,.34f),new Vector3(.31f,.22f,.22f),armor);brow.localRotation=Quaternion.Euler(0,0,sign*18);
                var crown=Detail("Swept crown horn",head,finMesh,new Vector3(sign*.29f,.35f,-.06f),new Vector3(.35f,.85f,.65f),armor);crown.localRotation=Quaternion.Euler(-15,sign*20,sign*-24);
                for(int i=0;i<3;i++)
                {
                    var gill=Detail("Layered gill fin",head,finMesh,new Vector3(sign*(.33f+i*.04f),-.05f-i*.13f,-.05f),new Vector3(.32f,.44f,.4f),i==1?glow:armor);gill.localRotation=Quaternion.Euler(0,sign*55,sign*-70);
                    Detail("Bioluminescent rib",bust,"Capsule.fbx",new Vector3(sign*(.18f+i*.045f),.19f-i*.14f,.31f),new Vector3(.025f,.08f,.025f),glow).localRotation=Quaternion.Euler(0,0,sign*48);
                }
            }
            jaw=Detail("Articulated armored jaw",head,headMesh,new Vector3(0,-.24f,.24f),new Vector3(.73f,.26f,.68f),armor);
            for(int i=0;i<8;i++)Detail("Crystal tooth",head,finMesh,new Vector3((i-3.5f)*.065f,-.18f,.405f-Mathf.Abs(i-3.5f)*.012f),new Vector3(.060f,.11f+Mathf.Abs(i-3.5f)*.016f,.10f),glow).localRotation=Quaternion.Euler(0,0,180);
            for(int i=0;i<5;i++){var fin=Detail("Dorsal tidal spine",bust,finMesh,new Vector3(0,.35f-i*.17f,-.34f),new Vector3(.22f,.38f,.45f),i%2==0?glow:armor);fin.localRotation=Quaternion.Euler(-45,0,0);}
            Detail("Tidal heart",bust,"Sphere.fbx",new Vector3(0,.15f,.37f),new Vector3(.15f,.21f,.07f),glow);
            torsoSurface = bust.GetComponent<Renderer>();
            headSurface = head.GetComponent<Renderer>();
            observerBody.AddRange(bust.GetComponentsInChildren<Renderer>(true));
            observerBody.AddRange(head.GetComponentsInChildren<Renderer>(true));
            var lightObject=new GameObject("Visual - tidal heart light");lightObject.transform.SetParent(root,false);coreLight=lightObject.AddComponent<Light>();coreLight.type=LightType.Point;coreLight.color=new Color(.08f,.65f,1);coreLight.intensity=1.4f;coreLight.shadows=LightShadows.None;
        }
        Transform Shape(string name,Transform parent,string mesh)
        {var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>(mesh);var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=water;r.shadowCastingMode=ShadowCastingMode.Off;return go.transform;}
        Transform Shape(string name,Transform parent,Mesh mesh,Material material)
        {var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.On;return go.transform;}
        Transform Detail(string name,Transform parent,string mesh,Vector3 position,Vector3 scale,Material material)=>Detail(name,parent,Resources.GetBuiltinResource<Mesh>(mesh),position,scale,material);
        Transform Detail(string name,Transform parent,Mesh mesh,Vector3 position,Vector3 scale,Material material)
        {var item=Shape(name,parent,mesh,material);item.localPosition=position;item.localScale=scale;return item;}
        void Restore(){foreach(var p in originals)if(p.Key)p.Key.sharedMaterials=p.Value;originals.Clear();}
        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeforeObserverCamera;
            RenderPipelineManager.endCameraRendering += AfterObserverCamera;
            if (!bound) foreach (var child in views.Values)
                if (child) child.gameObject.SetActive(true);
        }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeObserverCamera;
            RenderPipelineManager.endCameraRendering -= AfterObserverCamera;
            RestoreObserverVisibility();
            if (!bound) foreach (var child in views.Values)
                if (child) child.gameObject.SetActive(false);
            Restore();
            for (int i = 0; i < 2; i++) hasHand[i] = false;
            if (bust) bust.parent.gameObject.SetActive(false);
        }
        void OnDestroy(){Restore();if(water)Destroy(water);if(armor)Destroy(armor);if(glow)Destroy(glow);if(torsoMesh)Destroy(torsoMesh);if(headMesh)Destroy(headMesh);if(finMesh)Destroy(finMesh);if(armMesh)Destroy(armMesh);}
    }
}
