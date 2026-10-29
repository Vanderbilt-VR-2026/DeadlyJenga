using System;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CastleBots;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    // Explicit check of real scene travel and the ordinary island rules.
    [InitializeOnLoad]
    public static class IslandSessionChecks
    {
        const string Key="IslandSessionChecks";
        [Serializable] sealed class Scenes { public SceneSetup[] scenes; }
        static string Report=>ProjectPath.Report("IslandSession.txt");
        static int stage,round,errors,eliminations;
        static double deadline;
        static float began,normalSpeed;
        static IslandRoomConnection room;
        static CastlePlaySession session;
        static CastleSurvivor player;
        static Vector3 ground;
        static bool initialized;
        sealed class JumpOnce : ICastleSurvivorDriver
        {
            bool sent;
            public CastleBotCommand ReadCommand(CastleSurvivor motor,float dt)
            {bool jump=!sent&&motor.Grounded;sent|=jump;return new CastleBotCommand{jump=jump,look=Vector3.forward};}
        }
        static CastleSurvivor jumping;
        static float jumpStart,jumpPeak;
        static bool airborne,botJump;
        static IslandSessionChecks()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key,false))return;
                SessionState.SetBool(Key,false);Application.logMessageReceived-=Log;GameEvents.Eliminated-=Eliminated;
                EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes","")).scenes);
                SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
                MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
                File.AppendAllText(Report,"PASS Original scene setup and match choices restored.\n");
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Island Rules and Repeat Games")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SpectatorVideoCapture.Busy)throw new InvalidOperationException("Stop Play Mode and recordings first.");
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>string.IsNullOrEmpty(s.path))||Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Save edited scenes first.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{scenes=scenes}));
            SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);
            File.WriteAllText(Report,"Actual Lobby -> Game -> Lobby travel, three solo launches; Candy Town safe ground and live island toggles. Desktop only.\n");
            SessionState.SetBool(Key,true);initialized=false;
            MatchSetup.BotTarget=1;MatchSetup.HandCount=0;MatchSetup.StartLocalSession=false;
            EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));EditorApplication.EnterPlaymode();
        }
        static void Check(bool ok,string message)
        {File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+message+"\n");if(!ok)throw new Exception(message);}
        static void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}}
        static void Eliminated(GameObject who,GameObject source){if(player&&who==player.gameObject)eliminations++;}
        static void Wait(int next){stage=next;deadline=EditorApplication.timeSinceStartup+35;}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))
            {
                string request=ProjectPath.Report("IslandSession.request");
                if(File.Exists(request)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating){File.Delete(request);Run();}
                return;
            }
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            try
            {
                if(!initialized){initialized=true;round=errors=eliminations=0;Application.logMessageReceived+=Log;GameEvents.Eliminated+=Eliminated;Wait(0);}
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timeout in stage "+stage);
                if(stage==0)
                {
                    var lobby=IslandLobby.Current;if(!lobby||lobby.Player==null)return;
                    if(!room)room=lobby.Room;
                    Check(room==lobby.Room&&room.CanLaunch,"Lobby "+(round+1)+" can launch using the same persistent room");
                    int town=Array.FindIndex(room.Catalog.maps,m=>m.id=="mode-christmas-town");Check(town>=0,"Candy Town is available");
                    room.SelectMap(town);room.SelectMode(Array.FindIndex(room.Catalog.modes,m=>m.id=="survival"));
                    Check(room.Setup.rules.island==IslandRule.Walkable,"Candy Town stays safe when selecting Survival");
                    room.SelectMap(0);Check(room.Setup.rules.island==IslandRule.Sink,"Castle retains Survival island penalties");room.SelectMap(town);
                    foreach(GameModeKind mode in Enum.GetValues(typeof(GameModeKind)))Check(MatchSetup.DefaultModeRules(mode,room.Catalog.maps[town]).island==IslandRule.Walkable,"Candy Town default is safe for "+mode);
                    room.SelectMode(Array.FindIndex(room.Catalog.modes,m=>m.id=="demo"));
                    var view=lobby.View();Check(view.InvokeChoice("island-effects")&&room.Setup.rules.island==IslandRule.Sink,"Lobby toggle enables island effects");
                    Check(view.InvokeChoice("island-effects")&&room.Setup.rules.island==IslandRule.Walkable,"Lobby toggle disables island effects");
                    var setup=room.Setup;setup.population=1;setup.hands=0;setup.rules.countdownSeconds=0;setup.rules.automaticDisasters=false;room.ConfigureSetup(setup);
                    room.Launch();Wait(1);return;
                }
                if(stage==1)
                {
                    session=Services.Session as CastlePlaySession;if(!session||!session.Modes||session.Player==null)return;
                    session.SetMenu(false);if(session.Social!=null)session.Social.VoiceEnabled=false;player=session.Survivor();player.InputSuspended=true;
                    Check(session.Modes.Rules.island==IslandRule.Walkable,"Game "+(round+1)+" loads the selected safe island rule");
                    Check(ModeDefaults.Create(GameModeKind.Survival).island==IslandRule.Walkable,"In-game mode changes also use Candy Town defaults");
                    if(round>0){session.Modes.ReturnToLobby();round++;Wait(round==3?6:0);return;}
                    var bounds=Services.World.PlayableBounds;bool found=false;ground=default;
                    for(float x=-8;x<=8&&!found;x+=2)for(float z=-8;z<=8;z+=2)
                    {
                        var origin=Services.World.Center+new Vector3(x,bounds.size.y+10,z);
                        if(Physics.Raycast(origin,Vector3.down,out var hit,100,(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore)&&!hit.rigidbody&&hit.normal.y>.9f){ground=hit.point+Vector3.up*.03f;found=true;break;}
                    }
                    Check(found,"Found actual static island ground on Candy Town");player.Place(ground,Quaternion.identity);began=Time.time;normalSpeed=player.EffectiveWalkSpeed;Wait(2);return;
                }
                if(stage==2)
                {
                    if(Time.time-began<6)return;
                    Check(player.Grounded&&!player.StandingBody&&player.Alive&&eliminations==0&&!player.Sinking&&Mathf.Approximately(player.EffectiveWalkSpeed,normalSpeed),"Candy Town ground is safe beyond sink grace and preserves normal movement");
                    var prefab=Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog").survivorPrefab.GetComponent<CastleSurvivor>();
                    Check(player.jumpHeight>=prefab.jumpHeight,"Pavement step height does not reduce the authored player jump");
                    float normalJump=player.jumpHeight;player.FitJump(3,.18f);
                    Check(player.jumpHeight>normalJump&&player.jumpHeight<3,"Tall block fitting still leaves a climb shortfall");player.FitJump(.2f,.18f);
                    Check(Mathf.Approximately(player.jumpHeight,normalJump),"Repeated fitting restores the normal jump on small steps");
                    botJump=false;StartJump(player);return;
                }
                if(stage==8)
                {
                    jumpPeak=Mathf.Max(jumpPeak,jumping.transform.position.y-jumpStart);airborne|=!jumping.Grounded;
                    if(Time.time-began<2)return;
                    Check(airborne&&jumpPeak>1.5f&&jumping.Grounded,(botJump?"Bot":"Player")+" physically jumps and lands on Candy Town; rise="+jumpPeak.ToString("0.00")+"m");
                    if(!botJump)
                    {
                        player.externallyDriven=false;player.InputDriver=null;player.InputSuspended=true;
                        Check(session.SurvivorBots.SpawnOne(),"Spawn ordinary bot for jump regression");
                        player.Place(ground+Vector3.right*2,Quaternion.identity);
                        var bot=session.Bots().Bots.Last();bot.enabled=false;bot.Motor.Place(ground,Quaternion.identity);
                        botJump=true;StartJump(bot.Motor);return;
                    }
                    session.SurvivorBots.Clear();player.Place(ground,Quaternion.identity);
                    var rules=session.Modes.Rules.Copy();rules.island=IslandRule.Sink;rules.sinkGraceSeconds=30;session.Modes.Configure(rules);began=Time.time;Wait(3);return;
                }
                if(stage==3)
                {
                    if(Time.time-began<1)return;
                    Check(player.Sinking&&player.EffectiveWalkSpeed<normalSpeed*.5f,"Enabling island effects produces real sinking and slower movement");
                    var rules=session.Modes.Rules.Copy();rules.island=IslandRule.Walkable;session.Modes.Configure(rules);began=Time.time;Wait(4);return;
                }
                if(stage==4)
                {
                    if(Time.time-began<1)return;
                    Check(!player.Sinking&&player.Alive&&Mathf.Approximately(player.EffectiveWalkSpeed,normalSpeed),"Disabling effects during sinking restores normal movement");
                    var rules=session.Modes.Rules.Copy();rules.island=IslandRule.Lethal;session.Modes.Configure(rules);began=Time.time;Wait(5);return;
                }
                if(stage==5)
                {
                    if(Time.time-began<1)return;
                    Check(eliminations>0,"Lethal island option still causes an actual elimination");session.Modes.ReturnToLobby();round++;Wait(0);return;
                }
                if(stage==6&&IslandLobby.Current)
                {Check(room.CanLaunch,"Third return leaves Start enabled");Check(errors==0,"No runtime errors");File.AppendAllText(Report,"PASS Complete: three real solo games and island rule transitions.\n");stage=7;EditorApplication.ExitPlaymode();}
            }
            catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");stage=7;EditorApplication.ExitPlaymode();}
        }
        static void StartJump(CastleSurvivor motor)
        {
            jumping=motor;jumpStart=motor.transform.position.y;jumpPeak=0;airborne=false;
            motor.externallyDriven=true;motor.InputSuspended=false;motor.InputDriver=new JumpOnce();began=Time.time;Wait(8);
        }
    }
}
