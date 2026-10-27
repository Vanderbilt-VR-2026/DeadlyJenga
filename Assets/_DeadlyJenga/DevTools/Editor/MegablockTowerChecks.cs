using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class MegablockTowerChecks
    {
        const string Key="MegablockTowerChecks";
        [Serializable] sealed class Scenes {public SceneSetup[] values;}
        static IEnumerator run;static int failures,errors;static Action restore;
        static string Report=>ProjectPath.Report("MegablockTower-runtime.txt");
        static MegablockTowerChecks(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        public static void Run()
        {
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>s.isLoaded&&(string.IsNullOrEmpty(s.path)||UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty)))throw new InvalidOperationException("Unsaved scenes left untouched.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{values=scenes}));
            SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);
            MatchSetup.MapId=MegablockTowerBuild.Id;MatchSetup.ModeId="demo";MatchSetup.BotTarget=1;MatchSetup.HandCount=0;MatchSetup.FromLobby=true;MatchSetup.StartLocalSession=false;
            File.WriteAllText(Report,"Actual Game.unity / Megablock Tower. Desktop Editor, not Quest or headset verification.\n"+(Application.isBatchMode?"Graphics-enabled batch Editor; manual camera captures are rendered. Frame timings describe this editor/simulation run, not full-screen gameplay performance.\n":"Uncapped interactive Editor timing.\n"));
            SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {failures=errors=0;EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();SessionState.SetBool(Key+"Running",true);Application.logMessageReceived+=Log;run=Tests();}
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                if(SessionState.GetBool(Key+"Running",false)){SessionState.SetBool(Key+"Running",false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();}
                SessionState.SetBool(Key,false);Application.logMessageReceived-=Log;run=null;restore=null;
                SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
                MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
                EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes","")).values);
                ValidationRunGate.Release("MegablockTower");
                if(File.Exists(ProjectPath.Report("MegablockTower-exterior.png")))InstallPreview();
                if(Application.isBatchMode)EditorApplication.Exit(SessionState.GetBool(Key+"Passed",false)?0:2);
            }
        }
        static void Tick()
        {
            if(run==null)return;
            try{if(run.MoveNext())return;}catch(Exception e){failures++;File.AppendAllText(Report,"FAIL "+e+"\n");}
            (run as IDisposable)?.Dispose();run=null;restore?.Invoke();restore=null;Application.logMessageReceived-=Log;
            File.AppendAllText(Report,$"COMPLETE failures={failures} runtimeErrors={errors}\n");SessionState.SetBool(Key+"Passed",failures==0&&errors==0);EditorApplication.ExitPlaymode();
        }
        static void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}}
        static void Check(bool pass,string text)
        {File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+text+"\n");if(!pass)throw new InvalidOperationException(text);}
        static IEnumerator Tests()
        {
            // MatchChoices resets during Play Mode's domain reload. Apply the selection afterwards.
            MatchSetup.MapId=MegablockTowerBuild.Id;MatchSetup.ModeId="demo";MatchSetup.BotTarget=1;MatchSetup.HandCount=0;MatchSetup.FromLobby=true;MatchSetup.StartLocalSession=false;
            EditorSceneManager.LoadSceneInPlayMode(ProjectPath.Find("Game.unity"),new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            float until=Time.realtimeSinceStartup+40;
            while((Services.Session?.SurvivorBots==null||!(Services.World is MegablockTowerMap))&&Time.realtimeSinceStartup<until)yield return null;
            var session=Services.Session;var map=Services.World as MegablockTowerMap;
            Check(map&&session?.Player!=null,"Production Game session loads selected tower through lobby catalog");
            bool background=Application.runInBackground;int rate=Application.targetFrameRate,vsync=QualitySettings.vSyncCount;float volume=AudioListener.volume;
            restore=()=>{Application.runInBackground=background;Application.targetFrameRate=rate;QualitySettings.vSyncCount=vsync;AudioListener.volume=volume;};
            Application.runInBackground=true;Application.targetFrameRate=-1;QualitySettings.vSyncCount=0;AudioListener.volume=0;
            session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots.Clear();if(session.Social!=null)session.Social.VoiceEnabled=false;
            session.SetMenu(true);
            File.AppendAllText(Report,$"Bodies={map.Blocks.Count}; collision shapes={map.Authored.Sum(m=>m.shapes.Count)}; height={map.TopHeight:F2}m; unified={TraversalCollision.Unified}.\n");
            Check(map.Blocks.Count==156,"156 independently movable assemblies including entrances and roof");
            Check(map.Blocks.All(b=>b&&!b.isKinematic),"All structural assemblies use gravity; no fixed tower core");
            var positions=map.Blocks.Select(b=>b.position).ToArray();
            until=Time.realtimeSinceStartup+12;while(Time.realtimeSinceStartup<until)yield return null;
            float drift=map.Blocks.Select((b,i)=>Vector3.Distance(b.position,positions[i])).Max();
            File.AppendAllText(Report,$"12-second gravity settle: maximum displacement={drift:F3}m; sleeping={map.Blocks.Count(b=>b.IsSleeping())}/{map.Blocks.Count}.\n");
            Check(drift<.35f,"Tower remains standing under its own weight");
            CheckCollisionLayout(map);
            // Window center ray must pass through the facade; sill ray must hit a real block.
            for(int floor=0;floor<MegablockTowerLayout.Floors;floor++)
            {
                float y=MegablockTowerLayout.FloorTop(floor);
                Check(!Physics.Raycast(new Vector3(5.3f,y+2.2f,-16),Vector3.forward,1.5f,1<<Layers.Block,QueryTriggerInteraction.Ignore),"Open front window at floor "+(floor+1));
                Check(Physics.Raycast(new Vector3(5.3f,y+.2f,-16),Vector3.forward,1.5f,1<<Layers.Block,QueryTriggerInteraction.Ignore),"Solid window sill at floor "+(floor+1));
            }
            Capture("exterior",new Vector3(118,102,-162),new Vector3(0,69,0),52);
            float eye=Mathf.Clamp(session.ActiveCamera.transform.position.y-((Component)session.Player).transform.position.y,.4f,2);
            float insideY=MegablockTowerLayout.FloorTop(12)+.08f+eye;
            Capture("ground",new Vector3(33,.08f+eye,-49),new Vector3(0,82,0),75);
            Capture("interior",new Vector3(10,insideY,5),new Vector3(-2,insideY+2,-8),86);
            Capture("room",new Vector3(16,insideY,11),new Vector3(10,insideY,2),85);
            Capture("window",new Vector3(12.5f,insideY,10),new Vector3(15,insideY,16),85);
            // Traverse every flight with the production survivor motor, not a camera flythrough.
            var actor=session.SurvivorBots.SpawnOne();var motor=actor?actor.GetComponent<CastleSurvivor>():null;
            Check(motor,"Production survivor bot can spawn on the island");
            Check(motor.TryStandOn(map.Blocks[0],0,out var stand)&&stand.y>.3f&&stand.y<1,"Landing queries still resolve the offset floor box");
            foreach(var c in actor.GetComponents<MonoBehaviour>())if(c is DeadlyJenga.CastleBots.ICastleSurvivorDriver)c.enabled=false;
            motor.enabled=false;motor.externallyDriven=true;motor.InputDriver=null;motor.RefreshMode();motor.InputSuspended=false;session.SetMenu(false);
            motor.Teleport(new Vector3(0,.08f,22),Quaternion.Euler(0,180,0));yield return null;
            var path=new List<Vector3>{new Vector3(0,.68f,13),new Vector3(-2.5f,.68f,9.5f)};
            for(int f=0;f<MegablockTowerLayout.Floors;f++)
            {
                float y=MegablockTowerLayout.FloorTop(f);
                path.Add(new Vector3(-2.5f,y+2.48f,-9.5f));path.Add(new Vector3(2.5f,y+2.48f,-9.5f));
                path.Add(new Vector3(2.5f,y+4.88f,9.5f));
                if(f+1<MegablockTowerLayout.Floors)path.Add(new Vector3(-2.5f,y+4.88f,9.5f));
            }
            path.Add(new Vector3(12,MegablockTowerLayout.FloorTop(30)+.08f,12));
            int waypoint=0;
            foreach(var authoredGoal in path)
            {
                var goal=authoredGoal;
                if(Physics.Raycast(goal+Vector3.up*.6f,Vector3.down,out var settledFloor,1.2f,1<<Layers.Block,QueryTriggerInteraction.Ignore))goal.y=settledFloor.point.y+.08f;
                bool reached=false;
                for(int step=0;step<650;step++)
                {
                    var delta=Vector3.ProjectOnPlane(goal-motor.transform.position,Vector3.up);
                    if(delta.magnitude<.22f&&Mathf.Abs(goal.y-motor.transform.position.y)<.3f){reached=true;break;}
                    if(delta.sqrMagnitude>.001f){motor.transform.rotation=Quaternion.LookRotation(delta);motor.viewCamera.transform.localRotation=Quaternion.identity;}
                    motor.InputSuspended=false;motor.Simulate(Vector2.up*Mathf.Clamp01(delta.magnitude/Mathf.Max(.001f,motor.EffectiveWalkSpeed*.02f)),false,.02f,false);
                    if(step%40==39)yield return null;
                }
                if(!reached)
                {
                    File.AppendAllText(Report,$"Stuck grounded={motor.Grounded}; normal={motor.GroundNormal}; step={motor.Capsule.stepOffset}; skin={motor.Capsule.skinWidth}; standing={motor.StandingSurface?.name}.\n");
                    foreach(var body in map.Blocks.Where(b=>b.position.y<goal.y+1&&b.position.y>goal.y-6))
                        File.AppendAllText(Report,$"Nearby body {body.name}: {body.position:F4}; rotation={body.rotation.eulerAngles:F4}.\n");
                    foreach(float offset in new[]{-.3f,0,.15f,.3f,.6f,1f})
                    {
                        var p=motor.transform.position+Vector3.forward*offset+Vector3.up*1.5f;
                        if(Physics.Raycast(p,Vector3.down,out var floorHit,2,1<<Layers.Block,QueryTriggerInteraction.Ignore))File.AppendAllText(Report,$"Ground sample dz={offset:F2}: {floorHit.point:F4}; {floorHit.collider.name}; center={(floorHit.collider as BoxCollider)?.center}.\n");
                    }
                    Capture("route-stuck",motor.transform.position+new Vector3(5,2,-3),motor.transform.position+Vector3.up*.4f,70);
                }
                Check(reached,$"Motor route waypoint {++waypoint}/{path.Count}, goal={goal}, actual={motor.transform.position}");
            }
            Check(motor.transform.position.y>143,"Survivor reaches roof from island without teleporting between flights");
            float faceY=MegablockTowerLayout.FloorTop(2)+.08f;
            motor.Teleport(new Vector3(20.34f,faceY,0),Quaternion.LookRotation(Vector3.left));motor.viewCamera.transform.localRotation=Quaternion.identity;
            motor.climbingEnabled=true;motor.climbGripSeconds=8;yield return null;
            for(int step=0;step<35;step++)motor.Simulate(Vector2.up,false,.02f,true);
            Check(motor.Climbing&&motor.transform.position.y>faceY+.15f,"Production motor climbs an offset wall collider");
            var gripCollider=typeof(CastleSurvivor).GetField("climbCollider",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.GetValue(motor) as Collider;
            Check(gripCollider&&motor.TryGetClimbGrip(true,out var grip)&&Vector3.Distance(grip.position,gripCollider.ClosestPoint(grip.position))<.12f,"Climb grip stays on the wall face rather than the collider transform origin");
            var anchor=typeof(CastleSurvivor).GetField("climbBody",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.GetValue(motor) as Rigidbody;
            Check(anchor,"Climbing resolves the structural rigidbody anchor");
            var beforeCarry=motor.transform.position;anchor.transform.position+=Vector3.forward*.03f;Physics.SyncTransforms();motor.Simulate(Vector2.zero,false,.02f,true);
            Check(motor.Climbing&&motor.transform.position.z>beforeCarry.z+.015f,"Clinging survivor follows a small structural-anchor movement");
            motor.Simulate(Vector2.zero,false,.02f,false);Check(!motor.Climbing,"Climb releases normally");
            CheckVaultQueries(motor,map);
            session.SurvivorBots.Clear();session.SetMenu(true);map.Rebuild();session.SurvivorBots.MapRebuilt();
            until=Time.realtimeSinceStartup+3;while(Time.realtimeSinceStartup<until)yield return null;
            var samples=new List<float>();until=Time.realtimeSinceStartup+5;while(Time.realtimeSinceStartup<until){yield return null;samples.Add(Time.unscaledDeltaTime*1000);}Frames("No bots / no disaster",samples);
            session.SurvivorBots.Running=true;for(int i=0;i<32;i++)session.SurvivorBots.SpawnOne();
            until=Time.realtimeSinceStartup+3;while(Time.realtimeSinceStartup<until)yield return null;
            Check(session.SurvivorBots.Count==32,"Stress run has 32 live survivor bots");
            samples.Clear();until=Time.realtimeSinceStartup+5;while(Time.realtimeSinceStartup<until){yield return null;samples.Add(Time.unscaledDeltaTime*1000);}Frames("32 bots / no disaster",samples);
            var wave=Object.Instantiate(Resources.Load<GameObject>("DemoDisasters/Tsunami"));var hazard=wave.GetComponent<IHazard>();var component=hazard as Component;
            Set(component,"randomDirection",false);Set(component,"approachHeading",0f);Set(component,"warningTime",0f);Set(component,"offshoreDistance",0f);Set(component,"riseTime",.1f);
            positions=map.Blocks.Select(b=>b.position).ToArray();hazard.Begin();samples.Clear();until=Time.realtimeSinceStartup+20;
            while(hazard.Active&&Time.realtimeSinceStartup<until){yield return null;samples.Add(Time.unscaledDeltaTime*1000);}Frames("32 bots + tsunami (20s targeted exposure)",samples);
            File.AppendAllText(Report,"Tsunami displaced>.5m="+map.Blocks.Where((b,i)=>Vector3.Distance(b.position,positions[i])>.5f).Count()+"/"+map.Blocks.Count+". Default wave forces; only staging shortened.\n");
            hazard.End();Object.Destroy(wave);Capture("tsunami",new Vector3(118,102,-162),new Vector3(0,69,0),52);
            session.SurvivorBots.Clear();map.Rebuild();until=Time.realtimeSinceStartup+3;while(Time.realtimeSinceStartup<until)yield return null;
            positions=map.Blocks.Select(b=>b.position).ToArray();
            var target=map.Blocks[150];target.GetComponent<IPushable>().Push(new Vector3(18,2,-8),null);
            until=Time.realtimeSinceStartup+10;while(Time.realtimeSinceStartup<until)yield return null;
            int displaced=map.Blocks.Where((b,i)=>Vector3.Distance(b.position,positions[i])>.5f).Count();
            Check(displaced>0,"Exposed roof corner impact (18,2,-8 m/s velocity change) moves physical assemblies; displaced="+displaced);
            Capture("impact",new Vector3(118,102,-162),new Vector3(0,69,0),52);
            map.Rebuild();until=Time.realtimeSinceStartup+3;while(Time.realtimeSinceStartup<until)yield return null;
            positions=map.Blocks.Select(b=>b.position).ToArray();map.Blocks[0].gameObject.SetActive(false);
            until=Time.realtimeSinceStartup+10;while(Time.realtimeSinceStartup<until)yield return null;
            displaced=map.Blocks.Where((b,i)=>b.gameObject.activeInHierarchy&&Vector3.Distance(b.position,positions[i])>.5f).Count();
            Check(displaced>0,"Removing one foundation corner causes gravity collapse; displaced="+displaced);
            Capture("collapse",new Vector3(118,102,-162),new Vector3(0,69,0),52);
            string saved=map.SaveLayout();int revision=map.Revision;
            Check(!map.LoadLayout(saved,map.Blocks.Count+1,new Pose(Vector3.zero,Quaternion.identity),Vector3.one)&&revision==map.Revision,"Invalid layout body count rejected without mutation");
            Check(map.LoadLayout(saved,156,new Pose(Vector3.zero,Quaternion.identity),Vector3.one),"Deterministic layout roundtrip and reset");
            Check(map.Blocks.Count==156&&map.Blocks.All(b=>b.gameObject.activeInHierarchy),"Reset restores every section");Check(errors==0,"No runtime errors");
        }
        static void Frames(string label,List<float> values)
        {
            var sorted=values.Skip(1).OrderBy(x=>x).ToArray();if(sorted.Length<2)return;
            File.AppendAllText(Report,$"{label}: frames={sorted.Length}, avg={1000/sorted.Average():F1}FPS, p95={sorted[(int)((sorted.Length-1)*.95f)]:F2}ms.\n");
        }
        static bool VaultQuery(CastleSurvivor motor,Vector3 feet,Vector3 forward,out Vector3 edge,out Vector3 landing)
        {
            bool enabled=motor.enabled;
            try
            {
                motor.enabled=true;motor.RefreshMode();motor.Teleport(feet,Quaternion.LookRotation(forward));motor.viewCamera.transform.localRotation=Quaternion.identity;motor.InputSuspended=false;
                // Seed an airborne query state; the natural walking/climbing tests run separately.
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                typeof(CastleSurvivor).GetProperty("Grounded")?.SetValue(motor,false);
                typeof(CastleSurvivor).GetField("airborneSince",flags)?.SetValue(motor,Time.time-1);
                typeof(CastleSurvivor).GetField("lastGrounded",flags)?.SetValue(motor,Time.time-1);
                typeof(CastleSurvivor).GetField("jumped",flags)?.SetValue(motor,true);
                Physics.SyncTransforms();return motor.TryGetVaultTarget(out _,out edge,out landing);
            }
            finally{motor.enabled=enabled;}
        }
        static void CheckVaultQueries(CastleSurvivor motor,MegablockTowerMap map)
        {
            float floor=MegablockTowerLayout.FloorTop(2);
            Check(Physics.Raycast(new Vector3(5.3f,floor+2,-14.3f),Vector3.down,out var sill,3,1<<Layers.Block,QueryTriggerInteraction.Ignore),"Window sill fixture finds the real collision surface");
            Check(VaultQuery(motor,new Vector3(5.3f,floor+.15f,-13.4f),Vector3.back,out var edge,out var landing),"Actual window accepts a seeded airborne vault query");
            Check(Mathf.Abs(edge.y-sill.point.y)<.08f&&Mathf.Abs(landing.y-sill.point.y-.04f)<.08f,"Window vault hands and landing match the sill rather than the whole assembly top");
            var root=new GameObject("Production legacy proxy fixture");root.transform.position=new Vector3(300,0,0);
            bool ignored=Physics.GetIgnoreLayerCollision(Layers.Survivor,Layers.Block);var surfacePolicy=new bool[32];
            for(int i=0;i<32;i++)surfacePolicy[i]=Physics.GetIgnoreLayerCollision(Layers.Surface,i);
            try
            {
                var legacy=root.AddComponent<CastleGenerator>();legacy.generateOnStart=false;legacy.environmentOnly=true;
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("CastleBlock.prefab"));var go=Object.Instantiate(prefab,root.transform);
                go.transform.localPosition=new Vector3(0,.55f,0);go.transform.localScale=new Vector3(6,1.1f,2);
                var block=go.GetComponent<CastleBlock>();block.Body.isKinematic=true;legacy.Bodies.Add(block.Body);
                var art=GameObject.CreatePrimitive(PrimitiveType.Cube);art.name="Legacy visual-height fixture";art.GetComponent<Collider>().enabled=false;
                art.transform.SetParent(block.Visual,false);art.transform.localPosition=Vector3.zero;art.transform.localScale=new Vector3(1,2,1);block.SyncSurface();Physics.SyncTransforms();
                Check(!TraversalCollision.Unified,"Production legacy generator retains proxy traversal");
                Check(VaultQuery(motor,new Vector3(300,.5f,-1.4f),Vector3.forward,out edge,out landing),"Legacy production block accepts the same seeded vault query");
                Check(Mathf.Abs(edge.y-landing.y+.04f-.55f)<.035f,"Legacy/proxy fixture retains its visual-top grip correction");
            }
            finally
            {
                root.SetActive(false);Object.Destroy(root);Services.World=map;
                Physics.IgnoreLayerCollision(Layers.Survivor,Layers.Block,ignored);
                for(int i=0;i<32;i++)Physics.IgnoreLayerCollision(Layers.Surface,i,surfacePolicy[i]);
            }
        }
        static void CheckCollisionLayout(MegablockTowerMap map)
        {
            bool geometry=true,routing=true;int rotatedChildren=0;
            for(int n=0;n<map.Blocks.Count;n++)
            {
                var body=map.Blocks[n];var member=body.GetComponent<MayhemStructuralMember>();var module=map.Authored[n];
                geometry&=member.Shapes.Count==module.shapes.Count;
                for(int s=0;s<member.Shapes.Count;s++)
                {
                    var shape=member.Shapes[s];routing&=Climbables.TryGet(shape,out var found)&&ReferenceEquals(found,member)&&ReferenceEquals(HitAbilities.Get<IPushable>(shape),member);
                    var expected=module.shapes[s];var inverse=Quaternion.Inverse(module.shapes[0].rotation);
                    if(shape is MeshCollider mesh)
                    {
                        var vertices=mesh.sharedMesh.vertices;geometry&=mesh.convex&&expected.vertices!=null&&vertices.Length==expected.vertices.Length;
                        if(expected.vertices!=null&&vertices.Length==expected.vertices.Length)
                            for(int v=0;v<vertices.Length;v++)geometry&=Vector3.Distance(body.transform.InverseTransformPoint(mesh.transform.TransformPoint(vertices[v])),inverse*(expected.center+expected.rotation*expected.vertices[v]))<.001f;
                        continue;
                    }
                    if(!(shape is BoxCollider box)){geometry=false;continue;}
                    var center=body.transform.InverseTransformPoint(box.transform.TransformPoint(box.center));
                    var rotation=Quaternion.Inverse(body.transform.rotation)*box.transform.rotation;
                    geometry&=Vector3.Distance(center,inverse*expected.center)<.001f&&Vector3.Distance(box.size,expected.size)<.001f&&Quaternion.Angle(rotation,inverse*expected.rotation)<.02f;
                    if(box.transform!=body.transform)rotatedChildren++;
                }
            }
            Check(geometry,"Every primitive and convex collider retains its authored geometry");
            Check(routing,"Every collider resolves the correct climbable and pushable owner");
            Check(rotatedChildren==60,"Only the 60 sloped ramp boxes require separate collider transforms");
            File.AppendAllText(Report,"Structural GameObjects including visuals="+map.Blocks.Sum(b=>b.GetComponentsInChildren<Transform>().Length)+"; colliders="+map.Authored.Sum(m=>m.shapes.Count)+". Collider count is unchanged by compaction.\n");
            var probe=new GameObject("Legacy builder compatibility probe");probe.transform.position=new Vector3(500,20,500);
            try
            {
                var rb=probe.AddComponent<Rigidbody>();rb.isKinematic=true;var old=probe.AddComponent<MayhemStructuralMember>();
                old.Build(map.Authored[0],map.palette,map.friction);
                Check(probe.GetComponents<Collider>().Length==1&&old.Shapes.Count==map.Authored[0].shapes.Count,"Existing three-argument builder keeps its legacy collider hierarchy");
            }
            finally{probe.SetActive(false);Object.Destroy(probe);}
        }
        static void Set(Component component,string field,object value)=>component?.GetType().GetField(field)?.SetValue(component,value);
        static void Capture(string name,Vector3 position,Vector3 target,float fov)
        {
            var go=new GameObject("Tower evidence camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.farClipPlane=1200;camera.nearClipPlane=.08f;camera.fieldOfView=fov;camera.clearFlags=CameraClearFlags.Skybox;
            camera.transform.position=position;camera.transform.LookAt(target);
            var rt=RenderTexture.GetTemporary(1600,1100,24);var old=RenderTexture.active;var texture=new Texture2D(1600,1100,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1600,1100),0,0);texture.Apply();File.WriteAllBytes(ProjectPath.Report("MegablockTower-"+name+".png"),texture.EncodeToPNG());}
            finally{RenderTexture.active=old;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Object.Destroy(texture);Object.Destroy(go);}
        }
        static void InstallPreview()
        {
            string folder=ProjectPath.FolderOf("epic-three-hearts.png").TrimEnd('/');string path=folder+"/"+MegablockTowerBuild.Id+".png";
            File.WriteAllBytes(path,File.ReadAllBytes(ProjectPath.Report("MegablockTower-exterior.png")));AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer){importer.maxTextureSize=1024;importer.mipmapEnabled=false;importer.SaveAndReimport();}
        }
    }
}
