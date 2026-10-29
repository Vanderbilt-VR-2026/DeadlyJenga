using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CandyPhysicsHands;
using DeadlyJenga.CastleBots;
using FlyingHand;
using DeadlyJenga.Core;
using DeadlyJenga.CharacterNetwork;
using Object = UnityEngine.Object;

namespace DeadlyJenga.HandValidation
{
    // Runs only when explicitly requested. No scenes or prefabs are saved.
    [InitializeOnLoad]
    public static class FlyingHandTrackingValidation
    {
        const string Key = "FlyingHandTrackingValidation";
        static readonly Stack<IEnumerator> work = new Stack<IEnumerator>();
        static string Report => ProjectPath.Report("FlyingHandTracking.txt");
        static FlyingHandTrackingValidation()
        {
            EditorApplication.playModeStateChanged += Changed;
            EditorApplication.update+=()=>
            {
                var request=ProjectPath.Report("FlyingHandTrackingExpansionMain.request");
                if(!File.Exists(request)||File.ReadAllText(request).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString()+":expanded2"||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                File.Delete(request);Run();
            };
            EditorApplication.delayCall += () =>
            {
                // Another task can recompile the shared project while this runner is active.
                if(SessionState.GetInt(Key,0)==0||!EditorApplication.isPlaying||work.Count!=0)return;
                int restarts=SessionState.GetInt(Key+"Restarts",0)+1;
                SessionState.SetInt(Key+"Restarts",restarts);
                SessionState.SetBool(Key+"Restart",restarts<=3);
                if(restarts>3){SessionState.SetBool(Key+"Failed",true);File.AppendAllText(Report,"FAIL: repeated script reloads interrupted validation.\n");}
                EditorApplication.ExitPlaymode();
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validate Flying Hand Tracking")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start in Edit Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            File.WriteAllText(Report, "Flying hand integration checks\n");
            SessionState.SetBool(Key + "Failed", false);
            SessionState.SetInt(Key+"Checks",0);
            SessionState.SetInt(Key+"Restarts",0);SessionState.SetBool(Key+"Restart",false);
            SessionState.SetString(Key+"PreviousScene",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            SessionState.SetInt(Key, 1);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange state)
        {
            int stage = SessionState.GetInt(Key, 0);
            if (stage == 0) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Application.logMessageReceived += Log;
                work.Push(stage == 1 ? CheckPrefab() : stage == 2 ? CheckGame() : CheckMagneticMap());
                EditorApplication.update += Tick;
            }
            if (state != PlayModeStateChange.EnteredEditMode) return;
            Application.logMessageReceived -= Log;
            if(SessionState.GetBool(Key+"Restart",false))
            {
                SessionState.SetBool(Key+"Restart",false);SessionState.SetBool(Key+"Failed",false);SessionState.SetInt(Key,1);
                File.WriteAllText(Report,"Flying hand integration checks (restarted after shared-project script reload)\n");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();return;
            }
            if(SessionState.GetInt(Key+"Checks",0)==0)
            {
                SessionState.SetBool(Key+"Failed",true);
                File.AppendAllText(Report,"FAIL: Play Mode ended before any checks ran (check the Unity compilation log).\n");
            }
            if (stage == 1 && !SessionState.GetBool(Key + "Failed", false))
            {
                SessionState.SetInt(Key, 2);
                SessionState.SetInt(Key+"Checks",0);
                EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
                EditorApplication.EnterPlaymode();
                return;
            }
            if(stage==2&&!SessionState.GetBool(Key+"Failed",false))
            {
                SessionState.SetInt(Key,3);SessionState.SetInt(Key+"Checks",0);
                EditorSceneManager.OpenScene(ProjectPath.Find("MapDrafterEvan2.unity"));EditorApplication.EnterPlaymode();return;
            }
            bool failed = SessionState.GetBool(Key + "Failed", false);
            SessionState.SetInt(Key, 0);
            File.AppendAllText(Report, failed ? "FAILED\n" : "COMPLETE: editor checks passed; Quest hardware not tested.\n");
            if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
            else if(!string.IsNullOrEmpty(SessionState.GetString(Key+"PreviousScene","")))EditorSceneManager.OpenScene(SessionState.GetString(Key+"PreviousScene",""));
        }
        static void Log(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            SessionState.SetBool(Key + "Failed", true);
            File.AppendAllText(Report, "FAIL Unity: " + message + "\n" + stack + "\n");
        }
        static void Tick()
        {
            try
            {
                if (work.Count == 0)
                {
                    EditorApplication.update -= Tick;
                    EditorApplication.ExitPlaymode();
                    return;
                }
                var next = work.Peek();
                if (!next.MoveNext()) { work.Pop(); return; }
                if (next.Current is IEnumerator nested) work.Push(nested);
            }
            catch (Exception e)
            {
                SessionState.SetBool(Key + "Failed", true);
                File.AppendAllText(Report, "FAIL " + e + "\n");
                work.Clear();
            }
        }
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            SessionState.SetInt(Key+"Checks",SessionState.GetInt(Key+"Checks",0)+1);
            File.AppendAllText(Report, "PASS " + message + "\n");
        }
        static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
        sealed class Poses : ICandyHandPoseSource
        {
            public CandyHandPose pose = new CandyHandPose
            {
                tracked = true, skeletonTracked = true, position = new Vector3(.2f, 1.2f, .4f),
                rotation = Quaternion.identity, skeleton = CandyHandSkeleton.Rest(false)
            };
            public bool TryGetPose(out CandyHandPose value) { value = pose; return pose.tracked; }
        }
        static IEnumerator CheckPrefab()
        {
            CheckGestures();
            var prefab = AssetDatabase.LoadAssetAtPath<CastleHandActor>(ProjectPath.Find("CastleFlyingHand.prefab"));
            Check(prefab, "Actual flying-hand prefab found");
            var actor = Object.Instantiate(prefab);
            var motor = actor.motor;
            motor.captureDesktopCursor = false;
            motor.ControlModeOverride = ControlMode.VR;
            motor.RefreshControlMode();
            actor.Place(new Vector3(0, 20, 0), Quaternion.identity);
            var bridge = actor.GetComponent<CandyFlyingHandBridge>();
            var input = new Poses();
            bridge.PoseSource = input;
            yield return Wait(1);
            Check(bridge.Mode == CandyHandMode.HandTracking && bridge.PhysicalHand && bridge.PhysicalHand.IsInteractive, "Local VR path activates articulated physical hand");
            Check(bridge.PhysicalHand.bodies.Length == 16, "Palm and all fifteen finger segments exist");
            actor.follow.SnapToTarget();
            Vector3 camera = actor.follow.transform.position;
            Vector3 palm = bridge.PhysicalHand.bodies[0].position;
            input.pose.position += Vector3.right * .3f;
            yield return Wait(.25f);
            float targetError = Vector3.Distance(bridge.PhysicalHand.Target.position, motor.transform.position + Vector3.right * 6);
            Check(targetError < .15f, "30 cm wrist movement reaches 20x pose target within 250 ms (error " + targetError.ToString("F3") + " m)");
            yield return Wait(1);
            Check(bridge.PhysicalHand.bodies[0].position.x - palm.x > 5.5f, "Physical palm follows six-metre scaled wrist displacement");
            Check(Vector3.Distance(camera, actor.follow.transform.position) < .02f, "Moving the wrist does not translate the observer camera");
            input.pose.rotation = Quaternion.Euler(0, 0, 60);
            yield return Wait(1);
            Check(Quaternion.Angle(bridge.PhysicalHand.bodies[0].rotation, input.pose.rotation) < 8, "Physical palm follows wrist rotation");
            motor.InputSuspended = true;
            input.pose.position += Vector3.up * .05f;
            yield return Wait(.5f);
            Check(bridge.PhysicalHand.skin.enabled && !bridge.PhysicalHand.IsInteractive, "Menu suspension preserves visible tracking and disables contacts");
            Check(bridge.PhysicalHand.Target.position.y > 20.9f, "Tracked target keeps updating while menu is open");
            motor.InputSuspended = false;
            yield return Wait(.4f);
            Check(bridge.PhysicalHand.IsInteractive, "Contacts resume after closing menu in clear space");
            input.pose.tracked = false;
            yield return Wait(.6f);
            Check(bridge.Mode == CandyHandMode.Inactive && !bridge.PhysicalHand.gameObject.activeSelf, "Tracking loss releases contacts and hides stale hand");
            input.pose.tracked = true;
            yield return Wait(.8f);
            Check(bridge.PhysicalHand.IsInteractive, "Tracking recovery reactivates physical hand");
            input.pose.skeletonTracked = false;
            yield return Wait(.8f);
            Check(bridge.Mode == CandyHandMode.Controller && bridge.PhysicalHand.IsInteractive, "Controller fallback rebuilds and activates geometry");
            input.pose.skeletonTracked = true;
            yield return Wait(.8f);
            Check(bridge.Mode == CandyHandMode.HandTracking && bridge.PhysicalHand.IsInteractive, "Controller-to-hand transition restores articulated tracking");
            actor.gameObject.SetActive(false);
            actor.gameObject.SetActive(true);
            actor.Place(new Vector3(30, 20, 0), Quaternion.identity);
            yield return Wait(.8f);
            Check(Vector3.Distance(bridge.PhysicalHand.bodies[0].position, motor.transform.position) < .1f, "Role re-entry builds at new spawn instead of dragging old rig across world");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name="Hand validation wall";wall.transform.position=new Vector3(32,20,0);wall.transform.localScale=new Vector3(.5f,12,12);
            input.pose.position+=Vector3.right*.2f;
            yield return Wait(2);
            Check(bridge.PhysicalHand.Target.position.x>33.9f&&bridge.PhysicalHand.bodies[0].position.x<32, "Tracked target beyond wall does not teleport physical palm through it");
            Check(bridge.PhysicalHand.MaximumAppliedForce<=bridge.settings.palmForce+.01f,"Physical contact forces stay bounded");
            Object.Destroy(wall);
            yield return Wait(1);
            bridge.NetworkPreview = true;
            yield return Wait(.2f);
            Check(bridge.PhysicalHand.bodies[0].isKinematic && !bridge.PhysicalHand.bodies[0].GetComponent<Collider>().enabled, "Client preview has no authoritative contacts");
            bridge.NetworkPreview = false;
            motor.externallyDriven = true;
            input.pose.position = new Vector3(40, 20, 0);
            bridge.ResetExternalRig();
            yield return Wait(.8f);
            Check(Vector3.Distance(bridge.PhysicalHand.Target.position, input.pose.position) < .01f, "External bot poses remain world-space without local scaling");
            motor.externallyDriven = false;
            bridge.PoseSource = null;
            motor.ControlModeOverride = ControlMode.Desktop;
            bridge.ResetTrackingRig();
            yield return Wait(.8f);
            Check(bridge.Mode == CandyHandMode.Desktop && bridge.PhysicalHand.IsInteractive, "Desktop mode still creates an interactive hand");
            File.AppendAllText(Report,"FLIGHT fixture focus before="+typeof(FlyingHandController).GetField("focused",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(motor)+" application="+Application.isFocused+"\n");
            motor.SendMessage("OnApplicationFocus",true);
            var tracking=actor.GetComponent<CandyTrackingSource>();
            var steeringInput=new Poses();tracking.LeftPoseSource=steeringInput;
            var steering=new Poses().pose;steering.skeleton=CandyHandSkeleton.Rest(true);
            var open=steering.skeleton;
            var pinch=open;pinch.SetJoint(3,pinch.Joint(7)+Vector3.right*.015f);
            pinch.SetJoint(2,pinch.Joint(3)+Vector3.back*.025f);pinch.SetJoint(1,pinch.Joint(2)+Vector3.back*.025f);
            steeringInput.pose=steering;
            motor.ControlModeOverride=ControlMode.VR;
            bridge.PoseSource=input;
            yield return Wait(.2f);
            steering.skeleton=pinch;steeringInput.pose=steering;
            yield return Wait(.1f);
            steering.position+=Vector3.forward*.15f;steeringInput.pose=steering;
            yield return Wait(.4f);
            File.AppendAllText(Report,"FLIGHT move="+motor.MoveInput+" velocity="+motor.Body.linearVelocity+" mode="+motor.ActiveControlMode+" suspended="+motor.InputSuspended+" source="+tracking.HasHandTracking+" valid="+CandyHandTransition.IsValidPose(steering)+"\n");
            Check(motor.MoveInput.y>.99f&&motor.Body.linearVelocity.z>.1f,"Tracked source through bridge drives real flight motor forward");
            motor.InputSuspended=true;
            yield return Wait(.1f);
            Check(motor.MoveInput==Vector2.zero,"Motor clears gesture input when suspended");
            motor.InputSuspended=false;
            yield return Wait(.1f);
            Check(motor.MoveInput==Vector2.zero,"Motor does not resume held pinch after suspension");
            steering.skeletonTracked=false;steeringInput.pose=steering;
            tracking.RightPoseSource=new Poses();yield return Wait(.1f);
            Check(!bridge.TryReadFlightInput(out _),"A controller in the steering hand does not get swallowed by gesture flight");
            CheckControllerBindings(motor);
            CheckFlightResponse(motor,actor.follow);
            Object.Destroy(actor.gameObject);
            yield return Wait(.2f);
            Check(Object.FindObjectsByType<CandyPhysicalHand>(FindObjectsSortMode.None).Length == 0, "Actor teardown leaves no physical-hand rig behind");
            yield return CheckSeaNetwork(prefab);
            yield return CheckBotContact(prefab);
        }
        static byte[] Pack(Action<BinaryWriter> write)
        {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){write(writer);return stream.ToArray();}}
        static void Read(INetworkActorView view,byte[] bytes)
        {using(var stream=new MemoryStream(bytes))using(var reader=new BinaryReader(stream))view.ReadState(reader,true);}
        static IEnumerator CheckSeaNetwork(CastleHandActor prefab)
        {
            var template=Object.Instantiate(prefab);template.InputSuspended=true;template.motor.enabled=false;template.follow.enabled=false;
            foreach(var camera in template.GetComponentsInChildren<Camera>(true))camera.enabled=false;
            var templateBridge=template.GetComponent<CandyFlyingHandBridge>();templateBridge.enabled=false;
            var pose=new CandyHandPose{tracked=true,skeletonTracked=true,position=new Vector3(0,20,0),rotation=Quaternion.identity,skeleton=DesktopHandPoses.Build(true,DesktopHandPose.Flat,0)};
            byte[] HandFrame(bool left,Vector3 at)=>Pack(w=>{var p=pose;p.position=at;p.skeleton=DesktopHandPoses.Build(left,DesktopHandPose.Flat,0);CharacterWire.WriteHand(w,left,templateBridge.settings.size,p,(Pose[])null);});
            byte[] SeaFrame(Vector3 delta)=>Pack(w=>
            {
                var placeholder=pose;placeholder.tracked=false;CharacterWire.WriteHand(w,false,templateBridge.settings.size,placeholder,(Pose[])null);
                CharacterWire.WriteSeaSpirit(w,new CharacterWire.SeaFrame{bust=new Pose(new Vector3(0,20,-20),Quaternion.identity),leftShoulder=new Pose(new Vector3(-2,20,-20),Quaternion.identity),rightShoulder=new Pose(new Vector3(2,20,-20),Quaternion.identity),left=HandFrame(true,new Vector3(-6,20,0)+delta),right=HandFrame(false,new Vector3(6,20,0)+delta)});
            });
            var host=new GameObject("Host spirit proxy").AddComponent<HandNetworkView>().Init(new NetworkContext{host=true,localHand=template.gameObject});
            Read(host,SeaFrame(Vector3.zero));yield return Wait(.3f);
            Check(host.Active&&host.LeftPhysicalHand&&host.RightPhysicalHand&&!host.Hand,"Sea wire creates two host physical hands and disables main hand");
            Check(host.LeftPhysicalHand.left&&!host.RightPhysicalHand.left,"Host preserves distinct left/right spirit hands");
            var client=new GameObject("Client spirit proxy").AddComponent<HandNetworkView>().Init(new NetworkContext{host=false,localHand=template.gameObject});
            Read(client,Pack(host.WriteState));client.Render(1);
            var before=client.LeftPhysicalHand.bodies[0].position;client.LeftPhysicalHand.StepDrives(.02f);
            Check(client.Active&&client.LeftPhysicalHand.ReplicatedBones&&Vector3.Distance(before,client.LeftPhysicalHand.bodies[0].position)<.001f,"Relayed spirit bones survive client FixedUpdate without local target overwrite");
            Check(Vector3.Distance(host.LeftHandAnchor.position,client.LeftHandAnchor.position)<.05f,"Sea palm anchors roundtrip through authoritative bones");
            var blocks=new Rigidbody[2];var starts=new Vector3[2];
            for(int i=0;i<2;i++)
            {
                var hand=i==0?host.LeftPhysicalHand:host.RightPhysicalHand;
                var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.transform.localScale=Vector3.one*1.5f;float front=hand.bodies[0].position.z;foreach(var collider in hand.GetComponentsInChildren<Collider>())front=Mathf.Max(front,collider.bounds.max.z);
                cube.transform.position=new Vector3(hand.bodies[0].position.x,hand.bodies[0].position.y,front+1.1f);
                blocks[i]=cube.AddComponent<Rigidbody>();blocks[i].mass=2;blocks[i].useGravity=false;blocks[i].linearDamping=1;starts[i]=blocks[i].position;
            }
            float end=Time.time+1.6f;
            while(Time.time<end){Read(host,SeaFrame(Vector3.forward*Mathf.Min(4,(Time.time-(end-1.6f))*3)));yield return null;}
            for(int i=0;i<2;i++){Check(Vector3.Distance(blocks[i].position,starts[i])>.15f,"Remote spirit hand "+i+" physically moves contact block");Object.Destroy(blocks[i].gameObject);}
            host.Suspend();Check(!host.Active&&!host.LeftPhysicalHand.IsInteractive&&!host.RightPhysicalHand.IsInteractive,"Sea suspension disables both contact rigs");
            Read(host,HandFrame(false,new Vector3(0,20,0)));Check(!host.Active&&host.Hand,"Normal hand packet safely exits dual spirit mode");
            bool rejected=false;try{Read(host,HandFrame(false,Vector3.one*999999));}catch(InvalidDataException){rejected=true;}
            Check(Services.World==null||rejected,"Out-of-map hand payload rejected when map bounds exist");
            Object.Destroy(host.gameObject);Object.Destroy(client.gameObject);Object.Destroy(template.gameObject);yield return Wait(.2f);
        }
        static void CheckControllerBindings(FlyingHandController motor)
        {
            var savedSettings=InputSystem.settings;var savedFlags=savedSettings.hideFlags;savedSettings.hideFlags=HideFlags.DontUnloadUnusedAsset;
            var testSettings=Object.Instantiate(savedSettings);testSettings.hideFlags=HideFlags.HideAndDontSave;testSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;testSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings=testSettings;
            const string layout="FlyingHandValidationController";
            InputSystem.RegisterLayout(@"{""name"":""FlyingHandValidationController"",""extend"":""XRController"",""controls"":[{""name"":""primary2DAxis"",""layout"":""Vector2""},{""name"":""trigger"",""layout"":""Axis""},{""name"":""grip"",""layout"":""Axis""},{""name"":""primaryButton"",""layout"":""Button""}]}");
            var device=InputSystem.AddDevice(layout);
            try
            {
                InputSystem.SetDeviceUsage(device,CommonUsages.LeftHand);
                using(var state=StateEvent.From(device,out var trackedEvent)){device.GetChildControl<ButtonControl>("isTracked").WriteValueIntoEvent(1f,trackedEvent);InputSystem.QueueEvent(trackedEvent);}
                InputSystem.QueueDeltaStateEvent(device.GetChildControl<Vector2Control>("primary2DAxis"),Vector2.up);
                InputSystem.QueueDeltaStateEvent(device.GetChildControl<AxisControl>("trigger"),1f);
                InputSystem.Update();
                var update=typeof(FlyingHandController).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
                update.Invoke(motor,null);
                File.AppendAllText(Report,"BINDINGS device="+device.enabled+" tracked="+device.GetChildControl<ButtonControl>("isTracked").ReadValue()+" move="+motor.move.ReadValue<Vector2>()+" up="+motor.flightUp.ReadValue<float>()+" enabled="+motor.move.enabled+" mode="+motor.ActiveControlMode+" suspended="+motor.InputSuspended+" output="+motor.MoveInput+"/"+motor.VerticalInput+"\n");
                Check(motor.MoveInput.y>.99f&&motor.VerticalInput>.99f,"Actual Input System left-controller bindings drive forward and ascent with right hand tracked");
                InputSystem.QueueDeltaStateEvent(device.GetChildControl<AxisControl>("trigger"),0f);
                InputSystem.QueueDeltaStateEvent(device.GetChildControl<AxisControl>("grip"),1f);
                InputSystem.Update();update.Invoke(motor,null);
                Check(motor.VerticalInput<-.99f,"Actual left grip binding drives descent without a right controller");
                using(var state=StateEvent.From(device,out var buttonEvent)){device.GetChildControl<ButtonControl>("primaryButton").WriteValueIntoEvent(1f,buttonEvent);InputSystem.QueueEvent(buttonEvent);}
                InputSystem.Update();update.Invoke(motor,null);
                Check(motor.MoveInput==Vector2.zero,"Actual X binding selects turning instead of horizontal flight");
            }
            finally{InputSystem.RemoveDevice(device);InputSystem.RemoveLayout(layout);InputSystem.settings=savedSettings;savedSettings.hideFlags=savedFlags;Object.Destroy(testSettings);}
        }
        static void CheckFlightResponse(FlyingHandController motor,ThirdPersonVRFollow follow)
        {
            var read=typeof(FlyingHandController).GetMethod("ReadLeftController",BindingFlags.Instance|BindingFlags.NonPublic);
            var step=typeof(FlyingHandController).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
            void Input(Vector2 stick,float up=0,float down=0,bool turn=false)=>read.Invoke(motor,new object[]{stick,up,down,turn});
            Input(new Vector2(.1f,0));Check(motor.MoveInput==Vector2.zero,"Left stick deadzone prevents idle drift");
            Input(Vector2.up*.5f);Check(motor.MoveInput.y>.1f&&motor.MoveInput.y<.2f,"Half stick provides precise low-speed flight");
            Input(Vector2.zero,1);Check(motor.VerticalInput==1,"Left trigger commands ascent");
            Input(Vector2.zero,0,1);Check(motor.VerticalInput==-1,"Left grip commands descent");
            motor.Body.rotation=Quaternion.identity;motor.Body.linearVelocity=Vector3.zero;
            Input(Vector2.up);
            for(int i=0;i<60;i++)step.Invoke(motor,null);
            Check(Mathf.Abs(motor.Body.linearVelocity.z-motor.vrFlightSpeed)<.01f,"VR flight reaches configured speed without exceeding it");
            Input(Vector2.zero);
            for(int i=0;i<20;i++)step.Invoke(motor,null);
            Check(motor.Body.linearVelocity.magnitude<.01f,"Releasing flight input brakes to a stop within 400 ms");
            follow.SnapToTarget();Input(Vector2.right,0,0,true);
            Check(Mathf.Abs(Mathf.DeltaAngle(0,motor.Body.rotation.eulerAngles.y)-30)<.01f,"X plus stick commands one 30-degree snap turn");
            Input(Vector2.right,0,0,true);
            Check(Mathf.Abs(Mathf.DeltaAngle(0,motor.Body.rotation.eulerAngles.y)-30)<.01f,"Held turn stick does not repeat until centered");
            motor.transform.rotation=motor.Body.rotation;follow.StepFollow(.016f);
            var position=follow.transform.position;follow.StepFollow(.016f);
            Check(Vector3.Distance(position,follow.transform.position)<.001f,"VR camera does not drift after a snap turn");
            Check(Quaternion.Angle(follow.transform.rotation,motor.Body.rotation)<.01f,"VR camera and flight heading agree after snap turn");
        }
        static IEnumerator CheckBotContact(CastleHandActor prefab)
        {
            var staging=new GameObject("Inactive bot setup");staging.SetActive(false);
            var actor=Object.Instantiate(prefab,staging.transform);
            actor.motor.externallyDriven=true;actor.motor.captureDesktopCursor=false;actor.follow.enabled=false;
            foreach(var camera in actor.GetComponentsInChildren<Camera>(true))camera.enabled=false;
            var bot=actor.gameObject.AddComponent<CastleHandBotBrain>();
            actor.transform.SetParent(null);Object.Destroy(staging);
            var personality=CastleHandBotPersonality.Create(2);
            bot.Initialize(null,actor,personality,1);
            actor.Place(new Vector3(0,19.6f,-5),Quaternion.identity);
            var block=GameObject.CreatePrimitive(PrimitiveType.Cube);block.name="Push test block";
            block.transform.position=new Vector3(0,20,0);block.transform.localScale=Vector3.one*2;
            var body=block.AddComponent<Rigidbody>();body.mass=20;body.useGravity=false;body.linearDamping=3;
            body.constraints=RigidbodyConstraints.FreezeRotation|RigidbodyConstraints.FreezePositionY;
            bot.SetTarget(body);
            float end=Time.time+25,worstAngle=0,worstAnchor=0;
            bool sawPreparation=false,sawPush=false,sawRecovery=false;
            string lastIntent="";
            while(Time.time<end)
            {
                var hand=bot.Bridge.PhysicalHand;
                if(hand&&hand.IsInteractive)
                {
                    if(bot.Intent!=lastIntent)
                    {
                        File.AppendAllText(Report,"BOT "+bot.Intent+" at "+Time.time.ToString("F2")+"\n");
                        CaptureBot(actor,block,"bot-"+(sawPush?"recover":sawPreparation?"push":"approach")+".png");lastIntent=bot.Intent;
                    }
                    sawPreparation|=bot.Preparing;sawPush|=bot.Striking;
                    sawRecovery|=bot.Withdrawing;
                    for(int i=1;i<16;i++)
                    {
                        int f=(i-1)/3,j=(i-1)%3,parent=j==0?0:i-1;
                        Quaternion Desired(int index)
                        {
                            if(index==0)return hand.Target.rotation;
                            int finger=(index-1)/3,joint=(index-1)%3;
                            var d=hand.Target.skeleton.Joint(finger*4+joint+1)-hand.Target.skeleton.Joint(finger*4+joint);
                            return CandyPhysicalHand.SegmentRotation(hand.Target.rotation*d,hand.Target.rotation*Vector3.up);
                        }
                        float angle=Quaternion.Angle(Quaternion.Inverse(hand.bodies[parent].rotation)*hand.bodies[i].rotation,Quaternion.Inverse(Desired(parent))*Desired(i));
                        worstAngle=Mathf.Max(worstAngle,angle);
                        var joint=hand.bodies[i].GetComponent<ConfigurableJoint>();
                        worstAnchor=Mathf.Max(worstAnchor,Vector3.Distance(joint.transform.TransformPoint(joint.anchor),joint.connectedBody.transform.TransformPoint(joint.connectedAnchor)));
                    }
                }
                if(bot.CompletedPushes>0&&sawRecovery)break;
                yield return null;
            }
            File.AppendAllText(Report,"BOT measured worst finger relative error "+worstAngle.ToString("F2")+" degrees; joint separation "+worstAnchor.ToString("F4")+" m; block displacement "+body.position.z.ToString("F3")+" m\n");
            Check(sawPreparation&&sawPush&&sawRecovery,"AI prepares a real contact attack, follows through, and withdraws");
            Check(bot.CompletedPushes>0&&body.position.z>.25f,"Physical hand contact moves the block; no block pose or velocity commands");
            Check(worstAngle<50,"AI finger articulation stays within 50 degrees of commanded joint pose during contact");
            Check(worstAnchor<.08f,"AI finger joint anchors stay connected during push");
            Check(bot.Bridge.PhysicalHand.JointDrivenFingers,"AI uses joint-driven finger physics");
            File.AppendAllText(Report,"AI joint X limit "+bot.Bridge.PhysicalHand.bodies[1].GetComponent<ConfigurableJoint>().highAngularXLimit.limit+"; spring "+bot.Bridge.PhysicalHand.bodies[1].GetComponent<ConfigurableJoint>().slerpDrive.positionSpring+"\n");
            int successes=bot.CompletedPushes,attempts=bot.Attacks;
            body.isKinematic=true;Vector3 fixedPosition=body.position;
            actor.Place(fixedPosition+new Vector3(0,-.4f,-5),Quaternion.identity);bot.SetTarget(body);
            end=Time.time+20;bool withdrew=false;
            while(Time.time<end)
            {
                if(bot.Attacks>attempts&&bot.Withdrawing){withdrew=true;break;}
                yield return null;
            }
            Check(withdrew&&bot.CompletedPushes==successes,"AI abandons an immovable block without claiming a successful push");
            Check(Vector3.Distance(body.position,fixedPosition)<.001f,"AI never moves an immovable block by changing its transform");
            CaptureBot(actor,block,"bot-blocked.png");
            Object.Destroy(block);yield return Wait(.3f);
            Check(!bot.Target&&bot.ReadFlightInput(actor.motor,Time.deltaTime).moveWorld.sqrMagnitude<.0001f,"AI clears a destroyed target without stale attack commands");
            Object.Destroy(actor.gameObject);Object.Destroy(block);Object.Destroy(personality);
            yield return Wait(.2f);
        }
        static void CaptureBot(CastleHandActor actor,GameObject block,string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
            {File.AppendAllText(Report,"SKIP screenshot: headless graphics device ("+name+")\n");return;}
            var cameraObject=new GameObject("Bot evidence camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            camera.transform.position=new Vector3(7,24,-9);camera.transform.LookAt(new Vector3(0,20,-1));
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.14f,.18f,.23f);camera.fieldOfView=45;
            var lightObject=new GameObject("Bot evidence light");var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=.8f;light.transform.rotation=Quaternion.Euler(45,-30,0);
            var rt=new RenderTexture(1000,800,24);var old=RenderTexture.active;Texture2D pixels=null;
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels=new Texture2D(1000,800,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,1000,800),0,0);pixels.Apply();File.WriteAllBytes(ProjectPath.Report(name),pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=old;camera.targetTexture=null;Object.Destroy(rt);if(pixels)Object.Destroy(pixels);Object.Destroy(cameraObject);Object.Destroy(lightObject);}
        }
        static void CaptureCastle(CastleHandBotPopulation population)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var world=population.Session.world;
            var go=new GameObject("Castle attack evidence camera");var camera=go.AddComponent<Camera>();camera.enabled=false;
            camera.transform.position=world.Center+new Vector3(world.Radius*1.8f,world.TopHeight*.65f,-world.Radius*2);
            camera.transform.LookAt(world.Center+Vector3.up*(world.TopHeight-world.Center.y)*.45f);camera.fieldOfView=50;
            var rt=new RenderTexture(1400,1000,24);var old=RenderTexture.active;Texture2D pixels=null;
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels=new Texture2D(1400,1000,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,1400,1000),0,0);pixels.Apply();File.WriteAllBytes(ProjectPath.Report("castle-hand-attacks.png"),pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=old;camera.targetTexture=null;Object.Destroy(rt);if(pixels)Object.Destroy(pixels);Object.Destroy(go);}
        }
        static void CheckGestures()
        {
            var gesture=new CandyHandFlightGesture();
            var hand=new Poses().pose;
            var open=hand.skeleton;
            var pinch=open;pinch.SetJoint(3,pinch.Joint(7)+Vector3.right*.015f);
            // Preserve plausible thumb segments while forming the pinch.
            pinch.SetJoint(2,pinch.Joint(3)+Vector3.back*.025f);
            pinch.SetJoint(1,pinch.Joint(2)+Vector3.back*.025f);
            hand.skeleton=pinch;
            gesture.Read(hand,Quaternion.identity,false);hand.position+=Vector3.forward*.15f;
            Check(gesture.Read(hand,Quaternion.identity,false).moveWorld==Vector3.zero,"Starting with a held pinch does not unexpectedly fly");
            hand.skeleton=open;gesture.Read(hand,Quaternion.identity,false);
            hand.skeleton=pinch;gesture.Read(hand,Quaternion.identity,false);
            hand.position+=Vector3.forward*.15f;
            Check(gesture.Read(hand,Quaternion.identity,false).moveWorld.z>.99f,"Fresh pinch and forward displacement command forward flight");
            Check(gesture.Read(hand,Quaternion.Euler(0,90,0),false).moveWorld.x>.99f,"Gesture flight follows observer heading");
            hand.rotation=Quaternion.Euler(0,45,0);
            Check(gesture.Read(hand,Quaternion.identity,false).yaw>.99f,"Steering-wrist turn commands flight yaw");
            Check(gesture.Read(hand,Quaternion.identity,true).moveWorld==Vector3.zero,"Menu suspension stops gesture thrust");
            Check(gesture.Read(hand,Quaternion.identity,false).moveWorld==Vector3.zero,"Closing menu requires release and fresh pinch");
            hand.skeleton=open;gesture.Read(hand,Quaternion.identity,false);
            hand.skeleton=pinch;gesture.Read(hand,Quaternion.identity,false);
            hand.position+=Vector3.up*.15f;
            Check(gesture.Read(hand,Quaternion.identity,false).moveWorld.y>.99f,"Upward steering displacement commands ascent");
            hand.tracked=false;
            Check(gesture.Read(hand,Quaternion.identity,false).moveWorld==Vector3.zero,"Tracking loss stops gesture thrust");
            hand.tracked=true;
            Check(gesture.Read(hand,Quaternion.identity,false).moveWorld==Vector3.zero,"Reacquisition with held pinch does not restart flight");
        }
        static IEnumerator CheckMagneticMap()
        {
            yield return Wait(5);
            var population=Object.FindFirstObjectByType<CastleHandBotPopulation>();
            Check(population&&population.Session.world!=null,"Actual Evan Demo2 initializes its own demo and world query");
            var world=population.Session.world;var blocks=world.Blocks.Where(b=>b).ToArray();
            var positions=blocks.Select(b=>b.position).ToArray();var pinned=blocks.Select(b=>b.isKinematic).ToArray();
            int initialPinned=pinned.Count(value=>value);
            Check(blocks.Length>100&&initialPinned>0,"Actual Evan Demo2 exposes supported magnetic blocks through generic world query");
            population.ClearHands();yield return Wait(.2f);
            for(int i=0;i<4;i++)population.SpawnHand();
            float until=Time.time+60,started=Time.time;
            while(Time.time<until)yield return null;
            int moved=0,released=0;
            for(int i=0;i<blocks.Length;i++)if(blocks[i])
            {
                if(Vector3.Distance(positions[i],blocks[i].position)>.5f)moved++;
                if(pinned[i]&&!blocks[i].isKinematic)released++;
            }
            int attempts=population.Hands.Sum(h=>h.Attacks),pushes=population.Hands.Sum(h=>h.CompletedPushes);
            File.AppendAllText(Report,"EVAN2 measured "+(Time.time-started).ToString("F1")+"s, blocks="+blocks.Length+" initially pinned="+initialPinned+" attempts="+attempts+" pushes="+pushes+" released="+released+" moved>.5m="+moved+"\n");
            foreach(var hand in population.Hands)File.AppendAllText(Report,hand.name+" "+hand.Intent+" style="+hand.AttackStyleName+" attempts="+hand.Attacks+" pushes="+hand.CompletedPushes+"\n");
            Check(attempts>=2&&pushes>=1,"Ordinary AI hands reach physical contact attacks on actual noncastle magnetic map");
            Check(released>=1&&moved>=2,"Actual contact breaks magnetic support and displaces noncastle map pieces");
            Check(!SessionState.GetBool(Key+"Failed",false),"Evan Demo2 hand attacks run without runtime exceptions");
        }
        static IEnumerator CheckGame()
        {
            yield return Wait(5);
            Check(!SessionState.GetBool(Key + "Failed", false), "Game.unity enters Play Mode and runs for five seconds without errors");
            var population=Object.FindFirstObjectByType<CastleHandBotPopulation>();
            Check(population&&population.Session!=null,"Game initializes real hand-bot population");
            population.Session.Bots.targetPopulation=Mathf.Max(2,population.Session.Bots.targetPopulation);
            var bot=population.SpawnHand();
            Check(bot,"Actual hand bot spawns using shared prefab");
            Vector3 start=bot.Actor.motor.transform.position;
            yield return Wait(3);
            Check(bot.Bridge.PhysicalHand&&bot.Bridge.PhysicalHand.IsInteractive,"Real hand bot activates its physical hand");
            Check(Vector3.Distance(start,bot.Actor.motor.transform.position)>.1f,"Real hand bot continues flying under external input");
            float timeout=Time.time+35;
            while(bot.Attacks==0&&Time.time<timeout)yield return null;
            File.AppendAllText(Report,"GAME BOT intent: "+bot.Intent+"; attempts: "+bot.Attacks+"; successful pushes: "+bot.CompletedPushes+"\n");
            Check(bot.Attacks>0,"Real castle bot selects an exposed block and reaches a deliberate push attempt");
            Check(!SessionState.GetBool(Key + "Failed", false),"Game and hand bot run without runtime errors");
            var blocks=population.Session.world.Blocks.Where(b=>b&&!b.isKinematic).ToArray();
            var positions=blocks.Select(b=>b.position).ToArray();
            for(int i=0;i<9;i++)population.SpawnHand();
            int displaced=0,successes=0;float maxSeparation=0;
            timeout=Time.time+90;float started=Time.time;
            while(Time.time<timeout)
            {
                displaced=0;for(int i=0;i<blocks.Length;i++)if(blocks[i]&&Vector3.Distance(positions[i],blocks[i].position)>1)displaced++;
                successes=population.Hands.Sum(h=>h.CompletedPushes);
                foreach(var h in population.Hands)
                {
                    var hand=h.Bridge.PhysicalHand;if(!hand||!hand.IsInteractive)continue;
                    foreach(var rb in hand.bodies)if(rb)maxSeparation=Mathf.Max(maxSeparation,Vector3.Distance(hand.bodies[0].position,rb.position));
                }
                // Run the whole interval to catch repeated approach stalls and hand crowding.
                yield return null;
            }
            CaptureCastle(population);
            File.AppendAllText(Report,"CASTLE measured over "+(Time.time-started).ToString("F1")+" seconds: "+population.Hands.Sum(h=>h.Attacks)+" attacks; "+successes+" successful contact pushes; "+displaced+" blocks displaced over 1m; finger reach "+maxSeparation.ToString("F2")+"m\n");
            foreach(var h in population.Hands)File.AppendAllText(Report,h.name+": "+h.Intent+" attempts="+h.Attacks+" successes="+h.CompletedPushes+"\n");
            Check(successes>=10,"Multiple hands actually displace loaded castle blocks through contact");
            Check(displaced>=40,"Contact attacks destabilize multiple castle blocks, not only an isolated floating fixture");
            Check(population.Hands.Count(h=>h.Attacks>0)>=6,"Most hands reach actual attacks during the sustained castle test");
            Check(population.Hands.Select(h=>h.Target).Where(t=>t).Distinct().Count()>=2,"Hands distribute their attacks across different blocks");
            var first=population.Hands[0].Bridge.PhysicalHand;var second=population.Hands[1].Bridge.PhysicalHand;
            Check(first&&second&&Physics.GetIgnoreCollision(first.bodies[0].GetComponent<Collider>(),second.bodies[0].GetComponent<Collider>()),"AI hand pairs cannot batter each other with physical colliders");
            Check(maxSeparation<population.Hands[0].Bridge.settings.size*.35f,"Fingers remain connected during real castle attacks");
        }
    }
}

