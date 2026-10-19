using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    public sealed class CandySeaSpirit : MonoBehaviour
    {
        bool bound;readonly Dictionary<Object,CandySeaSpirit> views=new Dictionary<Object,CandySeaSpirit>();readonly List<Object> expired=new List<Object>();float handScanAt;ISeaSpiritView view;float scanAt;Transform bust,head;readonly Transform[] arms=new Transform[8];Material water;
        readonly Dictionary<Renderer,Material[]> originals=new Dictionary<Renderer,Material[]>();
        void Update()
        {
            if(!bound){Discover();return;}
            if(view is Object obj&&!obj)view=null;
            if(view==null&&Time.unscaledTime>=scanAt&&(Services.Session as ISeaSpiritControls)?.SeaSpiritActive==true)
            {scanAt=Time.unscaledTime+1;foreach(var c in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))if(c is ISeaSpiritView found){view=found;break;}}
            bool active=view!=null&&view.Active;if(!active){if(bust)bust.parent.gameObject.SetActive(false);Restore();return;}
            if(!bust)Build();bust.parent.gameObject.SetActive(true);
            Vector3 eye=view.BustAnchor.position;float waterline=Services.World?.WaterHeight(eye)??0;float shoulders=(view.LeftShoulder.position.y+view.RightShoulder.position.y)*.5f;float torsoHeight=Mathf.Max(6,shoulders-waterline+3);
            bust.position=new Vector3(eye.x,waterline+torsoHeight*.42f,eye.z);bust.rotation=Quaternion.Euler(0,view.BustAnchor.eulerAngles.y,0);bust.localScale=new Vector3(Mathf.Max(8,torsoHeight*.45f),torsoHeight*1.2f,Mathf.Max(5,torsoHeight*.27f));
            float headHeight=Mathf.Max(5,torsoHeight*.25f);head.position=eye+Vector3.up*(headHeight*.42f);head.localScale=new Vector3(headHeight*.86f,headHeight,headHeight*.8f);
            for(int side=0;side<2;side++)
            {
                var shoulder=side==0?view.LeftShoulder:view.RightShoulder;var hand=side==0?view.LeftHandAnchor:view.RightHandAnchor;if(!shoulder||!hand)continue;
                Vector3 start=shoulder.position,end=hand.position;
                for(int i=0;i<4;i++)
                {float a=i/4f,b=(i+1)/4f;Vector3 p=Vector3.Lerp(start,end,a)+Vector3.down*Mathf.Sin(a*Mathf.PI)*2,q=Vector3.Lerp(start,end,b)+Vector3.down*Mathf.Sin(b*Mathf.PI)*2;var segment=arms[side*4+i];segment.position=(p+q)*.5f;segment.rotation=Quaternion.FromToRotation(Vector3.up,q-p);float width=Mathf.Lerp(2.5f,.8f,(a+b)*.5f);segment.localScale=new Vector3(width,Vector3.Distance(p,q)*.5f+.15f,width);}
                if(Time.unscaledTime<handScanAt)continue;var visualRoot=hand.parent?hand.parent:hand;foreach(var r in visualRoot.GetComponentsInChildren<Renderer>())if(!originals.ContainsKey(r)){originals[r]=r.sharedMaterials;var values=new Material[r.sharedMaterials.Length];for(int i=0;i<values.Length;i++)values[i]=water;r.sharedMaterials=values;}
            }
            if(Time.unscaledTime>=handScanAt)handScanAt=Time.unscaledTime+1;
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
            var shader=Resources.Load<Shader>("CandyScenery/CandySpirit");water=new Material(shader?shader:Shader.Find("Universal Render Pipeline/Lit"));water.SetColor("_BaseColor",new Color(.025f,.65f,.8f));
            var root=new GameObject("Visual - ocean spirit bust and arms").transform;root.SetParent(transform,false);
            bust=Shape("Water torso",root,"Sphere.fbx");head=Shape("Water head",root,"Sphere.fbx");for(int i=0;i<arms.Length;i++)arms[i]=Shape("Flowing arm",root,"Capsule.fbx");
        }
        Transform Shape(string name,Transform parent,string mesh)
        {var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>(mesh);var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=water;r.shadowCastingMode=ShadowCastingMode.Off;return go.transform;}
        void Restore(){foreach(var p in originals)if(p.Key)p.Key.sharedMaterials=p.Value;originals.Clear();}
        void OnDisable()=>Restore();
        void OnDestroy(){Restore();if(water)Destroy(water);}
    }
}
