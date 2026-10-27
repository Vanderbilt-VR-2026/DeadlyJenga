using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class DemoBulkEquipmentChecks
    {
        const string Key="DemoBulkEquipment";
        static int checks,errors;static bool failed,completed;
        static string Report=>ProjectPath.Report("DemoBulkEquipment.txt");
        [Serializable] sealed class Scenes{public SceneSetup[] values;}
        static DemoBulkEquipmentChecks()
        {
            EditorApplication.update+=()=>
            {
                string path=ProjectPath.Report("DemoBulkEquipment.request");
                if(!File.Exists(path)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
                if(!ValidationRunGate.TryAcquire(Key))return;
                File.Delete(path);Run();
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {checks=errors=0;failed=completed=false;AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();SessionState.SetBool(Key+"Locked",true);Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Guard());}
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    ValidationRunGate.Release(Key);
                    if(SessionState.GetBool(Key+"Locked",false)){SessionState.SetBool(Key+"Locked",false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();}
                    Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
                    SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
                    MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
                    EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes","")).values);
                    failed|=!completed;File.AppendAllText(Report,$"COMPLETE checks={checks} runtimeErrors={errors} failed={failed}. Desktop runtime; headset not tested.\n");
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Demo Bulk Equipment")]
        public static void Run()
        {
            SessionState.SetBool(Key+"VisualOnly",false);
            var setup=EditorSceneManager.GetSceneManagerSetup();
            if(setup.Any(s=>s.isLoaded&&UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))throw new InvalidOperationException("Save edited scenes first.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{values=setup}));
            SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);SessionState.SetBool(Key,true);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;
            File.WriteAllText(Report,"Actual host bulk equipment, life-only and persistent respawn integration.\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType kind)
        {
            if(Fusion.NetworkProjectConfig.Global.PeerMode==Fusion.NetworkProjectConfig.PeerModes.Multiple&&message.EndsWith("PeerModes.Multiple requires a scene to be set in StartGameArgs.Scene. Set a scene or disable Multiple Peer mode in the NetworkProjectConfig.",StringComparison.Ordinal)){File.AppendAllText(Report,"TRANSPORT FIXTURE NOTE: empty scene multiple-peer diagnostic; packet test, not replicated gameplay scene.\n");return;}
            if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n");}
        }
        static void Check(bool pass,string label)
        {File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+label+"\n");checks++;if(!pass)throw new InvalidOperationException(label);}
        static IEnumerator Guard()
        {
            var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(Test());
            while(stack.Count>0)
            {
                bool more=false;object value=null;Exception error=null;
                try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){failed=true;File.AppendAllText(Report,"FAIL "+error+"\n");break;}
                if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator child){stack.Push(child);continue;}yield return value;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();completed=!failed;EditorApplication.ExitPlaymode();
        }
        static IEnumerator Test()
        {
            var session=Object.FindFirstObjectByType<CastlePlaySession>();float until=Time.realtimeSinceStartup+30;
            while((session.Player==null||session.Modes==null)&&Time.realtimeSinceStartup<until)yield return null;
            Check(session.Player!=null&&session.Modes!=null,"solo Game initializes");
            session.SetRole(false);session.SetMenu(false);session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots.Clear();if(session.Social!=null)session.Social.VoiceEnabled=false;
            var runtime=session.Modes;var player=session.Survivor();var rules=ModeSimulation.Defaults(GameModeKind.Demo);rules.countdownSeconds=0;rules.deathEnabled=true;rules.healthEnabled=true;rules.fallDamage=false;rules.island=IslandRule.Walkable;runtime.Configure(rules);runtime.StartRound();
            yield return new WaitForSeconds(.4f);player.InputSuspended=true;
            for(int i=0;i<3;i++)session.SurvivorBots.SpawnOne();yield return new WaitForSeconds(.4f);
            foreach(var member in session.SurvivorBots.Members){member.GetComponent<CastleSurvivor>().InputSuspended=true;member.GetComponent<CastleBotBrain>().enabled=false;}
            var group=session.SurvivorBots.Members.Select(m=>m.GetComponent<CastleSurvivor>()).ToArray();var bot=group[0];var all=runtime.Frame.players.Where(p=>p.alive&&p.role=="survivor").Select(p=>p.id).ToArray();
            var bulk=(IDemoLoadoutControls)runtime;
            Check(all.Length>=4&&bulk.RespawnEquipment==EquipmentKind.None,"Host and three bots start with respawn grants off");
            int pickups=runtime.Frame.pickups.Length;
            Check(bulk.GiveEveryoneEquipment(EquipmentKind.DualRifle,false),"Current-life bulk grant accepted");
            Check(all.All(id=>runtime.EquipmentFor(id).kind==EquipmentKind.DualRifle),"Every living survivor receives one rifle");
            Check(runtime.Frame.pickups.Length==pickups,"Bulk grant creates no replacement pickup pile");
            Check(bulk.RespawnEquipment==EquipmentKind.None,"Current-life grant leaves automatic respawn grants off");
            bot.Eliminate(null);yield return new WaitForSeconds(.3f);
            Check(runtime.Frame.pickups.Count(p=>p.kind==EquipmentKind.DualRifle)==1,"Killed bot drops exactly one granted rifle");
            float end=Time.realtimeSinceStartup+8;while((!bot.Alive||runtime.Frame.players.First(p=>p.id==bot.ActorId).dying)&&Time.realtimeSinceStartup<end)yield return null;yield return new WaitForSeconds(.5f);
            Check(bot.Alive&&runtime.EquipmentFor(bot.ActorId).kind==EquipmentKind.None,"Current-life weapon does not return on actual bot respawn");
            Check(bulk.GiveEveryoneEquipment(EquipmentKind.Blaster,true),"Now plus every respawn accepted");
            Check(all.All(id=>runtime.EquipmentFor(id).kind==EquipmentKind.Blaster)&&bulk.RespawnEquipment==EquipmentKind.Blaster,"Persistent grant equips everyone immediately and publishes setting");
            bot.Eliminate(null);yield return new WaitForSeconds(.3f);end=Time.realtimeSinceStartup+8;
            while((!bot.Alive||runtime.EquipmentFor(bot.ActorId).kind!=EquipmentKind.Blaster)&&Time.realtimeSinceStartup<end)yield return null;
            Check(bot.Alive&&runtime.EquipmentFor(bot.ActorId).kind==EquipmentKind.Blaster,"Actual bot respawn restores chosen weapon");
            Check(runtime.Frame.pickups.Count(p=>p.kind==EquipmentKind.Blaster)==1,"Persistent respawn does not duplicate the death drop");
            var newcomer=session.SurvivorBots.SpawnOne();yield return new WaitForSeconds(.7f);newcomer.GetComponent<CastleSurvivor>().InputSuspended=true;newcomer.GetComponent<CastleBotBrain>().enabled=false;
            Check(runtime.EquipmentFor(newcomer.GetComponent<IModeActor>().ActorId).kind==EquipmentKind.Blaster,"New survivor bots inherit the active respawn loadout");
            Check(runtime.GiveEquipment(EquipmentKind.BoxingGlove),"Player may replace weapon during the same life");yield return new WaitForSeconds(.5f);
            Check(runtime.EquipmentFor(runtime.LocalActorId).kind==EquipmentKind.BoxingGlove,"Respawn policy does not overwrite a mid-life weapon swap");
            player.Eliminate(null);yield return new WaitForSeconds(.3f);end=Time.realtimeSinceStartup+8;
            while((!player.Alive||runtime.EquipmentFor(runtime.LocalActorId).kind!=EquipmentKind.Blaster)&&Time.realtimeSinceStartup<end)yield return null;
            Check(player.Alive&&runtime.EquipmentFor(runtime.LocalActorId).kind==EquipmentKind.Blaster,"Actual local player respawn also restores chosen loadout");player.InputSuspended=true;
            var saved=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());Check(saved.demoRespawnEquipment==EquipmentKind.Blaster&&ModeFrameValidation.Frame(saved,Services.World.Blocks.Count),"Snapshot JSON preserves valid automatic equipment policy");
            typeof(MatchModeRuntime).GetMethod("RestoreEquipment",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(runtime,new object[]{saved});
            Check(bulk.RespawnEquipment==EquipmentKind.Blaster,"Authority-state restore preserves automatic equipment policy");
            Check(bulk.GiveEveryoneEquipment(EquipmentKind.None,false)&&bulk.RespawnEquipment==EquipmentKind.None,"Stop future grants without clearing held gear");
            Check(runtime.EquipmentFor(bot.ActorId).kind==EquipmentKind.Blaster,"Stopping grants leaves current-life gear intact");
            bot.Eliminate(null);yield return new WaitForSeconds(.3f);end=Time.realtimeSinceStartup+8;while(!bot.Alive&&Time.realtimeSinceStartup<end)yield return null;yield return new WaitForSeconds(.5f);
            Check(bot.Alive&&runtime.EquipmentFor(bot.ActorId).kind==EquipmentKind.None,"Stopped policy stays stopped on the next real respawn");
            Check(!bulk.GiveEveryoneEquipment((EquipmentKind)999,true)&&!bulk.GiveEveryoneEquipment(EquipmentKind.None,true),"Invalid policy requests are rejected");
            Check(bulk.GiveEveryoneEquipment(EquipmentKind.Blaster,true),"Persistent policy can be selected again");
            runtime.StartRound();yield return new WaitForSeconds(.7f);Check(bulk.RespawnEquipment==EquipmentKind.Blaster&&runtime.EquipmentFor(runtime.LocalActorId).kind==EquipmentKind.Blaster,"Demo round restart preserves policy and rearms survivors");
            rules=ModeSimulation.Defaults(GameModeKind.CaptureFlag);rules.countdownSeconds=0;runtime.Configure(rules);runtime.StartRound();yield return new WaitForSeconds(.4f);
            Check(bulk.RespawnEquipment==EquipmentKind.Blaster&&bulk.GiveEveryoneEquipment(EquipmentKind.Blaster,true),"Competitive modes preserve and accept development loadouts");
            saved=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());saved.demoRespawnEquipment=EquipmentKind.Blaster;Check(ModeFrameValidation.Frame(saved,Services.World.Blocks.Count),"Competitive snapshots preserve the development loadout policy");
            runtime.Configure(ModeSimulation.Defaults(GameModeKind.Demo));runtime.StartRound();yield return new WaitForSeconds(.4f);Check(bulk.RespawnEquipment==EquipmentKind.Blaster,"Mode changes preserve the explicitly selected development policy");
            BotObjectiveContractChecks.Run();ModeCriteriaChecks.Run();MysteryRuleChecks.Run();
            Check(BotObjectiveContractChecks.LastPassed&&!File.ReadAllText(ProjectPath.Report("ModeCriteria.txt")).Contains("FAIL")&&!File.ReadAllText(ProjectPath.Report("MysteryRules.txt")).Contains("FAIL"),"All-mode objective, scoring and Mystery rule regressions pass");
            var replicaObject=new GameObject("Non-authoritative grant rejection");var replica=replicaObject.AddComponent<MatchModeRuntime>();
            Check(!replica.GiveEveryoneEquipment(EquipmentKind.Blaster,true),"Non-authoritative runtime cannot grant or set a global policy");Object.Destroy(replicaObject);
            Check(bulk.GiveEveryoneEquipment(EquipmentKind.PoweredArmor,true),"Transport fixture selects a persistent suit loadout");
            Check(runtime.RequestThrust(true),"Transport fixture creates real active suit state");var thrust=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());
            runtime.RequestThrust(false);var stopped=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());
            yield return PoweredSuitNetworkChecks.Run(runtime,thrust,stopped,Check);
            Check(bulk.GiveEveryoneEquipment(EquipmentKind.None,false),"Transport fixture stops future loadouts after verification");
            var menu=Object.FindFirstObjectByType<CastleWorldMenu>(FindObjectsInactive.Include);Check(menu,"VR menu exists");menu.Show(true,session.ActiveCamera);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(CastleWorldMenu).GetField("giveAllEquipment",flags).SetValue(menu,true);typeof(CastleWorldMenu).GetField("giveEquipmentPage",flags).SetValue(menu,true);
            typeof(CastleWorldMenu).GetMethod("PointGiveEquipmentPage",flags).Invoke(menu,new object[]{session.ActiveCamera,false});
            var texts=menu.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t=>t.text).ToArray();
            Check(texts.Any(t=>t.Contains("ALL PLAYERS + BOTS"))&&texts.Any(t=>t.Contains("THIS LIFE ONLY"))&&texts.Any(t=>t.Contains("Stop respawn grants")),"Actual VR menu exposes target, life policy and stop controls");
            menu.Show(false,session.ActiveCamera);var hud=session.GetComponent<DeadlyJenga.CastlePrototype.CandyDemoHud>();
            typeof(CandyDemoHud).GetField("giveEquipmentPage",flags).SetValue(hud,true);typeof(CandyDemoHud).GetField("giveAllEquipment",flags).SetValue(hud,true);typeof(CandyDemoHud).GetField("repeatEquipment",flags).SetValue(hud,true);typeof(CandyDemoHud).GetField("page",flags).SetValue(hud,0);
            session.SetMenu(true);yield return new WaitForSeconds(.2f);yield return new WaitForEndOfFrame();var screenshot=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(ProjectPath.Report("Demo-Give-Everyone.png"),screenshot.EncodeToPNG());Object.Destroy(screenshot);
        }
    }
}
