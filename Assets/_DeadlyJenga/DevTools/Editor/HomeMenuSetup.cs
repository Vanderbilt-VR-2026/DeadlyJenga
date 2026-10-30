using System;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class HomeMenuSetup
    {
        static HomeMenuSetup() => EditorApplication.update += Tick;
        static void Tick()
        {
            string request=ProjectPath.Report("HomeMenuSetup.request");
            if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(File.ReadAllText(request).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString())return;
            File.Delete(request);
            try { Install(); File.WriteAllText(ProjectPath.Report("HomeMenuSetup.result"),"PASS: Home scene installed first in build settings."); }
            catch(Exception error){File.WriteAllText(ProjectPath.Report("HomeMenuSetup.result"),error.ToString());}
        }
        [MenuItem("Tools/Deadly Jenga/Install Home Menu")]
        public static void Install()
        {
            var previous=EditorSceneManager.GetSceneManagerSetup();
            if(previous.Any(s=>s.isLoaded&&(string.IsNullOrEmpty(s.path)||SceneManager.GetSceneByPath(s.path).isDirty)))throw new InvalidOperationException("Unsaved scenes left untouched.");
            string homePath=ProjectPath.FolderOf("Lobby.unity")+"Home.unity";
            try
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var home=new GameObject("Home session").AddComponent<HomeSession>();
                home.catalog=AssetDatabase.LoadAssetAtPath<IslandLobbyCatalog>(ProjectPath.Find("Catalog.asset"));
                if(!home.catalog)throw new InvalidOperationException("Lobby catalog is missing.");
                home.lobbyScene=Path.GetFileNameWithoutExtension(ProjectPath.Find("Lobby.unity"));
                home.gameScene=Path.GetFileNameWithoutExtension(ProjectPath.Find("Game.unity"));
                EditorSceneManager.SaveScene(scene,homePath);
                var entries=EditorBuildSettings.scenes.Where(s=>s.path!=homePath).ToList();
                entries.Insert(0,new EditorBuildSettingsScene(homePath,true));EditorBuildSettings.scenes=entries.ToArray();
                foreach(string name in new[]{"01-Tsunami.png","02-GiantHand.png","03-CaptureTheFlag.png","04-CloudCarnival.png"})
                {
                    var importer=AssetImporter.GetAtPath(ProjectPath.Find(name)) as TextureImporter;
                    if(!importer)throw new InvalidOperationException("Missing slide: "+name);
                    importer.textureType=TextureImporterType.Default;importer.maxTextureSize=2048;importer.mipmapEnabled=false;
                    importer.isReadable=false;importer.wrapMode=TextureWrapMode.Clamp;importer.npotScale=TextureImporterNPOTScale.None;
                    importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }
    }
}
