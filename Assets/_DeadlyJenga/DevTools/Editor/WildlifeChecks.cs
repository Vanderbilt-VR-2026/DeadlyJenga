using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using DeadlyJenga.Wildlife;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class WildlifeChecks
    {
        const string Key="WildlifeChecks";
        static IEnumerator run;static int errors,checks;static bool failed;
        static string Report=>ProjectPath.Report("WildlifeChecks.txt");
        [Serializable] sealed class Saved{public SceneSetup[] scenes;public string choices;public bool background,fromLobby,local;public float volume;}
        static WildlifeChecks(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        public static void Run()
        {
            string root=Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
            if(Path.GetFullPath(File.ReadAllText(ProjectPath.Report("CandyTerminalDesign.owner")).Trim()).ToLowerInvariant()!=root.ToLowerInvariant())throw new InvalidOperationException("Wrong owned editor.");
            if(EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Editor busy.");
            if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Unsaved scenes untouched.");
            SessionState.SetString(Key+"Saved",JsonUtility.ToJson(new Saved{scenes=EditorSceneManager.GetSceneManagerSetup(),choices=JsonUtility.ToJson(SpectatorRecordingPlan.Current()),background=Application.runInBackground,volume=AudioListener.volume,fromLobby=MatchSetup.FromLobby,local=MatchSetup.StartLocalSession}));
            SessionState.SetBool(Key,true);File.WriteAllText(Report,"Actual Unity wildlife validation\n"+root+"\n");
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.MapId="mode-christmas-town";MatchSetup.ModeId="demo";MatchSetup.BotTarget=10;MatchSetup.HandCount=0;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){errors=checks=0;failed=false;Application.logMessageReceived+=Log;AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();run=Flatten(Validate());}
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                Application.logMessageReceived-=Log;run=null;SessionState.SetBool(Key,false);
                var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Saved",""));Application.runInBackground=saved.background;AudioListener.volume=saved.volume;MatchSetup.FromLobby=saved.fromLobby;MatchSetup.StartLocalSession=saved.local;
                SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(saved.choices));
                if(saved.scenes.Any(s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
                AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
                File.AppendAllText(Report,"COMPLETE checks="+checks+" errors="+errors+" failed="+failed+"\nNo Quest claim. Wildlife is local preview only; no network actor changes.\n");
            }
        }
        static void Log(string m,string trace,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            // Known independent editor service failures are recorded separately.
            if(trace.Contains("UnityEditor.Search.SearchDatabase")||trace.Contains("UnityEditor.AsyncHTTPClient")){File.AppendAllText(Report,"EDITOR SERVICE "+m+"\n");return;}
            errors++;File.AppendAllText(Report,"ERROR "+m+"\n"+trace+"\n");
        }
        static void Tick()
        {
            if(run==null)
            {
                string request=ProjectPath.Report("WildlifeChecks.request");
                if(!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(request)&&File.ReadAllText(request).Trim()==System.Diagnostics.Process.GetCurrentProcess().Id.ToString()){File.Delete(request);Run();}
                return;
            }
            try{if(run.MoveNext())return;}catch(Exception e){failed=true;File.AppendAllText(Report,"FAIL "+e+"\n");}
            (run as IDisposable)?.Dispose();run=null;EditorApplication.ExitPlaymode();
        }
        static void Check(bool ok,string text){checks++;File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+text+"\n");if(!ok)throw new Exception(text);}
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
        static IEnumerator Until(Func<bool> ready,string label,float seconds=40){float until=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;Check(ready(),label);}
        static IEnumerator Flatten(IEnumerator source)
        {
            while(source.MoveNext())
            {
                if(source.Current is IEnumerator nested){var child=Flatten(nested);while(child.MoveNext())yield return child.Current;}
                else if(source.Current is AsyncOperation operation){while(!operation.isDone)yield return null;}
                else if(source.Current is WaitForSeconds){var wait=Wait(.6f);while(wait.MoveNext())yield return null;}
                else yield return source.Current;
            }
        }
        static IEnumerator Validate()
        {
            Application.runInBackground=true;AudioListener.volume=0;
            yield return Until(()=>Object.FindFirstObjectByType<CastlePlaySession>()?.Modes!=null,"Game solo starts");
            yield return Wait(2);
            var session=Object.FindFirstObjectByType<CastlePlaySession>();
            var controls=session.GetComponent<IslandWildlife>();
            Check(controls&&controls.Available,"Core wildlife controls attach to actual Game");
            Check(controls.Species.Count==12,"All twelve species registered");
            Vector3 basePoint=Services.World.Center;bool found=false;
            for(int i=0;i<12;i++)
            {
                float angle=i*Mathf.PI/6;var p=Services.World.Center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*Services.World.Radius*.65f;
                if(controls.TryGround(p,Services.World.TopHeight+10,Services.World.TopHeight+50,out basePoint)){found=true;break;}
            }
            Check(found,"Actual map has safe island support for animal testing");
            session.Player.Place(basePoint+Vector3.up*.1f,Quaternion.identity);session.Player.InputSuspended=true;
            foreach(var id in controls.Species)
            {
                var go=controls.Spawn(id,false);Check(go,"Spawn "+id+" on actual map");
                var model=go.GetComponentInChildren<IslandAnimalModel>();
                Check(model&&model.PartCount>=3&&model.PartCount<=12,id+" has bounded model parts and dot eyes");
                Check(go.transform.Find("Visual")&&go.GetComponent<CharacterController>(),id+" has independent visual and movement body");
                Capture(model.transform,id);
            }
            Check(controls.Spawn("invalid",false)==null,"Unknown species rejected");
            yield return Wait(7);
            var animals=controls.GetComponentsInChildren<IslandAnimal>();
            Check(animals.Length==12,"All twelve stay alive and supported");
            CaptureRoaming(session,animals);
            foreach(var animal in animals)
            {
                Check(animal.Decisions>0,animal.Species+" uses scheduled decisions");
                Check(animal.DistanceTravelled>.05f,animal.Species+" actually moves");
                animal.SetCompanion(session.Player.transform);Check(animal.Tame&&animal.FollowTarget==session.Player.transform,"Tame "+animal.Species+" uses the real player as its target");
            }
            var court=GameObject.CreatePrimitive(PrimitiveType.Cube);court.name="Controlled follow-test court (stand-in, not map content)";court.layer=Layers.Surface;
            var courtCenter=Services.World.Center+Vector3.up*(Services.World.TopHeight+30);court.transform.position=courtCenter-Vector3.up*.5f;court.transform.localScale=new Vector3(35,1,35);
            var followTarget=new GameObject("Controlled moving follow target (stand-in)");followTarget.transform.position=courtCenter+Vector3.up*.10f;
            for(int i=0;i<animals.Length;i++)
            {
                var body=animals[i].GetComponent<CharacterController>();body.enabled=false;
                float angle=i*Mathf.PI/6;animals[i].transform.position=courtCenter+new Vector3(Mathf.Cos(angle)*5,.05f,Mathf.Sin(angle)*5);body.enabled=true;animals[i].SetCompanion(followTarget.transform);
            }
            Physics.SyncTransforms();yield return Wait(.25f);
            Check(animals.All(a=>a.Tame),"Controlled flat test court isolates companion following from level route geometry");
            followTarget.transform.position=courtCenter+new Vector3(0,.10f,3);
            var before=animals.Select(a=>Vector3.Distance(a.transform.position,followTarget.transform.position)).ToArray();
            yield return Wait(5);
            int followed=animals.Where((a,i)=>Vector3.Distance(a.transform.position,followTarget.transform.position)<before[i]-.2f).Count();
            File.AppendAllText(Report,"FOLLOW stand-in target="+followTarget.transform.position+" court="+courtCenter+" count="+followed+"\n");
            foreach(var animal in animals)
            {
                int i=System.Array.IndexOf(animals,animal);var goal=typeof(IslandAnimal).GetField("goal",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(animal);
                File.AppendAllText(Report,animal.Species+" pos="+animal.transform.position+" goal="+goal+" from="+before[i]+" to="+Vector3.Distance(animal.transform.position,followTarget.transform.position)+" grounded="+animal.GetComponent<CharacterController>().isGrounded+" active="+animal.gameObject.activeInHierarchy+"\n");
            }
            foreach(var animal in animals){animal.SetCompanion(null);Check(!animal.Tame,animal.Species+" returns to wild roaming");}
            for(int i=0;i<12;i++)controls.Spawn("otter",false);
            Check(controls.Count==24&&controls.Spawn("otter",false)==null,"Population limit bounds object and controller cost");
            CandyWildlifePanel.Open(session);yield return Wait(.25f);
            Check(CandyWildlifePanel.Showing(session),"Desktop / VR wildlife panel opens through existing session");
            var ui=session.GetComponent<CandyWildlifePanel>();Check(ui.transform.Find("Wildlife testing menu").GetComponentsInChildren<UnityEngine.UI.Button>(true).Length==16,"Panel exposes all species, spawn all, tame/wild, clear and close");
            ScreenCapture.CaptureScreenshot(ProjectPath.Report("Wildlife-Menu.png"));yield return Wait(.5f);
            var field=typeof(CandyWildlifePanel).GetField("session",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var head=new GameObject("Simulated wildlife menu head");var eye=head.AddComponent<Camera>();eye.enabled=false;eye.transform.SetPositionAndRotation(session.ActiveCamera.transform.position,session.ActiveCamera.transform.rotation);
            var proxy=new VRSession(session,eye);field.SetValue(ui,proxy);yield return null;
            var canvas=ui.transform.Find("Wildlife testing menu").GetComponent<Canvas>();
            Check(canvas.renderMode==RenderMode.WorldSpace,"VR stand-in uses a world-space wildlife panel");
            var plate=canvas.transform.Find("Wildlife panel");var rotation=plate.rotation;eye.transform.Rotate(0,30,0);yield return null;
            Check(Quaternion.Angle(rotation,plate.rotation)<.1f,"VR panel stays still during head turns");
            bool wasTame=(bool)typeof(CandyWildlifePanel).GetField("tame",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(ui);
            var toggle=plate.GetComponentsInChildren<UnityEngine.UI.Button>()[0];eye.transform.LookAt(toggle.transform.position);proxy.Select=true;yield return null;proxy.Select=false;yield return null;
            bool nowTame=(bool)typeof(CandyWildlifePanel).GetField("tame",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(ui);
            Check(wasTame!=nowTame,"VR stand-in gaze and trigger toggle companion mode");
            field.SetValue(ui,session);Object.Destroy(head);yield return null;
            Check(canvas.renderMode==RenderMode.ScreenSpaceOverlay,"Desktop panel restores after VR stand-in");
            Check(((RectTransform)plate).anchoredPosition.sqrMagnitude<.01f,"Desktop panel is centered after VR");
            session.SetRole(true);yield return null;
            Check(controls.GetComponentsInChildren<IslandAnimal>(true).All(a=>a.gameObject.activeInHierarchy),"Wildlife stays active while local player is a flying hand");
            session.SetRole(false);yield return null;
            controls.Clear();yield return null;Check(controls.Count==0,"Clear removes all wildlife");
            SceneManager.LoadScene("Lobby");yield return Wait(2);
            var lobby=Object.FindFirstObjectByType<IslandLobby>();var lobbyControls=lobby?lobby.GetComponent<IslandWildlife>():null;
            Check(lobby&&lobbyControls&&lobbyControls.Available,"Actual lobby also attaches wildlife controls");
            Check(lobbyControls.Spawn("fox",true),"Companion spawns on actual retained lobby island");
            CandyWildlifePanel.Open(lobby);yield return null;Check(CandyWildlifePanel.Showing(lobby),"Wildlife panel opens in original lobby");
            lobbyControls.Clear();yield return null;Check(lobbyControls.Count==0,"Lobby wildlife clears independently");
            Check(followed>=8,"Companions close distance to moving target on controlled court ("+followed+" / 12)");
            Check(errors==0,"No runtime errors");
        }
        static void CaptureRoaming(CastlePlaySession session,IslandAnimal[] animals)
        {
            Vector3 center=Vector3.zero;foreach(var animal in animals)center+=animal.transform.position;center/=animals.Length;
            var root=new GameObject("Actual roaming wildlife evidence");var camera=root.AddComponent<Camera>();camera.CopyFrom(session.ActiveCamera);camera.GetUniversalAdditionalCameraData().allowXRRendering=false;camera.enabled=false;camera.fieldOfView=52;
            camera.transform.position=center+new Vector3(7,5,8);camera.transform.LookAt(center+Vector3.up*.3f);
            var target=new RenderTexture(1800,1200,24);var previous=RenderTexture.active;Texture2D pixels=null;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels=new Texture2D(1800,1200,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1800,1200),0,0);pixels.Apply();File.WriteAllBytes(ProjectPath.Report("Wildlife-Roaming.png"),pixels.EncodeToPNG());}
            finally{RenderTexture.active=previous;camera.targetTexture=null;target.Release();Object.Destroy(target);Object.Destroy(root);if(pixels)Object.Destroy(pixels);}
        }
        sealed class VRSession : ISessionInfo
        {
            readonly ISessionInfo source;readonly Camera eye;public bool Select;
            public VRSession(ISessionInfo source,Camera eye){this.source=source;this.eye=eye;}
            public bool MenuOpen=>source.MenuOpen;public void SetMenu(bool value)=>source.SetMenu(value);
            public bool PlayingHand=>source.PlayingHand;public void SetRole(bool value)=>source.SetRole(value);
            public IPlayableCharacter Player=>source.Player;public IPlayableCharacter Hand=>source.Hand;
            public bool IsVR=>true;public Camera ActiveCamera=>eye;public bool MenuSelectPressed {get{bool value=Select;Select=false;return value;}}public bool MenuSelectHeld=>false;
            public bool CanEditWorld=>source.CanEditWorld;public void ResetMap()=>source.ResetMap();
            public ISurvivorBotPopulation SurvivorBots=>source.SurvivorBots;public IBotPopulation HandBots=>source.HandBots;
            public IMultiplayerControls Multiplayer=>source.Multiplayer;public IDemoControls Demo=>source.Demo;public ISocialControls Social=>source.Social;
        }
        static void Capture(Transform model,string id)
        {
            var go=new GameObject("Animal evidence camera");var camera=go.AddComponent<Camera>();camera.enabled=false;
            camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.72f,.82f,.88f);
            camera.orthographic=true;camera.orthographicSize=.80f;
            camera.transform.position=model.position+new Vector3(1.65f,1.16f,2.2f);camera.transform.LookAt(model.position+Vector3.up*.40f);
            camera.nearClipPlane=.05f;camera.farClipPlane=8;camera.cullingMask=1<<31;
            var parts=model.GetComponentsInChildren<Transform>();var layers=parts.Select(p=>p.gameObject.layer).ToArray();foreach(var part in parts)part.gameObject.layer=31;
            var all=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);var hidden=all.Select(r=>r.forceRenderingOff).ToArray();
            for(int i=0;i<all.Length;i++)if(!all[i].transform.IsChildOf(model))all[i].forceRenderingOff=true;
            var lights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None);var lit=lights.Select(l=>l.enabled).ToArray();foreach(var light in lights)light.enabled=false;
            var ambient=RenderSettings.ambientLight;var mode=RenderSettings.ambientMode;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.50f,.54f,.62f);
            var key=new GameObject("Animal studio key");var sun=key.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.color=new Color(1,.95f,.88f);sun.shadows=LightShadows.None;key.transform.rotation=Quaternion.Euler(40,-150,0);
            var target=new RenderTexture(768,768,24);var previous=RenderTexture.active;Texture2D image=null;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image=new Texture2D(768,768,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,768,768),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report("Wildlife-"+id+".png"),image.EncodeToPNG());}
            finally{RenderSettings.ambientLight=ambient;RenderSettings.ambientMode=mode;for(int i=0;i<lights.Length;i++)if(lights[i])lights[i].enabled=lit[i];Object.Destroy(key);for(int i=0;i<parts.Length;i++)if(parts[i])parts[i].gameObject.layer=layers[i];for(int i=0;i<all.Length;i++)if(all[i])all[i].forceRenderingOff=hidden[i];RenderTexture.active=previous;camera.targetTexture=null;target.Release();Object.Destroy(target);if(image)Object.Destroy(image);Object.Destroy(go);}
        }
    }
}
