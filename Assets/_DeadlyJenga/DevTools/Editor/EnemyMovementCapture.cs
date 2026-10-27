using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class EnemyMovementCapture
    {
        const string Key="EnemyMovement.Capture";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        static IEnumerator run; static int errors;
        static string Report=>ProjectPath.Report("EnemyMovementCapture.txt");
        [Serializable] sealed class Saved {public SceneSetup[] scenes;public string map,mode;public int bots,hands;public bool fromLobby,local,background,defaultScene;public float volume;}
        static EnemyMovementCapture(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        static void Tick()
        {
            if(run==null)
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isUpdating||EditorApplication.isCompiling)return;
                string close=ProjectPath.Report("EnemyMovementCapture.close");
                if(Own(close)){File.Delete(close);EditorApplication.Exit(0);return;}
                string request=ProjectPath.Report("EnemyMovementCapture.request");
                if(!Own(request))return;
                var scenes=EditorSceneManager.GetSceneManagerSetup();
                if(Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(s=>s.isLoaded&&s.isDirty)){File.WriteAllText(Report,"Deferred: unsaved scene left untouched.");return;}
                if(!ValidationRunGate.TryAcquire(Key))return;
                File.Delete(request);File.WriteAllText(Report,"Enemy movement capture started "+DateTime.Now.ToString("O")+"\n");
                SessionState.SetString(Key+"Saved",JsonUtility.ToJson(new Saved{scenes=scenes,defaultScene=SceneManager.GetActiveScene().rootCount>0,map=MatchSetup.MapId,mode=MatchSetup.ModeId,bots=MatchSetup.BotTarget,hands=MatchSetup.HandCount,fromLobby=MatchSetup.FromLobby,local=MatchSetup.StartLocalSession,background=Application.runInBackground,volume=AudioListener.volume}));
                MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=1;MatchSetup.HandCount=0;MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;
                SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();return;
            }
            try{if(run.MoveNext())return;}catch(Exception e){errors++;File.AppendAllText(Report,"FAIL "+e+"\n");}
            run=null;Application.logMessageReceived-=Log;
            File.AppendAllText(Report,"COMPLETE; errors="+errors+". Actual desktop Editor; staged driver input and temporary inspection arena. No Quest or autonomous navigation verification.\n");
            EditorApplication.ExitPlaymode();
        }
        static bool Own(string file)=>File.Exists(file)&&File.ReadAllText(file).Trim()==System.Diagnostics.Process.GetCurrentProcess().Id.ToString();
        static void State(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)&&ValidationRunGate.Owns(Key))
            {
                SessionState.SetBool(Key,false);SessionState.SetBool(Key+"Running",true);errors=0;
                EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();Application.logMessageReceived+=Log;run=Validate();
            }
            if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key+"Running",false))return;
            SessionState.SetBool(Key+"Running",false);Application.logMessageReceived-=Log;run=null;
            AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
            var s=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Saved",""));
            MatchSetup.MapId=s.map;MatchSetup.ModeId=s.mode;MatchSetup.BotTarget=s.bots;MatchSetup.HandCount=s.hands;MatchSetup.FromLobby=s.fromLobby;MatchSetup.StartLocalSession=s.local;Application.runInBackground=s.background;AudioListener.volume=s.volume;
            try{if(s.scenes!=null&&s.scenes.Any(x=>x.isLoaded&&!string.IsNullOrEmpty(x.path)))EditorSceneManager.RestoreSceneManagerSetup(s.scenes);else EditorSceneManager.NewScene(s.defaultScene?NewSceneSetup.DefaultGameObjects:NewSceneSetup.EmptyScene,NewSceneMode.Single);}
            finally{ValidationRunGate.Release(Key);}
        }
        static void Log(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+stack+"\n");}}
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);File.AppendAllText(Report,"PASS "+message+"\n");}
        static IEnumerator Validate()
        {
            Application.runInBackground=true;AudioListener.volume=0;
            float end=Time.realtimeSinceStartup+90;
            while(!(Services.Session is CastlePlaySession)&&Time.realtimeSinceStartup<end)yield return null;
            var session=Services.Session as CastlePlaySession;Require(session,"Actual Game session loaded");
            yield return new WaitForSeconds(.5f);
            var rules=DeadlyJenga.Modes.ModeSimulation.Defaults(GameModeKind.Demo);rules.countdownSeconds=0;rules.healthEnabled=true;rules.deathEnabled=false;rules.fallDamage=false;rules.island=IslandRule.Walkable;
            session.Modes.Configure(rules);session.Modes.StartRound();session.SurvivorBots.AutoFill=false;session.SurvivorBots.Running=false;session.SurvivorBots.Clear();
            var player=session.Survivor();player.InputSuspended=true;
            var center=Services.World.Center+new Vector3(-70,12,-70);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Enemy capture staging floor";floor.layer=Layers.Surface;floor.transform.position=center-Vector3.up*.3f;floor.transform.localScale=new Vector3(30,.6f,30);
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=new Color(.25f,.34f,.43f);material.SetFloat("_Smoothness",.65f);floor.GetComponent<Renderer>().sharedMaterial=material;
            player.Teleport(center+new Vector3(8,.08f,5),Quaternion.identity);
            var cameraRoot=new GameObject("Enemy close capture camera");var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=42;camera.clearFlags=CameraClearFlags.Skybox;camera.stereoTargetEye=StereoTargetEyeMask.None;
            var population=session.SurvivorBots as IEnemyPopulation;Require(population!=null,"Real enemy population available");
            foreach(var kind in new[]{"mushroom"})
            {
                var enemy=population.SpawnEnemy(new EnemySpawnSettings{archetypeId=kind,hitDamage=10});Require(enemy,"Spawned actual "+kind);
                var brain=enemy.GetComponent<DeadlyJenga.CastleBots.CastleBotBrain>();brain.enabled=false;
                var motor=enemy.GetComponent<CastleSurvivor>();motor.InputSuspended=true;motor.Teleport(center+Vector3.up*.08f,Quaternion.identity);
                yield return new WaitForSeconds(1.2f);
                var avatar=enemy.GetComponent<DeadlyJenga.CandyPlayers.CandyPlayerAvatar>();
                var shell=avatar.Rig.GetComponent<DeadlyJenga.CandyPlayers.CandyMushroomAppearance>();
                Require(shell&&shell.RendererCount==6,"Compact mushroom shell uses six renderers");
                Require(!avatar.Rig.bodyRenderer.enabled&&!avatar.Rig.headRenderer.enabled,"Original humanoid body and head are hidden");
                Require(shell.transform.Find("Visual - mushroom creature").GetComponentsInChildren<Collider>().Length==0,"Creature shell adds no colliders");
                Require(enemy.GetComponent<IEnemyMovement>().JumpMultiplier==5&&!enemy.GetComponent<IEnemyMovement>().CanClimb,"Original mushroom jump and traversal settings preserved");
                var capRenderer=shell.transform.Find("Visual - mushroom creature/Cap wobble/Visual - broad toffee cap").GetComponent<Renderer>();
                var capProperties=new MaterialPropertyBlock();capRenderer.GetPropertyBlock(capProperties);
                Require(capProperties.GetFloat("_Smoothness")>.9f&&capProperties.GetFloat("_CandyGlaze")>.8f,"Candy gloss and glaze survive live health presentation");
                var capColor=capProperties.GetColor("_BaseColor");
                Require(Mathf.Abs(capColor.r-.56f)<.001f&&Mathf.Abs(capColor.g-.205f)<.001f&&Mathf.Abs(capColor.b-.075f)<.001f,"Original toffee RGB color preserved");
                float h=motor.Height;Shot(camera,enemy,kind+"-front.png",new Vector3(1.9f,1.15f,3),h*.6f);
                Shot(camera,enemy,kind+"-side.png",new Vector3(3,1.05f,.8f),h*.6f);
                Require(enemy.GetComponent<IEnemyMovement>().CanClimb==(kind!="mushroom"),kind+" climb setting recorded");
                File.AppendAllText(Report,"MEASURE "+kind+" scale="+motor.playerScale+" jumpHeight="+motor.jumpHeight+" multiplier="+enemy.GetComponent<IEnemyMovement>().JumpMultiplier+"\n");
                var driver=new CaptureDriver{direction=Vector3.forward};motor.InputDriver=driver;motor.InputSuspended=false;
                var before=enemy.transform.position;yield return new WaitForSeconds(.35f);Shot(camera,enemy,kind+"-moving.png",new Vector3(2.7f,1.1f,2),h*.6f);
                Require(Vector3.ProjectOnPlane(enemy.transform.position-before,Vector3.up).magnitude>.05f,kind+" moves through real character motor using staged forward input");
                driver.direction=Vector3.zero;yield return new WaitForSeconds(.15f);float ground=enemy.transform.position.y;driver.jump=true;
                File.AppendAllText(Report,"MEASURE before jump grounded="+motor.Grounded+" inputSuspended="+motor.InputSuspended+" canJump="+session.Modes.RulesFor(enemy).canJump+"\n");
                end=Time.realtimeSinceStartup+3;while(enemy.transform.position.y<ground+.3f&&Time.realtimeSinceStartup<end)yield return null;
                Require(enemy.transform.position.y>ground+.2f,kind+" leaves ground through real jump command");
                Shot(camera,enemy,kind+"-jump.png",new Vector3(2.7f,1.2f,3),h*.6f);
                // Include ground below the airborne actor in a second, fixed-target view.
                camera.transform.position=center+new Vector3(6,4,8);camera.transform.LookAt(center+Vector3.up*1.5f);Capture(camera,kind+"-jump-wide.png");
                driver.jump=false;end=Time.realtimeSinceStartup+5;while(!motor.Grounded&&Time.realtimeSinceStartup<end)yield return null;
                motor.InputSuspended=true;motor.Teleport(center+Vector3.up*.08f,Quaternion.identity);
                if(kind=="jaguar")
                {
                    typeof(DeadlyJenga.CastleBots.CastleBotBrain).GetProperty("CombatState").SetValue(brain,DeadlyJenga.CastleBots.BotCombatState.Windup);
                    yield return new WaitForSeconds(.45f);Shot(camera,enemy,"jaguar-melee-posture.png",new Vector3(2,1.2f,3),h*.6f);
                    motor.Damage(45,player.gameObject);yield return new WaitForSeconds(.12f);Shot(camera,enemy,"jaguar-hurt.png",new Vector3(2,1.2f,3),h*.6f);
                    motor.Eliminate(player.gameObject);yield return null;camera.transform.position=center+new Vector3(2,1.2f,3);camera.transform.LookAt(center+Vector3.up*.3f);Capture(camera,"jaguar-hairball.png");
                }
                if(kind=="mushroom"){
                    motor.Damage(25,player.gameObject);yield return new WaitForSeconds(.2f);
                    capRenderer.GetPropertyBlock(capProperties);
                    Require(capProperties.GetFloat("_CandyGlaze")>.8f&&capProperties.GetFloat("_Smoothness")>.9f,"Damage fill retains the candy glaze");
                    Shot(camera,enemy,"mushroom-hurt.png",new Vector3(1.9f,1.15f,3),h*.6f);
                    avatar.ApplyAppearance(new AvatarAppearance());yield return null;
                    Require(avatar.Rig.bodyRenderer.enabled&&avatar.Rig.headRenderer.enabled,"Switching to a regular appearance restores the original avatar");
                    avatar.ApplyAppearance(new AvatarAppearance{skin=AvatarSkin.MushroomSprite});yield return null;
                    Require(shell.RendererCount==6&&!avatar.Rig.bodyRenderer.enabled,"Switching back reuses the mushroom shell without duplicates");
                }
                population.ClearEnemies();yield return null;
            }
            Object.Destroy(cameraRoot);Object.Destroy(floor);Object.Destroy(material);
        }
        sealed class CaptureDriver:DeadlyJenga.CastleBots.ICastleSurvivorDriver
        {
            public Vector3 direction;public bool jump;
            public DeadlyJenga.CastleBots.CastleBotCommand ReadCommand(CastleSurvivor motor,float dt)
            {bool pressed=jump&&motor.Grounded;if(pressed)jump=false;return new DeadlyJenga.CastleBots.CastleBotCommand{move=direction,look=Vector3.forward,jump=pressed};}
        }
        static void Shot(Camera camera,GameObject enemy,string name,Vector3 offset,float focus)
        {camera.transform.position=enemy.transform.position+offset;camera.transform.LookAt(enemy.transform.position+Vector3.up*focus);Capture(camera,name);}
        static void Capture(Camera camera,string file)
        {
            var before=camera.targetTexture;var active=RenderTexture.active;var target=new RenderTexture(1800,1200,24);var image=new Texture2D(1800,1200,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1800,1200),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report(file),image.EncodeToPNG());}
            finally{camera.targetTexture=before;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(image);}
        }
    }
}
