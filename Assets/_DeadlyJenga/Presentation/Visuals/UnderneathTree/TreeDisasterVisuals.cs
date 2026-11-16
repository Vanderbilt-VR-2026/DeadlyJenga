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
            public readonly List<Transform> legs=new List<Transform>();
            public readonly List<Transform> tail=new List<Transform>();
            public readonly List<Transform> arms=new List<Transform>();
            public readonly List<Transform> hands=new List<Transform>();
            public readonly Dictionary<int,Transform> ornaments=new Dictionary<int,Transform>();
            public readonly Stack<Transform> recycled=new Stack<Transform>();
        }
        readonly Dictionary<string,Look> looks=new Dictionary<string,Look>();
        readonly List<string> stale=new List<string>();
        readonly List<Material> owned=new List<Material>();
        Mesh sphere,cylinder,cone;
        Material fur,dark,cream,pink,eye,black,red,gold,blue;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register(){GameEvents.SessionStarted-=Attach;GameEvents.SessionStarted+=Attach;}
        static void Attach(GameObject session)
        {if(MatchChoices.MapId=="workshop-under-the-tree"&&session&&!session.GetComponent<TreeDisasterVisuals>())session.AddComponent<TreeDisasterVisuals>();}
        void Awake()
        {
            sphere=Resources.GetBuiltinResource<Mesh>("Sphere.fbx");cylinder=Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");cone=Cone();
            fur=Mat("Tabby warm fur",new Color(.38f,.22f,.11f),.18f);
            dark=Mat("Tabby dark stripes",new Color(.075f,.042f,.024f),.12f);
            cream=Mat("Cream fur and Santa trim",new Color(.94f,.87f,.69f),.22f);
            pink=Mat("Nose and inner ears",new Color(.56f,.19f,.18f),.38f);
            eye=Mat("Amber eyes",new Color(.72f,.72f,.12f),.7f);
            black=Mat("Boots belt and pupils",new Color(.018f,.012f,.009f),.45f);
            red=Mat("Santa velvet and ruby ornaments",new Color(.62f,.018f,.032f),.5f,.15f);
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
        static void Link(Transform t,Vector3 a,Vector3 b,float radius)
        {t.position=(a+b)*.5f;t.rotation=Quaternion.FromToRotation(Vector3.up,b-a);t.localScale=new Vector3(radius,Vector3.Distance(a,b)*.5f,radius);}
        Look Cat()
        {
            var look=new Look{root=Root("Tree Disaster / CatAttack")};var r=look.root;
            Part(r,"Long feline torso",new Vector3(0,17,0),new Vector3(20,23,42),fur);
            Part(r,"Cream chest",new Vector3(0,17,13),new Vector3(17,20,15),cream);
            Part(r,"Round tabby head",new Vector3(0,28,22),new Vector3(21,19,19),fur);
            for(int side=-1;side<=1;side+=2)
            {
                var ear=Part(r,"Pointed ear",new Vector3(side*7,39,20),new Vector3(8,12,8),fur,cone);
                ear.localRotation=Quaternion.Euler(0,0,-side*14);
                Part(r,"Inner ear",new Vector3(side*7,38,23),new Vector3(4.5f,7,1),pink,cone);
                Part(r,"Muzzle",new Vector3(side*3,24,30),new Vector3(8,7,6),cream);
                Part(r,"Eye",new Vector3(side*5,30,30),new Vector3(5,4,2),eye);
                Part(r,"Vertical pupil",new Vector3(side*5,30,31),new Vector3(1.1f,3.5f,.6f),black);
                Part(r,"Eye highlight",new Vector3(side*5-.7f,31,31.3f),Vector3.one*.8f,cream);
                for(int j=0;j<3;j++)
                {
                    var w=Part(r,"Whisker",Vector3.zero,Vector3.one,cream,cylinder);
                    var a=new Vector3(side*5,24-j*.8f,31);var b=a+new Vector3(side*10,(j-1)*1.8f,-1);
                    w.localPosition=(a+b)*.5f;w.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);w.localScale=new Vector3(.13f,(b-a).magnitude*.5f,.13f);
                }
                for(int j=0;j<2;j++)
                {
                    var leg=Root("Running leg",r);leg.localPosition=new Vector3(side*7,14,j==0?12:-13);
                    Part(leg,"Fur leg",new Vector3(0,-5,0),new Vector3(6,15,7),fur);
                    Part(leg,"Cream paw",new Vector3(0,-11,2),new Vector3(7,4,10),cream);look.legs.Add(leg);
                }
                for(int j=0;j<6;j++)Part(r,"Tabby flank stripe",new Vector3(side*8.5f,20,-15+j*6),new Vector3(3,14,2),dark);
                var brow=Part(r,"Forehead stripe",new Vector3(side*3,35,29),new Vector3(2,6,1),dark);brow.localRotation=Quaternion.Euler(0,0,side*24);
            }
            Part(r,"Nose",new Vector3(0,26,33),new Vector3(4,2.8f,2),pink);
            for(int j=0;j<9;j++)look.tail.Add(Part(r,"Curving tail",Vector3.zero,Vector3.one,j%2==0?fur:dark));
            return look;
        }
        Look Santa()
        {
            var look=new Look{root=Root("Tree Disaster / EvilSanta")};var r=look.root;
            Part(r,"Heavy red coat",new Vector3(0,39,0),new Vector3(41,49,31),red);
            Part(r,"Coat hem fur",new Vector3(0,20,0),new Vector3(42,7,32),cream);
            Part(r,"Black belt",new Vector3(0,35,1),new Vector3(42,8,32),black);
            Part(r,"Gold buckle",new Vector3(0,35,17),new Vector3(8,8,3),gold);
            Part(r,"Buckle center",new Vector3(0,35,19),new Vector3(4,4,1),black);
            Part(r,"Face",new Vector3(0,69,1),new Vector3(24,25,22),pink);
            Part(r,"Huge white beard",new Vector3(0,58,13),new Vector3(26,29,15),cream);
            for(int side=-1;side<=1;side+=2)
            {
                Part(r,"Boot",new Vector3(side*10,7,4),new Vector3(13,15,22),black);
                Part(r,"White cuff",new Vector3(side*10,15,0),new Vector3(14,7,15),cream);
                Part(r,"Menacing eye",new Vector3(side*5,73,11),new Vector3(4.5f,3,2),red);
                Part(r,"Dark pupil",new Vector3(side*5,73,12),new Vector3(1.3f,2,1),black);
                var brow=Part(r,"Angled white brow",new Vector3(side*5,76,12),new Vector3(9,3,3),cream);brow.localRotation=Quaternion.Euler(0,0,side*22);
                Part(r,"Moustache",new Vector3(side*5,65,15),new Vector3(12,5,7),cream);
                look.arms.Add(Part(r,"Reaching red sleeve",Vector3.zero,Vector3.one,red,cylinder));
                look.hands.Add(Part(r,"Grabbing mitten",Vector3.zero,Vector3.one*9,cream));
            }
            Part(r,"Nose",new Vector3(0,69,15),new Vector3(5,6,6),pink);
            Part(r,"Hat fur brim",new Vector3(0,81,0),new Vector3(28,6,25),cream);
            var hat=Part(r,"Red pointed hat",new Vector3(0,94,0),new Vector3(25,28,23),red,cone);hat.localRotation=Quaternion.Euler(0,0,-12);
            Part(r,"Hat pompom",new Vector3(6,107,0),Vector3.one*8,cream);
            for(int j=0;j<7;j++)Part(r,"Beard curls",new Vector3((j-3)*3,52+Mathf.Abs(j-3)*2,17),new Vector3(5,10,5),cream);
            return look;
        }
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
                {look=f.kind=="CatAttack"?Cat():f.kind=="EvilSanta"?Santa():new Look{root=Root("Tree Disaster / "+f.kind)};looks.Add(f.kind,look);}
                look.root.gameObject.SetActive(f.warning<=0);if(f.warning>0||f.points==null)continue;
                if(f.kind=="CatAttack"&&f.points.Length>0)
                {
                    var p=f.points[0];look.root.position=p.position+p.velocity*Mathf.Clamp(Time.time-f.sampledAt,0,.25f);look.root.rotation=Quaternion.LookRotation(p.velocity.normalized);
                    for(int i=0;i<look.legs.Count;i++)look.legs[i].localRotation=Quaternion.Euler(Mathf.Sin(f.age*12+i*Mathf.PI)*35,0,0);
                    for(int i=0;i<look.tail.Count;i++){look.tail[i].localPosition=new Vector3(Mathf.Sin(f.age*2+i*.3f)*3,20+i*1.4f,-21-i*3);look.tail[i].localScale=Vector3.one*(5-i*.3f);}
                }
                else if(f.kind=="EvilSanta"&&f.points.Length>=3)
                {
                    look.root.position=f.points[0].position;look.root.rotation=Quaternion.LookRotation(f.points[0].velocity);
                    for(int i=0;i<2;i++)
                    {var shoulder=look.root.TransformPoint(new Vector3(i==0?-18:18,55,0));Link(look.arms[i],shoulder,f.points[i+1].position,10);look.hands[i].position=f.points[i+1].position;}
                }
                else
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
        static Mesh Cone()
        {
            const int n=24;var vertices=new Vector3[n+2];var triangles=new int[n*6];vertices[n]=Vector3.up*.5f;vertices[n+1]=Vector3.down*.5f;
            for(int i=0;i<n;i++){float a=i*2*Mathf.PI/n;vertices[i]=new Vector3(Mathf.Cos(a)*.5f,-.5f,Mathf.Sin(a)*.5f);int j=(i+1)%n;int k=i*6;triangles[k]=i;triangles[k+1]=n;triangles[k+2]=j;triangles[k+3]=j;triangles[k+4]=n+1;triangles[k+5]=i;}
            var m=new Mesh{name="Tree creature pointed features",vertices=vertices,triangles=triangles};m.RecalculateNormals();m.RecalculateBounds();return m;
        }
        void OnDestroy(){foreach(var m in owned)if(m)Destroy(m);if(cone)Destroy(cone);}
    }
}
