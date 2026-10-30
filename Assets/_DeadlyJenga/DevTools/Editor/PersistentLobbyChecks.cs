using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class PersistentLobbyChecks
    {
        const string Key="PersistentLobbyChecks";
        static IEnumerator run;static int errors,checks;static bool failed;
        static string Report=>ProjectPath.Report("PersistentLobbyChecks.txt");
        [Serializable] sealed class Saved{public SceneSetup[] scenes;public string choices;public bool background,fromLobby,local;public float volume;}
        static PersistentLobbyChecks(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        public static void Run()
        {
            string root=Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
            if(Path.GetFullPath(File.ReadAllText(ProjectPath.Report("CandyTerminalDesign.owner")).Trim()).ToLowerInvariant()!=root.ToLowerInvariant())throw new InvalidOperationException("Wrong owned editor.");
            if(EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Editor busy.");
            if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Unsaved scenes untouched.");
            SessionState.SetString(Key+"Saved",JsonUtility.ToJson(new Saved{scenes=EditorSceneManager.GetSceneManagerSetup(),choices=JsonUtility.ToJson(SpectatorRecordingPlan.Current()),background=Application.runInBackground,volume=AudioListener.volume,fromLobby=MatchSetup.FromLobby,local=MatchSetup.StartLocalSession}));
            SessionState.SetBool(Key,true);File.WriteAllText(Report,"Actual Unity persistent real-lobby validation\n"+root+"\n");
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=10;MatchSetup.HandCount=0;
            EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));EditorApplication.EnterPlaymode();
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
                File.AppendAllText(Report,"COMPLETE checks="+checks+" errors="+errors+" failed="+failed+"\nNo Quest hardware claim. Two-peer lobby messaging is distinct from full two-client match scene travel.\n");
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
                string request=ProjectPath.Report("PersistentLobbyChecks.request");
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
            yield return Until(()=>IslandLobby.Persistent&&IslandLobby.Persistent.Retained,"Actual Lobby scene is retained");
            yield return Wait(2);
            var lobby=IslandLobby.Persistent;var view=lobby.GetComponent<IslandLobbyView>();var wall=lobby.GetComponentInChildren<LobbySpectatorTVWall>();var player=lobby.Player;
            Check(lobby==IslandLobby.Current&&ReferenceEquals(Services.Session,lobby)&&ReferenceEquals(Services.World,lobby),"Initial lobby owns services and routing");
            Check(view&&wall,"Original terminals and TV wall build once");
            int lobbyId=lobby.GetInstanceID(),viewId=view.GetInstanceID(),wallId=wall.GetInstanceID(),playerId=player.gameObject.GetInstanceID();Vector3 center=lobby.Center;
            var terminals=view.transform.Find("JOIN");int terminalId=terminals.GetInstanceID();var wardrobe=view.transform.Find("WARDROBE");int wardrobeId=wardrobe.GetInstanceID();
            Check(Vector3.Distance(view.WardrobePosition,lobby.Center+new Vector3(0,.65f,-5.3f))<.01f,"Wardrobe interaction follows the retained lobby position");
            Check(wall.AssignedChannels==0&&wall.RenderCount==0,"TVs remain blank before a match, including lobby bots");
            Capture(lobby,"PersistentLobby-Before.png");
            for(int round=0;round<2;round++)
            {
                Check(lobby.SelectMode("demo")&&lobby.SelectMap("castle"),"Real lobby selects demo/castle for round "+round);
                Check(lobby.CanStart,"Start is available for round "+round);lobby.StartOrCancel();
                yield return Until(()=>Object.FindFirstObjectByType<CastlePlaySession>()&&SceneManager.GetActiveScene().name=="Game","Lobby starts Game round "+round,60);
                yield return Wait(3);
                var session=Object.FindFirstObjectByType<CastlePlaySession>();var world=Services.World;
                Check(IslandLobby.Persistent&&IslandLobby.Persistent.GetInstanceID()==lobbyId,"Same lobby survives Game load "+round);
                Check(view.GetInstanceID()==viewId&&wall.GetInstanceID()==wallId&&terminals.GetInstanceID()==terminalId&&wardrobe.GetInstanceID()==wardrobeId,"Terminals, wardrobe and TV objects survive unchanged "+round);
                Check(lobby.Player.gameObject.GetInstanceID()==playerId&&!player.gameObject.activeInHierarchy,"Original lobby player is parked, not recreated "+round);
                Check(IslandLobby.Current==null&&!NetworkActors.Local.Contains(player.gameObject.GetComponent<INetworkActor>()),"Parked lobby stays out of game network routing "+round);
                float elapsed=session.Modes.Frame.elapsed;session.VisitLobby();yield return null;
                Check(session.VisitingLobby&&lobby.VisitingLobby&&IslandLobby.Persistent.GetInstanceID()==lobbyId,"Exit to lobby reuses original lobby "+round);
                Check(ReferenceEquals(Services.World,world)&&ReferenceEquals(Services.Session,session),"Lobby visit preserves running match services "+round);
                Check(!NetworkActors.Local.Contains(player.gameObject.GetComponent<INetworkActor>())&&!Tracked.Movers.Contains(player as IMovementState),"Visitor does not enter match registries "+round);
                Check(!lobby.CanConfigure&&lobby.CanStart,"Visit locks setup and offers return "+round);
                AimAtWall(lobby,wall);
                yield return Until(()=>wall.AssignedChannels>0&&wall.ReadyChannels==wall.AssignedChannels,"Every assigned TV receives a real match frame "+round,45);
                Check(session.Modes.Frame.elapsed>elapsed,"Match continues while visiting "+round);
                Check(!view.Environment.enabled&&!view.Environment.applyLighting,"Lobby background does not replace match lighting/world "+round);
                Capture(lobby,"PersistentLobby-Live-"+round+".png");
                lobby.StartOrCancel();yield return Wait(.5f);
                Check(!session.VisitingLobby&&!player.gameObject.activeInHierarchy&&wall.GetInstanceID()==wallId,"Return parks lobby without destroying TVs "+round);
                session.VisitLobby();yield return null;Check(session.VisitingLobby&&lobby.GetInstanceID()==lobbyId,"Repeat visit reuses lobby "+round);session.ReturnToMatch();yield return null;
                Check(Object.FindObjectsByType<IslandLobby>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,"Only one lobby exists during the match "+round);
                session.Modes.EndGame();yield return Wait(1.4f);
                Check(wall.AssignedChannels==0,"TVs return to blank after round end "+round);
                session.Modes.ReturnToLobby();
                yield return Until(()=>IslandLobby.Current&&ReferenceEquals(Services.Session,lobby)&&!Object.FindFirstObjectByType<CastlePlaySession>(),"Results return reactivates original lobby "+round);
                yield return Wait(.5f);
                Check(lobby.GetInstanceID()==lobbyId&&view.GetInstanceID()==viewId&&wall.GetInstanceID()==wallId&&player.gameObject.GetInstanceID()==playerId,"All original lobby identities persist between games "+round);
                Check(player.gameObject.activeInHierarchy&&player.ViewCamera.enabled&&!lobby.MatchResident,"Lobby controls and camera resume "+round);
                Check(lobby.Center==center&&Vector3.Distance(view.Environment.transform.position,center)<.1f,"Lobby island remains at its original retained position "+round);
                Check(Object.FindObjectsByType<IslandLobby>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,"Scene return creates no duplicate lobby "+round);
            }
            // Real second Photon peer, existing production transport and codec. No scene travel is claimed by this probe.
            yield return ModeSelectionNetworkChecks.Run(Check);
            Check(IslandLobby.Persistent.GetInstanceID()==lobbyId,"Real two-peer lobby test preserves the same lobby instance");
            Capture(lobby,"PersistentLobby-Returned.png");
            SceneManager.LoadScene("Home");yield return Wait(2);
            Check(!player.gameObject.activeInHierarchy&&!DeadlyJenga.CandyAudio.CandyLobbyMusic.Active,"Home screen parks lobby camera/player and music");
            SceneManager.LoadScene("Lobby");yield return Wait(2);
            Check(IslandLobby.Current==lobby&&lobby.GetInstanceID()==lobbyId,"Home-to-lobby reuses the same original lobby");
            // Preserve Game.unity direct-entry development support: load the actual Lobby scene only once on demand.
            Object.Destroy(lobby.gameObject);yield return Wait(.3f);SceneManager.LoadScene("Game");
            yield return Until(()=>Object.FindFirstObjectByType<CastlePlaySession>(),"Game supports direct scene entry");yield return Wait(2);
            var direct=Object.FindFirstObjectByType<CastlePlaySession>();var directWorld=Services.World;direct.VisitLobby();
            yield return Until(()=>direct&&direct.VisitingLobby,"Direct Game entry loads the real Lobby scene on first visit",45);
            Check(IslandLobby.Persistent&&ReferenceEquals(Services.World,directWorld),"On-demand real lobby does not replace match world");
            var directLobby=IslandLobby.Persistent;direct.ReturnToMatch();yield return null;direct.VisitLobby();yield return null;
            Check(direct.VisitingLobby&&IslandLobby.Persistent==directLobby,"On-demand lobby is reused on the second visit");
            direct.ReturnToMatch();Check(errors==0,"No runtime errors");
        }
        static void AimAtWall(IslandLobby lobby,LobbySpectatorTVWall wall)
        {
            lobby.Player.Place(wall.transform.TransformPoint(new Vector3(0,0,-4)),Quaternion.LookRotation(wall.transform.forward));lobby.Player.InputSuspended=true;
            lobby.ActiveCamera.transform.rotation=Quaternion.LookRotation(wall.transform.TransformPoint(new Vector3(0,1.5f,0))-lobby.ActiveCamera.transform.position);
        }
        static void Capture(IslandLobby lobby,string name)
        {
            var root=new GameObject("Persistent lobby evidence camera");var camera=root.AddComponent<Camera>();camera.CopyFrom(lobby.ActiveCamera);camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
            camera.transform.position=lobby.Center+new Vector3(7,6.2f,-10.5f);camera.transform.LookAt(lobby.Center+new Vector3(-.5f,1.2f,-1.7f));camera.fieldOfView=65;camera.enabled=false;
            var visual=lobby.Player.gameObject.GetComponent<IModeActor>()?.Visual;var renderers=visual?visual.GetComponentsInChildren<Renderer>(true):Array.Empty<Renderer>();var hidden=renderers.Select(x=>x.forceRenderingOff).ToArray();foreach(var renderer in renderers)renderer.forceRenderingOff=true;
            var target=new RenderTexture(1800,1200,24);camera.targetTexture=target;var previous=RenderTexture.active;Texture2D image=null;
            try{camera.Render();RenderTexture.active=target;image=new Texture2D(1800,1200,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1800,1200),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report(name),image.EncodeToPNG());}
            finally{for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].forceRenderingOff=hidden[i];RenderTexture.active=previous;camera.targetTexture=null;Object.Destroy(root);target.Release();Object.Destroy(target);if(image)Object.Destroy(image);}
        }
    }
}
