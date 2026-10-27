using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class BreakwaterVillagePlayChecks
    {
        const string Key="BreakwaterVillagePlayChecks";
        [Serializable] sealed class SavedScenes{public SceneSetup[] values;}
        static int errors;
        static string Report=>ProjectPath.Report("BreakwaterPlay.txt");
        static BreakwaterVillagePlayChecks()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    errors=0;Application.logMessageReceived+=Log;
                    MatchSetup.MapId=BreakwaterVillageBuilder.MapId;MatchSetup.ModeId="demo";
                    EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Guard());
                }
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
                    SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
                    MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
                    EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key+"Scenes","")).values);
                    if(Application.isBatchMode)EditorApplication.Exit(SessionState.GetBool(Key+"Failed",false)?1:0);
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Play Check Breakwater Village")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>s.isLoaded&&UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))throw new InvalidOperationException("Save edited scenes before village checks.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new SavedScenes{values=scenes}));
            SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.MapId=BreakwaterVillageBuilder.MapId;MatchSetup.ModeId="demo";
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Failed",false);
            File.WriteAllText(Report,"Actual Game.unity, real player, actual bots and normal disaster FixedUpdate lifecycle. Desktop Editor only; no headset or two-player claim.\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        public static void Profile()
        {SessionState.SetBool(Key+"Profile",true);Run();}
        static void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n");}}
        static IEnumerator Guard()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Check());bool failed=false;
            while(stack.Count>0)
            {
                bool next=false;object current=null;Exception failure=null;
                try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception e){failure=e;}
                if(failure!=null){File.AppendAllText(Report,"FAIL "+failure+"\n");failed=true;break;}
                if(!next){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(current is IEnumerator nested){stack.Push(nested);continue;}yield return current;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            File.AppendAllText(Report,"Runtime errors="+errors+"\n");SessionState.SetBool(Key+"Failed",failed||errors>0);EditorApplication.ExitPlaymode();
        }
        static IEnumerator Check()
        {
            var session=Object.FindFirstObjectByType<CastlePlaySession>();float deadline=Time.realtimeSinceStartup+35;
            while((session.Player==null||session.SurvivorBots==null)&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(session.Player!=null&&session.Hand!=null,"actual solo player and hand initialized");
            session.SurvivorBots.Clear();session.HandBots.Clear();if(session.Social!=null)session.Social.VoiceEnabled=false;
            if(!(Services.World is BreakwaterVillageMap)){MatchSetup.MapId=BreakwaterVillageBuilder.MapId;MatchSetup.ApplyMap();yield return null;}
            Require(Services.World is BreakwaterVillageMap,"registered lobby map resolves and loads in Game");
            var map=(BreakwaterVillageMap)Services.World;session.SetMenu(true);yield return new WaitForSeconds(6);
            Require(map.BrokenConnections==0,"actual Game quiet village connections remain intact");
            for(int i=0;i<8;i++)session.SurvivorBots.SpawnOne();yield return new WaitForSeconds(5);
            Require(session.SurvivorBots.Count==8,"eight real survivor bots spawned");
            yield return Sample("social + 8 bots");
            BreakwaterVillageChecks.Shot("10-game-social-bots",new Vector3(-40,28,-43),new Vector3(0,3,5));
            var quake=BreakwaterVillageChecks.MakeHazard("Earthquake",new Dictionary<string,float>{{"force",14},{"frequency",1.35f},{"vertical",.12f},{"warning",0},{"duration",12}});
            quake.Begin();Require(quake.Active,"earthquake begins through normal map validation");yield return new WaitForSeconds(12.5f);
            quake.End();Object.Destroy(quake.gameObject);yield return new WaitForSeconds(3);
            File.AppendAllText(Report,"Earthquake broken connections="+map.BrokenConnections+"\n");Require(map.BrokenConnections>0,"live earthquake changes building structure");
            yield return Sample("after live earthquake + bots");BreakwaterVillageChecks.Shot("11-game-after-earthquake",new Vector3(-40,28,-43),new Vector3(0,3,5));
            if(SessionState.GetBool(Key+"Profile",false))
            {
                SessionState.SetBool(Key+"Profile",false);session.SurvivorBots.Clear();yield return new WaitForSeconds(2);
                yield return Sample("same earthquake aftermath, bots removed");
                session.ResetMap();yield return new WaitForSeconds(4);
                Require(map.BrokenConnections==0,"map resets intact before death-visual regression");
                yield return MeltControlChecks.Play(session,Require,(melt,label)=>
                {var center=melt.CurrentBodyBounds.center;BreakwaterVillageChecks.Shot("13-"+label,center+new Vector3(-2.5f,1.2f,-3.5f),center);});
                Require(errors==0,"profile run has no runtime errors");yield break;
            }
            session.ResetMap();yield return new WaitForSeconds(5);Require(map.BrokenConnections==0,"session reset restores native structures");
            UnityEngine.Random.InitState(91827);
            var shower=BreakwaterVillageChecks.MakeHazard("MeteorShower",new Dictionary<string,float>{{"size",3.6f},{"deviation",.6f},{"frequency",3},{"force",18},{"warning",0},{"duration",24}});
            shower.Begin();yield return new WaitForSeconds(24.5f);shower.End();Object.Destroy(shower.gameObject);yield return new WaitForSeconds(3);
            File.AppendAllText(Report,"Meteor broken connections="+map.BrokenConnections+"\n");Require(map.BrokenConnections>0,"live meteor shower changes structure");
            yield return Sample("after live meteors + bots");BreakwaterVillageChecks.Shot("12-game-after-meteors",new Vector3(-40,28,-43),new Vector3(0,3,5));
            Require(map.pieces.All(p=>Finite(p.Body.position)),"all member positions finite after live hazards");
            session.ResetMap();yield return new WaitForSeconds(4);Require(map.BrokenConnections==0,"second reset after meteors restores connections");
            Require(errors==0,"no runtime errors during Game scene test");File.AppendAllText(Report,"COMPLETE desktop Game scene checks. Quest and two-player multiplayer remain unverified.\n");
        }
        static IEnumerator Sample(string label)
        {
            var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);
            var counters=new List<(string name,ProfilerRecorder recorder)>();
            foreach(var handle in handles)
            {
                var d=ProfilerRecorderHandle.GetDescription(handle);
                if(d.Name=="Physics.Simulate"||d.Name=="Physics.SyncTransforms"||d.Name=="BehaviourUpdate"||d.Name=="BehaviourFixedUpdate"||d.Name=="Main Thread"||d.Name=="Render Thread"||d.Name=="GC Allocated In Frame"||d.Name.StartsWith("DJ."))
                    counters.Add((d.Name,ProfilerRecorder.StartNew(d.Category,d.Name,64)));
            }
            try
            {
                var frames=new List<float>();for(int i=0;i<60;i++){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}
                frames.Sort();File.AppendAllText(Report,$"FRAME {label} median={frames[30]:F2}ms p90={frames[54]:F2}ms; 60 Editor frames, not device FPS; awake={Services.World.Blocks.Count(b=>b&&!b.IsSleeping())}\n");
                foreach(var c in counters)
                {var samples=new List<ProfilerRecorderSample>();c.recorder.CopyTo(samples);if(samples.Count>0)File.AppendAllText(Report,$"  {c.name} mean={samples.Average(s=>(double)s.Value):F0}"+(c.name=="GC Allocated In Frame"?" bytes":" ns")+"\n");}
            }
            finally{foreach(var c in counters)c.recorder.Dispose();}
        }
        static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.x)&&!float.IsInfinity(p.y)&&!float.IsInfinity(p.z);
        static void Require(bool pass,string message){File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+message+"\n");if(!pass)throw new InvalidOperationException(message);}
    }
}
