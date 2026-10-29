using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyArt;
using Object=UnityEngine.Object;
namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class EpicCityBuild
    {
        public const int BuildVersion=5;
        static double nextImport;
        static EpicCityBuild(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            string path=ProjectPath.Report("EpicCity.request");if(!File.Exists(path))return;var request=File.ReadAllText(path).Trim().Split(' ');
            if(request.Length!=3||request[0]!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString()||request[2]!=BuildVersion.ToString())return;
            var assembly=typeof(EpicCityMap).Assembly.Location;if(string.IsNullOrEmpty(assembly))assembly="Library/ScriptAssemblies/DeadlyJenga.World.dll";
            if(File.GetLastWriteTimeUtc(assembly)<File.GetLastWriteTimeUtc(ProjectPath.Find("EpicCityMap.cs"))||File.GetLastWriteTimeUtc(assembly)<File.GetLastWriteTimeUtc(ProjectPath.Find("EpicCityLayout.cs")))
            {if(EditorApplication.timeSinceStartup>nextImport){nextImport=EditorApplication.timeSinceStartup+5;AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);}return;}
            File.Delete(path);try{if(request[1]=="build")Build();else if(request[1]=="test")EpicCityValidation.Run();else if(request[1]=="streets")EpicCityValidation.Run(true);else throw new ArgumentException("Unknown city request");}
            catch(Exception e){File.WriteAllText(ProjectPath.Report("EpicCityBuild-error.txt"),e.ToString());Debug.LogException(e);}
        }
        static string Folder(string parent,string name){string p=parent+"/"+name;if(!AssetDatabase.IsValidFolder(p))AssetDatabase.CreateFolder(parent,name);return p;}
        public static void Run(){try{Build();if(Application.isBatchMode)EditorApplication.Exit(0);}catch(Exception e){File.WriteAllText(ProjectPath.Report("EpicCityBuild-error.txt"),e.ToString());Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(2);}}
        [MenuItem("Tools/Deadly Jenga/Maps/Build Ten Connected Epic Cities")]
        public static void Build()
        {
            var previous=Services.World;var catalog=Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");if(!catalog)throw new InvalidOperationException("Missing lobby catalog.");
            string maps=Path.GetDirectoryName(ProjectPath.FolderOf("EpicCityMap.cs").TrimEnd('/')).Replace('\\','/');
            string resources=Folder(maps,"Resources"),output=Folder(resources,"ModeMaps");
            var entries=new List<IslandLobbyCatalog.Map>(catalog.maps);var report=new StringBuilder("Ten connected epic cities. Explicit asset build; existing maps preserved.\n");
            for(int i=0;i<10;i++)
            {
                var root=new GameObject(EpicCityLayout.Names[i]);
                try
                {
                    root.AddComponent<DeadlyJenga.Presentation.VillageFlatScenery>();
                    var map=root.AddComponent<EpicCityMap>();map.design=i;map.palette=Palette(i);map.friction=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(ProjectPath.Find("Castle Friction.physicMaterial"));
                    var ground=new GameObject("Shore matched continuous city terrain");ground.transform.SetParent(root.transform,false);map.foundation=ground.transform;
                    map.islandCollision=Terrain(map,catalog,resources);ground.AddComponent<MeshCollider>().sharedMesh=map.islandCollision;
                    var groundVisual=new GameObject("Visual",typeof(MeshFilter),typeof(MeshRenderer));groundVisual.transform.SetParent(ground.transform,false);groundVisual.GetComponent<MeshFilter>().sharedMesh=map.islandCollision;groundVisual.GetComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(ProjectPath.Find("Warm Sand.mat"));
                    var art=PrefabUtility.InstantiatePrefab(Art(map)) as GameObject;art.name="Visual";art.transform.SetParent(root.transform,false);
                    string path=output+"/Mode_"+EpicCityLayout.Ids[i]+".prefab";PrefabUtility.SaveAsPrefabAsset(root,path);
                    var entry=entries.Find(e=>e.id==EpicCityLayout.Ids[i]);if(entry==null){entry=new IslandLobbyCatalog.Map{id=EpicCityLayout.Ids[i]};entries.Add(entry);}
                    entry.title=EpicCityLayout.Names[i];entry.mapPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);entry.scenePath=ProjectPath.Find("Game.unity");entry.walkableIsland=true;entry.recommendedMode="demo";entry.recommendedPlayers=32;entry.supportedModes=Array.Empty<string>();
                    report.AppendLine((i+1)+" "+entry.title+" bodies="+map.Plan.modules.Count+" shapes="+map.Plan.modules.Sum(m=>m.shapes.Count)+" sites="+map.Plan.sites.Count+" lowerStructures="+map.Plan.sites.Count(s=>s.kind=="Street fabric")+" elevated="+map.Plan.routes.Count(r=>!r.name.StartsWith("Ground street"))+" ground="+map.islandCollision.bounds.size);
                }
                finally{Object.DestroyImmediate(root);Services.World=previous;}
            }
            catalog.maps=entries.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();File.WriteAllText(ProjectPath.Report("EpicCityBuild.txt"),report.ToString());EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup");
        }
        public static Material[] Palette(int design)
        {
            string visuals=Path.GetDirectoryName(ProjectPath.FolderOf("CandyEnvironment.cs").TrimEnd('/')).Replace('\\','/');string folder=Folder(Folder(visuals,"EpicCities"),"Materials");
            Color[] colors={Color.HSVToRGB((.43f+design*.025f)%1,.74f,.8f),Color.HSVToRGB((.015f+design*.055f)%1,.70f,.98f),new Color(.58f,.36f,.95f),new Color(1,.84f,.55f)};
            var result=new Material[4];for(int n=0;n<4;n++){string path=folder+"/City-"+design+"-"+n+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="City candy "+design+" "+n};mat.color=colors[n];mat.SetFloat("_Smoothness",.65f);mat.SetFloat("_Metallic",.08f);mat.enableInstancing=true;AssetDatabase.CreateAsset(mat,path);}result[n]=mat;}return result;
        }
        static Mesh Terrain(EpicCityMap map,IslandLobbyCatalog catalog,string output)
        {
            var environment=catalog.environmentPrefab?catalog.environmentPrefab.GetComponent<CandyEnvironment>():null;var profile=catalog.art as CandyArtProfile;if(!profile&&environment)profile=environment.profile;if(!profile)throw new InvalidOperationException("Missing island art.");
            var probe=new GameObject("Temporary city coast sampler");var sand=probe.AddComponent<CandySandbar>();
            try
            {
                sand.profile=profile;sand.map=map;sand.Refresh();var outline=new Vector2[128];
                for(int i=0;i<128;i++){float angle=i*Mathf.PI*2/128;var dir=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));float low=0,high=600;for(int step=0;step<20;step++){float mid=(low+high)*.5f;if(sand.ShapeDistance(dir*mid)<-1)low=mid;else high=mid;}outline[i]=dir*low;}
                var mesh=EpicCityTerrain.Create(map.Plan,outline);string path=output+"/"+EpicCityLayout.Ids[map.design]+"-terrain.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);return saved;}AssetDatabase.CreateAsset(mesh,path);return mesh;
            }
            finally{Object.DestroyImmediate(probe);}
        }
        static GameObject Art(EpicCityMap map)
        {
            string visuals=Path.GetDirectoryName(ProjectPath.FolderOf("CandyEnvironment.cs").TrimEnd('/')).Replace('\\','/');string folder=Folder(visuals,"EpicCities");string path=folder+"/"+EpicCityLayout.Ids[map.design]+"-wayfinding.prefab";
            var root=new GameObject("City street wayfinding");
            try
            {
                var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                foreach(var site in map.Plan.sites)
                {
                    var sign=new GameObject(site.name);sign.transform.SetParent(root.transform,false);var p=site.position+Quaternion.Euler(0,site.heading,0)*new Vector3(0,1.8f,-site.depth*.5f-10);p.y=EpicCityLayout.GroundHeight(map.Plan,p)+1.8f;sign.transform.localPosition=p;
                    var text=sign.AddComponent<TextMesh>();text.text=site.name;text.font=font;text.fontSize=64;text.characterSize=.32f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.16f,.07f,.3f);text.GetComponent<MeshRenderer>().sharedMaterial=font.material;
                }
                var vertices=new List<Vector3>();var indices=new List<int>();
                foreach(var route in map.Plan.routes)
                {
                    var a=map.Plan.sites[route.from].position;var b=map.Plan.sites[route.to].position;var side=Vector3.Cross(Vector3.up,(b-a).normalized)*2.8f;int segments=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/3));
                    for(int n=0;n<segments;n++){var p=Vector3.Lerp(a,b,n/(float)segments);var q=Vector3.Lerp(a,b,(n+1)/(float)segments);int first=vertices.Count;foreach(var point in new[]{p-side,p+side,q+side,q-side}){var v=point;v.y=EpicCityLayout.GroundHeight(map.Plan,v)+.04f;vertices.Add(v);}indices.AddRange(new[]{first,first+2,first+1,first,first+3,first+2});}
                }
                var mesh=new Mesh{name="City ground street ribbons"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();string meshPath=folder+"/"+EpicCityLayout.Ids[map.design]+"-streets.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}else{saved=mesh;AssetDatabase.CreateAsset(saved,meshPath);}
                var road=new GameObject("Non-colliding ground streets",typeof(MeshFilter),typeof(MeshRenderer));road.transform.SetParent(root.transform,false);road.GetComponent<MeshFilter>().sharedMesh=saved;road.GetComponent<MeshRenderer>().sharedMaterial=map.palette[3];
                return PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
