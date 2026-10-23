using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using DeadlyJenga.Core;
using Fusion.Photon.Realtime;
using Object=UnityEngine.Object;
using DeadlyJenga.DevTools.Editor;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad] public static class LobbyStationChecks
    {
        const string Key="LobbyStationChecks",Request="Logs/Validation/LobbyStations.request";
        static int errors;
        static LobbyStationChecks()
        {
            EditorApplication.update+=()=>{if(File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating){File.Delete(Request);Run();}};
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode){errors=0;Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>IslandLobby.Current.StartCoroutine(Guard(Check()));}
                if(state==PlayModeStateChange.EnteredEditMode){Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);string original=SessionState.GetString(Key+"Original","");if(File.Exists(original))EditorSceneManager.OpenScene(original);}
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validate Three Lobby Stations")]
        public static void Run()
        {
            File.WriteAllText(ProjectPath.Report("LobbyStations.txt"),"Three lobby stations / giant physical keyboard\n");
            SessionState.SetString(Key+"Original",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));EditorApplication.EnterPlaymode();
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Lobby Population")]
        public static void RunPopulation(){Run();SessionState.SetBool(Key+"PopulationOnly",true);}
        static void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(ProjectPath.Report("LobbyStations.txt"),"ERROR "+message+"\n"+trace+"\n");}}
        static void Require(bool ok,string message){File.AppendAllText(ProjectPath.Report("LobbyStations.txt"),(ok?"PASS ":"FAIL ")+message+"\n");if(!ok)throw new Exception(message);}
        static IEnumerator Guard(IEnumerator test)
        {
            var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(test);
            while(stack.Count>0){bool next=false;object value=null;Exception error=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){File.AppendAllText(ProjectPath.Report("LobbyStations.txt"),"FAIL "+error+"\n");if(IslandLobby.Current?.Room!=null)_=IslandLobby.Current.Room.Leave();EditorApplication.ExitPlaymode();yield break;}
                if(!next){(stack.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator child){stack.Push(child);continue;}yield return value;}
            EditorApplication.ExitPlaymode();
        }
        static IEnumerator Until(Func<bool> condition,string label,float seconds=45)
        {float end=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;Require(condition(),label);}
        static IEnumerator Check()
        {
            yield return new WaitForSecondsRealtime(1);
            if(SessionState.GetBool(Key+"PopulationOnly",false)){SessionState.SetBool(Key+"PopulationOnly",false);yield return Population();yield break;}
            var lobby=IslandLobby.Current;var room=lobby.Room;lobby.SetMenu(true);
            Require(lobby.GetComponentsInChildren<LobbyTerminalFaces>().Length==3,"Exactly three stations replace the previous terminal cluster");
            Require(lobby.Survivor().viewCamera.clearFlags==CameraClearFlags.Skybox&&RenderSettings.skybox==lobby.View().Environment.profile.sky,"Lobby displays the castle skybox through the shared character camera");
            Require(lobby.JoinStationEnabled&&!lobby.SetupStationEnabled,"Joining starts active while map and mode choices start locked");
            Require(!lobby.View().InvokeChoice("player-target"),"Population control is locked before hosting");
            var resort=lobby.GetComponent<DeadlyJenga.CandyArt.CandyLobbyResort>();
            Require(resort&&resort.transform.Find("Visual \u00b7 Candy island resort"),"Optional candy resort builds its own Visual child");
            var dressing=resort.transform.Find("Visual \u00b7 Candy island resort");
            Require(dressing.GetComponentsInChildren<Collider>().Length==0&&dressing.GetComponentsInChildren<Rigidbody>().Length==0,"Resort art adds no colliders or rigidbodies");
            var artMeshes=dressing.GetComponentsInChildren<MeshFilter>();
            Require(artMeshes.Length<=12&&artMeshes.Sum(m=>m.sharedMesh.vertexCount)<85000,"Resort and milkshake geometry stay within twelve static color batches and 85k vertices");
            var keyboard=lobby.View().GiantKeyboard.GetComponent<RectTransform>();
            Require(Mathf.Abs(keyboard.rect.width*keyboard.lossyScale.x-2.2f)<.01f&&keyboard.GetComponent<BoxCollider>()&&!keyboard.GetComponent<Rigidbody>(),"Oversized piano keyboard has a solid static case at the matching 2.2 metre screen width");
            Require(keyboard.GetComponentsInChildren<MeshRenderer>().Length>=40,"Every keyboard key has a raised rounded 3D cap");
            foreach(char c in "ASIA-ABC234")Require(lobby.View().InvokeChoice("key-"+c),"Keyboard accepts "+c);
            Require(lobby.EnteredCode=="ASIA-ABC234"&&IslandRoomAddress.TryParse(lobby.EnteredCode,room.Region,out _,out _),"Keyboard supports full region-qualified room codes");
            lobby.View().InvokeChoice("clear-code");
            var key=lobby.GetComponentsInChildren<BoxCollider>().First(c=>c.name=="key-Z");Physics.SyncTransforms();var point=key.transform.TransformPoint(new Vector3(0,0,-55));
            Require(lobby.View().TouchKeyboard(point,0)&&lobby.EnteredCode=="Z","Physical fingertip contact presses a 3D key");
            Require(!lobby.View().TouchKeyboard(point,0)&&lobby.EnteredCode=="Z","Holding a fingertip on a key does not repeat characters");
            lobby.View().TouchKeyboard(Vector3.one*500,0);yield return new WaitForSecondsRealtime(.2f);
            Require(lobby.View().TouchKeyboard(point,0)&&lobby.EnteredCode=="ZZ","Lifting and pressing again re-arms a physical key");
            lobby.View().InvokeChoice("clear-code");Require(lobby.View().InvokeChoice("code"),"The large code box focuses desktop typing");
            foreach(char c in "EU-ABC234")InputSystem.QueueTextEvent(Keyboard.current,c);yield return null;
            Require(lobby.EnteredCode=="EU-ABC234","Desktop keyboard text enters the same world-space code field");
            Require(lobby.View().InvokeChoice("detect-nearby"),"Large Detect Nearby button opens the picker");
            var app=PhotonAppSettings.Global.AppSettings;string protocol=(app.AppVersion??"")+"/"+lobby.catalog.NetworkVersion;
            using(var nearby=new IslandDiscovery(app.AppIdFusion,protocol,0))
            {Require(room.Discovery.Receive(nearby.EncodePresence("US-ABC234",2),new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,47738),Time.unscaledTime),"Compatible LAN rooms populate the nearby picker");}
            yield return null;Require(lobby.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.text.Contains("JOIN US-ABC234")),"Nearby result displays its regular Fusion join code");
            Require(lobby.View().InvokeChoice("code-instead"),"Nearby picker can return to direct code entry");yield return null;
            Capture(lobby,"lobby-three-stations-idle.png",new Vector3(0,3.2f,-7.5f),new Vector3(0,1.25f,.6f));
            Capture(lobby,"lobby-piano-keyboard.png",new Vector3(-3.1f,2.1f,-2.5f),new Vector3(-3.05f,.9f,.1f));
            Capture(lobby,"lobby-resort-overview.png",new Vector3(9,7,-11),new Vector3(0,1.8f,1.1f));
            Capture(lobby,"lobby-resort-arrival.png",new Vector3(0,1.65f,-4.4f),new Vector3(0,2.25f,2.8f));
            Require(lobby.View().InvokeChoice("host"),"Become Host is available on the second station");
            Require(!lobby.JoinStationEnabled&&!lobby.View().InvokeChoice("key-A")&&!lobby.View().InvokeChoice("detect-nearby"),"Choosing host immediately deactivates join controls and keyboard");
            yield return Until(()=>!room.Busy,"Host connection completes");Require(room.IsHost,"Real Fusion hosting succeeds: "+room.Status);
            yield return null;
            Require(lobby.SetupStationEnabled&&!lobby.JoinStationEnabled,"Hosting unlocks Map + Mode while keeping Join disabled");
            MatchChoices.BotTarget=10;yield return null;
            foreach(int target in new[]{15,25,50,100,10})
            {
                Require(lobby.View().InvokeChoice("player-target")&&MatchChoices.BotTarget==target,"Host population selector chooses "+target);
                yield return Until(()=>room.Runner.SessionInfo.Properties.TryGetValue("population",out var value)&&(int)value==target,"Fusion publishes total population "+target);
            }
            Require(lobby.View().InvokeChoice("invite-all"),"Host can invite nearby players from the host station");
            Require(lobby.View().InvokeChoice("mode-placeholder")&&!room.CanLaunch,"Placeholder remains unavailable for launch");lobby.View().InvokeChoice("mode-demo");
            int map=room.MapIndex;lobby.View().InvokeChoice("map-next");lobby.View().InvokeChoice("map-prev");Require(room.MapIndex==map&&room.CanLaunch,"Compact map arrows and mode selection preserve launch eligibility");
            Capture(lobby,"lobby-three-stations-hosting.png",new Vector3(0,3.2f,-7.5f),new Vector3(0,1.25f,.6f));
            Require(lobby.View().InvokeChoice("abandon-host"),"Abandon Hosting uses the existing clean room-leave flow");yield return Until(()=>!room.Busy&&!room.Connected,"Abandoning hosting closes the Fusion room");yield return null;
            Require(lobby.JoinStationEnabled&&!lobby.SetupStationEnabled,"Leaving hosting restores Join and locks Map + Mode");
            Require(errors==0,"Lobby stations and input run without errors");
            File.AppendAllText(ProjectPath.Report("LobbyStations.txt"),"COMPLETE: editor contact simulation, native text input, LAN packet receive and real Fusion host/leave. Physical headset touch comfort not tested.\n");
        }
        static void Capture(IslandLobby lobby,string name,Vector3 position,Vector3 target)
        {
            var go=new GameObject("Temporary station screenshot camera");var camera=go.AddComponent<Camera>();camera.fieldOfView=65;camera.transform.position=position;camera.transform.LookAt(target);
            var rt=new RenderTexture(1600,1000,24);var old=RenderTexture.active;Texture2D pixels=null;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();File.WriteAllBytes(ProjectPath.Report(name),pixels.EncodeToPNG());}
            finally{RenderTexture.active=old;Object.Destroy(rt);if(pixels)Object.Destroy(pixels);Object.Destroy(go);}
        }
        static IEnumerator Population()
        {
            var lobby=IslandLobby.Current;var room=lobby.Room;lobby.SetMenu(true);
            Require(!lobby.View().InvokeChoice("player-target"),"Population selection is locked before hosting");
            Require(lobby.View().InvokeChoice("host"),"Host connects through the real lobby terminal");
            yield return Until(()=>!room.Busy,"Host connection completes");Require(room.IsHost,"Real Fusion host owns population controls");
            MatchChoices.BotTarget=10;yield return null;
            foreach(int target in new[]{15,25,50,100,10})
            {
                Require(lobby.View().InvokeChoice("player-target")&&MatchChoices.BotTarget==target,"Actual host terminal selects "+target);
                yield return Until(()=>room.Runner.SessionInfo.Properties.TryGetValue("population",out var value)&&(int)value==target,"Room publishes population "+target);
            }
            yield return new WaitForSeconds(.3f);
            Capture(lobby,"Repair-LobbyPopulation.png",new Vector3(3.05f,1.65f,-1),new Vector3(3.05f,1.62f,1));
            Require(lobby.View().InvokeChoice("abandon-host"),"Host leaves through the terminal");yield return Until(()=>!room.Connected&&!room.Busy,"Room closes cleanly");
            Require(!lobby.View().InvokeChoice("player-target")&&errors==0,"Population locks again without runtime errors");
            File.AppendAllText(ProjectPath.Report("LobbyStations.txt"),"COMPLETE population selector checks. No physical headset used.\n");
        }
    }
}
