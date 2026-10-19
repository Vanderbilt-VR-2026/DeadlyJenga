using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug=UnityEngine.Debug;

namespace DeadlyJenga.CastlePrototype.Editor
{
    public static class ModeMapPhysicsChecks
    {
        const string Pending="DeadlyJenga.ModeMapGravity.Pending";
        [MenuItem("Tools/Deadly Jenga/Maps/Check Mode Map Gravity (15 seconds each)")]
        public static void BeginPlay()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop the current Play session before starting map gravity checks.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Resume()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false))return;
                SessionState.SetBool(Pending,false);EditorApplication.delayCall+=Execute;
            };
        }
        static void Execute()
        {
            int result=0;try{Run();}catch(Exception error){Debug.LogException(error);result=1;}
            if(Application.isBatchMode)EditorApplication.Exit(result);else EditorApplication.ExitPlaymode();
        }
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Use BeginPlay to run actual runtime physics checks.");
            var report=new StringBuilder("Isolated Unity PhysX mode map checks\n15 simulated seconds per map; .02s fixed steps; no forced sleep, joints, kinematic blocks or altered gravity.\n");int passed=0,failed=0;
            void Check(bool result,string message){report.Append(result?"PASS ":"FAIL ").AppendLine(message);if(result)passed++;else failed++;}
            var previous=Services.World;var watch=Stopwatch.StartNew();var surfaceCollision=new bool[32];for(int layer=0;layer<32;layer++)surfaceCollision[layer]=Physics.GetIgnoreLayerCollision(Layers.Surface,layer);bool survivorBlock=Physics.GetIgnoreLayerCollision(Layers.Survivor,Layers.Block);
            try
            {
                // Reproduce CastleBlock.Awake's runtime policy; the editor does not invoke Awake.
                Physics.IgnoreLayerCollision(Layers.Survivor,Layers.Block,true);
                for(int layer=0;layer<32;layer++)Physics.IgnoreLayerCollision(Layers.Surface,layer,layer!=Layers.Survivor);
                foreach(string preset in ModeMapLayout.Presets)
                {
                    Scene scene=default;GameObject root=null;Scene previousScene=SceneManager.GetActiveScene();
                    try
                    {
                        scene=SceneManager.CreateScene("Mode physics QA - "+preset,new CreateSceneParameters(LocalPhysicsMode.Physics3D));var physics=scene.GetPhysicsScene();
                        Check(physics.IsValid()&&!physics.Equals(Physics.defaultPhysicsScene),preset+" uses its own isolated physics scene");
                        SceneManager.SetActiveScene(scene);
                        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("Mode_"+preset+".prefab"));if(!prefab)throw new InvalidOperationException("Build mode prefabs first.");
                        root=UnityEngine.Object.Instantiate(prefab);SceneManager.MoveGameObjectToScene(root,scene);var generator=root.GetComponent<CastleGenerator>();generator.generateOnStart=false;generator.Rebuild();
                        var bodies=generator.Bodies.ToArray();var positions=bodies.Select(b=>b.position).ToArray();var rotations=bodies.Select(b=>b.rotation).ToArray();
                        Check(generator.foundation&&generator.foundation.gameObject.layer==0&&!generator.foundation.GetComponent<Rigidbody>(),preset+" static foundation uses Default, not the survivor-only proxy layer");
                        Check(bodies.Length>=400&&bodies.Length<=800,preset+" instantiates expected bounded physics population: "+bodies.Length);
                        Check(bodies.All(b=>!b.isKinematic&&b.useGravity&&b.detectCollisions),preset+" every block uses real dynamic gravity and collisions");
                        Check(root.GetComponentsInChildren<Joint>().Length==0,preset+" has no hidden stabilizing joints");
                        Physics.SyncTransforms();Check(physics.Raycast(generator.Center+Vector3.up*.5f,Vector3.down,out var floorHit,2,1<<0,QueryTriggerInteraction.Ignore)&&floorHit.collider.transform==generator.foundation,preset+" center ground entry stands on its actual foundation");
                        for(int step=0;step<750;step++)physics.Simulate(.02f);
                        foreach(var b in bodies)b.GetComponent<CastleBlock>()?.SyncSurface();Physics.SyncTransforms();
                        float drift=0,tilt=0,rms=0;int falling=0,escaped=0,awake=0;var footprint=CastleLayout.FootprintSize(generator.settings)+Vector2.one*4;
                        for(int i=0;i<bodies.Length;i++)
                        {
                            var b=bodies[i];drift=Mathf.Max(drift,Vector3.Distance(positions[i],b.position));tilt=Mathf.Max(tilt,Quaternion.Angle(rotations[i],b.rotation));rms+=b.linearVelocity.sqrMagnitude;if(b.position.y<0)falling++;if(Mathf.Abs(b.position.x)>footprint.x*.5f||Mathf.Abs(b.position.z)>footprint.y*.5f)escaped++;if(!b.IsSleeping())awake++;
                        }
                        rms=Mathf.Sqrt(rms/Mathf.Max(1,bodies.Length));
                        report.AppendLine($"MEASURE {preset}: max displacement={drift:F4}m, max rotation={tilt:F3}deg, velocity RMS={rms:F4}m/s, awake={awake}/{bodies.Length}, below foundation={falling}, outside footprint={escaped}");
                        Check(falling==0&&escaped==0,preset+" no spontaneous collapse or escape from its foundation");
                        Check(drift<.7f&&tilt<4,preset+" settled structure retains its authored configuration");
                        Check(rms<.08f,preset+" residual motion remains below .08m/s RMS");
                        Check(bodies.All(b=>Finite(b.position)&&Finite(b.linearVelocity)),preset+" all body positions and velocities remain finite");
                        var spawn=generator.SafeSpawnPoint();var spawnHits=new Collider[8];Check(physics.OverlapCapsule(spawn+Vector3.up*.18f,spawn+Vector3.up*.72f,.18f,spawnHits,(1<<0)|(1<<Layers.Block)|(1<<Layers.Surface),QueryTriggerInteraction.Ignore)==0,preset+" actual safe-spawn point has a .9m avatar's head and body clearance");
                        if(preset=="ctf")CheckFortressRoutes(generator,physics,report,Check);
                    }
                    catch(Exception error){Check(false,preset+" exception: "+error);}
                    finally {if(root)UnityEngine.Object.DestroyImmediate(root);if(previousScene.IsValid())SceneManager.SetActiveScene(previousScene);if(scene.IsValid())SceneManager.UnloadSceneAsync(scene);Services.World=previous;}
                    File.WriteAllText(ProjectPath.Report("ModeMapPhysics.txt"),report.ToString());
                }
            }
            finally {Services.World=previous;for(int layer=0;layer<32;layer++)Physics.IgnoreLayerCollision(Layers.Surface,layer,surfaceCollision[layer]);Physics.IgnoreLayerCollision(Layers.Survivor,Layers.Block,survivorBlock);}
            watch.Stop();report.AppendLine($"TOTAL: {passed} passed, {failed} failed; wall time {watch.Elapsed.TotalSeconds:F2}s.");
            report.AppendLine("NOT VERIFIED: actual player jump/vault/climb control execution, bot navigation, headset rendering/frame budget, network peers, or stability after attacks. Route checks establish capsule-sized ledge clearance and geometric links, not a navigation implementation.");
            File.WriteAllText(ProjectPath.Report("ModeMapPhysics.txt"),report.ToString());Debug.Log(report.ToString());if(failed>0)throw new InvalidOperationException(failed+" map physics checks failed; see ModeMapPhysics.txt");
        }
        static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.x)&&!float.IsInfinity(v.y)&&!float.IsInfinity(v.z);
        static void CheckFortressRoutes(CastleGenerator map,PhysicsScene physics,StringBuilder report,Action<bool,string> check)
        {
            var bodies=map.Bodies;var tops=new Vector3[bodies.Count];var clear=new bool[bodies.Count];var overlap=new Collider[16];int clearCount=0;
            for(int i=0;i<bodies.Count;i++)
            {
                var box=bodies[i].GetComponent<BoxCollider>();if(!box)continue;tops[i]=box.bounds.center+Vector3.up*(box.bounds.extents.y+.035f);
                // Current half-size avatar: .9m standing height and .18m body radius.
                clear[i]=physics.OverlapCapsule(tops[i]+Vector3.up*.18f,tops[i]+Vector3.up*.72f,.18f,overlap,(1<<Layers.Block)|(1<<Layers.Surface),QueryTriggerInteraction.Ignore)==0;
                if(clear[i])clearCount++;
            }
            var reached=new bool[bodies.Count];var queue=new Queue<int>();
            for(int i=0;i<tops.Length;i++)if(clear[i]&&tops[i].y<map.StepHeight+.25f){reached[i]=true;queue.Enqueue(i);}
            while(queue.Count>0)
            {
                int current=queue.Dequeue();for(int j=0;j<tops.Length;j++)
                {
                    if(reached[j]||!clear[j])continue;float rise=tops[j].y-tops[current].y;
                    if(rise<-.2f||rise>map.StepHeight+.15f)continue;var horizontal=Vector3.ProjectOnPlane(tops[j]-tops[current],Vector3.up);
                    if(horizontal.sqrMagnitude>3.1f*3.1f)continue;reached[j]=true;queue.Enqueue(j);
                }
            }
            float left=float.NegativeInfinity,right=float.NegativeInfinity;int leftReached=0,rightReached=0;
            var authored=CastleLayout.Build(map.settings);float fortressThreshold=(2*map.settings.blockSize.x-map.settings.blockSize.z)*1.5f+map.settings.blockSize.z*.5f;
            // Region membership comes from the authored layout, not tiny post-settle drift at x=+/-20.
            for(int i=0;i<tops.Length;i++)if(reached[i]){if(authored[i].Position.x<-fortressThreshold){left=Mathf.Max(left,tops[i].y);leftReached++;}if(authored[i].Position.x>fortressThreshold){right=Mathf.Max(right,tops[i].y);rightReached++;}}
            report.AppendLine($"MEASURE ctf route candidates: clear ledges={clearCount}/{bodies.Count}, left reachable ledges={leftReached} height={left:F2}m, right={rightReached} height={right:F2}m. Ground to first ledge <=one block; subsequent links <=one block rise and 3.1m horizontal.");
            check(left>31&&right>31,"ctf both highest fortress roofs have ground-connected capsule-clear ledge candidates");
            check(Mathf.Abs(left-right)<.1f&&leftReached==rightReached,"ctf team ends retain equal reachable ledge count and height after settling");
            bool stairs=true;float pitch=2*map.settings.blockSize.x-map.settings.blockSize.z;
            for(int side=-1;side<=1;side+=2)for(int step=0;step<16;step++)
            {
                var tread=map.transform.TransformPoint(new Vector3(side*(4*pitch-15*map.settings.blockSize.z+step*map.settings.blockSize.z),(step+1)*map.settings.blockSize.y,side*(pitch+map.settings.blockSize.x+map.settings.blockSize.z)));
                bool ground=physics.Raycast(tread+Vector3.up*.25f,Vector3.down,out var hit,.6f,1<<Layers.Block,QueryTriggerInteraction.Ignore);
                if(!ground){stairs=false;continue;}
                var feet=hit.point+Vector3.up*.035f;
                stairs&=physics.OverlapCapsule(feet+Vector3.up*.18f,feet+Vector3.up*.72f,.18f,overlap,(1<<Layers.Block)|(1<<Layers.Surface),QueryTriggerInteraction.Ignore)==0;
                stairs&=!physics.Raycast(feet+Vector3.up*.95f,Vector3.up,50,1<<Layers.Block,QueryTriggerInteraction.Ignore);
            }
            check(stairs,"ctf all 32 settled exterior treads retain capsule clearance and open overhead jump space");
            report.AppendLine("CTF carrier approaches: 6m-wide grounded exterior treads, 2m run/rise, no overhead pinch; real jump-plus-slow-vault traversal remains a separate controller acceptance check.");
        }
    }
}
