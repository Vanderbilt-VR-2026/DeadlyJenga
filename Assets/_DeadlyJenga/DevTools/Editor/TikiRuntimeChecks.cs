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
    [InitializeOnLoad] public static class TikiRuntimeChecks
    {
        const string Key="TikiRuntimeChecks";
        [Serializable] sealed class Scenes {public SceneSetup[] scenes;}
        static double began;
        static float readyAt;
        static int errors;
        static bool ready;
        static int captureIndex;
        static string Report=>ProjectPath.Report("Tiki-runtime.txt");
        static TikiRuntimeChecks()
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
        [MenuItem("Tools/Deadly Jenga/Validation/Check Tiki Map Runtime")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode before validating.");
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>string.IsNullOrEmpty(s.path)||s.isLoaded&&SceneManager.GetSceneByPath(s.path).isDirty))throw new Exception("Save edited scenes before running the resort check.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{scenes=scenes}));
            SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);
            MatchSetup.MapId="mode-tiki-resort";MatchSetup.ModeId="demo";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;
            MatchSetup.FromLobby=true;MatchSetup.StartLocalSession=true;SessionState.SetBool(Key,true);
            File.WriteAllText(Report,"Actual Game scene and survivor, resort map. Desktop only.\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string stack,LogType kind)
        {if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n");}}
        static void Tick()
        {
            string request=ProjectPath.Report("Tiki.runtime.request");
            if(File.Exists(request)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating)
            {File.Delete(request);try{Run();}catch(Exception e){File.WriteAllText(Report,"NOT RUN "+e.Message);}return;}
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            try
            {
                var session=Services.Session;var map=Services.World as TikiResortMap;
                if(session?.Player!=null&&!map)
                {
                    MatchSetup.MapId="mode-tiki-resort";MatchSetup.ModeId="demo";MatchSetup.FromLobby=false;
                    Require(MatchSetup.SelectedMap?.id=="mode-tiki-resort","Lobby catalog resolves Waterfront Tiki Resort after the editor domain reload");
                    session.SurvivorBots?.Clear();session.HandBots?.Clear();
                    if(session.Social!=null)session.Social.VoiceEnabled=false;
                    MatchSetup.ApplyMap();return;
                }
                if(!map||session?.Player==null){if(EditorApplication.timeSinceStartup-began>35)throw new Exception("Tiki/player initialization timed out.");return;}
                if(captureIndex>0)
                {
                    Photo(session.ActiveCamera,map,captureIndex-1);captureIndex++;
                    if(captureIndex<=5)return;
                    Require(errors==0,"No runtime errors observed");File.AppendAllText(Report,"COMPLETE Desktop startup, standing, route and six actual game-camera captures passed. No headset or two-device test.\n");EditorApplication.ExitPlaymode();return;
                }
                if(!ready)
                {
                    Require(session.Player.ViewCamera,"Survivor initialized on the selected resort map");
                    session.Player.Place(map.SafeSpawnPoint(),Quaternion.identity);readyAt=Time.time;ready=true;return;
                }
                if(Time.time-readyAt<3)return;
                var character=session.Player as Component;Require(character,"Player has a scene body");
                Require((character.transform.position-map.SafeSpawnPoint()).sqrMagnitude<4,"Standing survivor remains safely on the floor after three seconds");
                Require(!map.IsSheltered(map.SafeSpawnPoint()),"Central seating is open to the sky");
                int mask=1<<Layers.Surface;
                var aisle=new[]{new Vector3(3.4f,.4f,-6.5f),new Vector3(3.4f,.4f,5.5f),new Vector3(4.2f,.4f,5.5f),new Vector3(4.2f,.4f,9.2f)};bool route=true;float scale=map.transform.lossyScale.y;
                for(int i=1;i<aisle.Length;i++){var start=map.transform.TransformPoint(aisle[i-1]);var end=map.transform.TransformPoint(aisle[i]);route&=!Physics.CapsuleCast(start,map.transform.TransformPoint(aisle[i-1]+Vector3.up*1.2f),.3f*scale,(end-start).normalized,out _,Vector3.Distance(start,end),mask,QueryTriggerInteraction.Ignore);}
                Require(route,"Main aisle provides a standing-width route through the cabana opening");
                Require(Mathf.Abs(session.ActiveCamera.transform.position.y-character.transform.position.y-.8f)<.25f,"Avatar eye height matches the half-scale room and furniture");
                Require(map.GetComponentInChildren<DeadlyJenga.CandyArt.TikiResortAtmosphere>(),"Tiki presentation is active in Game");
                Require(!Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Any(l=>l.type==LightType.Directional&&l.enabled&&!l.transform.IsChildOf(map.transform)),"Resort controls one sun without outside lighting interference");
                Require(!Object.FindObjectsByType<DeadlyJenga.CandyArt.CandyEnvironment>(FindObjectsSortMode.None).SelectMany(e=>e.GetComponentsInChildren<Renderer>()).Any(r=>r.enabled),"External island scenery cannot intersect the resort floor");
                Require(map.IsSheltered(map.transform.TransformPoint(new Vector3(-10,1,0))),"Thatched bar provides physical shelter");
                var stairStart=map.transform.TransformPoint(new Vector3(8,.4f,-9.5f));
                Require(!Physics.CapsuleCast(stairStart,stairStart+Vector3.up*.6f,.15f,Vector3.back,out _,2.1f,1<<Layers.Surface,QueryTriggerInteraction.Ignore),"Beach stair opening is free of railing blockers");
                Require(map.GetComponentsInChildren<Collider>().Count(c=>c.name=="Cabana post")==24,"All twenty-four cabana posts have simple collision");
                Require(Physics.Raycast(map.transform.TransformPoint(new Vector3(22,1,0)),Vector3.down,3,1<<Layers.Surface),"Beach scenery matches a walkable ground collider");
                Require(map.PlayableBounds.Contains(map.transform.TransformPoint(new Vector3(22,-.45f,0))),"World bounds include the lower beach surface");
                var lagoon=map.transform.Find("Visual/Turquoise waterfront").GetComponent<Renderer>();var properties=new MaterialPropertyBlock();lagoon.GetPropertyBlock(properties);
                Require(Mathf.Abs(properties.GetFloat("_WaterHeight")-map.WaterHeight(map.SafeSpawnPoint()))<.01f,"Visible lagoon height matches the world water query");
                var pierStart=map.transform.TransformPoint(new Vector3(0,.4f,-11));
                Require(!Physics.CapsuleCast(pierStart,pierStart+Vector3.up*.6f,.15f,Vector3.back,out _,10.5f,1<<Layers.Surface,QueryTriggerInteraction.Ignore),"Pier has a clear standing route to the waterfront landing");
                Require(map.GetComponentsInChildren<Renderer>().Count(r=>r.enabled)<60,"Shared-material batches keep visible renderer count below sixty");
                var camera=session.ActiveCamera;Require(camera,"Actual player camera is active");Capture(camera);
                captureIndex=1;
            }
            catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");EditorApplication.ExitPlaymode();}
        }
        static void Require(bool ok,string message){if(!ok)throw new Exception(message);File.AppendAllText(Report,"PASS "+message+"\n");}
        static void Photo(Camera camera,TikiResortMap map,int index)
        {
            Vector3[] positions={new Vector3(3,1.7f,-7),new Vector3(-4,2,-2),new Vector3(8,2,-16),new Vector3(25,3,-12),new Vector3(45,33,-48)};
            Vector3[] targets={new Vector3(-6,2,2),new Vector3(-11,2,.2f),new Vector3(0,1,-30),new Vector3(-2,1,0),new Vector3(0,0,-5)};
            string[] names={"bar-seating","tiki-bar","waterfront","beach","overview"};var pose=new Pose(camera.transform.position,camera.transform.rotation);
            try{camera.transform.position=map.transform.TransformPoint(positions[index]);camera.transform.LookAt(map.transform.TransformPoint(targets[index]));Capture(camera,"Tiki-"+names[index]+".png");}
            finally{camera.transform.SetPositionAndRotation(pose.position,pose.rotation);}
            File.AppendAllText(Report,"PASS Actual game-camera capture: "+names[index]+"\n");
        }
        static void Capture(Camera camera,string filename="Tiki-runtime-player.png")
        {
            var before=camera.targetTexture;var old=RenderTexture.active;var rt=new RenderTexture(1400,900,24);var tex=new Texture2D(1400,900,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1400,900),0,0);tex.Apply();File.WriteAllBytes(ProjectPath.Report(filename),tex.EncodeToPNG());}
            finally{camera.targetTexture=before;RenderTexture.active=old;Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);}
        }
    }
}
