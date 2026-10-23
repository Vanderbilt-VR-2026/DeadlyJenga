using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class UserFeatureChecks
    {
        const string Key="UserFeatureChecks";
        [Serializable] sealed class SavedScenes {public SceneSetup[] scenes;}
        static int checks,errors;static bool failed;
        static string Report=>ProjectPath.Report("UserFeatures.txt");
        static UserFeatureChecks()
        {
            EditorApplication.playModeStateChanged+=state=>{
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode){checks=errors=0;failed=false;Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>new GameObject("User feature integration checks").AddComponent<UserFeatureDriver>().StartCoroutine(Guard());}
                if(state==PlayModeStateChange.EnteredEditMode){Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);var saved=JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key+"Scenes","")).scenes;if(saved.Any(s=>s.isLoaded&&s.isActive))EditorSceneManager.RestoreSceneManagerSetup(saved);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);File.AppendAllText(Report,"PASS Scene setup and match choices restored\n");}
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Weapons Jaguar Death and Controls")]
        public static void Run()=>Start(false);
        public static void RunInputOnly()=>Start(true);
        static void Start(bool inputOnly)
        {
            SessionState.SetBool(Key+"InputOnly",inputOnly);
            var scenes=EditorSceneManager.GetSceneManagerSetup();foreach(var item in scenes)if(item.isLoaded&&SceneManager.GetSceneByPath(item.path).isDirty)throw new Exception("Save edited scenes first.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new SavedScenes{scenes=scenes}));SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);SessionState.SetBool(Key,true);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.ModeId="demo";MatchSetup.MapId="castle";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;
            File.WriteAllText(Report,"Actual Game + original audio import fixtures + jaguar/health rendering + full-size death and synthetic XR checks. No physical headset.\n");EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Check(bool pass,string text){File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+text+"\n");if(!pass)throw new Exception(text);checks++;}
        static void Log(string message,string trace,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}}
        static IEnumerator Guard()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Checks());
            while(stack.Count>0){bool more=false;object current=null;Exception error=null;try{more=stack.Peek().MoveNext();if(more)current=stack.Peek().Current;}catch(Exception e){error=e;}if(error!=null){failed=true;File.AppendAllText(Report,"FAIL "+error+"\n");break;}if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}if(current is IEnumerator child){stack.Push(child);continue;}yield return current;}
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();File.AppendAllText(Report,"COMPLETE checks="+checks+" runtimeErrors="+errors+"\n");
            if(Application.isBatchMode){SessionState.SetBool(Key,false);SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);File.AppendAllText(Report,"INFO Batch run leaves scene edits unsaved and exits\n");EditorApplication.Exit(failed||errors>0?1:0);}else EditorApplication.ExitPlaymode();
        }
        static IEnumerator Checks()
        {
            float deadline=Time.realtimeSinceStartup+40;while(!(Services.Session is CastlePlaySession)&&Time.realtimeSinceStartup<deadline)yield return null;
            var session=Services.Session as CastlePlaySession;Check(session,"Game initializes");session.SetRole(false);session.SetMenu(false);session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();if(session.Social!=null)session.Social.VoiceEnabled=false;
            var rules=session.Modes.Rules.Copy();rules.countdownSeconds=0;rules.automaticDisasters=false;rules.healthEnabled=true;rules.fallDamage=false;rules.island=IslandRule.Walkable;session.Modes.Configure(rules);session.Modes.StartRound();
            if(SessionState.GetBool(Key+"InputOnly",false)){yield return DeadlyJenga.CastlePrototype.Editor.CastleInputValidation.Validate(session,true);Check(!File.ReadAllText(ProjectPath.Report("InputValidation.txt")).Contains("FAIL "),"Synthetic XR input and third-person restoration regressions pass");yield break;}
            var player=session.Survivor();player.InputSuspended=true;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Temporary feature capture floor";floor.layer=Layers.Surface;floor.transform.position=new Vector3(2000,10,2000);floor.transform.localScale=new Vector3(8,1,8);
            GameObject enemy=null;
            try
            {
                player.Teleport(floor.transform.position+Vector3.up*.54f,Quaternion.identity);yield return new WaitForSeconds(.25f);
                yield return WeaponPresentationChecks.Run();Check(File.ReadAllText(ProjectPath.Report("WeaponPresentation.txt")).Contains("COMPLETE checks="),"Original gun sounds and bounded bullet fixtures pass");
                Check(JaguarSuiteChecks.ValidateSounds()>=36,"All nine Jaguar sound families and 27 PCM variants validate");
                var population=session.SurvivorBots as IEnemyPopulation;Check(population!=null,"Existing enemy population contract is present");enemy=population.SpawnEnemy(new EnemySpawnSettings{archetypeId="jaguar",hitDamage=20});Check(enemy,"Jaguar spawns through the real enemy population");
                var motor=enemy.GetComponent<CastleSurvivor>();motor.InputSuspended=true;motor.Teleport(floor.transform.position+new Vector3(1.5f,.54f,0),Quaternion.identity);yield return new WaitForSeconds(.3f);
                var avatar=enemy.GetComponent<CandyPlayerAvatar>();Check(JaguarSuiteChecks.ValidateAppearance(avatar.Rig)>20,"Actual enemy retains the 44-bone rig and complete rosette shell");
                Capture(motor.transform.position+new Vector3(1.25f,.9f,1.8f),motor.transform.position+Vector3.up*.48f,"Jaguar-Front.png");Capture(motor.transform.position+new Vector3(-1.5f,1,-1.7f),motor.transform.position+Vector3.up*.45f,"Jaguar-Rear.png");
                var ownAvatar=player.GetComponent<CandyPlayerAvatar>();var savedAppearance=ownAvatar.Appearance;
                try
                {
                    ownAvatar.ApplyAppearance(new AvatarAppearance{accessories=AvatarAccessory.PantherHead|AvatarAccessory.PantherClaws|AvatarAccessory.PantherTail});yield return new WaitForSeconds(.25f);
                    yield return JaguarSuiteChecks.ValidateHealthFrames(ownAvatar.Rig,n=>Check(n>20,"Health-enabled player avatar retains rosette coat and normal map across frames"));
                    yield return MeltControlChecks.Play(session,Check,(melt,name)=>{var bounds=melt.CurrentBodyBounds;Capture(bounds.center+new Vector3(1.2f,.75f,1.6f),bounds.center,name+".png");});
                }
                finally{ownAvatar.ApplyAppearance(savedAppearance);}
                var beforeDeath=new HashSet<CandyMelt>(Object.FindObjectsByType<CandyMelt>(FindObjectsSortMode.None));
                enemy.GetComponent<IDamageable>().Eliminate(player.gameObject);yield return null;
                CandyMelt natural=null;foreach(var candidate in Object.FindObjectsByType<CandyMelt>(FindObjectsSortMode.None))if(!beforeDeath.Contains(candidate)){natural=candidate;break;}
                Check(natural&&natural.SnapshotBounds.size.y>.6f&&natural.MeltProgress==0,"Actual NPC elimination creates a full-size ragdoll before the source is destroyed");
                if(natural){Capture(natural.CurrentBodyBounds.center+new Vector3(1.2f,.75f,1.6f),natural.CurrentBodyBounds.center,"Jaguar-Natural-Death.png");Object.Destroy(natural.gameObject);}
                Check(EditorApplication.ExecuteMenuItem("Tools/Candy Audio/Validate Bank"),"Audio bank validator executes with new fallback cue families");
                foreach(string shaderName in new[]{"CandyAvatarHealth.shader","CandyDeathMelt.shader"}){var shader=AssetDatabase.LoadAssetAtPath<Shader>(ProjectPath.Find(shaderName));Check(shader&&!ShaderUtil.ShaderHasError(shader),"Updated texture-preserving shader compiles: "+shaderName);}
                yield return CastleVariantChecks.Play();Check(File.ReadAllText(ProjectPath.Report("CastleVariants.txt")).Contains("COMPLETE checks="),"Both castle variants pass actual Play Mode gravity and map loading");
                yield return DeadlyJenga.CastlePrototype.Editor.CastleInputValidation.Validate(session,true);Check(!File.ReadAllText(ProjectPath.Report("InputValidation.txt")).Contains("FAIL "),"Synthetic XR input and third-person restoration regressions pass");
                Check(errors==0,"No unexpected runtime errors");
            }
            finally{if(enemy)Object.Destroy(enemy);if(floor)Object.Destroy(floor);}
        }
        static void Capture(Vector3 position,Vector3 target,string name)=>typeof(QueuedFeatureChecks).GetMethod("Capture",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{position,target,name});
    }
    public sealed class UserFeatureDriver:MonoBehaviour { }
}
