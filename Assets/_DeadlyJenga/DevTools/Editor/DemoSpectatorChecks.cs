using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPlayers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class DemoSpectatorChecks
    {
        const string Key="DemoSpectatorChecks";
        static string Report=>ProjectPath.Report("DemoSpectator.txt");
        static int failures,errors;
        static DemoSpectatorChecks()
        {
            EditorApplication.update+=()=>
            {
                var request=ProjectPath.Report("DemoSpectator.request");
                if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                File.Delete(request);Run();
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    failures=errors=0;Application.logMessageReceived+=Log;
                    EditorApplication.delayCall+=()=>new GameObject("Demo spectator checks").AddComponent<DemoSpectatorCheckDriver>().StartCoroutine(Guard());
                }
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
                    AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
                    var original=SessionState.GetString(Key+"Scene","");if(File.Exists(original))EditorSceneManager.OpenScene(original);
                    if(SessionState.GetBool(Key+"Batch",false))EditorApplication.Exit(SessionState.GetInt(Key+"Failures",1));
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Demo Spectator")]
        public static void Run()=>Begin(false);
        public static void RunBatch()=>Begin(true);
        static void Begin(bool batch)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before this check.");
            File.WriteAllText(Report,"Demo spectator integration\n");
            SessionState.SetString(Key+"Scene",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Batch",batch);SessionState.SetInt(Key+"Failures",1);
            EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.ModeId="demo";MatchSetup.MapId="castle";
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType kind)
        {if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}}
        static void Check(bool value,string label)
        {File.AppendAllText(Report,(value?"PASS ":"FAIL ")+label+"\n");if(!value)throw new Exception(label);}
        static IEnumerator Guard()
        {
            var test=CheckScene();
            while(true)
            {
                bool next=false;object value=null;Exception caught=null;
                try{next=test.MoveNext();if(next)value=test.Current;}catch(Exception error){caught=error;}
                if(caught!=null){failures++;File.AppendAllText(Report,"FAIL "+caught+"\n");break;}
                if(!next)break;yield return value;
            }
            File.AppendAllText(Report,$"COMPLETE failures={failures}; errors={errors}. Headset comfort and remote-player POVs not verified.\n");
            SessionState.SetInt(Key+"Failures",failures+errors);EditorApplication.ExitPlaymode();
        }
        static IEnumerator CheckScene()
        {
            yield return new WaitForSeconds(1);
            var session=Object.FindFirstObjectByType<CastlePlaySession>();Check(session&&session.Modes,"Actual Game scene provides session and mode runtime");
            var controls=session.Demo as ISpectatorControls;Check(controls!=null&&controls.CanSpectate,"Demo exposes shared spectator controls");
            var runtime=session.Modes;var tick=runtime.GetType().GetMethod("TickCamera",BindingFlags.Instance|BindingFlags.NonPublic);
            session.SurvivorBots.Clear();
            for(int i=0;i<4;i++)Check(session.SurvivorBots.SpawnOne(),"Ordinary survivor prefab bot spawns "+(i+1));
            yield return new WaitForSeconds(.7f);
            Check(!controls.Spectating&&Views(runtime).Length==0,"Idle spectator allocates no additional POV cameras");
            var source=session.Player.ViewCamera;var start=session.Player.transform.position;var rotation=session.Player.transform.rotation;
            controls.SetFourViews(false);controls.SetSpectating(true);session.SetMenu(false);tick.Invoke(runtime,null);
            Check(controls.Spectating&&session.Player.InputSuspended,"Spectating pauses local control after closing menu");
            Check(session.Player.transform.position==start&&session.Player.transform.rotation==rotation,"Entering spectator never teleports or rotates the local body");
            Check(session.Player.gameObject.activeSelf,"Local body remains active in the world");
            Check(!source.enabled&&session.ActiveCamera&&session.ActiveCamera.enabled,"Spectator camera replaces rendering without enabling bot cameras");
            Check(Views(runtime).Length==1,"Single view renders only one observer camera");
            var first=controls.WatchedRoot(session.ActiveCamera);Check(first&&first!=session.Player.transform,"Observer selects a different living participant");
            var actor=first.GetComponent<IModeActor>();var playable=first.GetComponent<IPlayableCharacter>();
            Check(Vector3.Distance(session.ActiveCamera.transform.position,actor.ViewPose.position)<.001f&&Quaternion.Angle(session.ActiveCamera.transform.rotation,actor.ViewPose.rotation)<.01f,"Observer matches the shared survivor eye position and facing exactly");
            Check(!playable.ViewCamera.enabled&&Mathf.Approximately(session.ActiveCamera.fieldOfView,playable.ViewCamera.fieldOfView),"Bot eye camera stays disabled and observer inherits its lens");
            var rig=first.GetComponentInChildren<CandyPlayerRig>();
            if(rig)
            {
                var before=typeof(CandyPlayerRig).GetMethod("BeforeCamera",BindingFlags.Instance|BindingFlags.NonPublic);
                var after=typeof(CandyPlayerRig).GetMethod("AfterCamera",BindingFlags.Instance|BindingFlags.NonPublic);
                before.Invoke(rig,new object[]{default(ScriptableRenderContext),session.ActiveCamera});
                Check(rig.headRenderer.shadowCastingMode==ShadowCastingMode.ShadowsOnly,"Watched character head is hidden only in its first-person observer camera");
                after.Invoke(rig,new object[]{default(ScriptableRenderContext),session.ActiveCamera});
                Check(rig.headRenderer.shadowCastingMode==ShadowCastingMode.On,"Head rendering restores for other cameras");
            }
            controls.CycleWatched(1);Check(controls.WatchedRoot(session.ActiveCamera)!=first,"Next command changes participant without controlling that participant");
            controls.SetFourViews(true);tick.Invoke(runtime,null);
            var views=Views(runtime);Check(views.Length==4&&views.All(camera=>camera.enabled),"Four-view mode renders four observer cameras");
            Check(views.Select(camera=>controls.WatchedRoot(camera)).Distinct().Count()==4,"Four quadrants follow four distinct living participants");
            Check(views.All(camera=>Mathf.Approximately(camera.rect.width,.5f)&&Mathf.Approximately(camera.rect.height,.5f))&&views.Select(camera=>camera.rect).Distinct().Count()==4,"Camera rectangles tile the screen without overlap");
            Check(views.All(camera=>!controls.WatchedRoot(camera).GetComponent<IPlayableCharacter>().ViewCamera.enabled),"No spectator view enables the original bot camera");
            Check(views.All(camera=>!controls.WatchedRoot(camera).GetComponent<IPlayableCharacter>().InputSuspended),"Watching does not suspend any living bot input driver");
            yield return null;yield return null;
            foreach(var hud in Object.FindObjectsByType<DeadlyJenga.CastlePrototype.CandyDemoHud>(FindObjectsSortMode.None)){typeof(DeadlyJenga.CastlePrototype.CandyDemoHud).GetMethod("PresentHints",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hud,null);Check(!hud.ControlHintCanvas||!hud.ControlHintCanvas.enabled,"Personal movement hints stay hidden while watching");}
            foreach(var hud in Object.FindObjectsByType<DeadlyJenga.Presentation.CandyMatchHud>(FindObjectsSortMode.None))typeof(DeadlyJenga.Presentation.CandyMatchHud).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hud,null);
            if(!Application.isBatchMode)yield return new WaitForEndOfFrame();
            var shot=ScreenCapture.CaptureScreenshotAsTexture();if(shot){File.WriteAllBytes(ProjectPath.Report("DemoSpectator-FourViews.png"),shot.EncodeToPNG());Object.Destroy(shot);}
            controls.SetFourViews(false);yield return null;
            Check(Views(runtime).Length==1,"Returning to single view removes the extra cameras");
            session.SurvivorBots.Clear();yield return new WaitForSeconds(.5f);tick.Invoke(runtime,null);
            Check(controls.WatchedRoot(session.ActiveCamera)==null&&session.ActiveCamera.enabled,"Removing all targets retains a valid build-overview camera");
            controls.SetSpectating(false);yield return null;
            Check(Views(runtime).Length==0&&source.enabled,"Leaving spectator removes all observer cameras and restores original camera");
            var focus=typeof(CastlePlaySession).GetMethod("OnApplicationFocus",BindingFlags.Instance|BindingFlags.NonPublic);
            focus.Invoke(session,new object[]{true});session.SetMenu(false);
            Check(!session.Player.InputSuspended,"Returning to focused play restores local input");
            session.SetMenu(true);Check(session.Player.InputSuspended,"Open menu still pauses local control after spectating");
            Check(errors==0,"No runtime errors during spectator lifecycle");
        }
        static Camera[] Views(Component runtime)=>runtime.GetComponentsInChildren<Camera>(true).Where(camera=>camera.name.StartsWith("Demo spectator POV ")).ToArray();
    }
    public sealed class DemoSpectatorCheckDriver:MonoBehaviour {}
}
