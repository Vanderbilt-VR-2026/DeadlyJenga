using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Presentation;
using DeadlyJenga.CandyArt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class FantasyMapValidation
    {
        const string Key="FantasyMaps.Validation";
        static IEnumerator run;static Action restore;static int errors;
        [Serializable] sealed class Saved {public SceneSetup[] scenes;public string map,mode;}
        static string Output=>Path.Combine(Path.GetDirectoryName(Application.dataPath),"ArtDirection","ApprovedFantasyMaps-20261026");
        static FantasyMapValidation(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        static void Tick()
        {
            if(run==null)
            {
                string request=ProjectPath.Report("FantasyMapValidation.request");
                if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                if(File.ReadAllText(request).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString())return;
                File.Delete(request);try{Start();}catch(Exception e){Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"error.txt"),e.ToString());}return;
            }
            try{if(run.MoveNext())return;}
            catch(Exception e){errors++;File.AppendAllText(Path.Combine(Output,"errors.txt"),e+"\n");}
            (run as IDisposable)?.Dispose();run=null;restore?.Invoke();restore=null;
            Application.logMessageReceived-=Log;File.WriteAllText(Path.Combine(Output,"complete.txt"),"Completed; logged errors="+errors);
            EditorApplication.ExitPlaymode();
        }
        [MenuItem("Tools/Deadly Jenga/Validate Approved Fantasy Maps")]
        public static void Start()
        {
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>s.isLoaded&&(string.IsNullOrEmpty(s.path)||SceneManager.GetSceneByPath(s.path).isDirty)))throw new InvalidOperationException("Unsaved scenes left untouched.");
            if(!ValidationRunGate.TryAcquire(Key))throw new InvalidOperationException("Another validation is active.");
            Directory.CreateDirectory(Output);errors=0;File.WriteAllText(Path.Combine(Output,"errors.txt"),"");
            File.WriteAllText(Path.Combine(Output,"started.txt"),DateTime.Now.ToString("O"));
            if(File.Exists(Path.Combine(Output,"complete.txt")))File.Delete(Path.Combine(Output,"complete.txt"));
            SessionState.SetString(Key+"Saved",JsonUtility.ToJson(new Saved{scenes=scenes,map=MatchSetup.MapId,mode=MatchSetup.ModeId}));
            MatchSetup.ModeId="demo";SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
            {SessionState.SetBool(Key,false);SessionState.SetBool(Key+"Running",true);EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();Application.logMessageReceived+=Log;run=Validate();}
            if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"Running",false))
            {
                SessionState.SetBool(Key+"Running",false);(run as IDisposable)?.Dispose();run=null;restore?.Invoke();restore=null;
                Application.logMessageReceived-=Log;AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
                var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Saved",""));
                if(saved!=null){MatchSetup.MapId=saved.map;MatchSetup.ModeId=saved.mode;EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);}
                ValidationRunGate.Release(Key);InstallPreviews();
            }
        }
        static void Log(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Path.Combine(Output,"errors.txt"),message+"\n"+stack+"\n");}}
        static IEnumerator Validate()
        {
            float deadline=Time.realtimeSinceStartup+40;ISessionInfo session;
            while((session=Services.Session)?.SurvivorBots==null&&Time.realtimeSinceStartup<deadline)yield return null;
            session=Services.Session;if(session?.SurvivorBots==null)throw new InvalidOperationException("Game session failed to start.");
            var controls=session.Demo as IDemoMapControls;if(controls==null||!controls.CanSelectMap)throw new InvalidOperationException("Production map controls unavailable.");
            bool background=Application.runInBackground;float volume=AudioListener.volume;
            restore=()=>{Application.runInBackground=background;AudioListener.volume=volume;};Application.runInBackground=true;AudioListener.volume=0;
            if(session.Social!=null)session.Social.VoiceEnabled=false;
            session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots?.Clear();
            session.Demo.Run(DemoCommand.StopDisasters);session.SetRole(true);session.SetMenu(true);
            var report=new StringBuilder("Actual production Game Play Mode; desktop Editor; no headset or network test.\n");
            foreach(string id in FantasyMapBuild.Ids)
            {
                session=Services.Session;controls=session.Demo as IDemoMapControls;
                if(!controls.Maps.Any(m=>m.id==id&&m.available))throw new InvalidOperationException("Map missing from demo picker: "+id);
                controls.SelectMap(id);for(int n=0;n<8;n++)yield return null;
                session=Services.Session;
                if(session?.SurvivorBots==null)throw new InvalidOperationException("Reloaded session unavailable: "+id);
                if(session.Social!=null)session.Social.VoiceEnabled=false;
                session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots?.Clear();
                session.Demo.Run(DemoCommand.StopDisasters);session.SetRole(true);session.SetMenu(true);
                var map=Services.World as EpicCityMap;if(!map||!map.GetComponent<FantasyMapArt>())throw new InvalidOperationException("Theme map did not load: "+id);
                float until=Time.realtimeSinceStartup+1.3f;while(Time.realtimeSinceStartup<until)yield return null;
                if(map.Blocks.Count!=map.Plan.modules.Count)throw new InvalidOperationException("Body count changed.");
                var art=map.GetComponent<FantasyMapArt>();
                foreach(var shader in art.blockMaterials.Select(m=>m.shader).Distinct())
                {var messages=ShaderUtil.GetShaderMessages(shader);if(messages.Any(m=>m.severity.ToString()=="Error"))throw new InvalidOperationException(string.Join("\n",messages.Select(m=>m.message)));}
                foreach(var body in map.Blocks){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.isKinematic=true;}
                Capture(id+"-overview.png",new Vector3(220,185,-300),new Vector3(0,8,0),43);
                Capture(id+"-close.png",new Vector3(72,48,-86),new Vector3(0,16,0),62);
                Capture(id+"-detail.png",new Vector3(32,22,-45),new Vector3(0,19,0),66);
                int rounded=0,vertices=0,triangles=0;
                foreach(var body in map.Blocks)
                {
                    var visual=body.GetComponent<IClimbable>()?.Visual;
                    if(!visual)throw new InvalidOperationException("Missing structural visual.");
                    foreach(var filter in visual.GetComponentsInChildren<MeshFilter>())
                    {
                        if(filter.sharedMesh.name!="Rounded fantasy surface")continue;
                        rounded++;vertices+=filter.sharedMesh.vertexCount;triangles+=filter.sharedMesh.triangles.Length/3;
                        if(filter.sharedMesh.normals.Any(n=>!float.IsFinite(n.x)||n.sqrMagnitude<.8f))throw new InvalidOperationException("Invalid rounded surface normals.");
                    }
                }
                if(rounded!=map.Blocks.Count)throw new InvalidOperationException("Some structural meshes did not receive the visual bevels: "+rounded);
                foreach(var material in art.blockMaterials)
                    if(!material.GetTexture("_EnvironmentCube"))throw new InvalidOperationException("Missing baked sky reflection.");
                int contacts=map.Blocks.Sum(b=>b.GetComponentsInChildren<Collider>().Length);
                if(contacts!=map.Authored.Sum(m=>m.shapes.Count))throw new InvalidOperationException("Structural collision count changed.");
                int decorations=map.GetComponentsInChildren<MeshRenderer>().Count(r=>r.name=="Fantasy decorations");
                int bodies=map.GetComponentsInChildren<Rigidbody>().Length;
                if(bodies!=map.Blocks.Count)throw new InvalidOperationException("Decoration added physics bodies.");
                if(map.GetComponentsInChildren<Collider>().Any(c=>c.transform.IsChildOf(art.transform)&&c.name.StartsWith("Visual ·")))throw new InvalidOperationException("Scenery has collision.");
                Vector3 spawn=map.SafeSpawnPoint();if(float.IsNaN(spawn.y)||spawn.y<-.1f)throw new InvalidOperationException("Invalid spawn point.");
                string saved=map.SaveLayout();int oldCount=map.Blocks.Count;
                if(!map.LoadLayout(saved,oldCount,new Pose(map.transform.position,map.transform.rotation),map.transform.localScale))throw new InvalidOperationException("Layout roundtrip failed.");
                for(int n=0;n<5;n++)yield return null;
                int resetDecorations=map.GetComponentsInChildren<MeshRenderer>().Count(r=>r.name=="Fantasy decorations");
                if(decorations!=resetDecorations)throw new InvalidOperationException("Decoration count changed after rebuild.");
                report.AppendLine("PASS "+id+": picker, load, material shaders, spawn, "+oldCount+" original bodies; decorations="+decorations+"; contacts="+contacts+"; rounded="+rounded+"; visual vertices="+vertices+"; triangles="+triangles+"; baked reflection assigned; reset and layout roundtrip.");
                File.WriteAllText(Path.Combine(Output,"Runtime.txt"),report.ToString());
            }
            controls=Services.Session.Demo as IDemoMapControls;
            controls.SelectMap("epic-three-hearts");for(int n=0;n<8;n++)yield return null;
            if(Object.FindObjectsByType<FantasyMapArt>(FindObjectsSortMode.None).Any(a=>a.isActiveAndEnabled))throw new InvalidOperationException("Old theme remains active after switching.");
            var environment=Object.FindFirstObjectByType<CandyEnvironment>();
            if(environment&&environment.waterRenderer&&environment.waterRenderer.forceRenderingOff)throw new InvalidOperationException("Theme failed to restore ocean visibility.");
            report.AppendLine("PASS switch to original: theme removed and ocean rendering restored.");
            File.WriteAllText(Path.Combine(Output,"Runtime.txt"),report.ToString());
        }
        static void Capture(string file,Vector3 position,Vector3 target,float fov)
        {
            var go=new GameObject("Fantasy map evidence camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=1600;camera.nearClipPlane=.2f;camera.fieldOfView=fov;camera.transform.position=position;camera.transform.LookAt(target);
            Object.FindFirstObjectByType<FantasyMapArt>()?.ConfigureCamera(camera);
            var rt=RenderTexture.GetTemporary(1920,1200,24);var old=RenderTexture.active;var image=new Texture2D(1920,1200,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1920,1200),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,file),image.EncodeToPNG());}
            finally{RenderTexture.active=old;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);Object.Destroy(go);}
        }
        static void InstallPreviews()
        {
            string folder=ProjectPath.FolderOf("MapPreviewImages.cs");
            string presentation=Path.GetDirectoryName(Path.GetDirectoryName(folder.TrimEnd('/'))).Replace('\\','/');
            foreach(string id in FantasyMapBuild.Ids)
            {
                string source=Path.Combine(Output,id+"-overview.png");if(!File.Exists(source))continue;
                string path=presentation+"/Resources/LobbyPreviews/"+id+".png";File.WriteAllBytes(path,File.ReadAllBytes(source));AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer){importer.maxTextureSize=1024;importer.mipmapEnabled=false;importer.SaveAndReimport();}
            }
        }
    }
}
