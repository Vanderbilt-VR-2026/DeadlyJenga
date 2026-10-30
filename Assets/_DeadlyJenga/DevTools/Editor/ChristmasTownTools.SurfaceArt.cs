using System.IO;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyArt;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeadlyJenga.DevTools.Editor
{
    public static partial class ChristmasTownTools
    {
        static void SurfaceMaterials()
        {
            Mat("Metal",new Color(.62f,.72f,.76f),.72f).SetFloat("_Metallic",.5f);
            Mat("DinerTile",new Color(.18f,.36f,.4f),.6f);
            TextureMaterial("Biscuit",0,.35f,.18f);
            TextureMaterial("Snow",1,.18f,.16f);TextureMaterial("Frosting",1,.4f,.12f);
            TextureMaterial("Cocoa",2,.25f,.16f);TextureMaterial("Caramel",2,.3f,.12f);
            TextureMaterial("Road",3,.22f,.22f);TextureMaterial("Paving",3,.28f,.16f);
            foreach(string name in new[]{"Cherry","Mint","Rose","Lavender"})TextureMaterial(name,4,.5f,.08f);
            var glow=Mat("LanternGlow",new Color(1,.55f,.2f));
            glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(1,.38f,.08f)*2.5f);
            materials["WarmWindow"].SetColor("_EmissionColor",new Color(1,.47f,.16f)*1.8f);
        }
        static void TextureMaterial(string name,int kind,float smoothness,float bump)
        {
            const int size=256;var pixels=new Color[size*size];var normals=new Color[pixels.Length];var heights=new float[pixels.Length];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=x/(float)size,v=y/(float)size;
                uint h=(uint)(x*374761393+y*668265263+1274126177);h=(h^(h>>13))*1274126177;float grain=(h&65535)/65535f;
                float value=1,relief=grain*.08f;
                if(kind==0)
                {
                    float bake=Mathf.Sin(u*Mathf.PI*6)*Mathf.Sin(v*Mathf.PI*6);
                    value=.88f+grain*.16f+bake*.045f;if(grain<.012f)value*=.62f;relief=grain*.25f;
                }
                if(kind==1){value=.94f+grain*.09f;relief=grain*.08f;}
                if(kind==2)
                {
                    float wave=Mathf.Sin(v*Mathf.PI*6)*.025f;
                    float rings=Mathf.Sin((u+wave)*Mathf.PI*62+Mathf.Sin(v*Mathf.PI*2)*1.2f);
                    bool seam=Mathf.Repeat(v*4,1)<.025f;
                    value=seam?.52f:.87f+rings*.075f+grain*.12f;relief=seam?0:.5f+rings*.06f;
                }
                if(kind==3)
                {
                    int row=Mathf.FloorToInt(v*4);float px=Mathf.Repeat(u*4+(row%2)*.5f,1),py=Mathf.Repeat(v*4,1);
                    float edge=Mathf.Min(Mathf.Min(px,1-px),Mathf.Min(py,1-py));float joint=Mathf.SmoothStep(0,1,Mathf.Clamp01((edge-.015f)/.045f));
                    value=Mathf.Lerp(.55f,.91f+grain*.12f,joint);relief=joint*.65f+grain*.06f;
                }
                if(kind==4){value=.96f+grain*.06f;relief=grain*.06f;}
                pixels[y*size+x]=new Color(value,value,value,1);heights[y*size+x]=relief;
            }
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=heights[y*size+(x+1)%size]-heights[y*size+(x+size-1)%size];
                float dy=heights[((y+1)%size)*size+x]-heights[((y+size-1)%size)*size+x];
                var n=new Vector3(-dx*2,-dy*2,1).normalized;normals[y*size+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);
            }
            var material=materials[name];material.SetTexture("_BaseMap",TextureAsset(name+" grain",pixels,false));
            material.SetTexture("_BumpMap",TextureAsset(name+" relief",normals,true));material.SetFloat("_BumpScale",bump);
            material.EnableKeyword("_NORMALMAP");material.SetFloat("_Smoothness",smoothness);EditorUtility.SetDirty(material);
        }
        static Texture2D TextureAsset(string name,Color[] pixels,bool normal)
        {
            string folder=Folder(artFolder,"Textures"),path=folder+"/"+name+".png";
            var texture=new Texture2D(256,256,TextureFormat.RGBA32,false);texture.SetPixels(pixels);texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=!normal;importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;
            importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=2;importer.maxTextureSize=256;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static void SurfaceUv(GameObject go,Vector3 size)
        {
            var filter=go.GetComponent<MeshFilter>();var mesh=Object.Instantiate(filter.sharedMesh);var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];
            for(int i=0;i<uv.Length;i++)
            {
                var p=Vector3.Scale(vertices[i],size);var n=normals[i];
                uv[i]=(Mathf.Abs(n.x)>.5f?new Vector2(p.z,p.y):Mathf.Abs(n.y)>.5f?new Vector2(p.x,p.z):new Vector2(p.x,p.y))*.5f;
            }
            mesh.uv=uv;mesh.RecalculateTangents();filter.sharedMesh=mesh;
        }
        static Light TownLight(Transform parent,string name,Vector3 p,float range,float intensity)
        {
            var t=Group(parent,name,p);var light=t.gameObject.AddComponent<Light>();light.type=LightType.Point;
            light.color=new Color(1,.63f,.32f);light.range=range;light.intensity=intensity;light.shadows=LightShadows.None;light.renderMode=LightRenderMode.Auto;
            return light;
        }
        static void TownLighting()
        {
            var mood=art.GetComponent<ChristmasTownAtmosphere>();
            string path=artFolder+"/Blue hour sky.mat";var sky=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!sky){sky=new Material(Shader.Find("DeadlyJenga/ChristmasTown/Winter Sky"));AssetDatabase.CreateAsset(sky,path);}
            sky.shader=Shader.Find("DeadlyJenga/ChristmasTown/Winter Sky");
            if(!sky.shader)throw new System.Exception("Winter sky shader is missing.");
            sky.SetColor("_Zenith",new Color(.018f,.035f,.12f));sky.SetColor("_Horizon",new Color(.12f,.22f,.36f));
            sky.SetColor("_Aurora",new Color(.1f,.65f,.46f));sky.SetFloat("_AuroraStrength",.2f);sky.SetFloat("_Stars",.75f);
            sky.SetVector("_MoonDirection",new Vector4(-.6f,.35f,-.35f,0));EditorUtility.SetDirty(sky);mood.winterSky=sky;
            var moon=Group(art.transform,"Winter evening key light").gameObject.AddComponent<Light>();moon.type=LightType.Directional;moon.shadows=LightShadows.Soft;moon.shadowStrength=.75f;mood.keyLight=moon;
            var lighting=Group(art.transform,"Warm neighborhood lights");
            foreach(var house in ChristmasTownLayout.Houses)
            {
                var local=Group(lighting,house.name,house.position,house.yaw);
                TownLight(local,"Warm interior bounce",new Vector3(0,2.8f,0),7,4.5f);
                TownLight(local,"Porch lantern spill",new Vector3(0,2.3f,-5*house.scale.z-1),5,1.8f);
                for(int side=-1;side<=1;side+=2)
                {
                    var p=new Vector3(side*1.9f,2.1f,-5*house.scale.z-.35f);
                    Visual(local,"Lantern bracket",p+Vector3.up*.3f,new Vector3(.12f,.65f,.12f),"Cocoa");
                    Visual(local,"Amber lantern",p,new Vector3(.3f,.4f,.25f),"LanternGlow");
                    Visual(local,"Lantern cap",p+Vector3.up*.26f,new Vector3(.42f,.12f,.35f),"Cocoa");
                }
            }
            for(int side=-1;side<=1;side+=2)for(int x=-24;x<=24;x+=12)
            {
                TownLight(lighting,"Street lantern pool",new Vector3(x,2.6f,side*5.5f),6,2);
                Visual(lighting,"Lantern glass glow",new Vector3(x,2.5f,side*5.5f),new Vector3(.3f,.45f,.3f),"LanternGlow");
            }
            TownLight(lighting,"Tree golden lights",new Vector3(0,3,-3),10,2.5f);
            for(int i=0;i<24;i++)
            {
                float y=.8f+i*.27f,angle=i*2.4f,radius=(8-y)*.28f;
                Visual(lighting,"Tree fairy light",new Vector3(Mathf.Cos(angle)*radius,y,-3+Mathf.Sin(angle)*radius),Vector3.one*.12f,"LanternGlow",PrimitiveType.Sphere);
            }
            var volume=Group(art.transform,"Gentle desktop color grade").gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=25;
            string profilePath=artFolder+"/Winter evening grade.asset";var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,profilePath);}
            if(!profile.TryGet<Bloom>(out var bloom)){bloom=profile.Add<Bloom>();AssetDatabase.AddObjectToAsset(bloom,profile);}
            bloom.intensity.Override(.22f);bloom.threshold.Override(1.15f);bloom.scatter.Override(.55f);bloom.clamp.Override(3);
            if(!profile.TryGet<ColorAdjustments>(out var grade)){grade=profile.Add<ColorAdjustments>();AssetDatabase.AddObjectToAsset(grade,profile);}
            grade.postExposure.Override(.15f);grade.contrast.Override(7);grade.saturation.Override(5);
            EditorUtility.SetDirty(bloom);EditorUtility.SetDirty(grade);EditorUtility.SetDirty(profile);volume.sharedProfile=profile;mood.desktopGrade=volume;
            mood.localLights=lighting.GetComponentsInChildren<Light>();
        }
    }
}
