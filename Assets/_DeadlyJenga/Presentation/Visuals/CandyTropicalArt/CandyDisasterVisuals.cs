using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyArt
{
    // Instanced candy shapes only. Collision and damage live in World.
    public sealed partial class CandyDisasterVisuals : MonoBehaviour
    {
        Mesh sphere,funnel;
        Vector3[] funnelVertices,funnelNormals;
        int[] funnelTriangles;
        float nextIceScan;GameModeKind iceMode;bool hasIceMode;
        Material coral,lemon,lavender,aqua,lime;
        readonly Matrix4x4[] matrices=new Matrix4x4[1023];
        readonly List<Matrix4x4> orange=new List<Matrix4x4>(),yellow=new List<Matrix4x4>(),purple=new List<Matrix4x4>(),blue=new List<Matrix4x4>(),green=new List<Matrix4x4>();
        readonly Dictionary<Renderer,MaterialPropertyBlock> iceOriginals=new Dictionary<Renderer,MaterialPropertyBlock>();
        MaterialPropertyBlock block;
        CandyEnvironment environment;
        void Awake()
        {
            environment=GetComponent<CandyEnvironment>();sphere=Sphere();InitializeObjects();InitializeFire();
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
            var mode=MatchRuntime.Current?.Rules.mode??GameModeKind.Demo;if(!hasIceMode||mode!=iceMode){RestoreIce();iceMode=mode;hasIceMode=true;}
            bool ending=MatchRuntime.Current?.Frame.phase==RoundPhase.Victory||MatchRuntime.Current?.Frame.phase==RoundPhase.Results;
            orange.Clear();yellow.Clear();purple.Clear();blue.Clear();green.Clear();BeginObjects();BeginFire();bool icy=false;
            foreach(var source in DisasterStates.Active)
            {
                var f=source.Capture();if(f==null||f.warning>0)continue;drawingSource=source;
                switch(f.kind)
                {
                    case "MeteorShower":case "PlanetKillerMeteor":Meteors(f);break;
                    case "Fire":if(!ending)Fire(f);break;
                    case "Tornado":Tornado(f);break;
                    case "Icey":if(!ending){icy=true;Ice();}break;
                    case "AcidRain":Rain(f);break;
                    case "AntiGravity":Bubbles(f);break;
                    case "Earthquake":Dust(f);break;
                }
            }
            if(!icy)RestoreIce();
            Draw(orange,coral);Draw(yellow,lemon);Draw(purple,gravityBubble);Draw(blue,aqua);Draw(green,lime);RenderFire();RenderObjects();
        }
        static float Hash(int i)=>Mathf.Repeat(Mathf.Sin(i*127.1f+311.7f)*43758.54f,1);
        void Ball(List<Matrix4x4> list,Vector3 p,Vector3 scale)=>list.Add(Matrix4x4.TRS(p,Quaternion.identity,scale));
        void Bubbles(DisasterFrame f)
        {for(int i=0;i<90;i++){var p=f.center+new Vector3((Hash(i)-.5f)*f.size.x,(Mathf.Repeat(Time.time*.12f+Hash(i+90),1)-.5f)*f.size.y,(Hash(i+180)-.5f)*f.size.z);Ball(purple,p,Vector3.one*(.15f+Hash(i+42)*.3f));}}
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
                    renderer.SetPropertyBlock(block);
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
        void OnEnable()=>GameEvents.BlockHit+=MeteorContact;
        void OnDisable(){RestoreIce();ClearCharred();GameEvents.BlockHit-=MeteorContact;ClearObjects();}
        void OnDestroy(){RestoreIce();DestroyObjects();DestroyFire();foreach(var m in new[]{coral,lemon,lavender,aqua,lime})if(m)Destroy(m);if(sphere)Destroy(sphere);if(funnel)Destroy(funnel);}
    }
}



