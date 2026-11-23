using System.Collections.Generic;
using DeadlyJenga.CandyArt;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.Polar
{
    [DefaultExecutionOrder(980)]
    public sealed partial class NeedlefrostArt : MonoBehaviour
    {
        public Shader skyShader, waterShader;
        public Material icebergMaterial;
        public Texture2D horizon;
        Material sky, sea, foam, previousSky;
        Mesh foamMesh;
        IWorldQuery world;
        readonly List<Vector3> foamVertices=new List<Vector3>();
        readonly List<Vector2> foamUv=new List<Vector2>();
        readonly List<int> foamIndices=new List<int>();
        readonly List<BoxCollider> iceContacts=new List<BoxCollider>();
        int foamRevision=-1;
        float nextFoam;
        // Each practical follows the closest structural bearing through collapse and reset.
        static readonly Vector3[] lampSites={new Vector3(-24,17.6f,18.8f),new Vector3(16,18.6f,14.8f),
            new Vector3(0,17.9f,12.7f),new Vector3(16,3.9f,-6.6f)};
        readonly Transform[] mounts=new Transform[4];
        readonly Vector3[] offsets=new Vector3[4];
        readonly Vector4[] lamps=new Vector4[4];
        Mesh seaMesh, bergMesh;
        GameObject scenery;
        CandyEnvironment environment;
        bool oldLighting;
        readonly Dictionary<Renderer,bool> hidden = new Dictionary<Renderer,bool>();
        float nextCheck;

        void OnEnable()
        {
            if(!Application.isPlaying) return;
            previousSky=RenderSettings.skybox;
            sky=new Material(skyShader); sea=new Material(waterShader);
            sky.SetTexture("_Horizon",horizon);
            scenery=new GameObject("Polar coast");scenery.transform.SetParent(transform,false);
            seaMesh=OceanSurface();
            Render("Open sea",seaMesh,sea);
            world=GetComponent<IWorldQuery>();
            foam=new Material(waterShader);foam.SetFloat("_ContactFoam",1);
            foamMesh=new Mesh{name="Foam around immersed ice"};foamMesh.MarkDynamic();
            Render("Ice contact foam",foamMesh,foam);
            bergMesh=Icebergs();
            Render("Offshore ice field",bergMesh,icebergMaterial);
            Apply();
        }

        void Render(string label,Mesh mesh,Material material)
        {
            var go=new GameObject(label);go.transform.SetParent(scenery.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }

        void LateUpdate()
        {
            if(!scenery)return;
            if(Time.unscaledTime>=nextFoam){nextFoam=Time.unscaledTime+.18f;UpdateFoam();}
            if(Time.unscaledTime<nextCheck)return;
            nextCheck=Time.unscaledTime+1;Apply();
        }

        void Apply()
        {
            RenderSettings.skybox=sky;
            var env=CandyEnvironment.ForMap(this);
            if(!env)return;
            if(environment!=env){environment=env;oldLighting=env.applyLighting;}
            env.applyLighting=false;
            foreach(var r in env.GetComponentsInChildren<Renderer>(true))
            {
                if(!hidden.ContainsKey(r))hidden.Add(r,r.enabled);
                r.enabled=false;
            }
        }

        void UpdateFoam()
        {
            if(world==null)return;
            int revision=(world as ISyncedWorld)?.Revision??0;
            if(revision!=foamRevision)
            {
                foamRevision=revision;iceContacts.Clear();
                BindPracticals();
                BindChandeliers();
                foreach(var body in world.Blocks)
                    if(body && body.TryGetComponent<BoxCollider>(out var box) && box.enabled)iceContacts.Add(box);
            }
            UpdatePracticals();
            foamVertices.Clear();foamUv.Clear();foamIndices.Clear();
            float seaY=transform.position.y+.08f;
            foreach(var box in iceContacts)
            {
                if(!box || !box.gameObject.activeInHierarchy)continue;
                Bounds b=box.bounds;
                if(b.min.y>=seaY || b.max.y<=seaY)continue;
                Vector3 local=transform.InverseTransformPoint(new Vector3(b.center.x,seaY+.035f,b.center.z));
                int start=foamVertices.Count;
                for(int k=0;k<=20;k++)
                {
                    float a=k*Mathf.PI*.1f,dx=Mathf.Cos(a),dz=Mathf.Sin(a);
                    float shell=1/Mathf.Max(Mathf.Abs(dx)/Mathf.Max(.1f,b.extents.x),Mathf.Abs(dz)/Mathf.Max(.1f,b.extents.z));
                    var dir=new Vector3(dx,0,dz);
                    foamVertices.Add(local+dir*(shell-.09f));foamUv.Add(Vector2.zero);
                    foamVertices.Add(local+dir*(shell+.82f));foamUv.Add(Vector2.right);
                    if(k==20)continue;
                    int v=start+k*2;
                    foamIndices.Add(v);foamIndices.Add(v+2);foamIndices.Add(v+1);
                    foamIndices.Add(v+1);foamIndices.Add(v+2);foamIndices.Add(v+3);
                }
            }
            foamMesh.Clear();foamMesh.SetVertices(foamVertices);foamMesh.SetUVs(0,foamUv);
            foamMesh.SetTriangles(foamIndices,0);
        }

        void BindPracticals()
        {
            for(int i=0;i<lampSites.Length;i++)
            {
                var at=transform.TransformPoint(lampSites[i]);float nearest=float.PositiveInfinity;
                mounts[i]=null;
                foreach(var body in world.Blocks)
                {
                    if(!body)continue;
                    float d=(body.worldCenterOfMass-at).sqrMagnitude;
                    if(d>=nearest)continue;
                    nearest=d;mounts[i]=body.transform;
                }
                if(mounts[i])offsets[i]=mounts[i].InverseTransformPoint(at);
            }
        }

        void UpdatePracticals()
        {
            for(int i=0;i<mounts.Length;i++)
            {
                if(!mounts[i] || !mounts[i].gameObject.activeInHierarchy){lamps[i]=Vector4.zero;continue;}
                var at=mounts[i].TransformPoint(offsets[i]);
                lamps[i]=new Vector4(at.x,at.y,at.z,1);
            }
            Shader.SetGlobalVectorArray("_NeedlefrostPracticals",lamps);
        }

        static Mesh OceanSurface()
        {
            // The fine central patch resolves chop near the player; the outside cells reach the horizon.
            const int cells=80,stride=cells+1;
            var verts=new Vector3[stride*stride];var tris=new int[cells*cells*6];
            float Coordinate(int k)
            {
                if(k==0)return -580;
                if(k==cells)return 580;
                return (k-cells*.5f)*1.8f;
            }
            for(int z=0;z<stride;z++)
            for(int x=0;x<stride;x++)
            {
                int v=z*stride+x;
                verts[v]=new Vector3(Coordinate(x),.08f,Coordinate(z));
                if(x==cells||z==cells)continue;
                int t=(z*cells+x)*6;
                tris[t]=v;tris[t+1]=v+stride;tris[t+2]=v+stride+1;
                tris[t+3]=v;tris[t+4]=v+stride+1;tris[t+5]=v+1;
            }
            var mesh=new Mesh{name="Polar ocean chop"};mesh.vertices=verts;mesh.triangles=tris;
            mesh.RecalculateBounds();return mesh;
        }

        static Mesh Icebergs()
        {
            var vertices=new List<Vector3>();var indices=new List<int>();
            void Face(Vector3 a,Vector3 b,Vector3 c)
            {
                int i=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
                indices.Add(i);indices.Add(i+1);indices.Add(i+2);
            }
            var random=new System.Random(8071);
            float Rand(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
            for(int island=0;island<24;island++)
            {
                float angle=island*2.39996f,dist=Rand(78,170);
                var center=new Vector3(Mathf.Sin(angle)*dist,-1,Mathf.Cos(angle)*dist);
                for(int peak=0;peak<4;peak++)
                {
                    float r=Rand(4,14),h=Rand(5,21);
                    var foot=center+new Vector3(Rand(-9,9),0,Rand(-6,6));
                    float lean=Rand(-.72f,.72f);
                    var tip=foot+new Vector3(r*lean,h,r*Rand(-.35f,.35f));
                    var low=new Vector3[9];var mid=new Vector3[9];var crest=new Vector3[9];
                    for(int i=0;i<9;i++)
                    {
                        float a=i*Mathf.PI*2/9;
                        low[i]=foot+new Vector3(Mathf.Cos(a)*r*Rand(.86f,1.1f),0,Mathf.Sin(a)*r*.72f);
                        var rim=low[i]-foot;
                        mid[i]=foot+rim*Rand(.79f,.98f)+new Vector3(.3f,h*Rand(.13f,.30f),.14f);
                        crest[i]=tip+new Vector3(rim.x*.40f,-h*Rand(.08f,.49f),rim.z*.31f);
                    }
                    for(int i=0;i<9;i++)
                    {
                        int j=(i+1)%9;Face(low[i],mid[i],mid[j]);Face(low[i],mid[j],low[j]);
                        Face(mid[i],crest[i],crest[j]);Face(mid[i],crest[j],mid[j]);Face(crest[i],tip,crest[j]);
                    }
                }
            }
            var mesh=new Mesh{name="Weathered offshore ridges"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();return mesh;
        }

        void OnDisable()
        {
            if(!scenery)return;
            ReleaseChandeliers();
            if(environment)environment.applyLighting=oldLighting;
            foreach(var pair in hidden)if(pair.Key)pair.Key.enabled=pair.Value;
            hidden.Clear();if(RenderSettings.skybox==sky)RenderSettings.skybox=previousSky;
            Destroy(scenery);Destroy(seaMesh);Destroy(bergMesh);Destroy(foamMesh);Destroy(sky);Destroy(sea);Destroy(foam);
        }
    }
}
