using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    // Render only the host's disaster frames. These models never own gameplay colliders.
    public sealed class TreeDisasterVisuals : MonoBehaviour
    {
        sealed class Look
        {
            public Transform root;
            public ChristmasCreaturePose pose;
            public readonly Dictionary<int,Transform> ornaments=new Dictionary<int,Transform>();
            public readonly Stack<Transform> recycled=new Stack<Transform>();
        }
        readonly Dictionary<string,Look> looks=new Dictionary<string,Look>();
        readonly List<string> stale=new List<string>();
        readonly List<Material> owned=new List<Material>();
        Mesh sphere,cylinder;
        Material red,gold,blue,palette;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register(){GameEvents.SessionStarted-=Attach;GameEvents.SessionStarted+=Attach;}
        static void Attach(GameObject session)
        {if(MatchChoices.MapId=="workshop-under-the-tree"&&session&&!session.GetComponent<TreeDisasterVisuals>())session.AddComponent<TreeDisasterVisuals>();}
        void Awake()
        {
            sphere=Resources.GetBuiltinResource<Mesh>("Sphere.fbx");cylinder=Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            palette=Mat("Christmas creature palette",Color.white,.32f);
            palette.SetTexture("_BaseMap",Resources.Load<Texture2D>("UnderneathTreeFinish/ChristmasCreaturePalette"));
            red=Mat("Ruby ornaments",new Color(.62f,.018f,.032f),.5f,.15f);
            gold=Mat("Ornament gold",new Color(.95f,.59f,.13f),.78f,.8f);
            blue=Mat("Sapphire ornaments",new Color(.018f,.14f,.66f),.8f,.45f);
        }
        Material Mat(string name,Color color,float smooth,float metal=0)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metal);owned.Add(m);return m;
        }
        Transform Part(Transform parent,string name,Vector3 p,Vector3 scale,Material material,Mesh mesh=null)
        {
            var go=new GameObject(name);var t=go.transform;t.SetParent(parent,false);t.localPosition=p;t.localScale=scale;
            go.AddComponent<MeshFilter>().sharedMesh=mesh?mesh:sphere;go.AddComponent<MeshRenderer>().sharedMaterial=material;return t;
        }
        Transform Root(string name,Transform parent=null)
        {var t=new GameObject(name).transform;t.SetParent(parent?parent:transform,false);return t;}
        Transform Ornament(Transform root,int id)
        {
            var t=Root("Falling ornament "+id,root);
            Part(t,"Polished bauble",Vector3.zero,Vector3.one,id%3==0?blue:id%3==1?red:gold);
            Part(t,"Gold equator",Vector3.zero,new Vector3(1.012f,.08f,1.012f),gold);
            Part(t,"Metal cap",Vector3.up*.53f,new Vector3(.2f,.08f,.2f),gold,cylinder);
            for(int j=0;j<8;j++)
            {float a=j*Mathf.PI/4;Part(t,"Hanging loop",new Vector3(Mathf.Sin(a)*.09f,.67f+Mathf.Cos(a)*.09f,0),Vector3.one*.045f,gold);}
            return t;
        }
        void LateUpdate()
        {
            stale.Clear();foreach(var pair in looks)stale.Add(pair.Key);
            foreach(var source in DisasterStates.Active)
            {
                var f=source.Capture();if(f==null||!(f.kind=="CatAttack"||f.kind=="EvilSanta"||f.kind=="OrnamentShower"||f.kind=="PlanetKillerOrnament"))continue;
                stale.Remove(f.kind);
                if(!looks.TryGetValue(f.kind,out var look))
                {
                    look=new Look{root=Root("Tree Disaster / "+f.kind)};
                    if(f.kind=="CatAttack"||f.kind=="EvilSanta")
                        look.pose=new ChristmasCreaturePose(look.root,f.kind=="CatAttack",palette);
                    looks.Add(f.kind,look);
                }
                look.root.gameObject.SetActive(look.pose!=null||f.warning<=0);
                if(f.points==null)continue;
                if(look.pose!=null)
                {
                    if(f.kind=="CatAttack" && f.points.Length>=1)look.pose.Cat(f);
                    else if(f.kind=="EvilSanta" && f.points.Length>=3)look.pose.Santa(f);
                }
                else if(f.warning<=0)
                {
                    var departed=new List<int>();
                    foreach(var pair in look.ornaments)
                        if(!Array.Exists(f.points,p=>p.id==pair.Key)){pair.Value.gameObject.SetActive(false);look.recycled.Push(pair.Value);departed.Add(pair.Key);}
                    foreach(int id in departed)look.ornaments.Remove(id);
                    foreach(var p in f.points)
                    {
                        if(!look.ornaments.TryGetValue(p.id,out var t))
                        {if(look.ornaments.Count>=128)continue;t=look.recycled.Count>0?look.recycled.Pop():Ornament(look.root,p.id);look.ornaments.Add(p.id,t);}
                        t.gameObject.SetActive(true);t.position=p.position;t.localScale=Vector3.one*p.size;t.rotation=Quaternion.Euler(f.age*25,p.id*67,12);
                    }
                }
            }
            foreach(var source in stale){Destroy(looks[source].root.gameObject);looks.Remove(source);}
        }
        void OnDestroy(){foreach(var m in owned)if(m)Destroy(m);}
    }
}
