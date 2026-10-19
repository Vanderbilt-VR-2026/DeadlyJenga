using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using Fusion.Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class LobbyArcadeChecks
    {
        const string Key="LobbyArcadeChecks";
        static int errors;
        static string Report=>ProjectPath.Report("LobbyArcade.txt");
        static LobbyArcadeChecks()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {errors=0;Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>IslandLobby.Current.StartCoroutine(Guard(Check()));}
                if(state==PlayModeStateChange.EnteredEditMode)
                {Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);var path=SessionState.GetString(Key+"Original","");if(File.Exists(path))EditorSceneManager.OpenScene(path);}
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Lobby Arcade %&i")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            File.WriteAllText(Report,"Actual lobby arcade keyboard and joining regression\n");
            SessionState.SetString(Key+"Original",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}}
        static void Require(bool ok,string message)
        {File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+message+"\n");if(!ok)throw new Exception(message);}
        static IEnumerator Guard(IEnumerator test)
        {
            while(true)
            {
                object value=null;bool next;
                try{next=test.MoveNext();if(next)value=test.Current;}catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");break;}
                if(!next)break;yield return value;
            }
            (test as IDisposable)?.Dispose();EditorApplication.ExitPlaymode();
        }
        static IEnumerator Check()
        {
            yield return new WaitForSecondsRealtime(1);
            var lobby=IslandLobby.Current;var view=lobby.View();lobby.SetMenu(true);var keyboard=view.GiantKeyboard;
            var yaw=Quaternion.Euler(0,-10,0);var expected=new Vector3(-3.05f,1.62f,1)+yaw*new Vector3(0,-.68f,-.45f);
            Require(Vector3.Distance(keyboard.position,expected)<.0001f&&Quaternion.Angle(keyboard.rotation,yaw*Quaternion.Euler(50,0,0))<.01f,"Keyboard keeps its original world position, angle and scale");
            Require(Mathf.Abs(((RectTransform)keyboard).rect.width*keyboard.lossyScale.x-1.008f)<.001f,"Original keyboard width remains unchanged");
            var key=keyboard.Find("key-Q");var ray=new Ray(key.TransformPoint(new Vector3(0,0,-500)),key.forward);Physics.SyncTransforms();
            view.Point(ray,false);
            Require(key.GetComponentInChildren<Text>().color==Color.white,"Gaze-selected key label stays white");
            var mpb=new MaterialPropertyBlock();key.GetComponentInChildren<MeshRenderer>().GetPropertyBlock(mpb);
            Require(mpb.GetColor("_EmissionColor").maxColorComponent>1&&key.Find("Bright selection rim").GetComponent<Image>().enabled,"Gaze selection lights both the emissive key and bright outline");
            view.Point(new Ray(Vector3.one*100,Vector3.up),false);
            Require(!key.Find("Bright selection rim").GetComponent<Image>().enabled&&key.GetComponentInChildren<Text>().color==Color.white,"Moving gaze away clears the glow without darkening labels");
            view.InvokeChoice("clear-code");view.Point(ray,true);Require(lobby.EnteredCode=="Q","Point-and-click still enters the selected letter");
            view.InvokeChoice("key-W");view.InvokeChoice("backspace");Require(lobby.EnteredCode=="Q","Backspace removes the last character, not a forward delete");
            Require(keyboard.GetComponentInChildren<LobbyBackspaceIcon>()!=null,"Backspace uses a drawn key-and-X symbol instead of relying on font coverage");
            Canvas.ForceUpdateCanvases();var iconMesh=keyboard.GetComponentInChildren<LobbyBackspaceIcon>().canvasRenderer.GetMesh();Require(iconMesh&&iconMesh.vertexCount>0,"Backspace icon generates visible canvas geometry");
            view.InvokeChoice("clear-code");foreach(char c in "US-ABC234")view.InvokeChoice("key-"+c);
            Require(lobby.CodeValid&&lobby.EnteredCode=="US-ABC234","Physical keys still support full region-qualified join codes");
            var z=keyboard.Find("key-Z");var touch=z.TransformPoint(new Vector3(0,0,-55));
            view.InvokeChoice("clear-code");Require(view.TouchKeyboard(touch,0)&&!view.TouchKeyboard(touch,0)&&lobby.EnteredCode=="Z","Fingertip key presses retain lift-to-repeat protection");
            var cabinet=view.transform.Find("Grounded red arcade cabinet");var mesh=cabinet.GetComponent<MeshFilter>().sharedMesh;
            var points=mesh.vertices.Select(v=>cabinet.TransformPoint(v)).ToArray();
            Require(points.Min(p=>p.y)<0&&points.Max(p=>p.y)>.9f,"Cabinet reaches below the sand and up to the keyboard");
            Require(points.All(p=>p.y<0||Mathf.Abs(keyboard.InverseTransformPoint(p).z-100)<.01f),"Sloped cabinet top meets the keyboard underside");
            Require(cabinet.GetComponentsInChildren<Collider>().Length==0&&Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Count(c=>c.name.Contains("Grounded red arcade cabinet"))==8,"Cabinet collision remains owned by the lobby through Core");
            var station=view.transform.Find("JOIN");Require(station.Find("CRT left bezel")&&station.Find("CRT right bezel"),"JOIN screen has thick rounded red monitor sides");
            view.InvokeChoice("clear-code");view.InvokeChoice("detect-nearby");
            var app=PhotonAppSettings.Global.AppSettings;string protocol=(app.AppVersion??"")+"/"+lobby.catalog.NetworkVersion;
            using(var nearby=new IslandDiscovery(app.AppIdFusion,protocol,0))
            {Require(lobby.Room.Discovery.Receive(nearby.EncodePresence("US-ABC234",2),new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,47738),Time.unscaledTime),"Compatible nearby-room packet still reaches the regular join picker");}
            yield return null;
            Require(station.Find("code").gameObject.activeInHierarchy&&station.Find("nearby-room-0").gameObject.activeInHierarchy,"Code entry stays visible while nearby results appear beneath search");
            Require(station.Find("detect-nearby").GetComponentInChildren<Text>().text=="SEARCH LOCAL","Primary search action has its simplified label");
            view.InvokeChoice("code-instead");yield return new WaitForSecondsRealtime(.25f);
            var origin=keyboard.position;
            Capture(lobby,"LobbyArcade-Front.png",origin+yaw*new Vector3(0,.83f,-1.85f),origin+yaw*new Vector3(0,.27f,.10f));
            view.Point(ray,false);
            Capture(lobby,"LobbyArcade-Keyboard.png",origin+yaw*new Vector3(0,.73f,-.75f),origin);
            Capture(lobby,"LobbyArcade-Side.png",origin+yaw*new Vector3(1.4f,.3f,-1.25f),origin+Vector3.up*.10f);
            var camera=lobby.Player.ViewCamera;camera.transform.position=station.position+station.forward*1.5f;station.GetComponent<LobbyTerminalFaces>().Refresh();
            Require(!station.GetComponent<LobbyTerminalFaces>().ShowingFront&&station.GetComponent<LobbyTerminalFaces>().RearOffset>.16f,"Rear face remains readable outside the thick monitor shell");
            Capture(lobby,"LobbyArcade-Back.png",station.position+station.forward*1.6f,station.position);
            Require(errors==0,"Actual lobby Play Mode completes without runtime errors");
            File.AppendAllText(Report,"COMPLETE: desktop selection, simulated touch, nearby packet, cabinet collision and four visual captures. Quest comfort not verified.\n");
        }
        static void Capture(IslandLobby lobby,string name,Vector3 position,Vector3 target)
        {
            var eye=lobby.Player.ViewCamera;eye.transform.position=position;eye.transform.LookAt(target);
            foreach(var face in lobby.GetComponentsInChildren<LobbyTerminalFaces>())face.Refresh();
            var go=new GameObject("Temporary arcade capture");var camera=go.AddComponent<Camera>();camera.fieldOfView=58;camera.transform.SetPositionAndRotation(eye.transform.position,eye.transform.rotation);
            var rt=new RenderTexture(1600,1200,24);var old=RenderTexture.active;Texture2D pixels=null;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels=new Texture2D(1600,1200,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1600,1200),0,0);pixels.Apply();File.WriteAllBytes(ProjectPath.Report(name),pixels.EncodeToPNG());}
            finally{RenderTexture.active=old;Object.Destroy(rt);if(pixels)Object.Destroy(pixels);Object.Destroy(go);}
        }
    }
}
