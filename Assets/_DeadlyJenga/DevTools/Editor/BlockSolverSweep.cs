using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.World.Hazards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.DevTools.Editor
{
    // Explicit isolated native-physics experiment. Never advances or saves the user's scene.
    [InitializeOnLoad] public static class BlockSolverSweep
    {
        const string Key="BlockSolverSweep";
        [Serializable] sealed class Scenes {public SceneSetup[] scenes;}
        static readonly int[] positions={80,80,80};
        static readonly int[] velocities={20,8,4};
        static readonly List<Rigidbody> bodies=new List<Rigidbody>();
        static readonly List<Vector3> start=new List<Vector3>();
        static readonly List<Quaternion> rotations=new List<Quaternion>();
        static readonly List<double> idle=new List<double>(),collapse=new List<double>(),waveTimes=new List<double>();
        static Scene scene;
        static PhysicsScene physics;
        static int profile=-1,tick;
        static float idleDrop,idleDrift,idleAngle;
        static string Report=>ProjectPath.Report("BlockSolverSweep.txt");
        static BlockSolverSweep()
        {
            EditorApplication.update+=Update;
            AssemblyReloadEvents.beforeAssemblyReload+=Clean;
            EditorApplication.quitting+=Clean;
            EditorApplication.playModeStateChanged+=state=>{
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode){try{profile=0;Build();}catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");Clean();EditorApplication.ExitPlaymode();}}
                if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);profile=-1;EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes","")).scenes);SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);}
            };
        }
        [MenuItem("Tools/Deadly Jenga/Performance/Compare Block Solvers")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||profile>=0)throw new Exception("The solver experiment needs an idle Editor.");
            File.WriteAllText(Report,"Isolated native 6098-block cathedral physics; original prefab collision, mass and damping; dt=.02. No rendering, bots, proxies or game FPS claim.\n300 idle steps; remove a bottom support patch + pull a block for 200 steps; northbound tsunami forces for 200 steps.\n");
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(v=>string.IsNullOrEmpty(v.path)||v.isLoaded&&SceneManager.GetSceneByPath(v.path).isDirty))throw new Exception("Save edited scenes before the solver experiment.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{scenes=scenes}));SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);SessionState.SetBool(Key,true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();
        }
        static void Update()
        {
            var request=ProjectPath.Report("BlockSolverSweep.request");
            if(profile<0)
            {
                if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                File.Delete(request);try{Run();}catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");Clean();}return;
            }
            try
            {
                if(!EditorApplication.isPlaying)return;
                var work=Stopwatch.StartNew();
                do { Step(); } while(profile>=0&&work.Elapsed.TotalMilliseconds<20);
            }
            catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");Clean();if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();}
        }
        static void Build()
        {
            CloseScene();bodies.Clear();start.Clear();rotations.Clear();idle.Clear();collapse.Clear();waveTimes.Clear();tick=0;
            scene=SceneManager.CreateScene("Temporary solver experiment "+profile,new CreateSceneParameters(LocalPhysicsMode.Physics3D));physics=scene.GetPhysicsScene();
            string source=File.ReadAllText(ProjectPath.Find("MapDrafterEvan1.unity"));
            var guid=Regex.Match(source,@"blockPrefab: \{fileID: \d+, guid: ([0-9a-f]+)").Groups[1].Value;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var template=prefab.GetComponent<Rigidbody>();var box=prefab.GetComponent<BoxCollider>();
            var settings=new CastleSettings{cathedral=true};var layout=CastleLayout.Build(settings);
            if(layout.Count!=6098)throw new Exception("Cathedral fixture count changed: "+layout.Count);
            var floor=New("Foundation");floor.transform.position=Vector3.down*.5f;var ground=floor.AddComponent<BoxCollider>();ground.size=new Vector3(CastleLayout.Footprint(settings)+4,1,CastleLayout.Footprint(settings)+4);ground.sharedMaterial=box.sharedMaterial;
            foreach(var p in layout)
            {
                var go=New("Block");go.layer=Layers.Block;go.transform.SetPositionAndRotation(p.Position,p.Rotation);go.transform.localScale=p.BlockSize(settings);
                var shape=go.AddComponent<BoxCollider>();shape.center=box.center;shape.size=box.size;shape.sharedMaterial=box.sharedMaterial;
                var body=go.AddComponent<Rigidbody>();body.mass=40;body.linearDamping=template.linearDamping;body.angularDamping=template.angularDamping;
                body.collisionDetectionMode=template.collisionDetectionMode;body.sleepThreshold=.005f;
                body.solverIterations=positions[profile];body.solverVelocityIterations=velocities[profile];
                bodies.Add(body);start.Add(p.Position);rotations.Add(p.Rotation);
            }
            Physics.SyncTransforms();
            File.AppendAllText(Report,"BEGIN "+positions[profile]+"/"+velocities[profile]+" collision="+template.collisionDetectionMode+"\n");
        }
        static GameObject New(string name){var go=new GameObject(name){hideFlags=HideFlags.DontSave};SceneManager.MoveGameObjectToScene(go,scene);return go;}
        static void Step()
        {
            if(tick==300)
            {
                idleDrop=idleDrift=idleAngle=0;
                for(int i=0;i<bodies.Count;i++){idleDrop=Mathf.Max(idleDrop,start[i].y-bodies[i].position.y);idleDrift=Mathf.Max(idleDrift,Vector3.ProjectOnPlane(start[i]-bodies[i].position,Vector3.up).magnitude);idleAngle=Mathf.Max(idleAngle,Quaternion.Angle(rotations[i],bodies[i].rotation));}
                int removed=0;
                for(int i=0;i<bodies.Count;i++)if(start[i].y<1.1f&&start[i].x<0&&start[i].z<0){bodies[i].gameObject.SetActive(false);removed++;}
                var pulled=bodies.Where(b=>b.gameObject.activeSelf&&b.position.y>8&&b.position.y<12).OrderBy(b=>b.position.x).First();pulled.AddForce(Vector3.left*10,ForceMode.VelocityChange);
                File.AppendAllText(Report,"IDLE maxDrop="+idleDrop.ToString("F4")+" maxXZ="+idleDrift.ToString("F4")+" maxAngle="+idleAngle.ToString("F3")+" sleeping="+bodies.Count(b=>b.IsSleeping())+" removed="+removed+"\n");
            }
            if(tick>=500)
            {
                var wave=new WaveFrame{active=true,center=new Vector3(0,0,55-(tick-500)*.16f),direction=Vector3.back,width=160,depth=28,height=22,waterHeight=.08f};
                foreach(var body in bodies)
                {
                    if(!body.gameObject.activeSelf)continue;
                    var bounds=body.GetComponent<Collider>().bounds;float surface=TsunamiHazard.SurfaceHeight(wave,bounds.center);float wet=Mathf.Clamp01((surface-bounds.min.y)/Mathf.Max(.1f,bounds.size.y));
                    if(wet>0)body.AddForce((Vector3.back*9+Vector3.up*5)*wet*.02f,ForceMode.VelocityChange);
                }
            }
            var timer=Stopwatch.StartNew();physics.Simulate(.02f);timer.Stop();
            (tick<300?idle:tick<500?collapse:waveTimes).Add(timer.Elapsed.TotalMilliseconds);tick++;
            if(tick!=700)return;
            int fallen=0;float height=0;
            for(int i=0;i<bodies.Count;i++)if(bodies[i].gameObject.activeSelf){if(start[i].y-bodies[i].position.y>2)fallen++;height=Mathf.Max(height,bodies[i].position.y);}
            File.AppendAllText(Report,"RESULT idle "+Stats(idle)+" collapse "+Stats(collapse)+" wave "+Stats(waveTimes)+" fallen>2m="+fallen+" maxHeight="+height.ToString("F2")+"\n");
            profile++;if(profile<positions.Length)Build();else{File.AppendAllText(Report,"COMPLETE\n");Clean();EditorApplication.ExitPlaymode();}
        }
        static string Stats(List<double> values){values.Sort();return "median="+values[values.Count/2].ToString("F2")+"ms p90="+values[(int)(values.Count*.9)].ToString("F2")+"ms";}
        static void CloseScene(){if(scene.IsValid()&&scene.isLoaded){if(Application.isPlaying){foreach(var go in scene.GetRootGameObjects())go.SetActive(false);SceneManager.UnloadSceneAsync(scene);}else EditorSceneManager.CloseScene(scene,true);}scene=default;}
        static void Clean(){CloseScene();bodies.Clear();profile=-1;}
    }
}
