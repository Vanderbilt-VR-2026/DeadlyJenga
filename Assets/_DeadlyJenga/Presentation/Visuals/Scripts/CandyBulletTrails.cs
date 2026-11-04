using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    // Cosmetic travel follows the authoritative cast endpoints. It never applies hits.
    public sealed class CandyBulletTrails : MonoBehaviour
    {
        sealed class Bullet
        {
            public Transform head;public MeshRenderer headRenderer;public LineRenderer trail;public Vector3 start,end;public float age,duration;public bool shotgun,needle,contact;public Color color;
        }
        const int Capacity=224;
        readonly List<Bullet> active=new List<Bullet>();
        readonly Stack<Bullet> pool=new Stack<Bullet>();
        Material material,ribbon,sparkMaterial;Mesh mesh;ParticleSystem sparks;
        MaterialPropertyBlock tint;
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor"),ColorId=Shader.PropertyToID("_Color");
        public int ActiveCount=>active.Count;
        public int AllocatedCount=>active.Count+pool.Count;
        void Awake()
        {
            tint=new MaterialPropertyBlock();
            var source=Resources.Load<Material>("CandyUtilityGlow");
            if(!source){enabled=false;return;}
            var lit=Shader.Find("Universal Render Pipeline/Lit");
            material=lit?new Material(lit):new Material(source);material.name="White-hot ammunition core";
            material.EnableKeyword("_EMISSION");material.SetFloat("_Metallic",.35f);material.SetFloat("_Smoothness",.76f);
            var shader=Resources.Load<Shader>("CandyOrdnance");
            ribbon=shader?new Material(shader):new Material(source);ribbon.SetFloat("_Ribbon",1);
            sparkMaterial=shader?new Material(shader):new Material(source);
            var emitter=new GameObject("Pooled muzzle and impact sparks");emitter.transform.SetParent(transform,false);
            sparks=emitter.AddComponent<ParticleSystem>();sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=sparks.main;main.loop=false;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=384;main.gravityModifier=.6f;
            var emission=sparks.emission;emission.enabled=false;var shape=sparks.shape;shape.enabled=false;
            var renderer=emitter.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=sparkMaterial;renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=1.6f;renderer.velocityScale=.08f;
            renderer.shadowCastingMode=ShadowCastingMode.Off;emitter.SetActive(false);
            mesh=BuildHead();
        }
        static Mesh BuildHead()
        {
            const int sides=10;
            var profile=new[]{new Vector2(-.74f,0),new Vector2(-.74f,.32f),new Vector2(-.55f,.41f),new Vector2(.23f,.38f),new Vector2(.64f,.22f),new Vector2(.86f,0)};
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int j=0;j<profile.Length;j++)for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides;vertices.Add(new Vector3(Mathf.Cos(a)*profile[j].y,Mathf.Sin(a)*profile[j].y,profile[j].x));
                if(j==profile.Length-1)continue;
                int k=j*sides+i,l=j*sides+(i+1)%sides;triangles.AddRange(new[]{k,l,k+sides,l,l+sides,k+sides});
            }
            var mesh=new Mesh{name="Shared beveled ammunition jacket"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();return mesh;
        }
        public void Fire(Vector3 start,Vector3[] endpoints,bool shotgun)
            => Fire(start,endpoints,shotgun,EquipmentKind.None);
        public void Fire(Vector3 start,Vector3[] endpoints,bool shotgun,EquipmentKind kind)
        {
            if(!enabled||!material||endpoints==null)return;
            bool needle=kind==EquipmentKind.NeedleDrill;
            float range=kind==EquipmentKind.None?0:shotgun&&kind==EquipmentKind.DualRifle?14:shotgun&&kind==EquipmentKind.SheriffHammer?18:EquipmentSpecifications.Get(kind).range;
            int count=Mathf.Min(7,endpoints.Length);
            for(int i=0;i<count;i++)
            {
                if(active.Count>=Capacity)ReleaseAt(0);
                var delta=endpoints[i]-start;if(!Finite(start)||!Finite(endpoints[i])||delta.sqrMagnitude<.0001f)continue;
                var bullet=pool.Count>0?pool.Pop():Create();
                bullet.start=start;bullet.end=endpoints[i];bullet.age=0;bullet.shotgun=shotgun;
                bullet.duration=Mathf.Clamp(delta.magnitude/(shotgun?95:190),.065f,.29f);
                bullet.needle=needle;
                // A full-range endpoint is a miss. No second raycast or gameplay prediction.
                bullet.contact=range>0&&delta.magnitude<range-.12f;
                bullet.head.gameObject.SetActive(true);bullet.trail.gameObject.SetActive(true);
                bullet.head.position=start;bullet.head.rotation=Quaternion.LookRotation(delta);bullet.head.localScale=needle?new Vector3(.035f,.035f,.23f):new Vector3(shotgun?.075f:.09f,shotgun?.075f:.09f,shotgun?.17f:.27f);
                Color color=needle?new Color(.72f,.80f,.87f):kind==EquipmentKind.ChainGun?new Color(1,.68f,.18f):shotgun?new Color(1,.43f,.09f,1):new Color(.24f,.88f,1,1);
                tint.Clear();tint.SetColor(BaseColor,color);tint.SetColor(ColorId,color);tint.SetColor("_EmissionColor",Color.Lerp(color,Color.white,.67f)*1.5f);
                bullet.headRenderer.SetPropertyBlock(tint);bullet.trail.SetPropertyBlock(tint);
                bullet.color=color;bullet.trail.startColor=Color.white;bullet.trail.endColor=new Color(1,1,1,.04f);
                if(i==0&&range>0)Burst(start,delta.normalized,color,shotgun?12:7,false);
                bullet.trail.widthMultiplier=needle?.058f:shotgun?.12f:.17f;
                bullet.trail.SetPosition(0,start);bullet.trail.SetPosition(1,start);active.Add(bullet);
            }
        }
        public void FireNeedles(Vector3 start,Vector3[] endpoints)=>Fire(start,endpoints,false,EquipmentKind.NeedleDrill);
        void Burst(Vector3 point,Vector3 direction,Color color,int count,bool impact)
        {
            if(!sparks)return;
            if(!sparks.gameObject.activeSelf)sparks.gameObject.SetActive(true);
            if(sparks.isStopped)sparks.Play();
            for(int i=0;i<count;i++)
            {
                var scatter=Quaternion.Euler(i*73+count*9,i*137.5f,i*41)*Vector3.forward;
                var velocity=(scatter*(impact?.95f:.3f)+direction*(impact?-.5f:1.2f)).normalized;
                sparks.Emit(new ParticleSystem.EmitParams{position=point,velocity=velocity*(impact?2+i*.47f:1+i*.35f),
                    startSize=(impact?.12f:.085f)*(i==0?2:1),startLifetime=(impact?.21f:.10f)+i*.011f,startColor=color},1);
            }
        }
        static bool Finite(Vector3 p)=>!float.IsNaN(p.sqrMagnitude)&&!float.IsInfinity(p.sqrMagnitude);
        Bullet Create()
        {
            var root=new GameObject("Visual bullet head");root.transform.SetParent(transform,false);
            root.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var line=new GameObject("Visual bullet trail");line.transform.SetParent(transform,false);
            var trail=line.AddComponent<LineRenderer>();trail.sharedMaterial=ribbon;trail.positionCount=2;trail.useWorldSpace=true;
            trail.shadowCastingMode=ShadowCastingMode.Off;trail.receiveShadows=false;trail.numCapVertices=0;trail.widthCurve=new AnimationCurve(new Keyframe(0,.85f),new Keyframe(.36f,1),new Keyframe(1,.08f));
            return new Bullet{head=root.transform,headRenderer=renderer,trail=trail};
        }
        void Update()
        {
            if(sparks&&sparks.gameObject.activeSelf&&sparks.particleCount==0)sparks.gameObject.SetActive(false);
            for(int i=active.Count-1;i>=0;i--)
            {
                var bullet=active[i];bullet.age+=Time.deltaTime;
                if(bullet.age>=bullet.duration){if(bullet.contact)Burst(bullet.end,(bullet.end-bullet.start).normalized,bullet.color,bullet.shotgun?4:10,true);ReleaseAt(i);continue;}
                float t=bullet.age/bullet.duration;
                bullet.head.position=Vector3.Lerp(bullet.start,bullet.end,t);
                bullet.trail.SetPosition(0,bullet.head.position);
                bullet.trail.SetPosition(1,Vector3.Lerp(bullet.start,bullet.end,Mathf.Max(0,t-(bullet.needle?.23f:bullet.shotgun?.43f:.55f))));
            }
        }
        void ReleaseAt(int i)
        {
            var bullet=active[i];bullet.head.gameObject.SetActive(false);bullet.trail.gameObject.SetActive(false);pool.Push(bullet);active.RemoveAt(i);
        }
        void OnDisable(){while(active.Count>0)ReleaseAt(active.Count-1);if(sparks){sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);sparks.gameObject.SetActive(false);}}
        void OnDestroy(){if(material)Destroy(material);if(mesh)Destroy(mesh);if(ribbon)Destroy(ribbon);if(sparkMaterial)Destroy(sparkMaterial);}
    }
}
