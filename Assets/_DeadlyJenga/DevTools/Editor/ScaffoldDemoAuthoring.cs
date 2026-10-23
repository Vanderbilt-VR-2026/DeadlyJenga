using System;
using System.IO;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static class ScaffoldDemoAuthoring
    {
        [MenuItem("Tools/Deadly Jenga/Maps/Build Scaffold Demo")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            var previous=Services.World;
            try
            {
                string blocks=ProjectPath.FolderOf("MagneticCubePlaceholder.prefab");
                string maps=Path.GetDirectoryName(ProjectPath.FolderOf("BuildUpMap.cs").TrimEnd('/')).Replace('\\','/');
                string resources=Folder(maps,"Resources"),moduleFolder=Folder(resources,"BuildUpModules"),mapFolder=Folder(resources,"ModeMaps");
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("MagneticCubePlaceholder.prefab"));
                var candy=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("Candy Physics Block.prefab")).GetComponent<CastleBlock>();
                var materials=new[]{Material("Lavender.mat"),Material("Apple Green.mat"),Material("Cherry Red.mat")};
                Scaffold scaffold;
                var root=new GameObject("Snapped Scaffold");
                try
                {
                    scaffold=root.AddComponent<Scaffold>();scaffold.Configure();
                    var visual=GameObject.CreatePrimitive(PrimitiveType.Cube);visual.name="Gray-box fallback";visual.transform.SetParent(scaffold.Visual,false);Object.DestroyImmediate(visual.GetComponent<Collider>());visual.transform.localPosition=new Vector3(4,1,4);visual.transform.localScale=new Vector3(8,2,8);
                    scaffold=PrefabUtility.SaveAsPrefabAsset(root,blocks+"/Scaffold.prefab").GetComponent<Scaffold>();
                }
                finally{Object.DestroyImmediate(root);}
                var modules=new ScaffoldModule[3];
                for(int variant=0;variant<3;variant++)
                {
                    root=new GameObject(new[]{"Open Watchtower","Split Arch Gallery","Zigzag Stair Court"}[variant]);
                    try
                    {
                        root.AddComponent<ScaffoldModule>();
                        for(int y=0;y<5;y++)for(int x=0;x<4;x++)for(int z=0;z<4;z++)
                        {
                            bool corner=(x==0||x==3)&&(z==0||z==3);
                            bool use=variant==0?(y==0||corner||y==3&&(x==0||x==3||z==0||z==3)):
                                     variant==1?(y==0||x==0||x==3||y==4):
                                     (y==0||corner||y==1&&z==0||y==2&&x==3||y==3&&z==3||y==4&&x==0);
                            if(!use)continue;
                            var go=(GameObject)PrefabUtility.InstantiatePrefab(source);go.transform.SetParent(root.transform,false);go.name="Magnetic cube "+x+","+y+","+z;go.transform.localPosition=new Vector3(x*2+1,y*2+1,z*2+1);go.transform.localScale=Vector3.one;
                            var block=go.GetComponent<CastleBlock>();go.GetComponent<BoxCollider>().size=Vector3.one*2;go.GetComponent<Rigidbody>().mass=40;
                            block.appearance=candy.appearance;block.sectionMaterial=materials[y==0?0:variant==1?1:2];block.visualRoot.localScale=Vector3.one*2;
                            var fallback=block.visualRoot.GetComponent<Renderer>();if(fallback)fallback.enabled=false;
                            var magnet=go.GetComponent<MagneticBlock>()??go.AddComponent<MagneticBlock>();magnet.foundationAnchor=y==0;magnet.strength=y==0?3:variant==1?2:1;
                        }
                        modules[variant]=PrefabUtility.SaveAsPrefabAsset(root,moduleFolder+"/BuildUp_"+variant+".prefab").GetComponent<ScaffoldModule>();
                    }
                    finally{Object.DestroyImmediate(root);}
                }
                root=new GameObject("Beacon Scaffold Ascent");
                try
                {
                    var map=root.AddComponent<BuildUpMap>();map.scaffoldPrefab=scaffold;map.modulePrefabs=modules;
                    var floor=new GameObject("Fixed Foundation");floor.transform.SetParent(root.transform,false);floor.transform.localPosition=Vector3.down*.5f;var box=floor.AddComponent<BoxCollider>();box.size=new Vector3(44,1,44);map.foundation=floor.transform;
                    PrefabUtility.SaveAsPrefabAsset(root,mapFolder+"/Mode_buildup.prefab");
                }
                finally{Object.DestroyImmediate(root);}
                AssetDatabase.SaveAssets();File.WriteAllText(ProjectPath.Report("ScaffoldAuthoring.txt"),"COMPLETE: reusable scaffold, three magnetic building modules, Beacon Scaffold Ascent map. No existing scenes or prefabs overwritten.\n");
                Debug.Log("Scaffold demo authored. Choose Beacon Scaffold Ascent in lobby, Demo mode.");
            }
            finally{Services.World=previous;}
        }
        static Material Material(string name)=>AssetDatabase.LoadAssetAtPath<Material>(ProjectPath.Find(name));
        static string Folder(string parent,string name){string result=parent.TrimEnd('/')+"/"+name;if(!AssetDatabase.IsValidFolder(result))AssetDatabase.CreateFolder(parent.TrimEnd('/'),name);return result;}
    }
}

