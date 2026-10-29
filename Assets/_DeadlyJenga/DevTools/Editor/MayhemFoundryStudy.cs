using System;
using System.Collections;
using System.Collections.Generic;
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

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class MayhemFoundryStudy
    {
        const string Key="MayhemFoundryStudy",Id="mayhem-foundry";
        static IEnumerator routine;
        static int errors;
        static readonly List<(string name,ProfilerRecorder recorder)> counters=new();
        [Serializable] sealed class Saved {public SceneSetup[] scenes;}
        static MayhemFoundryStudy()
        {
            EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=State;
        }
        static void Poll()
        {
            if(SessionState.GetBool(Key,false)&&EditorApplication.isPlaying&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!SessionState.GetBool(Key+"Finishing",false)&&routine==null)
            {StartRunner();return;}
            var request=ProjectPath.Report("MayhemFoundry.request");
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(!ValidationRunGate.TryAcquire(Key))return;
            string phase=File.ReadAllText(request).Trim();File.Delete(request);
            try
            {
                if(phase=="build"){Build();ValidationRunGate.Release(Key);return;}
                var scenes=EditorSceneManager.GetSceneManagerSetup();
                if(scenes.Any(s=>s.isLoaded&&(string.IsNullOrEmpty(s.path)||SceneManager.GetSceneByPath(s.path).isDirty)))throw new InvalidOperationException("Existing scenes have unsaved changes; left untouched.");
                SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Saved{scenes=scenes}));SessionState.SetString(Key+"Phase",phase);SessionState.SetBool(Key+"Finishing",false);SessionState.SetBool(Key,true);
                if(phase=="game")EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                EditorApplication.EnterPlaymode();
            }
            catch(Exception e){File.AppendAllText(ProjectPath.Report("MayhemFoundry-error.txt"),e+"\n");ValidationRunGate.Release(Key);UnityEngine.Debug.LogException(e);}
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {StartRunner();}
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update-=Tick;Application.logMessageReceived-=Log;routine=null;SessionState.SetBool(Key,false);
                if(SessionState.GetBool(Key+"Locked",false)){SessionState.SetBool(Key+"Locked",false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();}
                var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Scenes",""));if(saved!=null)EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
                if(SessionState.GetString(Key+"Phase","")=="game"&&File.Exists(ProjectPath.Report("Foundry-island-intact.png")))InstallPreview();
                ValidationRunGate.Release(Key);
            }
        }
        static void StartRunner()
        {
            if(routine!=null)return;
            if(!SessionState.GetBool(Key+"Locked",false)){EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();SessionState.SetBool(Key+"Locked",true);}
            errors=0;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
            routine=SessionState.GetString(Key+"Phase","")=="game"?GameCheck():PhysicsChecks();EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Log(string msg,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(ProjectPath.Report("MayhemFoundry-errors.txt"),msg+"\n"+trace+"\n");}}
        static void Tick()
        {
            if(routine==null||EditorApplication.isCompiling)return;
            try{if(routine.MoveNext())return;}
            catch(Exception e){File.AppendAllText(ProjectPath.Report("MayhemFoundry-error.txt"),e+"\n");UnityEngine.Debug.LogException(e);}
            (routine as IDisposable)?.Dispose();routine=null;EditorApplication.update-=Tick;SessionState.SetBool(Key+"Finishing",true);EditorApplication.ExitPlaymode();
        }
        [MenuItem("Tools/Deadly Jenga/Maps/Build Mayhem Foundry")]
        public static void Build()
        {
            var old=Services.World;var root=new GameObject("Mayhem Foundry - Ten Districts");
            try
            {
                var map=root.AddComponent<MayhemFoundryMap>();
                map.palette=new[]{Material("Apple Green.mat"),Material("Cherry Red.mat"),Material("Lavender.mat"),Material("Golden bamboo.mat")};
                map.friction=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(ProjectPath.Find("Castle Friction.physicMaterial"));
                string maps=Path.GetDirectoryName(ProjectPath.FolderOf("MayhemFoundryMap.cs").TrimEnd('/')).Replace('\\','/');
                string resources=Folder(maps,"Resources"),output=Folder(resources,"ModeMaps");
                var catalog=Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");if(!catalog)throw new InvalidOperationException("Missing lobby catalog.");
                var floor=new GameObject("Continuous island foundation");floor.transform.SetParent(root.transform,false);map.foundation=floor.transform;
                var island=IslandCollider(map,catalog,resources);map.islandCollision=island;floor.AddComponent<MeshCollider>().sharedMesh=island;
                var signs=PrefabUtility.InstantiatePrefab(Signs(map.palette)) as GameObject;signs.name="Visual";signs.transform.SetParent(root.transform,false);
                string path=output+"/Mode_"+Id+".prefab";PrefabUtility.SaveAsPrefabAsset(root,path);
                var entries=new List<IslandLobbyCatalog.Map>(catalog.maps);var entry=entries.Find(e=>e.id==Id);
                if(entry==null){entry=new IslandLobbyCatalog.Map{id=Id};entries.Add(entry);}
                entry.title="Mayhem Foundry · Ten Districts";entry.mapPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);entry.scenePath=ProjectPath.Find("Game.unity");
                entry.walkableIsland=true;entry.recommendedMode="demo";entry.recommendedPlayers=24;entry.supportedModes=Array.Empty<string>();
                catalog.maps=entries.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
                var layout=MayhemStructureLayout.Build();
                File.WriteAllText(ProjectPath.Report("MayhemFoundry-build.txt"),"BUILT "+path+"; catalog selectable; ten districts across a 680 x 470 m district envelope; continuous shore-matched island "+island.bounds.size+"; "+layout.Count+" bodies / "+layout.Sum(m=>m.shapes.Count)+" solid shapes. Existing maps/scenes preserved.\n");
                EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup");
            }
            finally{Object.DestroyImmediate(root);Services.World=old;}
        }
        static Material Material(string name)=>AssetDatabase.LoadAssetAtPath<Material>(ProjectPath.Find(name));
        static string Folder(string parent,string name){string p=parent+"/"+name;if(!AssetDatabase.IsValidFolder(p))AssetDatabase.CreateFolder(parent,name);return p;}
        static Mesh IslandCollider(MayhemFoundryMap map,IslandLobbyCatalog catalog,string output)
        {
            var environment=catalog.environmentPrefab?catalog.environmentPrefab.GetComponent<DeadlyJenga.CandyArt.CandyEnvironment>():null;
            var profile=catalog.art as DeadlyJenga.CandyArt.CandyArtProfile;if(!profile&&environment)profile=environment.profile;
            if(!profile)throw new InvalidOperationException("Missing island art profile for shoreline collision matching.");
            var probe=new GameObject("Temporary shoreline sampler");var terrain=probe.AddComponent<DeadlyJenga.CandyArt.CandySandbar>();
            try
            {
                terrain.profile=profile;terrain.map=map;terrain.Refresh();
                var vertices=new List<Vector3>{new Vector3(0,-.02f,0)};var triangles=new List<int>();const int sides=128;
                for(int i=0;i<sides;i++)
                {
                    float angle=i*Mathf.PI*2/sides;var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));float low=0,high=600;
                    for(int n=0;n<20;n++){float mid=(low+high)*.5f;if(terrain.ShapeDistance(direction*mid)<-1)low=mid;else high=mid;}
                    var p=direction*low;vertices.Add(new Vector3(p.x,-.02f,p.y));
                }
                for(int i=0;i<sides;i++){triangles.Add(0);triangles.Add(1+(i+1)%sides);triangles.Add(1+i);}
                var mesh=new Mesh{name="Mayhem Foundry shore collision"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                string path=output+"/MayhemFoundryIsland.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);return saved;}
                AssetDatabase.CreateAsset(mesh,path);return mesh;
            }
            finally{Object.DestroyImmediate(probe);}
        }
        static GameObject Signs(Material[] palette)
        {
            string visuals=Path.GetDirectoryName(ProjectPath.FolderOf("CandyEnvironment.cs").TrimEnd('/')).Replace('\\','/');
            string folder=Folder(visuals,"MayhemFoundry");string path=folder+"/MayhemFoundrySigns.prefab";
            var root=new GameObject("Mayhem Foundry wayfinding");var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            try
            {
                for(int d=0;d<10;d++)
                {
                    var sign=new GameObject(MayhemStructureLayout.Names[d]);sign.transform.SetParent(root.transform,false);sign.transform.localPosition=MayhemStructureLayout.Centers[d]+new Vector3(-57,0,-58);
                    void Block(Vector3 p,Vector3 size,int finish)
                    {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);Object.DestroyImmediate(go.GetComponent<Collider>());go.transform.SetParent(sign.transform,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=palette[finish];}
                    Block(new Vector3(-5,1.5f,0),new Vector3(.5f,3,.5f),0);Block(new Vector3(5,1.5f,0),new Vector3(.5f,3,.5f),0);Block(new Vector3(0,3,0),new Vector3(14,3,.3f),2);
                    var textObject=new GameObject("District name");textObject.transform.SetParent(sign.transform,false);textObject.transform.localPosition=new Vector3(0,3,-.17f);
                    var text=textObject.AddComponent<TextMesh>();text.font=font;text.fontSize=64;text.characterSize=.35f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=Color.white;text.text=(d+1).ToString("D2")+"\n"+MayhemStructureLayout.Names[d];textObject.GetComponent<MeshRenderer>().sharedMaterial=font.material;
                }
                return PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{Object.DestroyImmediate(root);}
        }
        static void InstallPreview()
        {
            string presentation=Path.GetDirectoryName(Path.GetDirectoryName(ProjectPath.FolderOf("CandyEnvironment.cs").TrimEnd('/'))).Replace('\\','/');
            string folder=Folder(Folder(presentation,"Resources"),"LobbyPreviews"),path=folder+"/"+Id+".png";
            File.WriteAllBytes(path,File.ReadAllBytes(ProjectPath.Report("Foundry-island-intact.png")));AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.maxTextureSize=1024;importer.mipmapEnabled=false;importer.SaveAndReimport();
        }
        static GameObject Create(int district,Scene scene)
        {
            var root=Object.Instantiate(Resources.Load<GameObject>("ModeMaps/Mode_"+Id));SceneManager.MoveGameObjectToScene(root,scene);
            var map=root.GetComponent<MayhemFoundryMap>();map.generateOnStart=false;map.district=district;map.Rebuild();return root;
        }
        static IEnumerator PhysicsChecks()
        {
            var report=new StringBuilder("Mayhem Foundry: actual isolated Unity PhysX, not frame-rate or headset certification.\n");
            File.WriteAllText(ProjectPath.Report("MayhemFoundry-physics.txt"),report.ToString());
            var previous=Services.World;var priorScene=SceneManager.GetActiveScene();
            for(int district=0;district<10;district++)
            {
                var scene=SceneManager.CreateScene("Foundry isolated "+district+" "+Guid.NewGuid().ToString("N"),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                SceneManager.SetActiveScene(scene);var root=Create(district,scene);var map=root.GetComponent<MayhemFoundryMap>();var physics=scene.GetPhysicsScene();
                try
                {
                    report.AppendLine("\nDISTRICT "+district+" "+MayhemStructureLayout.Names[district]+" bodies="+map.Blocks.Count+" shapes="+map.Blocks.Sum(b=>b.GetComponentsInChildren<Collider>().Length));
                    var shapes=root.GetComponentsInChildren<Collider>().Where(c=>c.attachedRigidbody).ToArray();
                    int overlap=0;float deepest=0;var details=new List<string>();
                    for(int i=0;i<shapes.Length;i++)for(int j=i+1;j<shapes.Length;j++)
                    {
                        var a=shapes[i];var c=shapes[j];if(a.attachedRigidbody==c.attachedRigidbody||!a.bounds.Intersects(c.bounds))continue;
                        if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,c,c.transform.position,c.transform.rotation,out _,out float d)&&d>.04f)
                        {overlap++;deepest=Mathf.Max(deepest,d);if(details.Count<16)details.Add(a.attachedRigidbody.name+" / "+c.attachedRigidbody.name+" depth="+d.ToString("F3"));}
                    }
                    report.AppendLine("GEOMETRY penetrations>.04m="+overlap+" deepest="+deepest.ToString("F3")+" "+string.Join("; ",details));
                    var starts=map.Blocks.Select(b=>b.position).ToArray();var rotations=map.Blocks.Select(b=>b.rotation).ToArray();
                    var watch=Stopwatch.StartNew();
                    for(int step=0;step<600;step++){physics.Simulate(.02f);if(step%100==99)yield return null;}
                    watch.Stop();
                    float drift=0,tilt=0,rms=0;int moved=0;
                    for(int i=0;i<map.Blocks.Count;i++)
                    {var rb=map.Blocks[i];float d=Vector3.Distance(rb.position,starts[i]);drift=Mathf.Max(drift,d);if(d>.6f)moved++;tilt=Mathf.Max(tilt,Quaternion.Angle(rb.rotation,rotations[i]));rms+=rb.linearVelocity.sqrMagnitude;}
                    rms=Mathf.Sqrt(rms/map.Blocks.Count);
                    report.AppendLine($"SETTLE 12 simulated seconds: maxDrift={drift:F3}m maxTilt={tilt:F2}deg rmsSpeed={rms:F3}m/s drift>.6m={moved}; wall={watch.Elapsed.TotalSeconds:F2}s (includes yielded frames).");
                    if(drift>.6f)report.AppendLine("MOVED "+string.Join("; ",map.Blocks.Select((rb,i)=>new {rb,i,d=Vector3.Distance(rb.position,starts[i])}).Where(v=>v.d>.6f).OrderByDescending(v=>v.d).Take(12).Select(v=>v.rb.name+" "+v.d.ToString("F2"))));
                    Capture(map,scene,district,"settled");
                    int samples=0,high=0;var points=new List<Vector3>();
                    foreach(var rb in map.Blocks){DeadlyJenga.CastleBots.BotGeometrySamples.Fill(rb,points);samples+=points.Count;high+=points.Count(p=>p.y>20);}
                    report.AppendLine("BOT GEOMETRY production sampler="+samples+" points; above20m="+high+". Coverage only, not route completion.");
                    string saved=map.SaveLayout();int revision=map.Revision;bool reject=!map.LoadLayout(saved,map.Blocks.Count+1,new Pose(Vector3.zero,Quaternion.identity),Vector3.one)&&map.Revision==revision;
                    map.SetReplica(true);bool kinematic=map.Blocks.All(rb=>rb.isKinematic);map.SetReplica(false);report.AppendLine("CONTRACT wrongCountRejected="+reject+" replicaKinematic="+kinematic+" authorityDynamic="+map.Blocks.All(rb=>!rb.isKinematic)+" rootColliders="+map.Blocks.All(rb=>rb.GetComponent<Collider>()));
                    var candidate=map.Blocks.FirstOrDefault(rb=>rb.name.Contains(district==7?"keystone":district==9?"heel counterweight":district==8?"cradle":"Bearing pier"))??map.Blocks[0];
                    var before=map.Blocks.Select(rb=>rb.position).ToArray();candidate.gameObject.SetActive(false);foreach(var rb in map.Blocks)rb.WakeUp();
                    for(int step=0;step<250;step++){physics.Simulate(.02f);if(step%100==99)yield return null;}
                    int altered=map.Blocks.Where((rb,i)=>rb!=candidate&&Vector3.Distance(rb.position,before[i])>.5f).Count();
                    report.AppendLine("SUPPORT / RELEASE REMOVED "+candidate.name+"; other members moved>.5m="+altered);
                    Capture(map,scene,district,"released");
                    File.WriteAllText(ProjectPath.Report("MayhemFoundry-physics.txt"),report.ToString());
                }
                finally{Object.Destroy(root);SceneManager.SetActiveScene(priorScene);SceneManager.UnloadSceneAsync(scene);Services.World=previous;}
                yield return null;
            }
            report.AppendLine("COMPLETE runtimeErrors="+errors);File.WriteAllText(ProjectPath.Report("MayhemFoundry-physics.txt"),report.ToString());
        }
        static void Capture(MayhemFoundryMap map,Scene scene,int district,string state)
        {
            var cameraObject=new GameObject("Foundry review camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);var camera=cameraObject.AddComponent<Camera>();camera.scene=scene;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.28f,.65f,.88f);camera.fieldOfView=48;camera.farClipPlane=2500;
            var target=district<0?new Vector3(0,12,0):MayhemStructureLayout.Centers[district]+Vector3.up*12;
            camera.transform.position=target+new Vector3(.65f,.7f,-1).normalized*(district<0?850:155);camera.transform.LookAt(target);
            bool hasSun=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Any(l=>l.gameObject.scene==scene&&l.enabled&&l.type==LightType.Directional);
            var sunObject=new GameObject("Foundry review sun");SceneManager.MoveGameObjectToScene(sunObject,scene);var sun=sunObject.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=hasSun?0:1.4f;sun.transform.rotation=Quaternion.Euler(48,-35,0);
            GameObject ground=null;
            if(!Object.FindObjectsByType<DeadlyJenga.CandyArt.CandyEnvironment>(FindObjectsSortMode.None).Any(e=>e.gameObject.scene==scene&&e.enabled))
            {ground=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(ground,scene);ground.transform.position=new Vector3(target.x,-1.1f,target.z);ground.transform.localScale=new Vector3(district<0?710:155,2,district<0?510:150);ground.GetComponent<Renderer>().sharedMaterial=Material("Warm Sand.mat");Object.DestroyImmediate(ground.GetComponent<Collider>());}
            var rt=new RenderTexture(1400,900,24);var old=RenderTexture.active;var image=new Texture2D(1400,900,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1400,900),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report("Foundry-"+(district<0?"island":district.ToString("D2"))+"-"+state+".png"),image.EncodeToPNG());}
            finally{RenderTexture.active=old;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(sunObject);Object.DestroyImmediate(ground);}
        }
        static IEnumerator GameCheck()
        {
            var report=new StringBuilder("Mayhem Foundry production Game.unity smoke / traversal / bots / tsunami. Desktop Editor; not Quest or live network certification.\n");
            string path=ProjectPath.Report("MayhemFoundry-game.txt");
            var session=Services.Session;float timeout=Time.realtimeSinceStartup+30;
            while(session?.SurvivorBots==null&&Time.realtimeSinceStartup<timeout){yield return null;session=Services.Session;}
            if(session?.SurvivorBots==null)throw new InvalidOperationException("Game session unavailable.");
            var old=Services.World as Component;bool oldActive=old&&old.gameObject.activeSelf;
            GameObject root=null,wave=null;bool background=Application.runInBackground;
            int vsync=QualitySettings.vSyncCount,rate=Application.targetFrameRate;float scale=Time.timeScale;
            float audio=AudioListener.volume;
            bool voice=session.Social?.VoiceEnabled??false;var random=UnityEngine.Random.state;
            try
            {
                Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;Time.timeScale=1;AudioListener.volume=0;
                session.Demo?.Run(DemoCommand.StopDisasters);session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots?.Clear();
                if(session.Social!=null)session.Social.VoiceEnabled=false;
                if(old)old.gameObject.SetActive(false);root=Create(-1,SceneManager.GetActiveScene());var map=root.GetComponent<MayhemFoundryMap>();
                if(MatchRuntime.Current?.Rules!=null){var rules=JsonUtility.FromJson<ModeRules>(JsonUtility.ToJson(MatchRuntime.Current.Rules));rules.island=IslandRule.Walkable;MatchRuntime.Current.Configure(rules);}
                session.SurvivorBots.MapRebuilt();session.SetRole(true);session.SetMenu(true);
                var camera=session.ActiveCamera;if(camera){camera.transform.position=new Vector3(490,400,-580);camera.transform.LookAt(new Vector3(0,10,0));camera.farClipPlane=2500;}
                float until=Time.realtimeSinceStartup+4;while(Time.realtimeSinceStartup<until)yield return null;
                report.AppendLine("CENSUS bodies="+map.Blocks.Count+" colliders="+root.GetComponentsInChildren<Collider>().Length+" GameObjects="+root.GetComponentsInChildren<Transform>().Length+" joints="+root.GetComponentsInChildren<Joint>().Length+" unified="+TraversalCollision.Unified+" sharedModels="+MayhemStructuralMember.CachedModelCount+" sharedConvexMeshes="+MayhemStructuralMember.CachedCollisionMeshCount+" submittedInstances="+DeadlyJenga.CandyArt.StructuralMeshBatches.SubmittedInstances+" structuralDraws="+DeadlyJenga.CandyArt.StructuralMeshBatches.DrawCalls);
                Capture(map,root.scene,-1,"intact");
                for(int d=0;d<10;d++)Capture(map,root.scene,d,"intact");
                var frames=new List<float>();until=Time.realtimeSinceStartup+4;
                while(Time.realtimeSinceStartup<until){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}Frames(report,"early idle / settling",frames);
                var walking=Walk(map,session,report);while(walking.MoveNext())yield return walking.Current;
                frames.Clear();until=Time.realtimeSinceStartup+4;while(Time.realtimeSinceStartup<until){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}Frames(report,"settled idle",frames);
                File.WriteAllText(path,report.ToString());
                session.SurvivorBots.Running=true;for(int i=0;i<32;i++)session.SurvivorBots.SpawnOne();
                until=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<until)yield return null;
                var bots=session.SurvivorBots.Members.Where(b=>b).ToArray();var positions=bots.Select(b=>b.transform.position).ToArray();
                StartCounters();
                frames.Clear();until=Time.realtimeSinceStartup+5;while(Time.realtimeSinceStartup<until){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}
                Frames(report,"32 requested bots / quiet",frames);report.AppendLine("BOT OBSERVATION spawned="+bots.Length+" moved>.5m="+bots.Where((b,i)=>b&&Vector3.Distance(b.transform.position,positions[i])>.5f).Count()+"; short movement observation only.");
                StopCounters(report);
                if(camera){camera.transform.position=MayhemStructureLayout.Centers[0]+new Vector3(0,1.6f,-40);camera.transform.LookAt(MayhemStructureLayout.Centers[0]+new Vector3(0,9,0));}
                until=Time.realtimeSinceStartup+1;while(Time.realtimeSinceStartup<until)yield return null;
                frames.Clear();until=Time.realtimeSinceStartup+4;while(Time.realtimeSinceStartup<until){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}Frames(report,"32 bots / nearby district view",frames);
                if(camera){camera.transform.position=new Vector3(490,400,-580);camera.transform.LookAt(new Vector3(0,10,0));}
                var prefab=Resources.Load<GameObject>("DemoDisasters/Tsunami");if(!prefab)throw new InvalidOperationException("Missing tsunami prefab.");
                UnityEngine.Random.InitState(71293);wave=Object.Instantiate(prefab);var hazard=wave.GetComponent<IHazard>();var component=hazard as Component;
                Set(component,"randomDirection",false);Set(component,"approachHeading",0f);Set(component,"warningTime",0f);Set(component,"offshoreDistance",0f);Set(component,"riseTime",.1f);
                var before=map.Blocks.Select(b=>b.worldCenterOfMass).ToArray();hazard.Begin();frames.Clear();until=Time.realtimeSinceStartup+120;
                while(hazard.Active&&Time.realtimeSinceStartup<until){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}
                Frames(report,"bots + actual tsunami / full route or 120 second limit",frames);bool complete=!hazard.Active;hazard.End();
                report.AppendLine("WAVE full route completed="+complete+"; members moved>.5m="+map.Blocks.Where((b,i)=>b&&Vector3.Distance(b.worldCenterOfMass,before[i])>.5f).Count()+"/"+map.Blocks.Count+". Default height, force and speed retained.");
                Capture(map,root.scene,-1,"tsunami");report.AppendLine("COMPLETE runtimeErrors="+errors);File.WriteAllText(path,report.ToString());
            }
            finally
            {
                StopCounters(report);
                if(wave){wave.GetComponent<IHazard>()?.End();Object.Destroy(wave);}session.SurvivorBots.Clear();
                if(root){root.SetActive(false);Object.Destroy(root);}if(old)old.gameObject.SetActive(oldActive);Services.World=old as IWorldQuery;
                Application.runInBackground=background;QualitySettings.vSyncCount=vsync;Application.targetFrameRate=rate;Time.timeScale=scale;AudioListener.volume=audio;UnityEngine.Random.state=random;
                if(session.Social!=null)session.Social.VoiceEnabled=voice;
            }
        }
        static void Set(Component c,string field,object value){c?.GetType().GetField(field)?.SetValue(c,value);}
        static void StartCounters()
        {
            var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);
            foreach(var handle in handles)
            {
                var d=ProfilerRecorderHandle.GetDescription(handle);
                if(d.Name=="Physics.Simulate"||d.Name=="Main Thread"||d.Name=="Render Thread"||d.Name=="GC Allocated In Frame"||d.Name.StartsWith("DJ."))counters.Add((d.Name,ProfilerRecorder.StartNew(d.Category,d.Name,1024)));
            }
        }
        static void StopCounters(StringBuilder report)
        {
            foreach(var c in counters)
            {
                var samples=new List<ProfilerRecorderSample>();c.recorder.CopyTo(samples);
                if(samples.Count>0)report.AppendLine("COUNTER "+c.name+" mean="+(samples.Average(s=>(double)s.Value)/(c.name=="GC Allocated In Frame"?1:1000000)).ToString("F3")+(c.name=="GC Allocated In Frame"?" bytes/frame":" ms/frame (nested scopes)"));
                c.recorder.Dispose();
            }
            counters.Clear();
        }
        static void Frames(StringBuilder report,string label,List<float> frames)
        {if(frames.Count==0){report.AppendLine("FAIL no sampled frames");return;}frames.Sort();report.AppendLine($"EDITOR [{label}] median={frames[frames.Count/2]:F2}ms p95={frames[(int)((frames.Count-1)*.95f)]:F2}ms n={frames.Count}. Unfocused desktop Editor, no FPS cap; not a device prediction.");}
        static IEnumerator Walk(MayhemFoundryMap map,ISessionInfo session,StringBuilder report)
        {
            var actor=session.SurvivorBots.SpawnOne();var motor=actor?actor.GetComponent<CastleSurvivor>():null;if(!motor){report.AppendLine("FAIL production survivor unavailable");yield break;}
            foreach(var control in actor.GetComponents<MonoBehaviour>())if(control is DeadlyJenga.CastleBots.ICastleSurvivorDriver)control.enabled=false;
            bool enabled=motor.enabled,external=motor.externallyDriven;var mode=motor.controlMode;var driver=motor.InputDriver;
            try
            {
                session.SetMenu(false);motor.enabled=false;motor.externallyDriven=true;motor.InputDriver=null;motor.RefreshMode();motor.InputSuspended=false;
                var origin=MayhemStructureLayout.Centers[0]+new Vector3(-51,1,-17.5f);
                var goals=new List<Vector3>{origin+new Vector3(14,4.15f,7.9f),origin+new Vector3(14,4.15f,0),origin+new Vector3(10,4.15f,0),origin+new Vector3(10,4.15f,-6),origin+new Vector3(14,4.15f,-6),origin+new Vector3(14,4.15f,-7.9f),origin+new Vector3(4.3f,7.65f,-7.9f),origin+new Vector3(4.3f,7.65f,7.9f),origin+new Vector3(14,11.15f,7.9f),origin+new Vector3(14,11.15f,6),origin+new Vector3(5.3f,11.15f,6)};
                motor.Teleport(origin+new Vector3(5.3f,.06f,7.9f),Quaternion.LookRotation(Vector3.right));
                var bookWalk=Drive(motor,goals,50,n=>report.AppendLine((n==goals.Count?"PASS ":"FAIL ")+"production motor Bookend ascent "+n+"/"+goals.Count+" landings; feet="+motor.transform.position));while(bookWalk.MoveNext())yield return bookWalk.Current;File.WriteAllText(ProjectPath.Report("MayhemFoundry-game.txt"),report.ToString());
                var portal=MayhemStructureLayout.Centers[6]+new Vector3(-45,0,-30);
                var portalGoals=new List<Vector3>{portal+new Vector3(4,5,-2.2f),portal+new Vector3(4,5,2.2f),portal+new Vector3(-4,9,2.2f),portal+new Vector3(-4,9,-2.2f),portal+new Vector3(4,13,-2.2f)};
                motor.Teleport(portal+new Vector3(-3,1.06f,-2.2f),Quaternion.LookRotation(Vector3.right));
                var portalWalk=Drive(motor,portalGoals,30,n=>report.AppendLine((n==portalGoals.Count?"PASS ":"FAIL ")+"production motor Portal ascent "+n+"/"+portalGoals.Count+" landings; feet="+motor.transform.position));while(portalWalk.MoveNext())yield return portalWalk.Current;File.WriteAllText(ProjectPath.Report("MayhemFoundry-game.txt"),report.ToString());
                var harbor=MayhemStructureLayout.Centers[9]+new Vector3(-43.5f,0,-26);
                var harborGoals=new List<Vector3>{harbor+new Vector3(3,4.35f,-6.5f),harbor+new Vector3(3.3f,4.325f,-10),harbor+new Vector3(-3,8.35f,-10),harbor+new Vector3(-3.3f,8.325f,-6.5f),harbor+new Vector3(3,12.35f,-6.5f),harbor+new Vector3(3.3f,12.35f,-4.8f),harbor+new Vector3(2.3f,12.35f,-4.8f),harbor+new Vector3(2.3f,12.35f,-2.6f),harbor+new Vector3(0,12.35f,-2.6f)};
                motor.Teleport(harbor+new Vector3(-3,2.06f,-6.5f),Quaternion.LookRotation(Vector3.right));
                var harborWalk=Drive(motor,harborGoals,35,n=>report.AppendLine((n==harborGoals.Count?"PASS ":"FAIL ")+"production motor Harbor stair-to-leaf "+n+"/"+harborGoals.Count+" landings; feet="+motor.transform.position));while(harborWalk.MoveNext())yield return harborWalk.Current;File.WriteAllText(ProjectPath.Report("MayhemFoundry-game.txt"),report.ToString());
                motor.controlMode=FlyingHand.ControlMode.VR;motor.externallyDriven=false;motor.RefreshMode();motor.InputSuspended=false;
                motor.Teleport(origin+new Vector3(-3.2f,2.8f,0),Quaternion.LookRotation(Vector3.right));motor.viewCamera.transform.localRotation=Quaternion.identity;
                bool attached=false;float start=motor.transform.position.y;
                for(int i=0;i<100;i++){motor.Simulate(Vector2.up,false,.02f,true);attached|=motor.Climbing;yield return null;}
                report.AppendLine((attached&&motor.transform.position.y>start+1?"PASS ":"FAIL ")+"VR motor climbs flush bookend through floor band: attached="+attached+" rise="+(motor.transform.position.y-start).ToString("F2")+"m. Simulator motor only, no tracked headset.");
            }
            finally{motor.enabled=enabled;motor.externallyDriven=external;motor.controlMode=mode;motor.InputDriver=driver;motor.RefreshMode();motor.InputSuspended=true;session.SurvivorBots.Clear();session.SetRole(true);session.SetMenu(true);}
        }
        static IEnumerator Drive(CastleSurvivor motor,IReadOnlyList<Vector3> goals,float seconds,Action<int> finished)
        {
            int goal=0;float until=Time.realtimeSinceStartup+seconds;
            while(goal<goals.Count&&Time.realtimeSinceStartup<until)
            {
                var target=goals[goal];var delta=Vector3.ProjectOnPlane(target-motor.transform.position,Vector3.up);
                if(delta.magnitude<.04f&&Mathf.Abs(target.y-motor.transform.position.y)<.1f){goal++;continue;}
                if(delta.sqrMagnitude>.001f){motor.transform.rotation=Quaternion.LookRotation(delta);motor.viewCamera.transform.localRotation=Quaternion.identity;}
                motor.InputSuspended=false;float remaining=Mathf.Min(Time.deltaTime,.1f);
                while(remaining>.0001f){float dt=Mathf.Min(.02f,remaining);float distance=Vector3.ProjectOnPlane(target-motor.transform.position,Vector3.up).magnitude;float amount=Mathf.Clamp01(distance/Mathf.Max(.001f,motor.EffectiveWalkSpeed*dt));motor.Simulate(Vector2.up*amount,false,dt,false);remaining-=dt;}
                yield return null;
            }
            finished(goal);
        }
    }
}




