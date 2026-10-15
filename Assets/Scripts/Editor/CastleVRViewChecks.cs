using System.Collections;
using System.Collections.Generic;
using System.IO;
using FlyingHand;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using Object=UnityEngine.Object;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class CastleVRViewChecks
    {
        const string Request="ArtDirection/VRViewChecks.request",Key="CastleVRViewChecks";
        static CastleVRViewChecks()
        {
            EditorApplication.update+=()=>
            {
                if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorUtility.scriptCompilationFailed||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
                File.Move(Request,Request+".consumed-"+System.DateTime.UtcNow.Ticks);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
            };
            EditorApplication.playModeStateChanged+=s=>
            {
                if(s!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);
                EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Check());
            };
        }
        static IEnumerator Check()
        {
            yield return null;
            var lines=new List<string>();void Assert(bool ok,string message){lines.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines("ArtDirection/VRViewChecks.txt",lines);}
            var session=Object.FindFirstObjectByType<CastlePlaySession>();session.SetMenu(true);
            var saved=InputSystem.settings;var flags=saved.hideFlags;saved.hideFlags=HideFlags.DontUnloadUnusedAsset;
            var settings=Object.Instantiate(saved);settings.hideFlags=HideFlags.HideAndDontSave;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings=settings;
            var hmd=InputSystem.AddDevice<XRHMD>("Survivor head pose regression HMD");
            var staging=new GameObject("Isolated VR view checks");staging.SetActive(false);
            var p=Object.Instantiate(session.survivorPrefab,staging.transform);p.enabled=false;p.controlMode=ControlMode.VR;
            foreach(var camera in p.GetComponentsInChildren<Camera>(true))camera.enabled=false;
            foreach(var listener in p.GetComponentsInChildren<AudioListener>(true))listener.enabled=false;
            staging.SetActive(true);p.RefreshMode();p.InputSuspended=false;
            var driver=p.viewCamera.GetComponent<TrackedPoseDriver>();
            var legacy=new GameObject("Legacy center-eye-only comparison");var old=legacy.AddComponent<TrackedPoseDriver>();old.enabled=false;
            old.rotationInput=new InputActionProperty(new InputAction("Legacy eye rotation",InputActionType.Value,"<XRHMD>/centerEyeRotation",expectedControlType:"Quaternion"));old.ignoreTrackingState=true;old.enabled=true;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(250,39.5f,0);floor.transform.localScale=new Vector3(30,1,30);
            try
            {
                p.Teleport(new Vector3(250,40.04f,0),Quaternion.identity);Physics.SyncTransforms();
                InputSystem.QueueDeltaStateEvent(hmd.trackingState,3);InputSystem.QueueDeltaStateEvent(hmd.devicePosition,new Vector3(.15f,1.65f,-.1f));
                var expected=Quaternion.Euler(18,65,-7);InputSystem.QueueDeltaStateEvent(hmd.deviceRotation,expected);yield return null;yield return null;
                Assert(Quaternion.Angle(old.transform.localRotation,expected)>20,"Legacy center-eye-only binding misses device-pose-only head rotation.");
                Assert(p.IsVR&&driver.enabled&&driver.trackingType==TrackedPoseDriver.TrackingType.RotationAndPosition,"Survivor enables position AND rotation tracking.");
                Assert(Quaternion.Angle(p.viewCamera.transform.localRotation,expected)<.1f,"Device-pose HMD yaw, pitch and roll reach the camera without a joystick.");
                Assert(Vector3.Distance(p.viewCamera.transform.localPosition,new Vector3(.15f,1.65f,-.1f))<.001f,"Physical leaning reaches the camera independently of locomotion.");
                expected=Quaternion.Euler(-12,-70,4);InputSystem.QueueDeltaStateEvent(hmd.centerEyeRotation,expected);InputSystem.QueueDeltaStateEvent(hmd.centerEyePosition,new Vector3(-.1f,1.55f,.2f));yield return null;yield return null;
                Assert(Quaternion.Angle(p.viewCamera.transform.localRotation,expected)<.1f,"Center-eye backends also track physical head turning.");
                p.InputSuspended=true;expected=Quaternion.Euler(25,25,0);InputSystem.QueueDeltaStateEvent(hmd.deviceRotation,expected);yield return null;yield return null;
                Assert(Quaternion.Angle(p.viewCamera.transform.localRotation,expected)<.1f,"Opening the menu never freezes headset looking.");p.InputSuspended=false;
                var local=p.viewCamera.transform.localRotation;p.playerScale=.6f;p.RefreshMode();
                Assert(Quaternion.Angle(p.viewCamera.transform.localRotation,local)<.01f,"Changing player scale does not reset tracked head rotation.");
                p.vrTurn=SurvivorVRTurn.Snap;p.vrSnapAngle=30;p.ApplyVRTurn(0,1f/60);var eye=p.viewCamera.transform.position;var yaw=p.transform.rotation;
                p.ApplyVRTurn(1,1f/60);Assert(Mathf.Abs(Quaternion.Angle(yaw,p.transform.rotation)-30)<.01f,"Snap turning rotates the rig exactly 30 degrees.");
                Assert(Vector3.Distance(eye,p.viewCamera.transform.position)<.001f&&Quaternion.Angle(local,p.viewCamera.transform.localRotation)<.01f,"Stick turn pivots around the eyes without overwriting head pose.");
                yaw=p.transform.rotation;for(int i=0;i<40;i++)p.ApplyVRTurn(1,1f/60);Assert(Quaternion.Angle(yaw,p.transform.rotation)<.01f,"Holding the snap stick does not repeat turns.");
                p.ApplyVRTurn(0,1f/60);p.ApplyVRTurn(-1,1f/60);Assert(Mathf.Abs(Quaternion.Angle(yaw,p.transform.rotation)-30)<.01f,"Releasing the stick rearms snap turn in either direction.");
                p.vrTurn=SurvivorVRTurn.Smooth;p.vrSmoothDegreesPerSecond=60;eye=p.viewCamera.transform.position;yaw=p.transform.rotation;
                for(int i=0;i<60;i++)p.ApplyVRTurn(1,1f/60);
                Assert(Mathf.Abs(Quaternion.Angle(yaw,p.transform.rotation)-60)<.02f&&Vector3.Distance(eye,p.viewCamera.transform.position)<.001f,"Smooth turning follows degrees/second around a stable eye pivot.");
                p.vrTurn=SurvivorVRTurn.PhysicalOnly;yaw=p.transform.rotation;p.ApplyVRTurn(1,1);Assert(Quaternion.Angle(yaw,p.transform.rotation)<.01f,"Physical-only mode ignores joystick turning.");
                expected=Quaternion.Euler(0,90,0);InputSystem.QueueDeltaStateEvent(hmd.deviceRotation,expected);yield return null;yield return null;
                p.Teleport(new Vector3(250,40.04f,0),Quaternion.identity);p.vrMovement=SurvivorVRMovement.BodyRelative;
                var start=p.transform.position;for(int i=0;i<20;i++)p.Simulate(Vector2.up,false,1f/60);
                var displacement=p.transform.position-start;Assert(displacement.z>1&&Mathf.Abs(displacement.x)<.02f,"Stationary movement keeps forward tied to rig facing while looking sideways.");
                p.Teleport(new Vector3(250,40.04f,0),Quaternion.identity);p.vrMovement=SurvivorVRMovement.HeadRelative;start=p.transform.position;
                for(int i=0;i<20;i++)p.Simulate(Vector2.up,false,1f/60);
                displacement=p.transform.position-start;Assert(displacement.x>1&&Mathf.Abs(displacement.z)<.02f,"Fluid movement follows horizontal head direction.");
                InputSystem.QueueDeltaStateEvent(hmd.trackingState,0);yield return null;var frozen=p.viewCamera.transform.localRotation;
                InputSystem.QueueDeltaStateEvent(hmd.deviceRotation,Quaternion.Euler(0,-40,0));yield return null;yield return null;
                Assert(Quaternion.Angle(frozen,p.viewCamera.transform.localRotation)<.01f,"Invalid tracking freezes the last valid pose instead of applying bad input.");
                InputSystem.QueueDeltaStateEvent(hmd.trackingState,3);InputSystem.QueueDeltaStateEvent(hmd.deviceRotation,Quaternion.Euler(0,-39,0));yield return null;yield return null;
                Assert(Quaternion.Angle(p.viewCamera.transform.localRotation,Quaternion.Euler(0,-39,0))<.1f,"Head rotation resumes after tracking recovers.");
                p.UseStationaryVR();Assert(p.vrMovement==SurvivorVRMovement.BodyRelative&&p.vrTurn==SurvivorVRTurn.Snap,"Seated preset uses stable movement plus snap turn.");
                p.UseFluidVR();Assert(p.vrMovement==SurvivorVRMovement.HeadRelative&&p.vrTurn==SurvivorVRTurn.Smooth,"Fluid preset uses head-directed movement plus smooth turn.");
                p.controlMode=ControlMode.Desktop;p.RefreshMode();Assert(!driver.enabled&&Quaternion.Angle(p.viewCamera.transform.localRotation,Quaternion.identity)<.01f,"Desktop mode restores mouse-owned camera rotation.");
                var menu=Object.FindFirstObjectByType<CastleWorldMenu>(FindObjectsInactive.Include);menu.Show(true,p.viewCamera);
                var buttons=menu.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                Assert(buttons.Length==13,"Headset demo menu includes its movement/turning settings entry.");
                var savedMove=session.Player.vrMovement;var savedTurn=session.Player.vrTurn;
                p.viewCamera.transform.LookAt(buttons[12].transform);menu.Point(p.viewCamera,true);menu.Point(p.viewCamera,false);
                Assert(buttons[1].GetComponentInChildren<UnityEngine.UI.Text>().text.StartsWith("Movement:"),"Gaze selection opens the actual VR comfort page.");
                p.viewCamera.transform.LookAt(buttons[5].transform);menu.Point(p.viewCamera,true);
                Assert(session.Player.vrMovement==SurvivorVRMovement.BodyRelative&&session.Player.vrTurn==SurvivorVRTurn.Snap,"Headset menu stationary preset changes the live local survivor.");
                p.viewCamera.transform.LookAt(buttons[6].transform);menu.Point(p.viewCamera,true);
                Assert(session.Player.vrMovement==SurvivorVRMovement.HeadRelative&&session.Player.vrTurn==SurvivorVRTurn.Smooth,"Headset menu fluid preset changes the live local survivor.");
                session.Player.vrMovement=savedMove;session.Player.vrTurn=savedTurn;menu.Show(false,p.viewCamera);
                Assert(Physics.GetIgnoreLayerCollision(CastleBlock.SurvivorLayer,CastleBlock.BlockLayer),"VR changes preserve player/block physics isolation.");
            }
            finally
            {
                old.enabled=false;old.rotationInput.action.Dispose();Object.Destroy(legacy);Object.Destroy(staging);Object.Destroy(floor);
                if(hmd.added)InputSystem.RemoveDevice(hmd);InputSystem.settings=saved;saved.hideFlags=flags;Object.Destroy(settings);
            }
            Debug.Log(string.Join("\n",lines));yield return CastleSeamCameraChecks.Check();
        }
    }
}
