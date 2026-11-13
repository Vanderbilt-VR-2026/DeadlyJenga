using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public sealed class HomeGamepadProbe:MonoBehaviour
    {
        public Vector2 move,look;
        public bool jump,fire,aim,sprint;
        void LateUpdate()
        {
            move=DesktopGamepad.Move;look=DesktopGamepad.Look;
            jump=DesktopGamepad.JumpHeld;fire=DesktopGamepad.FireHeld;
            aim=DesktopGamepad.AimHeld;sprint=DesktopGamepad.SprintHeld;
        }
    }

    [InitializeOnLoad]
    public static class HomeInputChecks
    {
        const string Key="HomeInputChecks";
        static string Output=>Path.GetFullPath("Logs/Validation/HomeInput");
        static string Report=>Path.Combine(Output,"report.txt");
        static IEnumerator run;
        static int nextFrame;
        static double deadline;
        static Gamepad pad;
        static InputSettings savedInput;
        static InputSettings.BackgroundBehavior savedBackground;
        static InputSettings.EditorInputBehaviorInPlayMode savedRouting;
        static GameObject motorObject,floor;
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
        [Serializable] sealed class Saved { public SceneSetup[] scenes;public bool hasPreference;public int preference; }
        static HomeInputChecks(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        static void Tick()
        {
            if(run==null)
            {
                var request=ProjectPath.Report("HomeInput.request");
                if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                if(File.ReadAllText(request).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString())return;
                File.Delete(request);Run();return;
            }
            if(Time.frameCount==nextFrame&&EditorApplication.timeSinceStartup<deadline)return;
            nextFrame=Time.frameCount;
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Home input fixture exceeded its wall-clock deadline");
                if(run.MoveNext())return;
                File.AppendAllText(Report,"PASS Complete home/gamepad fixture\n");
            }
            catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");}
            finally { }
            (run as IDisposable)?.Dispose();run=null;
            if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);pad=null;
            if(motorObject)Object.Destroy(motorObject);if(floor)Object.Destroy(floor);
            if(savedInput){savedInput.backgroundBehavior=savedBackground;savedInput.editorInputBehaviorInPlayMode=savedRouting;}
            savedInput=null;
            EditorApplication.ExitPlaymode();
        }
        public static void Run()
        {
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>s.isLoaded&&(string.IsNullOrEmpty(s.path)||SceneManager.GetSceneByPath(s.path).isDirty)))throw new InvalidOperationException("Unsaved scene edits left untouched");
            if(!ValidationRunGate.TryAcquire(Key))throw new InvalidOperationException("Another validation owns this editor");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Report,"Actual Home UI and survivor motor, synthetic gamepad and controlled VR camera; no physical controller or Quest evidence.\n");
            SessionState.SetString(Key+"Saved",JsonUtility.ToJson(new Saved{scenes=scenes,hasPreference=PlayerPrefs.HasKey("DeadlyJenga.PreferredControlMode"),preference=PlayerPrefs.GetInt("DeadlyJenga.PreferredControlMode",0)}));
            PlayerPrefs.SetInt("DeadlyJenga.PreferredControlMode",4);
            SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Home.unity"));EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){deadline=EditorApplication.timeSinceStartup+120;nextFrame=-1;run=Checks();}
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Saved",""));
            if(saved.hasPreference)PlayerPrefs.SetInt("DeadlyJenga.PreferredControlMode",saved.preference);else PlayerPrefs.DeleteKey("DeadlyJenga.PreferredControlMode");
            PlayerPrefs.Save();
            SessionState.SetBool(Key,false);EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
            ValidationRunGate.Release(Key);File.AppendAllText(Report,"CLEANUP returned to saved Edit Mode scenes and input preference\n");
        }
        static void Check(bool pass,string message)
        {
            File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+message+"\n");
            if(!pass)throw new InvalidOperationException(message);
        }
        static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Flags).GetValue(owner);
        static object Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Flags).Invoke(owner,args);
        static IEnumerator Checks()
        {
            yield return null;yield return null;
            var home=Object.FindFirstObjectByType<HomeMenuView>();Check(home,"Actual Home scene creates its production menu");
            home.SendMessage("OnApplicationFocus",true);
            savedInput=InputSystem.settings;savedBackground=savedInput.backgroundBehavior;savedRouting=savedInput.editorInputBehaviorInPlayMode;
            savedInput.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            savedInput.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Check(InputSystem.LoadLayout("CandyGamepadDpad").type==typeof(UnityEngine.InputSystem.Controls.DpadControl),"Gamepad D-pad is isolated from the installed OpenXR layout collision");
            pad=InputSystem.AddDevice<Gamepad>();
            var probe=home.gameObject.AddComponent<HomeGamepadProbe>();
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;yield return null;
            Check(DesktopGamepad.Current==pad&&MenuGamepad.Connected==pad,"Hot-plugged standard gamepad is detected by gameplay and home UI");
            Check(Field<Text>(home,"inputModeText").text.Contains("GAMEPAD"),"Home displays the manual Gamepad input mode");
            PlayerPrefs.SetInt("DeadlyJenga.PreferredControlMode",1);
            Check(DesktopGamepad.Current==null&&MenuGamepad.Current==null,"Explicit keyboard/mouse mode ignores connected gamepad");
            PlayerPrefs.SetInt("DeadlyJenga.PreferredControlMode",0);
            Check(DesktopGamepad.Current==pad,"Auto mode recognizes connected gamepad without a headset");
            Call(home,"SetInputMode",4);
            Check(PlayerPrefs.GetInt("DeadlyJenga.PreferredControlMode")==4,"Direct home input choice applies Gamepad mode");
            var frame=Field<RectTransform>(home,"frame");int clicked=0;
            var button=(Button)Call(home,"ButtonAt",frame,"PAD INPUT CHECK",Vector2.zero,new Vector2(420,100),(Action)(()=>clicked++));
            typeof(MenuGamepad).GetField("position",Flags).SetValue(null,new Vector2(Screen.width*.5f,Screen.height*.5f));
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));
            yield return null;yield return null;yield return null;
            Check(clicked==1,"One gamepad press invokes a home button exactly once; holding does not repeat or double-submit");
            Check(probe.jump,"Queued gamepad input is observed in the normal PlayerLoop");
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;yield return null;
            button.gameObject.SetActive(false);yield return null;
            // The same menu renders in world space with a controlled camera for pointer/style checks.
            PlayerPrefs.SetInt("DeadlyJenga.PreferredControlMode",3);Call(home,"RefreshDisplayMode");
            var eye=Field<Camera>(home,"eye");
            var driver=eye.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();if(driver)driver.enabled=false;
            yield return null;yield return null;yield return null;yield return null;
            var play=home.GetComponentsInChildren<Button>().First(b=>b.name=="PLAY MODE");
            eye.transform.LookAt(play.transform.position);Call(home,"Point");
            Check(play.GetComponent<Outline>().enabled&&play.transform.localScale.x>1.04f,"VR ray hover visibly raises the Play button and lights its mint outline");
            Check(play.image.color.r>.5f,"VR hover changes the actual button face color");
            Capture(eye,"Home-VR-hover.png");
            eye.transform.rotation=Quaternion.LookRotation(-frame.forward);Call(home,"Point");
            Check(!play.GetComponent<Outline>().enabled&&Mathf.Approximately(play.transform.localScale.x,1),"Looking away clears VR hover feedback");
            var blurred=Field<RenderTexture>(home,"blurredSlide");
            Check(blurred.width==512&&blurred.height==288&&!blurred.useMipMap,"VR surround uses cached 512 x 288 blur, with no coarse mip sampling");
            Check(Field<Material>(home,"blur")!=null,"Reusable slide blur shader is loaded");
            foreach(var shaderName in new[]{"CandyHomeBackdrop","CandyHomeBlur"})Check(!ShaderUtil.ShaderHasError(Resources.Load<Shader>(shaderName)),shaderName+" has no Unity shader errors");
            Capture(eye,"Home-VR-surround.png");
            PlayerPrefs.SetInt("DeadlyJenga.PreferredControlMode",4);Call(home,"RefreshDisplayMode");
            Check(!Field<bool>(home,"vr"),"Choosing Gamepad exits the headset presentation immediately");
            home.enabled=false;
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Controller test floor";floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(40,1,40);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("CastleSurvivor.prefab"));
            motorObject=Object.Instantiate(prefab,Vector3.zero,Quaternion.identity);
            foreach(var camera in motorObject.GetComponentsInChildren<Camera>())camera.enabled=false;
            foreach(var listener in motorObject.GetComponentsInChildren<AudioListener>())listener.enabled=false;
            var motor=motorObject.GetComponent<CastleSurvivor>();motor.InputSuspended=false;motor.SendMessage("OnApplicationFocus",true);
            yield return null;yield return null;
            var start=motor.transform.position;float yaw=motor.transform.eulerAngles.y;
            InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=new Vector2(0,.8f),rightStick=new Vector2(.65f,0)});
            float until=Time.time+.5f;while(Time.time<until)yield return null;
            File.AppendAllText(Report,"Motor position "+start+" -> "+motor.transform.position+"; yaw "+yaw+" -> "+motor.transform.eulerAngles.y+"\n");
            Check(Vector3.ProjectOnPlane(motor.transform.position-start,Vector3.up).magnitude>.25f,"Actual survivor motor walks from left-stick input");
            Check(Mathf.Abs(Mathf.DeltaAngle(yaw,motor.transform.eulerAngles.y))>10,"Actual survivor motor turns from right-stick input");
            Check(probe.move.y>.5f&&probe.look.x>.4f,"Held sticks persist across multiple PlayerLoop frames");
            InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.up}.WithButton(GamepadButton.LeftStick));
            yield return null;yield return null;
            Check(motor.SprintRequested,"Movement-stick click requests sprint");
            InputSystem.QueueStateEvent(pad,new GamepadState{rightTrigger=1,leftTrigger=1}.WithButton(GamepadButton.South));
            yield return null;yield return null;
            Check(probe.fire&&probe.aim&&probe.jump,"Triggers and bottom button reach fire, aim and jump inputs");
            InputSystem.RemoveDevice(pad);pad=null;yield return null;yield return null;
            Check(DesktopGamepad.Current==null&&probe.move==Vector2.zero&&!probe.fire&&!probe.jump,"Unplugging releases movement and held buttons");
            foreach(var layout in new[]{"XInputController","DualShockGamepad","SwitchProControllerHID"})
            {
                if(!InputSystem.ListLayouts().Contains(layout)){File.AppendAllText(Report,"UNAVAILABLE layout "+layout+"\n");continue;}
                pad=InputSystem.AddDevice(layout) as Gamepad;Check(pad!=null&&DesktopGamepad.Current==pad,"Recognized native layout "+layout);
                InputSystem.RemoveDevice(pad);pad=null;
            }
        }
        static void Capture(Camera camera,string name)
        {
            var previous=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1280,800,24);var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,name),image.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(image);}
        }
    }
}
