using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyArt
{
    // Instanced candy shapes only. Collision and damage live in World.
    public sealed class CandyDisasterVisuals : MonoBehaviour
    {
        Mesh sphere,funnel;
        Vector3[] funnelVertices,funnelNormals;
        int[] funnelTriangles;
        float nextIceScan;
        Material coral,lemon,lavender,aqua,lime;
        readonly Matrix4x4[] matrices=new Matrix4x4[1023];
        readonly List<Matrix4x4> orange=new List<Matrix4x4>(),yellow=new List<Matrix4x4>(),purple=new List<Matrix4x4>(),blue=new List<Matrix4x4>(),green=new List<Matrix4x4>();
        readonly Dictionary<Renderer,MaterialPropertyBlock> iceOriginals=new Dictionary<Renderer,MaterialPropertyBlock>();
        MaterialPropertyBlock block;
        CandyEnvironment environment;
        void Awake()
        {
            environment=GetComponent<CandyEnvironment>();sphere=Sphere();
            coral=Material(new Color(1,.13f,.06f));lemon=Material(new Color(1,.7f,.08f));lavender=Material(new Color(.54f,.25f,1));aqua=Material(new Color(.24f,.9f,1));lime=Material(new Color(.45f,1,.08f));
        }
        Material Material(Color color)
        {
            var template=environment&&environment.profile?environment.profile.red:null;
            var m=template?new Material(template):new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name="Candy disaster material";m.SetColor("_BaseColor",color);m.SetColor("_Color",color);m.SetFloat("_Smoothness",.85f);m.SetFloat("_Metallic",.12f);m.enableInstancing=true;return m;
        }
        void LateUpdate()
        {
            if(!sphere||!coral)return;
            orange.Clear();yellow.Clear();purple.Clear();blue.Clear();green.Clear();bool icy=false;
            foreach(var source in DisasterStates.Active)
            {
                var f=source.Capture();if(f==null||f.warning>0)continue;
                switch(f.kind)
                {
                    case "MeteorShower":case "PlanetKillerMeteor":Meteors(f);break;
                    case "Fire":Fire(f);break;
                    case "Tornado":Tornado(f);break;
                    case "Icey":icy=true;Ice();break;
                    case "AcidRain":Rain(f);break;
                    case "AntiGravity":Bubbles(f);break;
                    case "Earthquake":Dust(f);break;
                }
            }
            if(!icy)RestoreIce();
            Draw(orange,coral);Draw(yellow,lemon);Draw(purple,lavender);Draw(blue,aqua);Draw(green,lime);
        }
        static float Hash(int i)=>Mathf.Repeat(Mathf.Sin(i*127.1f+311.7f)*43758.54f,1);
        void Ball(List<Matrix4x4> list,Vector3 p,Vector3 scale)=>list.Add(Matrix4x4.TRS(p,Quaternion.identity,scale));
        void Meteors(DisasterFrame f)
        {
            if(f.points==null)return;
            foreach(var p in f.points)
            {
                Vector3 position=p.position+p.velocity*Mathf.Clamp(Time.time-f.sampledAt,0,.25f);
                Ball(orange,position,Vector3.one*p.size);
                Vector3 back=-p.velocity.normalized;
                for(int i=1;i<=3;i++)Ball(i%2==0?yellow:orange,position+back*p.size*(.4f+i*.35f),Vector3.one*p.size*(.55f-i*.1f));
            }
        }
        void Fire(DisasterFrame f)
        {
            var world=Services.World;if(world==null||f.blocks==null)return;
            int stride=Mathf.Max(1,Mathf.CeilToInt(f.blocks.Length/192f));
            for(int i=0;i<f.blocks.Length;i+=stride)
            {
                int id=f.blocks[i];if(id<0||id>=world.Blocks.Count)continue;var rb=world.Blocks[id];if(!rb||!rb.gameObject.activeInHierarchy)continue;
                var collider=rb.GetComponent<Collider>();if(!collider)continue;var bounds=collider.bounds;
                for(int j=0;j<3;j++)
                {
                    float phase=Time.time*5+id*1.7f+j*2,scale=.65f+.18f*Mathf.Sin(phase);
                    Vector3 p=new Vector3(bounds.center.x+Mathf.Sin(j*2.4f)*bounds.extents.x*.6f,bounds.max.y+.35f,bounds.center.z+Mathf.Cos(j*2.4f)*bounds.extents.z*.6f);
                    Ball(orange,p,new Vector3(scale,scale*1.7f,scale));Ball(yellow,p+Vector3.up*.35f,new Vector3(scale*.55f,scale*1.5f,scale*.55f));
                }
            }
        }
        void Tornado(DisasterFrame f)
        {
            if(f.values==null||f.values.Length<4)return;
            float height=f.values[0],size=f.values[1],ferocity=f.values[2];
            if(!funnel)funnel=new Mesh{name="Candy cyclone funnel"};
            const int rows=24,cols=32;
            if(funnelVertices==null){funnelVertices=new Vector3[(rows+1)*(cols+1)];funnelNormals=new Vector3[funnelVertices.Length];funnelTriangles=new int[rows*cols*6];}
            var vertices=funnelVertices;var normals=funnelNormals;var triangles=funnelTriangles;int index=0;
            for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++)
            {
                float t=y/(float)rows,a=x/(float)cols*Mathf.PI*2+t*4+Time.time*(1+ferocity*.12f),r=Mathf.Lerp(1,size*.5f,t*t);
                vertices[y*(cols+1)+x]=new Vector3(Mathf.Cos(a)*r,t*height,Mathf.Sin(a)*r);normals[y*(cols+1)+x]=new Vector3(Mathf.Cos(a),-.15f,Mathf.Sin(a)).normalized;
                if(y<rows&&x<cols){int i=y*(cols+1)+x;triangles[index++]=i;triangles[index++]=i+cols+1;triangles[index++]=i+1;triangles[index++]=i+1;triangles[index++]=i+cols+1;triangles[index++]=i+cols+2;}
            }
            funnel.Clear();funnel.vertices=vertices;funnel.normals=normals;funnel.triangles=triangles;funnel.RecalculateBounds();
            Graphics.DrawMesh(funnel,Matrix4x4.TRS(f.center,Quaternion.identity,Vector3.one),lavender,0,null,0,null,ShadowCastingMode.Off,false);
            for(int i=0;i<90;i++){float t=i/89f,a=t*40+Time.time*(1+ferocity*.12f),r=Mathf.Lerp(1.3f,size*.54f,t*t);Ball(blue,f.center+new Vector3(Mathf.Cos(a)*r,t*height,Mathf.Sin(a)*r),Vector3.one*Mathf.Lerp(.3f,1.4f,t));}
        }
        void Rain(DisasterFrame f)
        {
            int n=Mathf.Clamp(Mathf.RoundToInt(f.values[1]*.7f),10,420);
            for(int i=0;i<n;i++)
            {
                float h=Mathf.Repeat(Hash(i+300)-Time.time*.45f,1);
                var p=f.center+new Vector3((Hash(i)-.5f)*f.size.x,(h-.4f)*(f.size.y+30),(Hash(i+800)-.5f)*f.size.z);
                Ball(green,p,new Vector3(.06f,.8f,.06f));
            }
        }
        void Bubbles(DisasterFrame f)
        {for(int i=0;i<90;i++){var p=f.center+new Vector3((Hash(i)-.5f)*f.size.x,(Mathf.Repeat(Time.time*.12f+Hash(i+90),1)-.5f)*f.size.y,(Hash(i+180)-.5f)*f.size.z);Ball(purple,p,Vector3.one*(.15f+Hash(i+42)*.3f));}}
        void Dust(DisasterFrame f)
        {float water=Services.World?.WaterHeight(f.center)??0;for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64,r=Mathf.Max(f.size.x,f.size.z)*.4f;Ball(yellow,new Vector3(f.center.x+Mathf.Cos(a)*r,water+.25f+Mathf.Abs(Mathf.Sin(Time.time*4+i))*.5f,f.center.z+Mathf.Sin(a)*r),new Vector3(1,.5f,1));}}
        void Ice()
        {
            if(Time.time<nextIceScan)return;nextIceScan=Time.time+1;
            block ??= new MaterialPropertyBlock();
            var world=Services.World;if(world==null)return;
            foreach(var body in world.Blocks)
            {
                if(!body)continue;
                var climb=body.GetComponent<IClimbable>();var visual=climb?.Visual;if(!visual)continue;
                foreach(var renderer in visual.GetComponentsInChildren<Renderer>())
                {
                    if(iceOriginals.ContainsKey(renderer))continue;
                    var original=new MaterialPropertyBlock();renderer.GetPropertyBlock(original);iceOriginals.Add(renderer,original);
                    renderer.GetPropertyBlock(block);block.SetFloat("_Smoothness",.98f);
                    var material=renderer.sharedMaterial;Color color=material&&material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):Color.white;
                    block.SetColor("_BaseColor",Color.Lerp(color,new Color(.45f,.9f,1),.45f));renderer.SetPropertyBlock(block);
                }
            }
        }
        void RestoreIce(){foreach(var pair in iceOriginals)if(pair.Key)pair.Key.SetPropertyBlock(pair.Value);iceOriginals.Clear();nextIceScan=0;}
        void Draw(List<Matrix4x4> list,Material material)
        {
            for(int start=0;start<list.Count;start+=1023){int count=Mathf.Min(1023,list.Count-start);list.CopyTo(start,matrices,0,count);Graphics.DrawMeshInstanced(sphere,0,material,matrices,count,null,ShadowCastingMode.Off,false);}
        }
        static Mesh Sphere()
        {
            const int rows=12,cols=20;var v=new Vector3[(rows+1)*(cols+1)];var n=new Vector3[v.Length];var uv=new Vector2[v.Length];var t=new int[rows*cols*6];int k=0;
            for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++){float a=x/(float)cols*Mathf.PI*2,b=y/(float)rows*Mathf.PI;int i=y*(cols+1)+x;n[i]=new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a));v[i]=n[i]*.5f;uv[i]=new Vector2(x/(float)cols,y/(float)rows);if(y<rows&&x<cols){t[k++]=i;t[k++]=i+1;t[k++]=i+cols+1;t[k++]=i+1;t[k++]=i+cols+2;t[k++]=i+cols+1;}}
            return new Mesh{name="Smooth candy disaster sphere",vertices=v,normals=n,uv=uv,triangles=t};
        }
        void OnDisable()=>RestoreIce();
        void OnDestroy(){RestoreIce();foreach(var m in new[]{coral,lemon,lavender,aqua,lime})if(m)Destroy(m);if(sphere)Destroy(sphere);if(funnel)Destroy(funnel);}
    }
}
