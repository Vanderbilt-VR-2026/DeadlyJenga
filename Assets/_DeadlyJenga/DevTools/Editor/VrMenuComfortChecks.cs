using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using Unity.XR.CoreUtils;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class VrMenuComfortChecks
    {
        const string Key="VrMenuComfortChecks";
        static IEnumerator run;static int errors,checks;static bool failed;
        static string Report=>ProjectPath.Report("VrMenuComfortChecks.txt");
        [Serializable] sealed class Saved{public SceneSetup[] scenes;public string choices;public bool background,fromLobby,local;public float volume;}
        static VrMenuComfortChecks(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        public static void Run()
        {
            string root=Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
            if(Path.GetFullPath(File.ReadAllText(ProjectPath.Report("CandyTerminalDesign.owner")).Trim()).ToLowerInvariant()!=root.ToLowerInvariant())throw new InvalidOperationException("Wrong owned editor.");
            if(EditorApplication.isCompiling||EditorApplication.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Editor busy.");
            if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Unsaved scenes untouched.");
            if(!ValidationRunGate.TryAcquire(Key))return;
            SessionState.SetString(Key+"Saved",JsonUtility.ToJson(new Saved{scenes=EditorSceneManager.GetSceneManagerSetup(),choices=JsonUtility.ToJson(SpectatorRecordingPlan.Current()),background=Application.runInBackground,volume=AudioListener.volume,fromLobby=MatchSetup.FromLobby,local=MatchSetup.StartLocalSession}));
            SessionState.SetBool(Key,true);File.WriteAllText(Report,"Actual Unity VR menu / desktop onboarding validation\n"+root+"\n");
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){SessionState.SetBool(Key+"Entered",true);errors=checks=0;failed=false;Application.logMessageReceived+=Log;AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();run=Flatten(Validate());new GameObject("Ordnance validation driver").AddComponent<TidalCheckDriver>().StartCoroutine(Drive());}
            if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"Entered",false))
            {
                SessionState.SetBool(Key+"Entered",false);Application.logMessageReceived-=Log;run=null;SessionState.SetBool(Key,false);
                var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Saved",""));Application.runInBackground=saved.background;AudioListener.volume=saved.volume;MatchSetup.FromLobby=saved.fromLobby;MatchSetup.StartLocalSession=saved.local;
                SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(saved.choices));
                if(saved.scenes.Any(s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
                AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();ValidationRunGate.Release(Key);
                File.AppendAllText(Report,"COMPLETE checks="+checks+" errors="+errors+" failed="+failed+"\nActual Game with scripted gaze and controller-pose fixtures. Headset comfort and physical input remain untested.\n");
            }
        }
        static void Log(string m,string trace,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            // Known independent editor service failures are recorded separately.
            if(trace.Contains("UnityEditor.Search.SearchDatabase")||trace.Contains("UnityEditor.AsyncHTTPClient")){File.AppendAllText(Report,"EDITOR SERVICE "+m+"\n");return;}
            errors++;File.AppendAllText(Report,"ERROR "+m+"\n"+trace+"\n");
        }
        static void Tick()
        {
            if(run==null)
            {
                string request=ProjectPath.Report("VrMenuComfortChecks.request");
                if(!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlaying&&!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(request)&&File.ReadAllText(request).Trim()==System.Diagnostics.Process.GetCurrentProcess().Id.ToString()){if(!ValidationRunGate.TryAcquire(Key))return;File.Delete(request);Run();}
                return;
            }
        }
        static IEnumerator Drive()
        {
            while(run!=null)
            {
                object current;
                try{if(!run.MoveNext())break;current=run.Current;}
                catch(Exception e){failed=true;File.AppendAllText(Report,"FAIL "+e+"\n");break;}
                yield return current;
            }
            (run as IDisposable)?.Dispose();run=null;EditorApplication.ExitPlaymode();
        }
        static void Check(bool ok,string text){checks++;File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+text+"\n");if(!ok)throw new Exception(text);}
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
        static IEnumerator Until(Func<bool> ready,string label,float seconds=40){float until=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;Check(ready(),label);}
        static IEnumerator Flatten(IEnumerator source)
        {
            while(source.MoveNext())
            {
                if(source.Current is IEnumerator nested){var child=Flatten(nested);while(child.MoveNext())yield return child.Current;}
                else if(source.Current is AsyncOperation operation){while(!operation.isDone)yield return null;}
                
                else yield return source.Current;
            }
        }
        static IEnumerator Validate()
        {
            Application.runInBackground=true;
            yield return Until(()=>Services.Session is CastlePlaySession ready&&ready.Modes&&Services.World!=null,"Game initializes");
            var session=(CastlePlaySession)Services.Session;session.SurvivorBots.AutoFill=false;
            var hud=session.GetComponent<CandyDemoHud>();
            Check(hud,"Actual session installs demo HUD");
            Check(!TutorialCoach.Instance,"Legacy tutorial coach does not auto-start");
            const string key="CandyJenga.BasicControlsSeen.v1.Desktop";
            bool stored=PlayerPrefs.HasKey(key);int saved=PlayerPrefs.GetInt(key);
            var camObject=new GameObject("Scripted VR menu eye");
            var eye=camObject.AddComponent<Camera>();eye.enabled=false;
            var menuObject=new GameObject("Scripted VR world menu");
            var menu=menuObject.AddComponent<CastleWorldMenu>();
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            try
            {
                menu.Build(session);
                eye.transform.SetPositionAndRotation(new Vector3(201,14,180),Quaternion.Euler(51,32,17));
                menu.Show(true,eye);
                Check(Mathf.Abs(menu.transform.position.y-eye.transform.position.y)<.002f,"Opening while tilted anchors at eye height");
                Check(Vector3.Dot(menu.transform.up,Vector3.up)>.9999f,"Menu ignores headset roll and pitch");
                var savedPosition=menu.transform.position;var savedRotation=menu.transform.rotation;
                eye.transform.position+=new Vector3(.17f,.14f,.04f);
                eye.transform.Rotate(10,7,12);
                menu.Point(eye,false);
                Check(menu.transform.position==savedPosition&&menu.transform.rotation==savedRotation,"Head turns and translations do not drag the open panel");
                var buttons=(UnityEngine.UI.Button[])typeof(CastleWorldMenu).GetField("buttons",flags).GetValue(menu);
                var targets=(BoxCollider[])typeof(CastleWorldMenu).GetField("targets",flags).GetValue(menu);
                var texts=(UnityEngine.UI.Text[])typeof(CastleWorldMenu).GetField("labels",flags).GetValue(menu);
                Check(new[]{"Rebuild Map","Disasters","Give Yourself a Gun","Bot Population"}.All(label=>texts.Any(t=>t.gameObject.activeInHierarchy&&t.text==label)),"All four quick actions appear on Demo Home");
                Check(texts.Any(t=>t.gameObject.activeInHierarchy&&t.text=="Become Flying Hand"),"Become Flying Hand is visible directly on Demo Home");
                int settings=Array.FindIndex(texts,x=>x.text=="VR settings");
                Check(settings>=0,"VR settings remains available on Demo Home");
                var main=(RectTransform)buttons[settings].transform;
                eye.transform.LookAt(main.TransformPoint(new Vector3(main.rect.xMax+8,0,0)));
                menu.Point(eye,false);
                var focus=typeof(CastleWorldMenu).GetField("focus",flags);
                Check((BoxCollider)focus.GetValue(menu)==targets[settings],"Padded edge is actionable and focused");
                Check(menu.transform.Find("Pointer dot").gameObject.activeSelf&&menu.transform.Find("Pointed control focus bar").gameObject.activeSelf,"Aim dot and focus bar follow the same target");
                Capture("VrMenuComfort.png",eye.transform.position,menu.transform.position,~0,0);
                menu.Point(eye,true,true);
                Check((bool)typeof(CastleWorldMenu).GetField("comfortPage",flags).GetValue(menu),"One edge press opens VR settings");
                menu.Point(eye,false);
                eye.transform.LookAt(menu.transform.Find("Back").position);menu.Point(eye,true);
                Check(!(bool)typeof(CastleWorldMenu).GetField("comfortPage",flags).GetValue(menu),"Back remains targetable after page relayout without a physics sync");
                menu.Point(eye,false);
                void Click(Transform target){eye.transform.LookAt(target.position);menu.Point(eye,true);menu.Point(eye,false);}
                Click(buttons[1].transform);
                Check((bool)typeof(CastleWorldMenu).GetField("disasterPage",flags).GetValue(menu),"Disasters opens directly from Demo Home");
                Click(menu.transform.Find("Demo Home"));
                Check(!(bool)typeof(CastleWorldMenu).GetField("disasterPage",flags).GetValue(menu),"Persistent Demo Home exits a subpage");
                Click(buttons[3].transform);
                Check((bool)typeof(CastleWorldMenu).GetField("batchPage",flags).GetValue(menu),"Bot batch preview opens directly");
                int beforeBots=session.SurvivorBots.Count;
                int preview=Array.FindIndex(texts,x=>x.gameObject.activeInHierarchy&&x.text.StartsWith("Preview 25"));
                Check(preview>=0,"Large batch lists an explicit 25-player preview");
                Click(buttons[preview].transform);
                Check(session.SurvivorBots.Count==beforeBots&&texts.Any(t=>t.gameObject.activeInHierarchy&&t.text=="Fill to 25 total"),"Preview never spawns bots before explicit apply");
                Click(menu.transform.Find("Demo Home"));
                Click(buttons[2].transform);
                Check((bool)typeof(CastleWorldMenu).GetField("quickGunPage",flags).GetValue(menu)&&
                    texts.Any(t=>t.gameObject.activeInHierarchy&&t.text=="Give ALIEN BLASTER"),"Quick gun opens the firearm-only picker");
                Click(menu.transform.Find("Demo Home"));
                eye.transform.LookAt(main.position);menu.Show(true,eye);
                eye.transform.LookAt(main.position);
                menu.Point(eye,true,true);
                Check(!(bool)typeof(CastleWorldMenu).GetField("comfortPage",flags).GetValue(menu),"Held selection on opening cannot activate an item");
                var rigObject=new GameObject("Scripted XR tracking origin");
                rigObject.SetActive(false);
                rigObject.transform.SetPositionAndRotation(new Vector3(198,12,179),Quaternion.Euler(0,24,0));
                camObject.transform.SetParent(rigObject.transform,true);
                var rig=rigObject.AddComponent<XROrigin>();rig.Camera=eye;rig.CameraFloorOffsetObject=rigObject;
                rigObject.SetActive(true);
                var controller=InputSystem.AddDevice<XRController>();
                try
                {
                    InputSystem.SetDeviceUsage(controller,UnityEngine.InputSystem.CommonUsages.RightHand);
                    var origin=eye.transform.position+new Vector3(.22f,-.32f,.17f);
                    using(var state=StateEvent.From(controller,out var stateEvent))
                    {
                        controller.isTracked.WriteValueIntoEvent(1f,stateEvent);
                        InputState.Change(controller,stateEvent);
                    }
                    InputState.Change(controller.trackingState,3);
                    InputState.Change(controller.devicePosition,rigObject.transform.InverseTransformPoint(origin));
                    InputState.Change(controller.deviceRotation,Quaternion.Inverse(rigObject.transform.rotation)*Quaternion.LookRotation(main.position-origin));
                    eye.transform.LookAt(menu.transform.position);
                    menu.Show(true,eye);
                    var aimed=typeof(CastleWorldMenu).GetMethod("ControllerRay",flags);
                    var args=new object[]{eye,default(Ray)};
                    Check((bool)aimed.Invoke(menu,args)&&Vector3.Distance(((Ray)args[1]).origin,origin)<.001f,"Tracked controller converts through the rotated XR origin");
                    menu.Point(eye,false);
                    Check(menu.transform.Find("Right controller menu ray").GetComponent<LineRenderer>().enabled,"Tracked controller shows its own ray");
                    using(var state=StateEvent.From(controller,out var stateEvent))
                    {
                        controller.isTracked.WriteValueIntoEvent(0f,stateEvent);
                        InputState.Change(controller,stateEvent);
                    }
                    menu.Point(eye,false);
                    Check(!menu.transform.Find("Right controller menu ray").GetComponent<LineRenderer>().enabled,"Controller loss falls back to gaze and hides the stale ray");
                }
                finally
                {
                    InputSystem.RemoveDevice(controller);camObject.transform.SetParent(null,true);Object.Destroy(rigObject);
                }
                menu.Show(false,eye);
                Check(!menuObject.activeSelf,"Closing clears world menu");

                var intro=(CandyDemoHud.Hint[])typeof(CandyDemoHud).GetMethod("IntroHints",flags).Invoke(hud,null);
                Check(intro.Select(x=>x.text).SequenceEqual(new[]{"Look","Move","Jump"}),"First-run prompt contains exactly Look, Move, Jump");
                PlayerPrefs.SetInt(key,0);session.SetMenu(false);hud.hintMode=CandyDemoHud.HintMode.Adaptive;
                typeof(CandyDemoHud).GetField("introRemaining",flags).SetValue(hud,4.5f);
                yield return Wait(.65f);
                Check(hud.GameplayHintsVisible,"Quiet prompt appears during first-run play");
                session.SetMenu(true);yield return Wait(.7f);
                Check(!hud.GameplayHintsVisible&&PlayerPrefs.GetInt(key)==0,"Opening menu hides hints without completing onboarding");
                session.SetMenu(false);yield return Wait(5.1f);
                Check(!hud.GameplayHintsVisible&&PlayerPrefs.GetInt(key)==1,"Prompt fades and records completion");
                session.SetMenu(true);yield return Wait(.3f);session.SetMenu(false);yield return Wait(.5f);
                Check(!hud.GameplayHintsVisible,"Closing the menu does not rearm completed prompts");
                hud.hintMode=CandyDemoHud.HintMode.Always;yield return Wait(.5f);
                Check(hud.GameplayHintsVisible,"Explicit Always setting still shows detailed controls");
            }
            finally
            {
                if(stored)PlayerPrefs.SetInt(key,saved);else PlayerPrefs.DeleteKey(key);
                Object.Destroy(camObject);Object.Destroy(menuObject);
            }
        }
        static void Capture(string name,Vector3 position,Vector3 target,int mask,float size)
        {
            var go=new GameObject("Ordnance capture camera");var camera=go.AddComponent<Camera>();camera.enabled=false;
            camera.GetUniversalAdditionalCameraData().allowXRRendering=false;camera.nearClipPlane=.03f;camera.farClipPlane=90;camera.fieldOfView=75;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.075f,.10f);
            camera.cullingMask=mask;camera.transform.position=position;camera.transform.LookAt(target);
            camera.orthographic=size>0;camera.orthographicSize=Mathf.Max(.01f,size);
            var key=new GameObject("Ordnance capture light");key.transform.rotation=Quaternion.Euler(32,147,0);var light=key.AddComponent<Light>();
            light.type=LightType.Directional;light.intensity=1.5f;light.cullingMask=mask;
            var texture=new RenderTexture(1280,960,24);var previous=RenderTexture.active;Texture2D image=null;
            try
            {
                camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
                image=new Texture2D(1280,960,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,960),0,0);image.Apply();
                File.WriteAllBytes(ProjectPath.Report(name),image.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;camera.targetTexture=null;texture.Release();Object.Destroy(texture);if(image)Object.Destroy(image);Object.Destroy(go);Object.Destroy(key);}
        }
    }
}
