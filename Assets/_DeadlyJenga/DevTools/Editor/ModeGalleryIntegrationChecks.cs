using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class ModeGalleryIntegrationChecks
    {
        const string Key="ModeGalleryIntegrationChecks";
        [Serializable] sealed class Scenes {public SceneSetup[] scenes;}
        static int checks,errors;static bool failed;
        static string Report=>ProjectPath.Report("ModeGallery.txt");
        static ModeGalleryIntegrationChecks()
        {
            EditorApplication.playModeStateChanged+=state=>{
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode){checks=errors=0;failed=false;Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>new GameObject("Action integration validation").AddComponent<ModeGalleryCheckDriver>().StartCoroutine(Guard());}
                if(state==PlayModeStateChange.EnteredEditMode){Restore();var saved=JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes",""));if(saved.scenes.Any(s=>s.isActive&&s.isLoaded))EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Lobby Picture Gallery and Mode Map Selection")]
        public static void Run()=>Start(false);
        public static void RunGalleryOnly()=>Start(true);
        static void Start(bool lifecycle)
        {
            SessionState.SetBool(Key+"Lifecycle",lifecycle);
            var scenes=EditorSceneManager.GetSceneManagerSetup();if(scenes.Any(s=>s.isLoaded&&SceneManager.GetSceneByPath(s.path).isDirty))throw new Exception("Save edited scenes first.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{scenes=scenes}));SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);SessionState.SetBool(Key,true);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.ModeId="demo";MatchSetup.MapId="castle";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;
            File.WriteAllText(Report,"Actual Lobby picture picker, mode-first compatible maps, two real Photon peers and solo Game startup. No headset.\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));EditorApplication.EnterPlaymode();
        }
        static void Restore(){Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);}
        static void Log(string message,string trace,UnityEngine.LogType type)
        {
            if(type!=UnityEngine.LogType.Error&&type!=UnityEngine.LogType.Exception&&type!=UnityEngine.LogType.Assert)return;
            if(NetworkProjectConfig.Global.PeerMode==NetworkProjectConfig.PeerModes.Multiple&&message.EndsWith("PeerModes.Multiple requires a scene to be set in StartGameArgs.Scene. Set a scene or disable Multiple Peer mode in the NetworkProjectConfig.",StringComparison.Ordinal)){File.AppendAllText(Report,"EDITOR NOTE Known Fusion diagnostic for an additional transport peer without a game scene.\n");return;}
            errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");
        }
        static void Check(bool pass,string text){File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+text+"\n");if(!pass)throw new Exception(text);checks++;}
        static IEnumerator Guard()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Checks());
            while(stack.Count>0){bool more=false;object current=null;Exception error=null;try{more=stack.Peek().MoveNext();if(more)current=stack.Peek().Current;}catch(Exception e){error=e;}if(error!=null){failed=true;File.AppendAllText(Report,"FAIL "+error+"\n");break;}if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}if(current is IEnumerator child){stack.Push(child);continue;}yield return current;}
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();File.AppendAllText(Report,"COMPLETE checks="+checks+" runtimeErrors="+errors+"\n");
            if(Application.isBatchMode){Restore();EditorApplication.Exit(failed||errors>0?1:0);}else EditorApplication.ExitPlaymode();
        }
        static IEnumerator Checks()
        {
            float until=Time.realtimeSinceStartup+40;while(!(Services.Session is IslandLobby)&&Time.realtimeSinceStartup<until)yield return null;
            Check(Services.Session is IslandLobby,"Actual Lobby initializes");yield return new WaitForSeconds(.3f);
            if(!SessionState.GetBool(Key+"Lifecycle",false))ModeMapSelectionChecks.Run(Check);
            yield return ModePicturePickerChecks.Run(Check);
            if(SessionState.GetBool(Key+"Lifecycle",false)){Check(errors==0,"Final gallery and support geometry checks have no runtime errors");yield break;}
            yield return ModeSelectionNetworkChecks.Run(Check);
            Check(errors==0,"Lobby gallery and two-peer selector checks have no unexpected errors");
            var driver=UnityEngine.Object.FindFirstObjectByType<ModeGalleryCheckDriver>();UnityEngine.Object.DontDestroyOnLoad(driver.gameObject);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.ModeId="demo";MatchSetup.MapId="castle";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ProjectPath.Find("Game.unity"),new LoadSceneParameters(LoadSceneMode.Single));
            until=Time.realtimeSinceStartup+40;while(!(Services.Session is CastlePlaySession)&&Time.realtimeSinceStartup<until)yield return null;
            var session=Services.Session as CastlePlaySession;Check(session,"Actual Game still enters solo Play Mode after the lobby selection changes");
            session.SetRole(false);session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();if(session.Social!=null)session.Social.VoiceEnabled=false;
            session.Modes.StartRound();yield return new WaitForSeconds(.2f);Check(session.Player!=null&&session.Modes.Frame.players.Length>0,"Solo survivor and game mode frame initialize");Check(errors==0,"Game startup has no runtime errors");
        }
    }
    public sealed class ModeGalleryCheckDriver:MonoBehaviour { }
}
