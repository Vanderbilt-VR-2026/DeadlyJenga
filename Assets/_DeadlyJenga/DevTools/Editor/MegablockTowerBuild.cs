using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyArt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class MegablockTowerBuild
    {
        public const string Id="megablock-tower", Title="Megablock Tower";
        static MegablockTowerBuild(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            string file=ProjectPath.Report("MegablockTower.request");
            if(!File.Exists(file)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            var request=File.ReadAllText(file).Trim().Split(' ');
            if(request.Length!=2||request[0]!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString())return;
            if(File.GetLastWriteTimeUtc(typeof(MegablockTowerLayout).Assembly.Location)<File.GetLastWriteTimeUtc(ProjectPath.Find("MegablockTowerLayout.cs"))||File.GetLastWriteTimeUtc(typeof(MegablockTowerChecks).Assembly.Location)<File.GetLastWriteTimeUtc(ProjectPath.Find("MegablockTowerChecks.cs")))return;
            if(!ValidationRunGate.TryAcquire("MegablockTower"))return;
            File.Delete(file);
            try{Build();if(request[1]=="test")MegablockTowerChecks.Run();else ValidationRunGate.Release("MegablockTower");}
            catch(Exception e){File.WriteAllText(ProjectPath.Report("MegablockTower-error.txt"),e.ToString());Debug.LogException(e);ValidationRunGate.Release("MegablockTower");}
        }
        static string Folder(string parent,string name)
        {string path=parent+"/"+name;if(!AssetDatabase.IsValidFolder(path))AssetDatabase.CreateFolder(parent,name);return path;}
        public static void RunIsolated()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Use this entry point only in the reserved graphics-enabled batch editor.");
            try
            {
                EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));Build();MegablockTowerChecks.Run();
            }
            catch(Exception e){File.WriteAllText(ProjectPath.Report("MegablockTower-error.txt"),e.ToString());Debug.LogException(e);EditorApplication.Exit(2);}
        }
        [MenuItem("Tools/Deadly Jenga/Maps/Build Megablock Tower")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before building map assets.");
            var catalog=Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");
            if(!catalog)throw new InvalidOperationException("Lobby catalog missing.");
            var previous=Services.World;var activeScene=SceneManager.GetActiveScene();
            var scratch=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scratch);
            var root=new GameObject(Title);
            try
            {
                var map=root.AddComponent<MegablockTowerMap>();map.generateOnStart=true;
                map.friction=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(ProjectPath.Find("Castle Friction.physicMaterial"));
                string visuals=Path.GetDirectoryName(ProjectPath.FolderOf("CandyEnvironment.cs").TrimEnd('/')).Replace('\\','/');
                string art=Folder(visuals,"MegablockTower");
                var colors=new[]{new Color(.12f,.76f,.43f),new Color(1,.19f,.24f),new Color(.53f,.23f,.90f),new Color(1,.85f,.44f)};
                map.palette=new Material[4];
                for(int i=0;i<4;i++)
                {
                    string materialPath=art+"/Tower-"+i+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,materialPath);}
                    mat.color=colors[i];mat.SetFloat("_Smoothness",.64f);mat.SetFloat("_Metallic",.04f);mat.enableInstancing=true;EditorUtility.SetDirty(mat);map.palette[i]=mat;
                }
                string maps=Path.GetDirectoryName(ProjectPath.FolderOf("MegablockTowerMap.cs").TrimEnd('/')).Replace('\\','/');
                string resources=Folder(maps,"Resources"),output=Folder(resources,"ModeMaps");
                var ground=new GameObject("Shore matched island");ground.transform.SetParent(root.transform,false);map.foundation=ground.transform;
                map.islandCollision=Terrain(map,catalog,output);
                ground.AddComponent<MeshCollider>().sharedMesh=map.islandCollision;
                var visual=new GameObject("Visual",typeof(MeshFilter),typeof(MeshRenderer));visual.transform.SetParent(ground.transform,false);
                visual.GetComponent<MeshFilter>().sharedMesh=map.islandCollision;visual.GetComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(ProjectPath.Find("Warm Sand.mat"));
                root.AddComponent<DeadlyJenga.Presentation.VillageFlatScenery>();
                string path=output+"/Mode_"+Id+".prefab";PrefabUtility.SaveAsPrefabAsset(root,path);
                var entries=new List<IslandLobbyCatalog.Map>(catalog.maps);
                var entry=entries.Find(m=>m.id==Id);if(entry==null){entry=new IslandLobbyCatalog.Map{id=Id};entries.Add(entry);}
                entry.title=Title;entry.mapPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);entry.scenePath=ProjectPath.Find("Game.unity");entry.walkableIsland=true;
                entry.recommendedMode="demo";entry.recommendedPlayers=16;entry.supportedModes=Array.Empty<string>();
                catalog.maps=entries.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
                var layout=MegablockTowerLayout.Build();
                File.WriteAllText(ProjectPath.Report("MegablockTower-build.txt"),$"{Title}: {MegablockTowerLayout.Floors} storeys; {MayhemStructureLayout.Bounds(layout).size}; bodies={layout.Count}; colliders={layout.Sum(m=>m.shapes.Count)}. Explicit new prefab and catalog entry; existing maps preserved.\n");
                EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup");
            }
            finally
            {
                Object.DestroyImmediate(root);Services.World=previous;
                if(activeScene.IsValid()&&activeScene.isLoaded)SceneManager.SetActiveScene(activeScene);
                EditorSceneManager.CloseScene(scratch,true);
            }
        }
        static Mesh Terrain(MegablockTowerMap map,IslandLobbyCatalog catalog,string folder)
        {
            var environment=catalog.environmentPrefab?catalog.environmentPrefab.GetComponent<CandyEnvironment>():null;
            var profile=catalog.art as CandyArtProfile;if(!profile&&environment)profile=environment.profile;
            if(!profile)throw new InvalidOperationException("Island art profile missing.");
            var probe=new GameObject("Temporary tower coast sampler");var sand=probe.AddComponent<CandySandbar>();
            try
            {
                sand.profile=profile;sand.map=map;sand.Refresh();var points=new List<Vector3>{Vector3.zero};var triangles=new List<int>();
                for(int i=0;i<128;i++)
                {
                    float angle=i*Mathf.PI*2/128;var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));float low=0,high=200;
                    for(int n=0;n<20;n++){float mid=(low+high)*.5f;if(sand.ShapeDistance(direction*mid)<-1)low=mid;else high=mid;}
                    points.Add(new Vector3(direction.x*low,0,direction.y*low));
                }
                for(int i=0;i<128;i++)triangles.AddRange(new[]{0,1+(i+1)%128,1+i});
                var mesh=new Mesh{name="Megablock flat island coast"};mesh.SetVertices(points);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                string path=folder+"/Megablock island collision.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);return saved;}
                AssetDatabase.CreateAsset(mesh,path);return mesh;
            }
            finally{Object.DestroyImmediate(probe);}
        }
    }
}
