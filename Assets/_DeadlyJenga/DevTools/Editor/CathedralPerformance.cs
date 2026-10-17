using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using DeadlyJenga.Core;
using DeadlyJenga.CandyArt;
using Object=UnityEngine.Object;
using DeadlyJenga.DevTools.Editor;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad] public static class CathedralPerformance
    {
        const string Key="CathedralPerformance";
        static string Report=>ProjectPath.Report("CathedralPerformance.txt");
        static CathedralPerformance()
        {
            EditorApplication.update+=()=>{const string path="Logs/Validation/CathedralPerformance.request";if(!File.Exists(path)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete(path);Run();};
            EditorApplication.playModeStateChanged+=state=>
            {if(!SessionState.GetBool(Key,false))return;if(state==PlayModeStateChange.EnteredPlayMode)EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Check());if(state==PlayModeStateChange.EnteredEditMode)SessionState.SetBool(Key,false);};
        }
        [MenuItem("Tools/Deadly Jenga/Maps/Profile Cathedral")]
        public static void Run()
        {EditorSceneManager.OpenScene(ProjectPath.Find("MapDrafterEvan1.unity"));SessionState.SetBool(Key,true);File.WriteAllText(Report,"Cathedral editor overview benchmark; VSync off; 24 frames per phase. Not a device FPS guarantee.\n");EditorApplication.EnterPlaymode();}
        static IEnumerator Check()
        {
            var session=Object.FindFirstObjectByType<CastlePlaySession>();session.SetMenu(true);
            var camera=session.Survivor().viewCamera;camera.transform.position=new Vector3(175,130,-210);camera.transform.LookAt(new Vector3(0,70,0));
            int vsync=QualitySettings.vSyncCount,rate=Application.targetFrameRate;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            var blocks=session.Generator().Bodies.Select(b=>b.GetComponent<CastleBlock>()).ToArray();
            var renderers=blocks.SelectMany(b=>b.GetComponentsInChildren<Renderer>()).ToArray();
            var previous=Physics.simulationMode;
            try
            {
                yield return new WaitForSecondsRealtime(2);
                File.AppendAllText(Report,"Blocks "+blocks.Length+"; renderers "+renderers.Length+"; shared visual mesh vertices "+blocks[0].GetComponentInChildren<MeshFilter>().sharedMesh.vertexCount+"\n");
                yield return Sample("Normal");
                foreach(var r in renderers)r.enabled=false;
                yield return Sample("Block rendering disabled");
                foreach(var r in renderers)r.enabled=true;
                foreach(var b in blocks)b.enabled=false;
                yield return Sample("Block behaviour/surface updates disabled");
                foreach(var b in blocks)b.enabled=true;
                Physics.simulationMode=SimulationMode.Script;
                yield return Sample("Physics simulation disabled");
                Physics.simulationMode=previous;
                yield return new WaitForSecondsRealtime(5);
                File.AppendAllText(Report,"Sleeping before settled sample: "+blocks.Count(b=>b.Body.IsSleeping())+" / "+blocks.Length+"\n");
                yield return Sample("Normal after settling");
                Verify(blocks,camera);
                yield return VerifyBatches(blocks,camera);
                var hud=Object.FindFirstObjectByType<CandyDemoHud>();if(hud)hud.enabled=false;
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(ProjectPath.Report("cathedral-batched-gameview.png"));
                yield return null;yield return null;if(hud)hud.enabled=true;
                File.AppendAllText(Report,"COMPLETE\n");
            }
            finally{Physics.simulationMode=previous;QualitySettings.vSyncCount=vsync;Application.targetFrameRate=rate;foreach(var b in blocks)if(b)b.enabled=true;foreach(var r in renderers)if(r)r.enabled=true;EditorApplication.ExitPlaymode();}
        }
        static IEnumerator VerifyBatches(CastleBlock[] blocks,Camera camera)
        {
            var visual=blocks[0].GetComponentInChildren<CandyCrateVisual>();var renderer=visual.GetComponent<MeshRenderer>();
            var original=camera.transform.position;
            camera.transform.position=visual.transform.position+Vector3.up*200;visual.Refresh();yield return new WaitForSecondsRealtime(.45f);
            if(SystemInfo.supportsInstancing&&renderer.sharedMaterial.enableInstancing)
                Require(renderer.forceRenderingOff,"Distant normal artwork uses instanced batches");
            var properties=new MaterialPropertyBlock();properties.SetColor("_BaseColor",Color.cyan);renderer.SetPropertyBlock(properties);
            yield return new WaitForSecondsRealtime(.45f);
            Require(!renderer.forceRenderingOff,"Per-block shader effects fall back to normal rendering");
            renderer.SetPropertyBlock(null);camera.transform.position=visual.transform.position+Vector3.up*3;visual.Refresh();
            yield return new WaitForSecondsRealtime(.45f);
            Require(!renderer.forceRenderingOff,"Close blocks return to fully detailed normal rendering");
            camera.transform.position=original;visual.Refresh();
        }
        static void Require(bool pass,string text)
        {File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+text+"\n");if(!pass)throw new InvalidOperationException(text);}
        static void Verify(CastleBlock[] blocks,Camera camera)
        {
            Require(blocks.All(b=>!b.Body.isKinematic&&b.Body.useGravity),"All 6098 structural bodies retain dynamic gravity");
            var visual=blocks[0].GetComponentInChildren<CandyCrateVisual>();var filter=visual.GetComponent<MeshFilter>();
            var pose=new Pose(camera.transform.position,camera.transform.rotation);
            camera.transform.position=visual.transform.position+Vector3.up*3;visual.Refresh();int close=filter.sharedMesh.vertexCount;
            camera.transform.position=visual.transform.position+Vector3.up*60;visual.Refresh();int middle=filter.sharedMesh.vertexCount;
            camera.transform.position=visual.transform.position+Vector3.up*200;visual.Refresh();int far=filter.sharedMesh.vertexCount;
            Require(close==2088&&middle==216&&far==96,"Near / middle / far visual detail: "+close+" / "+middle+" / "+far+" vertices; close artwork preserved");
            camera.transform.SetPositionAndRotation(pose.position,pose.rotation);
            var go=new GameObject("Temporary surface regression");go.transform.position=new Vector3(3000,10,3000);
            var shape=go.AddComponent<BoxCollider>();var body=go.AddComponent<Rigidbody>();var block=go.AddComponent<CastleBlock>();
            try
            {
                var surface=(BoxCollider)typeof(CastleBlock).GetField("surface",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(block);
                for(int i=0;i<10;i++)block.SyncSurface();
                Require(Climbables.TryGet(surface,out var climbable)&&ReferenceEquals(climbable,block),"Climbable proxy registration remains intact");
                go.transform.SetPositionAndRotation(new Vector3(3001,12,3002),Quaternion.Euler(20,30,40));go.transform.localScale=new Vector3(6,2,2);shape.center=new Vector3(.1f,.2f,.3f);shape.size=new Vector3(.8f,.9f,.7f);block.SyncSurface();
                Require(surface.attachedRigidbody.position.Equals(go.transform.position)&&Quaternion.Angle(surface.attachedRigidbody.rotation,go.transform.rotation)<.01f&&surface.transform.lossyScale.Equals(go.transform.lossyScale)&&surface.center.Equals(shape.center)&&surface.size.Equals(shape.size),"Proxy follows translation, rotation, scale and collider edits");
                Require(surface.attachedRigidbody.isKinematic&&!body.isKinematic&&Physics.GetIgnoreLayerCollision(Layers.Block,Layers.Survivor),"One-way player collision retained; players cannot push real blocks");
                var simulation=Physics.simulationMode;
                try{Physics.simulationMode=SimulationMode.Script;block.Push(Vector3.right*2,null);Physics.Simulate(.02f);Require(body.linearVelocity.x>1.9f,"Hand / hazard push still accelerates dynamic blocks");}
                finally{Physics.simulationMode=simulation;}
            }
            finally{Object.Destroy(go);}
        }
        static IEnumerator Sample(string phase)
        {
            var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);
            var counters=new List<(string name,ProfilerRecorder recorder)>();
            foreach(var handle in handles)
            {
                var description=ProfilerRecorderHandle.GetDescription(handle);
                if(description.Name=="Physics.Simulate"||description.Name=="Physics.SyncTransforms"||description.Name=="BehaviourUpdate"||description.Name=="BehaviourFixedUpdate"||description.Name=="Main Thread"||description.Name=="Render Thread")
                    counters.Add((description.Name,ProfilerRecorder.StartNew(description.Category,description.Name,32)));
            }
            try
            {
            for(int i=0;i<6;i++)yield return null;
            var frames=new List<float>();long triangles=0,batches=0;
            for(int i=0;i<24;i++){yield return null;frames.Add(Time.unscaledDeltaTime*1000);triangles+=UnityStats.triangles;batches+=UnityStats.batches;}
            frames.Sort();File.AppendAllText(Report,phase+": median "+frames[12].ToString("F2")+"ms, p90 "+frames[21].ToString("F2")+"ms; triangles "+triangles/24+"; batches "+batches/24+"\n");
            foreach(var counter in counters)
            {
                var samples=new List<ProfilerRecorderSample>();counter.recorder.CopyTo(samples);
                if(samples.Count>0)File.AppendAllText(Report,"  "+counter.name+" mean "+samples.Average(s=>(double)s.Value)/1000000+"ms\n");
            }
            }
            finally{foreach(var counter in counters)counter.recorder.Dispose();}
        }
    }
}
