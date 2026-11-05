using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Presentation;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Management;
using UnityEditor.XR.Management;
using Object=UnityEngine.Object;
using LogType=UnityEngine.LogType;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class MenuFlowChecks
    {
        const string Key="MenuFlowChecks";
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly Stack<IEnumerator> steps=new();
        static int errors,checks;
        static bool running;
        static NetworkRunner probe;
        static XRGeneralSettings desktopSettings;
        static string Report=>ProjectPath.Report("MenuFlowChecks.txt");
        [Serializable] sealed class Saved { public SceneSetup[] scenes;public string choices;public bool fromLobby,local,background; }
        static MenuFlowChecks(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;if(SessionState.GetBool(Key,false)&&EditorApplication.isPlayingOrWillChangePlaymode)DesktopFixture();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void DesktopFixture()
        {
            if(!SessionState.GetBool(Key,false))return;
            XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if(!desktopSettings)desktopSettings=ScriptableObject.CreateInstance<XRGeneralSettings>();
            desktopSettings.InitManagerOnStart=false;
            XRGeneralSettings.Instance=desktopSettings;
        }
        static void Tick()
        {
            if(!running)
            {
                var request=ProjectPath.Report(Key+".request");
                if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(request))return;
                if(File.ReadAllText(request).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString())return;
                File.Delete(request);Run();return;
            }
        }
        static IEnumerator Drive()
        {
            while(steps.Count>0)
            {
                object value=null;Exception failure=null;
                try
                {
                    while(steps.Count>0)
                    {
                        var next=steps.Peek();if(!next.MoveNext()){(steps.Pop() as IDisposable)?.Dispose();continue;}
                        if(next.Current is IEnumerator nested){steps.Push(nested);continue;}value=next.Current;break;
                    }
                }
                catch(Exception e){failure=e;}
                if(failure!=null){errors++;File.AppendAllText(Report,"FAIL "+failure+"\n");break;}
                if(steps.Count>0)yield return value;
            }
            if(probe)probe.Shutdown();
            while(steps.Count>0)(steps.Pop() as IDisposable)?.Dispose();
            File.AppendAllText(Report,$"COMPLETE checks={checks} errors={errors}\nTwo Fusion runners in one editor; physical Quest input and separate MPP virtual players remain device/editor checks.\n");
            EditorApplication.ExitPlaymode();
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Menu Flows")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)throw new InvalidOperationException("Editor busy.");
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>s.isLoaded&&(string.IsNullOrEmpty(s.path)||SceneManager.GetSceneByPath(s.path).isDirty)))throw new InvalidOperationException("Unsaved scenes left untouched.");
            if(!ValidationRunGate.TryAcquire(Key))return;
            SessionState.SetString(Key+"Saved",JsonUtility.ToJson(new Saved{scenes=scenes,choices=JsonUtility.ToJson(SpectatorRecordingPlan.Current()),fromLobby=MatchSetup.FromLobby,local=MatchSetup.StartLocalSession,background=Application.runInBackground}));
            File.WriteAllText(Report,"Menu flow integration checks\n");SessionState.SetBool(Key,true);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=1;MatchSetup.HandCount=0;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.ExitingEditMode)DesktopFixture();
            if(state==PlayModeStateChange.EnteredPlayMode){errors=checks=0;Check(XRGeneralSettings.Instance==desktopSettings&&!desktopSettings.InitManagerOnStart,"Desktop fixture bypasses native simulator startup without editing XR assets");running=true;Application.logMessageReceived+=Log;steps.Push(Validate());var driver=new GameObject("Menu flow checks");Object.DontDestroyOnLoad(driver);driver.AddComponent<TidalCheckDriver>().StartCoroutine(Drive());}
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                XRGeneralSettings.Instance=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
                if(desktopSettings)Object.DestroyImmediate(desktopSettings);
                running=false;steps.Clear();Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
                var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Saved", ""));
                MatchSetup.FromLobby=saved.fromLobby;MatchSetup.StartLocalSession=saved.local;Application.runInBackground=saved.background;
                SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(saved.choices));
                EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);ValidationRunGate.Release(Key);
            }
        }
        static void Log(string text,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+text+"\n"+trace+"\n");}}
        static void Check(bool pass,string message){checks++;File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+message+"\n");if(!pass)throw new InvalidOperationException(message);}
        static IEnumerator Until(Func<bool> condition,string message,float seconds=60)
        {float end=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;Check(condition(),message);}
        static IEnumerator Done(System.Threading.Tasks.Task task,string message)
        {yield return Until(()=>task.IsCompleted,message);if(task.IsFaulted)throw task.Exception;}
        static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Private).GetValue(owner);
        static void Call(object owner,string name)=>owner.GetType().GetMethod(name,Private).Invoke(owner,null);
        static IEnumerator Validate()
        {
            Application.runInBackground=true;
            yield return Until(()=>Services.Session is CastlePlaySession&&Services.World!=null&&Services.Session.Player?.ViewCamera,"Game scene initializes solo with its player and world");
            var session=(CastlePlaySession)Services.Session;session.SetMenu(true);
            var menu=new GameObject("Menu flow test",typeof(RectTransform)).AddComponent<CastleWorldMenu>();menu.Build(session);menu.Show(true,session.ActiveCamera);menu.Point(session.ActiveCamera,false);
            var labels=Field<UnityEngine.UI.Text[]>(menu,"labels");
            Check(labels.Any(t=>t.gameObject.activeInHierarchy&&t.text=="Resume")&&labels.Any(t=>t.gameObject.activeInHierarchy&&t.text=="Controls & comfort"),"Pause menu exposes player controls first");
            Check(!labels.Any(t=>t.gameObject.activeInHierarchy&&t.text.Contains("Give Yourself")),"Equipment spawning is absent from the pause root");
            Check(labels.Any(t=>t.gameObject.activeInHierarchy&&t.text=="Host tools"),"Solo Demo owner can open Host tools");
            Object.Destroy(menu.gameObject);
            yield return PointerChecks();
            SceneManager.LoadScene("Home");
            yield return Until(()=>IslandLobby.Current&&IslandLobby.Current.View(),"Home bypass enters the real island lobby");
            var lobby=IslandLobby.Current;var view=lobby.View();var room=lobby.Room;
            Check(!Object.FindFirstObjectByType<HomeMenuView>(),"Legacy 2D Host/Join screen stays disabled");
            Check(lobby.Player?.gameObject.activeInHierarchy==true,"Player body exists before connection completes");
            var keyboard=(RectTransform)view.GiantKeyboard;
            Call(view,"UpdatePrivateKeyboard");Check(!keyboard.gameObject.activeSelf,"Private keyboard starts hidden");
            yield return Until(()=>!room.Busy,"Automatic host attempt finishes");
            Check(room.Connected&&lobby.IsHost,"Island automatically hosts a Fusion room");
            lobby.FocusCode();Call(view,"UpdatePrivateKeyboard");
            Check(keyboard.gameObject.activeSelf&&Vector3.Distance(keyboard.position,lobby.Player.ViewCamera.transform.position)<1.3f,"Room field opens a keyboard in front of the local player");
            Check(!keyboard.GetComponentInParent<NetworkObject>()&&!keyboard.GetComponentInChildren<NetworkBehaviour>(true),"Keyboard and typed text are not network objects");
            lobby.TypeCode('A');Call(view,"UpdatePrivateKeyboard");
            Check(!view.TouchKeyboard(keyboard.position,0)&&lobby.EnteredCode=="A","Fingertip contact cannot enter room characters");
            view.InvokeChoice("keyboard-close");Call(view,"UpdatePrivateKeyboard");Check(!keyboard.gameObject.activeSelf&&!lobby.CodeFocused,"Close dismisses keyboard and code focus");
            var app=Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings.GetCopy();app.FixedRegion=room.Region;app.AppVersion=(app.AppVersion??"")+"/"+room.Catalog.NetworkVersion;
            var go=new GameObject("Menu flow second Fusion host");Object.DontDestroyOnLoad(go);probe=go.AddComponent<NetworkRunner>();
            string oldCode=room.Code,address=IslandRoomAddress.Create(room.Region);
            var config=IslandRoomConnection.MigrationConfig();config.PeerMode=NetworkProjectConfig.PeerModes.Single;
            var host=probe.StartGame(new StartGameArgs{GameMode=Fusion.GameMode.Host,SessionName=address,PlayerCount=10,IsVisible=false,CustomPhotonAppSettings=app,Config=config,Scene=new NetworkSceneInfo(),SceneManager=go.AddComponent<NetworkSceneManagerDefault>()});
            yield return Done(host,"Independent Fusion room starts");Check(host.Result.Ok,"Second host creates a joinable room");
            yield return Done(room.Join(address),"Joining another room finishes");
            Check(room.Connected&&room.Code==address&&room.Code!=oldCode&&!lobby.IsHost,"Join replaces the old hosted connection with the requested guest room");
            yield return Until(()=>room.HumanCount==2,"Both Fusion players are in the same room");
            Check(!MenuAccess.HostTools(lobby),"Joined players cannot see Host tools");
            var guestMenu=new GameObject("Guest menu test",typeof(RectTransform)).AddComponent<CastleWorldMenu>();guestMenu.Build(lobby);guestMenu.Show(true,lobby.Player.ViewCamera);guestMenu.Point(lobby.Player.ViewCamera,false);
            Check(!Field<UnityEngine.UI.Text[]>(guestMenu,"labels").Any(t=>t.gameObject.activeInHierarchy&&t.text=="Host tools"),"Guest pause root has no Host tools entry");
            guestMenu.GetType().GetField("hostTools",Private).SetValue(guestMenu,true);guestMenu.GetType().GetField("menuCategory",Private).SetValue(guestMenu,1);guestMenu.Point(lobby.Player.ViewCamera,false);
            Check(!Field<bool>(guestMenu,"hostTools")&&Field<int>(guestMenu,"menuCategory")==-1,"Stale host development pages reset for a guest");Object.Destroy(guestMenu.gameObject);
            yield return Done(room.Leave(),"Guest leaves without shutting down remote host");Check(probe&&probe.IsRunning,"Remote host remains running after guest leaves");
            yield return Done(probe.Shutdown(),"Second test host shuts down");Object.Destroy(go);probe=null;
            Check(errors==0,"Game, lobby, private keyboard and two-runner join flow complete without runtime errors");
        }
        static void SetTracked(XRController device,bool tracked)
        {
            using(var state=StateEvent.From(device,out var pointer))
            {device.isTracked.WriteValueIntoEvent(tracked?1f:0f,pointer);InputState.Change(device,pointer);}
        }
        static IEnumerator PointerChecks()
        {
            var go=new GameObject("Menu pointer test origin");go.SetActive(false);go.transform.SetPositionAndRotation(new Vector3(5,2,8),Quaternion.Euler(0,35,0));
            var camera=new GameObject("Menu pointer test camera").AddComponent<Camera>();camera.enabled=false;camera.transform.SetParent(go.transform,false);camera.transform.localPosition=Vector3.up*1.6f;
            var origin=go.AddComponent<XROrigin>();origin.Camera=camera;origin.CameraFloorOffsetObject=go;go.SetActive(true);
            InputSystem.RegisterLayout(@"{""name"":""MenuFlowController"",""extend"":""XRController"",""canRunInBackground"":true,""controls"":[{""name"":""trigger"",""layout"":""Axis""}]}");
            var right=(XRController)InputSystem.AddDevice("MenuFlowController");var left=(XRController)InputSystem.AddDevice("MenuFlowController");
            try
            {
                InputSystem.SetDeviceUsage(right,CommonUsages.RightHand);InputSystem.SetDeviceUsage(left,CommonUsages.LeftHand);
                void Pose(XRController device,Vector3 point){SetTracked(device,true);InputState.Change(device.devicePosition,point);InputState.Change(device.deviceRotation,Quaternion.identity);}
                Pose(right,new Vector3(.2f,1.3f,.2f));Pose(left,new Vector3(-.2f,1.3f,.2f));
                bool read=MenuPointer.TryRead(camera,false,out var sample);
                Check(read&&Vector3.Distance(sample.Ray.origin,go.transform.TransformPoint(right.devicePosition.ReadValue()))<.001f,"Controller ray respects the translated and rotated XR tracking origin (read="+read+", actual="+sample.Ray.origin+", expected="+go.transform.TransformPoint(right.devicePosition.ReadValue())+", tracked="+right.isTracked.isPressed+")");
                InputState.Change(left.GetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("trigger"),1f);yield return null;
                Check(MenuPointer.TryRead(camera,false,out sample)&&sample.Pressed&&sample.Held&&Vector3.Distance(sample.Ray.origin,go.transform.TransformPoint(left.devicePosition.ReadValue()))<.001f,"Pressing left trigger selects the left controller laser");
                yield return null;MenuPointer.TryRead(camera,false,out sample);Check(!sample.Pressed&&sample.Held,"Holding trigger does not repeatedly click");
                SetTracked(right,false);SetTracked(left,false);yield return null;
                Check(!MenuPointer.TryRead(camera,false,out sample),"Tracking loss cannot fall back to head-gaze selection");
            }
            finally{InputSystem.RemoveDevice(right);InputSystem.RemoveDevice(left);Object.Destroy(go);}
        }
    }
}
