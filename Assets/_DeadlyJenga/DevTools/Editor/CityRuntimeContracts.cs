using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using FlyingHand;
using UnityEngine;
using Object=UnityEngine.Object;
namespace DeadlyJenga.DevTools.Editor
{
    public static class CityRuntimeContracts
    {
        public static IEnumerator Run(SparseStructureMap map,CastleSurvivor motor,StringBuilder report,Action<bool> outcome)
        {
            var fixture=new GameObject("City collision acceptance fixtures");var oldMode=motor.controlMode;bool external=motor.externallyDriven;
            void Check(bool pass,string text){report.AppendLine((pass?"PASS ":"FAIL ")+"CITY CONTRACT "+text);outcome(pass);}
            GameObject Floor(Vector3 p,Vector3 size){var f=new GameObject("Acceptance static floor");f.transform.SetParent(fixture.transform);f.transform.position=p;f.AddComponent<BoxCollider>().size=size;return f;}
            Rigidbody Piece(Vector3 p,Vector3 size,bool kinematic=false)
            {
                var go=Object.Instantiate(map.blockPrefab,fixture.transform);go.transform.position=p;go.transform.localScale=size;
                var b=go.GetComponent<Rigidbody>();b.position=p;b.rotation=Quaternion.identity;b.useGravity=false;b.isKinematic=kinematic;
                b.GetComponent<CastleBlock>().UseOriginalTraversal();return b;
            }
            try
            {
                Time.timeScale=1;motor.externallyDriven=true;motor.RefreshMode();motor.InputSuspended=false;
                Check(map.UsesOriginalColliders&&TraversalCollision.Unified,"world explicitly selects original collision geometry");
                Check(map.GetComponentsInChildren<CityStructuralMember>().All(c=>c.SurvivorShapes.Count==0),"new sections allocate no survivor collision copies");
                Check(!Physics.GetIgnoreLayerCollision(Layers.Survivor,Layers.Block),"original structural layer collides with survivors");
                Floor(new Vector3(1000,999.5f,1000),new Vector3(30,1,30));
                var wall=Piece(new Vector3(1000,1001,1003),new Vector3(5,2,1));wall.constraints=RigidbodyConstraints.FreezeRotation|RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezePositionY;
                motor.Teleport(new Vector3(1000,1000.03f,1001.8f),Quaternion.identity);motor.viewCamera.transform.localRotation=Quaternion.identity;
                var original=wall.position;
                for(int i=0;i<50;i++){motor.Simulate(Vector2.up,false,.02f);yield return null;}
                Check(Vector3.Distance(wall.position,original)<.02f,"walking into a free dynamic wall does not push it");
                Check(motor.transform.position.z<1002.6f&&motor.Grounded,"dynamic wall blocks walking while feet remain supported");
                motor.Teleport(new Vector3(1000,1000.1f,1003),Quaternion.identity);yield return null;motor.Simulate(Vector2.zero,false,.02f);
                var probe=fixture.AddComponent<CapsuleCollider>();probe.enabled=false;probe.center=motor.Capsule.center;probe.radius=motor.Capsule.radius*.98f;probe.height=motor.Capsule.height;
                bool penetration=Physics.ComputePenetration(probe,motor.transform.position,motor.transform.rotation,wall.GetComponent<Collider>(),wall.position,wall.rotation,out _,out float depth);
                Check(!penetration||depth<.03f,"a capsule initially inside a dynamic wall recovers out of the overlap");
                wall.gameObject.SetActive(false);
                var platform=Piece(new Vector3(1010,1000.5f,1000),new Vector3(5,1,5),true);motor.Teleport(new Vector3(1010,1001.02f,1000),Quaternion.identity);
                for(int i=0;i<8;i++){motor.Simulate(Vector2.zero,false,.02f);yield return null;}
                var start=motor.transform.position;
                for(int i=0;i<40;i++){platform.position+=Vector3.right*.025f;platform.rotation=Quaternion.Euler(0,(i+1)*.3f,0);platform.transform.SetPositionAndRotation(platform.position,platform.rotation);motor.Simulate(Vector2.zero,false,.02f);yield return null;}
                Check(motor.transform.position.x-start.x>.7f&&Mathf.Abs(motor.transform.position.y-1001)<.12f&&motor.Grounded,"standing follows a translating and rotating original-collider platform");
                platform.gameObject.SetActive(false);
                motor.externallyDriven=false;motor.controlMode=ControlMode.VR;motor.RefreshMode();motor.viewCamera.transform.localPosition=Vector3.zero;motor.viewCamera.transform.localRotation=Quaternion.identity;motor.enabled=true;
                var ledge=Piece(new Vector3(1000,1001,1002),new Vector3(6,2,2),true);motor.Teleport(new Vector3(1000,1001.05f,1000.45f),Quaternion.identity);Physics.SyncTransforms();
                bool vault=motor.TryStartVault();float largest=0;
                for(int i=0;i<60;i++){var before=motor.transform.position;motor.Simulate(Vector2.zero,false,.02f);largest=Mathf.Max(largest,Vector3.Distance(before,motor.transform.position));}
                Check(vault&&!motor.Vaulting&&motor.transform.position.y>1002&&largest<.2f,"VR motor vaults a real structural-layer ledge with bounded motion");ledge.gameObject.SetActive(false);
                var climb=Piece(new Vector3(1000,1005,1002),new Vector3(6,10,2),true);motor.Teleport(new Vector3(1000,1002,1000.82f),Quaternion.identity);Physics.SyncTransforms();
                for(int i=0;i<12;i++)motor.Simulate(Vector2.zero,false,.02f,true);bool attached=motor.Climbing;float height=motor.transform.position.y;
                for(int i=0;i<30;i++)motor.Simulate(Vector2.up,false,.02f,true);
                Check(attached&&motor.Climbing&&motor.transform.position.y>height+.4f,"VR motor catches and ascends an original structural collider");motor.Simulate(Vector2.zero,false,.02f,false);
                Check(!motor.Climbing&&Quaternion.Angle(motor.viewCamera.transform.localRotation,Quaternion.identity)<.01f,"climb release preserves view rotation");climb.gameObject.SetActive(false);motor.enabled=false;motor.externallyDriven=true;motor.RefreshMode();
                motor.Teleport(new Vector3(1000,1000.05f,1000),Quaternion.identity);
                var fast=Piece(new Vector3(1300,1200,0),Vector3.one*2);var sink=Piece(new Vector3(1400,1004,0),Vector3.one*2);var impact=Piece(new Vector3(1500,1008,0),Vector3.one*2);
                Floor(new Vector3(1400,999.5f,0),new Vector3(12,1,12));Floor(new Vector3(1500,999.5f,0),new Vector3(12,1,12));
                var cleanup=fixture.AddComponent<CityDebrisCleanup>();cleanup.Initialize(new[]{fast,sink,impact});float until=Time.time+2.05f;while(Time.time<until)yield return null;
                fast.position+=Vector3.up*10;fast.transform.position=fast.position;Physics.SyncTransforms();bool converted=cleanup.TryPush(fast,Vector3.forward*25);
                Check(converted&&cleanup.IsVisual(fast)&&fast.isKinematic&&fast.GetComponent<Collider>().isTrigger,"clear distant fast debris uses a query-only swept path");
                if(!Tracked.Movers.Contains(motor))Tracked.Movers.Add(motor);
                motor.Teleport(fast.position+Vector3.up*3,Quaternion.identity);yield return null;yield return null;
                Check(!cleanup.IsVisual(fast)&&!fast.isKinematic&&!fast.GetComponent<Collider>().isTrigger,"approaching observer restores native collision before contact");
                motor.Teleport(new Vector3(1000,1000.03f,1000),Quaternion.identity);sink.useGravity=true;impact.useGravity=true;impact.linearVelocity=Vector3.down*25;
                until=Time.time+4;while(Time.time<until&& (sink.gameObject.activeSelf||impact.gameObject.activeSelf))yield return null;
                Check(!sink.gameObject.activeSelf&&cleanup.SunkCount>0,"displaced unoccupied ground debris sinks and retires");
                Check(!impact.gameObject.activeSelf&&cleanup.ShatteredCount>0,"genuine high-speed native impact retires distant debris");
                Check(map.Blocks.Count==633,"cleanup preserves stable authored body indices");
                map.SetReplica(true);Check(map.Blocks.All(b=>b.isKinematic),"replica keeps every original collider with suspended independent simulation");map.SetReplica(false);
                Check(map.Blocks.All(b=>!b.isKinematic),"authority recovery restores independent original bodies");
                Check(DeadlyJenga.CandyArt.StructuralMeshBatches.SubmittedInstances>0,"room artwork instancing is active with fallback renderers available");
            }
            finally
            {
                Tracked.Movers.Remove(motor);motor.enabled=false;motor.externallyDriven=external;motor.controlMode=oldMode;motor.RefreshMode();Object.Destroy(fixture);
            }
        }
    }
}
