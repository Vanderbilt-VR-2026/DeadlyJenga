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
    public static class TycoonPhysicsChecks
    {
        const string Pending="DeadlyJenga.TycoonGravity.Pending";

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
            var report=new StringBuilder("Isolated Unity PhysX mode map checks\n15 simulated seconds per map; .02s fixed steps; authored breakaway supports; no joints or altered gravity.\n");int passed=0,failed=0;
            void Check(bool result,string message){report.Append(result?"PASS ":"FAIL ").AppendLine(message);if(result)passed++;else failed++;}
            var previous=Services.World;var watch=Stopwatch.StartNew();var surfaceCollision=new bool[32];for(int layer=0;layer<32;layer++)surfaceCollision[layer]=Physics.GetIgnoreLayerCollision(Layers.Surface,layer);bool survivorBlock=Physics.GetIgnoreLayerCollision(Layers.Survivor,Layers.Block);
            try
            {
                // Reproduce CastleBlock.Awake's runtime policy; the editor does not invoke Awake.
                Physics.IgnoreLayerCollision(Layers.Survivor,Layers.Block,true);
                for(int layer=0;layer<32;layer++)Physics.IgnoreLayerCollision(Layers.Surface,layer,layer!=Layers.Survivor);
                foreach(string preset in new[] { TycoonLayout.Preset })
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
                        Check(bodies.All(b=>b.isKinematic&&b.useGravity&&b.detectCollisions),preset+" all pieces are supported before impact and colliders remain active");
                        Check(root.GetComponentsInChildren<Joint>().Length==0,preset+" has no hidden stabilizing joints");
                        Physics.SyncTransforms();Check(physics.Raycast(generator.Center+Vector3.up*.5f,Vector3.down,out var floorHit,2,1<<0,QueryTriggerInteraction.Ignore)&&floorHit.collider.transform==generator.foundation,preset+" center ground entry stands on its actual foundation");
                        for(int step=0;step<750;step++)physics.Simulate(.02f);
                        foreach(var b in bodies)b.GetComponent<CastleBlock>()?.SyncSurface();Physics.SyncTransforms();
                        float drift=0,tilt=0,rms=0;int falling=0,escaped=0,awake=0;var footprint=CastleLayout.FootprintSize(generator.settings)+Vector2.one*4;
                        for(int i=0;i<bodies.Length;i++)
                        {
                            var b=bodies[i];drift=Mathf.Max(drift,Vector3.Distance(positions[i],b.position));tilt=Mathf.Max(tilt,Quaternion.Angle(rotations[i],b.rotation));rms+=b.linearVelocity.sqrMagnitude;if(b.position.y<positions[i].y-.7f)falling++;if(Mathf.Abs(b.position.x)>footprint.x*.5f||Mathf.Abs(b.position.z)>footprint.y*.5f)escaped++;if(!b.isKinematic&&!b.IsSleeping())awake++;
                        }
                        rms=Mathf.Sqrt(rms/Mathf.Max(1,bodies.Length));
                        report.AppendLine($"MEASURE {preset}: max displacement={drift:F4}m, max rotation={tilt:F3}deg, velocity RMS={rms:F4}m/s, awake={awake}/{bodies.Length}, dropped from resting course={falling}, outside footprint={escaped}");
                        Check(falling==0&&escaped==0,preset+" no spontaneous collapse or escape from its foundation");
                        Check(drift<.7f&&tilt<4,preset+" settled structure retains its authored configuration");
                        Check(rms<.08f,preset+" residual motion remains below .08m/s RMS");
                        Check(bodies.All(b=>Finite(b.position)&&Finite(b.linearVelocity)),preset+" all body positions and velocities remain finite");
                        var spawn=generator.SafeSpawnPoint();var spawnHits=new Collider[8];Check(physics.OverlapCapsule(spawn+Vector3.up*.18f,spawn+Vector3.up*.72f,.18f,spawnHits,(1<<0)|(1<<Layers.Block)|(1<<Layers.Surface),QueryTriggerInteraction.Ignore)==0,preset+" actual safe-spawn point has a .9m avatar's head and body clearance");
                        var layout=CastleLayout.Build(generator.settings); foreach(var entry in bodies.Select((body,index)=>(body,index)).Where(entry=>Vector3.Distance(positions[entry.index],entry.body.position)>.1f).OrderByDescending(entry=>Vector3.Distance(positions[entry.index],entry.body.position)).Take(30)){var p=layout[entry.index]; report.AppendLine($"{entry.index} {p.Section} {p.DeckProfile.Finish} {positions[entry.index]} => {entry.body.position}");}
                    }
                    catch(Exception error){Check(false,preset+" exception: "+error);}
                    finally {if(root)UnityEngine.Object.DestroyImmediate(root);if(previousScene.IsValid())SceneManager.SetActiveScene(previousScene);if(scene.IsValid())SceneManager.UnloadSceneAsync(scene);Services.World=previous;}
                    File.WriteAllText(ProjectPath.Report("TycoonPhysics.txt"),report.ToString());
                }
            }
            finally {Services.World=previous;for(int layer=0;layer<32;layer++)Physics.IgnoreLayerCollision(Layers.Surface,layer,surfaceCollision[layer]);Physics.IgnoreLayerCollision(Layers.Survivor,Layers.Block,survivorBlock);}
            watch.Stop();report.AppendLine($"TOTAL: {passed} passed, {failed} failed; wall time {watch.Elapsed.TotalSeconds:F2}s.");
            report.AppendLine("NOT VERIFIED: actual player jump/vault/climb control execution, bot navigation, headset rendering/frame budget, network peers, or stability after attacks. Route checks establish capsule-sized ledge clearance and geometric links, not a navigation implementation.");
            File.WriteAllText(ProjectPath.Report("TycoonPhysics.txt"),report.ToString());Debug.Log(report.ToString());if(failed>0)throw new InvalidOperationException(failed+" map physics checks failed; see TycoonPhysics.txt");
        }
        static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.x)&&!float.IsInfinity(v.y)&&!float.IsInfinity(v.z);
    }
}
