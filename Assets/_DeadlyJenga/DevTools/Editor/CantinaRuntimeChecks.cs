using System;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class CantinaRuntimeChecks
    {
        const string Key="CantinaRuntimeChecks";
        [Serializable] sealed class Scenes {public SceneSetup[] scenes;}
        static double began;
        static float readyAt;
        static int errors;
        static bool ready;
        static int captureIndex;
        static string Report=>ProjectPath.Report("Cantina-runtime.txt");
        static CantinaRuntimeChecks()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode){began=EditorApplication.timeSinceStartup;errors=0;ready=false;captureIndex=0;Application.logMessageReceived+=Log;}
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
                    SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
                    MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
                    EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes","")).scenes);
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Cantina Map Runtime")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode before validating.");
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>string.IsNullOrEmpty(s.path)||s.isLoaded&&SceneManager.GetSceneByPath(s.path).isDirty))throw new Exception("Save edited scenes before running the cantina check.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{scenes=scenes}));
            SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);
            MatchSetup.MapId="mode-cantina";MatchSetup.ModeId="demo";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;
            MatchSetup.FromLobby=true;MatchSetup.StartLocalSession=true;SessionState.SetBool(Key,true);
            File.WriteAllText(Report,"Actual Game scene and survivor, cantina map. Desktop only.\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string stack,LogType kind)
        {if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n");}}
        static void Tick()
        {
            string request=ProjectPath.Report("Cantina.runtime.request");
            if(File.Exists(request)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating)
            {File.Delete(request);try{Run();}catch(Exception e){File.WriteAllText(Report,"NOT RUN "+e.Message);}return;}
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            try
            {
                var session=Services.Session;var map=Services.World as CantinaMap;
                if(session?.Player!=null&&!map)
                {
                    MatchSetup.MapId="mode-cantina";MatchSetup.ModeId="demo";MatchSetup.FromLobby=false;
                    Require(MatchSetup.SelectedMap?.id=="mode-cantina","Lobby catalog resolves Amber Cantina after the editor domain reload");
                    session.SurvivorBots?.Clear();session.HandBots?.Clear();
                    if(session.Social!=null)session.Social.VoiceEnabled=false;
                    MatchSetup.ApplyMap();return;
                }
                if(!map||session?.Player==null){if(EditorApplication.timeSinceStartup-began>35)throw new Exception("Cantina/player initialization timed out.");return;}
                if(captureIndex>0)
                {
                    Photo(session.ActiveCamera,map,captureIndex-1);captureIndex++;
                    if(captureIndex<=5)return;
                    Require(errors==0,"No runtime errors observed");File.AppendAllText(Report,"COMPLETE Desktop startup, standing, route and six actual game-camera captures passed. No headset or two-device test.\n");EditorApplication.ExitPlaymode();return;
                }
                if(!ready)
                {
                    Require(session.Player.ViewCamera,"Survivor initialized on the selected cantina map");
                    session.Player.Place(map.SafeSpawnPoint(),Quaternion.identity);readyAt=Time.time;ready=true;return;
                }
                if(Time.time-readyAt<3)return;
                var character=session.Player as Component;Require(character,"Player has a scene body");
                Require((character.transform.position-map.SafeSpawnPoint()).sqrMagnitude<4,"Standing survivor remains safely on the floor after three seconds");
                Require(map.IsSheltered(map.SafeSpawnPoint()),"Player spawn is sheltered by the completed ceiling");
                int mask=1<<Layers.Surface;
                Vector3 a=new Vector3(3.4f,.4f,-6.5f),b=new Vector3(3.4f,.4f,8);
                var start=map.transform.TransformPoint(a);var end=map.transform.TransformPoint(b);float scale=map.transform.lossyScale.y;
                Require(!Physics.CapsuleCast(start,map.transform.TransformPoint(a+Vector3.up*1.2f),.3f*scale,(end-start).normalized,out _,Vector3.Distance(start,end),mask,QueryTriggerInteraction.Ignore),"Main aisle has a continuous standing-width route to the door wall");
                Require(Mathf.Abs(session.ActiveCamera.transform.position.y-character.transform.position.y-.8f)<.25f,"Avatar eye height matches the half-scale room and furniture");
                Require(map.GetComponentInChildren<DeadlyJenga.CandyArt.CantinaAtmosphere>(),"Cantina presentation is active in Game");
                Require(!Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Any(l=>l.type==LightType.Directional&&l.enabled),"Outdoor sunlight is disabled inside the cantina");
                Require(!Object.FindObjectsByType<DeadlyJenga.CandyArt.CandyEnvironment>(FindObjectsSortMode.None).SelectMany(e=>e.GetComponentsInChildren<Renderer>()).Any(r=>r.enabled),"External island scenery cannot intersect the cantina floor");
                var camera=session.ActiveCamera;Require(camera,"Actual player camera is active");Capture(camera);
                captureIndex=1;
            }
            catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");EditorApplication.ExitPlaymode();}
        }
        static void Require(bool ok,string message){if(!ok)throw new Exception(message);File.AppendAllText(Report,"PASS "+message+"\n");}
        static void Photo(Camera camera,CantinaMap map,int index)
        {
            Vector3[] positions={new Vector3(2.8f,1.65f,-6.5f),new Vector3(-5,1.65f,-2),new Vector3(7,1.65f,-.2f),new Vector3(-1.8f,1.65f,-4.5f),new Vector3(22,23,-25)};
            Vector3[] targets={new Vector3(0,1.9f,9),new Vector3(-11,1.4f,1),new Vector3(11.594f,1.8f,4.3f),new Vector3(-7,1.7f,-9),new Vector3(0,0,1)};
            string[] names={"doors","bar","tanks","band","overview"};var pose=new Pose(camera.transform.position,camera.transform.rotation);
            var ceiling=map.transform.Find("Visual/Inferred room enclosure/Ceiling");var south=map.transform.Find("Visual/Inferred room enclosure/South wall");
            try
            {
                if(index==4){ceiling.gameObject.SetActive(false);south.gameObject.SetActive(false);}
                camera.transform.position=map.transform.TransformPoint(positions[index]);camera.transform.LookAt(map.transform.TransformPoint(targets[index]));
                Capture(camera,"Cantina-"+names[index]+".png");
            }
            finally{camera.transform.SetPositionAndRotation(pose.position,pose.rotation);if(index==4){ceiling.gameObject.SetActive(true);south.gameObject.SetActive(true);}}
            File.AppendAllText(Report,"PASS Actual game-camera capture: "+names[index]+(index==4?" (roof-off cutaway)":"")+"\n");
        }
        static void Capture(Camera camera,string filename="Cantina-runtime-player.png")
        {
            var before=camera.targetTexture;var old=RenderTexture.active;var rt=new RenderTexture(1400,900,24);var tex=new Texture2D(1400,900,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1400,900),0,0);tex.Apply();File.WriteAllBytes(ProjectPath.Report(filename),tex.EncodeToPNG());}
            finally{camera.targetTexture=before;RenderTexture.active=old;Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);}
        }
    }
}
