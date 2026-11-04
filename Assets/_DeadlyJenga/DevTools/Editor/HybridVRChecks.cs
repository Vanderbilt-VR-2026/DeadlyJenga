using System;
using System.IO;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CandyPhysicsHands;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using FlyingHand;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class HybridVRChecks
    {
        const string Key="HybridVRChecks";
        static readonly string Report="Logs/Validation/HybridVRChecks.txt";
        static int errors,checks;
        static HybridVRChecks(){EditorApplication.playModeStateChanged+=State;}
        public static void RunBatch()
        {
            Directory.CreateDirectory("Logs/Validation");File.WriteAllText(Report,"Actual Game + synthetic XR device/hand inputs. No headset.\n");
            try
            {
                InstallFeature();
                MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.FromLobby=true;MatchSetup.BotTarget=1;MatchSetup.HandCount=0;
                EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
            }
            catch(Exception e){File.AppendAllText(Report,"FAIL setup "+e);EditorApplication.Exit(2);}
        }
        static void InstallFeature()
        {
            foreach(var group in new[]{BuildTargetGroup.Android,BuildTargetGroup.Standalone})
            {
                var settings=OpenXRSettings.GetSettingsForBuildTargetGroup(group);if(!settings)throw new Exception("OpenXR settings missing: "+group);
                var feature=settings.GetFeature<HybridHandsFeature>();
                if(!feature)
                {
                    feature=ScriptableObject.CreateInstance<HybridHandsFeature>();feature.name="HybridHandsFeature "+group;
                    AssetDatabase.AddObjectToAsset(feature,settings);
                    var data=new SerializedObject(settings);var list=data.FindProperty("features");int n=list.arraySize;list.InsertArrayElementAtIndex(n);list.GetArrayElementAtIndex(n).objectReferenceValue=feature;data.ApplyModifiedPropertiesWithoutUndo();
                }
                var serialized=new SerializedObject(feature);
                serialized.FindProperty("featureIdInternal").stringValue="deadlyjenga.hybrid-hands";
                serialized.FindProperty("openxrExtensionStrings").stringValue="XR_META_simultaneous_hands_and_controllers";
                serialized.ApplyModifiedPropertiesWithoutUndo();feature.enabled=true;EditorUtility.SetDirty(feature);
                foreach(var f in settings.GetFeatures<OpenXRFeature>())
                    if(f.GetType().Name=="HandTracking"||f.GetType().Name=="HandInteractionProfile") {f.enabled=true;EditorUtility.SetDirty(f);}
                EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();Check(OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeature<HybridHandsFeature>().enabled,"Android simultaneous tracking feature is installed and enabled");
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {errors=0;Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Run());}
            if(state==PlayModeStateChange.EnteredEditMode)
            {SessionState.SetBool(Key,false);Application.logMessageReceived-=Log;EditorApplication.Exit(SessionState.GetBool(Key+"Passed",false)?0:2);}
        }
        static void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n");}}
        static void Check(bool ok,string message)
        {checks++;File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+message+"\n");if(!ok)throw new Exception(message);}
        sealed class PoseSource:ICandyHandPoseSource
        {public CandyHandPose pose;public bool TryGetPose(out CandyHandPose value){value=pose;return value.tracked;}}
        static void Refresh(CandyTrackingSource source)=>typeof(CandyTrackingSource).GetMethod("ReadLatest",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(source,null);
        static void Input(InputDevice device,string control,float value)
        {using(var state=StateEvent.From(device,out var inputEvent)){device.GetChildControl<AxisControl>(control).WriteValueIntoEvent(value,inputEvent);InputSystem.QueueEvent(inputEvent);}}
        static IEnumerator Run()
        {
            yield return null;yield return null;
            var session=Object.FindFirstObjectByType<CastlePlaySession>();
            InputDevice left=null,right=null;GameObject fixture=null;bool passed=false;
            var saved=InputSystem.settings;var flags=saved.hideFlags;saved.hideFlags=HideFlags.DontUnloadUnusedAsset;
            var inputSettings=Object.Instantiate(saved);inputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;inputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings=inputSettings;
            int oldPlayStyle=PlayerPrefs.GetInt("SurvivorVRPlayStyle",0);
            try
            {
                Check(session&&session.Player!=null&&Services.World!=null,"Game initializes with production session, survivor and map");session.SetMenu(false);
                InputSystem.RegisterLayout(@"{""name"":""HybridFixController"",""extend"":""XRController"",""controls"":[{""name"":""primary2DAxis"",""layout"":""Vector2""},{""name"":""primaryButton"",""layout"":""Button""},{""name"":""secondaryButton"",""layout"":""Button""},{""name"":""primary2DAxisClick"",""layout"":""Button""},{""name"":""gripPressed"",""layout"":""Button""},{""name"":""trigger"",""layout"":""Axis""},{""name"":""pointerRotation"",""layout"":""Quaternion""}]}");
                left=InputSystem.AddDevice("HybridFixController");InputSystem.SetDeviceUsage(left,CommonUsages.LeftHand);Input(left,"isTracked",1);
                InputSystem.QueueDeltaStateEvent(left.GetChildControl<Vector2Control>("primary2DAxis"),new Vector2(.3f,.8f));
                InputSystem.QueueDeltaStateEvent(left.GetChildControl<QuaternionControl>("pointerRotation"),Quaternion.Euler(5,25,0));InputSystem.Update();
                Check(HybridXRInput.SingleControllerLeft,"Recognizes a left controller without a right controller");
                Check(Vector2.Distance(HybridXRInput.MoveStick(),new Vector2(.3f,.8f))<.001f,"Single left controller supplies walking input");
                fixture=new GameObject("Hybrid tracking fixture");var source=fixture.AddComponent<CandyTrackingSource>();
                var bare=new PoseSource{pose=new CandyHandPose{tracked=true,skeletonTracked=true,rotation=Quaternion.identity,skeleton=CandyHandSkeleton.Rest(false)}};
                var controller=new PoseSource{pose=new CandyHandPose{tracked=true,skeletonTracked=false,rotation=Quaternion.Euler(-45,0,0),skeleton=CandyHandSkeleton.Rest(true)}};
                source.LeftPoseSource=controller;source.RightPoseSource=bare;Refresh(source);
                Check(source.IsHybrid&&source.GestureHand.skeletonTracked,"Recognizes left-controller + right optical-hand combination");
                Check(source.TryControllerAim(true,out var aim)&&Quaternion.Angle(aim.rotation,Quaternion.Euler(5,25,0))<.01f,"Weapon aim follows pointer rotation instead of raised palm/grip rotation");
                var player=session.Survivor();var tracking=player.GetComponent<CandyTrackingSource>();tracking.LeftPoseSource=controller;tracking.RightPoseSource=bare;Refresh(tracking);
                player.controlMode=ControlMode.VR;player.RefreshMode();player.InputSuspended=false;
                typeof(CastleSurvivor).GetField("vrInputFocused",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(player,true);
                typeof(CastleSurvivor).GetMethod("DetectHybridControls",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(player,null);
                Check(player.StandingPlay,"Hybrid detection automatically selects standing gesture controls");
                var h=bare.pose;h.skeleton.SetJoint(3,Vector3.zero);h.skeleton.SetJoint(7,Vector3.right*.1f);
                player.ResetVRJump();player.ReadVRJump(h,false,out _,out _);h.skeleton.SetJoint(7,Vector3.right*.01f);
                player.ReadVRJump(h,false,out var pressed,out var held);Check(pressed&&held,"Tracked hand pinch jumps and holds climbing input");
                player.ReadVRJump(default,false,out _,out _);player.ReadVRJump(default,true,out pressed,out held);Check(pressed&&held,"Standing controller-button fallback works when optical tracking is lost");
                player.UseStationaryVR();player.vrTurn=SurvivorVRTurn.Smooth;player.vrSmoothDegreesPerSecond=360;
                var start=player.transform.rotation;var eye=player.viewCamera.transform.position;player.ApplyVRTurn(1,.25f);
                Check(Mathf.Abs(Quaternion.Angle(start,player.transform.rotation)-90)<.1f,"360-degree-per-second setting produces a 90-degree quarter-second turn");
                Check(Vector3.Distance(eye,player.viewCamera.transform.position)<.001f,"Fast turning preserves the headset pivot");
                player.vrSmoothDegreesPerSecond=90;player.CycleSmoothTurnSpeed();Check(player.vrSmoothDegreesPerSecond==180,"Existing settings menu reaches speeds beyond its previous 90-degree cap");
                Input(left,"isTracked",0);right=InputSystem.AddDevice("HybridFixController");InputSystem.SetDeviceUsage(right,CommonUsages.RightHand);Input(right,"isTracked",1);
                InputSystem.QueueDeltaStateEvent(right.GetChildControl<Vector2Control>("primary2DAxis"),new Vector2(-.2f,.7f));InputSystem.Update();
                source.LeftPoseSource=bare;source.RightPoseSource=controller;Refresh(source);
                Check(source.IsHybrid&&!HybridXRInput.SingleControllerLeft,"Recognizes mirrored right-controller + left tracked-hand combination");
                Check(Vector2.Distance(HybridXRInput.MoveStick(),new Vector2(-.2f,.7f))<.001f,"Single right controller also supplies walking input");
                Input(right,"isTracked",0);InputSystem.Update();Check(!HybridXRInput.SingleController&&HybridXRInput.MoveStick()==Vector2.zero,"Controller tracking loss clears stale joystick movement");
                fixture.transform.SetPositionAndRotation(new Vector3(100,20,-50),Quaternion.Euler(0,70,0));
                var localPose=bare.pose;localPose.position=new Vector3(.3f,1.4f,.2f);
                var spiritPose=SeaSpiritController.ResolveTrackingPose(localPose,null,fixture.transform);
                Check(Vector3.Distance(spiritPose.position,fixture.transform.TransformPoint(localPose.position))<.001f,"Sea monster converts local hand tracking to its distant observer world space");
                Check(Quaternion.Angle(spiritPose.rotation,fixture.transform.rotation*localPose.rotation)<.01f,"Sea monster hand rotation follows observer orientation");
                var worldPose=SeaSpiritController.ResolveTrackingPose(spiritPose,fixture.transform,fixture.transform);
                Check(Vector3.Distance(worldPose.position,spiritPose.position)<.001f,"Already-world-space sea monster poses are not transformed twice");
                Check(System.Runtime.InteropServices.Marshal.SizeOf<HybridHandsFeature.ResumeInfo>()==(IntPtr.Size==8?16:8),"OpenXR resume structure has native pointer alignment");
                passed=true;
            }
            catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");}
            finally
            {
                if(left!=null)InputSystem.RemoveDevice(left);if(right!=null)InputSystem.RemoveDevice(right);if(fixture)Object.Destroy(fixture);
                var p=session.Survivor();var t=p.GetComponent<CandyTrackingSource>();t.LeftPoseSource=t.RightPoseSource=null;p.controlMode=ControlMode.Desktop;
                InputSystem.settings=saved;saved.hideFlags=flags;Object.Destroy(inputSettings);PlayerPrefs.SetInt("SurvivorVRPlayStyle",oldPlayStyle);
                File.AppendAllText(Report,$"COMPLETE checks={checks} errors={errors} passed={passed}\n");SessionState.SetBool(Key+"Passed",passed&&errors==0);EditorApplication.ExitPlaymode();
            }
        }
    }
}
