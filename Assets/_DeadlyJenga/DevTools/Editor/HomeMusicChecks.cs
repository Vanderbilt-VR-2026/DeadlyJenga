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
    [InitializeOnLoad] public static class HomeMusicChecks
    {
        const string Key = "HomeMusic.Checks";
        static IEnumerator run;
        static int errors;
        static string Report => ProjectPath.Report("HomeMusicChecks.txt");
        [Serializable] sealed class Saved { public SceneSetup[] scenes; public string map, mode; public int population; }
        static HomeMusicChecks() { EditorApplication.update += Tick; EditorApplication.playModeStateChanged += State; }
        static void Tick()
        {
            if (run == null)
            {
                string request = ProjectPath.Report("HomeMusicChecks.request");
                if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                if (File.ReadAllText(request).Trim() != System.Diagnostics.Process.GetCurrentProcess().Id.ToString()) return;
                File.Delete(request);
                var scenes = EditorSceneManager.GetSceneManagerSetup();
                if (scenes.Any(s => s.isLoaded && (string.IsNullOrEmpty(s.path) || SceneManager.GetSceneByPath(s.path).isDirty))) { File.WriteAllText(Report,"Deferred: unsaved scenes left untouched."); return; }
                if (!ValidationRunGate.TryAcquire(Key)) { File.WriteAllText(Report,"Deferred: another validation owns the editor."); return; }
                SessionState.SetString(Key+"Saved",JsonUtility.ToJson(new Saved { scenes=scenes, map=MatchSetup.MapId,mode=MatchSetup.ModeId,population=MatchSetup.BotTarget }));
                File.WriteAllText(Report,"Home music checks started " + DateTime.Now.ToString("O") + "\n");
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
        static IEnumerator Validate()
        {
            Application.runInBackground=true;
            float deadline=Time.realtimeSinceStartup+30;
            while(!Object.FindFirstObjectByType<CandyHomeMusic>()&&Time.realtimeSinceStartup<deadline)yield return null;
            var music=Object.FindFirstObjectByType<CandyHomeMusic>();Require(music,"Home event attaches music");
            var source=music.GetComponent<AudioSource>();
            while(!source.isPlaying&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(source.isPlaying&&source.loop&&source.spatialBlend==0,"Home automatically plays a looping stereo source");
            Require(Mathf.Abs(source.clip.length-54.181813f)<.03f,"First four seconds removed from Shorebreak");
            var importer=(AudioImporter)AssetImporter.GetAtPath(ProjectPath.Find("Shorebreak-Home.wav"));
            Require(importer.defaultSampleSettings.loadType==AudioClipLoadType.Streaming&&!importer.forceToMono,"Music streams and preserves stereo");
            Object.FindFirstObjectByType<HomeMenuView>().ShowJoin(true);yield return null;
            Require(source.isPlaying,"Join subpage keeps home music playing");
            source.time=source.clip.length-.12f;
            deadline=Time.realtimeSinceStartup+4;
            while(source.time>2&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(source.isPlaying&&source.time<2,"Playback wraps at the end of the trimmed clip");
            music.enabled=false;Require(!source.isPlaying&&!CandyHomeMusic.Active,"Disabling the home presentation stops music");
            music.enabled=true;deadline=Time.realtimeSinceStartup+5;
            while(!source.isPlaying&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(source.isPlaying,"Re-enabling starts one home track");
            Object.FindFirstObjectByType<HomeSession>().RandomLobby();deadline=Time.realtimeSinceStartup+45;
            while(!IslandLobby.Current&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(IslandLobby.Current&&!source&&!CandyHomeMusic.Active,"Entering actual lobby destroys home music and its source");
            var lobbyMusic=Object.FindFirstObjectByType<CandyLobbyMusic>();
            Require(lobbyMusic&&CandyLobbyMusic.Active,"Lobby event creates lobby music");
            var lobbySource=lobbyMusic.GetComponent<AudioSource>();deadline=Time.realtimeSinceStartup+10;
            while(!lobbySource.isPlaying&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(lobbySource.isPlaying&&lobbySource.loop&&lobbySource.spatialBlend==0&&lobbySource.clip.name=="ReadySetSplash-Lobby","Ready Set Splash autoplays and loops in stereo");
            Require(Mathf.Abs(lobbySource.clip.length-75)<.03f,"Lobby uses complete approved 75-second track");
            lobbySource.time=lobbySource.clip.length-.12f;deadline=Time.realtimeSinceStartup+4;
            while(lobbySource.time>2&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(lobbySource.isPlaying&&lobbySource.time<2,"Lobby track wraps at its loop boundary");
            var lobbyView=Object.FindFirstObjectByType<IslandLobbyView>();
            Require(lobbyView,"Lobby presentation attaches");
            foreach(string name in new[]{"Host controller computer","Mode controller computer"})
            {
                var root=lobbyView.transform.Find(name);
                var wall=root.Find("Flat grounded terminal wall");var face=root.Find("Vertical controller face");
                Require(wall&&face,"Flat wall and control face exist: "+name);
                Require(wall.localPosition.y-wall.localScale.y*.5f<0&&wall.localScale.z<=.181f,"Thin wall reaches ground: "+name);
                var screen=lobbyView.transform.Find(name.StartsWith("Host")?"HOST":"MAP + MODE");
                Require(Vector3.Distance(new Vector3(root.position.x,screen.position.y,root.position.z),screen.position)<.001f,"Wall is directly in line with screen: "+name);
                Require(Vector3.Dot(face.up,Vector3.up)>.999f&&face.GetComponentsInChildren<MeshRenderer>().All(r=>r.GetComponent<TextMesh>()||r.transform.localScale.z<=.02f),"Decorations lie flat on upright panel: "+name);
            }
            var player=Services.Session.Player;player.InputSuspended=true;
            player.Place(new Vector3(-5,0,-6),Quaternion.identity);yield return null;
            Capture(new Vector3(1.0f,1.55f,-4.7f),new Vector3(1.3f,1.3f,1),"Lobby-Flat-Terminals.png");
            SceneManager.LoadScene("Home");deadline=Time.realtimeSinceStartup+20;
            while(!Object.FindFirstObjectByType<HomeSession>()&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;
            Require(Object.FindObjectsByType<CandyHomeMusic>(FindObjectsSortMode.None).Length==1,"Returning home creates exactly one music owner");
            Require(!lobbySource&&!CandyLobbyMusic.Active,"Leaving lobby closes its music source");
            Object.FindFirstObjectByType<HomeSession>().Demo();deadline=Time.realtimeSinceStartup+45;
            while(Services.Session?.Demo==null&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(Services.Session?.Demo!=null&&Services.World!=null,"Actual Game scene enters Play Mode");
            Require(!Object.FindFirstObjectByType<CandyHomeMusic>()&&!CandyHomeMusic.Active,"Demo transition also removes home music");
            Require(!Object.FindFirstObjectByType<CandyLobbyMusic>()&&!CandyLobbyMusic.Active,"Lobby music is absent during gameplay");
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
