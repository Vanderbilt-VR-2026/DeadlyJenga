using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CandyPhysicsHands;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;
using FlyingHand;

namespace DeadlyJenga.CastlePrototype.Editor
{
    public static class DesktopPoseValidation
    {
        [MenuItem("Tools/Characters/Validate desktop hand poses")]
        public static void Run()
        {
            int failures=0,count=0;var report=new StringBuilder();
            foreach(bool left in new[]{false,true})
            foreach(DesktopHandPose pose in Enum.GetValues(typeof(DesktopHandPose)))
            foreach(float clench in new[]{0f,.5f,1f})
            {
                var rest=CandyHandSkeleton.Rest(left);var value=DesktopHandPoses.Build(left,pose,clench);
                bool valid=true;
                for(int finger=0;finger<5;finger++)for(int joint=0;joint<3;joint++)
                {
                    int j=finger*4+joint;
                    float expected=Vector3.Distance(rest.Joint(j),rest.Joint(j+1));
                    float actual=Vector3.Distance(value.Joint(j),value.Joint(j+1));
                    valid&=Mathf.Abs(expected-actual)<.002f;
                }
                count++;if(!valid)failures++;
                report.AppendLine((valid?"PASS ":"FAIL ")+left+" "+pose+" clench "+clench);
            }
            var previousWorld=Services.World;
            var fixture=new GameObject("Temporary hand boundary check");fixture.SetActive(false);
            try
            {
                var world=fixture.AddComponent<FakeWorldQuery>();world.waterHeight=5;Services.World=world;
                var motorObject=new GameObject("Motor");motorObject.SetActive(false);motorObject.transform.SetParent(fixture.transform);
                var motor=motorObject.AddComponent<FlyingHandController>();motor.enabled=false;
                fixture.SetActive(true);motorObject.SetActive(true);motor.ConfigureBody();motor.Body.position=Vector3.zero;motor.Body.linearVelocity=Vector3.down;Physics.SyncTransforms();
                motor.ClampWaterBoundary();
                bool wrist=Mathf.Abs(motor.Body.position.y-(5-motor.wristSubmersion))<.001f&&motor.Body.linearVelocity.y>=0;
                count++;if(!wrist)failures++;report.AppendLine((wrist?"PASS ":"FAIL ")+"Wrist can partly submerge but stops descending at boundary y="+motor.Body.position.y+" velocity="+motor.Body.linearVelocity);
                var rig=new GameObject("Observer");rig.SetActive(false);rig.transform.SetParent(fixture.transform);
                var camera=new GameObject("Camera").AddComponent<Camera>();camera.transform.SetParent(rig.transform);camera.transform.localPosition=Vector3.down;
                var follow=rig.AddComponent<ThirdPersonVRFollow>();follow.viewCamera=camera;follow.ClampCameraWater();
                bool above=camera.transform.position.y>=5.079f;
                count++;if(!above)failures++;report.AppendLine((above?"PASS ":"FAIL ")+"Actual camera stays above water including local headset offset");
            }
            finally{UnityEngine.Object.DestroyImmediate(fixture);Services.World=previousWorld;}
            report.AppendLine(count+" pose checks, "+failures+" failures. Not verified: headset, contact grip, visual finger overlap.");
            Directory.CreateDirectory("Logs/Validation");File.WriteAllText("Logs/Validation/DesktopHandPoses.txt",report.ToString());
            if(failures>0)throw new Exception("Desktop pose validation failed: "+failures);
        }
    }
}
