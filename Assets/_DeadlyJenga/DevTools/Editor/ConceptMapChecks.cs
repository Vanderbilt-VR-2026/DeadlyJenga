using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class ConceptMapChecks
    {
        const string Key="ConceptMapChecks";
        [Serializable] sealed class SavedScenes {public SceneSetup[] values;}
        [Serializable] sealed class RunIdentity
        {
            public string kind="Actual Unity local-physics fixture; not Game motor, bots, production hazard, network or Quest evidence";
            public string project,unity,assemblyIdentity,startedUtc,completedUtc;
            public int runtimeErrors,cases,failures;
            public bool passed;
        }
        static readonly StringBuilder report=new();
        static readonly List<string> failed=new();
        static string output;
        static int errors;
        static RunIdentity identity;
        static bool executing,interrupted;
        static DateTime deadlineUtc;
        static string Project=>Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        static bool GameRun=>SessionState.GetBool(Key+"Game",false);
        static readonly string[] MapIds={"moving-shelter","peel-away-screen","collapse-created-route","pivoting-causeway","refuge-exchange","break-contact-borough"};
        enum GamePhase {Load,Ready,Settle,Observe,Late,Reset}
        static GamePhase gamePhase;
        static int gameIndex,gameLast,gameFrame,resetRevision;
        static float gameFixed,phaseUntil;
        static bool gameConfigured;
        static ConceptMechanismMap gameMap;
        static CastlePlaySession gameSession;
        static Probe gameProbe;
        static Pose[] gameStart;
        static ConceptMapChecks(){EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;Rearm();}

        [MenuItem("Tools/Deadly Jenga/Validation/Check Six Concept Maps")]
        public static void Begin(){Start(-1,false);}
        [MenuItem("Tools/Deadly Jenga/Validation/Check Peel-Away Screen")]
        public static void BeginPeelAway(){Start((int)ConceptMapKind.PeelAwayScreen,false);}
        [MenuItem("Tools/Deadly Jenga/Validation/Check Six Concept Maps In Game")]
        public static void BeginGame(){Start(-1,true);}
        [MenuItem("Tools/Deadly Jenga/Validation/Check Peel-Away Screen In Game")]
        public static void BeginGamePeelAway(){Start((int)ConceptMapKind.PeelAwayScreen,true);}
        [Serializable] sealed class Choices
        {
            public string map,mode,rules;
            public int population,hands,survivorPreset,handPreset;
            public bool sameTeam,fromLobby,local;
        }
        [Serializable] sealed class Completion
        {public bool scenarioPassed,restorationPassed,passed;public string project,completedUtc,error;}
        static bool ExplicitCommand()
        {
            if(!Application.isBatchMode)return true;
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-executeMethod");
            return i>=0&&i+1<args.Length&&new[]{"Begin","BeginPeelAway","BeginGame","BeginGamePeelAway"}.Any(name=>args[i+1]==typeof(ConceptMapChecks).FullName+"."+name);
        }
        static void Start(int selected,bool game)
        {
            if(!ExplicitCommand())throw new InvalidOperationException("Batch entry must name this explicit validation method.");
            if(SessionState.GetBool(Key,false))throw new InvalidOperationException("An explicit concept check is already pending.");
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait for an idle editor.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var scene=SceneManager.GetSceneAt(i);
                if(scene.isLoaded&&(scene.isDirty||string.IsNullOrEmpty(scene.path)&&scene.rootCount>0))
                    throw new InvalidOperationException("Unsaved scenes left untouched.");
            }
            if(!ValidationRunGate.TryAcquire(Key))throw new InvalidOperationException("Another validation owns this editor.");
            try
            {
                SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new SavedScenes{values=setup}));
                SessionState.SetString(Key+"Choices",JsonUtility.ToJson(new Choices{map=MatchChoices.MapId,mode=MatchChoices.ModeId,rules=JsonUtility.ToJson(MatchChoices.Rules),population=MatchChoices.BotTarget,hands=MatchChoices.HandCount,survivorPreset=MatchChoices.SurvivorPreset,handPreset=MatchChoices.HandPreset,sameTeam=MatchChoices.SameHumanTeam,fromLobby=MatchSetup.FromLobby,local=MatchSetup.StartLocalSession}));
                SessionState.SetInt(Key+"Selected",selected);
                SessionState.SetBool(Key+"Game",game);SessionState.SetBool(Key+"Started",false);SessionState.SetBool(Key+"Stopping",false);
                SessionState.SetString(Key+"Project",Project);
                output=Path.GetFullPath(Path.GetDirectoryName(ProjectPath.Report("ConceptMaps/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"/report.txt")));
                Directory.CreateDirectory(output);SessionState.SetString(Key+"Output",output);
                SessionState.SetString(Key+"GameScene",game?ProjectPath.Find("Game.unity"):"");
                SessionState.SetString(Key+"DeadlineUtc",DateTime.UtcNow.AddSeconds(120).ToString("O"));
                SessionState.SetBool(Key+"Passed",false);SessionState.SetBool(Key,true);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                Rearm();
                EditorApplication.EnterPlaymode();
            }
            catch
            {
                Cleanup();
                throw;
            }
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)Rearm();
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            Cleanup();
        }
        static void Rearm()
        {
            // Import alone never arms a test. Only this editor's explicit request survives its normal Play reload.
            if(!SessionState.GetBool(Key,false))return;
            if(!ExplicitCommand()||!string.Equals(Project,SessionState.GetString(Key+"Project",""),StringComparison.OrdinalIgnoreCase)||!ValidationRunGate.Owns(Key))
            {AbandonRequest();return;}
            output=SessionState.GetString(Key+"Output","");
            interrupted=SessionState.GetBool(Key+"Started",false)&&!executing;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)){EditorApplication.update-=Tick;return;}
            if(SessionState.GetBool(Key+"Stopping",false))
            {if(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling)Cleanup();return;}
            try
            {
                if(!ExplicitCommand()||!string.Equals(Project,SessionState.GetString(Key+"Project",""),StringComparison.OrdinalIgnoreCase)||!ValidationRunGate.Owns(Key))
                {AbandonRequest();return;}
                if(string.IsNullOrEmpty(output)||!Path.GetFullPath(output).StartsWith(Path.Combine(Project,"Logs","Validation","ConceptMaps")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Unexpected persisted report directory.");
                if(!DateTime.TryParse(SessionState.GetString(Key+"DeadlineUtc",""),null,System.Globalization.DateTimeStyles.RoundtripKind,out deadlineUtc)||deadlineUtc.Kind!=DateTimeKind.Utc)
                    throw new InvalidOperationException("Malformed persisted readiness deadline.");
                if(interrupted)throw new InvalidOperationException("Reload interrupted an executing probe; retained evidence, no duplicate execution.");
                if(DateTime.UtcNow>deadlineUtc)
                {
                    if(executing&&GameRun){CompleteGameCase(new TimeoutException("Bounded Game readiness/probe deadline exceeded."));return;}
                    throw new TimeoutException("Play startup/readiness exceeded 120 seconds.");
                }
                if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                if(!executing)
                {
                    executing=true;SessionState.SetBool(Key+"Started",true);
                    if(GameRun)StartGameCases();else Execute();
                }
                else if(GameRun)TickGame();
            }
            catch(Exception error)
            {
                SessionState.SetBool(Key+"Passed",false);
                if(string.Equals(Project,SessionState.GetString(Key+"Project",""),StringComparison.OrdinalIgnoreCase)&&!string.IsNullOrEmpty(output))
                    try{File.AppendAllText(Path.Combine(output,"startup-failure.txt"),"FAIL "+error+Environment.NewLine);}catch(IOException){ }
                Debug.LogException(error);Stop(false);
            }
        }
        static void AbandonRequest()
        {
            SessionState.SetBool(Key,false);SessionState.SetBool(Key+"Passed",false);EditorApplication.update-=Tick;
            // A stale request must never close or restore another owner's scene/session.
            Debug.LogError("Concept request lost explicit command, project identity or validation ownership; no scene or Play state changed.");
        }
        static void Stop(bool passed)
        {
            SessionState.SetBool(Key+"Passed",passed);SessionState.SetBool(Key+"Stopping",true);
            if(EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.ExitPlaymode();else Cleanup();
        }
        static void Cleanup()
        {
            bool passed=SessionState.GetBool(Key+"Passed",false);
            var completion=new Completion{scenarioPassed=passed,restorationPassed=true,project=Project,completedUtc=DateTime.UtcNow.ToString("O")};
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
            executing=false;
            output=SessionState.GetString(Key+"Output","");
            try
            {
                if(!string.Equals(Project,SessionState.GetString(Key+"Project",""),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Wrong project; saved scenes and choices left untouched.");
                var saved=JsonUtility.FromJson<Choices>(SessionState.GetString(Key+"Choices",""));
                if(saved!=null)
                {
                    MatchChoices.MapId=saved.map;MatchChoices.ModeId=saved.mode;MatchChoices.Rules=JsonUtility.FromJson<MatchRules>(saved.rules);
                    MatchChoices.BotTarget=saved.population;MatchChoices.HandCount=saved.hands;MatchChoices.SurvivorPreset=saved.survivorPreset;MatchChoices.HandPreset=saved.handPreset;MatchChoices.SameHumanTeam=saved.sameTeam;
                    MatchSetup.FromLobby=saved.fromLobby;MatchSetup.StartLocalSession=saved.local;
                }
                RestoreScenes();
            }
            catch(Exception error)
            {
                passed=false;completion.restorationPassed=false;completion.error=error.ToString();Debug.LogException(error);
            }
            finally
            {
                try
                {
                    bool reportOwned=string.Equals(Project,SessionState.GetString(Key+"Project",""),StringComparison.OrdinalIgnoreCase)&&!string.IsNullOrEmpty(output)&&Path.GetFullPath(output).StartsWith(Path.Combine(Project,"Logs","Validation","ConceptMaps")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
                    if(!reportOwned)throw new InvalidOperationException("No owned completion-report path; no successful completion claimed.");
                    if(!passed)
                        foreach(var name in new[]{"identity.json","game-identity.json"})
                        {
                            var path=Path.Combine(output,name);if(!File.Exists(path))continue;
                            var prior=JsonUtility.FromJson<RunIdentity>(File.ReadAllText(path));
                            if(prior!=null){prior.passed=false;prior.failures++;File.WriteAllText(path,JsonUtility.ToJson(prior,true));}
                        }
                    completion.passed=passed;File.WriteAllText(Path.Combine(output,"completion.json"),JsonUtility.ToJson(completion,true));
                }
                catch(Exception error){passed=false;Debug.LogException(error);}
                SessionState.SetBool(Key+"Passed",passed);ValidationRunGate.Release(Key);
                if(Application.isBatchMode&&ExplicitCommand())EditorApplication.Exit(passed?0:2);
            }
        }
        static void RestoreScenes()
        {
            var text=SessionState.GetString(Key+"Scenes","");
            var saved=string.IsNullOrEmpty(text)?null:JsonUtility.FromJson<SavedScenes>(text);
            if(saved?.values==null||saved.values.Length==0)return;
            var files=saved.values.Where(s=>!string.IsNullOrEmpty(s.path)).ToArray();
            if(files.Length>0)EditorSceneManager.RestoreSceneManagerSetup(files);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            foreach(var item in saved.values.Where(s=>string.IsNullOrEmpty(s.path)))
            {
                if(files.Length==0)break;
                var empty=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                if(item.isActive)SceneManager.SetActiveScene(empty);
            }
        }
        static void Execute()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;
            errors=0;report.Clear();failed.Clear();identity=null;
            bool subscribed=false,written=false,passed=false;
            var previousWorld=Services.World;
            bool previousCollision=Physics.GetIgnoreLayerCollision(Layers.Survivor,Layers.Block);
            try
            {
                if(Services.Session!=null)throw new InvalidOperationException("Use the explicit empty-scene entry point; live session left untouched.");
                identity=new RunIdentity{project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")),unity=Application.unityVersion,
                    assemblyIdentity=typeof(ConceptMechanismMap).Assembly.ManifestModule.ModuleVersionId.ToString(),startedUtc=DateTime.UtcNow.ToString("O")};
                report.AppendLine(identity.kind);report.AppendLine("Project="+identity.project+"; Unity="+identity.unity+"; assembly MVID="+identity.assemblyIdentity);
                report.AppendLine("An external sealed source manifest must accompany this report; MVID is loaded-assembly identity, not a Git revision.");
                Application.logMessageReceived+=Log;subscribed=true;
                int selected=SessionState.GetInt(Key+"Selected",-1);
                foreach(ConceptMapKind kind in Enum.GetValues(typeof(ConceptMapKind)))
                {
                    if(selected>=0&&(int)kind!=selected)continue;
                    identity.cases++;
                    try{RunCase(kind);}
                    catch(Exception error){failed.Add(kind+": "+error.Message);report.AppendLine("CASE FAIL "+kind+" "+error);}
                }
                Check(identity.cases==(selected>=0?1:6),"Every explicitly selected native concept case executed");
                Check(errors==0,"No Unity error/assert/exception during fixtures");
                Check(DateTime.UtcNow<=deadlineUtc,"Native fixture completed inside its bounded deadline");
                passed=failed.Count==0;
            }
            catch(Exception error){failed.Add(error.Message);report.AppendLine("FAIL "+error);}
            finally
            {
                if(subscribed)Application.logMessageReceived-=Log;
                Services.World=previousWorld;
                Physics.IgnoreLayerCollision(Layers.Survivor,Layers.Block,previousCollision);
                try
                {
                    report.AppendLine("COMPLETE passed="+passed+" cases="+(identity?.cases??0)+" runtimeErrors="+errors+" caseFailures="+failed.Count);
                    report.AppendLine("PENDING: real Game.unity solo per map, actual survivor/carrier traversal, bots/objectives, production hazard calibration, independent peers, Quest comfort/performance.");
                    report.AppendLine("Images are empty-scene fixture captures with synthetic cameras, not screenshots of production Game or a headset.");
                    if(output!=null)
                    {
                        File.WriteAllText(Path.Combine(output,"report.txt"),report.ToString());
                        if(identity==null)identity=new RunIdentity{kind="Native fixture startup failed; no completed physics case",project=Project};
                        identity.passed=passed;identity.runtimeErrors=errors;identity.failures=failed.Count;identity.completedUtc=DateTime.UtcNow.ToString("O");
                        File.WriteAllText(Path.Combine(output,"identity.json"),JsonUtility.ToJson(identity,true));written=true;
                        Debug.Log("Concept maps fixture report: "+output);
                    }
                }
                catch(Exception error){Debug.LogException(error);}
                finally{Stop(passed&&written);}
            }
        }
        static void Log(string text,string trace,LogType kind)
        {
            if(kind!=LogType.Error&&kind!=LogType.Exception&&kind!=LogType.Assert)return;
            errors++;Note("UNITY ERROR "+text);
        }
        static void Note(string text)
        {
            report.AppendLine(text);
            if(GameRun&&!string.IsNullOrEmpty(output))File.AppendAllText(Path.Combine(output,"game-report.txt"),text+Environment.NewLine);
        }
        static void Check(bool ok,string text)
        {
            Note((ok?"PASS ":"FAIL ")+text);if(!ok)throw new InvalidOperationException(text);
        }
        static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
        static bool Finite(Vector3 p)=>Finite(p.x)&&Finite(p.y)&&Finite(p.z);
        static void Step(PhysicsScene physics,int steps){for(int i=0;i<steps;i++)physics.Simulate(.02f);Physics.SyncTransforms();}
        static Rigidbody Find(ConceptMechanismMap map,string prefix)
        {
            foreach(var m in map.Authored)if(m.purpose.StartsWith(prefix,StringComparison.Ordinal)&&map.TryGetBody(m.purpose,out var body))return body;
            throw new InvalidOperationException("Missing transformation body prefix "+prefix);
        }
        static void Deadline(int seconds)=>SessionState.SetString(Key+"DeadlineUtc",DateTime.UtcNow.AddSeconds(seconds).ToString("O"));
        static void StartGameCases()
        {
            errors=0;report.Clear();failed.Clear();gameConfigured=false;
            int selected=SessionState.GetInt(Key+"Selected",-1);gameIndex=selected>=0?selected:0;gameLast=selected>=0?selected:5;
            identity=new RunIdentity{kind="Actual Game scene initialization and live-physics mechanism probes; fixture pushes, not route traversal, production hazards, peer or Quest acceptance",project=Project,unity=Application.unityVersion,assemblyIdentity=typeof(ConceptMechanismMap).Assembly.ManifestModule.ModuleVersionId.ToString(),startedUtc=DateTime.UtcNow.ToString("O")};
            File.WriteAllText(Path.Combine(output,"game-report.txt"),identity.kind+Environment.NewLine);
            Note("Project="+Project+"; loaded assemblyMVID="+identity.assemblyIdentity+"; pair with exact sealed source manifest.");
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;gamePhase=GamePhase.Load;Deadline(60);
        }
        static void TickGame()
        {
            try
            {
                if(gamePhase==GamePhase.Load){LoadGameCase();return;}
                if(gamePhase==GamePhase.Ready){WaitGameReady();return;}
                if(Time.realtimeSinceStartup<phaseUntil)return;
                Check(ReferenceEquals(Services.Session,gameSession)&&ReferenceEquals(Services.World,gameMap),"Actual session/map identity remains current");
                var physics=gameMap.gameObject.scene.GetPhysicsScene();
                if(gamePhase==GamePhase.Settle)
                {
                    CheckSettledGame();Fallback(gameMap,physics,(ConceptMapKind)gameIndex);
                    CaptureGameCamera("intact");gameProbe=BeginProbe(gameMap,physics,(ConceptMapKind)gameIndex);
                    gameFrame=Time.frameCount;gameFixed=Time.fixedTime;gamePhase=GamePhase.Observe;phaseUntil=Time.realtimeSinceStartup+2;return;
                }
                if(gamePhase==GamePhase.Observe)
                {
                    Check(Time.frameCount>gameFrame+3&&Time.fixedTime>gameFixed+.05f,"Game frames and fixed physics advance after actual probe input");
                    ValidateProbe(gameMap,physics,gameProbe);CaptureGameCamera("after-probe");gamePhase=GamePhase.Late;phaseUntil=Time.realtimeSinceStartup+4;return;
                }
                if(gamePhase==GamePhase.Late)
                {
                    Fallback(gameMap,physics,(ConceptMapKind)gameIndex);CheckActorSupport();
                    resetRevision=gameMap.Revision;gameSession.ResetMap();gamePhase=GamePhase.Reset;phaseUntil=Time.realtimeSinceStartup+1;return;
                }
                Check(gameMap.Revision>resetRevision&&gameMap.Blocks.Count==ConceptMapLayout.Build((ConceptMapKind)gameIndex).Count&&gameMap.Blocks.All(body=>body&&body.gameObject.activeInHierarchy),"Normal session reset rebuilds all authored bodies");
                CheckActorSupport();CompleteGameCase(null);
            }
            catch(Exception error){CompleteGameCase(error);}
        }
        static void LoadGameCase()
        {
            identity.cases++;gameMap=null;gameSession=null;gameConfigured=false;
            string id="sparse-prototype-"+MapIds[gameIndex];Note("CASE BEGIN "+id);
            MatchChoices.MapId=id;MatchChoices.ModeId="demo";MatchChoices.BotTarget=1;MatchChoices.HandCount=0;
            MatchSetup.FromLobby=true;MatchSetup.StartLocalSession=false;
            var entry=MatchSetup.SelectedMap;
            Check(entry!=null&&entry.mapPrefab,"Selected catalog entry and imported prefab resolve for "+id);
            var authored=entry.mapPrefab.GetComponent<ConceptMechanismMap>();
            Check(authored&&authored.kind==(ConceptMapKind)gameIndex,"Imported prefab matches expected concept kind");
            Check(entry.walkableIsland&&entry.supportedModes!=null&&entry.supportedModes.SequenceEqual(new[]{"demo"}),"Catalog entry is explicitly Demo-only with walkable island");
            string scene=SessionState.GetString(Key+"GameScene","");
            Check(entry.scenePath==scene&&SceneUtility.GetBuildIndexByScenePath(scene)>=0,"Catalog resolves actual enabled Game scene");
            gameFrame=Time.frameCount;gameFixed=Time.fixedTime;gamePhase=GamePhase.Ready;Deadline(60);
            // Choices are applied after Play's subsystem reset, before the real Game scene starts.
            SceneManager.LoadScene(SceneUtility.GetBuildIndexByScenePath(scene));
        }
        static void WaitGameReady()
        {
            var session=Services.Session as CastlePlaySession;var map=Services.World as ConceptMechanismMap;
            if(!session||!map||session.Player==null||session.Hand==null||!session.Modes||session.Demo==null||session.Social==null||session.SurvivorBots==null||session.HandBots==null||!session.ActiveCamera)return;
            if(!gameConfigured)
            {
            var scene=SceneManager.GetActiveScene();
            Check(scene.path==SessionState.GetString(Key+"GameScene","")&&map.gameObject.scene==scene&&session.gameObject.scene==scene,"Real Game scene owns current session and selected map");
            Check(map.kind==(ConceptMapKind)gameIndex&&MatchSetup.MapId=="sparse-prototype-"+MapIds[gameIndex],"Services.World is exact selected concept, not legacy or local fixture");
            Check(map.Blocks.Count==ConceptMapLayout.Build(map.kind).Count,"Actual Game body census matches authored layout");
            Check(session.HasWorldAuthority&&(!session.Network||!session.Network.WaitingForWorld),"Solo world authority and synchronized-world readiness");
            Check(session.Demo.DemoMode&&session.Demo.CanCommand,"Actual session permits the normal Demo commands used by this probe");
            Check(!session.IsVR,"This fixture is desktop-only; no headset state substituted");
            Check(scene.GetPhysicsScene().Equals(Physics.defaultPhysicsScene)&&Physics.simulationMode==SimulationMode.FixedUpdate,"Actual Game uses normally advancing default fixed physics");
            Check(Services.World is ITraversalCollisionWorld collision&&collision.UsesOriginalColliders&&!Physics.GetIgnoreLayerCollision(Layers.Survivor,Layers.Block),"Actual traversal collision policy is enabled");
            Check(session.Modes.Rules.mode==GameModeKind.Demo&&session.Modes.Rules.island==IslandRule.Walkable,"Actual mode rules match Demo and walkable map");
                var probeRules=session.Modes.Rules.Copy();probeRules.automaticDisasters=false;session.Modes.Configure(probeRules);
                Note("Explicit fixture configuration: copied actual Demo/Walkable rules with automaticDisasters=false; not a validated production hazard profile.");
                session.SurvivorBots.AutoFill=false;session.Demo.Run(DemoCommand.ClearSurvivors);session.Demo.Run(DemoCommand.ClearHands);session.Demo.Run(DemoCommand.StopDisasters);session.Social.VoiceEnabled=false;
                Check(!session.Modes.Rules.automaticDisasters,"No automatic hazard profile is active");
                session.SetRole(false);session.SetMenu(true);gameSession=session;gameMap=map;gameConfigured=true;
            }
            if(!ReferenceEquals(gameSession,session)||!ReferenceEquals(gameMap,map))throw new InvalidOperationException("Session/world changed during actor readiness.");
            var player=session.Survivor();
            if(!player||!player.Capsule||!player.Capsule.enabled||!player.Grounded||Time.frameCount<gameFrame+8||Time.fixedTime<=gameFixed+.08f)return;
            Check(session.SurvivorBots.Count==0&&session.HandBots.Count==0,"One local survivor slot and zero AI bots/hands realized");
            CheckActorSupport();
            gameStart=map.Authored.Select(m=>new Pose(map.transform.TransformPoint(m.position),map.transform.rotation*m.rotation*m.shapes[0].rotation)).ToArray();
            gamePhase=GamePhase.Settle;phaseUntil=Time.realtimeSinceStartup+4;Deadline(25);
            Note("GAME READY "+map.kind+"; frames="+(Time.frameCount-gameFrame)+"; fixedSeconds="+(Time.fixedTime-gameFixed)+"; actor="+player.GetInstanceID()+"; actualCamera="+session.ActiveCamera.GetInstanceID());
        }
        static void CheckActorSupport()
        {
            var player=gameSession?gameSession.Survivor():null;
            Check(player&&player.Capsule&&player.Capsule.enabled&&player.Grounded&&Finite(player.transform.position),"Real survivor motor is enabled, finite and grounded");
            Check(gameMap.gameObject.scene.GetPhysicsScene().Raycast(player.FootPosition+Vector3.up*.2f,Vector3.down,out var hit,1,(1<<Layers.Block)|1,QueryTriggerInteraction.Ignore)&&hit.normal.y>.5f,"Real survivor feet have physical upward support");
        }
        static void CheckSettledGame()
        {
            for(int i=0;i<gameMap.Blocks.Count;i++)
            {
                var body=gameMap.Blocks[i];Check(body&&body.gameObject.activeInHierarchy&&Finite(body.position)&&Finite(body.linearVelocity)&&Finite(body.angularVelocity),"Game intact body finite/active "+i);
                Check(Vector3.Distance(gameStart[i].position,body.position)<.25f&&Quaternion.Angle(gameStart[i].rotation,body.rotation)<5&&body.linearVelocity.magnitude<.2f&&body.angularVelocity.magnitude<.2f,"Game intact body settles near authored pose "+i);
            }
        }
        static void CaptureGameCamera(string phase)
        {
            var camera=gameSession.ActiveCamera;Check(camera&&camera.enabled,"Actual survivor camera available for "+phase);
            bool menu=gameSession.MenuOpen,suspended=gameSession.Player.InputSuspended;gameSession.SetMenu(false);gameSession.Player.InputSuspended=true;
            try{SaveCamera(camera,"Game-"+(ConceptMapKind)gameIndex+"-"+phase+"-actual-camera");}
            finally{gameSession.SetMenu(menu);gameSession.Player.InputSuspended=suspended;}
        }
        static void CompleteGameCase(Exception error)
        {
            if(error!=null){failed.Add(((ConceptMapKind)gameIndex)+": "+error.Message);Note("CASE FAIL "+(ConceptMapKind)gameIndex+" "+error);}
            else Note("CASE PASS "+(ConceptMapKind)gameIndex);
            if(gameIndex<gameLast){gameIndex++;gamePhase=GamePhase.Load;Deadline(60);return;}
            Application.logMessageReceived-=Log;
            int expected=SessionState.GetInt(Key+"Selected",-1)>=0?1:6;
            bool passed=failed.Count==0&&errors==0&&identity.cases==expected;
            identity.passed=passed;identity.runtimeErrors=errors;identity.failures=failed.Count;identity.completedUtc=DateTime.UtcNow.ToString("O");
            Note("COMPLETE Game cases="+identity.cases+" expected="+expected+" failures="+failed.Count+" runtimeErrors="+errors+" passed="+passed);
            File.WriteAllText(Path.Combine(output,"game-identity.json"),JsonUtility.ToJson(identity,true));Stop(passed);
        }
        static void RunCase(ConceptMapKind kind)
        {
            var previousScene=SceneManager.GetActiveScene();var previousWorld=Services.World;
            bool previousCollision=Physics.GetIgnoreLayerCollision(Layers.Survivor,Layers.Block);
            var scene=SceneManager.CreateScene("Concept fixture "+kind,new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            GameObject root=null;
            try
            {
                SceneManager.SetActiveScene(scene);var physics=scene.GetPhysicsScene();
                Check(physics.IsValid()&&!physics.Equals(Physics.defaultPhysicsScene),kind+" has isolated native physics");
                root=new GameObject("Concept map fixture "+kind);var map=root.AddComponent<ConceptMechanismMap>();
                map.generateOnStart=false;map.kind=kind;map.Rebuild();
                report.AppendLine("CENSUS kind="+kind+" bodies="+map.Blocks.Count+" shapes="+map.Authored.Sum(m=>m.shapes.Count)+" bounds="+map.PlayableBounds);
                Check(map.Blocks.Count>0&&map.Blocks.Count<=20&&map.Blocks.Count==map.Authored.Count,kind+" bounded body census matches authored units");
                var b=map.PlayableBounds;
                Check(Finite(b.center)&&Finite(b.size)&&b.size.x>0&&b.size.y>0&&b.size.z>0,kind+" finite positive world-query bounds");
                var copy=ConceptMapLayout.Build(kind);
                Check(copy.Count==map.Authored.Count&&copy.Select((m,i)=>m.purpose==map.Authored[i].purpose&&m.position==map.Authored[i].position&&m.rotation==map.Authored[i].rotation&&m.shapes.Count==map.Authored[i].shapes.Count).All(v=>v),kind+" deterministic module order and poses");
                Check(copy.All(m=>m.shapes.Count>0&&m.shapes.All(s=>Finite(s.center)&&Finite(s.size)&&s.size.x>0&&s.size.y>0&&s.size.z>0)),kind+" finite positive authored shapes");
                string saved=map.SaveLayout();int revision=map.Revision,count=map.Blocks.Count;
                Check(!map.LoadLayout(saved,count+1,new Pose(Vector3.zero,Quaternion.identity),Vector3.one),kind+" rejects mismatched body census");
                Check(map.Revision==revision&&map.SaveLayout()==saved&&map.Blocks.Count==count,kind+" rejection does not mutate map");
                Check(!map.LoadLayout(saved,count,new Pose(Vector3.zero,Quaternion.identity),new Vector3(1,2,1)),kind+" rejects nonuniform replica scale");
                Check(map.Revision==revision&&map.SaveLayout()==saved,kind+" invalid scale leaves layout untouched");
                Check(!map.LoadLayout(saved,count,new Pose(Vector3.zero,Quaternion.identity),Vector3.one*2),kind+" rejects unsupported uniform scale without mutation");
                Check(map.Revision==revision&&map.SaveLayout()==saved,kind+" unsupported uniform scale leaves layout untouched");
                var shifted=new Pose(new Vector3(2,1,-3),Quaternion.Euler(0,35,0));
                Check(map.LoadLayout(saved,count,shifted,Vector3.one),kind+" supports translated yaw-rotated layout origin");
                Check(Vector3.Distance(map.Root.position,shifted.position)<.001f&&Quaternion.Angle(map.Root.rotation,shifted.rotation)<.01f,kind+" applies accepted layout origin");
                Check(map.LoadLayout(saved,count,new Pose(Vector3.zero,Quaternion.identity),Vector3.one),kind+" accepts its own serialized layout");
                map.SetReplica(true);Check(map.Blocks.All(rb=>rb.isKinematic),kind+" replica suspends all bodies");
                map.SetReplica(false);
                for(int i=0;i<map.Authored.Count;i++)Check(map.Blocks[i].isKinematic==map.Authored[i].purpose.StartsWith("Fixed ",StringComparison.Ordinal),kind+" authority restores authored anchoring "+map.Authored[i].purpose);
                Physics.SyncTransforms();
                var start=map.Blocks.Select(rb=>new Pose(rb.position,rb.rotation)).ToArray();
                Step(physics,200);
                float maxDrift=0,maxTilt=0,maxSpeed=0,maxSpin=0;
                for(int i=0;i<map.Blocks.Count;i++)
                {
                    var rb=map.Blocks[i];Check(Finite(rb.position)&&Finite(rb.linearVelocity)&&Finite(rb.angularVelocity),kind+" finite native body state "+i);
                    maxDrift=Mathf.Max(maxDrift,Vector3.Distance(start[i].position,rb.position));maxTilt=Mathf.Max(maxTilt,Quaternion.Angle(start[i].rotation,rb.rotation));
                    maxSpeed=Mathf.Max(maxSpeed,rb.linearVelocity.magnitude);maxSpin=Mathf.Max(maxSpin,rb.angularVelocity.magnitude);
                }
                report.AppendLine($"SETTLE kind={kind} seconds=4 maxDrift={maxDrift:F4}m maxTilt={maxTilt:F3}deg maxSpeed={maxSpeed:F4}m/s maxSpin={maxSpin:F4}rad/s");
                Check(maxDrift<.25f&&maxTilt<5&&maxSpeed<.2f&&maxSpin<.2f,kind+" intact native structure settles within declared tolerances");
                var spawn=map.SafeSpawnPoint();var hits=new Collider[32];
                Check(Finite(spawn),kind+" finite supported spawn");
                Check(physics.OverlapCapsule(spawn+Vector3.up*.2f,spawn+Vector3.up*.8f,.18f,hits,~0,QueryTriggerInteraction.Ignore)==0,kind+" fixture capsule clears spawn");
                Check(physics.Raycast(spawn+Vector3.up*.1f,Vector3.down,out var support,.5f,~0,QueryTriggerInteraction.Ignore)&&support.normal.y>.8f,kind+" spawn has nearby upward support");
                Fallback(map,physics,kind);
                Capture(root,map,kind+"-intact-overview",false);
                Capture(root,map,kind+"-intact-fixture-eye",true);
                ApplyTransformation(map,physics,kind);
                Capture(root,map,kind+"-after-probe-overview",false);
                Step(physics,200);Fallback(map,physics,kind);Capture(root,map,kind+"-late-plus4s-overview",false);
            }
            finally
            {
                if(root)Object.DestroyImmediate(root);
                Services.World=previousWorld;Physics.IgnoreLayerCollision(Layers.Survivor,Layers.Block,previousCollision);
                if(previousScene.IsValid())SceneManager.SetActiveScene(previousScene);
                if(scene.IsValid())SceneManager.UnloadSceneAsync(scene);
            }
        }
        static void Fallback(ConceptMechanismMap map,PhysicsScene physics,ConceptMapKind kind)
        {
            var size=ConceptMapLayout.FoundationSize(kind);float z=size.y*.5f-.65f;
            var hits=new Collider[16];
            for(float x=-size.x*.5f+1;x<=size.x*.5f-1;x+=2)
            {
                Vector3 p=map.transform.TransformPoint(new Vector3(x,.45f,z));
                Check(physics.OverlapCapsule(p+Vector3.up*.2f,p+Vector3.up*.8f,.18f,hits,1<<Layers.Block,QueryTriggerInteraction.Ignore)==0,kind+" outer ground strip fixture clearance x="+x);
                Check(physics.Raycast(p+Vector3.up*.1f,Vector3.down,out var hit,.4f,~0,QueryTriggerInteraction.Ignore)&&hit.normal.y>.8f,kind+" outer ground strip support x="+x);
            }
            report.AppendLine("CLEARANCE discrete capsule/support samples on authored outer ground strip only; not continuous motor navigation.");
        }
        sealed class Probe
        {
            public ConceptMapKind kind;
            public Rigidbody body;
            public Vector3 position,anchor;
            public Quaternion rotation;
        }
        static void ApplyTransformation(ConceptMechanismMap map,PhysicsScene physics,ConceptMapKind kind)
        {
            var probe=BeginProbe(map,physics,kind);Step(physics,100);ValidateProbe(map,physics,probe);
        }
        static Probe BeginProbe(ConceptMechanismMap map,PhysicsScene physics,ConceptMapKind kind)
        {
            var probe=new Probe{kind=kind};
            if(kind==ConceptMapKind.RefugeExchange)
            {
                Check(map.IsSheltered(map.transform.TransformPoint(new Vector3(-6,.65f,0))),"Low refuge has actual upward block occlusion");
                Check(!map.IsSheltered(map.transform.TransformPoint(new Vector3(6,4.65f,0))),"High refuge is open overhead");
                return probe;
            }
            if(kind==ConceptMapKind.PeelAwayScreen)
            {
                Vector3 eye=map.transform.TransformPoint(new Vector3(0,1.4f,-7));
                Check(physics.Raycast(eye,map.transform.forward,out _,14,1<<Layers.Block,QueryTriggerInteraction.Ignore),"Standing screens occlude the central sightline");
                foreach(var name in new[]{"Screen west","Screen east"})
                {
                    Check(map.TryGetBody(name,out var screen),"Expected screen "+name);
                    Check(screen.GetComponent<HingeJoint>(),name+" has a ground-edge peel hinge");
                    Check(screen.GetComponent<IPushable>()!=null,name+" exposes actual push ability");
                    screen.GetComponent<IPushable>().Push(map.transform.right*(name.EndsWith("west",StringComparison.Ordinal)?-12:12),map.gameObject);
                }
                Note("SCREEN explicit fixture inputs: two outward12m/s ability pushes; no player-input claim.");return probe;
            }
            var prefix=kind==ConceptMapKind.CollapseCreatedRoute?"Falling route":kind==ConceptMapKind.MovingShelter?"Moving roof":kind==ConceptMapKind.PivotingCauseway?"Pivot ":"Connector ";
            var body=Find(map,prefix);probe.body=body;probe.position=body.position;probe.rotation=body.rotation;
            if(kind==ConceptMapKind.CollapseCreatedRoute)
            {Find(map,"Removable support").gameObject.SetActive(false);Physics.SyncTransforms();Note("SUPPORT deactivated by fixture, not a hazard or player command.");return probe;}
            Vector3 velocity=map.transform.right*3;
            if(kind==ConceptMapKind.MovingShelter)
                Check(map.IsSheltered(map.transform.TransformPoint(new Vector3(1.5f,.65f,0))),"Moving canopy initially covers transfer probe");
            if(kind==ConceptMapKind.PivotingCauseway)
            {
                var joint=body.GetComponent<HingeJoint>();Check(joint&&joint.connectedBody,"Causeway has connected hinge");
                Vector3 axis=body.transform.TransformDirection(joint.axis).normalized;
                Check(Vector3.Dot(axis,map.transform.up)>.999f,"Causeway hinge rotates about map-up");
                probe.anchor=joint.connectedBody.transform.TransformPoint(joint.connectedAnchor);
                var radius=Vector3.ProjectOnPlane(body.worldCenterOfMass-body.transform.TransformPoint(joint.anchor),axis);
                Check(radius.sqrMagnitude>.01f,"Hinge has nonzero torque arm");velocity=Vector3.Cross(axis,radius).normalized*2;
            }
            if(kind==ConceptMapKind.BreakContactBorough)
            {
                velocity=map.transform.forward*8;
                foreach(var name in new[]{"Fuel pod west","Fuel pod middle"})
                {
                    Check(map.TryGetBody(name,out var pod),"Expected adjacent "+name);
                    Check(ContactGap(body.GetComponent<Collider>(),pod.GetComponent<Collider>())<=.3f,"Connector initially contacts "+name+" under Fire threshold");
                }
            }
            Check(body.GetComponent<IPushable>()!=null,prefix+" exposes actual push ability");
            body.GetComponent<IPushable>().Push(velocity,map.gameObject);Note("PUSH explicit fixture velocity="+velocity);return probe;
        }
        static float ContactGap(Collider source,Collider target)
        {
            Check(source&&target,"Actual root contact colliders exist");
            var a=source.ClosestPoint(target.bounds.center);return Vector3.Distance(a,target.ClosestPoint(a));
        }
        static void ValidateProbe(ConceptMechanismMap map,PhysicsScene physics,Probe probe)
        {
            var kind=probe.kind;
            if(kind==ConceptMapKind.RefugeExchange)
            {
                Check(map.IsSheltered(map.transform.TransformPoint(new Vector3(-6,.65f,0))),"Low refuge remains sheltered");
                Check(!map.IsSheltered(map.transform.TransformPoint(new Vector3(6,4.65f,0))),"High refuge remains open");
                Check(map.TryGetBody("Fixed broad high-refuge ramp south",out var south)&&south.gameObject.activeInHierarchy&&map.TryGetBody("Fixed broad high-refuge ramp north",out var north)&&north.gameObject.activeInHierarchy,"Both broad refuge ramps exist in actual scene");
                Note("REFUGE geometry only; no production Rain/Wave or capacity acceptance.");return;
            }
            if(kind==ConceptMapKind.PeelAwayScreen)
            {
                var eye=map.transform.TransformPoint(new Vector3(0,1.4f,-7));
                Check(!physics.Raycast(eye,map.transform.forward,out _,14,1<<Layers.Block,QueryTriggerInteraction.Ignore),"Peeled screens expose central sightline");
                var contacts=new Collider[16];
                for(int z=-7;z<=7;z++)
                {var p=map.transform.TransformPoint(new Vector3(0,.46f,z));Check(physics.OverlapCapsule(p+Vector3.up*.2f,p+Vector3.up*.8f,.18f,contacts,1<<Layers.Block,QueryTriggerInteraction.Ignore)==0,"Screen corridor sampled capsule clearance z="+z);}
                foreach(var name in new[]{"Screen west","Screen east"})
                {map.TryGetBody(name,out var screen);Check(Mathf.Abs(screen.GetComponent<HingeJoint>().angle)>20,name+" physically peels about hinge");}
                return;
            }
            var body=probe.body;Check(body&&Finite(body.position)&&Finite(body.linearVelocity),kind+" finite physical response");
            float moved=Vector3.Distance(probe.position,body.position),turned=Quaternion.Angle(probe.rotation,body.rotation);
            Note($"RESPONSE {kind} displacement={moved:F4}m rotation={turned:F3}deg");
            if(kind==ConceptMapKind.CollapseCreatedRoute)
            {Check(moved>.2f,"Support removal moves falling plate");Note("Created ascent remains UNPROVEN without actual motor traversal; fixed bypass is checked separately.");return;}
            Check(moved>.08f||turned>3,kind+" responds to actual ability push");
            if(kind==ConceptMapKind.MovingShelter)
            {
                Check(map.IsSheltered(map.transform.TransformPoint(new Vector3(-6,.65f,0))),"Fixed refuge retains shelter");
                Check(!map.IsSheltered(map.transform.TransformPoint(new Vector3(1.5f,.65f,0))),"Shifted canopy exposes former probe");
                Check(Mathf.Abs(map.transform.InverseTransformPoint(body.position).x-4)<=2.65f,"Roof remains within slider travel tolerance");
            }
            if(kind==ConceptMapKind.PivotingCauseway)
            {
                Check(turned>3,"Causeway push produces yaw");var joint=body.GetComponent<HingeJoint>();
                Check(Vector3.Distance(body.transform.TransformPoint(joint.anchor),probe.anchor)<.12f,"Causeway stays attached to fixed pivot");
                Check(map.TryGetBody("Fixed outer route bridge",out var bypass)&&bypass.gameObject.activeInHierarchy&&bypass.isKinematic,"Independent raised causeway bypass remains fixed");
            }
            if(kind==ConceptMapKind.BreakContactBorough)
            {
                var connector=body.GetComponent<Collider>();
                foreach(var name in new[]{"Fuel pod west","Fuel pod middle"})
                {
                    map.TryGetBody(name,out var pod);var floor=pod.GetComponent<Collider>();
                    float forward=ContactGap(connector,floor),reverse=ContactGap(floor,connector);
                    Note("FIREBREAK "+name+" source/target gaps="+forward+" / "+reverse);
                    Check(forward>.3f&&reverse>.3f,"Both directed Fire contact tests are broken for "+name);
                }
                Note("Contact geometry only; production Fire seeding/spread and multiple ignitions remain untested.");
            }
        }
        static void Capture(GameObject root,ConceptMechanismMap map,string name,bool eye)
        {
            var cameraObject=new GameObject("Explicit fixture camera");cameraObject.transform.SetParent(root.transform,false);
            var lightObject=new GameObject("Explicit fixture light");lightObject.transform.SetParent(root.transform,false);
            var camera=cameraObject.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.11f,.17f,.22f);camera.fieldOfView=58;camera.nearClipPlane=.05f;
            var bounds=map.PlayableBounds;
            if(eye)
            {
                camera.transform.position=map.SafeSpawnPoint()+Vector3.up*.8f;
                camera.transform.LookAt(bounds.center+Vector3.up*.5f);
            }
            else
            {
                float radius=Mathf.Max(bounds.extents.x,bounds.extents.z);
                camera.transform.position=bounds.center+new Vector3(radius*1.35f,radius*1.1f,-radius*1.6f);camera.transform.LookAt(bounds.center);
            }
            lightObject.transform.rotation=Quaternion.Euler(45,-35,0);var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;
            try{SaveCamera(camera,name);Note(eye?"Synthetic fixture eye=.8m above selected spawn; not a real actor":"Synthetic fixture overview camera and light");}
            finally{Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(lightObject);}
        }
        static void SaveCamera(Camera camera,string name)
        {
            var target=new RenderTexture(1200,800,24);var image=new Texture2D(1200,800,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            var previousTarget=camera.targetTexture;
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1200,800),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
                Note("CAPTURE "+name+" camera="+camera.transform.position+" rotation="+camera.transform.rotation+" FOV="+camera.fieldOfView+" dimensions=1200x800");
            }
            finally
            {
                camera.targetTexture=previousTarget;RenderTexture.active=previous;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);
            }
        }
    }
}
