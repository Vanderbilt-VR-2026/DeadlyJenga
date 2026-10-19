using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    public enum CandySceneSkin { TropicalDay,TropicalNight,TropicalSunset,Volcano,CloudKingdom,Desert,Ice }

    // Scene materials only. Never changes block, character, collider or water-height data.
    [DefaultExecutionOrder(500)]
    public sealed class CandySceneSkins : MonoBehaviour
    {
        public static CandySceneSkin Selected {get;private set;}
        public static readonly string[] Names=Enum.GetNames(typeof(CandySceneSkin));
        static readonly List<CandySceneSkins> live=new List<CandySceneSkins>();
        CandyEnvironment environment;CandyArtProfile original,copy;Material water,sand,sky;
        readonly List<GameObject> scenery=new List<GameObject>();
        Color ambient;float intensity;bool applied;CandySceneSkin appliedSkin;
        public static void Select(int index)
        {Selected=(CandySceneSkin)Mathf.Clamp(index,0,Names.Length-1);foreach(var item in live)if(item)item.Apply(Selected);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){live.Clear();Selected=CandySceneSkin.TropicalDay;}
        void OnEnable(){live.Add(this);environment=GetComponent<CandyEnvironment>();Apply(Selected);}
        void OnDisable(){live.Remove(this);Restore();}
        public void Apply(CandySceneSkin skin)
        {
            if(!environment||!environment.profile)return;
            if(applied&&appliedSkin==skin)return;
            Restore();original=environment.profile;copy=Instantiate(original);copy.name="Runtime scenic skin "+skin;
            water=original.water?new Material(original.water):null;sand=original.sand?new Material(original.sand):null;
            var shader=Resources.Load<Shader>("CandyScenery/CandyScenicSky");sky=shader?new Material(shader):original.sky?new Material(original.sky):null;
            copy.water=water;copy.sand=sand;copy.sky=sky;copy.sunEuler=original.sunEuler;
            Color zenith=new Color(.035f,.37f,.92f),horizon=new Color(.48f,.86f,1),island=new Color(.86f,.67f,.4f),shallow=new Color(.035f,.82f,.78f),deep=new Color(.015f,.32f,.65f);
            ambient=new Color(.55f,.72f,.9f);intensity=1;float stars=0,cover=.44f,style=0;
            switch(skin)
            {
                case CandySceneSkin.TropicalNight:zenith=new Color(.009f,.015f,.09f);horizon=new Color(.065f,.09f,.23f);shallow=new Color(.035f,.26f,.38f);deep=new Color(.007f,.04f,.14f);ambient=new Color(.25f,.3f,.5f);copy.sunColor=new Color(.55f,.7f,1);copy.sunIntensity=.7f;copy.sunEuler=new Vector3(32,-45,0);stars=1;cover=.22f;break;
                case CandySceneSkin.TropicalSunset:zenith=new Color(.27f,.12f,.53f);horizon=new Color(1,.46f,.38f);shallow=new Color(.28f,.66f,.68f);deep=new Color(.17f,.18f,.4f);ambient=new Color(.75f,.46f,.57f);copy.sunColor=new Color(1,.48f,.22f);copy.sunIntensity=1.25f;copy.sunEuler=new Vector3(12,-32,0);cover=.32f;break;
                case CandySceneSkin.Volcano:zenith=new Color(.08f,.025f,.07f);horizon=new Color(.8f,.19f,.035f);island=new Color(.12f,.075f,.095f);shallow=new Color(1,.45f,.015f);deep=new Color(.7f,.025f,.005f);ambient=new Color(.7f,.25f,.15f);copy.sunColor=new Color(1,.43f,.18f);copy.sunIntensity=1.4f;style=1;cover=.72f;break;
                case CandySceneSkin.CloudKingdom:island=new Color(.92f,.94f,1);shallow=new Color(.8f,.91f,1,.025f);deep=new Color(.83f,.91f,1,.025f);copy.waveHeight=.025f;cover=.62f;style=3;break;
                case CandySceneSkin.Desert:zenith=new Color(.42f,.65f,.8f);horizon=new Color(1,.77f,.43f);island=new Color(.9f,.62f,.25f);shallow=new Color(.8f,.5f,.17f);deep=new Color(.68f,.36f,.09f);ambient=new Color(.9f,.72f,.45f);copy.sunColor=new Color(1,.87f,.58f);copy.waveHeight=.08f;cover=.05f;style=2;break;
                case CandySceneSkin.Ice:zenith=new Color(.13f,.42f,.66f);horizon=new Color(.78f,.94f,1);island=new Color(.67f,.9f,.97f);shallow=new Color(.2f,.65f,.76f);deep=new Color(.055f,.22f,.37f);ambient=new Color(.62f,.8f,.92f);copy.sunColor=new Color(.85f,.95f,1);copy.waveHeight=.06f;cover=.08f;style=4;break;
            }
            if(sky){Set(sky,"_Zenith",zenith);Set(sky,"_Horizon",horizon);Set(sky,"_Cloud",skin==CandySceneSkin.Volcano?new Color(.22f,.12f,.16f):skin==CandySceneSkin.TropicalNight?new Color(.16f,.19f,.32f):Color.white);sky.SetFloat("_Stars",stars);sky.SetFloat("_CloudCover",cover);sky.SetVector("_SunDirection",Quaternion.Euler(copy.sunEuler)*Vector3.back);sky.SetColor("_SunColor",copy.sunColor);}
            if(water){Set(water,"_Shallow",shallow);Set(water,"_Deep",deep);Set(water,"_Reflection",horizon);Set(water,"_Horizon",horizon);water.SetFloat("_Glints",skin==CandySceneSkin.Ice?.12f:.3f);}
            if(sand){Set(sand,"_BaseColor",island);sand.SetFloat("_ScenicStyle",style);}
            if(style==1||style==2||style==3)
            {
                var liquid=Resources.Load<Shader>("CandyScenery/CandyScenicSurface");if(liquid&&water){water.shader=liquid;water.SetFloat("_Style",style);Set(water,"_BaseColor",deep);Set(water,"_Accent",shallow);}
            }
            environment.profile=copy;environment.Refresh();appliedSkin=skin;applied=true;
            if(skin==CandySceneSkin.Ice)BuildIce();
        }
        static void Set(Material m,string property,Color c){if(m.HasProperty(property))m.SetColor(property,c);}
        void LateUpdate()
        {
            if(!applied)return;
            RenderSettings.ambientSkyColor=ambient;RenderSettings.ambientEquatorColor=ambient*.72f;RenderSettings.ambientGroundColor=ambient*.45f;RenderSettings.ambientIntensity=intensity;
        }
        void BuildIce()
        {
            if(!water)return;var world=Services.World;float radius=world?.Radius??26;Vector3 center=world?.Center??transform.position;float y=world?.WaterHeight(center)??0;
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.name="Runtime scenic ice";material.SetColor("_BaseColor",new Color(.65f,.91f,1));material.SetFloat("_Smoothness",.86f);sandIce=material;
            for(int i=0;i<18;i++)
            {
                float a=i*2.399963f,r=radius*(1.5f+(i%4)*.65f);var go=new GameObject("Visual - distant ice floe");go.transform.SetParent(transform,false);go.transform.position=center+new Vector3(Mathf.Cos(a)*r,y-center.y-.3f,Mathf.Sin(a)*r);go.transform.rotation=Quaternion.Euler(0,i*43,0);go.transform.localScale=new Vector3(2+i%4,1+(i%3)*.7f,2+i%5);
                var mesh=go.AddComponent<MeshFilter>();mesh.sharedMesh=IceMesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;scenery.Add(go);
            }
        }
        Material sandIce;static Mesh iceMesh;
        static Mesh IceMesh {get{if(iceMesh)return iceMesh;iceMesh=new Mesh{name="Scenic low-poly iceberg"};iceMesh.vertices=new[]{new Vector3(-1,0,-1),new Vector3(1,0,-1),new Vector3(1,0,1),new Vector3(-1,0,1),new Vector3(-.4f,1,-.3f),new Vector3(.4f,.65f,.3f)};iceMesh.triangles=new[]{0,4,1,1,4,5,1,5,2,2,5,3,3,5,4,3,4,0,0,1,2,0,2,3};iceMesh.RecalculateNormals();return iceMesh;}}
        void Restore()
        {
            foreach(var go in scenery)if(go)Destroy(go);scenery.Clear();if(sandIce)Destroy(sandIce);
            if(environment&&original&&environment.profile==copy){environment.profile=original;environment.Refresh();}
            if(copy)Destroy(copy);if(water)Destroy(water);if(sand)Destroy(sand);if(sky)Destroy(sky);copy=null;original=null;applied=false;
        }
    }
}
