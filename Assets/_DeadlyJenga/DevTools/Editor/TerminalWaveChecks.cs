using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.Core;
using DeadlyJenga.World.Hazards;
using Object=UnityEngine.Object;
using DeadlyJenga.DevTools.Editor;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class TerminalWaveChecks
    {
        const string Key="TerminalWaveChecks",Request="Logs/Validation/TerminalWave.request";
        static int errors;
        static TerminalWaveChecks()
        {
            EditorApplication.update+=()=>{if(File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating){File.Delete(Request);Run();}};
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode){errors=0;Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>{MonoBehaviour owner=IslandLobby.Current;if(!owner)owner=Object.FindFirstObjectByType<CastlePlaySession>();owner.StartCoroutine(Guard(Check()));};}
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    Application.logMessageReceived-=Log;
                    if(SessionState.GetInt(Key+"Stage",0)==0&&!SessionState.GetBool(Key+"Failed",false))
                    {SessionState.SetInt(Key+"Stage",1);EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();}
                    else{SessionState.SetBool(Key,false);string path=SessionState.GetString(Key+"Original","");if(File.Exists(path))EditorSceneManager.OpenScene(path);if(Application.isBatchMode)EditorApplication.Exit(SessionState.GetBool(Key+"Failed",false)?1:0);}
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validate Terminals and Wave Sliders")]
        public static void Run()
        {
            File.WriteAllText(ProjectPath.Report("TerminalWaveChecks.txt"),"Lobby / tsunami UI validation\n");
            Assert(EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Test Disaster Map Contract"),"All disaster map contracts pass translated and scaled bounds checks");
            SessionState.SetString(Key+"Original",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);SessionState.SetBool(Key+"Failed",false);SessionState.SetInt(Key+"Stage",0);SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string m,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(ProjectPath.Report("TerminalWaveChecks.txt"),"ERROR "+m+"\n"+trace+"\n");}}
        static void Assert(bool condition,string text){File.AppendAllText(ProjectPath.Report("TerminalWaveChecks.txt"),(condition?"PASS ":"FAIL ")+text+"\n");if(!condition)throw new Exception(text);}
        static IEnumerator Guard(IEnumerator root)
        {
            var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(root);
            while(stack.Count>0){bool next=false;object value=null;Exception error=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){File.AppendAllText(ProjectPath.Report("TerminalWaveChecks.txt"),"FAIL "+error+"\n");SessionState.SetBool(Key+"Failed",true);EditorApplication.ExitPlaymode();yield break;}
                if(!next){(stack.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator child){stack.Push(child);continue;}yield return value;}
            EditorApplication.ExitPlaymode();
        }
        static IEnumerator Check()
        {
            yield return new WaitForSeconds(2);
            if(SessionState.GetInt(Key+"Stage",0)==0)
            {
                var lobby=IslandLobby.Current;lobby.SetMenu(true);var camera=lobby.Survivor().viewCamera;
                var panel=lobby.GetComponentsInChildren<LobbyTerminalFaces>().First(f=>f.name=="JOIN");
                camera.transform.position=panel.transform.position-panel.transform.forward*2;camera.transform.LookAt(panel.transform);panel.Refresh();
                Assert(panel.ShowingFront&&panel.GetComponent<CanvasGroup>().alpha==1,"Front face is fully visible and readable");Capture(camera,"terminal-front.png");
                var key=lobby.GetComponentsInChildren<BoxCollider>().First(c=>c.name=="key-A");string before=lobby.EnteredCode;
                lobby.View().Point(new Ray(key.transform.position+panel.transform.forward,-panel.transform.forward),true);
                Assert(lobby.EnteredCode==before,"Backside cannot accidentally activate a front keypad key");
                camera.transform.position=panel.transform.position+panel.transform.forward*2;camera.transform.LookAt(panel.transform);panel.Refresh();
                Assert(!panel.ShowingFront&&panel.GetComponent<CanvasGroup>().alpha==0,"Back view hides mirrored front text");Capture(camera,"terminal-back.png");
                lobby.View().Point(new Ray(key.transform.position-panel.transform.forward,panel.transform.forward),true);Assert(lobby.EnteredCode==before+"A","Front keypad remains interactive");
                camera.transform.position=new Vector3(0,2,-4.5f);camera.transform.LookAt(new Vector3(0,1.7f,.8f));foreach(var f in lobby.GetComponentsInChildren<LobbyTerminalFaces>())f.Refresh();Capture(camera,"lobby-clean-panels.png");
                Assert(errors==0,"Lobby panels run without errors");yield break;
            }
            var s=Object.FindFirstObjectByType<CastlePlaySession>();s.SetMenu(true);
            Assert(EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"),"Disaster catalog validation command runs");
            var prefab=DemoDisasters.Prefabs.Single(p=>p.GetComponent<TsunamiHazard>());var definition=prefab.GetComponent<TsunamiHazard>();float force=definition.pushAcceleration,lift=definition.liftAcceleration,speed=definition.approachSpeed;
            var probe=Object.Instantiate(prefab);var tune=probe.GetComponent<IWaveTuning>();var configured=probe.GetComponent<TsunamiHazard>();
            tune.Configure(100,20);Assert(configured.approachSpeed==30&&configured.pushAcceleration==force*5,"Wave contract bounds excessive tuning");
            tune.Configure(10,2);tune.Configure(10,2);Assert(configured.pushAcceleration==force*2,"Repeated tuning does not compound force");Object.Destroy(probe);
            s.tsunamiSpeed=13.5f;s.tsunamiForce=2.5f;DemoDisasters.Request(s,prefab);
            var wave=s.GetComponentsInChildren<TsunamiHazard>().Single(h=>h.Active);
            Assert(Mathf.Abs(wave.approachSpeed-13.5f)<.01f&&Mathf.Abs(wave.pushAcceleration-force*2.5f)<.01f&&Mathf.Abs(wave.liftAcceleration-lift*2.5f)<.01f,"Spawn applies selected speed and push/lift force before Begin");
            s.tsunamiSpeed=2;s.tsunamiForce=0;Assert(wave.approachSpeed==13.5f&&wave.pushAcceleration==force*2.5f,"Changing sliders leaves the existing wave untouched");
            Assert(definition.approachSpeed==speed&&definition.pushAcceleration==force&&definition.liftAcceleration==lift,"Prefab tuning is never modified");
            DemoDisasters.StopAll(s);yield return null;
            var command=JsonUtility.FromJson<CastleDemoCommand>(JsonUtility.ToJson(new CastleDemoCommand{action=CastleDemoAction.Disaster,rules=prefab.name,tuneWave=true,waveSpeed=18,waveForce=0}));
            Assert(CastleDemoCommands.Execute(s,command),"Network command serialization preserves per-spawn options");
            wave=s.GetComponentsInChildren<TsunamiHazard>().Single(h=>h.Active);Assert(wave.approachSpeed==18&&wave.pushAcceleration==0&&wave.liftAcceleration==0,"Zero force disables push and lift");
            DemoDisasters.StopAll(s);yield return null;
            Assert(!DemoDisasters.Execute(s,prefab.name,float.NaN,1),"Host rejects nonfinite tuning values");
            var menu=Object.FindFirstObjectByType<CastleWorldMenu>(FindObjectsInactive.Include);var cam=s.Survivor().viewCamera;menu.Show(true,cam);
            typeof(CastleWorldMenu).GetField("disasterPage",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(menu,true);typeof(CastleWorldMenu).GetField("disasterSelection",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(menu,0);menu.Point(cam,false);
            var slider=((Slider[])typeof(CastleWorldMenu).GetField("disasterSliders",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(menu))[0];
            Assert(slider.gameObject.activeSelf&&slider.interactable,"VR disaster page exposes its wave sliders");
            cam.transform.LookAt(slider.transform.TransformPoint(new Vector3(125,-13,0)));menu.Point(cam,true,true);
            Assert(Mathf.Abs(s.tsunamiSpeed-Mathf.Lerp(.5f,30,.75f))<.2f,"VR gaze plus held trigger adjusts speed slider continuously");
            var forceSlider=((Slider[])typeof(CastleWorldMenu).GetField("disasterSliders",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(menu))[1];
            cam.transform.LookAt(forceSlider.transform.TransformPoint(new Vector3(0,-13,0)));menu.Point(cam,true,true);
            Assert(Mathf.Abs(s.tsunamiForce-2.5f)<.2f,"VR force slider uses the same held-input interaction");
            cam.transform.LookAt(menu.transform.position);Capture(cam,"tsunami-vr-sliders.png");
            menu.Show(false,cam);typeof(CandyDemoHud).GetField("page",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(s.GetComponent<CandyDemoHud>(),3);
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(ProjectPath.Report("tsunami-desktop-sliders.png"));yield return new WaitForSeconds(.5f);
            DemoDisasters.StopAll(s);Assert(errors==0,"Wave tuning and VR controls run without errors");
            File.AppendAllText(ProjectPath.Report("TerminalWaveChecks.txt"),"COMPLETE: desktop editor checks; physical headset pointer/comfort not tested.\n");
        }
        static void Capture(Camera camera,string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var rt=new RenderTexture(1200,1000,24);var old=RenderTexture.active;var target=camera.targetTexture;float fov=camera.fieldOfView;Texture2D pixels=null;
            try{camera.targetTexture=rt;camera.fieldOfView=65;camera.Render();RenderTexture.active=rt;pixels=new Texture2D(1200,1000,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1200,1000),0,0);pixels.Apply();File.WriteAllBytes(ProjectPath.Report(name),pixels.EncodeToPNG());}
            finally{camera.targetTexture=target;camera.fieldOfView=fov;RenderTexture.active=old;Object.Destroy(rt);if(pixels)Object.Destroy(pixels);}
        }
    }
}
