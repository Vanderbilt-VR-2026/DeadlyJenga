using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class BotPipelineChecks
    {
        const string Key="BotPipelineChecks";
        [Serializable] public sealed class Request { public int seconds=45; public int fps=60; public bool carrier; public int[] populations={1,10,50}; }
        [Serializable] sealed class Scenes { public SceneSetup[] scenes; }
        sealed class Target:IBotObjectives
        {
            public Vector3 destination;
            public void Collect(GameObject actor,List<BotObjective> goals)=>goals.Add(new BotObjective{id="validation-cross-map",kind=BotObjectiveKind.Travel,position=destination,radius=.8f,priority=2});
        }
        static Request request;
        static CastlePlaySession session;
        static int stage=-1,failures,lastFrame=-1;
        static float began,nextSample,oldScale,nextTrace;
        static int oldCapture,oldTarget;
        static double boot;
        static bool configured;
        static IBotObjectives previousObjectives;
        static readonly StringBuilder report=new StringBuilder();
        static readonly Dictionary<int,Vector3> starts=new Dictionary<int,Vector3>();
        static readonly HashSet<int> crossed=new HashSet<int>();
        static readonly List<double> frameMilliseconds=new List<double>();
        static double lastAt;
        static int captures;
        static string Report=>ProjectPath.Report("BotPipeline.txt");
        static string Csv=>ProjectPath.Report("BotPipeline.csv");
        static BotPipelineChecks()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"Restore",false))Restore();};
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Bot Pipeline")]
        public static void Run()=>Begin(new Request());
        static void Begin(Request input)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SpectatorVideoCapture.Busy)throw new InvalidOperationException("A bot check requires an idle Editor.");
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>string.IsNullOrEmpty(s.path))||Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Save your edited scenes before running bot checks.");
            input.seconds=Mathf.Clamp(input.seconds,10,300);input.fps=Mathf.Clamp(input.fps,15,120);
            if(input.populations==null||input.populations.Length==0||input.populations.Any(n=>n<1||n>100))throw new ArgumentException("Use populations between 1 and 100.");
            SessionState.SetFloat(Key+"Scale",Time.timeScale);SessionState.SetInt(Key+"Capture",Time.captureFramerate);SessionState.SetInt(Key+"Target",Application.targetFrameRate);
            SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"FromLobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{scenes=scenes}));
            SessionState.SetString(Key+"Request",JsonUtility.ToJson(input));SessionState.SetBool(Key,true);
            File.WriteAllText(Report,"Actual Game scene / Mode_ctf bot pipeline checks. Desktop only. Carrier checks use one initial placement at the enemy flag; all subsequent movement and scoring are ordinary gameplay.\n");
            File.WriteAllText(Csv,"population,time,id,action,state,reason,x,y,z,speed,desired,plannerAge,recoveries,objective,team,targetX,targetY,targetZ\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            string cancel=ProjectPath.Report("BotPipeline.cancel");
            if(File.Exists(cancel)){File.Delete(cancel);if(SessionState.GetBool(Key,false)){File.AppendAllText(Report,"Cancelled by request.\n");Finish();}return;}

            if(!SessionState.GetBool(Key,false))
            {
                string file=ProjectPath.Report("BotPipeline.request.json");
                if(!File.Exists(file)||EditorApplication.isCompiling||EditorApplication.isUpdating||SpectatorVideoCapture.Busy)return;
                try{string json=File.ReadAllText(file);File.Delete(file);Begin(JsonUtility.FromJson<Request>(json)??new Request());}
                catch(Exception e){File.WriteAllText(Report,"FAIL "+e);}
                return;
            }
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            try
            {
                if(!EditorApplication.isPlaying)return;
                if(request==null)
                {
                    request=JsonUtility.FromJson<Request>(SessionState.GetString(Key+"Request",""));
                    oldCapture=SessionState.GetInt(Key+"Capture",0);oldTarget=SessionState.GetInt(Key+"Target",-1);oldScale=SessionState.GetFloat(Key+"Scale",1);
                    Time.timeScale=1;Time.captureFramerate=request.fps;Application.targetFrameRate=request.fps;
                    boot=EditorApplication.timeSinceStartup;stage=-1;failures=0;configured=false;
                }
                if(!configured)
                {
                    if(EditorApplication.timeSinceStartup-boot<3)return;
                    session=UnityEngine.Object.FindFirstObjectByType<CastlePlaySession>();
                    if(!session||session.SurvivorBots==null){if(EditorApplication.timeSinceStartup-boot>30)throw new Exception("Game runtime did not initialize within 30 seconds.");return;}
                    session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots.Clear();
                    if(session.Social!=null)session.Social.VoiceEnabled=false;
                    var prefab=Resources.Load<GameObject>("ModeMaps/Mode_ctf");if(!prefab)throw new Exception("CTF map missing");
                    if(Services.World is Component old){old.gameObject.SetActive(false);UnityEngine.Object.Destroy(old.gameObject);}
                    var map=UnityEngine.Object.Instantiate(prefab);
                    foreach(var c in map.GetComponentsInChildren<MonoBehaviour>())if(c is IRebuildable builder){builder.Rebuild();break;}
                    configured=true;Next();return;
                }
                if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
                double now=EditorApplication.timeSinceStartup;
                if(lastAt>0&&Time.time-began>3)frameMilliseconds.Add((now-lastAt)*1000);
                lastAt=now;
                foreach(var team in session.Modes.Frame.teams)captures=Mathf.Max(captures,team.captures);
                foreach(var bot in session.Bots().Bots)
                    if(starts.TryGetValue(bot.GetInstanceID(),out var origin)&&Mathf.Abs(origin.x-Services.World.Center.x)>10&&(origin.x-Services.World.Center.x)*(bot.transform.position.x-Services.World.Center.x)<0)crossed.Add(bot.GetInstanceID());
                if(Time.time>=nextSample)
                {
                    nextSample=Time.time+.5f;var rows=new StringBuilder();
                    foreach(var bot in session.Bots().Bots)
                    {
                        var p=bot.transform.position;
                        rows.AppendLine(string.Join(",",request.populations[stage],F(Time.time-began),bot.GetInstanceID(),bot.CurrentAction,bot.MovementState,bot.StationaryReason.Replace(',',';'),F(p.x),F(p.y),F(p.z),F(bot.ActualSpeed),F(bot.DesiredSpeed),F(bot.PlannerAge),bot.Recoveries,bot.ObjectiveId,bot.Motor.Team,F(bot.TripDestination.x),F(bot.TripDestination.y),F(bot.TripDestination.z)));
                    }
                    File.AppendAllText(Csv,rows.ToString());
                }
                if(request.carrier&&Time.time>=nextTrace)
                {
                    nextTrace=Time.time+5;
                    foreach(var bot in session.Bots().Bots)File.AppendAllText(Report,"TRACE t="+F(Time.time-began)+" position="+bot.transform.position+" trip="+bot.TripDestination+" goal="+bot.Goal+" state="+bot.DebugState+" recovery="+bot.RecoveryDiagnostics+" route="+bot.LastRouteSearch+"\n");
                }
                if(Time.time-began<request.seconds)return;
                int moved=0,blocked=0;float travel=0,idle=0;
                foreach(var bot in session.Bots().Bots)
                {
                    if(bot.TravelDistance>2)moved++;
                    if(bot.UnexplainedIdleSeconds>request.seconds*.5f)blocked++;
                    travel+=bot.TravelDistance;idle+=bot.UnexplainedIdleSeconds;
                    File.AppendAllText(Report,bot.name+" action="+bot.CurrentAction+" distance="+F(bot.TravelDistance)+" moving="+F(bot.MovingSeconds)+" idle="+F(bot.UnexplainedIdleSeconds)+" maxIdle="+F(bot.LongestIdle)+" recovery="+bot.Recoveries+" route="+bot.LastRouteSearch+" changes="+bot.LastRouteChange+" objective="+bot.ObjectiveId+" position="+bot.transform.position+" reason="+bot.StationaryReason+"\n");
                }
                int population=request.populations[stage];
                if(session.Modes.ObjectiveDirector!=null)
                {
                    var director=session.Modes.ObjectiveDirector;
                    File.AppendAllText(Report,"Director mode="+director.ModeId+" active="+director.ActiveCount+" candidates="+director.CandidateCount+" lastMs="+director.LastMilliseconds.ToString("F3",CultureInfo.InvariantCulture)+" peakMs="+director.PeakMilliseconds.ToString("F3",CultureInfo.InvariantCulture)+" (includes facts queries)\n");
                    foreach(var e in director.History)File.AppendAllText(Report,"ASSIGNMENT t="+F(e.time-began)+" actor="+e.actor+" token="+e.assignment+" objective="+e.objective+" outcome="+e.outcome+" reason="+e.reason+"\n");
                    if(population>1&&director.ActiveCount==0){failures++;File.AppendAllText(Report,"FAIL No active director assignments.\n");}
                }
                else if(population>1){failures++;File.AppendAllText(Report,"FAIL Director missing.\n");}
                bool pass=session.SurvivorBots.Count==population&&moved>=Mathf.CeilToInt(population*.8f)&&blocked<=Mathf.FloorToInt(population*.2f)&&crossed.Count>=Mathf.Max(1,Mathf.FloorToInt(population*.2f));
                if(!pass)failures++;
                File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+"population="+population+" fps="+request.fps+" seconds="+request.seconds+" moved>2m="+moved+" mostlyBlocked="+blocked+" crossedMidfield="+crossed.Count+" captures="+captures+" totalTravel="+F(travel)+" unexplainedIdle="+F(idle)+" nav="+session.Bots().WalkingRoutes.Status+" cacheHits="+BotTraversalCache.Hits+" misses="+BotTraversalCache.Misses+"\n");
                frameMilliseconds.Sort();
                if(frameMilliseconds.Count>0)File.AppendAllText(Report,"Editor frame interval mean="+frameMilliseconds.Average().ToString("F2",CultureInfo.InvariantCulture)+"ms p95="+frameMilliseconds[(int)(frameMilliseconds.Count*.95)].ToString("F2",CultureInfo.InvariantCulture)+"ms; includes rendering/editor, not isolated AI CPU.\n");
                if(request.carrier&&population==1&&captures==0){failures++;File.AppendAllText(Report,"FAIL Carrier did not complete an actual capture.\n");}
                if(stage+1<request.populations.Length)Next();else Finish();
            }
            catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");failures++;Finish();}
        }
        static string F(float n)=>n.ToString("F3",CultureInfo.InvariantCulture);
        static void Next()
        {
            if(previousObjectives!=null)BotObjectives.Current=previousObjectives;
            stage++;session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();
            var rules=ModeSimulation.Defaults(GameModeKind.CaptureFlag);rules.countdownSeconds=0;rules.roundSeconds=0;rules.automaticDisasters=false;rules.flagTarget=20;
            MatchSetup.FromLobby=true;MatchSetup.StartLocalSession=false;MatchSetup.BotTarget=request.populations[stage]+1;
            session.Modes.Configure(rules);
            session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.SurvivorBots.TargetPopulation=request.populations[stage]+1;
            for(int i=0;i<request.populations[stage];i++)if(!session.SurvivorBots.SpawnOne())throw new Exception("Spawn failed "+i);
            session.Modes.StartRound();previousObjectives=BotObjectives.Current;
            if(request.populations[stage]==1)
            {
                var bot=session.Bots().Bots[0];var flags=session.Modes.Frame.flags;
                var opposite=flags.FirstOrDefault(f=>f.team!=bot.Motor.Team);
                if(opposite!=null)
                {
                    if(request.carrier)bot.Motor.Place(opposite.home+Vector3.right*.5f,Quaternion.identity);
                    else BotObjectives.Current=new Target{destination=opposite.home};
                }
            }
            began=Time.time;nextSample=began;nextTrace=began;starts.Clear();crossed.Clear();captures=0;lastAt=0;frameMilliseconds.Clear();
            foreach(var bot in session.Bots().Bots){bot.diagnostics=true;starts[bot.GetInstanceID()]=bot.transform.position;}
        }
        static void Finish()
        {
            SessionState.SetBool(Key,false);
            if(previousObjectives!=null)BotObjectives.Current=previousObjectives;
            Time.captureFramerate=SessionState.GetInt(Key+"Capture",0);Application.targetFrameRate=SessionState.GetInt(Key+"Target",-1);Time.timeScale=SessionState.GetFloat(Key+"Scale",1);
            File.AppendAllText(Report,"Finished; failures="+failures+". No Quest or real-peer claim.\n");
            request=null;session=null;SessionState.SetBool(Key+"Restore",true);EditorApplication.ExitPlaymode();
        }
        static void Restore()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            SessionState.SetBool(Key+"Restore",false);
            var saved=JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes",""));
            if(saved?.scenes!=null)EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
            var previous=SessionState.GetString(Key+"Choices","");
            if(!string.IsNullOrEmpty(previous))SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(previous));
            MatchSetup.FromLobby=SessionState.GetBool(Key+"FromLobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
            bool same=saved?.scenes!=null&&EditorSceneManager.GetSceneManagerSetup().Select(x=>x.path).SequenceEqual(saved.scenes.Select(x=>x.path));
            File.AppendAllText(Report,(same?"PASS ":"FAIL ")+"Original scene setup restored.\n");
        }
    }
}
