using System.Collections.Generic;
using DeadlyJenga.CandyArt;
using UnityEngine;

namespace DeadlyJenga.Polar
{
    [DefaultExecutionOrder(980)]
    public sealed class NeedlefrostArt : MonoBehaviour
    {
        public Shader skyShader, waterShader;
        public Material icebergMaterial;
        Material sky, sea, previousSky;
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
            scenery=new GameObject("Polar coast");scenery.transform.SetParent(transform,false);
            seaMesh=new Mesh{name="Sapphire ocean"};
            seaMesh.vertices=new[]{new Vector3(-580,.08f,-580),new Vector3(-580,.08f,580),
                new Vector3(580,.08f,580),new Vector3(580,.08f,-580)};
            seaMesh.triangles=new[]{0,1,2,0,2,3};seaMesh.RecalculateNormals();
            Render("Open sea",seaMesh,sea);
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
            if(!scenery || Time.unscaledTime<nextCheck)return;
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
                    var tip=foot+new Vector3(r*.46f,h,r*.18f);
                    var low=new Vector3[9];var mid=new Vector3[9];
                    for(int i=0;i<9;i++)
                    {
                        float a=i*Mathf.PI*2/9;
                        low[i]=foot+new Vector3(Mathf.Cos(a)*r*Rand(.86f,1.1f),0,Mathf.Sin(a)*r*.72f);
                        mid[i]=Vector3.Lerp(low[i],tip,Rand(.24f,.56f))+new Vector3(.7f,Rand(-.5f,1),.14f);
                    }
                    for(int i=0;i<9;i++)
                    {
                        int j=(i+1)%9;Face(low[i],mid[i],mid[j]);Face(low[i],mid[j],low[j]);Face(mid[i],tip,mid[j]);
                    }
                }
            }
            var mesh=new Mesh{name="Weathered offshore ridges"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();return mesh;
        }

        void OnDisable()
        {
            if(!scenery)return;
            if(environment)environment.applyLighting=oldLighting;
            foreach(var pair in hidden)if(pair.Key)pair.Key.enabled=pair.Value;
            hidden.Clear();if(RenderSettings.skybox==sky)RenderSettings.skybox=previousSky;
            Destroy(scenery);Destroy(seaMesh);Destroy(bergMesh);Destroy(sky);Destroy(sea);
        }
    }
}
