using System;
using System.Linq;
using System.Collections;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPlayers;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static class LedgeViewChecks
    {
        public static IEnumerator Run(Action<bool,string> check)
        {
            Validate(check);yield break;
        }
        public static void Validate(Action<bool,string> check)
        {
            var fixture=new GameObject("Ledge view fixture");var oldWorld=Services.World;
            try
            {
                var world=fixture.AddComponent<FakeWorldQuery>();world.stepHeight=2;
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("CastleSurvivor.prefab"));
                var instance=Object.Instantiate(prefab,fixture.transform);var motor=instance.GetComponent<CastleSurvivor>();
                motor.externallyDriven=true;motor.RefreshMode();motor.InputSuspended=false;
                var origin=new Vector3(1400,1400,1400);
                GameObject Box(string name,Vector3 offset,Vector3 size)
                {
                    var go=new GameObject(name);go.transform.SetParent(fixture.transform);go.transform.position=origin+offset;
                    go.AddComponent<BoxCollider>().size=size;return go;
                }
                Box("Island",new Vector3(0,-.1f,0),new Vector3(10,.2f,10));
                var ledge=Box("First two-metre course",new Vector3(0,1,1.4f),new Vector3(4,2,2));
                void Place(){motor.Teleport(origin+Vector3.up*.04f,Quaternion.identity);motor.viewCamera.transform.rotation=Quaternion.identity;Physics.SyncTransforms();}
                Place();motor.SetSinking(false);
                check(!motor.TryFindLedge(out _,out _),"Ordinary vault does not gain a two-metre standing reach");
                motor.SetSinking(true);
                check(motor.TryGetVaultTarget(out var surface,out _,out _)&&surface==ledge.GetComponent<Collider>(),"Sinking player can catch the first two-metre course without a jump");
                var ceiling=Box("Ceiling",new Vector3(0,2+motor.Capsule.height*.5f,1.4f),new Vector3(4,.2f,2));Physics.SyncTransforms();
                check(!motor.TryStartVault(),"Sinking escape still rejects an occupied landing");ceiling.SetActive(false);Physics.SyncTransforms();
                motor.InputSuspended=true;check(!motor.TryStartVault(),"Paused player cannot start an escape vault");motor.InputSuspended=false;
                check(motor.TryStartVault(),"Sinking escape uses the normal vault motor used by bots");
                for(int i=0;i<100;i++)motor.Simulate(Vector2.zero,false,.02f);
                check(!motor.Vaulting&&motor.FootPosition.y>origin.y+2,"Sinking escape completes using collision-swept traversal");
                Place();motor.SetSinking(true);world.stepHeight=20;
                ledge.GetComponent<BoxCollider>().size=new Vector3(4,6,2);ledge.transform.position=origin+new Vector3(0,3,1.4f);Physics.SyncTransforms();
                check(!motor.TryStartVault(),"An oversized map step cannot grant unlimited escape height");
                var rig=instance.GetComponentInChildren<CandyPlayerRig>();
                if(!rig){var visual=new GameObject("Rig fixture");visual.transform.SetParent(fixture.transform);rig=visual.AddComponent<CandyPlayerRig>();}
                var camera=new GameObject("Owner view").AddComponent<Camera>();camera.transform.SetParent(fixture.transform);rig.ownerCamera=camera;
                var original=rig.bodyRenderer.sharedMesh;var pose=new Pose(camera.transform.position,Quaternion.Euler(85,0,0));camera.transform.SetPositionAndRotation(pose.position,pose.rotation);
                void RenderCallback(string name,Camera value)=>typeof(CandyPlayerRig).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(rig,new object[]{default(ScriptableRenderContext),value});
                RenderCallback("BeforeCamera",camera);var visible=rig.bodyRenderer.sharedMesh;
                check(visible!=original&&visible.vertexCount==original.vertexCount&&visible.triangles.Length<original.triangles.Length,"First-person body removes obstructing torso triangles and keeps the shared rig");
                var weights=visible.boneWeights;var indices=visible.triangles;
                check(indices.Any(i=>weights[i].boneIndex0==4)&&indices.Any(i=>weights[i].boneIndex0==10),"First-person mesh retains hands and feet");
                check(camera.transform.position==pose.position&&Quaternion.Angle(camera.transform.rotation,pose.rotation)<.001f,"Downward view never displaces the tracked camera");
                RenderCallback("AfterCamera",camera);check(rig.bodyRenderer.sharedMesh==original,"Body is restored after the local camera finishes");
                var external=new GameObject("External camera").AddComponent<Camera>();external.transform.SetParent(fixture.transform);
                RenderCallback("BeforeCamera",external);check(rig.bodyRenderer.sharedMesh==original,"External cameras see the complete avatar");RenderCallback("AfterCamera",external);
                rig.SetAppearance(new AvatarAppearance{poweredArmor=true});
                RenderCallback("BeforeCamera",camera);check(rig.bodyRenderer.sharedMesh==visible,"Repeated local views reuse a cached mesh");
                rig.enabled=false;check(rig.bodyRenderer.sharedMesh==original,"Disabling the rig restores the complete body");
                check(rig.GetComponentsInChildren<Renderer>().All(r=>r.shadowCastingMode!=ShadowCastingMode.ShadowsOnly),"Interrupted first-person rendering restores cosmetic attachments on disable");
            }
            finally {Object.DestroyImmediate(fixture);Services.World=oldWorld;}
        }
    }
}
