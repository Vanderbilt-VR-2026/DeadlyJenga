using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    // Cosmetic travel follows the authoritative cast endpoints. It never applies hits.
    public sealed class CandyBulletTrails : MonoBehaviour
    {
        sealed class Bullet
        {
            public Transform head;public MeshRenderer headRenderer;public LineRenderer trail;public Vector3 start,end;public float age,duration;public bool shotgun;
        }
        const int Capacity=224;
        readonly List<Bullet> active=new List<Bullet>();
        readonly Stack<Bullet> pool=new Stack<Bullet>();
        Material material;Mesh mesh;
        MaterialPropertyBlock tint;
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor"),ColorId=Shader.PropertyToID("_Color");
        public int ActiveCount=>active.Count;
        public int AllocatedCount=>active.Count+pool.Count;
        void Awake()
        {
            tint=new MaterialPropertyBlock();
            var source=Resources.Load<Material>("CandyUtilityGlow");
            if(!source){enabled=false;return;}
            material=new Material(source){name="Candy ammunition glow"};
            mesh=new Mesh{name="Shared visible ammunition"};
            mesh.vertices=new[]{Vector3.forward,Vector3.back,new Vector3(-.35f,0,0),new Vector3(.35f,0,0),new Vector3(0,.35f,0),new Vector3(0,-.35f,0)};
            mesh.triangles=new[]{0,2,4,0,4,3,0,3,5,0,5,2,1,4,2,1,3,4,1,5,3,1,2,5};mesh.RecalculateNormals();mesh.RecalculateBounds();
        }
        public void Fire(Vector3 start,Vector3[] endpoints,bool shotgun)
        {
            if(!enabled||!material||endpoints==null)return;
            int count=Mathf.Min(7,endpoints.Length);
            for(int i=0;i<count;i++)
            {
                if(active.Count>=Capacity)ReleaseAt(0);
                var delta=endpoints[i]-start;if(!Finite(start)||!Finite(endpoints[i])||delta.sqrMagnitude<.0001f)continue;
                var bullet=pool.Count>0?pool.Pop():Create();
                bullet.start=start;bullet.end=endpoints[i];bullet.age=0;bullet.shotgun=shotgun;
                bullet.duration=Mathf.Clamp(delta.magnitude/(shotgun?100:220),.065f,.3f);
                bullet.head.gameObject.SetActive(true);bullet.trail.gameObject.SetActive(true);
                bullet.head.position=start;bullet.head.rotation=Quaternion.LookRotation(delta);bullet.head.localScale=new Vector3(shotgun?.055f:.065f,shotgun?.055f:.065f,shotgun?.16f:.24f);
                Color color=shotgun?new Color(1,.43f,.09f,1):new Color(.24f,.88f,1,1);
                tint.Clear();tint.SetColor(BaseColor,color);tint.SetColor(ColorId,color);
                bullet.headRenderer.SetPropertyBlock(tint);bullet.trail.SetPropertyBlock(tint);
                bullet.trail.startColor=color;bullet.trail.endColor=color;
                bullet.trail.widthMultiplier=shotgun?.032f:.04f;
                bullet.trail.SetPosition(0,start);bullet.trail.SetPosition(1,start);active.Add(bullet);
            }
        }
        static bool Finite(Vector3 p)=>!float.IsNaN(p.sqrMagnitude)&&!float.IsInfinity(p.sqrMagnitude);
        Bullet Create()
        {
            var root=new GameObject("Visual bullet head");root.transform.SetParent(transform,false);
            root.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var line=new GameObject("Visual bullet trail");line.transform.SetParent(transform,false);
            var trail=line.AddComponent<LineRenderer>();trail.sharedMaterial=material;trail.positionCount=2;trail.useWorldSpace=true;
            trail.shadowCastingMode=ShadowCastingMode.Off;trail.receiveShadows=false;trail.numCapVertices=2;
            return new Bullet{head=root.transform,headRenderer=renderer,trail=trail};
        }
        void Update()
        {
            for(int i=active.Count-1;i>=0;i--)
            {
                var bullet=active[i];bullet.age+=Time.deltaTime;
                if(bullet.age>=bullet.duration){ReleaseAt(i);continue;}
                float t=bullet.age/bullet.duration;
                bullet.head.position=Vector3.Lerp(bullet.start,bullet.end,t);
                bullet.trail.SetPosition(0,bullet.head.position);
                bullet.trail.SetPosition(1,Vector3.Lerp(bullet.start,bullet.end,Mathf.Max(0,t-.28f)));
            }
        }
        void ReleaseAt(int i)
        {
            var bullet=active[i];bullet.head.gameObject.SetActive(false);bullet.trail.gameObject.SetActive(false);pool.Push(bullet);active.RemoveAt(i);
        }
        void OnDisable(){while(active.Count>0)ReleaseAt(active.Count-1);}
        void OnDestroy(){if(material)Destroy(material);if(mesh)Destroy(mesh);}
    }
}
