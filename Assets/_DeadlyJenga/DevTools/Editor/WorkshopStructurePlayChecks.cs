using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.World.Hazards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class WorkshopStructurePlayChecks
    {
        const string Key="WorkshopStructurePlayChecks";
        static readonly string[] Cases=new[]{"andy","castle"}.Concat(WorkshopStructureStudy.Ids).ToArray();
        static string Report=>ProjectPath.Report("SparseStructuresPlay.txt");
        static int errors;
        [Serializable] sealed class Scenes{public SceneSetup[] values;}
        static WorkshopStructurePlayChecks()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode){errors=0;Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Guard());}
                if(state==PlayModeStateChange.EnteredEditMode){Application.logMessageReceived-=Log;EditorApplication.delayCall+=Next;}
            };
        }
        [MenuItem("Tools/Deadly Jenga/Maps/Play Check Workshop Structure Suite")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            if(setup.Any(s=>s.isLoaded&&SceneManager.GetSceneByPath(s.path).isDirty))throw new InvalidOperationException("Save edited scenes first.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{values=setup}));
            SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);
            SessionState.SetInt(Key+"Index",-1);SessionState.SetBool(Key+"Failed",false);SessionState.SetBool(Key,true);
            File.WriteAllText(Report,"Actual Game/Andy scene smoke and sparse maps with 0/16 survivor bots and real seeded tsunami. Short Windows Editor CPU samples; not Quest or release FPS.\n");Next();
        }
        static void Next()
        {
            int index=SessionState.GetInt(Key+"Index",-1)+1;
            if(index>=Cases.Length||SessionState.GetBool(Key+"Failed",false))
            {
                bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);
                SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
                MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
                var setup=JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes",""));if(setup?.values?.Length>0)EditorSceneManager.RestoreSceneManagerSetup(setup.values);
                File.AppendAllText(Report,failed?"FAILED\n":"COMPLETE all requested cases, no runtime errors. Headset and live multiplayer remain unverified.\n");
                if(Application.isBatchMode)EditorApplication.Exit(failed?1:0);return;
            }
            SessionState.SetInt(Key+"Index",index);MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.ModeId="demo";MatchSetup.MapId=Cases[index];
            EditorSceneManager.OpenScene(ProjectPath.Find(index==0?"Andy.unity":"Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Assert||type==LogType.Exception){errors++;File.AppendAllText(Report,"ERROR "+message+"\n");}}
        static IEnumerator Guard()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Check());bool failed=false;
            while(stack.Count>0)
            {
                bool next=false;object current=null;Exception error=null;
                try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){File.AppendAllText(Report,"FAIL "+error+"\n");failed=true;break;}
                if(!next){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(current is IEnumerator nested){stack.Push(nested);continue;}yield return current;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            File.AppendAllText(Report,"Runtime errors="+errors+"\n");if(failed||errors>0)SessionState.SetBool(Key+"Failed",true);EditorApplication.ExitPlaymode();
        }
        static IEnumerator Check()
        {
            var session=Object.FindFirstObjectByType<CastlePlaySession>();float deadline=Time.realtimeSinceStartup+30;
            while((Services.Session==null||session.Player==null||session.SurvivorBots==null)&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(session&&session.Player!=null&&session.Hand!=null&&Services.World!=null,"solo player, hand and world initialize");
            session.SurvivorBots.Clear();session.HandBots.Clear();if(session.Social!=null)session.Social.VoiceEnabled=false;session.SetMenu(true);
            string id=Cases[SessionState.GetInt(Key+"Index",0)];var world=Services.World;
            File.AppendAllText(Report,"\nMAP "+id+" bodies="+world.Blocks.Count+" bounds="+world.PlayableBounds+"\n");
            Require(world.PlayableBounds.size.x>0&&world.PlayableBounds.size.y>0,"valid world/disaster bounds");
            if(id=="andy")
            {
                Require(world.Blocks.Count==0,"Andy is a blank island");var sync=world as ISyncedWorld;
                Require(sync!=null&&sync.LoadLayout(sync.SaveLayout(),0,new Pose(sync.Root.position,sync.Root.rotation),sync.Root.localScale),"Andy layout round-trips");
                session.ResetMap();Require(world.Blocks.Count==0,"Andy reset remains blank");yield return new WaitForSeconds(1);yield break;
            }
            Require(world.Blocks.Count>0,"selected map generates members");
            yield return new WaitForSeconds(3);
            yield return Sample("settled",24);
            if(id=="castle")yield break;
            Require(world is WorkshopStructureMap,"lobby selection loads the new sparse map prefab");
            var map=(WorkshopStructureMap)world;var position=map.Bodies.Select(b=>b.position).ToArray();session.ResetMap();
            Require(Services.World==world&&map.Bodies.Count==position.Length,"actual session reset preserves map type and member count");
            for(int i=0;i<16;i++)session.SurvivorBots.SpawnOne();yield return new WaitForSeconds(2);
            File.AppendAllText(Report,"Actual survivor bots="+session.SurvivorBots.Count+"/16 requested\n");
            yield return Sample("bots",24);
            UnityEngine.Random.InitState(71293);
            var wave=Object.Instantiate(Resources.Load<GameObject>("DemoDisasters/Tsunami")).GetComponent<TsunamiHazard>();
            wave.randomDirection=false;wave.approachHeading=0;wave.warningTime=0;wave.offshoreDistance=0;wave.riseTime=.1f;wave.Begin();
            try{yield return new WaitForSeconds(3);yield return Sample("bots+tsunami",24);}
            finally{wave.End();Object.Destroy(wave.gameObject);}
            Require(map.Bodies.All(b=>b&&Finite(b.position)&&Finite(b.linearVelocity)),"all original identities remain finite after tsunami");
        }
        static IEnumerator Sample(string label,int count)
        {
            var frames=new List<float>();for(int i=0;i<count;i++){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}
            frames.Sort();int awake=Services.World.Blocks.Count(b=>b&&b.gameObject.activeInHierarchy&&!b.IsSleeping());
            File.AppendAllText(Report,$"FRAME {label}: median={frames[count/2]:F2}ms p90={frames[Mathf.Min(count-1,(int)(count*.9f))]:F2}ms awake={awake} samples={count}\n");
        }
        static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.x)&&!float.IsInfinity(p.y)&&!float.IsInfinity(p.z);
        static void Require(bool pass,string message)
        {if(!pass)throw new InvalidOperationException(message);File.AppendAllText(Report,"PASS "+message+"\n");}
    }
}
