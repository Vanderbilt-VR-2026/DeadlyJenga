using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CastleBots;
using DeadlyJenga.Modes;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class MysteryPlayChecks
    {
        const string Key="MysteryPlayChecks";
        static string Report=>ProjectPath.Report("MysteryPlay.txt");
        static int errors;
        static MysteryPlayChecks()
        {
            EditorApplication.update+=()=>{var request=ProjectPath.Report("MysteryPlay.request");if(File.Exists(request)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating){try{File.Delete(request);}catch(IOException){return;}Run();}};
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode){AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();SessionState.SetBool(Key+"Locked",true);errors=0;Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>{var go=new GameObject("Mystery regression driver");Object.DontDestroyOnLoad(go);go.AddComponent<MysteryCheckDriver>().StartCoroutine(Guard(Check()));};}
                if(state==PlayModeStateChange.EnteredEditMode){File.AppendAllText(Report,(errors==0?"PASS ":"FAIL ")+"Play Mode teardown completes without errors\n");Application.logMessageReceived-=Log;if(SessionState.GetBool(Key+"Locked",false)){SessionState.SetBool(Key+"Locked",false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();}SessionState.SetBool(Key,false);var original=SessionState.GetString(Key+"Original","");if(File.Exists(original))EditorSceneManager.OpenScene(original);}
            };
        }
        static void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}}
        [MenuItem("Tools/Deadly Jenga/Validation/Check Mystery Play")]
        public static void Run()
        {
            File.WriteAllText(Report,"Integrated Murder Mystery gameplay checks\n");SessionState.SetString(Key+"Original",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);MatchSetup.FromLobby=false;MatchSetup.ModeId="demo";MatchSetup.MapId="castle";EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Require(bool ok,string label){File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+label+"\n");if(!ok)throw new Exception(label);}
        static IEnumerator Until(Func<bool> predicate,string label,float timeout=30)
        {float end=Time.realtimeSinceStartup+timeout;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;Require(predicate(),label);}
        static IEnumerator Guard(IEnumerator test)
        {
            var stack=new Stack<IEnumerator>();stack.Push(test);
            while(stack.Count>0){bool more=false;object value=null;Exception error=null;try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception e){error=e;}if(error!=null){File.AppendAllText(Report,"FAIL "+error+"\n");break;}if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator child){stack.Push(child);continue;}yield return value;}
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();EditorApplication.ExitPlaymode();
        }
        static IEnumerator Check()
        {
            yield return Until(()=>Object.FindFirstObjectByType<CastlePlaySession>()?.Modes!=null,"Real Game scene starts");
            var session=Object.FindFirstObjectByType<CastlePlaySession>();var mode=session.Modes;
            var rules=ModeSimulation.Defaults(GameModeKind.MurderMystery);rules.countdownSeconds=.2f;rules.spawn=SpawnRule.Top;rules.mystery.attackGraceSeconds=0;rules.mystery.reviveSeconds=.3f;mode.Configure(rules);
            while(session.SurvivorBots.Count<5)Require(session.Bots().SpawnBot(),"Spawn a real participant bot");
            foreach(var go in session.SurvivorBots.Members){var motor=go.GetComponent<CastleSurvivor>();motor.InputDriver=null;var brain=go.GetComponent<CastleBotBrain>();if(brain)brain.enabled=false;}
            mode.StartRound();yield return Until(()=>mode.Frame.phase==RoundPhase.Playing&&mode.Frame.players.Length>=6,"Mystery round includes humans and actual bot avatars");
            mode.SetDemoRole(MysteryRole.Murderer);Require(mode.Personal.role==MysteryRole.Murderer,"Host demo role control changes the real local secret role");
            Require(mode.Frame.players.All(p=>!p.name.StartsWith("Red")&&!p.name.StartsWith("Blue")),"Mystery participants avoid reserved red and blue avatar colors");
            var source=mode.FindActor(mode.LocalActorId);var candidates=mode.Frame.players.Where(p=>p.bot&&p.alive).ToArray();var victim=candidates[0];var target=mode.FindActor(victim.id);
            PlacePair(source,target);mode.SetSpecialAttack(true);var model=(MysterySimulation)typeof(MatchModeRuntime).GetField("mystery",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(mode);var delta=target.transform.position+Vector3.up*.6f-source.ViewPose.position;Physics.Raycast(source.ViewPose.position,delta.normalized,out var obstruction,delta.magnitude,(1<<Layers.Block)|(1<<Layers.Surface),QueryTriggerInteraction.Ignore);File.AppendAllText(Report,"DIAGNOSTIC source="+source.transform.position+" eye="+source.ViewPose.position+" target="+target.transform.position+" private="+JsonUtility.ToJson(mode.Personal)+" expected="+model.Hit(mode.LocalActorId,victim.id)+" victimRole="+model.Role(victim.id)+" victimAlive="+model.Alive(victim.id)+" sourceAlive="+model.Alive(mode.LocalActorId)+" winner="+model.Winner+" elapsed="+model.Elapsed+" obstruction="+(obstruction.collider?obstruction.collider.name:"none")+"\n");Require(mode.RequestPunch(source.gameObject,source.ViewPose.position,delta.normalized,3),"Real special punch is accepted");File.AppendAllText(Report,"DIAGNOSTIC immediate="+JsonUtility.ToJson(model.Public())+"\n");
            var strikeFrame=(MysteryPublicFrame)typeof(MatchModeRuntime).GetMethod("MysteryFrame",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(mode,null);Require(strikeFrame.strikes.Any(s=>s.actor==mode.LocalActorId&&s.role==MysteryRole.Murderer),"Special red weapon reveal is included in the public attack frame");
            yield return new WaitForSecondsRealtime(.3f);
            if(!mode.Clues.bodies.Any(b=>b.id==victim.id))File.AppendAllText(Report,"DIAGNOSTIC "+mode.CaptureFrameJson()+"\n");Require(mode.Clues.bodies.Any(b=>b.id==victim.id)&&mode.Frame.players.Any(p=>p.id==victim.id&&!p.alive&&!p.dying),"Murderer punch kills and creates a corpse rather than the puddle transition");
            Require(Object.FindObjectsByType<DeadlyJenga.CandyPlayers.CandyMysteryBody>(FindObjectsSortMode.None).Length==1,"Corpse reuses the current shared avatar with ragdoll physics");
            string publicJson=mode.CaptureFrameJson();Require(!publicJson.Contains("specialAttack")&&!publicJson.Contains("disasterChoices"),"Public match snapshot excludes secret role and choice records");
            mode.ReportBody(victim.id);Require(!mode.Clues.bodies.First(b=>b.id==victim.id).reported,"Own-kill report cooldown is enforced in gameplay");
            yield return new WaitForSecondsRealtime(1.2f);Require(errors==0,"Corpse settles and renders without Unity errors");
            mode.SetDemoRole(MysteryRole.Innocent);yield return new WaitForSecondsRealtime(.3f);
            var next=mode.Frame.players.First(p=>p.bot&&p.alive&&JsonUtility.FromJson<MysteryPrivateFrame>(mode.CaptureSecret(p.id)).role==MysteryRole.Innocent);
            target=mode.FindActor(next.id);PlacePair(source,target);target.gameObject.GetComponent<IDamageable>().Damage(target.MaxHealth-4,source.gameObject);
            mode.RequestPunch(source.gameObject,source.ViewPose.position,(target.transform.position+Vector3.up*.6f-source.ViewPose.position).normalized,3);yield return new WaitForSecondsRealtime(.3f);
            Require(mode.Clues.downed.Contains(next.id)&&mode.Frame.players.Any(p=>p.id==mode.LocalActorId&&!p.alive),"Wrong innocent punch eliminates attacker and leaves the victim revivable");
            Require(mode.RulesFor(target.gameObject).downed&&!mode.RulesFor(target.gameObject).canJump,"Downed participant has crawling movement rules");
            var helperRow=mode.Frame.players.First(p=>p.bot&&p.alive&&p.id!=next.id);var helper=mode.FindActor(helperRow.id);
            helper.gameObject.GetComponent<IPlayableCharacter>().Place(target.transform.position+Vector3.right*.65f,Quaternion.identity);Physics.SyncTransforms();
            float reviveEnd=Time.realtimeSinceStartup+1.2f;
            while(Time.realtimeSinceStartup<reviveEnd){mode.ReceiveCommand(helperRow.id,"mystery:revive:"+next.id);yield return new WaitForSecondsRealtime(.1f);}
            Require(!mode.Clues.downed.Contains(next.id)&&target.Health==target.MaxHealth,"Held revive restores the actual downed character and health");
            Require(session.GetComponent<CandyMysteryView>()&&session.GetComponent<CandyMysteryTrails>(),"Mystery HUD and footprint renderer attach to actual session");
            Require(errors==0,"Integrated Mystery check finishes without errors");File.AppendAllText(Report,"COMPLETE: desktop solo gameplay. Quest and multi-device Mystery remain unverified.\n");
        }
        static void PlacePair(IModeActor source,IModeActor target)
        {
            var point=Services.World.SafeSpawnPoint()+Vector3.up*3;
            source.gameObject.GetComponent<IPlayableCharacter>().Place(point,Quaternion.identity);
            target.gameObject.GetComponent<IPlayableCharacter>().Place(point+Vector3.forward*1.1f,Quaternion.identity);Physics.SyncTransforms();
        }
    }
    public sealed class MysteryCheckDriver:MonoBehaviour{}
}
