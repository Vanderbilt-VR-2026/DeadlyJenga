using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.World.Hazards;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CandyArt;
using DeadlyJenga.Presentation;
using System.Reflection;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Explicit request only. Measures the real Game scene and restores the user's setup.
    [InitializeOnLoad] public static class DenseWavePerformance
    {
        const string Key = "DenseWavePerformance";
        [Serializable] sealed class Scenes { public SceneSetup[] scenes; }
        static string Report => ProjectPath.Report("DenseWave-" + SessionState.GetString(Key+"Label", "run") + ".txt");
        static int errors;
        static DenseWavePerformance()
        {
            EditorApplication.update += () => {
                var request = ProjectPath.Report("DenseWave.request");
                if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)) return;
                var label = File.ReadAllText(request).Trim(); File.Delete(request);
                try { Run(label); } catch (Exception e) { Debug.LogException(e); }
            };
            EditorApplication.playModeStateChanged += state => {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode) {
                    errors=0; Application.logMessageReceived += Log;
                    EditorApplication.delayCall += () => Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Guard());
                }
                if (state == PlayModeStateChange.EnteredEditMode) {
                    Application.logMessageReceived -= Log; SessionState.SetBool(Key, false);
                    SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices", "")));
                    MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby", false); MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local", false);
                    EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes", "")).scenes);
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Performance/Profile Dense Blocks Bots and Tsunami")]
        public static void RunMenu() => Run("manual");
        static void Run(string label)
        {
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if (scenes.Any(s=>string.IsNullOrEmpty(s.path)||s.isLoaded&&UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))
                throw new Exception("Save edited scenes before profiling.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{scenes=scenes}));
            SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby); SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);
            SessionState.SetString(Key+"Label",label); SessionState.SetBool(Key,true);
            MatchSetup.FromLobby=false; MatchSetup.StartLocalSession=false;
            string scene=label.Contains("dense") ? "MapDrafterEvan1.unity" : "Game.unity";
            File.WriteAllText(Report,"Actual "+scene+"; castle; 0/32 bots; identical seeded tsunami. Editor CPU timing, not GPU/device certification.\n");
            EditorSceneManager.OpenScene(ProjectPath.Find(scene)); EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType kind)
        { if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n");} }
        static IEnumerator Guard()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Check());
            while(stack.Count>0) {
                bool next=false;object current=null;Exception error=null;
                try { next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current; } catch(Exception e){error=e;}
                if(error!=null){File.AppendAllText(Report,"FAIL "+error+"\n");break;}
                if(!next){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(current is IEnumerator child){stack.Push(child);continue;}yield return current;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            File.AppendAllText(Report,"COMPLETE runtimeErrors="+errors+"\n"); EditorApplication.ExitPlaymode();
        }
        static IEnumerator Check()
        {
            float until=Time.realtimeSinceStartup+40;
            while(!(Services.Session is CastlePlaySession)&&Time.realtimeSinceStartup<until)yield return null;
            var session=Services.Session as CastlePlaySession;
            if(!session)throw new Exception("Game session unavailable.");
            session.SurvivorBots?.Clear();session.HandBots?.Clear();if(session.Social!=null)session.Social.VoiceEnabled=false;
            if(!SessionState.GetString(Key+"Label", "").Contains("dense")) { MatchSetup.ModeId="demo";MatchSetup.MapId="mode-castle-gaps";MatchSetup.FromLobby=false;MatchSetup.ApplyMap(); }
            yield return null;
            int vsync=QualitySettings.vSyncCount,rate=Application.targetFrameRate;float scale=Time.timeScale;
            var random=UnityEngine.Random.state;
            QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;Time.timeScale=1;
            try {
                if(SessionState.GetString(Key+"Label", "")=="runtime") {
                    session.ResetMap();yield return new WaitForSeconds(2);session.SurvivorBots.SpawnOne();
                    yield return RuntimeChecks(session);yield break;
                }
                bool physicsComparison=SessionState.GetString(Key+"Label", "").Contains("grouping");
                if(physicsComparison) {
                    var map=session.Generator();var label=SessionState.GetString(Key+"Label", "");
                    map.groupRestingBlocks=!label.Contains("reference") && !label.Contains("native");
                    map.sinkDisplacedDebris=map.shatterHeavyImpacts=label.Contains("cleanup");
                    if(label.Contains("contracts"))map.physicsGroupThreshold=1;
                }
                if(physicsComparison && SessionState.GetString(Key+"Label", "").Contains("contracts")) {yield return GroupChecks(session);yield break;}
                for(int phase=0;phase<4;phase++) {
                    if(physicsComparison && phase!=0 && phase!=3)continue;
                    session.SurvivorBots.Clear();session.HandBots.Clear();session.Demo.Run(DemoCommand.StopDisasters);
                    UnityEngine.Random.InitState(71293);session.ResetMap();yield return new WaitForSeconds(physicsComparison?8:2);
                    var camera=session.ActiveCamera;var bounds=Services.World.PlayableBounds;
                    session.SetMenu(true);camera.transform.position=bounds.center+new Vector3(bounds.size.x*.8f,bounds.size.y*.7f,-bounds.size.z);camera.transform.LookAt(bounds.center);
                    if(phase>=2){for(int i=0;i<32;i++)session.SurvivorBots.SpawnOne();}
                    yield return new WaitForSeconds(2);
                    TsunamiHazard wave=null;
                    if(phase%2==1) {
                        wave=Object.Instantiate(Resources.Load<GameObject>("DemoDisasters/Tsunami")).GetComponent<TsunamiHazard>();
                        wave.randomDirection=false;wave.approachHeading=0;wave.warningTime=0;wave.offshoreDistance=0;wave.riseTime=.1f;wave.Begin();
                        yield return new WaitForSeconds(SessionState.GetString(Key+"Label", "").Contains("late")?8:1);
                    }
                    File.AppendAllText(Report,"PHASE "+phase+" blocks="+Services.World.Blocks.Count+" bots="+session.SurvivorBots.Count+" wave="+(wave&&wave.Active)+"\n");
                    yield return Sample(phase);
                    if(wave){wave.End();Object.Destroy(wave.gameObject);}
                }
                if(!physicsComparison)yield return RuntimeChecks(session);
                else if(!SessionState.GetString(Key+"Label", "").Contains("reference")) yield return GroupChecks(session);
            } finally { QualitySettings.vSyncCount=vsync;Application.targetFrameRate=rate;Time.timeScale=scale;UnityEngine.Random.state=random; }
        }
        static void Require(bool pass,string message)
        {if(!pass)throw new Exception(message);File.AppendAllText(Report,"PASS "+message+"\n");}
        static IEnumerator GroupChecks(CastlePlaySession session)
        {
            session.SurvivorBots.Clear();session.HandBots.Clear();
            session.Demo.Run(DemoCommand.StopDisasters);session.ResetMap();yield return new WaitForSeconds(8);
            var map=session.Generator(); var groups=map.PhysicsGroups;
            Require(HitAbilities.Get<IPushable>(null)==null,"Hit lookup accepts a missing collider");
            var ordinary=map.Bodies.First(b=>b && b.gameObject.activeSelf);
            Require(HitAbilities.Get<Rigidbody>(ordinary.GetComponent<Collider>())==ordinary && HitAbilities.Get<IPushable>(ordinary.GetComponent<Collider>())==ordinary.GetComponent<IPushable>(),"Ordinary collider lookup retains original abilities and body");
            SharedHitCheck(map);
            Physics.SyncTransforms();
            var live=map.Bodies.Where(b=>b && b.gameObject.activeSelf).Take(64).ToArray();
            var proxies=new List<Collider>();
            bool exactOwners=true;
            foreach(var original in live)
            {
                var found=Physics.OverlapBox(original.position,Vector3.one*.1f,Quaternion.identity,1<<Layers.Surface,QueryTriggerInteraction.Ignore)
                    .FirstOrDefault(c=>Climbables.TryGet(c,out var owner) && owner.Body==original);
                exactOwners &= found && found.attachedRigidbody && HitAbilities.Get<Rigidbody>(found)==original && ReferenceEquals(HitAbilities.Get<IPushable>(found),original.GetComponent<IPushable>());
                proxies.Add(found);
            }
            Require(exactOwners,"Survivor colliders keep their exact original block owners");
            Require(proxies.Select(c=>c.attachedRigidbody).Distinct().Count()<proxies.Count/2,"Survivor colliders use fewer shared kinematic bodies");
            if(map.Debris)Require(map.Debris.SunkCount==0 && map.Debris.ShatteredCount==0 && map.Debris.SinkingCount==0,"Original tower survives settling without sinking or shattering");
            if(!map.groupRestingBlocks || map.Bodies.Count<map.physicsGroupThreshold)
            {
                Require(!groups,"Native setting keeps independent block physics");yield return ProxyCheck(map);if(map.Debris)yield return DebrisChecks(map);
                if(map.Bodies.Count>=map.physicsGroupThreshold) {map.groupRestingBlocks=true;map.sinkDisplacedDebris=map.shatterHeavyImpacts=false;yield return GroupChecks(session);}
                Require(EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"),"Map/disaster authoring validation runs");yield break;
            }
            Require(groups && groups.DormantBodies>map.Bodies.Count/2,"Naturally settled test map groups most blocks");
            if(map.Debris)Require(map.Debris.SunkCount==0 && map.Debris.ShatteredCount==0 && map.Debris.SinkingCount==0,"Original tower survives settling without sinking or shattering");
            var identities=map.Bodies.ToArray();
            var block=map.Bodies.Where(b=>groups.IsDormant(b) && b.position.y>map.TopHeight-4).OrderByDescending(b=>(b.position-map.Center).sqrMagnitude).First().GetComponent<CastleBlock>();
            Require(block.CanClimb && block.Body==map.Bodies.First(b=>b==block.Body),"Dormant blocks retain original body identity and climbing ability");
            Require(block.Body.GetComponent<Collider>().bounds.size.sqrMagnitude>1,"Dormant original colliders retain bounds for world queries");
            Physics.SyncTransforms();
            var surfaces=Physics.OverlapBox(block.Body.position,Vector3.one*.1f,Quaternion.identity,1<<Layers.Surface,QueryTriggerInteraction.Ignore);
            Require(surfaces.Any(c=>Climbables.TryGet(c,out var climbable) && climbable.Body==block.Body),"Shared survivor surfaces resolve to the original climbable block");
            var shared=surfaces.First(c=>Climbables.TryGet(c,out var owner) && owner.Body==block.Body);
            Require(HitAbilities.Get<Rigidbody>(shared)==block.Body && ReferenceEquals(HitAbilities.Get<IPushable>(shared),block) && ReferenceEquals(HitAbilities.Get<IHazardMotion>(shared),block),"Shared collider resolves original body, push and hazard motion");
            var position=block.Body.position;
            var outward=Vector3.ProjectOnPlane(position-map.Center,Vector3.up).normalized;
            block.Push((outward.sqrMagnitude>.1f?outward:Vector3.right)*8,null);
            Require(!block.Body.isKinematic,"Push releases the original block before applying force");
            yield return new WaitForSeconds(.2f);
            Require((block.Body.position-position).sqrMagnitude>.001f,"Released original block moves under independent physics");
            yield return ProxyCheck(map);
            map.SetReplica(true);
            Require(map.Bodies.All(b=>b.isKinematic),"Replica suspends every original block for authoritative pose playback");
            yield return new WaitForSeconds(.1f);
            map.SetReplica(false);
            Require(map.Bodies.All(b=>!b.isKinematic),"Authority recovery restores every original dynamic body");
            Require(map.Bodies.SequenceEqual(identities),"Dormancy, push and authority changes preserve network block indices");
            Require(map.Bodies.All(b=>b.collisionDetectionMode==map.blockPrefab.GetComponent<Rigidbody>().collisionDetectionMode),"Released bodies restore original collision detection");
            if(map.Debris)yield return DebrisChecks(map);
            Require(EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"),"Map/disaster authoring validation runs");
        }
        static void SharedHitCheck(CastleGenerator map)
        {
            var owners=map.Bodies.Where(b=>b && b.gameObject.activeSelf).Take(2).Select(b=>b.GetComponent<CastleBlock>()).ToArray();
            var root=new GameObject("Temporary shared-collider contract fixture");root.SetActive(false);
            var first=root.AddComponent<BoxCollider>();var second=root.AddComponent<BoxCollider>();
            var fallback=root.AddComponent<Rigidbody>();fallback.isKinematic=true;
            var plain=root.AddComponent<BoxCollider>();
            first.enabled=second.enabled=plain.enabled=false;root.SetActive(true);
            Climbables.Register(first,owners[0]);Climbables.Register(second,owners[1]);
            try
            {
                Require(HitAbilities.Get<Rigidbody>(first)==owners[0].Body && HitAbilities.Get<Rigidbody>(second)==owners[1].Body && ReferenceEquals(HitAbilities.Get<IPushable>(first),owners[0]) && ReferenceEquals(HitAbilities.Get<IPushable>(second),owners[1]),"Shared-object contract fixture resolves distinct original owners for each collider");
                Require(HitAbilities.Get<Rigidbody>(plain)==fallback,"An unregistered collider uses its ordinary parent component");
            }
            finally {Climbables.Unregister(first);Climbables.Unregister(second);Object.Destroy(root);}
        }
        static IEnumerator ProxyCheck(CastleGenerator map)
        {
            var body=map.Bodies.Where(b=>b && b.gameObject.activeSelf).OrderByDescending(b=>b.position.y).First();
            map.PhysicsGroups?.Disturb(body);
            var position=body.position;var rotation=body.rotation;var mode=body.collisionDetectionMode;
            body.collisionDetectionMode=CollisionDetectionMode.Discrete;body.isKinematic=true;
            try
            {
                body.position+=Vector3.up*3;body.rotation=Quaternion.Euler(12,24,8)*rotation;
                body.transform.SetPositionAndRotation(body.position,body.rotation);
                yield return null;Physics.SyncTransforms();
                var surface=Physics.OverlapBox(body.position,Vector3.one*.1f,Quaternion.identity,1<<Layers.Surface,QueryTriggerInteraction.Ignore)
                    .FirstOrDefault(c=>HitAbilities.Get<Rigidbody>(c)==body);
                var original=body.GetComponent<Collider>();
                Require(surface && (surface.bounds.center-original.bounds.center).sqrMagnitude<.000001f && (surface.bounds.size-original.bounds.size).sqrMagnitude<.000001f,"Shared survivor collider follows actual block translation and rotation before motor queries");
            }
            finally
            {
                body.position=position;body.rotation=rotation;body.transform.SetPositionAndRotation(position,rotation);
                body.isKinematic=false;body.collisionDetectionMode=mode;body.GetComponent<CastleBlock>().SyncSurface();
            }
        }
        static IEnumerator DebrisChecks(CastleGenerator map)
        {
            var debris=map.Debris;
            var floors=new List<GameObject>();
            for(int i=0;i<4;i++)
            {var floor=new GameObject("Temporary debris test floor");floor.transform.position=new Vector3(i==3?1200:1000+i*20,-.5f,0);floor.AddComponent<BoxCollider>().size=new Vector3(12,1,12);floors.Add(floor);}
            var targets=map.Bodies.Where(b=>b && b.gameObject.activeSelf && b.position.y>10).Take(4).ToArray();
            try {
                debris.SinkDebris=false;
                for(int i=0;i<4;i++) {map.PhysicsGroups?.Disturb(targets[i]);targets[i].isKinematic=false;targets[i].position=new Vector3(i==3?1200:1000+i*20,i==0||i==3?12:i==2?30:3,0);targets[i].rotation=Quaternion.identity;targets[i].linearVelocity=i==0?Vector3.down*25:Vector3.zero;targets[i].angularVelocity=Vector3.zero;}
                debris.VisualFastDebris=false;yield return new WaitForSeconds(.6f);
                Require(!targets[0].gameObject.activeSelf && debris.ShatteredCount>0,"A severe actual rigidbody impact shatters and retires the block");
                debris.VisualFastDebris=true;
                targets[3].angularVelocity=Vector3.up*40;targets[3].GetComponent<CastleBlock>().Push(Vector3.down*30,null);
                Require(debris.IsVisual(targets[3]),"High-speed downward debris enters swept ballistic simulation");
                var fast=targets[2].GetComponent<CastleBlock>();fast.Push(Vector3.right*30,null);
                Require(debris.IsVisual(targets[2]) && targets[2].isKinematic && targets[2].GetComponent<Collider>().isTrigger && !fast.CanClimb,"Forecast high-speed debris switches to trigger-only visual motion before applying its push");
                var started=targets[2].position;yield return null;
                Require((targets[2].position-started).sqrMagnitude>.01f,"Visual debris carries its momentum without a dynamic collision body");
                var camera=Services.Session.ActiveCamera;var view=new Pose(camera.transform.position,camera.transform.rotation);
                try {camera.transform.position=targets[2].position+Vector3.up*5;yield return null;Require(!debris.IsVisual(targets[2]) && targets[2].detectCollisions && !targets[2].isKinematic && fast.CanClimb,"Approaching observer restores native collision, climbing and motion");}
                finally {camera.transform.SetPositionAndRotation(view.position,view.rotation);}
                targets[2].position=new Vector3(1040,3,0);targets[2].linearVelocity=targets[2].angularVelocity=Vector3.zero;
                yield return new WaitForSeconds(.9f);
                Require(!targets[3].gameObject.activeSelf && debris.ShatteredCount>=2,"Swept ballistic contact shatters and retires its original block");
                Require(targets[1].gameObject.activeSelf && targets[2].gameObject.activeSelf,"Ordinary short drops do not shatter blocks");
                debris.SinkDebris=true;
                yield return new WaitForSeconds(4);
                Require(!targets[1].gameObject.activeSelf && !targets[2].gameObject.activeSelf && debris.SunkCount>=2,"Displaced debris visibly sinks through ground and deactivates after burial");
                Require(map.Bodies.Contains(targets[0]) && map.Bodies.Contains(targets[1]),"Shattered and buried blocks keep their original network indices");
            } finally {foreach(var floor in floors)Object.Destroy(floor);}
        }
        static IEnumerator RuntimeChecks(CastlePlaySession session)
        {
            var brain=session.Bots().Bots.First(b=>b&&!b.Respawning);
            var rig=brain.GetComponent<CandyPlayerAvatar>().Rig;
            var detail=rig.GetComponent<CandyAvatarDetail>();var camera=session.ActiveCamera;
            Require(detail,"Actual population avatars have a detail controller");
            // Isolate the LOD test from secondary spectator/preview cameras without destroying them.
            var others=Camera.allCameras.Where(c=>c!=camera&&c.enabled).ToArray();foreach(var c in others)c.enabled=false;
            var pose=new Pose(camera.transform.position,camera.transform.rotation);
            try {
                camera.transform.position=rig.transform.position+Vector3.up*.5f;yield return null;detail.Refresh();
                var full=rig.bodyRenderer.sharedMesh;Require(detail.Detail==0,"Near external avatar retains full detail");
                camera.transform.position=rig.transform.position+Vector3.up*100;yield return null;detail.Refresh();
                Require(detail.Detail==2&&rig.bodyRenderer.sharedMesh.vertexCount<full.vertexCount/2,"Distant actual avatar selects the shared reduced mesh");
                var second=new GameObject("Temporary second observer").AddComponent<Camera>();second.transform.position=rig.transform.position+Vector3.up*.5f;
                yield return null;detail.Refresh();Require(detail.Detail==0,"A nearby secondary observer preserves full detail for split-screen");Object.Destroy(second.gameObject);
                yield return null;
                rig.ownerCamera=camera;detail.Refresh();Require(detail.Detail==0&&rig.bodyRenderer.sharedMesh==full,"Local owner keeps original first-person mesh at every distance");rig.ownerCamera=null;
            } finally {camera.transform.SetPositionAndRotation(pose.position,pose.rotation);foreach(var c in others)if(c)c.enabled=true;}
            var block=Services.World.Blocks.First(b=>b&&b.gameObject.activeInHierarchy&&b.GetComponentInChildren<CandyCrateVisual>()?.isActiveAndEnabled==true);
            var visual=block.GetComponentInChildren<CandyCrateVisual>();camera.transform.position=block.position+Vector3.up*250;visual.Refresh();
            yield return new WaitForSeconds(.6f);
            var batch=(CandyCrateBatchRenderer)typeof(CandyCrateBatchRenderer).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            Require(batch,"Crate batch manager is live (hidden runtime object)");
            var entries=(System.Collections.IDictionary)typeof(CandyCrateBatchRenderer).GetField("visuals",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(batch);
            var entry=entries[visual];Require(entry!=null,"Active block artwork is registered for instancing");var field=entry.GetType().GetField("matrix");
            block.isKinematic=true;block.position+=new Vector3(2,3,1);block.rotation=Quaternion.Euler(12,35,18);
            yield return new WaitForEndOfFrame();
            var actual=(Matrix4x4)field.GetValue(entry);var expected=visual.GetComponent<Renderer>().localToWorldMatrix;
            Require(Enumerable.Range(0,16).All(i=>Mathf.Abs(actual[i]-expected[i])<.001f),"Cached instancing matrix follows translation and rotation of an actual physics block");
            block.transform.localScale*=1.1f;yield return new WaitForEndOfFrame();
            actual=(Matrix4x4)field.GetValue(entry);expected=visual.GetComponent<Renderer>().localToWorldMatrix;
            Require(Enumerable.Range(0,16).All(i=>Mathf.Abs(actual[i]-expected[i])<.001f),"Cached instancing matrix follows scale changes");
            camera.transform.SetPositionAndRotation(pose.position,pose.rotation);
            CandyMelt.Spawn(rig.transform,Color.red);
            var melt=Object.FindObjectsByType<CandyMelt>(FindObjectsSortMode.None).OrderBy(m=>m.Age).First();
            Require(melt.SkinnedSnapshotVertices>0&&melt.RagdollConstraintCount>30,"Death snapshot retains real skinned geometry and articulated skeleton");
            var born=melt.SnapshotBounds;
            Require((born.size-melt.CurrentBodyBounds.size).sqrMagnitude<.000001f,"Ragdoll begins at the original avatar size");
            yield return new WaitForSecondsRealtime(.5f);
            Require(melt&&melt.MeltProgress==0&&float.IsFinite(melt.CurrentBodyBounds.center.x)&&float.IsFinite(melt.CurrentBodyBounds.size.y),"Optimized deformation keeps a finite full-size ragdoll before melting");
            float deadline=Time.realtimeSinceStartup+16;bool liquefied=false;
            while(melt&&Time.realtimeSinceStartup<deadline){liquefied|=melt.MeltProgress>=1;yield return null;}
            Require(liquefied&&!melt,"Ragdoll melts into its puddle, dries and releases itself");
        }
        static IEnumerator Sample(int phase)
        {
            var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);
            var counters=new List<(string name,ProfilerRecorder recorder)>();
            foreach(var handle in handles) {
                var d=ProfilerRecorderHandle.GetDescription(handle);
                if(d.Name=="Physics.Simulate"||d.Name=="Physics.SyncTransforms"||d.Name=="BehaviourUpdate"||d.Name=="BehaviourFixedUpdate"||d.Name=="Main Thread"||d.Name=="Render Thread"||d.Name=="GC Allocated In Frame"||d.Name.StartsWith("DJ."))
                    counters.Add((d.Name,ProfilerRecorder.StartNew(d.Category,d.Name,256)));
            }
            var frames=new List<float>();long batches=0,triangles=0;
            try {
                bool physicsComparison=SessionState.GetString(Key+"Label", "").Contains("grouping");
                bool late=SessionState.GetString(Key+"Label", "").Contains("late");
                float end=Time.realtimeSinceStartup+(late?180:physicsComparison?20:6),simulationEnd=Time.time+8;
                while(Time.realtimeSinceStartup<end && (!late || Time.time<simulationEnd) && frames.Count<240){yield return null;frames.Add(Time.unscaledDeltaTime*1000);batches+=UnityStats.batches;triangles+=UnityStats.triangles;}
                frames.Sort();int n=frames.Count;
                File.AppendAllText(Report,"median="+frames[n/2].ToString("F2")+"ms p90="+frames[Mathf.Min(n-1,(int)(n*.9f))].ToString("F2")+"ms frames="+n+" batches="+batches/n+" triangles="+triangles/n+" awake="+Services.World.Blocks.Count(b=>b&&!b.IsSleeping())+"\n");
                var groups=Object.FindFirstObjectByType<CastlePhysicsGroups>();
                if(groups)File.AppendAllText(Report,"  dormant="+groups.DormantBodies+" released="+groups.ReleasedBodies+"\n");
                var debris=Object.FindFirstObjectByType<CastleDebris>();
                if(debris)File.AppendAllText(Report,"  sinking="+debris.SinkingCount+" sunk="+debris.SunkCount+" shattered="+debris.ShatteredCount+" visual="+debris.VisualCount+" converted="+debris.ConvertedCount+" restored="+debris.RestoredCount+" active="+Services.World.Blocks.Count(b=>b && b.gameObject.activeSelf)+"\n");
                foreach(var c in counters){var samples=new List<ProfilerRecorderSample>();c.recorder.CopyTo(samples);if(samples.Count>0)File.AppendAllText(Report,"  "+c.name+" mean="+samples.Average(s=>(double)s.Value).ToString("F0")+(c.name=="GC Allocated In Frame"?" bytes":" ns")+"\n");}
            } finally {foreach(var c in counters)c.recorder.Dispose();}
        }
    }
}
