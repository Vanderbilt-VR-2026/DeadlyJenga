using System;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyArt;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.SceneManagement;

namespace DeadlyJenga.DevTools.Editor
{
    public static partial class ChristmasTownTools
    {
        public static void CaptureWeatherPreview()
        {
            var saved=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(ProjectPath.Find("Christmas Candy Town Preview.unity"));
                var map=UnityEngine.Object.FindFirstObjectByType<ChristmasTownMap>();art=map.transform.Find("Visual").gameObject;
                SetupPreview();
                foreach(var snow in art.GetComponentsInChildren<ChristmasTownSnow>())
                {snow.RefreshMask();snow.flakes.Simulate(18,true,true,true);snow.flakes.Pause();}
                Capture(new Vector3(18,4,-7),new Vector3(0,3,-3),"ChristmasTown-evening-square.png");
                Capture(new Vector3(-13.5f,1.6f,23),new Vector3(-10.5f,1.2f,28),"ChristmasTown-home.png");
                Capture(new Vector3(13,7,-10),new Vector3(0,2,-25),"ChristmasTown-diner-exterior.png");
            }
            finally
            {
                if(saved.Length>0&&System.Array.TrueForAll(saved,s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(saved);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
        static void WinterBackdrop()
        {
            var backdrop=Group(art.transform,"Visual backdrop - outside playable bounds");
            Visual(backdrop,"Distant snow carpet",new Vector3(0,-1.4f,0),new Vector3(240,.3f,220),"Snow");
            for(int i=0;i<22;i++)
            {
                float angle=i*Mathf.PI*2/22;
                var p=new Vector3(Mathf.Cos(angle)*76,-4,Mathf.Sin(angle)*65);
                Visual(backdrop,"Sheltering snowy hill",p,new Vector3(38+(i%3)*5,24+(i%4)*4,35+(i%3)*4),"Snow",PrimitiveType.Sphere);
            }
            for(int side=-1;side<=1;side+=2)
            {
                for(int x=-54;x<=54;x+=4)
                {
                    float z=side*(44+Mathf.Abs(Mathf.Sin(x*1.7f))*2.5f);
                    Prop(backdrop,(x/4)%2==0?"tree-snow-a":"tree-snow-b",new Vector3(x,-.9f,z),2.9f+Mathf.Abs(Mathf.Sin(x))*.9f,x*23);
                }
                for(int z=-40;z<=40;z+=4)
                {
                    float x=side*(54+Mathf.Abs(Mathf.Sin(z*1.3f))*2.5f);
                    Prop(backdrop,(z/4)%2==0?"tree-snow-a":"tree-snow-b",new Vector3(x,-.9f,z),3+Mathf.Abs(Mathf.Cos(z))*.9f,z*19);
                }
            }
        }
        static void OutdoorSnow()
        {
            const int width=256,depth=192;int mask=(1<<Layers.Surface)|(1<<Layers.Block);
            var heights=new float[width*depth];Physics.SyncTransforms();
            for(int z=0;z<depth;z++)for(int x=0;x<width;x++)
            {
                var p=new Vector3((x+.5f)/width*96-48,24,(z+.5f)/depth*76-38);
                if(Physics.Raycast(p,Vector3.down,out var hit,32,mask,QueryTriggerInteraction.Ignore))heights[z*width+x]=hit.point.y;
            }
            // Conservative expansion prevents wind from carrying flakes through open windows.
            var colors=new Color[heights.Length];
            for(int z=0;z<depth;z++)for(int x=0;x<width;x++)
            {
                float top=0;
                for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)top=Mathf.Max(top,heights[Mathf.Clamp(z+dz,0,depth-1)*width+Mathf.Clamp(x+dx,0,width-1)]);
                colors[z*width+x]=new Color(top,0,0,1);
            }
            string path=artFolder+"/Outdoor snow shelter heights.asset";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(!texture){texture=new Texture2D(width,depth,TextureFormat.RFloat,false,true);AssetDatabase.CreateAsset(texture,path);}
            texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Point;texture.SetPixels(colors);texture.Apply();EditorUtility.SetDirty(texture);
            foreach(var h in ChristmasTownLayout.Houses)
            {
                int x=Mathf.FloorToInt((h.position.x+48)/96*width),z=Mathf.FloorToInt((h.position.z+38)/76*depth);
                if(texture.GetPixel(x,z).r<3)throw new Exception("Snow shelter mask misses "+h.name);
            }
            var material=Mat("Outdoor snowfall",Color.white);material.shader=Shader.Find("DeadlyJenga/ChristmasTown/Outdoor Snow");
            if(!material.shader)throw new Exception("Outdoor snow shader is missing.");
            material.SetTexture("_RoofMap",texture);material.SetColor("_Color",new Color(.85f,.92f,1,.65f));EditorUtility.SetDirty(material);
            var root=Group(art.transform,"Light outdoor snowfall",new Vector3(0,18,0));
            var flakes=root.gameObject.AddComponent<ParticleSystem>();flakes.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=flakes.main;main.loop=true;main.prewarm=true;main.duration=18;main.startLifetime=18;
            main.startSpeed=0;main.startSize=new ParticleSystem.MinMaxCurve(.045f,.11f);main.maxParticles=700;
            main.simulationSpace=ParticleSystemSimulationSpace.Local;main.startColor=Color.white;main.gravityModifier=0;
            var shape=flakes.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(95,.1f,75);
            var emission=flakes.emission;emission.rateOverTime=36;
            var velocity=flakes.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;
            velocity.x=new ParticleSystem.MinMaxCurve(.08f,.22f);velocity.y=new ParticleSystem.MinMaxCurve(-1.25f,-.95f);velocity.z=new ParticleSystem.MinMaxCurve(.02f,.12f);
            var collision=flakes.collision;collision.enabled=false;
            var visual=flakes.GetComponent<ParticleSystemRenderer>();visual.sharedMaterial=material;visual.renderMode=ParticleSystemRenderMode.Billboard;
            visual.shadowCastingMode=ShadowCastingMode.Off;visual.receiveShadows=false;visual.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
            var snow=root.gameObject.AddComponent<ChristmasTownSnow>();snow.mapSpace=art.transform;snow.flakes=flakes;snow.shelterHeights=texture;snow.RefreshMask();
            flakes.useAutoRandomSeed=false;flakes.randomSeed=424242;flakes.Simulate(16,true,true,true);flakes.Pause();
        }
    }
}
