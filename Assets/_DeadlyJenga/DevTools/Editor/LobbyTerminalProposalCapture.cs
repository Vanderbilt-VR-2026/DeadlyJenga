using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.CandyAudio;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class LobbyTerminalProposalCapture
    {
        const string Key = "LobbyTerminalProposal.Capture";
        static IEnumerator run;
        static int errors;
        static string Report => ProjectPath.Report("LobbyTerminalProposalCapture.txt");
        [Serializable] sealed class Saved { public SceneSetup[] scenes; public string map, mode; public int population; }
        static LobbyTerminalProposalCapture() { EditorApplication.update += Tick; EditorApplication.update += CaptureExistingLobby; EditorApplication.playModeStateChanged += State; }
        static void Tick()
        {
            if (run == null)
            {
                string request = ProjectPath.Report("LobbyTerminalProposalCapture.request");
                if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                if (File.ReadAllText(request).Trim() != System.Diagnostics.Process.GetCurrentProcess().Id.ToString()) return;
                var scenes = EditorSceneManager.GetSceneManagerSetup();
                if (scenes.Any(s => s.isLoaded && (string.IsNullOrEmpty(s.path) || SceneManager.GetSceneByPath(s.path).isDirty))) { File.WriteAllText(Report,"Deferred: unsaved scenes left untouched."); return; }
                if (!ValidationRunGate.TryAcquire(Key)) { File.WriteAllText(Report,"Deferred: another validation owns the editor."); return; }
                File.Delete(request);
                SessionState.SetString(Key+"Saved",JsonUtility.ToJson(new Saved { scenes=scenes, map=MatchSetup.MapId,mode=MatchSetup.ModeId,population=MatchSetup.BotTarget }));
                File.WriteAllText(Report,"Lobby terminal capture started " + DateTime.Now.ToString("O") + "\n");
                SessionState.SetBool(Key,true); EditorSceneManager.OpenScene(ProjectPath.Find("Home.unity")); EditorApplication.EnterPlaymode();
                return;
            }
            try { if (run.MoveNext()) return; }
            catch(Exception error) { errors++; File.AppendAllText(Report,"FAIL " + error + "\n"); }
            run=null; Application.logMessageReceived-=Log;
            File.AppendAllText(Report,"COMPLETE; errors="+errors+". Desktop Editor only; headset audio not verified.\n");
            EditorApplication.ExitPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
            {
                SessionState.SetBool(Key,false);SessionState.SetBool(Key+"Running",true);errors=0;
                EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();
                Application.logMessageReceived+=Log;run=Validate();
            }
            if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"Running",false))
            {
                SessionState.SetBool(Key+"Running",false);Application.logMessageReceived-=Log;run=null;
                AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
                var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Saved",""));
                MatchSetup.MapId=saved.map;MatchSetup.ModeId=saved.mode;MatchSetup.BotTarget=saved.population;
                EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);ValidationRunGate.Release(Key);
            }
        }
        static void Log(string message,string trace,LogType type)
        { if(type==LogType.Error||type==LogType.Assert||type==LogType.Exception){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");} }
        static void Require(bool value,string message)
        { if(!value)throw new InvalidOperationException(message);File.AppendAllText(Report,"PASS "+message+"\n"); }
        static void CaptureExistingLobby()
        {
            string request=ProjectPath.Report("LobbyTerminalProposalCapture.request");
            if(!File.Exists(request)||!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(File.ReadAllText(request).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString())return;
            var view=Object.FindFirstObjectByType<IslandLobbyView>();
            if(!view||!view.transform.Find("MAP + MODE")||!view.transform.Find("JOIN"))return;
            // Synchronous offscreen views can share an already-running lobby without moving its player.
            try{CaptureSet(view);File.Delete(request);File.WriteAllText(Report,"COMPLETE actual Unity lobby screen captures. Isolated front views; player, scene and gameplay state unchanged.\n");}
            catch(Exception e){File.Delete(request);File.WriteAllText(Report,"FAIL "+e);}
        }
        static IEnumerator Validate()
        {
            Application.runInBackground=true;
            Object.FindFirstObjectByType<HomeSession>().RandomLobby();
            float deadline=Time.realtimeSinceStartup+45;
            while(!Object.FindFirstObjectByType<IslandLobbyView>()&&Time.realtimeSinceStartup<deadline)yield return null;
            var view=Object.FindFirstObjectByType<IslandLobbyView>();Require(view,"Actual lobby presentation ready");
            yield return null;yield return null;CaptureSet(view);
        }
        static void CaptureSet(IslandLobbyView view)
        {
            var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var enabled=canvases.Select(c=>c.enabled).ToArray();
            var groups=view.GetComponentsInChildren<CanvasGroup>();var alphas=groups.Select(g=>g.alpha).ToArray();
            try
            {
                foreach(string name in new[]{"JOIN","HOST","MAP + MODE"})
                {
                    var screen=view.transform.Find(name);if(!screen)throw new InvalidOperationException("Missing terminal: "+name);
                    foreach(var c in canvases)c.enabled=c.transform==screen||c.transform.IsChildOf(screen)||name=="JOIN"&&c.name=="Giant piano keyboard";
                    foreach(var group in screen.GetComponentsInChildren<CanvasGroup>())group.alpha=1;
                    Capture(screen.position-screen.forward*3.6f+Vector3.up*.05f,screen.position-Vector3.up*.15f,"Terminal-"+name.Replace(" + ","-")+"-Current.png");
                }
                for(int i=0;i<canvases.Length;i++)canvases[i].enabled=enabled[i]&&canvases[i].renderMode==RenderMode.WorldSpace&&!canvases[i].name.StartsWith("WARDROBE",StringComparison.Ordinal);
                Capture(new Vector3(0,2.5f,-8),new Vector3(0,1.65f,1),"Terminals-Overview-Current.png");
            }
            finally
            {
                for(int i=0;i<canvases.Length;i++)if(canvases[i])canvases[i].enabled=enabled[i];
                for(int i=0;i<groups.Length;i++)if(groups[i])groups[i].alpha=alphas[i];
            }
        }
        static void Capture(Vector3 position,Vector3 target,string name)
        {
            var go=new GameObject("Flat terminal validation camera");var camera=go.AddComponent<Camera>();
            camera.enabled=false;camera.fieldOfView=60;camera.transform.position=position;camera.transform.LookAt(target);camera.stereoTargetEye=StereoTargetEyeMask.None;
            var rt=new RenderTexture(1600,1000,24);camera.targetTexture=rt;var previous=RenderTexture.active;
            try
            {
                camera.Render();RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report(name),image.EncodeToPNG());Object.Destroy(image);
            }
            finally{RenderTexture.active=previous;rt.Release();Object.Destroy(rt);Object.Destroy(go);}
        }
    }
}
