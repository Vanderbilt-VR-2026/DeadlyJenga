using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;
using FlyingHand;
using Object=UnityEngine.Object;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class SurvivorControlValidation
    {
        const string Key="SurvivorControlValidation";
        static double started;
        static bool errors;
        static readonly StringBuilder report=new StringBuilder();
        static SurvivorControlValidation()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {started=EditorApplication.timeSinceStartup;Application.logMessageReceived+=Log;EditorApplication.update+=Wait;}
                if(state==PlayModeStateChange.EnteredEditMode)
                {File.AppendAllText(ProjectPath.Report("SurvivorControls.txt"),"FAIL: Play Mode was interrupted before completion.\n");SessionState.SetBool(Key,false);if(Application.isBatchMode)EditorApplication.Exit(1);}
            };
        }
        public static void RunBatch()
        {
            File.WriteAllText(ProjectPath.Report("SurvivorControls.txt"),"Tower player controls: awaiting Play Mode\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors=true;report.AppendLine("ERROR "+message);}}
        static void Check(bool ok,string text){if(!ok)throw new Exception(text);report.AppendLine("PASS "+text);}
        static void Wait()
        {
            if(EditorApplication.timeSinceStartup-started<5)return;
            EditorApplication.update-=Wait;
            var fixture=new GameObject("Temporary survivor control checks");bool passed=false;
            try
            {
                Check(!errors,"Game.unity runs five seconds without errors");
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("CastleSurvivor.prefab"));
                var actor=Object.Instantiate(prefab,fixture.transform);
                var motor=actor.GetComponent<CastleSurvivor>();motor.enabled=false;motor.controlMode=ControlMode.VR;motor.RefreshMode();
                motor.SetVRPlayStyle(SurvivorVRPlayStyle.Seated,false);
                var eye=motor.viewCamera.transform;eye.localPosition=new Vector3(.2f,1.1f,.15f);eye.localRotation=Quaternion.Euler(12,17,4);
                motor.Teleport(new Vector3(1000,1000,1000),Quaternion.identity);
                var local=eye.localRotation;var position=eye.position;
                for(int i=0;i<60;i++)motor.ApplyVRTurn(1,1f/60);
                Check(Mathf.Abs(Mathf.DeltaAngle(0,motor.transform.eulerAngles.y)-60)<.02f,"Seated turn is continuous at 60 degrees per second");
                Check(Vector3.Distance(position,eye.position)<.003f&&Quaternion.Angle(local,eye.localRotation)<.01f,"Turning pivots about eyes and preserves tracked pitch, yaw and roll");
                var rotation=motor.transform.rotation;motor.ApplyVRTurn(.1f,1);Check(Quaternion.Angle(rotation,motor.transform.rotation)<.01f,"Turn deadzone prevents idle drift");
                motor.SetVRPlayStyle(SurvivorVRPlayStyle.Standing,false);motor.ApplyVRTurn(1,1);
                Check(Quaternion.Angle(rotation,motor.transform.rotation)<.01f,"Standing ignores right-stick turning");
                Check(motor.FilterVRMove(new Vector2(.1f,0))==Vector2.zero&&motor.FilterVRMove(Vector2.up).y>.99f,"Move stick has a continuous deadzone and full travel");
                var open=new CandyHandPose{tracked=true,skeletonTracked=true,skeleton=CandyHandSkeleton.Rest(false)};
                var pinch=open;pinch.skeleton.SetJoint(3,pinch.skeleton.Joint(7)+Vector3.right*.015f);
                motor.ReadVRJump(pinch,false,out var press,out var held);Check(!press&&!held,"Held gesture at entry cannot auto-jump");
                motor.ReadVRJump(open,false,out _,out _);motor.ReadVRJump(pinch,false,out press,out held);
                Check(press&&held,"Fresh right pinch jumps and holds for climbing");
                pinch.skeleton.SetJoint(3,pinch.skeleton.Joint(7)+Vector3.right*.027f);
                motor.ReadVRJump(pinch,false,out press,out held);Check(!press&&held,"Pinch hysteresis prevents chatter and repeated jumps");
                motor.InputSuspended=true;motor.ReadVRJump(pinch,false,out press,out held);motor.InputSuspended=false;
                motor.ReadVRJump(pinch,false,out press,out held);Check(!press&&!held,"Menu closing requires a released gesture before another jump");
                pinch.skeleton.SetJoint(3,pinch.skeleton.Joint(7)+Vector3.right*.015f);
                motor.ReadVRJump(default,false,out _,out _);motor.ReadVRJump(pinch,false,out press,out held);Check(!press&&!held,"Tracking reacquisition does not auto-jump");
                motor.jumpGesture=SurvivorJumpGesture.Fist;motor.ResetVRJump();motor.ReadVRJump(open,false,out _,out _);
                var fist=open;
                for(int f=1;f<5;f++){var root=fist.skeleton.Joint(f*4);fist.skeleton.SetJoint(f*4+1,root+Vector3.forward*.025f);fist.skeleton.SetJoint(f*4+2,root+new Vector3(0,-.025f,.025f));fist.skeleton.SetJoint(f*4+3,root+Vector3.down*.02f);}
                motor.ReadVRJump(fist,false,out press,out held);Check(press&&held,"Fist is a selectable alternative to pinch");
                motor.SetVRPlayStyle(SurvivorVRPlayStyle.Seated,false);motor.ReadVRJump(default,false,out _,out _);motor.ReadVRJump(default,true,out press,out held);
                Check(press&&held,"Seated button jumps without requiring hand tracking");
                motor.ReadVRJump(default,true,out press,out held);Check(!press&&held,"Held jump button climbs without repeated jump presses");
                motor.CalibrateSeatedHeight();Check(Mathf.Abs(motor.transform.InverseTransformPoint(eye.position).y-motor.eyeHeight*motor.playerScale)<.002f,"Seated calibration restores avatar eye height");
                motor.SetVRPlayStyle(SurvivorVRPlayStyle.Standing,false);motor.SetVRPlayStyle(SurvivorVRPlayStyle.Seated,false);
                eye.localPosition=Vector3.zero;eye.localRotation=Quaternion.identity;motor.Teleport(new Vector3(1000,1001.05f,1000.45f),Quaternion.identity);
                GameObject Box(Vector3 position,Vector3 size)
                {var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.transform.SetParent(fixture.transform);box.transform.position=position;box.transform.localScale=size;return box;}
                var ledge=Box(new Vector3(1000,1001,1002),new Vector3(6,2,2));Physics.SyncTransforms();
                Check(motor.TryStartVault(),"VR vault starts only at a real clear ledge");
                position=motor.transform.position;rotation=motor.transform.rotation;motor.ApplyVRTurn(1,1);
                Check(Quaternion.Angle(rotation,motor.transform.rotation)<.01f,"Artificial turning cannot displace an active vault");
                float worstStep=0;
                for(int i=0;i<60;i++){var before=motor.transform.position;motor.Simulate(Vector2.zero,false,.02f);worstStep=Mathf.Max(worstStep,Vector3.Distance(before,motor.transform.position));}
                Check(!motor.Vaulting&&motor.transform.position.y>1002&&motor.Grounded,"Vault settles on the ledge with grounded state restored");
                Check(worstStep<.15f&&Quaternion.Angle(eye.localRotation,Quaternion.identity)<.01f,"Vault movement stays bounded and never animates head rotation");
                ledge.SetActive(false);
                var wall=Box(new Vector3(1000,1005,1002),new Vector3(6,10,2));wall.layer=Layers.Surface;wall.AddComponent<SurvivorControlTestSurface>();Physics.SyncTransforms();
                motor.Teleport(new Vector3(1000,1002,1000.82f),Quaternion.identity);
                for(int i=0;i<12;i++)motor.Simulate(Vector2.zero,false,.02f,true);
                Check(motor.Climbing,"Holding jump catches a real climbable face");
                position=motor.transform.position;motor.Simulate(Vector2.up,false,.02f,true);
                Check(motor.transform.position.y-position.y<.012f,"VR climb starts with bounded acceleration");
                for(int i=0;i<20;i++)motor.Simulate(Vector2.up,false,.02f,true);
                Check(motor.Climbing&&motor.transform.position.y>position.y+.4f,"VR climb reaches useful upward motion");
                motor.Simulate(Vector2.zero,false,.02f,false);Check(!motor.Climbing,"Release exits climb without locking the camera");
                motor.externallyDriven=true;motor.RefreshMode();motor.Teleport(new Vector3(1010,1010,1010),Quaternion.identity);
                position=motor.transform.position;motor.Simulate(Vector2.up,false,.1f);
                Check(Mathf.Abs((motor.transform.position-position).z-motor.walkSpeed*.1f)<.003f,"Externally driven survivor keeps its original movement speed");
                Check(!errors,"No runtime errors during control and traversal checks");passed=true;
            }
            catch(Exception e){report.AppendLine("FAIL "+e);}
            finally
            {
                Object.DestroyImmediate(fixture);Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
                report.AppendLine(passed?"COMPLETE: Quest tracking and comfort still require device testing.":"FAILED");
                File.WriteAllText(ProjectPath.Report("SurvivorControls.txt"),report.ToString());
                if(Application.isBatchMode)EditorApplication.Exit(passed?0:1);else EditorApplication.ExitPlaymode();
            }
        }
    }
    public sealed class SurvivorControlTestSurface:MonoBehaviour,IClimbable
    {
        public bool CanClimb=>true;
        public float Grip=>1;
        public Transform Anchor=>transform;
        public Rigidbody Body=>null;
        public Transform Visual=>null;
    }
}
