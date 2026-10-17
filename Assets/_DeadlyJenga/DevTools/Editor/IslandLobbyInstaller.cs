using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyArt;

namespace DeadlyJenga.CastlePrototype.Editor
{
    public static class IslandLobbyInstaller
    {
        [MenuItem("Tools/Deadly Jenga/Install Island Lobby")]
        public static void Install()
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("Stop Play Mode before replacing the lobby.");
            string lobbyPath=ProjectPath.Find("Lobby.unity"),gamePath=ProjectPath.Find("Game.unity");
            var game=EditorSceneManager.OpenScene(gamePath,OpenSceneMode.Single);
            var session=Object.FindFirstObjectByType<CastlePlaySession>();if(!session)throw new System.InvalidOperationException("Game session is missing");
            var environment=Object.FindFirstObjectByType<CandyEnvironment>();
            var prefab=AssetDatabase.LoadAssetAtPath<CandyEnvironment>(ProjectPath.Find("Candy Environment.prefab"));
            string folder=Path.GetDirectoryName(ProjectPath.FolderOf("IslandLobbyCatalog.cs").TrimEnd('/')).Replace('\\','/')+"/Resources/IslandLobby";
            EnsureFolder(folder);string path=folder+"/Catalog.asset";
            var catalog=AssetDatabase.LoadAssetAtPath<IslandLobbyCatalog>(path);
            if(!catalog){catalog=ScriptableObject.CreateInstance<IslandLobbyCatalog>();AssetDatabase.CreateAsset(catalog,path);}
            catalog.survivorPrefab=session.survivorPrefab.GetComponent<CastleSurvivor>();catalog.environmentPrefab=prefab;catalog.art=environment?environment.profile:prefab.profile;
            catalog.maps[0].scenePath=gamePath;EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Island lobby · shared character and castle art");root.AddComponent<IslandLobby>().catalog=catalog;
            // The character prefab owns the camera and XR input. No second XR rig or spawner.
            RenderSettings.skybox=catalog.art.sky;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.55f,.72f,.9f);RenderSettings.ambientEquatorColor=new Color(.43f,.57f,.64f);RenderSettings.ambientGroundColor=new Color(.3f,.3f,.36f);
            EditorSceneManager.SaveScene(scene,lobbyPath);AssetDatabase.SaveAssets();
            File.WriteAllText(ProjectPath.Report("IslandLobbyInstall.txt"),"Lobby replaced through Unity scene APIs; current survivor and environment prefab references retained.\n"+path+"\n");
        }
        static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
    }
}
