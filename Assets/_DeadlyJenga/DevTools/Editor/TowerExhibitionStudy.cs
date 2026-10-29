using System;
using System.Collections;
using System.Collections.Generic;
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

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class TowerExhibitionStudy
    {
        const string Key="TowerExhibitionStudy";
        static IEnumerator routine;
        static int errors;
        static bool city;
        static string Id=>city?"city-tower-exhibition":"tower-lab-island";
        static string Label=>city?"City Buildings 11–20":"Tower Laboratory 1–10";
        static string Prefix=>city?"CityExhibition":"TowerExhibition";
        [Serializable] sealed class Saved {public SceneSetup[] scenes;}
        static TowerExhibitionStudy(){EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=State;}
        static void Poll()
        {
            if(SessionState.GetBool(Key,false)&&EditorApplication.isPlaying&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!SessionState.GetBool(Key+"Finishing",false)&&routine==null){Start();return;}
            string request=ProjectPath.Report(Key+".request");
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(!ValidationRunGate.TryAcquire(Key))return;
            string phase=File.ReadAllText(request).Trim();File.Delete(request);
            try
            {
                if(phase=="build"){Build();ValidationRunGate.Release(Key);return;}
                var scenes=EditorSceneManager.GetSceneManagerSetup();
                if(scenes.Any(s=>s.isLoaded&&(string.IsNullOrEmpty(s.path)||SceneManager.GetSceneByPath(s.path).isDirty)))throw new InvalidOperationException("Unsaved scenes left untouched.");
                SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Saved{scenes=scenes}));SessionState.SetString(Key+"Phase",phase);
                SessionState.SetBool(Key+"Finishing",false);SessionState.SetBool(Key,true);
                if(phase=="game"||phase=="routes"||phase=="stress")EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                EditorApplication.EnterPlaymode();
            }
            catch(Exception e){File.AppendAllText(ProjectPath.Report(Key+"-error.txt"),e+"\n");ValidationRunGate.Release(Key);UnityEngine.Debug.LogException(e);}
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)Start();
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;routine=null;SessionState.SetBool(Key,false);
            if(SessionState.GetBool(Key+"Locked",false)){SessionState.SetBool(Key+"Locked",false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();}
            var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Scenes",""));if(saved!=null)EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
            if(SessionState.GetString(Key+"Phase","")=="game"||SessionState.GetString(Key+"Phase","")=="stress")foreach(bool c in new[]{true,false}){city=c;InstallPreview();}
            ValidationRunGate.Release(Key);
        }
        static void Start()
        {
            if(routine!=null)return;
            EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();SessionState.SetBool(Key+"Locked",true);
            errors=0;Application.logMessageReceived+=Log;
            string phase=SessionState.GetString(Key+"Phase","");routine=phase=="routes"?RouteReviews():phase=="game"||phase=="stress"?Games():PhysicsChecks();EditorApplication.update+=Tick;
        }
        static void Log(string msg,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(ProjectPath.Report(Key+"-errors.txt"),msg+"\n"+trace+"\n");}}
        static void Tick()
        {
            if(routine==null)return;
            try{if(routine.MoveNext())return;}catch(Exception e){File.AppendAllText(ProjectPath.Report(Key+"-error.txt"),e+"\n");UnityEngine.Debug.LogException(e);}
            (routine as IDisposable)?.Dispose();routine=null;EditorApplication.update-=Tick;SessionState.SetBool(Key+"Finishing",true);EditorApplication.ExitPlaymode();
        }
        static string Folder(string parent,string name){string p=parent+"/"+name;if(!AssetDatabase.IsValidFolder(p))AssetDatabase.CreateFolder(parent,name);return p;}
        [MenuItem("Tools/Deadly Jenga/Maps/Build Both Tower Exhibition Islands")]
        public static void Build()
        {
            var previous=Services.World;var report=new StringBuilder();
            foreach(bool c in new[]{true,false})
            {
                city=c;var root=new GameObject(Label);
                try
                {
                    var map=root.AddComponent<TowerExhibitionMap>();map.city=city;map.palette=Palette();
                    map.friction=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(ProjectPath.Find("Castle Friction.physicMaterial"));
                    string maps=Path.GetDirectoryName(ProjectPath.FolderOf("TowerExhibitionMap.cs").TrimEnd('/')).Replace('\\','/');
                    string resources=Folder(maps,"Resources"),output=Folder(resources,"ModeMaps");
                    var catalog=Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");if(!catalog)throw new InvalidOperationException("Missing lobby catalog.");
                    var floor=new GameObject("Continuous shore-matched island foundation");floor.transform.SetParent(root.transform,false);map.foundation=floor.transform;
                    map.islandCollision=IslandCollider(map,catalog,resources);floor.AddComponent<MeshCollider>().sharedMesh=map.islandCollision;
                    var signs=PrefabUtility.InstantiatePrefab(Signs(map.palette)) as GameObject;signs.name="Visual";signs.transform.SetParent(root.transform,false);
                    string path=output+"/Mode_"+Id+".prefab";PrefabUtility.SaveAsPrefabAsset(root,path);
                    var entries=new List<IslandLobbyCatalog.Map>(catalog.maps);var entry=entries.Find(e=>e.id==Id);
                    if(entry==null){entry=new IslandLobbyCatalog.Map{id=Id};entries.Add(entry);}
                    entry.title=Label;entry.mapPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);entry.scenePath=ProjectPath.Find("Game.unity");
                    entry.walkableIsland=true;entry.recommendedMode="demo";entry.recommendedPlayers=24;entry.supportedModes=Array.Empty<string>();catalog.maps=entries.ToArray();EditorUtility.SetDirty(catalog);
                    var layout=TowerExhibitionLayout.Build(city);report.AppendLine(Label+": selectable "+path+"; bodies="+layout.Count+" collisionShapes="+layout.Sum(m=>m.shapes.Count)+" island="+map.islandCollision.bounds.size);
                }
                finally{Object.DestroyImmediate(root);Services.World=previous;}
            }
            AssetDatabase.SaveAssets();File.WriteAllText(ProjectPath.Report(Key+"-build.txt"),report.ToString());EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup");
        }
        static Material[] Palette()
        {
            string visuals=Path.GetDirectoryName(ProjectPath.FolderOf("CandyEnvironment.cs").TrimEnd('/')).Replace('\\','/');
            string folder=Folder(Folder(visuals,"TowerExhibition"),"Materials");
            var colors=new[]{new Color(.12f,.74f,.56f),new Color(.95f,.26f,.26f),new Color(.55f,.32f,.95f),new Color(1,.84f,.5f)};
            var names=new[]{"Jade candy piers","Coral candy floors","Violet candy routes","Vanilla candy courts"};var result=new Material[4];
            for(int i=0;i<4;i++)
            {
                string path=folder+"/"+names[i]+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=names[i]};mat.color=colors[i];mat.SetFloat("_Smoothness",.67f);mat.SetFloat("_Metallic",.08f);mat.enableInstancing=true;AssetDatabase.CreateAsset(mat,path);}
                result[i]=mat;
            }
            return result;
        }
        static Mesh IslandCollider(TowerExhibitionMap map,IslandLobbyCatalog catalog,string output)
        {
            var environment=catalog.environmentPrefab?catalog.environmentPrefab.GetComponent<DeadlyJenga.CandyArt.CandyEnvironment>():null;
            var profile=catalog.art as DeadlyJenga.CandyArt.CandyArtProfile;if(!profile&&environment)profile=environment.profile;
            if(!profile)throw new InvalidOperationException("Missing island art profile.");
            var probe=new GameObject("Temporary shore sampler");var terrain=probe.AddComponent<DeadlyJenga.CandyArt.CandySandbar>();
            try
            {
                terrain.profile=profile;terrain.map=map;terrain.Refresh();var vertices=new List<Vector3>{new(0,-.02f,0)};var triangles=new List<int>();const int sides=128;
                for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;var dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));float low=0,high=700;for(int n=0;n<20;n++){float mid=(low+high)*.5f;if(terrain.ShapeDistance(dir*mid)<-1)low=mid;else high=mid;}var p=dir*low;vertices.Add(new(p.x,-.02f,p.y));}
                for(int i=0;i<sides;i++){triangles.Add(0);triangles.Add(1+(i+1)%sides);triangles.Add(1+i);}
                var mesh=new Mesh{name=Label+" ground"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                string path=output+"/"+Id+"-island.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);return saved;}AssetDatabase.CreateAsset(mesh,path);return mesh;
            }
            finally{Object.DestroyImmediate(probe);}
        }
        static GameObject Signs(Material[] palette)
        {
            string visuals=Path.GetDirectoryName(ProjectPath.FolderOf("CandyEnvironment.cs").TrimEnd('/')).Replace('\\','/');
            string path=Folder(visuals,"TowerExhibition")+"/"+Id+"-wayfinding.prefab";var root=new GameObject(Label+" signs");var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            try
            {
                for(int d=0;d<10;d++)
                {
                    var sign=new GameObject("District "+d);sign.transform.SetParent(root.transform,false);sign.transform.localPosition=TowerExhibitionLayout.Centers[d]+new Vector3(0,2,-43);
                    var text=sign.AddComponent<TextMesh>();text.text=(d+(city?11:1)).ToString("D2")+"  "+TowerExhibitionLayout.Names(city)[d];text.font=font;text.fontSize=64;text.characterSize=.55f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.14f,.07f,.25f);text.GetComponent<MeshRenderer>().sharedMaterial=font.material;
                }
                return PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{Object.DestroyImmediate(root);}
        }
        static TowerExhibitionMap Create(int district,Scene scene)
        {
            var root=Object.Instantiate(Resources.Load<GameObject>("ModeMaps/Mode_"+Id));SceneManager.MoveGameObjectToScene(root,scene);
            var map=root.GetComponent<TowerExhibitionMap>();map.generateOnStart=false;map.district=district;map.Rebuild();return map;
        }
        static IEnumerator PhysicsChecks()
        {
            var report=new StringBuilder("Native Unity PhysX: isolated 12-second settling, geometry, replicas and physical impact. Not FPS or headset certification.\n");
            var previous=Services.World;var prior=SceneManager.GetActiveScene();
            foreach(bool c in new[]{true,false})for(int d=0;d<10;d++)
            {
                city=c;var scene=SceneManager.CreateScene(Prefix+d+Guid.NewGuid().ToString("N"),new CreateSceneParameters(LocalPhysicsMode.Physics3D));SceneManager.SetActiveScene(scene);var map=Create(d,scene);var physics=scene.GetPhysicsScene();
                try
                {
                    var bodies=map.Blocks;var shapes=map.GetComponentsInChildren<Collider>().Where(s=>s.attachedRigidbody).ToArray();int overlaps=0;float deepest=0;var details=new List<string>();
                    for(int i=0;i<shapes.Length;i++)for(int j=i+1;j<shapes.Length;j++)
                    {
                        var a=shapes[i];var b=shapes[j];if(a.attachedRigidbody==b.attachedRigidbody||!a.bounds.Intersects(b.bounds))continue;
                        if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float depth)&&depth>.04f){overlaps++;deepest=Mathf.Max(deepest,depth);if(details.Count<12)details.Add(a.attachedRigidbody.name+" / "+b.attachedRigidbody.name+" "+depth.ToString("F2"));}
                    }
                    var start=bodies.Select(b=>b.position).ToArray();var angles=bodies.Select(b=>b.rotation).ToArray();
                    for(int s=0;s<600;s++){physics.Simulate(.02f);if(s%100==99)yield return null;}
                    float drift=bodies.Select((b,i)=>Vector3.Distance(b.position,start[i])).Max(),tilt=bodies.Select((b,i)=>Quaternion.Angle(b.rotation,angles[i])).Max();
                    report.AppendLine($"{(city?11:1)+d:D2} {TowerExhibitionLayout.Names(city)[d]} bodies={bodies.Count} shapes={shapes.Length} overlaps>.04={overlaps} deepest={deepest:F3} drift={drift:F3} tilt={tilt:F2} "+(drift<.6f&&tilt<5&&overlaps==0?"PASS":"REVIEW"));
                    if(details.Count>0)report.AppendLine(string.Join("; ",details));
                    if(drift>.6f)report.AppendLine("MOVED "+string.Join("; ",bodies.Select((b,i)=>new{b,d=Vector3.Distance(b.position,start[i])}).OrderByDescending(x=>x.d).Take(5).Select(x=>x.b.name+" "+x.d.ToString("F2"))));
                    Capture(map,d,"settled");
                    int samples=0;var points=new List<Vector3>();foreach(var rb in bodies){DeadlyJenga.CastleBots.BotGeometrySamples.Fill(rb,points);samples+=points.Count;}
                    string saved=map.SaveLayout();int rev=map.Revision;bool rejects=!map.LoadLayout(saved,bodies.Count+1,new Pose(Vector3.zero,Quaternion.identity),Vector3.one)&&map.Revision==rev;
                    map.SetReplica(true);bool replica=bodies.All(b=>b.isKinematic);map.SetReplica(false);report.AppendLine("Contracts wrongCountRejected="+rejects+" replica="+replica+" dynamic="+bodies.All(b=>!b.isKinematic)+" botSamples="+samples);
                    if(!city&&(d==5||d==8||d==9))
                    {
                        string purpose=d==5?"Removable drop shoe":d==8?"Wing receiving foot":"Loose crown compression keystone";
                        var removed=bodies.Where(b=>b.name.Contains(purpose)).Take(d==5?2:1).ToArray();
                        var mechanismStart=bodies.Select(b=>b.position).ToArray();foreach(var rb in removed)rb.gameObject.SetActive(false);foreach(var rb in bodies)if(rb.gameObject.activeSelf)rb.WakeUp();
                        for(int s=0;s<300;s++){physics.Simulate(.02f);if(s%100==99)yield return null;}
                        report.AppendLine("MECHANISM removed="+purpose+" members="+removed.Length+" others moved>.5m="+bodies.Where((b,i)=>b.gameObject.activeSelf&&Vector3.Distance(b.position,mechanismStart[i])>.5f).Count());
                        if(d==5){var cassette=bodies.First(b=>b.name.Contains("Two-floor dropping cassette"));int index=bodies.ToList().IndexOf(cassette);report.AppendLine("Drop cassette vertical travel="+(mechanismStart[index].y-cassette.position.y).ToString("F3")+" speed="+cassette.linearVelocity.magnitude.ToString("F3"));}
                        Capture(map,d,"released");
                    }
                    var before=bodies.Select(b=>b.position).ToArray();var hit=bodies.Where(b=>b.position.y>1&&b.position.y<18).FirstOrDefault();
                    if(hit)hit.AddForce(new Vector3(9,0,2),ForceMode.VelocityChange);
                    for(int s=0;s<600;s++){physics.Simulate(.02f);if(s%100==99)yield return null;}
                    report.AppendLine("Impact 9m/s on lower inhabited member: moved>.5m="+bodies.Where((b,i)=>Vector3.Distance(b.position,before[i])>.5f).Count()+"/"+bodies.Count);Capture(map,d,"impact");
                    File.WriteAllText(ProjectPath.Report(Key+"-physics.txt"),report.ToString());
                }
                finally{map.gameObject.SetActive(false);Object.Destroy(map.gameObject);SceneManager.SetActiveScene(prior);SceneManager.UnloadSceneAsync(scene);Services.World=previous;}
                yield return null;
            }
            report.AppendLine("COMPLETE runtimeErrors="+errors);File.WriteAllText(ProjectPath.Report(Key+"-physics.txt"),report.ToString());
        }
        static void Capture(TowerExhibitionMap map,int d,string state)
        {
            var scene=map.gameObject.scene;var go=new GameObject("Exhibition evidence camera");SceneManager.MoveGameObjectToScene(go,scene);var camera=go.AddComponent<Camera>();camera.scene=scene;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.28f,.65f,.88f);camera.fieldOfView=48;camera.farClipPlane=2500;
            var target=d<0?new Vector3(0,15,0):TowerExhibitionLayout.Centers[d]+Vector3.up*23;
            camera.transform.position=target+new Vector3(.6f,.55f,-1).normalized*(d<0?900:170);camera.transform.LookAt(target);
            bool sunExists=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Any(l=>l.gameObject.scene==scene&&l.enabled&&l.type==LightType.Directional);
            var sunObject=new GameObject("Exhibition review sun");SceneManager.MoveGameObjectToScene(sunObject,scene);var sun=sunObject.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=sunExists?0:1.4f;sun.transform.rotation=Quaternion.Euler(48,-35,0);
            GameObject ground=null;
            if(!Object.FindObjectsByType<DeadlyJenga.CandyArt.CandyEnvironment>(FindObjectsSortMode.None).Any(e=>e.gameObject.scene==scene&&e.enabled))
            {ground=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(ground,scene);ground.transform.position=new Vector3(target.x,-1.1f,target.z);ground.transform.localScale=new Vector3(d<0?710:180,2,d<0?510:180);ground.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(ProjectPath.Find("Warm Sand.mat"));Object.DestroyImmediate(ground.GetComponent<Collider>());}
            var rt=new RenderTexture(1600,1000,24);var old=RenderTexture.active;var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report(Prefix+"-"+(d<0?"island":(d+(city?11:1)).ToString("D2"))+"-"+state+".png"),image.EncodeToPNG());}
            finally{RenderTexture.active=old;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);Object.DestroyImmediate(go);Object.DestroyImmediate(sunObject);Object.DestroyImmediate(ground);}
        }
        static IEnumerator Games()
        {
            foreach(bool c in new[]{true,false}){city=c;var game=Game(null,SessionState.GetString(Key+"Phase","")=="stress");while(game.MoveNext())yield return game.Current;yield return null;}
        }
        static IEnumerator RouteReviews()
        {
            foreach(bool c in new[]{true,false}){city=c;var game=Game(city?new[]{0}:new[]{0,9});while(game.MoveNext())yield return game.Current;yield return null;}
        }
        static IEnumerator Game(int[] only=null,bool stressOnly=false)
        {
            bool focused=only!=null;var report=new StringBuilder(Label+" production Game.unity. Desktop Editor; not headset/network certification.\n");string path=ProjectPath.Report(Prefix+(focused?"-routes.txt":"-game.txt"));
            var session=Services.Session;float timeout=Time.realtimeSinceStartup+30;while(session?.SurvivorBots==null&&Time.realtimeSinceStartup<timeout){yield return null;session=Services.Session;}
            if(session?.SurvivorBots==null)throw new InvalidOperationException("Session unavailable.");
            var old=Services.World as Component;bool active=old&&old.gameObject.activeSelf;TowerExhibitionMap map=null;GameObject wave=null;
            bool background=Application.runInBackground,voice=session.Social?.VoiceEnabled??false;int vsync=QualitySettings.vSyncCount,rate=Application.targetFrameRate;float scale=Time.timeScale,audio=AudioListener.volume;var random=UnityEngine.Random.state;
            try
            {
                Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;Time.timeScale=1;AudioListener.volume=0;
                session.Demo?.Run(DemoCommand.StopDisasters);session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots?.Clear();if(session.Social!=null)session.Social.VoiceEnabled=false;
                if(old)old.gameObject.SetActive(false);map=Create(-1,SceneManager.GetActiveScene());
                if(MatchRuntime.Current?.Rules!=null){var rules=JsonUtility.FromJson<ModeRules>(JsonUtility.ToJson(MatchRuntime.Current.Rules));rules.island=IslandRule.Walkable;MatchRuntime.Current.Configure(rules);}
                session.SurvivorBots.MapRebuilt();session.SetRole(true);session.SetMenu(true);var cam=session.ActiveCamera;
                if(cam){cam.transform.position=new(480,360,-650);cam.transform.LookAt(new Vector3(0,20,0));cam.farClipPlane=2500;}
                float until=Time.realtimeSinceStartup+7;while(Time.realtimeSinceStartup<until)yield return null;
                report.AppendLine("CENSUS bodies="+map.Blocks.Count+" shapes="+map.GetComponentsInChildren<Collider>().Count(s=>s.attachedRigidbody)+" joints="+map.GetComponentsInChildren<Joint>().Length+" unified="+TraversalCollision.Unified);
                if(!focused){Capture(map,-1,"intact");for(int d=0;d<10;d++)Capture(map,d,"intact");}
                var frames=new List<float>();until=Time.realtimeSinceStartup+4;while(Time.realtimeSinceStartup<until){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}Frames(report,"quiet whole island",frames);
                if(!stressOnly){var walk=Walk(map,session,report,path,only);while(walk.MoveNext())yield return walk.Current;}
                else report.AppendLine("Traversal covered by the preceding full suite and focused follow-up; this repeat checks only the new cached water loading. Geometry and physical mass unchanged.");
                File.WriteAllText(path,report.ToString());
                if(focused){report.AppendLine("Focused follow-up: 16cm waypoint tolerance (well inside the landing), three setup frames, explicit flight-bearing section selection. No wave or demolition repeated. COMPLETE runtimeErrors="+errors);File.WriteAllText(path,report.ToString());yield break;}
                session.SurvivorBots.Running=true;for(int i=0;i<32;i++)session.SurvivorBots.SpawnOne();until=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<until)yield return null;
                var bots=session.SurvivorBots.Members.Where(b=>b).ToArray();var positions=bots.Select(b=>b.transform.position).ToArray();frames.Clear();until=Time.realtimeSinceStartup+5;
                while(Time.realtimeSinceStartup<until){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}Frames(report,"32 requested bots / quiet whole island",frames);report.AppendLine("Bots spawned="+bots.Length+" moved>.5m="+bots.Where((b,i)=>b&&Vector3.Distance(b.transform.position,positions[i])>.5f).Count());File.WriteAllText(path,report.ToString());
                var prefab=Resources.Load<GameObject>("DemoDisasters/Tsunami");if(!prefab)throw new InvalidOperationException("Missing tsunami.");UnityEngine.Random.InitState(71293);wave=Object.Instantiate(prefab);var hazard=wave.GetComponent<IHazard>();var component=hazard as Component;
                Set(component,"randomDirection",false);Set(component,"approachHeading",0f);Set(component,"warningTime",0f);Set(component,"offshoreDistance",0f);Set(component,"riseTime",.1f);
                var before=map.Blocks.Select(b=>b.worldCenterOfMass).ToArray();hazard.Begin();frames.Clear();until=Time.realtimeSinceStartup+120;
                while(hazard.Active&&Time.realtimeSinceStartup<until){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}Frames(report,"32 bots + actual tsunami",frames);bool complete=!hazard.Active;hazard.End();
                report.AppendLine("Wave completed="+complete+" moved>.5m="+map.Blocks.Where((b,i)=>b&&Vector3.Distance(b.worldCenterOfMass,before[i])>.5f).Count()+"/"+map.Blocks.Count+"; production default height/speed/force.");
                until=Time.realtimeSinceStartup+5;while(Time.realtimeSinceStartup<until)yield return null;Capture(map,-1,"tsunami");for(int d=0;d<10;d++)Capture(map,d,"tsunami");
                session.SurvivorBots.Clear();map.Rebuild();until=Time.realtimeSinceStartup+4;while(Time.realtimeSinceStartup<until)yield return null;
                var intact=map.Blocks.Select(b=>b.worldCenterOfMass).ToArray();
                for(int d=0;d<10;d++)
                {
                    var member=map.Blocks.FirstOrDefault(b=>b.GetComponent<MayhemStructuralMember>().District==d&&b.position.y>.5f&&b.position.y<20);
                    if(member)member.AddForceAtPosition(new Vector3(d%2==0?22:-22,0,9),member.worldCenterOfMass+Vector3.up*4,ForceMode.VelocityChange);
                }
                until=Time.realtimeSinceStartup+14;while(Time.realtimeSinceStartup<until)yield return null;
                report.AppendLine("Separate DEMOLITION trial: reset intact map, one 22m/s X + 9m/s Z off-centre pulse per district, applied 4m above lower section COM. No bots. Moved>.5m="+map.Blocks.Where((b,i)=>b&&Vector3.Distance(b.worldCenterOfMass,intact[i])>.5f).Count()+"/"+map.Blocks.Count+". Not a production disaster or FPS benchmark.");
                Capture(map,-1,"demolition");for(int d=0;d<10;d++)Capture(map,d,"demolition");
                report.AppendLine("COMPLETE runtimeErrors="+errors);File.WriteAllText(path,report.ToString());
            }
            finally
            {
                if(wave){wave.GetComponent<IHazard>()?.End();Object.Destroy(wave);}session.SurvivorBots.Clear();if(map){map.gameObject.SetActive(false);Object.Destroy(map.gameObject);}if(old)old.gameObject.SetActive(active);Services.World=old as IWorldQuery;
                Application.runInBackground=background;QualitySettings.vSyncCount=vsync;Application.targetFrameRate=rate;Time.timeScale=scale;AudioListener.volume=audio;UnityEngine.Random.state=random;if(session.Social!=null)session.Social.VoiceEnabled=voice;
            }
        }
        static void Set(Component c,string field,object value){c?.GetType().GetField(field)?.SetValue(c,value);}
        static void Frames(StringBuilder report,string title,List<float> values)
        {if(values.Count<2)return;var samples=values.Skip(1).ToArray();var sorted=samples.OrderBy(x=>x).ToArray();report.AppendLine(title+": frames="+samples.Length+" mean="+samples.Average().ToString("F2")+"ms / "+(1000/samples.Average()).ToString("F1")+"FPS p95="+sorted[(int)((sorted.Length-1)*.95f)].ToString("F2")+"ms; first phase-boundary sample omitted; uncapped desktop Editor, not Quest.");}
        static bool FlightShape(MayhemShape s)=>s.vertices!=null&&s.vertices[6].y-s.vertices[4].y>1&&s.vertices[6].y-s.vertices[4].y<6&&Vector3.Distance(s.vertices[0],s.vertices[4])<.001f;
        static IEnumerator Walk(TowerExhibitionMap map,ISessionInfo session,StringBuilder report,string path,int[] only=null)
        {
            var actor=session.SurvivorBots.SpawnOne();var motor=actor?actor.GetComponent<CastleSurvivor>():null;if(!motor)throw new InvalidOperationException("Production survivor unavailable.");
            foreach(var driver in actor.GetComponents<MonoBehaviour>())if(driver is DeadlyJenga.CastleBots.ICastleSurvivorDriver)driver.enabled=false;
            motor.enabled=false;motor.externallyDriven=true;motor.InputDriver=null;motor.RefreshMode();session.SetMenu(false);motor.InputSuspended=false;
            for(int warm=0;warm<3;warm++){Physics.SyncTransforms();yield return null;}
            for(int d=0;d<10;d++)
            {
                if(only!=null&&!only.Contains(d))continue;
                var section=map.Blocks.Select((b,i)=>new{body=b,module=map.Authored[i]}).FirstOrDefault(s=>s.module.district==d&&s.body.position.y>.5f&&s.body.position.y<2&&s.module.shapes.Any(FlightShape));
                if(section==null){report.AppendLine("FAIL ROUTE "+d+" no ground flight-bearing section");continue;}
                var rb=section.body;var authored=section.module;
                // Read the first three route flights from authored convex shapes, preserving their real transform.
                var flights=authored.shapes.Where(FlightShape).GroupBy(s=>Mathf.Round(s.center.y*100)).Select(g=>g.First()).Take(3).ToArray();
                var firstRotation=Quaternion.Inverse(authored.shapes[0].rotation);
                Vector3 Point(MayhemShape s,int v)=>rb.transform.TransformPoint(firstRotation*(s.center+s.rotation*s.vertices[v]));
                var start=(Point(flights[0],4)+Point(flights[0],5))*.5f;motor.Teleport(start+Vector3.up*.07f,Quaternion.identity);
                int passed=0,flightIndex=0;
                foreach(var f in flights)
                {
                    var entry=(Point(f,4)+Point(f,5))*.5f;var end=(Point(f,6)+Point(f,7))*.5f;var delta=Vector3.ProjectOnPlane(end-entry,Vector3.up).normalized;
                    var goals=new List<Vector3>();if(flightIndex++>0)goals.Add(entry-delta*.9f);goals.Add(end-delta*.15f);goals.Add(end+delta*.9f);
                    foreach(var goal in goals)
                    {
                        float until=Time.realtimeSinceStartup+7;
                        while(Time.realtimeSinceStartup<until)
                        {
                            var distance=Vector3.ProjectOnPlane(goal-motor.transform.position,Vector3.up);
                            if(distance.magnitude<.16f&&Mathf.Abs(goal.y-motor.transform.position.y)<.16f){passed++;break;}
                            if(distance.sqrMagnitude>.0001f){motor.transform.rotation=Quaternion.LookRotation(distance);motor.viewCamera.transform.localRotation=Quaternion.identity;}
                            motor.InputSuspended=false;float remaining=Mathf.Min(Time.deltaTime,.1f);
                            while(remaining>.0001f){float dt=Mathf.Min(.02f,remaining);float amount=Mathf.Clamp01(Vector3.ProjectOnPlane(goal-motor.transform.position,Vector3.up).magnitude/Mathf.Max(.001f,motor.EffectiveWalkSpeed*dt));motor.Simulate(Vector2.up*amount,false,dt,false);remaining-=dt;}yield return null;
                        }
                    }
                    // Next flight alternates direction; both pads occupy the same side of the opening.
                    start=end;
                }
                int expected=flights.Length*3-1;
                report.AppendLine((passed==expected?"PASS":"REVIEW")+" production motor district "+(d+(city?11:1))+" ramp/landing checkpoints="+passed+"/"+expected+" feet="+motor.transform.position);File.WriteAllText(path,report.ToString());
            }
            var climb=map.Blocks.First(b=>b.GetComponent<MayhemStructuralMember>().District==0&&b.position.y>.5f&&b.position.y<2);
            var module=map.Authored[map.Blocks.ToList().IndexOf(climb)];var first=module.shapes[0];
            var face=climb.transform.TransformPoint(Quaternion.Inverse(first.rotation)*first.center)-climb.transform.right*(first.size.x*.5f+.2f);face.y=climb.position.y+2.8f;
            motor.controlMode=FlyingHand.ControlMode.VR;motor.externallyDriven=false;motor.RefreshMode();motor.InputSuspended=false;motor.Teleport(face,Quaternion.LookRotation(climb.transform.right));motor.viewCamera.transform.localRotation=Quaternion.identity;
            bool attached=false;float height=motor.transform.position.y;
            for(int i=0;i<120;i++){motor.InputSuspended=false;motor.Simulate(Vector2.up,false,.02f,true);attached|=motor.Climbing;yield return null;}
            report.AppendLine((attached&&motor.transform.position.y>height+1.5f?"PASS":"REVIEW")+" simulated VR motor clear-face climb across first floor band: attached="+attached+" rise="+(motor.transform.position.y-height).ToString("F2")+"m; no tracked headset.");
            session.SurvivorBots.Clear();session.SetRole(true);session.SetMenu(true);
        }
        static void InstallPreview()
        {
            string source=ProjectPath.Report(Prefix+"-island-intact.png");if(!File.Exists(source))return;
            string presentation=Path.GetDirectoryName(Path.GetDirectoryName(ProjectPath.FolderOf("CandyEnvironment.cs").TrimEnd('/'))).Replace('\\','/');
            string path=Folder(Folder(presentation,"Resources"),"LobbyPreviews")+"/"+Id+".png";File.WriteAllBytes(path,File.ReadAllBytes(source));AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.maxTextureSize=1024;importer.mipmapEnabled=false;importer.SaveAndReimport();
        }
    }
}
