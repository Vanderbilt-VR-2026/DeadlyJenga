using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using Object=UnityEngine.Object;
namespace DeadlyJenga.DevTools.Editor
{
 [InitializeOnLoad] public static class EpicCityValidation
 {
  const string Key="EpicCities.Validation";static IEnumerator run;static int errors,failures;static Action restore;
  [Serializable] sealed class Saved {public SceneSetup[] scenes;}
  static EpicCityValidation(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
  public static void Run(bool streetReview=false)
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before validation.");
   var scenes=EditorSceneManager.GetSceneManagerSetup();if(scenes.Any(x=>x.isLoaded&&(string.IsNullOrEmpty(x.path)||SceneManager.GetSceneByPath(x.path).isDirty)))throw new InvalidOperationException("Unsaved scenes left untouched.");
   if(!ValidationRunGate.TryAcquire(Key))throw new InvalidOperationException("Another validation is running.");
   File.WriteAllText(ProjectPath.Report("EpicCityRuntime-started.txt"),"Version="+EpicCityBuild.BuildVersion+" "+DateTime.Now.ToString("O"));
   SessionState.SetBool(Key+"Streets",streetReview);SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Saved{scenes=scenes}));EpicCityBuild.Build();SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
  }
  static void State(PlayModeStateChange state)
  {
   if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){SessionState.SetBool(Key,false);SessionState.SetBool(Key+"Running",true);EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();run=Games();Application.logMessageReceived+=Log;}
   if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"Running",false)){SessionState.SetBool(Key+"Running",false);(run as IDisposable)?.Dispose();run=null;restore?.Invoke();restore=null;Application.logMessageReceived-=Log;AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Scenes",""));if(saved!=null)EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);ValidationRunGate.Release(Key);}
  }
  static void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(ProjectPath.Report("EpicCityRuntime-errors.txt"),message+"\n"+stack+"\n");}}
  static void Tick()
  {
   if(run==null)return;
   try{if(run.MoveNext())return;}catch(Exception e){failures++;File.AppendAllText(ProjectPath.Report("EpicCityRuntime-errors.txt"),e.ToString());}
   (run as IDisposable)?.Dispose();run=null;restore?.Invoke();restore=null;Application.logMessageReceived-=Log;
   File.WriteAllText(ProjectPath.Report("EpicCityRuntime-complete.txt"),"COMPLETE failures="+failures+" runtimeErrors="+errors);AssetDatabase.SaveAssets();if(Application.isBatchMode)EditorApplication.Exit(failures==0&&errors==0?0:2);else EditorApplication.ExitPlaymode();
  }
  static IEnumerator Games()
  {
   bool streetReview=SessionState.GetBool(Key+"Streets",false);int botCount=streetReview?12:32;float waveSeconds=streetReview?16:100;
   var session=Services.Session;float end=Time.realtimeSinceStartup+40;while(session?.SurvivorBots==null&&Time.realtimeSinceStartup<end){yield return null;session=Services.Session;}
   if(session?.SurvivorBots==null)throw new InvalidOperationException("Game session did not start.");
   bool background=Application.runInBackground,voice=session.Social?.VoiceEnabled??false;int frameRate=Application.targetFrameRate,vsync=QualitySettings.vSyncCount;float volume=AudioListener.volume;
   restore=()=>{Application.runInBackground=background;Application.targetFrameRate=frameRate;QualitySettings.vSyncCount=vsync;AudioListener.volume=volume;if(session.Social!=null)session.Social.VoiceEnabled=voice;};
   Application.runInBackground=true;Application.targetFrameRate=-1;QualitySettings.vSyncCount=0;AudioListener.volume=0;
   session.Demo.Run(DemoCommand.StopDisasters);session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots?.Clear();if(session.Social!=null)session.Social.VoiceEnabled=false;
   for(int warm=0;warm<3;warm++)yield return null;
   // Scene providers can finish Start after the session; retire every prior world before introducing the map.
   foreach(var other in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))if(other is IWorldQuery&&!(other is ISessionInfo))other.gameObject.SetActive(false);
   for(int index=0;index<10;index++)
   {
    string path=ProjectPath.Report("EpicCity-"+(index+1).ToString("D2")+"-game.txt");var report=new StringBuilder(EpicCityLayout.Names[index]+" production Game.unity; actual motor, bots, default-force wave. Not Quest/network certification.\n");
    var prefab=Resources.Load<GameObject>("ModeMaps/Mode_"+EpicCityLayout.Ids[index]);if(!prefab)throw new InvalidOperationException("Missing map prefab "+index);
    var root=Object.Instantiate(prefab);var map=root.GetComponent<EpicCityMap>();map.generateOnStart=false;map.Rebuild();session.SurvivorBots.MapRebuilt();
    if(MatchRuntime.Current?.Rules!=null){var rules=JsonUtility.FromJson<ModeRules>(JsonUtility.ToJson(MatchRuntime.Current.Rules));rules.island=IslandRule.Walkable;MatchRuntime.Current.Configure(rules);}
    session.SetRole(true);session.SetMenu(true);var camera=session.ActiveCamera;if(camera){camera.transform.position=new Vector3(220,185,-300);camera.transform.LookAt(new Vector3(0,8,0));camera.farClipPlane=1600;}
    end=Time.realtimeSinceStartup+3;while(Time.realtimeSinceStartup<end)yield return null;
    report.AppendLine("Bodies="+map.Blocks.Count+" shapes="+map.Authored.Sum(m=>m.shapes.Count)+" routes="+map.Plan.routes.Count+" elevated="+map.Plan.routes.Count(r=>!r.name.StartsWith("Ground street"))+" lowerStructures="+map.Plan.sites.Count(s=>s.kind=="Street fabric")+" originalColliders="+TraversalCollision.Unified);
    report.AppendLine("Active world="+Services.World?.GetType().Name);File.WriteAllText(path,report.ToString());
    var low=map.Plan.sites.Select((s,n)=>new{s,n}).Where(x=>x.s.kind=="Street fabric").ToArray();
    var isolated=low.Where(x=>!map.Plan.routes.Any(r=>!r.name.StartsWith("Ground street")&&(r.from==x.n||r.to==x.n))).Select(x=>x.s.name).ToArray();
    report.AppendLine("Lower buildings without roof connections="+isolated.Length+" "+string.Join(", ",isolated));
    Capture(map,index,"intact");InstallPreview(index);
    var motorRun=Walk(map,session,report);while(motorRun.MoveNext())yield return motorRun.Current;
    File.WriteAllText(path,report.ToString());
    session.SetRole(true);session.SetMenu(true);session.SurvivorBots.Clear();session.SurvivorBots.Running=true;for(int n=0;n<botCount;n++)session.SurvivorBots.SpawnOne();
    end=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<end)yield return null;var bots=session.SurvivorBots.Members.Where(b=>b).ToArray();var positions=bots.Select(b=>b.transform.position).ToArray();var frames=new List<float>();
    end=Time.realtimeSinceStartup+4;while(Time.realtimeSinceStartup<end){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}Frames(report,botCount+" bots / quiet",frames);report.AppendLine("Bots spawned="+bots.Length+" moved>.5m="+bots.Where((b,n)=>b&&Vector3.Distance(b.transform.position,positions[n])>.5f).Count());
    File.WriteAllText(path,report.ToString());
    var wave=Object.Instantiate(Resources.Load<GameObject>("DemoDisasters/Tsunami"));var hazard=wave.GetComponent<IHazard>();var component=hazard as Component;
    Set(component,"randomDirection",false);Set(component,"approachHeading",0f);Set(component,"warningTime",0f);Set(component,"offshoreDistance",0f);Set(component,"riseTime",.1f);
    var before=map.Blocks.Select(b=>b.position).ToArray();hazard.Begin();end=Time.realtimeSinceStartup+waveSeconds;frames.Clear();while(hazard.Active&&Time.realtimeSinceStartup<end){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}
    Frames(report,botCount+" bots + tsunami",frames);report.AppendLine((streetReview?"Targeted 16-second wave exposure; ":"")+"Wave completed="+!hazard.Active+" bodies moved>.5m="+map.Blocks.Where((b,n)=>Vector3.Distance(b.position,before[n])>.5f).Count()+"/"+map.Blocks.Count+". Default height/speed/forces; warning/offshore staging shortened only.");if(hazard.Active&&!streetReview)failures++;hazard.End();Object.Destroy(wave);Capture(map,index,"tsunami");
    session.SurvivorBots.Clear();map.Rebuild();end=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<end)yield return null;before=map.Blocks.Select(b=>b.position).ToArray();
    for(int d=0;d<map.Plan.sites.Count;d++){var hit=map.Blocks.FirstOrDefault(b=>b.GetComponent<MayhemStructuralMember>().District==d&&b.name.Contains("inhabited section"));if(hit)hit.AddForceAtPosition(new Vector3(d%2==0?14:-14,0,5),hit.worldCenterOfMass+Vector3.up*3,ForceMode.VelocityChange);}
    end=Time.realtimeSinceStartup+8;while(Time.realtimeSinceStartup<end)yield return null;Capture(map,index,"impact");report.AppendLine("Separate reset + 14m/s X / 5m/s Z off-center demolition moved>.5m="+map.Blocks.Where((b,n)=>Vector3.Distance(b.position,before[n])>.5f).Count()+"/"+map.Blocks.Count+". Not normal tsunami behavior.");
    string saved=map.SaveLayout();int count=map.Blocks.Count,revision=map.Revision;bool reject=!map.LoadLayout(saved,count+1,new Pose(Vector3.zero,Quaternion.identity),Vector3.one)&&map.Revision==revision;bool reload=map.LoadLayout(saved,count,new Pose(Vector3.zero,Quaternion.identity),Vector3.one);report.AppendLine("Layout invalidCountRejected="+reject+" validRoundtrip="+reload+" resetCount="+map.Blocks.Count+" runtimeErrorsSoFar="+errors);if(!reject||!reload||map.Blocks.Count!=count)failures++;
    File.WriteAllText(path,report.ToString());root.SetActive(false);Object.Destroy(root);yield return null;
   }
  }
  static IEnumerator Walk(EpicCityMap map,ISessionInfo session,StringBuilder report)
  {
   var actor=session.SurvivorBots.SpawnOne();var motor=actor?actor.GetComponent<CastleSurvivor>():null;if(!motor)throw new InvalidOperationException("Missing production survivor motor.");
   foreach(var c in actor.GetComponents<MonoBehaviour>())if(c is DeadlyJenga.CastleBots.ICastleSurvivorDriver)c.enabled=false;
   motor.enabled=false;motor.externallyDriven=true;motor.InputDriver=null;motor.RefreshMode();session.SetMenu(false);motor.InputSuspended=false;
   for(int warm=0;warm<3;warm++)yield return null;
   for(int district=0;district<map.Plan.sites.Count;district++)
   {
    int index=map.Authored.ToList().FindIndex(m=>m.district==district&&m.shapes.Any(Flight)&&m.position.y<map.Plan.sites[district].position.y+2);
    if(index<0){report.AppendLine("REVIEW no ground cassette flight at "+district);failures++;continue;}
    var module=map.Authored[index];var rb=map.Blocks[index];var inv=Quaternion.Inverse(module.shapes[0].rotation);var flight=module.shapes.First(Flight);
    Vector3 Point(int v)=>rb.transform.TransformPoint(inv*(flight.center+flight.rotation*flight.vertices[v]));var a=(Point(4)+Point(5))*.5f;var b=(Point(6)+Point(7))*.5f;var dir=Vector3.ProjectOnPlane(b-a,Vector3.up).normalized;
    motor.Teleport(a+Vector3.up*.07f,Quaternion.identity);yield return null;bool reached=false;
    var goal=b-dir*.15f;
    for(int step=0;step<700;step++)
    {
     var delta=Vector3.ProjectOnPlane(goal-motor.transform.position,Vector3.up);if(delta.magnitude<.18f&&Mathf.Abs(goal.y-motor.transform.position.y)<.18f){reached=true;break;}
     if(delta.sqrMagnitude>.0001f){motor.transform.rotation=Quaternion.LookRotation(delta);motor.viewCamera.transform.localRotation=Quaternion.identity;}
     motor.InputSuspended=false;motor.Simulate(Vector2.up*Mathf.Clamp01(delta.magnitude/Mathf.Max(.001f,motor.EffectiveWalkSpeed*.02f)),false,.02f,false);
     if(step%80==79)yield return null;
    }
    report.AppendLine((reached?"PASS":"REVIEW")+" production motor first flight "+map.Plan.sites[district].name+" position="+motor.transform.position);if(!reached)failures++;
   }
   session.SurvivorBots.Clear();
  }
  static bool Flight(MayhemShape s)=>s.vertices!=null&&s.vertices[6].y-s.vertices[4].y>1&&s.vertices[6].y-s.vertices[4].y<6&&Vector3.Distance(s.vertices[0],s.vertices[4])<.001f;
  static void Set(Component c,string field,object value){c?.GetType().GetField(field)?.SetValue(c,value);}
  static void Frames(StringBuilder report,string label,List<float> values){if(values.Count<2)return;var samples=values.Skip(1).OrderBy(v=>v).ToArray();report.AppendLine(label+" frames="+samples.Length+" average="+(1000/samples.Average()).ToString("F1")+"FPS p95="+samples[(int)((samples.Length-1)*.95f)].ToString("F1")+"ms. Uncapped desktop Editor, not Quest; first boundary sample omitted.");}
  static void Capture(EpicCityMap map,int index,string state)
  {
   var go=new GameObject("City production evidence camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=1600;camera.fieldOfView=43;camera.transform.position=new Vector3(220,185,-300);camera.transform.LookAt(new Vector3(0,8,0));
   var rt=RenderTexture.GetTemporary(1600,1050,24);var previous=RenderTexture.active;var image=new Texture2D(1600,1050,TextureFormat.RGB24,false);
   try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,1050),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report("EpicCity-"+(index+1).ToString("D2")+"-"+state+".png"),image.EncodeToPNG());}
   finally{RenderTexture.active=previous;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);Object.Destroy(go);}
  }
  static void InstallPreview(int index)
  {
   string source=ProjectPath.Report("EpicCity-"+(index+1).ToString("D2")+"-intact.png");string folder=Path.GetDirectoryName(Path.GetDirectoryName(ProjectPath.FolderOf("CandyEnvironment.cs").TrimEnd('/'))).Replace('\\','/')+"/Resources/LobbyPreviews";
   string path=folder+"/"+EpicCityLayout.Ids[index]+".png";File.WriteAllBytes(path,File.ReadAllBytes(source));AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
   var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer){importer.maxTextureSize=1024;importer.mipmapEnabled=false;importer.SaveAndReimport();}
  }
 }
}
