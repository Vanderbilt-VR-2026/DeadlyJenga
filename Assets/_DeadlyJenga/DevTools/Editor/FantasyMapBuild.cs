using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class FantasyMapBuild
    {
        public static readonly string[] Ids={"epic-honeycomb-hive","epic-cloud-carnival","epic-candy-glass-cloud"};
        public static readonly string[] Titles={"Honeycomb Beehive","Cotton Candy Cloud Carnival","Candy Glass Above Clouds"};
        static FantasyMapBuild(){EditorApplication.update+=Tick;}
        static void Tick()
        {
            string file=ProjectPath.Report("FantasyMapBuild.request");
            if(!File.Exists(file)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(File.ReadAllText(file).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString())return;
            File.Delete(file);try{Build();}catch(Exception e){File.WriteAllText(ProjectPath.Report("FantasyMapBuild-error.txt"),e.ToString());Debug.LogException(e);}
        }
        static string Folder(string parent,string name)
        {string path=parent+"/"+name;if(!AssetDatabase.IsValidFolder(path))AssetDatabase.CreateFolder(parent,name);return path;}
        [MenuItem("Tools/Deadly Jenga/Maps/Build Approved Fantasy Maps")]
        public static void Build()
        {
            var catalog=Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");if(!catalog)throw new InvalidOperationException("Lobby catalog missing.");
            string visuals=Path.GetDirectoryName(ProjectPath.FolderOf("FantasyMapArt.cs").TrimEnd('/')).Replace('\\','/');
            string artFolder=Folder(visuals,"FantasyMaps"),materials=Folder(artFolder,"Materials");
            string maps=ProjectPath.FolderOf("Mode_epic-three-hearts.prefab").TrimEnd('/');
            var entries=new List<IslandLobbyCatalog.Map>(catalog.maps);var report=new System.Text.StringBuilder();var previous=Services.World;
            for(int i=0;i<3;i++)
            {
                string source=i==0?"Mode_epic-canopy-borough.prefab":"Mode_epic-three-hearts.prefab";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find(source));
                var go=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                try
                {
                    PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);go.name=Titles[i];
                    var map=go.GetComponent<EpicCityMap>();
                    foreach(var child in go.transform.Cast<Transform>().ToArray())if(child!=map.foundation)Object.DestroyImmediate(child.gameObject);
                    var old=go.GetComponent<VillageFlatScenery>();if(old)Object.DestroyImmediate(old);
                    var art=go.AddComponent<FantasyMapArt>();art.theme=(FantasyMapTheme)i;art.ground=map.foundation.GetComponentInChildren<Renderer>();
                    var bounds=map.islandCollision.bounds;art.halfWidth=bounds.extents.x;art.halfDepth=bounds.extents.z;
                    art.blockMaterials=Palette(i,materials);map.palette=art.blockMaterials;
                    art.groundMaterial=Surface(materials,Titles[i]+" ground",i==0?new Color(.96f,.64f,.17f):i==1?new Color(1,.91f,.97f):new Color(.97f,.99f,1),i==0?new Color(1,.85f,.42f):Color.white,i==0?0:3,.35f,.3f);
                    art.groundMaterial.SetVector("_CloudRadius",i==0?Vector4.zero:new Vector4(art.halfWidth,art.halfDepth,0,0));
                    art.cloudMaterial=Surface(materials,Titles[i]+" cloud",Color.white,new Color(.88f,.93f,1),3,.18f,.25f);
                    art.goldMaterial=Surface(materials,Titles[i]+" gold",new Color(1,.68f,.10f),new Color(1,.94f,.55f),i==0?0:2,.85f,.8f);
                    art.pinkMaterial=Surface(materials,Titles[i]+" pink",i==0?new Color(1,.49f,.05f):new Color(1,.25f,.57f),i==0?new Color(1,.84f,.22f):new Color(1,.75f,.87f),i==0?0:2,.9f,.8f);
                    art.blueMaterial=Surface(materials,Titles[i]+" aqua",i==0?new Color(1,.94f,.7f):new Color(.12f,.87f,.86f),Color.white,2,.72f,1);
                    art.darkMaterial=Surface(materials,Titles[i]+" dark",new Color(.09f,.045f,.018f),new Color(.23f,.11f,.03f),1,.35f,1);
                    art.skyMaterial=Sky(materials,i);
                    var reflection=FantasyMaterialLighting.Bake(art.skyMaterial,materials,i);
                    foreach(var material in art.blockMaterials.Concat(new[]{art.groundMaterial,art.cloudMaterial,art.goldMaterial,art.pinkMaterial,art.blueMaterial,art.darkMaterial}))
                    {if(material.HasProperty("_EnvironmentCube"))material.SetTexture("_EnvironmentCube",reflection);EditorUtility.SetDirty(material);}
                    var grain=Detail("PropolisGrain-v1.png");var wax=Detail("BeeswaxDetail-v1.png");
                    foreach(var material in art.blockMaterials.Concat(new[]{art.groundMaterial,art.cloudMaterial,art.goldMaterial,art.pinkMaterial,art.blueMaterial,art.darkMaterial}))
                    {
                        if(!material.HasProperty("_DetailTex"))continue;
                        bool wood=material.GetFloat("_Pattern")==1;material.SetTexture("_DetailTex",wood?grain:wax);
                        material.SetFloat("_DetailStrength",wood?1:material.GetFloat("_Pattern")==0?.35f:.06f);
                    }
                    if(i==1){art.blockMaterials[0].SetFloat("_Stripes",0);art.blockMaterials[1].SetFloat("_Stripes",.48f);art.blockMaterials[2].SetFloat("_Stripes",.32f);art.blockMaterials[3].SetFloat("_Stripes",.12f);}
                    art.goldMaterial.SetFloat("_Pattern",4);art.goldMaterial.SetFloat("_Metallic",i==0?.12f:.65f);art.goldMaterial.SetFloat("_Glow",i==1?.18f:0);
                    art.texturedCarnivalSky=i==1&&art.skyMaterial.shader.name=="DeadlyJenga/Carnival Cube Sky";
                    art.texturedEnvironmentSky=i!=1&&art.skyMaterial.shader.name=="DeadlyJenga/Carnival Cube Sky";
                    string path=maps+"/Mode_"+Ids[i]+".prefab";PrefabUtility.SaveAsPrefabAsset(go,path);
                    var entry=entries.Find(m=>m.id==Ids[i]);if(entry==null){entry=new IslandLobbyCatalog.Map{id=Ids[i]};entries.Add(entry);}
                    entry.title=Titles[i];entry.scenePath=ProjectPath.Find("Game.unity");entry.mapPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);entry.walkableIsland=true;entry.recommendedPlayers=16;entry.recommendedMode="demo";entry.supportedModes=Array.Empty<string>();
                    report.AppendLine(Ids[i]+" source="+source+" design="+map.design+" bodies="+map.Plan.modules.Count+" no physics changes; materials="+art.blockMaterials.Length);
                }
                finally{Object.DestroyImmediate(go);Services.World=previous;}
            }
            catalog.maps=entries.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
            File.WriteAllText(ProjectPath.Report("FantasyMapBuild.txt"),report.ToString());
            EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup");
        }
        static Material[] Palette(int theme,string folder)
        {
            if(theme==2)
            {
                var shader=Resources.Load<Shader>("FantasyMaps/FantasyGlass");
                if(!shader)throw new InvalidOperationException("Candy glass shader missing.");
                var colors=new[]{new Color(.09f,.93f,.19f,.33f),new Color(1,.025f,.13f,.34f),new Color(.57f,.07f,1,.32f),new Color(.97f,.045f,.18f,.34f)};
                return colors.Select((c,i)=>Material(folder,"Fruit glass "+i,shader,c)).ToArray();
            }
            if(theme==0)return new[]{Surface(folder,"Propolis wood",new Color(.32f,.14f,.045f),new Color(.58f,.30f,.09f),1,.5f,1),Surface(folder,"Golden wax cells",new Color(1,.61f,.12f),new Color(1,.86f,.35f),0,.83f,1),Surface(folder,"Amber honey",new Color(.96f,.40f,.035f),new Color(1,.82f,.26f),0,.95f,.8f),Surface(folder,"Pale beeswax",new Color(1,.85f,.42f),new Color(1,.96f,.74f),0,.6f,.6f)};
            return new[]{Surface(folder,"Blueberry star posts",new Color(.105f,.10f,.34f),new Color(.23f,.22f,.57f),2,.82f,.6f),Surface(folder,"Mint icing",new Color(.22f,.89f,.76f),new Color(.85f,1,.91f),2,.78f,.7f),Surface(folder,"Raspberry candy",new Color(.88f,.14f,.48f),new Color(.77f,.38f,.93f),2,.82f,.8f),Surface(folder,"Vanilla sugar",new Color(1,.87f,.59f),new Color(1,.97f,.84f),2,.7f,.7f)};
        }
        static Material Material(string folder,string name,Shader shader,Color color)
        {
            string path=folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;
            if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",color);mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
        }
        static Material Surface(string folder,string name,Color color,Color accent,int pattern,float gloss,float scale)
        {
            var shader=Resources.Load<Shader>("FantasyMaps/FantasySurface");if(!shader)throw new InvalidOperationException("Fantasy surface shader missing.");
            var mat=Material(folder,name,shader,color);mat.SetColor("_Accent",accent);mat.SetFloat("_Pattern",pattern);mat.SetFloat("_Smoothness",gloss);mat.SetFloat("_Scale",scale);mat.SetFloat("_Metallic",0);mat.SetFloat("_Stripes",.35f);mat.SetFloat("_Glow",0);return mat;
        }
        static Texture2D Detail(string name)
        {
            string path=ProjectPath.Find(name);if(string.IsNullOrEmpty(path))return null;
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer){importer.wrapMode=TextureWrapMode.Repeat;importer.maxTextureSize=1024;importer.mipmapEnabled=true;importer.anisoLevel=4;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();}
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Material Sky(string folder,int theme)
        {
            if(theme!=1)
            {
                string themedPath=ProjectPath.Find(theme==0?"HiveSkyCube-v1.mat":"GlassSkyCube-v1.mat");
                if(!string.IsNullOrEmpty(themedPath)){var themed=AssetDatabase.LoadAssetAtPath<Material>(themedPath);if(themed)return themed;}
            }
            if(theme==1)
            {
                string cubePath=ProjectPath.Find("CarnivalCube-v1.mat");
                if(!string.IsNullOrEmpty(cubePath)){var cube=AssetDatabase.LoadAssetAtPath<Material>(cubePath);if(cube)return cube;}
                string panoramaPath=ProjectPath.Find("CarnivalPanorama-v1.mat");
                if(!string.IsNullOrEmpty(panoramaPath))
                {var panorama=AssetDatabase.LoadAssetAtPath<Material>(panoramaPath);if(panorama)return panorama;}
            }
            var shader=Resources.Load<Shader>("CandyScenery/CandyScenicSky");if(!shader)throw new InvalidOperationException("Sky shader missing.");
            var mat=Material(folder,"Fantasy sky "+theme,shader,Color.white);
            mat.SetColor("_Zenith",theme==0?new Color(.58f,.27f,.04f):theme==1?new Color(.40f,.44f,.88f):new Color(.08f,.45f,.94f));
            mat.SetColor("_Horizon",theme==0?new Color(1,.74f,.27f):theme==1?new Color(1,.71f,.65f):new Color(.68f,.88f,1));
            mat.SetColor("_Cloud",theme==1?new Color(1,.84f,.91f):Color.white);mat.SetFloat("_CloudCover",theme==0?0:.30f);mat.SetFloat("_Stars",0);mat.SetVector("_SunDirection",Quaternion.Euler(35,-32,0)*Vector3.back);mat.SetColor("_SunColor",theme==0?new Color(1,.77f,.3f):Color.white);return mat;
        }
    }
}
