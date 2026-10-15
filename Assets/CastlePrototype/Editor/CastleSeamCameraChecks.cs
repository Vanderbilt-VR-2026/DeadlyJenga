using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using FlyingHand;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CandyPhysicsHands;
using Object=UnityEngine.Object;

namespace DeadlyJenga.CastlePrototype.Editor
{
    public static class CastleSeamCameraChecks
    {
        public static IEnumerator Check()
        {
            yield return null;
            var lines=new List<string>();var session=Object.FindFirstObjectByType<CastlePlaySession>();session.SetMenu(true);
            void Assert(bool ok,string text){lines.Add((ok?"PASS ":"FAIL ")+text);File.WriteAllLines("ArtDirection/SeamCameraChecks.txt",lines);}
            var staging=new GameObject("Seam validation");staging.SetActive(false);
            var motor=Object.Instantiate(session.survivorPrefab,staging.transform).GetComponent<CastleSurvivor>();motor.externallyDriven=true;motor.enabled=false;
            var avatar=motor.GetComponent<CandyPlayerAvatar>();if(avatar)avatar.remoteDriven=true;
            foreach(var c in motor.GetComponentsInChildren<Camera>(true))c.enabled=false;foreach(var a in motor.GetComponentsInChildren<AudioListener>(true))a.enabled=false;
            staging.SetActive(true);motor.RefreshMode();const float dt=1f/60;
            var blocks=new List<CastleBlock>();
            CastleBlock Block(Vector3 p,Quaternion r,Vector3 size)
            {
                var b=Object.Instantiate(session.generator.blockPrefab).GetComponent<CastleBlock>();b.GetComponent<Rigidbody>().isKinematic=true;b.transform.SetPositionAndRotation(p,r);b.transform.localScale=size;b.SyncSurface();blocks.Add(b);return b;
            }
            void Clear(){foreach(var b in blocks)Object.DestroyImmediate(b.gameObject);blocks.Clear();}
            void Tick(Vector2 input,int n){for(int i=0;i<n;i++)motor.Simulate(input,false,dt,true);}
            foreach(float gap in new[]{.08f,.15f,1f})
            {
                Block(new Vector3(120,45,2),Quaternion.identity,new Vector3(6,10,2));Block(new Vector3(126+gap,45,2),Quaternion.identity,new Vector3(6,10,2));
                motor.Teleport(new Vector3(122.7f,42,.82f),Quaternion.identity);Tick(Vector2.zero,15);Tick(Vector2.right,35);
                Assert(gap<.2f?motor.Climbing&&motor.transform.position.x>123+gap:!motor.Climbing||motor.transform.position.x<123.2f,"Horizontal seam "+gap+"m "+(gap<.2f?"crosses":"cannot cross with cling alone")+" (position "+motor.transform.position+", normal "+motor.ClimbNormal+").");Clear();
            }
            // Actual layout coordinates, dimensions and alternating 45-degree courses.
            var settings=session.generator.settings;var layout=CastleLayout.Build(settings);var origin=new Vector3(120,40,0);
            foreach(var p in layout)if(p.Section==CastleSection.Keep&&p.Course<3)Block(origin+p.Position,p.Rotation,settings.blockSize);
            float radius=(settings.blockSize.x-settings.blockSize.z*.4f)*.5f/Mathf.Tan(Mathf.PI/8);
            for(int side=0;side<4;side++)
            {
                var rotation=Quaternion.Euler(0,side*90,0);
                motor.Teleport(origin+rotation*new Vector3(2.8f,.8f,radius+settings.blockSize.z*.5f+.16f),rotation*Quaternion.LookRotation(Vector3.back));
                Tick(Vector2.zero,15);float start=motor.transform.position.y;Tick(Vector2.up,48);
                Assert(motor.Climbing&&motor.transform.position.y>start+.8f,"Actual purple keep intersection "+side+" crosses cardinal/diagonal course join.");
            }
            Clear();Object.Destroy(staging);
            // A rig with synthetic head poses isolates observer following from input and physics.
            var rig=new GameObject("Observer test");rig.SetActive(false);var offset=new GameObject("XR camera floor offset");offset.transform.SetParent(rig.transform,false);offset.transform.localPosition=Vector3.up*1.6f;
            var head=new GameObject("Tracked camera").AddComponent<Camera>();head.transform.SetParent(offset.transform,false);head.enabled=false;
            head.transform.localPosition=new Vector3(.1f,.05f,.08f);head.transform.localRotation=Quaternion.Euler(7,12,0);
            var heading=new GameObject("Flight heading");var palm=new GameObject("Visible physical palm");palm.transform.position=new Vector3(130,45,0);
            var follow=rig.AddComponent<ThirdPersonVRFollow>();follow.target=heading.transform;follow.positionTarget=palm.transform;follow.viewCamera=head;follow.compactHandView=true;rig.SetActive(true);follow.enabled=false;
            follow.ApplyViewMode(ControlMode.Desktop);follow.SnapToTarget();
            Assert(Vector3.Distance(head.transform.position,palm.transform.position+new Vector3(0,follow.handViewHeight,-follow.handViewDistance))<.01f,"Desktop eye framing compensates the XR camera-floor parent.");
            heading.transform.position=new Vector3(500,900,200);for(int i=0;i<60;i++){palm.transform.position+=Vector3.right*.1f;follow.StepFollow(dt);}
            Assert(Vector3.Distance(head.transform.position,palm.transform.position)<5.5f,"Observer remains near the visible hand even when the flight target is far away.");
            palm.transform.rotation=Quaternion.Euler(80,160,45);follow.StepFollow(dt);Assert(Mathf.Abs(Mathf.DeltaAngle(rig.transform.eulerAngles.y,0))<.01f,"Wrist rotation does not steer observer heading.");
            follow.ApplyViewMode(ControlMode.VR);follow.StepFollow(dt);var localPosition=head.transform.localPosition;var localRotation=head.transform.localRotation;
            head.transform.localPosition+=new Vector3(.12f,.07f,0);var lean=head.transform.localPosition;
            follow.StepFollow(dt);Assert(head.transform.localPosition==lean&&Quaternion.Angle(head.transform.localRotation,localRotation)<.001f,"VR head pose and natural leaning remain untouched by following.");
            heading.transform.rotation=Quaternion.Euler(0,12,0);follow.StepFollow(dt);Assert(Mathf.Abs(Mathf.DeltaAngle(rig.transform.eulerAngles.y,0))<.01f,"Small flight yaw does not smoothly rotate the VR view.");
            heading.transform.rotation=Quaternion.Euler(0,35,0);follow.StepFollow(dt);Assert(Mathf.Abs(Mathf.DeltaAngle(rig.transform.eulerAngles.y,30))<.01f,"VR observer heading advances in predictable snap steps.");
            Assert(Vector3.Angle(rig.transform.up,Vector3.up)<.001f,"Observer horizon stays upright.");
            Object.Destroy(rig);Object.Destroy(heading);Object.Destroy(palm);
            // An unfocused Scene view unlocks the cursor and the session opens its menu,
            // deliberately suspending hand construction. Isolate this wiring check from
            // desktop focus/menu policy rather than interpreting that as broken wiring.
            session.SetRole(true);session.SetMenu(false);bool sessionEnabled=session.enabled;session.enabled=false;
            for(int frame=0;frame<35;frame++){session.Hand.motor.InputSuspended=false;yield return null;}
            var bridge=session.Hand.GetComponent<CandyFlyingHandBridge>();
            Assert(bridge.PhysicalHand&&session.Hand.follow.positionTarget==bridge.PhysicalHand.visualBones[0]&&session.Hand.follow.compactHandView,"Real demo wires observer position to current physical palm and compact framing.");
            session.enabled=sessionEnabled;session.SetMenu(true);Debug.Log(string.Join("\n",lines));yield return CastleClimbChecks.Check();
        }
    }
}
