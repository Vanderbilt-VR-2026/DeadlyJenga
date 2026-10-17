using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.CandyArt;
using DeadlyJenga.World.Maps;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class LobbyResortSetup
    {
        static LobbyResortSetup()
        {
            EditorApplication.update+=()=>
            {
                const string request="Logs/Validation/LobbyResortSetup.request";
                if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                File.Delete(request);Install();
            };
        }
        [MenuItem("Tools/Deadly Jenga/Apply Lobby Resort Dressing")]
        public static void Install()
        {
            var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(current.isDirty){File.WriteAllText(ProjectPath.Report("LobbyResortSetup.txt"),"Not applied: save intended scene edits first.");return;}
            string original=current.path;var scene=current.path==ProjectPath.Find("Lobby.unity")?current:EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));
            var lobby=Object.FindFirstObjectByType<IslandLobby>();
            if(!lobby||!lobby.catalog||!lobby.catalog.art){File.WriteAllText(ProjectPath.Report("LobbyResortSetup.txt"),"FAIL: missing lobby/catalog.");return;}
            var art=lobby.GetComponent<CandyLobbyResort>();if(!art)art=Undo.AddComponent<CandyLobbyResort>(lobby.gameObject);
            var shake=lobby.GetComponentInChildren<LobbyMilkshake>();
            if(!shake){var go=new GameObject("Milkshake · beach delivery clutter");Undo.RegisterCreatedObjectUndo(go,"Add milkshake challenge");go.transform.SetParent(lobby.transform,false);go.transform.localPosition=new Vector3(2.6f,.45f,-3.8f);shake=Undo.AddComponent<LobbyMilkshake>(go);}
            art.milkshakeGeometry=shake.transform;
            art.candyMaterial=((DeadlyJenga.CandyArt.CandyArtProfile)lobby.catalog.art).purple;EditorUtility.SetDirty(art);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText(ProjectPath.Report("LobbyResortSetup.txt"),"Applied optional Presentation/Visuals resort dressing and static World/Maps milkshake challenge to existing Lobby scene. No scene regeneration or network changes.\n");
            if(original!=scene.path&&File.Exists(original))EditorSceneManager.OpenScene(original);
        }
    }
}
