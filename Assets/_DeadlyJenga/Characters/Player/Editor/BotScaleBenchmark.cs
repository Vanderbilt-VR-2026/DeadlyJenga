using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Profiling;
using DeadlyJenga.Core;
using DeadlyJenga.CastleBots;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class BotScaleBenchmark
    {
        const string Key="BotScaleBenchmark";
        static readonly int[] populations={10,50,100};
        static readonly List<float> frames=new List<float>(4096),physics=new List<float>(4096),fixedScripts=new List<float>(4096),allocations=new List<float>(4096);
        static readonly StringBuilder report=new StringBuilder();
        static ISessionInfo session;
        static ProfilerRecorder physicsRecorder,fixedRecorder,gcRecorder;
        static int stage,lastFrame=-1,minPopulation,minAlive;
        static double started,lastAt;
        static bool ready;
        static BotScaleBenchmark()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
                stage=-1;ready=false;started=EditorApplication.timeSinceStartup;report.Clear();EditorApplication.update+=Tick;
            };
        }
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                double now=EditorApplication.timeSinceStartup;
                if(!ready)
                {
                    if(now-started<8)return;
                    session=Services.Session;if(session?.SurvivorBots==null||MatchRuntime.Current==null)throw new Exception("Game runtime not ready");
                    Time.timeScale=1;session.HandBots.Clear();if(session.Social!=null)session.Social.VoiceEnabled=false;
                    var prefab=Resources.Load<GameObject>("ModeMaps/Mode_territory");if(!prefab)throw new Exception("Actual territory map not found");
                    var previous=Services.World as Component;if(previous){previous.gameObject.SetActive(false);UnityEngine.Object.Destroy(previous.gameObject);}
                    var map=UnityEngine.Object.Instantiate(prefab);foreach(var c in map.GetComponentsInChildren<MonoBehaviour>())if(c is IRebuildable builder){builder.Rebuild();break;}
                    var rules=ModeDefaults.Create(GameModeKind.Demo);rules.automaticDisasters=false;rules.countdownSeconds=0;rules.roundSeconds=0;rules.deathEnabled=false;rules.island=IslandRule.Walkable;MatchRuntime.Current.Configure(rules);
                    report.AppendLine("Desktop Unity Editor batch CPU workload, not Quest performance. timeScale=1; graphics="+SystemInfo.graphicsDeviceType+"; rendering disabled="+(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null));
                    report.AppendLine("Actual Mode_territory geometry; blocks="+Services.World.Blocks.Count+"; voice/disasters off; normal bot controllers; 5s warmup +15s measurement per population.");
                    physicsRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Physics,"Physics.Simulate",1);
                    fixedRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"BehaviourFixedUpdate",1);
                    gcRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
                    ready=true;Next();return;
                }
                if(Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
                minPopulation=Math.Min(minPopulation,session.SurvivorBots.Count);
                int alive=0;foreach(var actor in session.SurvivorBots.Members)if(actor&&actor.GetComponent<IModeActor>() is IModeActor state&&state.Alive)alive++;
                minAlive=Math.Min(minAlive,alive);
                if(now-started>=5)
                {
                    frames.Add((float)((now-lastAt)*1000));
                    if(physicsRecorder.Valid)physics.Add(physicsRecorder.LastValue/1000000f);
                    if(fixedRecorder.Valid)fixedScripts.Add(fixedRecorder.LastValue/1000000f);
                    if(gcRecorder.Valid)allocations.Add(gcRecorder.LastValue);
                }
                lastAt=now;
                if(now-started<20)return;
                int failures=0,reached=0;foreach(var actor in session.SurvivorBots.Members)if(actor&&actor.GetComponent<CastleBotBrain>() is CastleBotBrain brain){failures+=brain.RouteFailures;reached+=brain.ObjectivesReached;}
                int awake=0;foreach(var body in Services.World.Blocks)if(body&&!body.IsSleeping())awake++;
                report.AppendLine("Population requested="+populations[stage]+" minimum actual="+minPopulation+" minimum alive="+minAlive+" frame samples="+frames.Count+" route failures="+failures+" objectives reached="+reached+" awake map bodies="+awake);
                Stats("Frame interval ms",frames);Stats("Physics.Simulate ms",physics);Stats("BehaviourFixedUpdate ms",fixedScripts);Stats("GC allocated bytes/frame",allocations);
                if(minPopulation!=populations[stage]||minAlive!=populations[stage])report.AppendLine("INVALID CAPACITY SAMPLE: population/alive count fell below requested; do not treat reduced workload as requested capacity.");
                if(stage==populations.Length-1){Finish();return;}Next();
            }
            catch(Exception error){report.AppendLine("FAIL "+error);Finish();}
        }
        static void Next()
        {
            stage++;session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();
            for(int i=0;i<populations[stage];i++)session.SurvivorBots.SpawnOne();
            MatchRuntime.Current.StartRound();
            frames.Clear();physics.Clear();fixedScripts.Clear();allocations.Clear();minPopulation=minAlive=populations[stage];lastFrame=-1;started=lastAt=EditorApplication.timeSinceStartup;
        }
        static void Stats(string name,List<float> values)
        {
            if(values.Count==0){report.AppendLine(name+": unavailable profiler marker");return;}values.Sort();double sum=0;foreach(float value in values)sum+=value;
            report.AppendLine(name+": mean="+(sum/values.Count).ToString("F3")+" median="+values[values.Count/2].ToString("F3")+" p95="+values[Math.Min(values.Count-1,(int)(values.Count*.95))].ToString("F3"));
        }
        static void Finish()
        {
            EditorApplication.update-=Tick;SessionState.SetBool(Key,false);physicsRecorder.Dispose();fixedRecorder.Dispose();gcRecorder.Dispose();
            report.AppendLine("Rendering, editor overhead and host desktop physics are included when enabled; marker samples are frame observations, not isolated bot CPU costs. No Quest GPU/thermal or real-peer claim.");
            Directory.CreateDirectory("Logs/Validation");File.WriteAllText("Logs/Validation/BotScale.txt",report.ToString());EditorApplication.ExitPlaymode();
            if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(0);
        }
    }
}
