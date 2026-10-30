using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class HealthLobbyEntranceChecks
    {
        const string Key="HealthLobbyEntranceChecks";
        static string Report=>ProjectPath.Report("HealthLobbyEntrance.txt");
        static int failures,errors;
        static HealthLobbyEntranceChecks()
        {
            EditorApplication.update+=()=>
            {
                var request=ProjectPath.Report("HealthLobbyEntrance.request");
                if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                File.Delete(request);Run();
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    failures=errors=0;Application.logMessageReceived+=Log;
                    EditorApplication.delayCall+=()=>{var go=new GameObject("Health and lobby checks");Object.DontDestroyOnLoad(go);go.AddComponent<HealthLobbyCheckDriver>().StartCoroutine(Guard());};
                }
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
                    var original=SessionState.GetString(Key+"Scene","");if(File.Exists(original))EditorSceneManager.OpenScene(original);
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Health Lobby and Spirit Entrance")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before this check.");
            File.WriteAllText(Report,"Health, lobby selectors and sea spirit entrance\n");
            SessionState.SetString(Key+"Scene",SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);
            EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.ModeId="demo";MatchSetup.MapId="castle";
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            if(message.StartsWith("[Package Manager Window]",StringComparison.Ordinal)){File.AppendAllText(Report,"EDITOR NOTE "+message+"\n");return;}
            errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");
        }
        static void Check(bool value,string label){File.AppendAllText(Report,(value?"PASS ":"FAIL ")+label+"\n");if(!value)throw new Exception(label);}
        static object Field(object target,string name)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
        static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
        static IEnumerator Guard()
        {
            var test=CheckScene();
            while(true)
            {
                bool next=false;object value=null;Exception caught=null;
                try{next=test.MoveNext();if(next)value=test.Current;}catch(Exception e){caught=e;}
                if(caught!=null){failures++;File.AppendAllText(Report,"FAIL "+caught+"\n");break;}
                if(!next)break;yield return value;
            }
            File.AppendAllText(Report,$"COMPLETE failures={failures}; errors={errors}. Quest comfort and a two-device health regeneration test remain unverified.\n");
            EditorApplication.ExitPlaymode();
        }
        static IEnumerator CheckScene()
        {
            yield return new WaitForSeconds(1);
            var session=Object.FindFirstObjectByType<CastlePlaySession>();Check(session&&session.Modes,"Actual Game scene starts");
            var rules=session.Modes.Rules.Copy();rules.healthEnabled=true;rules.automaticDisasters=false;session.Modes.Configure(rules);
            var player=session.Player.gameObject.GetComponent<CastleSurvivor>();player.Revive();session.SetMenu(true);
            Check(player.healthRecoveryDelay==10&&player.healthRecoveryPerSecond==5,"Default recovery waits ten seconds then restores five HP per second");
            player.Damage(40,null);Call(player,"RecoverHealth",1f);Check(Mathf.Approximately(player.Health,60),"Recent damage prevents recovery");
            Set(player,"lastDamageAt",Time.time-10.5f);Call(player,"RecoverHealth",.5f);Check(Mathf.Abs(player.Health-62.5f)<.01f,"Recovery restores health gradually at configured rate");
            player.Damage(5,null);Call(player,"RecoverHealth",1f);Check(Mathf.Abs(player.Health-57.5f)<.01f,"Further damage restarts the recovery delay");
            var downed=rules.Copy();downed.downed=true;player.ApplyModeRules(downed);Set(player,"lastDamageAt",Time.time-20);Call(player,"RecoverHealth",1f);Check(Mathf.Abs(player.Health-57.5f)<.01f,"Recovery cannot bypass a downed-player revive");player.ApplyModeRules(rules);
            Set(player,"eliminated",true);Call(player,"RecoverHealth",1f);Check(Mathf.Abs(player.Health-57.5f)<.01f,"Recovery cannot resurrect an eliminated survivor");Set(player,"eliminated",false);
            var ledger=new RemoteHealthLedger();ledger.Damage("peer",40,true);ledger.Recover("peer",9.9f);Check(Mathf.Approximately(ledger.Read("peer"),.6f),"Host remote health also respects recovery delay");
            ledger.Recover("peer",.2f);Check(Mathf.Abs(ledger.Read("peer")-.605f)<.0001f,"Host remote recovery handles partial delay crossing");ledger.Damage("peer",100,true);ledger.Recover("peer",50);Check(ledger.Read("peer")==0,"Host cannot regenerate eliminated remote players");
            Set(player,"lastDamageAt",Time.time);yield return new WaitForSeconds(.4f);
            var hud=session.GetComponent<CandyMatchHud>();Check(hud,"Health HUD is attached through Presentation");Call(hud,"Update");var group=(CanvasGroup)Field(hud,"healthGroup");Check(group.alpha>.5f,"Damage reveals the local health bar");
            var shader=Resources.Load<Shader>("CandyScenery/CandyAvatarHealth");Check(shader&&ShaderUtil.GetShaderMessages(shader).All(m=>m.severity!=UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),"Avatar health shader compiles");
            var visual=session.GetComponent<CandyMatchVisuals>();Call(visual,"Update");
            var renderer=player.Visual.GetComponentsInChildren<Renderer>().First(r=>r.sharedMaterial&&r.sharedMaterial.shader==shader);var props=new MaterialPropertyBlock();renderer.GetPropertyBlock(props);Check(props.GetColor("_BaseColor")!=Color.white,"Health material retains the assigned character color");
            player.Revive();yield return new WaitForSeconds(.4f);Check(group.alpha>.8f,"Full health keeps the health bar visible briefly");yield return new WaitForSecondsRealtime(5.5f);Check(group.alpha<.05f,"Health bar fades away five seconds after full health");
            var before=new Pose(session.ActiveCamera.transform.position,session.ActiveCamera.transform.rotation);session.SetSeaSpirit(true);
            var spirit=session.Hand.gameObject.GetComponent<SeaSpiritController>();Check(spirit&&spirit.Entering&&!session.MenuOpen,"Becoming a spirit starts the entrance and closes the menu");
            Call(spirit,"Update");Check(Vector3.Distance(session.ActiveCamera.transform.position,before.position)<1,"Entrance begins at the previous survivor viewpoint");Check(spirit.LeftHand.InputSuspended&&spirit.RightHand.InputSuspended,"Physical hands cannot attack during the entrance");
            float born=(float)Field(spirit,"entranceAt");Set(spirit,"entranceAt",Time.unscaledTime-spirit.entranceSeconds*.46f);Call(spirit,"Update");Check(session.ActiveCamera.transform.position.y>Services.World.TopHeight+5,"Reveal camera rises above the build");Check(spirit.BustAnchor.position!=session.ActiveCamera.transform.position,"Monster anchor stays separate from the reveal camera");
            yield return new WaitForSecondsRealtime(1.1f);
            Set(spirit,"entranceAt",Time.unscaledTime-spirit.entranceSeconds*.46f);Call(spirit,"Update");yield return null;
            if(!Application.isBatchMode){yield return new WaitForEndOfFrame();var shot=ScreenCapture.CaptureScreenshotAsTexture();if(shot){File.WriteAllBytes(ProjectPath.Report("SpiritEntrance-Reveal.png"),shot.EncodeToPNG());Object.Destroy(shot);}}
            Set(spirit,"entranceAt",Time.unscaledTime-spirit.entranceSeconds-1);Call(spirit,"Update");Check(!spirit.Entering&&!spirit.LeftHand.InputSuspended&&!spirit.RightHand.InputSuspended,"Controls unlock after the entrance finishes");Check(Vector3.Distance(session.ActiveCamera.transform.position,spirit.BustAnchor.position)<.1f,"Camera settles at the spirit gameplay eye");
            session.SetSeaSpirit(false);yield return null;Check(!session.SeaSpiritActive,"Leaving spirit mode cleans up the entrance controller");
            EditorSceneManager.LoadSceneInPlayMode(ProjectPath.Find("Lobby.unity"),new LoadSceneParameters(LoadSceneMode.Single));yield return new WaitForSeconds(1);
            var lobbyView=Object.FindFirstObjectByType<IslandLobbyView>();Check(lobbyView,"Actual lobby builds its terminals");var lobby=Services.Session as ILobbyControls;var details=lobby as ILobbyDetails;
            var buttons=lobbyView.GetComponentsInChildren<Button>(true);Check(new[]{"map-previous","map-next","mode-previous","mode-next"}.All(id=>buttons.Any(b=>b.name==id)),"Lobby offers previous and next arrows for both map and mode");
            string original=details.SelectedMapId;Check(lobbyView.InvokeChoice("map-next")&&lobbyView.InvokeChoice("map-previous")&&details.SelectedMapId==original,"Map arrows cycle and wrap without opening a list");
            Check(lobbyView.InvokeChoice("mode-next")&&lobbyView.InvokeChoice("mode-previous"),"Mode arrows also work");
            lobbyView.InvokeChoice("map-current");lobbyView.Point(new Ray(new Vector3(1000,1000,1000),Vector3.up),false);Call(lobbyView,"UpdateGameStation");
            var rows=buttons.Where(b=>b.gameObject.activeSelf&&b.name.StartsWith("picker-")&&int.TryParse(b.name.Substring(7),out _)).ToArray();
            Check(rows.Length>0&&rows.Length<=7,"Map list is bounded to seven choices per page");Check(rows.All(b=>Mathf.Abs(((RectTransform)b.transform).anchoredPosition.y)+((RectTransform)b.transform).rect.height*.5f<350),"Picker rows stay inside terminal content bounds");
            if(details.Maps.Count>7){Check(lobbyView.InvokeChoice("picker-next"),"Next page is reachable for a long map catalog");Check(lobbyView.InvokeChoice("picker-previous"),"Previous page returns through a long map catalog");}
            lobbyView.InvokeChoice("setup-back");Check((string)Field(lobbyView,"setupPage")=="main","Back returns directly to map and mode arrows");
            Check(errors==0,"No gameplay runtime errors during checks");
        }
    }
    public sealed class HealthLobbyCheckDriver:MonoBehaviour {}
}
