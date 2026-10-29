using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Runs only after an explicit menu command or process-addressed request file.
    [InitializeOnLoad] public static class EpicCityCinematicCapture
    {
        const string Key="EpicCity.Cinematics";
        static IEnumerator run;
        static Action restore;
        static int errors;
        [Serializable] sealed class Saved { public SceneSetup[] scenes; }
        [Serializable] public sealed class Shot
        {
            public string mapId,mapName,file,title,description;
            public Vector3 camera,target;
            public float fieldOfView;
        }
        [Serializable] sealed class ShotList { public string captured; public List<Shot> shots=new(); }
        static string Output=>Path.Combine(Path.GetDirectoryName(Application.dataPath),"ArtDirection","EpicCityCinematics-20261026");
        static EpicCityCinematicCapture(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        [MenuItem("Tools/Deadly Jenga/Capture Epic City Cinematics")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before cinematic capture.");
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>s.isLoaded&&(string.IsNullOrEmpty(s.path)||SceneManager.GetSceneByPath(s.path).isDirty)))throw new InvalidOperationException("Unsaved scenes left untouched.");
            if(!ValidationRunGate.TryAcquire(Key))throw new InvalidOperationException("Another validation is running.");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output,"started.txt"),DateTime.Now.ToString("O"));
            File.WriteAllText(Path.Combine(Output,"progress.txt"),"");
            if(File.Exists(Path.Combine(Output,"complete.txt")))File.Delete(Path.Combine(Output,"complete.txt"));
            errors=0;
            File.WriteAllText(Path.Combine(Output,"errors.txt"),"");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Saved{scenes=scenes}));
            SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
            {
                SessionState.SetBool(Key,false);SessionState.SetBool(Key+"Running",true);
                EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();
                Application.logMessageReceived+=Log;run=CaptureAll();
            }
            if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"Running",false))
            {
                SessionState.SetBool(Key+"Running",false);(run as IDisposable)?.Dispose();run=null;
                restore?.Invoke();restore=null;Application.logMessageReceived-=Log;
                AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
                var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Scenes",""));
                if(saved!=null)EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
                ValidationRunGate.Release(Key);
                InstallExtraPreviews();
            }
        }
        static void Log(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Path.Combine(Output,"errors.txt"),message+"\n"+stack+"\n");}}
        static void Tick()
        {
            if(run==null)
            {
                string request=ProjectPath.Report("EpicCityCinematics.request");
                if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                if(File.ReadAllText(request).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString())return;
                File.Delete(request);try{Run();}catch(Exception e){Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"errors.txt"),e.ToString());}return;
            }
            try{if(run.MoveNext())return;}catch(Exception e){errors++;File.AppendAllText(Path.Combine(Output,"errors.txt"),e.ToString());}
            (run as IDisposable)?.Dispose();run=null;restore?.Invoke();restore=null;Application.logMessageReceived-=Log;
            File.WriteAllText(Path.Combine(Output,"complete.txt"),"COMPLETE runtimeErrors="+errors);
            EditorApplication.ExitPlaymode();
        }
        static IEnumerator CaptureAll()
        {
            var session=Services.Session;float until=Time.realtimeSinceStartup+40;
            while(session?.SurvivorBots==null&&Time.realtimeSinceStartup<until){yield return null;session=Services.Session;}
            if(session?.SurvivorBots==null)throw new InvalidOperationException("Production Game session did not start.");
            bool background=Application.runInBackground,voice=session.Social?.VoiceEnabled??false;float volume=AudioListener.volume;
            restore=()=>{Application.runInBackground=background;AudioListener.volume=volume;if(session.Social!=null)session.Social.VoiceEnabled=voice;};
            Application.runInBackground=true;AudioListener.volume=0;session.Demo.Run(DemoCommand.StopDisasters);
            session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots?.Clear();
            if(session.Social!=null)session.Social.VoiceEnabled=false;
            session.SetRole(true);session.SetMenu(true);
            for(int i=0;i<3;i++)yield return null;
            foreach(var other in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))if(other is IWorldQuery&&!(other is ISessionInfo))other.gameObject.SetActive(false);
            var list=new ShotList{captured=DateTime.Now.ToString("O")};
            var catalog=Object.Instantiate(Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog"));catalog.EnsureModeMaps();
            var missing=catalog.maps.Where(m=>!Resources.Load<Texture2D>("LobbyPreviews/"+m.id)).Select(m=>m.id).ToArray();
            File.WriteAllText(Path.Combine(Output,"preview-resources.txt"),"Runtime catalog="+catalog.maps.Length+" missing="+missing.Length+" "+string.Join(", ",missing));Object.Destroy(catalog);
            int[] landmarks={4,4,5,5,3,4,4,3,6,3};
            for(int design=0;design<10;design++)
            {
                var prefab=Resources.Load<GameObject>("ModeMaps/Mode_"+EpicCityLayout.Ids[design]);
                if(!prefab)throw new InvalidOperationException("Missing production prefab "+EpicCityLayout.Ids[design]);
                var root=Object.Instantiate(prefab);var map=root.GetComponent<EpicCityMap>();map.generateOnStart=false;map.Rebuild();
                session.SurvivorBots.MapRebuilt();
                until=Time.realtimeSinceStartup+1.5f;while(Time.realtimeSinceStartup<until)yield return null;
                // These are intact baseline art references; freeze only after the authored structure settles.
                foreach(var body in map.Blocks){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.isKinematic=true;}
                var low=map.Plan.sites.Where(s=>s.kind=="Street fabric").ToArray();
                if(low.Length==0)throw new InvalidOperationException("Revised street layer missing "+design);
                var street=low.OrderBy(s=>(s.position-new Vector3(-25,0,-70)).sqrMagnitude).First();
                var center=low.OrderBy(s=>s.position.sqrMagnitude).First();
                var landmark=map.Plan.sites[landmarks[design]];
                var shots=new[]{
                    Make(design,1,"Street-level layers",street.name+" with its open ground passage, roof terrace and surrounding skyline.",street.position+Vector3.up*4.5f,Quaternion.Euler(0,street.heading,0)*new Vector3(-23,-1.3f,-31),62),
                    Make(design,2,"Landmark and neighborhood",landmark.name+" framed above the lower network; an art reference for scale and material hierarchy.",Vector3.Lerp(landmark.position,center.position,.24f)+Vector3.up*15,new Vector3(64,38,-76),53),
                    Make(design,3,"Connected rooftops",center.name+" and its middle-city roof connections, viewed across the low-to-high layers.",center.position+Vector3.up*9,new Vector3(60,48,40),56)
                };
                foreach(var shot in shots)
                {
                    Capture(shot);list.shots.Add(shot);
                    File.WriteAllText(Path.Combine(Output,"Shots.json"),JsonUtility.ToJson(list,true));
                    yield return null;
                }
                File.AppendAllText(Path.Combine(Output,"progress.txt"),EpicCityLayout.Ids[design]+" 3 captures; "+map.Blocks.Count+" bodies; lower buildings="+low.Length+"\n");
                root.SetActive(false);Object.Destroy(root);yield return null;
            }
            var extras=ExtraPreviews(session);while(extras.MoveNext())yield return extras.Current;
        }
        static IEnumerator ExtraPreviews(ISessionInfo session)
        {
            string oldMap=MatchSetup.MapId;
            try
            {
                foreach(string id in new[]{"christmas-town","powerup-test"})
                {
                    MatchSetup.MapId="mode-"+id;
                    var root=Object.Instantiate(Resources.Load<GameObject>("ModeMaps/Mode_"+id));
                    foreach(var component in root.GetComponentsInChildren<MonoBehaviour>())if(component is IRebuildable rebuild){rebuild.Rebuild();break;}
                    session.SurvivorBots.Clear();session.SurvivorBots.MapRebuilt();
                    float until=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<until)yield return null;
                    if(id=="powerup-test")
                    {
                        int pickups=MatchRuntime.Current?.Frame?.pickups?.Length??0;
                        File.AppendAllText(Path.Combine(Output,"preview-resources.txt"),"\nPowerup stations="+pickups);
                        if(pickups==0)throw new InvalidOperationException("Powerup preview did not seed actual gameplay pickups.");
                    }
                    Capture(new Shot{file="preview-mode-"+id+".png",camera=id=="christmas-town"?new Vector3(78,62,-95):new Vector3(53,38,-64),target=id=="christmas-town"?new Vector3(0,2,0):new Vector3(0,4,-4),fieldOfView=43});
                    root.SetActive(false);Object.Destroy(root);yield return null;
                }
            }
            finally{MatchSetup.MapId=oldMap;}
        }
        static void InstallExtraPreviews()
        {
            string presentation=Path.GetDirectoryName(Path.GetDirectoryName(ProjectPath.FolderOf("CandyEnvironment.cs").TrimEnd('/'))).Replace('\\','/');
            foreach(string id in new[]{"christmas-town","powerup-test"})
            {
                string source=Path.Combine(Output,"preview-mode-"+id+".png");if(!File.Exists(source))continue;
                string path=presentation+"/Resources/LobbyPreviews/mode-"+id+".png";
                File.WriteAllBytes(path,File.ReadAllBytes(source));AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer){importer.maxTextureSize=1024;importer.mipmapEnabled=false;importer.SaveAndReimport();}
            }
        }
        static Shot Make(int design,int n,string title,string description,Vector3 target,Vector3 offset,float fov)
        {return new Shot{mapId=EpicCityLayout.Ids[design],mapName=EpicCityLayout.Names[design],file=EpicCityLayout.Ids[design]+"-"+n.ToString("D2")+".png",title=title,description=description,target=target,camera=target+offset,fieldOfView=fov};}
        static void Capture(Shot shot)
        {
            var go=new GameObject("Temporary city cinematic camera");var camera=go.AddComponent<Camera>();camera.enabled=false;
            camera.clearFlags=CameraClearFlags.Skybox;camera.nearClipPlane=.2f;camera.farClipPlane=1200;camera.fieldOfView=shot.fieldOfView;
            camera.transform.position=shot.camera;camera.transform.LookAt(shot.target);
            var rt=RenderTexture.GetTemporary(1920,1200,24);var previous=RenderTexture.active;var image=new Texture2D(1920,1200,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1920,1200),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,shot.file),image.EncodeToPNG());}
            finally{RenderTexture.active=previous;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);Object.Destroy(go);}
        }
    }
}
