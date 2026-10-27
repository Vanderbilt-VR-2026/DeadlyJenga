using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
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
    [InitializeOnLoad] public static class LobbyBrowserBeaconChecks
    {
        const string Key = "LobbyBrowserBeacon.Checks";
        static IEnumerator run;
        static int errors;
        static string Report => ProjectPath.Report("LobbyBrowserBeaconChecks.txt");
        [Serializable] sealed class Saved { public SceneSetup[] scenes; public string map, mode; public int population,hands,gameSize; public bool fromLobby,local,background,gameMaximized; public float volume; }
        static LobbyBrowserBeaconChecks() { EditorApplication.update += Tick; EditorApplication.playModeStateChanged += State; }
        static void Tick()
        {
            if (run == null)
            {
                string close = ProjectPath.Report("LobbyBrowserBeaconChecks.close");
                if(File.Exists(close)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&File.ReadAllText(close).Trim()==System.Diagnostics.Process.GetCurrentProcess().Id.ToString())
                {File.Delete(close);EditorApplication.Exit(0);return;}
                string request = ProjectPath.Report("LobbyBrowserBeaconChecks.request");
                if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                if (File.ReadAllText(request).Trim() != System.Diagnostics.Process.GetCurrentProcess().Id.ToString()) return;
                
                var scenes = EditorSceneManager.GetSceneManagerSetup();
                if (scenes.Any(s => s.isLoaded && (string.IsNullOrEmpty(s.path) || SceneManager.GetSceneByPath(s.path).isDirty))) { File.WriteAllText(Report,"Deferred: unsaved scenes left untouched."); return; }
                if (!ValidationRunGate.TryAcquire(Key)) return;
                File.Delete(request);
                var game=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"),false,"Game",false);
                SessionState.SetString(Key+"Saved",JsonUtility.ToJson(new Saved { scenes=scenes, map=MatchSetup.MapId,mode=MatchSetup.ModeId,population=MatchSetup.BotTarget,hands=MatchSetup.HandCount,fromLobby=MatchSetup.FromLobby,local=MatchSetup.StartLocalSession,background=Application.runInBackground,volume=AudioListener.volume,gameMaximized=game.maximized,gameSize=(int)game.GetType().GetProperty("selectedSizeIndex",Flags).GetValue(game) }));
                game.maximized=true;game.Focus();
                File.WriteAllText(Report,"Lobby browser / beacon checks started " + DateTime.Now.ToString("O") + "\n");
                SessionState.SetBool(Key,true); MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=1;MatchSetup.HandCount=0;MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;
                EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity")); EditorApplication.EnterPlaymode();
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
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)&&ValidationRunGate.Owns(Key))
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
                MatchSetup.MapId=saved.map;MatchSetup.ModeId=saved.mode;MatchSetup.BotTarget=saved.population;MatchSetup.HandCount=saved.hands;MatchSetup.FromLobby=saved.fromLobby;MatchSetup.StartLocalSession=saved.local;Application.runInBackground=saved.background;AudioListener.volume=saved.volume;
                try
                {
                    if(saved.scenes!=null&&saved.scenes.Any(s=>s.isLoaded))EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
                    else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                }
                finally
                {
                    var game=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"),false,"Game",false);
                    Call(game,"SizeSelectionCallback",saved.gameSize,null);game.maximized=saved.gameMaximized;
                    ValidationRunGate.Release(Key);
                }
            }
        }
        static void Log(string message,string trace,LogType type)
        { if(type==LogType.Error||type==LogType.Assert||type==LogType.Exception){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");} }
        static void Require(bool value,string message)
        { if(!value)throw new InvalidOperationException(message);File.AppendAllText(Report,"PASS "+message+"\n"); }
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        static object Field(object value,string name)=>value.GetType().GetField(name,Flags).GetValue(value);
        static void Set(object value,string name,object next)=>value.GetType().GetField(name,Flags).SetValue(value,next);
        static object Call(object value,string method,params object[] args)=>value.GetType().GetMethod(method,Flags).Invoke(value,args);
        static object[] Rows(object picker)=>((IEnumerable)picker.GetType().GetProperty("Rows",Flags).GetValue(picker)).Cast<object>().ToArray();
        static bool Bit(object row,string name)=>(bool)Field(row,name);
        static object Header(object picker,bool experimental)=>Rows(picker).First(r=>Bit(r,"collection")&&Bit(r,"experimental")==experimental);
        static void Expand(object picker,object row)
        {if(((string)Call(picker,"Title",row)).TrimStart().StartsWith(">"))Call(picker,"Toggle",row);}
        static void Collapse(object picker,object row)
        {if(((string)Call(picker,"Title",row)).TrimStart().StartsWith("v"))Call(picker,"Toggle",row);}
        static void SetCaptureSize()
        {
            var assembly=typeof(UnityEditor.Editor).Assembly;
            var type=assembly.GetType("UnityEditor.GameViewSizes");
            var sizes=type.BaseType.GetProperty("instance",BindingFlags.Static|BindingFlags.Public).GetValue(null);
            var group=type.GetProperty("currentGroup",Flags).GetValue(sizes);
            var game=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"),false,"Game",false);
            for(int i=0;i<(int)Call(group,"GetTotalCount");i++)
            {
                var size=Call(group,"GetGameViewSize",i);
                if((int)size.GetType().GetProperty("width",Flags).GetValue(size)!=1920||(int)size.GetType().GetProperty("height",Flags).GetValue(size)!=1080)continue;
                Call(game,"SizeSelectionCallback",i,null);game.Focus();game.Repaint();return;
            }
            throw new InvalidOperationException("Built-in Full HD Game view preset is missing.");
        }
        static void CheckPicker(IReadOnlyList<LobbyOption> maps)
        {
            var type=typeof(IslandLobbyView).Assembly.GetType("DeadlyJenga.CastlePrototype.MapCategoryPicker");
            var picker=Activator.CreateInstance(type,true);Call(picker,"Update",maps,"castle");
            Require(Rows(picker).Count(r=>Bit(r,"collection"))==2,"Main and experimental dropdowns both exist");
            var seen=new HashSet<string>();
            for(int group=0;group<2;group++)
            {
                bool experimental=group==1;Expand(picker,Header(picker,experimental));
                var categories=Rows(picker).Where(r=>!Bit(r,"collection")&&Field(r,"map")==null&&Bit(r,"experimental")==experimental).ToArray();
                Require(categories.Length>0,"Nested categories preserved for "+(experimental?"experiments":"main maps"));
                foreach(var category in categories)
                {
                    Expand(picker,category);
                    foreach(var row in Rows(picker).Where(r=>Field(r,"map")!=null&&Bit(r,"experimental")==experimental))
                        Require(seen.Add(((LobbyOption)Field(row,"map")).id),"Unique categorized map: "+((LobbyOption)Field(row,"map")).id);
                }
            }
            Require(seen.SetEquals(maps.Select(m=>m.id)),"Every catalog map remains reachable exactly once");
            var selected=maps.First(m=>m.id.Contains("sparse-"));
            var active=Activator.CreateInstance(type,true);Call(active,"Update",maps,selected.id);
            Require(Rows(active).Any(r=>((LobbyOption)Field(r,"map"))?.id==selected.id),"Current experimental map opens its collection and category");
            var status=type.Assembly.GetType("DeadlyJenga.CastlePrototype.MapBrowseStatus");
            var classify=status.GetMethod("Experimental",Flags);
            Require((bool)classify.Invoke(null,new object[]{new LobbyOption{id="mode-powerup-test"}}),"Runtime mode-prefixed test maps are experimental");
            foreach(string id in new[]{"epic-honeycomb-hive","epic-cloud-carnival","epic-candy-glass-cloud"})
                Require(!(bool)classify.Invoke(null,new object[]{new LobbyOption{id=id}}),"Approved art map remains in main library: "+id);
        }
        static IEnumerator Validate()
        {
            Application.runInBackground=true;AudioListener.volume=0;
            SetCaptureSize();for(int i=0;i<5;i++)yield return null;
            Require(Screen.width>=1280&&Screen.height>=720,"Game view captures use a readable fixed resolution");
            float deadline=Time.realtimeSinceStartup+90;
            while(!IslandLobby.Current&&Time.realtimeSinceStartup<deadline)yield return null;
            var lobby=IslandLobby.Current;Require(lobby,"Actual lobby starts");
            var details=(ILobbyDetails)lobby;var view=Object.FindFirstObjectByType<IslandLobbyView>();
            while(!view&&Time.realtimeSinceStartup<deadline){yield return null;view=Object.FindFirstObjectByType<IslandLobbyView>();}
            Require(view,"Actual lobby presentation builds");CheckPicker(details.Maps);
            lobby.Player.InputSuspended=true;lobby.Player.Place(new Vector3(-5,0,-2),Quaternion.identity);yield return null;
            Capture(new Vector3(0,1.55f,-3),new Vector3(0,1.5f,1),68,"Lobby-Terminals-Updated.png");
            Capture(new Vector3(0,1.55f,-2.3f),new Vector3(0,1.65f,1),50,"Lobby-Host-Flat-Panel.png");
            Capture(new Vector3(3.05f,1.55f,-2.3f),new Vector3(3.05f,1.65f,1),50,"Lobby-Mode-Flat-Panel.png");
            Require(view.InvokeChoice("map-current"),"Lobby opens the categorized map picker");yield return null;
            var browser=Field(view,"lobbyMapPicker");Collapse(browser,Header(browser,false));var experimental=Header(browser,true);Expand(browser,experimental);
            int anchor=(int)browser.GetType().GetProperty("HeaderIndex",Flags).GetValue(browser);Set(view,"pickerOffset",anchor/7*7);
            yield return null;
            Capture(new Vector3(3.05f,1.95f,-1.5f),new Vector3(3.05f,1.95f,1),46,"Lobby-Experimental-Maps.png");
            Require(Rows(browser).Any(r=>Bit(r,"experimental")&&!Bit(r,"collection")),"Lobby experimental dropdown expands into categories");
            Set(view,"setupPage","main");
            var shader=Resources.Load<Shader>("CandyScenery/CandyAuroraTransit");Require(shader&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),"Aurora transit shader imports and compiles");
            lobby.Player.Place(new Vector3(1.5f,0,-2.5f),Quaternion.identity);yield return null;
            GameEvents.RaiseDepartureStarted(10000);yield return null;
            var travel=Object.FindFirstObjectByType<CandySessionTravel>();Require(travel,"Departure event attaches the new beacon");
            var material=(Material)Field(travel,"liquid");Require(material&&material.shader==shader,"Transition uses aurora material rather than liquid curtain");
            Require(!((UnityEngine.UI.Image)Field(travel,"curtain")).raycastTarget,"Transition does not intercept UI input");
            foreach(var phase in new[]{.40f,.65f,.92f})
            {
                Set(travel,"started",Time.unscaledTime-phase*7000);Call(travel,"Render");yield return null;
                ScreenCapture.CaptureScreenshot(ProjectPath.Report("Aurora-Beacon-"+Mathf.RoundToInt(phase*100)+".png"));
                float wait=Time.realtimeSinceStartup+.4f;while(Time.realtimeSinceStartup<wait)yield return null;
            }
            GameEvents.RaiseEmoted(lobby.gameObject,"DepartureCancelled");deadline=Time.realtimeSinceStartup+5;
            while(Object.FindFirstObjectByType<CandySessionTravel>()&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(!Object.FindFirstObjectByType<CandySessionTravel>(),"Cancellation clears the beacon and its material");
            Require(lobby.CanStart,"Local lobby can start the selected game");lobby.StartOrCancel();
            deadline=Time.realtimeSinceStartup+90;
            while(Services.Session?.Demo==null&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(Services.Session?.Demo!=null&&Services.World!=null,"Real lobby departure loads playable Game scene");
            deadline=Time.realtimeSinceStartup+5;
            while(Object.FindFirstObjectByType<CandySessionTravel>()&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(!Object.FindFirstObjectByType<CandySessionTravel>(),"Arrival removes beacon after revealing the game");
            var hud=Object.FindFirstObjectByType<CandyDemoHud>();Require(hud,"Desktop demo menu exists");
            Call(hud,"MapContentHeight");var demo=Field(hud,"demoMapPicker");
            Require(Rows(demo).Count(r=>Bit(r,"collection"))==2,"Desktop demo uses both map collections");
            var vr=Object.FindObjectsByType<CastleWorldMenu>(FindObjectsInactive.Include,FindObjectsSortMode.None).First();
            var vrPicker=Field(vr,"vrMapPicker");var maps=(IDemoMapControls)Services.Session.Demo;Call(vrPicker,"Update",maps.Maps,maps.CurrentMapId);
            Require(Rows(vrPicker).Count(r=>Bit(r,"collection"))==2,"VR demo uses the same categorized collections");
            Services.Session.SetMenu(true);Set(hud,"page",2);Set(hud,"subsection",2);Set(hud,"scroll",Vector2.zero);
            Collapse(demo,Header(demo,false));Expand(demo,Header(demo,true));yield return null;
            ScreenCapture.CaptureScreenshot(ProjectPath.Report("Demo-Experimental-Maps.png"));
            float end=Time.realtimeSinceStartup+.5f;while(Time.realtimeSinceStartup<end)yield return null;
        }
        static void Capture(Vector3 position,Vector3 target,float fov,string name)
        {
            var go=new GameObject("Terminal presentation capture");var camera=go.AddComponent<Camera>();
            camera.enabled=false;camera.fieldOfView=fov;camera.transform.position=position;camera.transform.LookAt(target);camera.stereoTargetEye=StereoTargetEyeMask.None;
            var rt=new RenderTexture(2000,1250,24);camera.targetTexture=rt;var previous=RenderTexture.active;
            try
            {
                camera.Render();RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report(name),image.EncodeToPNG());Object.Destroy(image);
            }
            finally{RenderTexture.active=previous;rt.Release();Object.Destroy(rt);Object.Destroy(go);}
        }
    }
}
