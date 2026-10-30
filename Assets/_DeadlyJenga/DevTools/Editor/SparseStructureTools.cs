using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using Debug=UnityEngine.Debug;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class SparseStructureTools
    {
        const string Key="SparseStructures";
        static IEnumerator runtime;
        static int runtimeErrors;
        static bool profileCleanup=true;
        static readonly List<(string name,ProfilerRecorder recorder)> counters=new();
        static void BeginCounters()
        {
            var available=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(available);
            foreach(var handle in available)
            {
                var d=ProfilerRecorderHandle.GetDescription(handle);
                if(d.Name=="Physics.Simulate"||d.Name=="Physics.SyncTransforms"||d.Name=="Main Thread"||d.Name=="Render Thread"||d.Name=="GC Allocated In Frame"||d.Name.StartsWith("DJ."))counters.Add((d.Name,ProfilerRecorder.StartNew(d.Category,d.Name,1024)));
            }
        }
        static void EndCounters(StringBuilder report)
        {
            foreach(var c in counters)
            {
                var samples=new List<ProfilerRecorderSample>();c.recorder.CopyTo(samples);
                if(samples.Count>0)report.AppendLine("CPU "+c.name+" mean="+(samples.Average(x=>(double)x.Value)/(c.name=="GC Allocated In Frame"?1:1000000)).ToString("F3")+(c.name=="GC Allocated In Frame"?" bytes":" ms/frame (nested)") );
                c.recorder.Dispose();
            }
            counters.Clear();
        }
        [Serializable] sealed class SavedScenes {public SceneSetup[] scenes;}
        static SparseStructureTools()
        {
            EditorApplication.update+=Poll;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode){runtimeErrors=0;Application.logMessageReceived+=Log;runtime=RunPhysics();EditorApplication.update+=Tick;}
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    EditorApplication.update-=Tick;Application.logMessageReceived-=Log;runtime=null;SessionState.SetBool(Key,false);
                    EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key+"Scenes","")).scenes);
                }
            };
        }
        static void Log(string message,string trace,LogType kind)
        {if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert){runtimeErrors++;File.AppendAllText(ProjectPath.Report("SparseStructures-"+SessionState.GetString(Key+"Phase","")+".txt"),"UNITY ERROR "+message+"\n");}}
        static void Tick()
        {
            if(runtime==null||EditorApplication.isCompiling)return;
            try{if(!runtime.MoveNext()){runtime=null;EditorApplication.update-=Tick;EditorApplication.ExitPlaymode();}}
            catch(Exception e){File.AppendAllText(ProjectPath.Report("SparseStructures-"+SessionState.GetString(Key+"Phase","")+".txt"),"RUNTIME FAIL "+e+"\n");runtime=null;EditorApplication.update-=Tick;EditorApplication.ExitPlaymode();}
        }
        static IEnumerable<SparseStructureKind> Kinds(string phase)=>Enumerable.Range(0,13).Select(i=>(SparseStructureKind)i).Where(k=>phase=="all"||phase=="moderate"&&(int)k<4||phase=="grand"&&(int)k>=4&&(int)k<8||phase=="hybrid"&&k==SparseStructureKind.Festival||phase=="city"&&k==SparseStructureKind.FestivalCity||phase=="city-shapes"&&(k==SparseStructureKind.FestivalCityPacked||k==SparseStructureKind.FestivalCitySurfaceMesh)||(phase.StartsWith("city-profile")||phase=="city-final-bench")&&k==SparseStructureKind.FestivalCityOptimized||phase=="city-runtime"&&k==SparseStructureKind.FestivalCityOptimized);
        static void Poll()
        {
            string integration=ProjectPath.Report("SparseMapIntegration.request");
            if(File.Exists(integration)&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {File.Delete(integration);CheckInstalledMaps();}
            string navigation=ProjectPath.Report("CityNavigation.request");
            if(File.Exists(navigation)&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {File.Delete(navigation);CheckCityNavigation();}
            string request=ProjectPath.Report("SparseStructures.request");
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            string phase=File.ReadAllText(request).Trim();File.Delete(request);
            try {BuildAndCheck(phase);}catch(Exception e){File.AppendAllText(ProjectPath.Report("SparseStructures-"+phase+".txt"),"FAIL "+e+"\n");Debug.LogException(e);}
        }
        [MenuItem("Tools/Deadly Jenga/Maps/Build and Check Sparse Structure Studies")]
        public static void BuildStudies()=>BuildAndCheck("moderate");
        [MenuItem("Tools/Deadly Jenga/Maps/Verify Installed Structure Maps")]
        public static void CheckInstalledMaps()
        {
            var report=new StringBuilder("Installed structure map integration: actual Unity asset/catalog loading.\n");
            int failures=0;
            void Check(bool pass,string label){report.AppendLine((pass?"PASS ":"FAIL ")+label);if(!pass)failures++;}
            try
            {
                var catalog=Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");
                Check(catalog,"lobby catalog loads from runtime Resources");
                for(int i=0;i<13;i++)
                {
                    var kind=(SparseStructureKind)i;string family="sparse-"+SparseStructureLayout.Id(kind);
                    var prefab=Resources.Load<GameObject>("ModeMaps/Mode_"+family);
                    var entry=catalog?Array.Find(catalog.maps,m=>m!=null&&m.id==family):null;
                    var map=prefab?prefab.GetComponent<SparseStructureMap>():null;
                    Check(prefab&&map&&map.kind==kind&&map.blockPrefab&&map.generateOnStart,family+" prefab and generator dependencies load");
                    Check(entry!=null&&entry.mapPrefab==prefab&&AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.scenePath),family+" selectable catalog entry targets this prefab and a valid game scene");
                    Check(entry!=null&&entry.SupportsMode("demo"),family+" available in Sandbox / Demo");
                }
                string lobby=ProjectPath.Find("Lobby.unity");
                Check(catalog&&File.ReadAllText(lobby).Contains(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(catalog))),"Lobby scene references the verified catalog");
            }
            catch(Exception e){failures++;report.AppendLine("FAIL "+e);}
            report.AppendLine("COMPLETE failures="+failures+". No scenes or prefabs modified.");
            File.WriteAllText(ProjectPath.Report("SparseMapIntegration.txt"),report.ToString());Debug.Log(report.ToString());
        }
        [MenuItem("Tools/Deadly Jenga/Maps/Build and Check Grand Sparse Structures")]
        public static void BuildGrand()=>BuildAndCheck("grand");
        [MenuItem("Tools/Deadly Jenga/Maps/Build and Check Festival Hybrid")]
        public static void BuildHybrid()=>BuildAndCheck("hybrid");
        [MenuItem("Tools/Deadly Jenga/Maps/Build and Check Festival City")]
        public static void BuildCity()=>BuildAndCheck("city");
        [MenuItem("Tools/Deadly Jenga/Maps/Build and Compare City Shape Copies")]
        public static void BuildCityShapes()=>BuildAndCheck("city-shapes");
        [MenuItem("Tools/Deadly Jenga/Maps/Build and Validate Optimized City")]
        public static void BuildOptimizedCity()=>BuildAndCheck("city-runtime");
        public static void BuildAndCheck(string phase)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before authoring maps.");
            var kinds=Kinds(phase).ToArray();
            if(kinds.Length==0)throw new ArgumentException("Use moderate, grand, hybrid, or all.");
            var report=new StringBuilder("Sparse structural maps · "+phase+"\nReal Unity isolated rigidbody simulation; not GPU/FPS, bot, headset or live network certification.\n");
            string maps=Path.GetDirectoryName(ProjectPath.FolderOf("SparseStructureLayout.cs").TrimEnd('/')).Replace('\\','/');
            string output=Folder(Folder(maps,"Resources"),"ModeMaps");
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("CastleBlock.prefab"));
            if(!source)throw new InvalidOperationException("Physical block prefab not found.");
            var previous=Services.World;int failures=0;
            foreach(var kind in kinds)
            {
                string id=SparseStructureLayout.Id(kind);var layout=SparseStructureLayout.Build(kind);
                void Check(bool pass,string label){report.Append(pass?"PASS ":"FAIL ").Append(id).Append(": ").AppendLine(label);if(!pass)failures++;}
                var bounds=SparseStructureLayout.Bounds(layout);
                report.AppendLine($"MAP {id}: {layout.Count} bodies; {bounds.size.x:F1} x {bounds.size.z:F1}m; height {bounds.max.y:F1}m; gravity only, no bonds or joints.");
                Check(layout.Count>20&&layout.Count<=1000,"explicit map body budget <=1000");
                Check(layout.All(p=>Finite(p.Position)&&Finite(p.Size)&&p.Size.x>0&&p.Size.y>0&&p.Size.z>0),"finite positive geometry");
                var twice=SparseStructureLayout.Build(kind);
                Check(layout.Count==twice.Count&&layout.Select((p,i)=>p.Position==twice[i].Position&&p.Size==twice[i].Size&&p.Rotation==twice[i].Rotation).All(v=>v),"deterministic stable member order");
                var overlaps=new List<string>();var unsupported=new List<int>();
                for(int i=0;i<layout.Count;i++)
                {
                    var a=layout[i].Bounds;var contacts=new List<Vector2>();
                    for(int j=0;j<layout.Count;j++)if(i!=j)
                    {
                        var c=layout[j].Bounds;float ox=Mathf.Min(a.max.x,c.max.x)-Mathf.Max(a.min.x,c.min.x),oz=Mathf.Min(a.max.z,c.max.z)-Mathf.Max(a.min.z,c.min.z),oy=Mathf.Min(a.max.y,c.max.y)-Mathf.Max(a.min.y,c.min.y);
                        if(!SparseStructureLayout.IsCity(kind)&&i<j&&ox>.001f&&oy>.001f&&oz>.001f&&Penetrates(layout[i],layout[j])&&overlaps.Count<12)overlaps.Add(i+" "+layout[i].Role+" / "+j+" "+layout[j].Role);
                        if(Mathf.Abs(a.min.y-c.max.y)<.001f&&ox>.001f&&oz>.001f)
                        {
                            float x0=Mathf.Max(a.min.x,c.min.x),x1=Mathf.Min(a.max.x,c.max.x),z0=Mathf.Max(a.min.z,c.min.z),z1=Mathf.Min(a.max.z,c.max.z);
                            contacts.Add(new Vector2(x0,z0));contacts.Add(new Vector2(x0,z1));contacts.Add(new Vector2(x1,z0));contacts.Add(new Vector2(x1,z1));
                        }
                    }
                    if(!SparseStructureLayout.IsCity(kind)&&a.min.y>.001f&&!Inside(contacts,new Vector2(a.center.x,a.center.z)))unsupported.Add(i);
                }
                if(SparseStructureLayout.IsCity(kind))report.AppendLine("NOTE city compound overlap/bearing checks are deferred to real convex-collider runtime validation.");
                else
                {
                    Check(overlaps.Count==0,"no penetrating members "+string.Join("; ",overlaps));
                    Check(unsupported.Count==0,"each raised center is inside its actual bearing polygon "+string.Join(",",unsupported.Take(12)));
                }
                if(overlaps.Count>0||unsupported.Count>0){File.WriteAllText(ProjectPath.Report("SparseStructures-"+phase+".txt"),report.ToString());continue;}
                var root=Create(kind,source);string path=output+"/Mode_sparse-"+id+".prefab";
                try
                {
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                    Check(AssetDatabase.LoadAssetAtPath<GameObject>(path),"selectable map prefab saved");
                }
                finally {Object.DestroyImmediate(root);Services.World=previous;}
                File.WriteAllText(ProjectPath.Report("SparseStructures-"+phase+".txt"),report.ToString());
            }
            if(failures==0)RegisterMaps(kinds,output,report);
            if((phase=="hybrid"||phase=="city"||phase=="city-shapes"||phase=="city-runtime")&&failures==0)EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup");
            AssetDatabase.SaveAssets();Services.World=previous;
            report.AppendLine("AUTHORING COMPLETE failures="+failures+". Runtime validation pending. No team scene was saved.");
            File.WriteAllText(ProjectPath.Report("SparseStructures-"+phase+".txt"),report.ToString());Debug.Log(report.ToString());
            if(failures==0)
            {
                var setup=EditorSceneManager.GetSceneManagerSetup();
                if(setup.Any(s=>string.IsNullOrEmpty(s.path)||s.isLoaded&&SceneManager.GetSceneByPath(s.path).isDirty))throw new InvalidOperationException("Save edited scenes before runtime validation; map prefabs have been authored.");
                SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new SavedScenes{scenes=setup}));SessionState.SetString(Key+"Phase",phase);SessionState.SetBool(Key,true);
                EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
            }
        }
        static IEnumerator RunPhysics()
        {
            yield return null;yield return null;
            string phase=SessionState.GetString(Key+"Phase","");string reportPath=ProjectPath.Report("SparseStructures-"+phase+".txt");var report=new StringBuilder(File.ReadAllText(reportPath));int failures=0;
            if(phase.StartsWith("city-profile")||phase=="city-final-bench")
            {
                var variants=phase=="city-final-bench"?new[]{"packed","final"}:new[]{"packed","direct","batch","cleanup","no-recovery"};
                try
                {
                    foreach(string variant in variants)
                    {
                        profileCleanup=variant=="cleanup"||variant=="final";DeadlyJenga.CandyArt.StructuralMeshBatches.BatchingEnabled=variant=="batch"||variant=="final";CastleSurvivor.UnifiedRecoveryEnabled=variant!="no-recovery";
                        report.AppendLine("PROFILE "+variant+"; recovery disabled is diagnostic only, not a release configuration.");
                        var run=ActualGame(variant=="packed"?SparseStructureKind.FestivalCityPacked:SparseStructureKind.FestivalCityOptimized,report,_=>{});
                        try{while(run.MoveNext()){File.WriteAllText(reportPath,report.ToString());yield return run.Current;}}
                        finally{(run as IDisposable)?.Dispose();}
                    }
                }
                finally{profileCleanup=true;DeadlyJenga.CandyArt.StructuralMeshBatches.BatchingEnabled=true;CastleSurvivor.UnifiedRecoveryEnabled=true;}
                report.AppendLine("PROFILE RESTORE unified="+TraversalCollision.Unified+" blockIgnored="+Physics.GetIgnoreLayerCollision(Layers.Survivor,Layers.Block));
                report.AppendLine("PROFILE COMPLETE runtime errors="+runtimeErrors);File.WriteAllText(reportPath,report.ToString());yield break;
            }
            foreach(var kind in Kinds(phase))
            {
                void Check(bool pass,string text){report.AppendLine((pass?"PASS ":"FAIL ")+SparseStructureLayout.Id(kind)+": "+text);if(!pass)failures++;}
                try{PhysicsCheck(kind,ProjectPath.Find("Mode_sparse-"+SparseStructureLayout.Id(kind)+".prefab"),SparseStructureLayout.Build(kind),report,Check);}catch(Exception e){Check(false,e.ToString());}
                File.WriteAllText(reportPath,report.ToString());yield return null;
            }
            if(failures==0&&(phase=="city"||phase=="city-shapes"||phase=="city-runtime"))
            {
                var baseline=ActualGame(phase=="city-runtime"?SparseStructureKind.FestivalCityPacked:phase=="city-shapes"?SparseStructureKind.FestivalCity:SparseStructureKind.Festival,report,pass=>{if(!pass)failures++;});
                try{while(baseline.MoveNext()){File.WriteAllText(reportPath,report.ToString());yield return baseline.Current;}}
                finally{(baseline as IDisposable)?.Dispose();}
            }
            if(failures==0)
                foreach(var kind in Kinds(phase))
                {
                    var actual=ActualGame(kind,report,pass=>{if(!pass)failures++;});
                    try{while(actual.MoveNext()){File.WriteAllText(reportPath,report.ToString());yield return actual.Current;}}
                    finally{(actual as IDisposable)?.Dispose();}
                }
            if(phase=="city-runtime")
            {
                bool restored=!TraversalCollision.Unified&&Physics.GetIgnoreLayerCollision(Layers.Survivor,Layers.Block);
                report.AppendLine((restored?"PASS ":"FAIL ")+"CITY CONTRACT returning to original map restores collision policy and matrix");if(!restored)failures++;
            }
            report.AppendLine("RUNTIME COMPLETE failures="+failures+"; Unity runtime errors="+runtimeErrors+". Actual Game.unity entered Play Mode; no live headset/network peers tested.");File.WriteAllText(reportPath,report.ToString());
        }
        static IEnumerator ActualGame(SparseStructureKind kind,StringBuilder report,Action<bool> outcome)
        {
            var session=Services.Session;float timeout=Time.realtimeSinceStartup+30;
            while(session==null&&Time.realtimeSinceStartup<timeout){yield return null;session=Services.Session;}
            if(session?.SurvivorBots==null){report.AppendLine("FAIL actual session/bot population unavailable");outcome(false);yield break;}
            var old=Services.World as Component;bool oldActive=old&&old.gameObject.activeSelf;GameObject root=null,waveObject=null;
            int oldVsync=QualitySettings.vSyncCount,oldRate=Application.targetFrameRate;bool background=Application.runInBackground,voice=session.Social?.VoiceEnabled??false;float scale=Time.timeScale;var random=UnityEngine.Random.state;
            QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;Application.runInBackground=true;Time.timeScale=1;
            try
            {
                session.Demo?.Run(DemoCommand.StopDisasters);session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots?.Clear();
                if(session.Social!=null)session.Social.VoiceEnabled=false;
                if(old)old.gameObject.SetActive(false);
                root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("Mode_sparse-"+SparseStructureLayout.Id(kind)+".prefab")));
                var map=root.GetComponent<SparseStructureMap>();map.generateOnStart=false;map.Rebuild();if(map.Debris&&!profileCleanup)map.Debris.enabled=false;session.SurvivorBots.MapRebuilt();
                session.SetRole(true);session.SetMenu(true);var bounds=map.PlayableBounds;var camera=session.ActiveCamera;
                if(camera){camera.transform.position=bounds.center+new Vector3(bounds.size.x*.7f,bounds.size.y*.7f,-bounds.size.z);camera.transform.LookAt(bounds.center);}
                float until=Time.realtimeSinceStartup+3;while(Time.realtimeSinceStartup<until)yield return null;
                var frames=new List<float>();until=Time.realtimeSinceStartup+3;
                while(Time.realtimeSinceStartup<until){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}
                Frames(report,kind,"idle, no bots",frames,map,0);
                var cityMembers=map.GetComponentsInChildren<CityStructuralMember>();var proxies=cityMembers.SelectMany(c=>c.SurvivorShapes).ToArray();
                report.AppendLine($"CENSUS {SparseStructureLayout.Id(kind)}: dynamic bodies={map.Blocks.Count}; physical shapes={map.Blocks.Sum(b=>b.GetComponentsInChildren<Collider>().Length)}; map GameObjects={map.GetComponentsInChildren<Transform>().Length}; new-section proxy shapes={proxies.Length}; proxy GameObjects={proxies.Select(c=>c.gameObject).Distinct().Count()}; new-section box shapes={cityMembers.Sum(c=>c.Shapes.Count(x=>x is BoxCollider))}; convex flights={cityMembers.Sum(c=>c.Shapes.Count(x=>x is MeshCollider))}.");
                Capture(map,root.scene,old,"intact");Services.World=map;
                if(SparseStructureLayout.IsCity(kind))
                    foreach(string detail in new[]{"hall-intact","house-intact","gate-intact"}){Capture(map,root.scene,old,detail);Services.World=map;}
                if(!SessionState.GetString(Key+"Phase","").StartsWith("city-profile")&&SessionState.GetString(Key+"Phase","")!="city-final-bench"&&SparseStructureLayout.IsCity(kind)&&!(SessionState.GetString(Key+"Phase","")=="city-shapes"&&kind==SparseStructureKind.FestivalCity)&&!(SessionState.GetString(Key+"Phase","")=="city-runtime"&&kind==SparseStructureKind.FestivalCityPacked))
                {
                    var traversal=CityWalkChecks(map,session,report,outcome);
                    try{while(traversal.MoveNext()){File.WriteAllText(ProjectPath.Report("SparseStructures-"+SessionState.GetString(Key+"Phase","")+".txt"),report.ToString());yield return traversal.Current;}}
                    finally{(traversal as IDisposable)?.Dispose();}
                }
                int requestedBots=(kind==SparseStructureKind.Festival||SparseStructureLayout.IsCity(kind))?32:16;
                session.SurvivorBots.Running=true;for(int i=0;i<requestedBots;i++)session.SurvivorBots.SpawnOne();
                until=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<until)yield return null;
                var bots=session.SurvivorBots.Members.Where(b=>b).ToArray();var positions=bots.Select(b=>b.transform.position).ToArray();
                frames.Clear();until=Time.realtimeSinceStartup+4;
                while(Time.realtimeSinceStartup<until){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}
                int moved=bots.Where((b,i)=>b&&Vector3.Distance(b.transform.position,positions[i])>.5f).Count();
                Frames(report,kind,"bots, no wave",frames,map,bots.Length);report.AppendLine($"BOT OBSERVATION {SparseStructureLayout.Id(kind)}: spawned {bots.Length}/{requestedBots}; {moved} changed position >.5m over 4 wall-clock seconds. This is a short movement observation, not objective or route completion.");
                if(bots.Length==0){outcome(false);report.AppendLine("FAIL no bots spawned on the production map");}
                var prefab=Resources.Load<GameObject>("DemoDisasters/Tsunami");
                if(prefab)
                {
                    var beforeWave=map.Blocks.Select(b=>b.position).ToArray();
                    UnityEngine.Random.InitState(71293);waveObject=Object.Instantiate(prefab);var hazard=waveObject.GetComponent<IHazard>();var component=hazard as Component;
                    Set(component,"randomDirection",false);Set(component,"approachHeading",0f);Set(component,"warningTime",0f);Set(component,"offshoreDistance",0f);Set(component,"riseTime",.1f);
                    hazard.Begin();until=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<until)yield return null;
                    BeginCounters();frames.Clear();until=Time.realtimeSinceStartup+(SessionState.GetString(Key+"Phase","").StartsWith("city")?25:5);while(Time.realtimeSinceStartup<until){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}
                    Frames(report,kind,"bots + actual tsunami",frames,map,session.SurvivorBots.Count);EndCounters(report);hazard.End();
                    report.AppendLine($"WAVE DISPLACEMENT {SparseStructureLayout.Id(kind)}: {map.Blocks.Where((b,i)=>b&&Vector3.Distance(b.position,beforeWave[i])>.5f).Count()}/{map.Blocks.Count} members moved >.5m during the early wave sample. Default wave force/height retained; route coverage differs by footprint.");
                }
                else{outcome(false);report.AppendLine("FAIL actual tsunami prefab unavailable");}
                if(map.Debris)report.AppendLine($"CITY CLEANUP converted={map.Debris.ConvertedCount}; restored={map.Debris.RestoredCount}; shattered={map.Debris.ShatteredCount}; sunk={map.Debris.SunkCount}; structural draws={DeadlyJenga.CandyArt.StructuralMeshBatches.DrawCalls}.");
                Capture(map,root.scene,old,"wave");
            }
            finally
            {
                if(counters.Count>0)EndCounters(report);
                if(waveObject){waveObject.GetComponent<IHazard>()?.End();Object.Destroy(waveObject);}session.SurvivorBots.Clear();if(root){root.SetActive(false);Object.Destroy(root);}if(old)old.gameObject.SetActive(oldActive);Services.World=old as IWorldQuery;
                QualitySettings.vSyncCount=oldVsync;Application.targetFrameRate=oldRate;Application.runInBackground=background;Time.timeScale=scale;UnityEngine.Random.state=random;if(session.Social!=null)session.Social.VoiceEnabled=voice;
            }
        }
        static void Set(Component component,string field,object value){component?.GetType().GetField(field)?.SetValue(component,value);}
        static void Frames(StringBuilder report,SparseStructureKind kind,string phase,List<float> frames,SparseStructureMap map,int bots)
        {
            if(frames.Count==0){report.AppendLine("FAIL no sampled frames");return;}frames.Sort();
            report.AppendLine($"EDITOR FRAMES {SparseStructureLayout.Id(kind)} [{phase}]: median={frames[frames.Count/2]:F2}ms; p90={frames[Mathf.Min(frames.Count-1,(int)(frames.Count*.9f))]:F2}ms; n={frames.Count}; bots={bots}; active={map.Blocks.Count(b=>b&&b.gameObject.activeSelf)}; awake={map.Blocks.Count(b=>b&&b.gameObject.activeSelf&&!b.IsSleeping())}. Short unfocused Editor samples, not Quest/GPU certification.");
        }
        static GameObject Create(SparseStructureKind kind,GameObject source)
        {
            var root=new GameObject(SparseStructureLayout.Title(kind));var map=root.AddComponent<SparseStructureMap>();map.kind=kind;map.blockPrefab=source;map.generateOnStart=false;
            map.cornerMaterial=Material("Apple Green.mat");map.wallMaterial=Material("Cherry Red.mat");map.keepMaterial=Material("Lavender.mat");
            var floor=new GameObject("Fixed sand foundation");floor.transform.SetParent(root.transform,false);floor.AddComponent<BoxCollider>();map.foundation=floor.transform;
            var bounds=SparseStructureLayout.Bounds(SparseStructureLayout.Build(kind));floor.transform.localPosition=new Vector3(bounds.center.x,-.5f,bounds.center.z);floor.transform.localScale=new Vector3(bounds.size.x+8,1,bounds.size.z+8);
            map.generateOnStart=true;return root;
        }
        static void PhysicsCheck(SparseStructureKind kind,string prefabPath,List<SparseMember> layout,StringBuilder report,Action<bool,string> check)
        {
            var oldScene=SceneManager.GetActiveScene();var oldWorld=Services.World;
            var scene=SceneManager.CreateScene("Sparse structure isolated check",new CreateSceneParameters(LocalPhysicsMode.Physics3D));GameObject root=null;
            try
            {
                SceneManager.SetActiveScene(scene);root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));var map=root.GetComponent<SparseStructureMap>();map.generateOnStart=false;map.Rebuild();var physics=scene.GetPhysicsScene();
                var bodies=map.Blocks.ToArray();var positions=bodies.Select(b=>b.position).ToArray();
                check(bodies.Length==layout.Count&&bodies.All(b=>!b.isKinematic&&b.useGravity&&b.detectCollisions),"all authored members use independent dynamic gravity");
                check(root.GetComponentsInChildren<Joint>().Length==0,"no stabilizing joints");
                if(SparseStructureLayout.IsCity(kind))CityGeometryChecks(map,check,report);
                for(int step=0;step<400;step++)physics.Simulate(.02f);
                foreach(var body in bodies){body.GetComponent<CastleBlock>()?.SyncSurface();body.GetComponent<CityStructuralMember>()?.SyncSurfaces();}Physics.SyncTransforms();
                float drift=bodies.Select((b,i)=>Vector3.Distance(b.position,positions[i])).Max();float tilt=bodies.Select((b,i)=>Quaternion.Angle(b.rotation,layout[i].Rotation)).Max();int awake=bodies.Count(b=>!b.IsSleeping());
                report.AppendLine($"SETTLE {SparseStructureLayout.Id(kind)}: 8s; maximum drift={drift:F4}m; tilt={tilt:F3}deg; awake={awake}/{bodies.Length}.");
                if(SparseStructureLayout.IsCity(kind))
                    foreach(int i in Enumerable.Range(0,bodies.Length).OrderByDescending(i=>Vector3.Distance(bodies[i].position,positions[i])).Take(8))report.AppendLine("CITY DRIFT "+bodies[i].name+": "+Vector3.Distance(bodies[i].position,positions[i]).ToString("F4")+" velocity="+bodies[i].linearVelocity);
                check(drift<.5f&&tilt<5,"unforced structure stays within .5m / 5deg during settling");
                var spawn=map.SafeSpawnPoint();var hits=new Collider[16];
                check(physics.OverlapCapsule(spawn+Vector3.up*.2f,spawn+Vector3.up*.74f,.18f,hits,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore)==0,"safe spawn has standing capsule clearance");
                var watch=Stopwatch.StartNew();for(int step=0;step<150;step++)physics.Simulate(.02f);watch.Stop();double settled=watch.Elapsed.TotalMilliseconds/150;
                // A deliberate support removal tests gravity response independently of hazard tuning.
                int removed=layout.FindIndex(p=>p.Bounds.min.y<.01f&&(p.Role=="pier"||p.Role=="keep pier"||p.Role=="terrace mass"));
                var before=bodies.Select(b=>b.position).ToArray();bodies[removed].gameObject.SetActive(false);foreach(var body in bodies)if(body.gameObject.activeSelf)body.WakeUp();
                watch.Restart();for(int step=0;step<250;step++)physics.Simulate(.02f);watch.Stop();double collapse=watch.Elapsed.TotalMilliseconds/250;
                int displaced=bodies.Where((b,i)=>i!=removed&&Vector3.Distance(before[i],b.position)>.5f).Count();
                report.AppendLine($"COLLAPSE {SparseStructureLayout.Id(kind)}: remove member {removed}; moved>.5m={displaced}; native physics average settled={settled:F3}ms/step, collapse={collapse:F3}ms/step. These timings exclude scripts, bots, rendering and character proxies.\n");
                if(displaced==0&&layout[removed].Role=="keep pier")
                {
                    int adjacent=layout.FindIndex(p=>p.Role=="keep pier"&&p.Bounds.min.y<.01f&&Mathf.Abs(p.Position.x-layout[removed].Position.x)<.01f&&Mathf.Abs(p.Position.z-layout[removed].Position.z-12*(kind==SparseStructureKind.Festival||SparseStructureLayout.IsCity(kind)?1.25f:1))<.01f);
                    check(adjacent>=0,"redundant keep has a second independently removable bearing");
                    if(adjacent>=0)
                    {
                        bodies[adjacent].gameObject.SetActive(false);foreach(var body in bodies)if(body.gameObject.activeSelf)body.WakeUp();
                        for(int step=0;step<250;step++)physics.Simulate(.02f);
                        displaced=bodies.Where((b,i)=>i!=removed&&i!=adjacent&&Vector3.Distance(before[i],b.position)>.5f).Count();
                        report.AppendLine($"REDUNDANCY {SparseStructureLayout.Id(kind)}: one corner survived; removing adjacent corner {adjacent} moved {displaced} members >.5m.");
                    }
                }
                check(displaced>0,"real bearing loss produces gravity-driven displacement without scripted failure");
                if(SparseStructureLayout.IsCity(kind))
                {
                    var hallBase=Enumerable.Range(512,bodies.Length-512).Where(i=>layout[i].Role=="Lantern Hall room"&&layout[i].Position.y<.01f).ToArray();
                    var hallBefore=bodies.Select(b=>b.position).ToArray();int fallen=0,removedHall=0;
                    foreach(int bearing in hallBase)
                    {
                        bodies[bearing].gameObject.SetActive(false);removedHall++;
                        foreach(var body in bodies)if(body.gameObject.activeSelf)body.WakeUp();
                        for(int step=0;step<250;step++)physics.Simulate(.02f);
                        fallen=Enumerable.Range(512,bodies.Length-512).Count(i=>layout[i].Role.StartsWith("Lantern Hall")&&bodies[i].gameObject.activeSelf&&Vector3.Distance(hallBefore[i],bodies[i].position)>.5f);
                        if(fallen>0)break;
                    }
                    report.AppendLine($"CITY BEARING LOSS: removing {removedHall}/{hallBase.Length} ground room sections displaced {fallen} other Hall members >.5m under native gravity.");
                    check(hallBase.Length==4&&fallen>0,"new Hall responds physically to lost supports");
                }
                string saved=map.SaveLayout();int revision=map.Revision;
                check(!map.LoadLayout(saved,bodies.Length+1,new Pose(Vector3.zero,Quaternion.identity),Vector3.one)&&map.Revision==revision,"invalid network count leaves live layout unchanged");
                check(!map.LoadLayout(saved,bodies.Length,new Pose(Vector3.zero,Quaternion.identity),new Vector3(float.NaN,1,1)),"invalid network scale rejected");
                map.SetReplica(true);check(map.Blocks.All(b=>b.isKinematic),"replica suspends independent simulation");map.SetReplica(false);check(map.Blocks.All(b=>!b.isKinematic),"authority recovery restores dynamic bodies");
                check(map.LoadLayout(saved,bodies.Length,new Pose(Vector3.zero,Quaternion.identity),Vector3.one)&&map.Blocks.Count==layout.Count,"serialized layout rebuild retains stable member count");
                Capture(map,scene,oldWorld as Component);
                if(SparseStructureLayout.IsCity(kind))
                    report.AppendLine("NOTE final city review images are captured after the normal Game frame warm-up; isolated preview can contain queued batch draws.");
            }
            finally
            {
                if(root)Object.DestroyImmediate(root);if(oldScene.IsValid())SceneManager.SetActiveScene(oldScene);if(scene.IsValid())SceneManager.UnloadSceneAsync(scene);Services.World=oldWorld;
            }
        }
        sealed class CityWalkDriver : DeadlyJenga.CastleBots.ICastleSurvivorDriver
        {
            public readonly List<Vector3> points=new();public int index,calls;
            public DeadlyJenga.CastleBots.CastleBotCommand ReadCommand(CastleSurvivor motor,float dt)
            {
                calls++;if(index>=points.Count)return default;
                var delta=points[index]-motor.transform.position;var flat=Vector3.ProjectOnPlane(delta,Vector3.up);
                if(flat.magnitude<.24f&&Mathf.Abs(delta.y)<.4f){index++;return default;}
                return new DeadlyJenga.CastleBots.CastleBotCommand{move=flat.normalized*Mathf.Min(1,flat.magnitude/Mathf.Max(.01f,motor.walkSpeed*dt)),look=flat.normalized};
            }
        }
        static IEnumerator CityWalkChecks(SparseStructureMap map,ISessionInfo session,StringBuilder report,Action<bool> outcome)
        {
            var actor=session.SurvivorBots.SpawnOne();if(!actor){report.AppendLine("FAIL city traversal actor did not spawn");outcome(false);yield break;}
            var motor=actor.GetComponent<CastleSurvivor>();var brain=actor.GetComponent<DeadlyJenga.CastleBots.CastleBotBrain>();var previous=motor.InputDriver;
            var driver=new CityWalkDriver();float oldTime=Time.timeScale;bool motorEnabled=motor.enabled;
            try
            {
                foreach(var control in actor.GetComponents<MonoBehaviour>())if(control is DeadlyJenga.CastleBots.ICastleSurvivorDriver)control.enabled=false;motor.InputDriver=driver;motor.InputSuspended=false;motor.enabled=false;session.SetMenu(false);Time.timeScale=2;
                foreach(string name in new[]{"Lantern Hall","Setback House","Gate Towers"})
                {
                    driver.points.Clear();driver.index=driver.calls=0;
                    Vector3 start;
                    if(name=="Gate Towers")
                    {
                        start=new Vector3(-6.3f,1.3f,77.5f);
                        for(int tier=0;tier<6;tier++)
                        {
                            driver.points.Add(new Vector3(6.3f,tier*5+3.75f,77.5f));driver.points.Add(new Vector3(6.3f,tier*5+3.75f,86.5f));
                            driver.points.Add(new Vector3(-6.3f,tier*5+6.25f,86.5f));driver.points.Add(new Vector3(-6.3f,tier*5+6.25f,77.5f));
                        }
                        driver.points.Add(new Vector3(-6.3f,31.25f,82));driver.points.Add(new Vector3(6.3f,31.25f,82));driver.points.Add(new Vector3(0,31.25f,82));driver.points.Add(new Vector3(0,31.25f,67));
                    }
                    else
                    {
                        bool house=name=="Setback House";Vector2 c=house?FestivalCityLayout.House:FestivalCityLayout.Hall;int tiers=house?8:12;
                        start=new Vector3(c.x-2.5f,1.3f,c.y-5.3f);
                        for(int tier=0;tier<tiers;tier++)
                        {
                            float inner=house&&tier>=4?3:5,next=house&&tier==3?3:inner;
                            driver.points.Add(new Vector3(c.x-inner*.5f,tier*5+3.75f,c.y+inner+.3f));
                            if(tier+1==tiers)break;
                            driver.points.Add(new Vector3(c.x+next*.5f,tier*5+3.75f,c.y+inner+.3f));
                            driver.points.Add(new Vector3(c.x+next*.5f,tier*5+6.25f,c.y-next-.3f));
                            driver.points.Add(new Vector3(c.x-next*.5f,tier*5+6.25f,c.y-next-.3f));
                        }
                    }
                    motor.Teleport(start,Quaternion.identity);float end=Time.realtimeSinceStartup+90;int reported=-1;
                    while(driver.index<driver.points.Count&&Time.realtimeSinceStartup<end)
                    {
                        motor.InputDriver=driver;motor.InputSuspended=false;
                        float remaining=Mathf.Min(Time.deltaTime,.2f);
                        while(remaining>.00001f)
                        {
                            float dt=Mathf.Min(.02f,remaining);var command=driver.ReadCommand(motor,dt);
                            if(command.look.sqrMagnitude>.01f){motor.transform.rotation=Quaternion.LookRotation(command.look);motor.viewCamera.transform.localRotation=Quaternion.identity;}
                            var forward=Vector3.ProjectOnPlane(motor.viewCamera.transform.forward,Vector3.up).normalized;
                            motor.Simulate(new Vector2(Vector3.Dot(command.move,Vector3.Cross(Vector3.up,forward)),Vector3.Dot(command.move,forward)),false,dt,false);remaining-=dt;
                        }
                        if(driver.index!=reported&&driver.index%5==0){reported=driver.index;report.AppendLine("CITY WALK progress "+name+" "+driver.index+"/"+driver.points.Count);}
                        yield return null;
                    }
                    bool success=driver.index==driver.points.Count;outcome(success);
                    report.AppendLine((success?"PASS":"FAIL")+" city production motor walked "+name+" at 2x clock without jump/climb: "+driver.index+"/"+driver.points.Count+" landings; feet="+motor.transform.position);
                    report.AppendLine("CITY WALK state calls="+driver.calls+" paused="+motor.InputSuspended+" grounded="+motor.Grounded+" angle="+motor.GroundAngle+" health="+((IDamageable)motor).Health+" status="+motor.ActionStatus);
                    if(!success)
                    {
                        var touched=Physics.OverlapCapsule(motor.transform.position+Vector3.up*.13f,motor.transform.position+Vector3.up*.72f,.115f,1<<Layers.Surface,QueryTriggerInteraction.Ignore);
                        report.AppendLine("CITY WALK touching "+string.Join("; ",touched.Select(c=>c.name)));
                    }
                }
                if(map.UsesOriginalColliders)
                {
                    var contracts=CityRuntimeContracts.Run(map,motor,report,outcome);
                    try{while(contracts.MoveNext()){File.WriteAllText(ProjectPath.Report("SparseStructures-"+SessionState.GetString(Key+"Phase","")+".txt"),report.ToString());yield return contracts.Current;}}
                    finally{(contracts as IDisposable)?.Dispose();}
                }
                var samples=new List<Vector3>();int flightSamples=0;
                foreach(var body in map.Blocks.Where(b=>b.name.Contains("flight"))){DeadlyJenga.CastleBots.BotGeometrySamples.Fill(body,samples);flightSamples+=samples.Count;}
                outcome(flightSamples>0);report.AppendLine((flightSamples>0?"PASS":"FAIL")+" city bot geometry samples actual convex flight surfaces: "+flightSamples);
            }
            finally{Time.timeScale=oldTime;motor.enabled=motorEnabled;motor.InputDriver=previous;if(brain)brain.enabled=true;session.SurvivorBots.Clear();session.SetMenu(true);}
        }
        [MenuItem("Tools/Deadly Jenga/Maps/Check Festival City Navigation Samples")]
        public static void CheckCityNavigation()
        {
            var previous=Services.World;var oldScene=SceneManager.GetActiveScene();GameObject root=null;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("Mode_sparse-festival-city.prefab")));
                var map=root.GetComponent<SparseStructureMap>();map.generateOnStart=false;map.Rebuild();Physics.SyncTransforms();
                var room=map.Blocks.First(b=>b.name.Contains("Lantern Hall room")&&b.position.y<.01f);var points=new List<Vector3>();
                DeadlyJenga.CastleBots.BotGeometrySamples.Fill(room,points);
                var upper=points.Where(p=>Mathf.Abs(p.y-8.75f)<.01f).First();
                bool near=DeadlyJenga.CastleBots.BotGeometrySamples.Near(room,upper,6,6,9);
                bool far=!DeadlyJenga.CastleBots.BotGeometrySamples.Near(room,upper+Vector3.right*1000,6,6,9);
                bool original=DeadlyJenga.CastleBots.BotGeometrySamples.Near(map.Blocks[0],map.Blocks[0].GetComponent<Collider>().bounds.center,6,6,9);
                string result=$"City navigation regression: {(near&&far&&original?"PASS":"FAIL")}. Upper gallery {upper} is >6m above its body root; sampled and accepted by actual collider bounds. Far-away geometry rejected={far}; ordinary block retained={original}. No Play Mode/headset inference.\n";
                File.WriteAllText(ProjectPath.Report("CityNavigation.txt"),result);Debug.Log(result);
            }
            catch(Exception e){File.WriteAllText(ProjectPath.Report("CityNavigation.txt"),"FAIL "+e);Debug.LogException(e);}
            finally{if(root)Object.DestroyImmediate(root);if(oldScene.IsValid())SceneManager.SetActiveScene(oldScene);EditorSceneManager.CloseScene(scene,true);Services.World=previous;}
        }
        static void CityGeometryChecks(SparseStructureMap map,Action<bool,string> check,StringBuilder report)
        {
            int bad=0;var bodies=map.Blocks;var details=new List<string>();
            var baseline=SparseStructureLayout.Build(SparseStructureKind.FestivalCity);var variant=SparseStructureLayout.Build(map.kind);
            check(baseline.Count==variant.Count&&variant.Select((p,i)=>p.Position==baseline[i].Position&&p.Rotation==baseline[i].Rotation&&p.Size==baseline[i].Size&&(p.Parts==null?baseline[i].Parts==null:p.Parts.SequenceEqual(baseline[i].Parts))).All(v=>v),"all authored members and room parts exactly match preserved Festival City");
            for(int i=0;i<bodies.Count;i++)for(int j=Mathf.Max(i+1,512);j<bodies.Count;j++)
            {
                var aa=bodies[i].GetComponentsInChildren<Collider>();var bb=bodies[j].GetComponentsInChildren<Collider>();
                foreach(var a in aa)foreach(var b in bb)
                    if(a.enabled&&b.enabled&&a.bounds.Intersects(b.bounds)&&Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float depth)&&depth>.02f)
                    {bad++;if(details.Count<16)details.Add(i+" "+bodies[i].name+" / "+j+" "+bodies[j].name+" depth="+depth.ToString("F3"));}
            }
            check(bad==0,"compound/flight solid shapes do not initially penetrate: "+string.Join("; ",details));
            var original=SparseStructureLayout.Build(SparseStructureKind.Festival);
            check(original.Count==512&&map.Blocks.Take(512).Select((b,i)=>b.GetComponent<CastleBlock>()&&Vector3.Distance(b.position,original[i].Position)<.001f&&Quaternion.Angle(b.rotation,original[i].Rotation)<.001f&&Vector3.Distance(b.transform.localScale,original[i].Size)<.001f).All(v=>v),"original 512 Festival members retain their exact layout before city additions");
            check(map.Blocks.All(b=>b.GetComponent<Collider>()),"every member exposes a root collider for existing mode queries");
            report.AppendLine("CITY member count="+bodies.Count+"; real solid collider count="+bodies.Sum(b=>b.GetComponentsInChildren<Collider>().Length)+" (excludes survivor proxies)");
        }
        static void Capture(SparseStructureMap map,Scene scene,Component oldMap,string phase="isolated")
        {
            bool wasActive=oldMap&&oldMap.gameObject.activeSelf;
            if(oldMap)oldMap.gameObject.SetActive(false);
            foreach(var environment in Object.FindObjectsByType<DeadlyJenga.CandyArt.CandyEnvironment>(FindObjectsSortMode.None))environment.Refresh();
            var cameraObject=new GameObject("Sparse map review camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);var camera=cameraObject.AddComponent<Camera>();
            camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.2f,.55f,.85f);camera.fieldOfView=48;
            var bounds=map.PlayableBounds;float distance=Mathf.Max(bounds.size.x,bounds.size.z,bounds.size.y)*1.45f;
            var target=bounds.center;
            if(phase.StartsWith("hall-")){target=new Vector3(65,32,-4);distance=102;}
            if(phase.StartsWith("house-")){target=new Vector3(-65,22,5);distance=78;}
            if(phase.StartsWith("gate-")){target=new Vector3(0,17,82);distance=65;}
            camera.transform.position=target+new Vector3(.8f,.65f,-1).normalized*distance;camera.transform.LookAt(target);
            var lightObject=new GameObject("Sparse map review sun");SceneManager.MoveGameObjectToScene(lightObject,scene);var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Any(l=>l!=light&&l.isActiveAndEnabled&&l.type==LightType.Directional)?0:1.2f;light.transform.rotation=Quaternion.Euler(45,-35,0);
            var targetTexture=new RenderTexture(1280,800,24);var previous=RenderTexture.active;var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=targetTexture;camera.Render();RenderTexture.active=targetTexture;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report("Sparse-"+SparseStructureLayout.Id(map.kind)+"-"+phase+".png"),image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(image);targetTexture.Release();Object.DestroyImmediate(targetTexture);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(lightObject);
                if(oldMap)oldMap.gameObject.SetActive(wasActive);Services.World=oldMap as IWorldQuery;
                foreach(var environment in Object.FindObjectsByType<DeadlyJenga.CandyArt.CandyEnvironment>(FindObjectsSortMode.None))environment.Refresh();
            }
        }
        static void RegisterMaps(SparseStructureKind[] kinds,string folder,StringBuilder report)
        {
            var catalogs=AssetDatabase.FindAssets("Catalog t:ScriptableObject").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ScriptableObject>).Where(a=>a&&a.GetType().Name=="IslandLobbyCatalog");
            foreach(var catalog in catalogs)
            {
                var serialized=new SerializedObject(catalog);var maps=serialized.FindProperty("maps");if(maps==null||!maps.isArray)continue;
                if(maps.arraySize>0&&maps.GetArrayElementAtIndex(0).FindPropertyRelative("mapPrefab")==null){report.AppendLine("NOTE: prefab-map lobby integration from the existing project is required to register these maps; no legacy catalog entries changed.");continue;}
                string scene=ProjectPath.Find("Game.unity");
                foreach(var kind in kinds)
                {
                    string id="sparse-"+SparseStructureLayout.Id(kind);SerializedProperty entry=null;
                    for(int i=0;i<maps.arraySize;i++)if(maps.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue==id){entry=maps.GetArrayElementAtIndex(i);break;}
                    if(entry==null){int i=maps.arraySize;maps.InsertArrayElementAtIndex(i);entry=maps.GetArrayElementAtIndex(i);}
                    entry.FindPropertyRelative("id").stringValue=id;entry.FindPropertyRelative("title").stringValue=SparseStructureLayout.Title(kind);entry.FindPropertyRelative("scenePath").stringValue=scene;
                    var prefab=entry.FindPropertyRelative("mapPrefab");
                    if(prefab==null){report.AppendLine("NOTE: catalog does not yet expose prefab maps; use these prefabs through Core IWorldQuery in a scratch scene.");continue;}
                    prefab.objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/Mode_sparse-"+SparseStructureLayout.Id(kind)+".prefab");
                    entry.FindPropertyRelative("recommendedPlayers").intValue=(int)kind<4?16:32;entry.FindPropertyRelative("recommendedMode").stringValue="demo";entry.FindPropertyRelative("walkableIsland").boolValue=false;
                    var modes=entry.FindPropertyRelative("supportedModes");if(modes!=null)modes.arraySize=0;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(catalog);report.AppendLine("REGISTERED map choices in "+AssetDatabase.GetAssetPath(catalog));
            }
        }
        static Material Material(string name){try{return AssetDatabase.LoadAssetAtPath<Material>(ProjectPath.Find(name));}catch(FileNotFoundException){return null;}}
        static string Folder(string parent,string name){string path=parent+"/"+name;if(!AssetDatabase.IsValidFolder(path))AssetDatabase.CreateFolder(parent,name);return path;}
        // Separating-axis test avoids mistaking a ramp's empty AABB corners for solid overlap.
        static bool Penetrates(SparseMember a,SparseMember b)
        {
            var aa=new[]{a.Rotation*Vector3.right,a.Rotation*Vector3.up,a.Rotation*Vector3.forward};
            var bb=new[]{b.Rotation*Vector3.right,b.Rotation*Vector3.up,b.Rotation*Vector3.forward};
            var axes=new List<Vector3>();axes.AddRange(aa);axes.AddRange(bb);
            foreach(var x in aa)foreach(var y in bb){var cross=Vector3.Cross(x,y);if(cross.sqrMagnitude>.000001f)axes.Add(cross.normalized);}
            var delta=b.Position-a.Position;
            foreach(var axis in axes)
            {
                float ra=0,rb=0;
                for(int i=0;i<3;i++){ra+=Mathf.Abs(Vector3.Dot(axis,aa[i]))*a.Size[i]*.5f;rb+=Mathf.Abs(Vector3.Dot(axis,bb[i]))*b.Size[i]*.5f;}
                if(ra+rb-Mathf.Abs(Vector3.Dot(delta,axis))<=.001f)return false;
            }
            return true;
        }
        static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.x)&&!float.IsInfinity(v.y)&&!float.IsInfinity(v.z);
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static bool Inside(List<Vector2> points,Vector2 center)
        {
            if(points.Count<3)return false;points.Sort((a,b)=>a.x==b.x?a.y.CompareTo(b.y):a.x.CompareTo(b.x));var hull=new List<Vector2>();
            foreach(var p in points){while(hull.Count>=2&&Cross(hull[hull.Count-1]-hull[hull.Count-2],p-hull[hull.Count-1])<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}int lower=hull.Count;
            for(int i=points.Count-2;i>=0;i--){var p=points[i];while(hull.Count>lower&&Cross(hull[hull.Count-1]-hull[hull.Count-2],p-hull[hull.Count-1])<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}
            if(hull.Count<4)return false;for(int i=0;i<hull.Count-1;i++)if(Cross(hull[i+1]-hull[i],center-hull[i])<-.001f)return false;return true;
        }
    }
}
