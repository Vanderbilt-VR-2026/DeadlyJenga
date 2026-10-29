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
    public static class PowerupLabChecks
    {
        const string Key="PowerupLabChecks";
        static CastlePlaySession session;static CastleSurvivor player;static PowerupTestMap map;
        static EffectType speed,jump;static int phase,errors;static double boot;static float began,waitAt;
        [Serializable]sealed class SavedScenes{public SceneSetup[] scenes;}
        static string Report=>ProjectPath.Report("PowerupLab-runtime.txt");
        static PowerupLabChecks()
        {
            EditorApplication.update+=Tick;
            Application.logMessageReceived+=(message,stack,kind)=>{if(SessionState.GetBool(Key,false)&&kind==LogType.Exception){errors++;File.AppendAllText(Report,message+"\n");}};
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key+"Restore",false))return;
                SessionState.SetBool(Key+"Restore",false);var saved=JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key+"Scenes",""));
                if(saved?.scenes?.Length>0)EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
                SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
                MatchSetup.FromLobby=SessionState.GetBool(Key+"FromLobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
                if(Application.isBatchMode)EditorApplication.Exit(SessionState.GetInt(Key+"Exit",1));
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Powerup Lab")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode first.");
            if(!Application.isBatchMode&&Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))throw new Exception("Save scenes first.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new SavedScenes{scenes=EditorSceneManager.GetSceneManagerSetup().Where(s=>!string.IsNullOrEmpty(s.path)).ToArray()}));
            SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));SessionState.SetBool(Key+"FromLobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);
            File.WriteAllText(Report,"Actual Game scene. Solo desktop powerup test; no headset/peer test.\n");SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        static void Apply(EffectType type)=>player.Apply(new Effect{type=type,duration=20,strength=1});
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            if(boot==0)boot=EditorApplication.timeSinceStartup;
            try
            {
                if(phase==0)
                {
                    if(EditorApplication.timeSinceStartup-boot<3)return;
                    session=UnityEngine.Object.FindFirstObjectByType<CastlePlaySession>();if(!session?.Player?.gameObject){Check(EditorApplication.timeSinceStartup-boot<45,"Game did not initialize.");return;}
                    if(session.Social!=null)session.Social.VoiceEnabled=false;
                    session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots.Clear();
                    MatchSetup.MapId="mode-powerup-test";MatchSetup.ApplyMap();map=Services.World as PowerupTestMap;Check(map,"Catalog did not load the powerup island.");
                    var rules=ModeSimulation.Defaults(GameModeKind.Demo);rules.island=IslandRule.Walkable;rules.automaticDisasters=false;
                    session.Modes.Configure(rules);session.Modes.StartRound();session.SetMenu(false);
                    player=session.Survivor();player.InputSuspended=true;player.RefreshMode();
                    speed=Resources.Load<EffectType>("Powerups/Speed2x");jump=Resources.Load<EffectType>("Powerups/Jump3x");Check(speed&&jump,"Effect assets missing.");
                    CheckMotion();CheckClimb();
                    player.Teleport(map.SafeSpawnPoint(),Quaternion.identity);player.Remove(speed);player.Remove(jump);
                    session.Demo.Run(DemoCommand.SpawnSpeedPowerup);var orb=UnityEngine.Object.FindObjectsByType<MovementPowerupPickup>(FindObjectsSortMode.None).Single(o=>o.effect==speed);
                    Check(Vector3.Distance(orb.transform.position,player.ViewCamera.transform.position)<3.5f,"Orb not in front of player.");
                    player.Teleport(new Vector3(orb.transform.position.x,.06f,orb.transform.position.z),Quaternion.identity);player.Simulate(Vector2.zero,false,.02f);
                    waitAt=Time.time;phase=1;return;
                }
                if(phase==1)
                {
                    if(Time.time-waitAt<.2f)return;Check(player.Has(speed),"Walking into the speed orb did not collect it.");
                    session.Demo.Run(DemoCommand.SpawnJumpPowerup);var orb=UnityEngine.Object.FindObjectsByType<MovementPowerupPickup>(FindObjectsSortMode.None).Single(o=>o.effect==jump);
                    Check(orb.Collect(player.gameObject)&&!orb.Collect(player.gameObject),"Orb collection is not one-shot.");
                    began=Time.time;phase=2;return;
                }
                if(phase==2&&Time.time-began>=5)
                {
                    var hud=player.GetComponent<PowerupHud>();Check(hud&&hud.ShownSpeedSeconds>13&&hud.ShownSpeedSeconds<16,"Speed HUD timer is not decreasing.");
                    Check(hud.ShownJumpSeconds>14&&hud.ShownJumpSeconds<16,"Jump HUD timer is not decreasing.");
                    session.Demo.Run(DemoCommand.SpawnSpeedPowerup);var orb=UnityEngine.Object.FindObjectsByType<MovementPowerupPickup>(FindObjectsSortMode.None).Single(o=>o.effect==speed);
                    Check(orb.Collect(player.gameObject),"Refresh pickup failed.");Check(player.PowerupSpeedMultiplier==2&&player.SecondsRemaining(speed)>19.8f,"Refresh stacked speed or failed to reset duration.");
                    ScreenCapture.CaptureScreenshot(ProjectPath.Report("PowerupLab-runtime.png"));
                    File.AppendAllText(Report,"PASS Both demo commands spawn collectible glass orbs; trigger pickup, one-shot collection, decreasing HUD timers and refresh without multiplier stacking.\n");phase=3;
                }
                if(phase==3&&Time.time-began>=20.4f)
                {Check(!player.Has(jump)&&player.Has(speed)&&player.PowerupUnlimitedStamina,"Independent jump expiry failed.");phase=4;}
                if(phase==4&&Time.time-began>=25.6f)
                {
                    Check(!player.Has(speed)&&!player.Has(jump)&&player.PowerupSpeedMultiplier==1&&!player.PowerupUnlimitedStamina,"Buffs did not restore baseline after twenty seconds.");
                    Check(errors==0,"Runtime exceptions occurred.");
                    File.AppendAllText(Report,"PASS Real-time 20-second expiry and independent refresh; normal movement and stamina return. Game entered Play Mode without runtime exceptions.\n");Finish(0);
                }
            }
            catch(Exception error){File.AppendAllText(Report,"FAIL "+error+"\n");Finish(1);}
        }
        static float Walk()
        {
            var start=new Vector3(-15,.06f,-18);player.Teleport(start,Quaternion.LookRotation(Vector3.right));
            for(int i=0;i<100;i++)player.Simulate(Vector2.up,false,.01f);
            return Vector3.ProjectOnPlane(player.transform.position-start,Vector3.up).magnitude;
        }
        static float Jump()
        {
            player.Teleport(new Vector3(-15,.06f,-18),Quaternion.identity);for(int i=0;i<5;i++)player.Simulate(Vector2.zero,false,.01f);
            float start=player.transform.position.y,top=start;player.Simulate(Vector2.zero,true,.01f);
            for(int i=0;i<250;i++){player.Simulate(Vector2.zero,false,.01f);top=Mathf.Max(top,player.transform.position.y);}
            return top-start;
        }
        static void CheckMotion()
        {
            float baseline=Walk();Apply(speed);float boosted=Walk();Check(baseline>1&&Mathf.Abs(boosted/baseline-2)<.03f,"Actual walking distance is not doubled.");
            player.Remove(speed);float low=Jump();Apply(jump);float high=Jump();Check(low>.2f&&Mathf.Abs(high/low-3)<.1f,"Actual jump apex is not tripled.");player.Remove(jump);
            session.SurvivorBots.SpawnOne();var bot=session.Bots().Bots.Last().Motor;bot.Apply(new Effect{type=speed,duration=20});bot.Apply(new Effect{type=jump,duration=20});
            Check(Mathf.Abs(bot.EffectiveWalkSpeed/bot.walkSpeed-2)<.01f&&Mathf.Abs(bot.EffectiveJumpHeight/bot.jumpHeight-3)<.01f&&bot.PowerupUnlimitedStamina,"Bot motor does not receive the same powers.");
            bot.Eliminate(null);Check(!bot.Has(speed)&&!bot.Has(jump),"Death did not clear buffs.");session.SurvivorBots.Clear();
            File.AppendAllText(Report,"PASS Measured walk distance "+baseline.ToString("F2")+" -> "+boosted.ToString("F2")+"m; jump apex "+low.ToString("F2")+" -> "+high.ToString("F2")+"m. Bot motor matches; death clears powers.\n");
        }
        static void CheckClimb()
        {
            float capacity=player.climbGripSeconds,drain=player.climbDrainPerSecond,climb=player.climbSpeed;
            try
            {
                player.climbGripSeconds=.2f;player.climbDrainPerSecond=4;player.climbSpeed=.1f;player.InputSuspended=false;
                player.Teleport(new Vector3(0,.06f,-2.24f),Quaternion.identity);Apply(speed);
                for(int i=0;i<60;i++)player.Simulate(Vector2.up,false,.02f,true);
                Check(player.Climbing&&player.GripRemaining>.999f,"Unlimited stamina failed during an actual climb.");player.Remove(speed);
            }
            finally{player.climbGripSeconds=capacity;player.climbDrainPerSecond=drain;player.climbSpeed=climb;player.InputSuspended=true;}
            File.AppendAllText(Report,"PASS Actual sustained climb keeps full stamina under accelerated drain.\n");
        }
        static void Finish(int code){SessionState.SetBool(Key,false);SessionState.SetBool(Key+"Restore",true);SessionState.SetInt(Key+"Exit",code);EditorApplication.ExitPlaymode();}
    }
}
