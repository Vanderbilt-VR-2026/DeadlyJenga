using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
using DeadlyJenga.CandyPlayers;

namespace DeadlyJenga.CastlePrototype.Editor
{
    public static class CastleClimbChecks
    {
        public static IEnumerator Check()
        {
            yield return null;
            var lines=new List<string>();var session=Object.FindFirstObjectByType<CastlePlaySession>();session.SetMenu(true);
            var staging=new GameObject("Climb check staging");staging.SetActive(false);
            var motor=Object.Instantiate(session.survivorPrefab,staging.transform).GetComponent<CastleSurvivor>();motor.externallyDriven=true;motor.enabled=false;
            var avatar=motor.GetComponent<CandyPlayerAvatar>();if(avatar)avatar.remoteDriven=true;
            foreach(var camera in motor.GetComponentsInChildren<Camera>(true))camera.enabled=false;
            foreach(var listener in motor.GetComponentsInChildren<AudioListener>(true))listener.enabled=false;
            staging.SetActive(true);motor.RefreshMode();
            var block=Object.Instantiate(session.generator.blockPrefab).GetComponent<CastleBlock>();
            var body=block.GetComponent<Rigidbody>();body.useGravity=false;body.isKinematic=true;
            const float dt=1f/60;
            void Assert(bool ok,string text){lines.Add((ok?"PASS ":"FAIL ")+text);File.WriteAllLines("ArtDirection/ClimbChecks.txt",lines);}
            void Setup(float height=10)
            {
                body.isKinematic=true;block.transform.SetPositionAndRotation(new Vector3(120,45,2),Quaternion.identity);block.transform.localScale=new Vector3(6,height,2);block.SyncSurface();block.RefreshAppearance();Physics.SyncTransforms();
                motor.Teleport(new Vector3(120,42,.82f),Quaternion.identity);
            }
            void Tick(Vector2 input,bool held,int frames){for(int i=0;i<frames;i++)motor.Simulate(input,false,dt,held);}
            Setup();Tick(Vector2.zero,false,12);Assert(!motor.Climbing,"Nearby wall does not automatically latch.");
            Setup();Tick(Vector2.zero,true,5);Assert(!motor.Climbing,"Brief jump press does not trigger a cling.");
            Tick(Vector2.zero,true,10);Assert(motor.Climbing,"Holding jump catches a vertical crate through its real survivor surface.");
            var start=motor.transform.position;Tick(Vector2.up,true,30);Assert(motor.Climbing&&motor.transform.position.y>start.y+.6f,"Forward input climbs upward.");
            start=motor.transform.position;Tick(Vector2.right,true,20);Assert(motor.Climbing&&motor.transform.position.x>start.x+.3f,"Right input shimmies right along the wall.");
            Assert(motor.TryGetClimbGrip(true,out var left)&&motor.TryGetClimbGrip(false,out var right)&&Vector3.Distance(left.position,right.position)>.1f,"Both avatar hands have separate wall contact targets.");
            if(avatar){avatar.RefreshExternal(dt);Assert(avatar.LastFrame.left.tracked&&Vector3.Distance(avatar.LastFrame.left.position,left.position)<.01f,"Shared avatar presents the wall grip to first and third person.");Capture(motor,avatar);}
            float spent=motor.GripRemaining;Tick(Vector2.zero,false,1);Assert(!motor.Climbing&&motor.GripRemaining<=spent,"Releasing drops without restoring stamina.");
            Setup();Tick(Vector2.zero,true,350);Assert(!motor.Climbing&&motor.GripRemaining<.01f,"Idle clinging exhausts grip and drops the survivor.");
            Tick(Vector2.zero,true,90);Assert(!motor.Climbing&&motor.GripRemaining<.01f,"Holding through exhaustion cannot relatch or recharge in air.");
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(120,motor.transform.position.y-1,0);floor.transform.localScale=new Vector3(20,.5f,20);
            Tick(Vector2.zero,false,140);Assert(motor.Grounded&&motor.GripRemaining>.7f,"Secure footing restores exhausted grip after a short rest.");Object.DestroyImmediate(floor);
            Setup();var next=Object.Instantiate(session.generator.blockPrefab).GetComponent<CastleBlock>();next.GetComponent<Rigidbody>().isKinematic=true;next.transform.SetPositionAndRotation(new Vector3(126,45,2),Quaternion.identity);next.transform.localScale=new Vector3(6,10,2);next.SyncSurface();
            motor.Teleport(new Vector3(122.7f,42,.82f),Quaternion.identity);Tick(Vector2.zero,true,15);spent=motor.GripRemaining;Tick(Vector2.right,true,35);
            Assert(motor.Climbing&&motor.transform.position.x>123.3f&&motor.GripRemaining<spent,"Climbing crosses adjacent crate seams without refilling grip.");Object.DestroyImmediate(next.gameObject);
            Setup();Tick(Vector2.zero,true,15);start=motor.transform.position;
            for(int i=0;i<10;i++){block.transform.position+=Vector3.right*.01f;block.SyncSurface();motor.Simulate(Vector2.zero,false,dt,true);}
            Assert(motor.Climbing&&motor.transform.position.x>start.x+.07f,"Gentle block movement carries the climber without parenting.");
            Assert(motor.transform.parent==staging.transform,"Climber never becomes a child of the physics block.");
            Setup();Tick(Vector2.zero,true,15);body.isKinematic=false;body.linearVelocity=new Vector3(8,2,0);start=motor.transform.position;Tick(Vector2.zero,true,1);
            Assert(!motor.Climbing&&motor.transform.position.x>start.x+.08f,"A thrown crate breaks grip and transfers launch momentum.");
            Setup();Tick(Vector2.zero,true,15);body.isKinematic=false;body.linearVelocity=Vector3.down*3;Tick(Vector2.zero,true,1);Assert(!motor.Climbing&&motor.VerticalSpeed<0,"A falling crate cannot be used as a permanent anchor.");
            Setup();Tick(Vector2.zero,true,15);body.isKinematic=false;body.angularVelocity=Vector3.up*3;Tick(Vector2.zero,true,1);Assert(!motor.Climbing,"Rapid tumbling breaks grip.");
            Setup();Tick(Vector2.zero,true,15);body.isKinematic=false;body.linearVelocity=Vector3.right;Tick(Vector2.zero,true,1);Assert(!motor.Climbing,"A sharp acceleration breaks grip even below the speed cap.");
            Setup();Tick(Vector2.zero,true,15);motor.ApplyTraversalImpulse(new Vector3(4,3,0));Assert(!motor.Climbing&&motor.VerticalSpeed>0,"Impact API breaks grip before applying knockback.");
            Setup();Tick(Vector2.zero,true,15);block.gameObject.SetActive(false);Tick(Vector2.zero,true,1);Assert(!motor.Climbing,"Disabled/destroyed support safely releases the climber.");block.gameObject.SetActive(true);
            Setup(2);motor.Teleport(new Vector3(120,44,.82f),Quaternion.identity);Tick(Vector2.up,true,100);
            Assert(motor.VaultCount>0&&!motor.Climbing,"Upward scramble transitions into the normal clear-ledge vault.");
            Setup();block.transform.rotation=Quaternion.Euler(20,0,0);block.SyncSurface();
            var patch=block.transform.TransformPoint(new Vector3(0,-.25f,-.5f));
            motor.Teleport(patch-block.transform.forward*.29f-Vector3.up*(motor.StandingHeight*.65f),Quaternion.identity);Tick(Vector2.zero,true,15);start=motor.transform.position;Tick(Vector2.up,true,20);
            Assert(motor.Climbing&&motor.transform.position.y>start.y+.3f,"A settled 70 degree face supports upward scrambling.");
            Assert(Physics.GetIgnoreLayerCollision(CastleBlock.SurvivorLayer,CastleBlock.BlockLayer),"Climbing keeps player-to-dynamic-block collision isolation.");
            Object.Destroy(staging);Object.Destroy(block.gameObject);Debug.Log(string.Join("\n",lines));EditorApplication.ExitPlaymode();
        }
        static void Capture(CastleSurvivor motor,CandyPlayerAvatar avatar)
        {
            var go=new GameObject("Climb proof camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.02f;camera.farClipPlane=500;camera.fieldOfView=52;
            camera.transform.position=motor.transform.position+new Vector3(1.4f,.9f,-1.7f);camera.transform.LookAt(motor.transform.position+Vector3.up*.45f);
            var rt=new RenderTexture(1100,850,24);var old=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var png=new Texture2D(1100,850,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1100,850),0,0);png.Apply();File.WriteAllBytes("ArtDirection/ClimbProof.png",png.EncodeToPNG());Object.DestroyImmediate(png);}
            finally{camera.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
        }
    }
}
