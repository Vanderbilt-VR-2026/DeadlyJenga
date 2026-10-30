using System;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class ChristmasTownChecks
    {
        const string Key="ChristmasTownChecks";
        static CastlePlaySession session;
        static ChristmasTownMap map;
        static float started;
        static double boot;
        static bool configured;
        static readonly System.Collections.Generic.Dictionary<int,Vector3> lastPositions=new System.Collections.Generic.Dictionary<int,Vector3>();
        static readonly System.Collections.Generic.Dictionary<int,float> distances=new System.Collections.Generic.Dictionary<int,float>();
        static int errorCount;
        static readonly System.Collections.Generic.List<string> errors=new System.Collections.Generic.List<string>();
        [Serializable] sealed class SavedScenes { public SceneSetup[] scenes; }
        static string Report=>ProjectPath.Report("ChristmasTown-runtime.txt");
        static ChristmasTownChecks()
        {
            EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key+"Restore",false))return;
                SessionState.SetBool(Key+"Restore",false);
                var saved=JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key+"Scenes",""));if(saved?.scenes!=null&&saved.scenes.Length>0)EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
                var plan=SessionState.GetString(Key+"Choices","");if(!string.IsNullOrEmpty(plan))SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(plan));
                MatchSetup.FromLobby=SessionState.GetBool(Key+"FromLobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
                if(Application.isBatchMode)EditorApplication.Exit(SessionState.GetInt(Key+"Exit",1));
            };
        }
        static void Log(string message,string stack,LogType kind)
        {if(SessionState.GetBool(Key,false)&&(kind==LogType.Exception||kind==LogType.Error&&message.Contains("Bot objective contract failed"))){errorCount++;errors.Add(message);}}
        [MenuItem("Tools/Deadly Jenga/Validation/Check Christmas Town Runtime")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            if(!Application.isBatchMode&&(EditorSceneManager.GetSceneManagerSetup().Any(s=>string.IsNullOrEmpty(s.path))||Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)))throw new InvalidOperationException("Save edited scenes before the check.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new SavedScenes{scenes=EditorSceneManager.GetSceneManagerSetup().Where(s=>!string.IsNullOrEmpty(s.path)).ToArray()}));
            SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"FromLobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);
            SessionState.SetBool(Key,true);File.WriteAllText(Report,"Actual Game scene, Christmas town, 20 bots, CTF. Desktop only.\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            if(boot==0)boot=EditorApplication.timeSinceStartup;
            try
            {
                if(!configured)
                {
                    if(EditorApplication.timeSinceStartup-boot<3)return;
                    session=UnityEngine.Object.FindFirstObjectByType<CastlePlaySession>();
                    if(!session||session.SurvivorBots==null){if(EditorApplication.timeSinceStartup-boot>45)throw new Exception("Game did not initialize.");return;}
                    if(session.Social!=null)session.Social.VoiceEnabled=false;
                    var prefab=Resources.Load<GameObject>("ModeMaps/Mode_christmas-town");if(!prefab)throw new Exception("Town prefab missing.");
                    if(Services.World is Component old){old.gameObject.SetActive(false);UnityEngine.Object.Destroy(old.gameObject);}
                    map=UnityEngine.Object.Instantiate(prefab).GetComponent<ChristmasTownMap>();map.Rebuild();
                    MatchSetup.FromLobby=true;MatchSetup.StartLocalSession=false;MatchSetup.BotTarget=21;
                    var rules=ModeSimulation.Defaults(GameModeKind.CaptureFlag);rules.countdownSeconds=0;rules.automaticDisasters=false;
                    session.Modes.Configure(rules);session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots.Clear();
                    for(int i=0;i<20;i++)if(!session.SurvivorBots.SpawnOne())throw new Exception("Bot spawn failed.");
                    CheckModeFoundations();session.Modes.Configure(rules);
                    session.Modes.StartRound();started=Time.time;configured=true;lastPositions.Clear();distances.Clear();return;
                }
                // Ignore startup placement and large respawn jumps; count ordinary physical movement.
                if(Time.time-started>=2)foreach(var bot in session.Bots().Bots)
                {
                    int id=bot.GetInstanceID();var p=bot.transform.position;
                    if(lastPositions.TryGetValue(id,out var before))
                    {float step=Vector3.Distance(before,p);if(step<3){distances.TryGetValue(id,out float walked);distances[id]=walked+step;}}
                    lastPositions[id]=p;
                }
                if(Time.time-started<20)return;
                int moved=session.Bots().Bots.Count(b=>distances.TryGetValue(b.GetInstanceID(),out float walked)&&walked>1);
                if(moved<16)throw new Exception("Only "+moved+"/20 bots moved >1m.");
                if(map.Blocks.Count!=7||map.GetComponentsInChildren<Rigidbody>().Any(b=>!b.isKinematic))throw new Exception("Static map physics contract changed.");
                if(session.Modes.Frame.flags.Length!=2)throw new Exception("CTF flag homes did not initialize.");
                var mood=map.GetComponentInChildren<DeadlyJenga.CandyArt.ChristmasTownAtmosphere>();
                if(!mood||RenderSettings.skybox!=mood.winterSky)throw new Exception("Town atmosphere did not apply in Game.");
                if(mood.localLights.Count(l=>l&&l.enabled)>16)throw new Exception("Desktop local light budget exceeded.");
                if(mood.localLights.Any(l=>l&&l.shadows!=LightShadows.None))throw new Exception("Local lights unexpectedly cast realtime shadows.");
                var snow=map.GetComponentInChildren<DeadlyJenga.CandyArt.ChristmasTownSnow>();
                if(!snow||!snow.flakes.isPlaying||snow.flakes.particleCount<10||snow.flakes.collision.enabled)throw new Exception("Lightweight snowfall did not start correctly.");
                foreach(var house in ChristmasTownLayout.Houses)
                    if(snow.AllowsSnowAt(map.transform.TransformPoint(house.position+Vector3.up*1.6f)))throw new Exception("Snow mask leaks into "+house.name);
                if(!snow.AllowsSnowAt(map.transform.TransformPoint(new Vector3(0,1.6f,12))))throw new Exception("Outdoor plaza snowfall incorrectly masked.");
                if(ShaderUtil.ShaderHasError(mood.winterSky.shader)||ShaderUtil.ShaderHasError(snow.flakes.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader))throw new Exception("Winter shaders have compilation errors.");
                if(errorCount>0)throw new Exception(string.Join("\n",errors));
                File.AppendAllText(Report,"PASS "+moved+"/20 bots moved >1m after settling; placement/large respawn jumps excluded. Seven claim pads remained kinematic. Two flags initialized.\nPASS Actual Game entered Play Mode; no bot contract errors observed. Not a completed-round, Quest or peer test.\n");
                File.AppendAllText(Report,"PASS Winter sky applied during actual play; local lights stay within the desktop budget and cast no realtime shadows.\n");
                File.AppendAllText(Report,"PASS Snow particles running without collision physics; all sixteen interior centers masked, open plaza allows snow; winter shaders have no reported compilation errors.\n");
                Finish(0);
            }
            catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");Finish(1);}
        }
        static void Finish(int code)
        {
            SessionState.SetBool(Key,false);SessionState.SetBool(Key+"Restore",true);SessionState.SetInt(Key+"Exit",code);EditorApplication.ExitPlaymode();
        }
        static void CheckModeFoundations()
        {
            var layout=Resources.LoadAll<DeadlyJenga.Modes.MapMatchLayout>("MapMatchLayouts").Single(l=>l.Matches(map));
            Physics.SyncTransforms();
            int mask=(1<<Layers.Surface)|(1<<Layers.Block)|1;
            foreach(GameModeKind kind in Enum.GetValues(typeof(GameModeKind)))
            {
                var rules=ModeSimulation.Defaults(kind);rules.countdownSeconds=0;rules.automaticDisasters=false;rules.island=IslandRule.Walkable;
                session.Modes.Configure(rules);session.Modes.StartRound();
                var distinct=new System.Collections.Generic.HashSet<Vector3>();
                for(int i=0;i<50;i++)
                {
                    int team=rules.spawn==SpawnRule.TeamPoles?i%rules.teamCount:-1;
                    var p=session.Modes.SpawnPoint(team,i,"survivor");
                    if(p.y>.5f||!Physics.Raycast(p+Vector3.up*.2f,Vector3.down,.4f,mask,QueryTriggerInteraction.Ignore)||
                        Physics.CheckCapsule(p+Vector3.up*.4f,p+Vector3.up*1.65f,.32f,mask,QueryTriggerInteraction.Ignore))
                        throw new Exception(kind+" selected a blocked or rooftop spawn: "+p);
                    distinct.Add(p);
                }
                // Queries do not reserve positions. Verify actual placements separately.
                var bots=session.Bots().Bots.Where(b=>b&&b.gameObject.activeInHierarchy).ToArray();
                if(bots.Select(b=>b.transform.position).Distinct().Count()!=bots.Length)throw new Exception(kind+" stacked actual bot spawns.");
                if(bots.Any(b=>b.transform.position.y>.6f))throw new Exception(kind+" placed a bot above ground level.");
                if(distinct.Count<4)throw new Exception(kind+" has too few distinct spawn choices: "+distinct.Count+" world="+Services.World?.GetType().Name+" signature="+(Services.World as ISyncedWorld)?.SaveLayout()+" first="+string.Join(";",distinct.Take(5)));
                if(kind==GameModeKind.CaptureFlag)
                    for(int t=0;t<2;t++)if(Vector3.Distance(session.Modes.Frame.flags[t].home,map.transform.TransformPoint(layout.teams[t].flagHome))>.01f)
                        throw new Exception("CTF ignored an authored flag home.");
            }
            foreach(int teams in new[]{4,8})
            {
                var rules=ModeSimulation.Defaults(GameModeKind.CaptureFlag);rules.teamCount=teams;rules.countdownSeconds=0;rules.automaticDisasters=false;
                session.Modes.Configure(rules);session.Modes.StartRound();
                if(session.Modes.Frame.flags.Length!=teams)throw new Exception("Flag homes missing for "+teams+" teams.");
                for(int t=0;t<teams;t++)if(Vector3.Distance(session.Modes.Frame.flags[t].home,map.transform.TransformPoint(layout.teams[t].flagHome))>.01f)
                    throw new Exception("Wrong authored flag home for team "+t);
            }
            File.AppendAllText(Report,"PASS All eight mode setup paths: fifty grounded spawn queries each; four/eight-team CTF flag homes match authored anchors. This checks setup, not complete rounds.\n");
        }
    }
}
