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
        public static void RunQuestRepairBatch()
        {
            SessionState.SetBool(Key+"ContinueFlow",true);
            RunBatch();
        }
        public static void RunBatch()
        {
            Directory.CreateDirectory("Logs/Validation");File.WriteAllText(Report,"Actual Game + synthetic XR device/hand inputs. No headset.\n");
            try
            {
                Check(OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeature<HybridHandsFeature>()?.enabled==true,"Android simultaneous tracking is enabled in the source snapshot");
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
            {
                SessionState.SetBool(Key,false);Application.logMessageReceived-=Log;
                bool passed=SessionState.GetBool(Key+"Passed",false);
                bool flow=SessionState.GetBool(Key+"ContinueFlow",false);SessionState.SetBool(Key+"ContinueFlow",false);
                if(passed&&flow)EditorApplication.delayCall+=HomeMenuChecks.RunQuestFlowBatch;
                else EditorApplication.Exit(passed?0:2);
            }
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
        static void CheckTrackedAvatarHeight(CastleSurvivor player,CandyPlayerAvatar avatar)
        {
            var eye=player.viewCamera.transform;
            var saved=eye.position;
            var remoteRoot=new GameObject("XR posture remote fixture");
            var remote=remoteRoot.AddComponent<DeadlyJenga.CharacterNetwork.SurvivorNetworkView>()
                .Init(new NetworkContext{localSurvivor=player.gameObject,actorId="xr-posture-fixture"});
            try
            {
                foreach(float height in new[]{.08f,1.05f,2.5f,1.05f})
                {
                    eye.position=new Vector3(saved.x,player.transform.position.y+height*player.playerScale,saved.z);
                    avatar.RefreshNow();
                    var tracking=avatar.LastFrame;
                    Check(Mathf.Abs(tracking.head.y-eye.position.y)<.001f,
                        "Local view and outgoing tracking preserve physical headset height");
                    CheckStandingBody(avatar.Rig,tracking);

                    using var stream=new MemoryStream();
                    using var writer=new BinaryWriter(stream);
                    using var reader=new BinaryReader(stream);
                    avatar.WriteState(writer);
                    stream.Position=0;
                    remote.ReadState(reader,true);
                    remote.Render(1);
                    Check(Mathf.Abs(remote.ViewPose.position.y-tracking.head.y)<.015f,
                        "Remote gameplay pose preserves transmitted headset height");
                    CheckStandingBody(remote.Visual.GetComponent<CandyPlayerRig>(),tracking);
                }
            }
            finally
            {
                eye.position=saved;
                avatar.RefreshNow();
                Object.Destroy(remoteRoot);
            }
        }

        static void CheckStandingBody(CandyPlayerRig rig,CandyAvatarFrame tracking)
        {
            float scale=tracking.scale;
            float expected=(1.6f-.14f)*scale+
                (tracking.headRotation*(rig.style?rig.style.headCenterOffset:new Vector3(0,0,-.08f))).y*scale;
            Check(Mathf.Abs(rig.HeadAnchor.position.y-tracking.feet.y-expected)<.015f,
                "Tracked height changes leave the rendered avatar head at standing height");
            Check(Mathf.Abs(rig.BodyAnchor.localScale.y-1)<.015f,
                "Tracked height changes do not compress or stretch the torso");
            foreach(int index in new[]{10,13})
            {
                float foot=rig.AppearanceBone(index).position.y;
                Check(foot>=tracking.feet.y-.015f&&foot<rig.HeadAnchor.position.y,
                    "The avatar foot stays below its head and above the floor");
            }
        }

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
                var leftDevice=(XRController)left;
                var controllerPosition=new Vector3(-.22f,1.21f,.35f);
                InputSystem.QueueDeltaStateEvent(leftDevice.devicePosition,controllerPosition);
                InputSystem.QueueDeltaStateEvent(leftDevice.deviceRotation,Quaternion.Euler(12,23,34));InputSystem.Update();
                Refresh(source);
                var actual=source.WorldHand(true);
                Check(actual.tracked&&!actual.skeletonTracked&&Vector3.Distance(actual.position,controllerPosition)<.001f,"Live controller grip pose stays distinct from optical gesture input");
                Check(Quaternion.Angle(actual.rotation,Quaternion.Euler(12,23,34))<.01f,"Live controller uses device grip orientation, not pointer rotation");
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
                var tiltBefore=player.viewCamera.transform.rotation;
                player.ApplyVRLook(Vector2.up,.2f);
                Check(Quaternion.Angle(tiltBefore,player.viewCamera.transform.rotation)>10&&Vector3.Distance(eye,player.viewCamera.transform.position)<.001f,"Artificial pitch changes view without moving tracked eye pivot");
                var open=CandyHandSkeleton.ControllerGrip(false,0,0);
                var fired=CandyHandSkeleton.ControllerGrip(false,0,1);
                Check(Vector3.Distance(open.Joint(7),fired.Joint(7))>.008f&&open.Joint(11)==fired.Joint(11),"Trigger curl moves only the index finger");
                var other=CandyHandSkeleton.ControllerGrip(true,0,0);
                Check(Mathf.Abs(other.Joint(3).x+open.Joint(3).x)<.002f,"Controller thumb geometry mirrors between hands");
                Check(DeadlyJenga.Presentation.CandyGunSights.EyeBoxContains(Vector3.back*.10f,Vector3.forward,Vector3.zero,Vector3.forward,1)&&!DeadlyJenga.Presentation.CandyGunSights.EyeBoxContains(new Vector3(.09f,0,-.10f),Vector3.forward,Vector3.zero,Vector3.forward,1),"Rifle eye box accepts on-axis relief and rejects side offset");
                foreach(EquipmentKind kind in Enum.GetValues(typeof(EquipmentKind)))
                {
                    var spec=EquipmentSpecifications.Get(kind);
                    var equipment=new EquipmentState{kind=kind};
                    Check(DeadlyJenga.Presentation.CandyGunSights.SupportsAim(equipment)==(kind!=EquipmentKind.None&&!spec.power&&!spec.melee),kind+" ranged aim availability");
                }
                foreach(var kind in new[]{EquipmentKind.Chainsaw,EquipmentKind.LaserAxe})
                    Check(DeadlyJenga.Presentation.CandyGunSights.SupportsAim(new EquipmentState{kind=kind,shotgun=true}),kind+" ranged mode supports aiming");
                var muzzle=new Pose(Vector3.forward*.5f,Quaternion.identity);
                Check(DeadlyJenga.Presentation.CandyGunSights.OpenSightAligned(Vector3.zero,Vector3.forward,muzzle)&&
                      !DeadlyJenga.Presentation.CandyGunSights.OpenSightAligned(Vector3.up*.4f,Vector3.forward,muzzle),"Scopeless VR aim detects a raised weapon and rejects a lowered weapon");
                var avatar=player.GetComponent<CandyPlayerAvatar>();var rig=avatar.Rig;var sample=avatar.ReadFrame();
                rig.ApplyFrame(sample);float uprightHead=rig.HeadAnchor.position.y;
                rig.SeatedVisual=true;sample.seated=true;rig.ApplyFrame(sample);
                Check(Mathf.Abs(uprightHead-rig.HeadAnchor.position.y)<.001f,"Seated comfort keeps the avatar in its upright pose");
                rig.SeatedVisual=false;
                using(var packet=new MemoryStream())
                using(var writer=new BinaryWriter(packet))
                using(var reader=new BinaryReader(packet))
                {
                    sample.seated=true;
                    DeadlyJenga.CharacterNetwork.CharacterWire.WriteSurvivor(writer,sample,0,default);
                    packet.Position=0;
                    DeadlyJenga.CharacterNetwork.CharacterWire.ReadSurvivor(reader,out var received,out _,out _,out _);
                    rig.ApplyFrame(received);
                    Check(received.seated&&Mathf.Abs(uprightHead-rig.HeadAnchor.position.y)<.001f,"The seated wire flag decodes without changing the upright avatar pose");
                    packet.SetLength(0);
                    sample.seated=false;
                    DeadlyJenga.CharacterNetwork.CharacterWire.WriteSurvivor(writer,sample,0,default);
                    packet.Position=0;
                    DeadlyJenga.CharacterNetwork.CharacterWire.ReadSurvivor(reader,out received,out _,out _,out _);
                    Check(!received.seated,"Existing survivor packet without seated flag still decodes");
                }
                CheckTrackedAvatarHeight(player,avatar);
                var testSkin=new Mesh{vertices=new[]{Vector3.zero},bindposes=new Matrix4x4[16]};
                testSkin.boneWeights=new[]{new BoneWeight{boneIndex0=1,weight0=.6f,boneIndex1=4,weight1=.4f}};
                var split=typeof(CandySmoothHandMesh).GetMethod("SeparateDigitWeights",BindingFlags.Static|BindingFlags.NonPublic);
                var fixedSkin=(Mesh)split.Invoke(null,new object[]{testSkin,1,5});
                Check(fixedSkin!=testSkin&&Mathf.Abs(fixedSkin.boneWeights[0].weight0-1)<.001f,"Legacy skin vertex no longer follows a neighboring finger");
                Object.Destroy(testSkin);Object.Destroy(fixedSkin);
                player.vrSmoothDegreesPerSecond=90;player.CycleSmoothTurnSpeed();Check(player.vrSmoothDegreesPerSecond==180,"Existing settings menu reaches speeds beyond its previous 90-degree cap");
                InputSystem.DisableDevice(left);right=InputSystem.AddDevice("HybridFixController");InputSystem.SetDeviceUsage(right,CommonUsages.RightHand);Input(right,"isTracked",1);
                InputSystem.QueueDeltaStateEvent(right.GetChildControl<Vector2Control>("primary2DAxis"),new Vector2(-.2f,.7f));InputSystem.Update();
                source.LeftPoseSource=bare;source.RightPoseSource=controller;Refresh(source);
                Check(source.IsHybrid&&!HybridXRInput.SingleControllerLeft,"Recognizes mirrored right-controller + left tracked-hand combination");
                Check(Vector2.Distance(HybridXRInput.MoveStick(),new Vector2(-.2f,.7f))<.001f,"Single right controller also supplies walking input");
                InputSystem.EnableDevice(left);
                Input(left,"isTracked",1);InputSystem.Update();source.LeftPoseSource=source.RightPoseSource=null;Refresh(source);
                Check(!source.IsHybrid&&source.WorldHand(true).tracked&&source.WorldHand(false).tracked&&
                    !source.WorldHand(true).skeletonTracked&&!source.WorldHand(false).skeletonTracked,"Two controllers own only their matching sides");
                source.LeftPoseSource=bare;
                Input(left,"isTracked",0);InputSystem.Update();Refresh(source);
                Check(!HybridXRInput.SingleController&&!source.IsHybrid&&!source.WorldHand(true).skeletonTracked,
                    "A brief loss of the movement controller does not enable optical gestures or single-controller mode");
                Check(HybridXRInput.MovementController(false)==null&&HybridXRInput.Stick(false).sqrMagnitude>.01f,
                    "A brief loss does not route the dominant look stick to movement");
                Input(left,"isTracked",1);InputSystem.Update();Refresh(source);
                Check(HybridXRInput.MovementController(false)==left&&!source.IsHybrid,
                    "Movement resumes on the original controller when its tracking returns");
                source.RightPoseSource=bare;
                Input(right,"isTracked",0);InputSystem.Update();Refresh(source);
                Check(!HybridXRInput.SingleController&&!source.IsHybrid&&HybridXRInput.Stick(false)==Vector2.zero,
                    "Losing the dominant controller briefly keeps the dual layout without stale look input");
                InputSystem.DisableDevice(left);InputSystem.DisableDevice(right);
                Check(!HybridXRInput.SingleController&&HybridXRInput.MoveStick()==Vector2.zero,
                    "Disabling controllers releases ownership and clears joystick movement");
                source.LeftPoseSource=new PoseSource{pose=new CandyHandPose{tracked=true,skeletonTracked=true,rotation=Quaternion.identity,skeleton=CandyHandSkeleton.Rest(true)}};
                source.RightPoseSource=bare;Refresh(source);
                Check(!source.IsHybrid&&source.WorldHand(true).skeletonTracked&&source.WorldHand(false).skeletonTracked,"Hand-only inputs retain both independent skeletons after controller loss");
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
