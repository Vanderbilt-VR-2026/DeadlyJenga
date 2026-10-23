using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class ActionFeatureChecks
    {
        const string Key="ActionFeatureChecks";
        [Serializable] sealed class Scenes {public SceneSetup[] scenes;}
        static int checks,errors;static bool failed;
        static string Report=>ProjectPath.Report(SessionState.GetBool(Key+"Lifecycle",false)?"ActionLifecycle.txt":"ActionFeatures.txt");
        static ActionFeatureChecks()
        {
            EditorApplication.playModeStateChanged+=state=>{
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode){checks=errors=0;failed=false;Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>new GameObject("Action integration validation").AddComponent<ActionCheckDriver>().StartCoroutine(Guard());}
                if(state==PlayModeStateChange.EnteredEditMode){Restore();var saved=JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes",""));if(saved.scenes.Any(s=>s.isActive&&s.isLoaded))EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Authoritative Sounds and Bullets")]
        public static void Run()=>Start(false);
        public static void RunLifecycleOnly()=>Start(true);
        static void Start(bool lifecycle)
        {
            SessionState.SetBool(Key+"Lifecycle",lifecycle);
            var scenes=EditorSceneManager.GetSceneManagerSetup();if(scenes.Any(s=>s.isLoaded&&SceneManager.GetSceneByPath(s.path).isDirty))throw new Exception("Save edited scenes first.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{scenes=scenes}));SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);SessionState.SetBool(Key,true);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.ModeId="demo";MatchSetup.MapId="castle";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;
            File.WriteAllText(Report,"Actual Game accepted weapon commands, NPC damage/death, pooled presentation and two real Photon transport peers. Cursor migration fixtures do not simulate a full host election. No headset.\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Restore(){Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);}
        static void Log(string message,string trace,UnityEngine.LogType type)
        {
            if(type!=UnityEngine.LogType.Error&&type!=UnityEngine.LogType.Exception&&type!=UnityEngine.LogType.Assert)return;
            if(NetworkProjectConfig.Global.PeerMode==NetworkProjectConfig.PeerModes.Multiple&&message.EndsWith("PeerModes.Multiple requires a scene to be set in StartGameArgs.Scene. Set a scene or disable Multiple Peer mode in the NetworkProjectConfig.",StringComparison.Ordinal)){File.AppendAllText(Report,"EDITOR NOTE Known Fusion diagnostic for an additional transport peer without a game scene.\n");return;}
            errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");
        }
        static void Check(bool pass,string text){File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+text+"\n");if(!pass)throw new Exception(text);checks++;}
        static IEnumerator Guard()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Checks());
            while(stack.Count>0){bool more=false;object current=null;Exception error=null;try{more=stack.Peek().MoveNext();if(more)current=stack.Peek().Current;}catch(Exception e){error=e;}if(error!=null){failed=true;File.AppendAllText(Report,"FAIL "+error+"\n");break;}if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}if(current is IEnumerator child){stack.Push(child);continue;}yield return current;}
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();File.AppendAllText(Report,"COMPLETE checks="+checks+" runtimeErrors="+errors+"\n");
            if(Application.isBatchMode){Restore();EditorApplication.Exit(failed||errors>0?1:0);}else EditorApplication.ExitPlaymode();
        }
        static IEnumerator Checks()
        {
            float until=Time.realtimeSinceStartup+40;while(!(Services.Session is CastlePlaySession)&&Time.realtimeSinceStartup<until)yield return null;
            var session=Services.Session as CastlePlaySession;Check(session,"Actual Game session initializes");session.SetRole(false);session.SetMenu(false);session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();if(session.Social!=null)session.Social.VoiceEnabled=false;
            var rules=session.Modes.Rules.Copy();rules.countdownSeconds=0;rules.automaticDisasters=false;rules.healthEnabled=true;rules.fallDamage=false;rules.island=IslandRule.Walkable;session.Modes.Configure(rules);session.Modes.StartRound();yield return new WaitForSeconds(.3f);
            if(SessionState.GetBool(Key+"Lifecycle",false)){yield return Lifecycle(session);Check(errors==0,"No unexpected errors in map / review lifecycle checks");yield break;}
            yield return WeaponEventChecks.Run(session);Check(File.ReadAllText(ProjectPath.Report("WeaponEvents.txt")).Contains("COMPLETE checks="),"Authoritative weapon interaction, denial, exact endpoint and presentation checks pass");
            session.Modes.StartRound();yield return new WaitForSeconds(.3f);
            yield return EnemyActionChecks.Run(session,Check);
            yield return ActionEventNetworkChecks.Run(session.Modes,EnemyActionChecks.Baseline,EnemyActionChecks.Accepted,Check);
            yield return Lifecycle(session);
            Check(errors==0,"No unexpected errors in the actual Game / two-peer action run");
        }
        sealed class LifecyclePickup:IEquipmentPickupSource
        {
            readonly Vector3 position;
            public LifecyclePickup(Vector3 value){position=value;}
            public EquipmentKind Kind=>EquipmentKind.DualRifle;
            public Vector3 Position=>position;
            public float Duration=>20;
        }
        static IEnumerator Lifecycle(CastlePlaySession session)
        {
            var runtime=session.Modes;var inventory=(IEquipmentRuntime)runtime;var player=session.Survivor();
            Vector3 oldPosition=player.transform.position;Quaternion oldRotation=player.transform.rotation;bool suspended=player.InputSuspended,tools=session.developmentReviewTools;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Event reset fixture floor";floor.layer=Layers.Surface;floor.transform.position=new Vector3(1400,10,1400);floor.transform.localScale=new Vector3(8,1,8);
            try
            {
                player.Teleport(floor.transform.position+Vector3.up*.54f,Quaternion.identity);player.InputSuspended=true;yield return new WaitForSeconds(.3f);
                int pickup=inventory.RegisterPickup(new LifecyclePickup(player.transform.position+Vector3.up*.35f));
                Check(inventory.RequestPickup(pickup,true)&&inventory.RequestEquipment(true,false,false),"Lifecycle fixture accepts real pickup / draw before rebuilding");
                string previous=runtime.Frame.equipmentEpoch;int sequence=runtime.Frame.equipmentSequence;
                var cursor=new DeadlyJenga.CandyAudio.CandyEquipmentActionCursor();cursor.Baseline(runtime.Frame);
                runtime.MapRebuilt();yield return new WaitForSeconds(.3f);
                Check(!string.IsNullOrEmpty(runtime.Frame.equipmentEpoch)&&runtime.Frame.equipmentEpoch==previous&&runtime.Frame.equipmentSequence==sequence&&runtime.Frame.equipmentActions.Length==0,"Map rebuild clears transients while retaining the session epoch and highest sequence");
                Check(!string.IsNullOrEmpty(runtime.Frame.enemyActionEpoch)&&DeadlyJenga.Modes.ModeFrameValidation.Frame(runtime.Frame,Services.World.Blocks.Count),"Map rebuild snapshot retains validated weapon and enemy event headers");
                pickup=inventory.RegisterPickup(new LifecyclePickup(player.transform.position+Vector3.up*.35f));
                Check(inventory.RequestPickup(pickup,true)&&inventory.RequestEquipment(true,false,false),"First pickup / draw after map reset is accepted");
                int consumed=0;cursor.Read(runtime.Frame,_=>consumed++);cursor.Read(runtime.Frame,_=>consumed++);
                Check(consumed==2,"Existing observer receives the first new-round actions once, even if no empty reset packet arrives");
                session.developmentReviewTools=true;previous=runtime.Frame.equipmentEpoch;string enemyEpoch=runtime.Frame.enemyActionEpoch;sequence=runtime.Frame.equipmentSequence;
                Check(runtime.CanEndGame,"Living demo round can end through the real review command");runtime.EndGame();
                Check(runtime.Frame.equipmentEpoch==previous&&runtime.Frame.enemyActionEpoch==enemyEpoch&&runtime.Frame.equipmentSequence==sequence,"End Game snapshot preserves both event stream baselines");
                Check(DeadlyJenga.Modes.ModeFrameValidation.Frame(runtime.Frame,Services.World.Blocks.Count),"Real post-game snapshot passes action / match validation");
            }
            finally{session.developmentReviewTools=tools;player.Teleport(oldPosition,oldRotation);player.InputSuspended=suspended;UnityEngine.Object.Destroy(floor);}
        }
    }
    public sealed class ActionCheckDriver:MonoBehaviour { }
}
