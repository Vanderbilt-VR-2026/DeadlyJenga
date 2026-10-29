using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyArt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Runs only after an explicit menu command or PID-addressed request.
    [InitializeOnLoad] public static class MapPreviewAudit
    {
        static MapPreviewAudit(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            var request=ProjectPath.Report("MapPreviewAudit.request");
            if(!File.Exists(request)||File.ReadAllText(request).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString())return;
            File.Delete(request);try{Run();}catch(Exception e){File.WriteAllText(ProjectPath.Report("MapPreviewAudit-error.txt"),e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Tools/Deadly Jenga/Maps/Link and Capture All Map Previews")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            if(!ValidationRunGate.TryAcquire("MapPreviewAudit"))throw new InvalidOperationException("Another capture is running.");
            var source=Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");
            var catalog=Object.Instantiate(source);catalog.EnsureModeMaps();
            var oldWorld=Services.World;var oldScene=SceneManager.GetActiveScene();var oldSky=RenderSettings.skybox;var oldAmbient=RenderSettings.ambientMode;var oldSkyColor=RenderSettings.ambientSkyColor;var oldEquator=RenderSettings.ambientEquatorColor;var oldGround=RenderSettings.ambientGroundColor;float oldIntensity=RenderSettings.ambientIntensity;
            string presentation=Path.GetDirectoryName(Path.GetDirectoryName(ProjectPath.FolderOf("CandyEnvironment.cs").TrimEnd('/'))).Replace('\\','/');
            string resources=Folder(presentation,"Resources"),folder=Folder(resources,"LobbyPreviews");
            var report=new StringBuilder("Actual map preview audit. Existing rendered captures reused; missing views rendered in isolated preview scenes. No gameplay scenes saved.\n");
            int retained=0,linked=0,captured=0,failed=0;
            try
            {
                foreach(var map in catalog.maps)
                {
                    string path=folder+"/"+map.id+".png";
                    try
                    {
                        if(File.Exists(path)){retained++;report.AppendLine("RETAIN "+map.id+" | "+path);continue;}
                        string existing=Existing(map.id);
                        if(existing!=null){File.WriteAllBytes(path,File.ReadAllBytes(existing));linked++;report.AppendLine("LINK "+map.id+" | "+existing);}
                        else{Capture(map,catalog,path);captured++;report.AppendLine("CAPTURE "+map.id+" | "+path);}
                        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                        if(importer){importer.maxTextureSize=1024;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();}
                        if(!AssetDatabase.LoadAssetAtPath<Texture2D>(path))throw new InvalidOperationException("Preview import failed.");
                    }
                    catch(Exception e){failed++;report.AppendLine("FAILED "+map.id+" | "+e);}
                    finally{Services.World=oldWorld;}
                    File.WriteAllText(ProjectPath.Report("MapPreviewAudit.txt"),report.ToString());
                }
                report.AppendLine("COMPLETE total="+catalog.maps.Length+" retained="+retained+" linked="+linked+" captured="+captured+" failed="+failed);
                File.WriteAllText(ProjectPath.Report("MapPreviewAudit.txt"),report.ToString());AssetDatabase.SaveAssets();
            }
            finally{Services.World=oldWorld;if(oldScene.IsValid()&&oldScene.isLoaded)SceneManager.SetActiveScene(oldScene);RenderSettings.skybox=oldSky;RenderSettings.ambientMode=oldAmbient;RenderSettings.ambientSkyColor=oldSkyColor;RenderSettings.ambientEquatorColor=oldEquator;RenderSettings.ambientGroundColor=oldGround;RenderSettings.ambientIntensity=oldIntensity;Object.DestroyImmediate(catalog);ValidationRunGate.Release("MapPreviewAudit");}
        }
        static string Folder(string parent,string name){string p=parent+"/"+name;if(!AssetDatabase.IsValidFolder(p))AssetDatabase.CreateFolder(parent,name);return p;}
        static string Existing(string id)
        {
            var names=new Dictionary<string,string>{
                {"castle","ArtDirection/CandyTropical/Results/candy-castle.png"},
                {"mode-castle-gaps","Logs/Validation/Castle-Long-Blocks-and-Gaps.png"},
                {"mode-castle-catwalks","Logs/Validation/castle-catwalks.png"},
                {"mode-castle-four-keeps","Logs/Validation/castle-four-keeps.png"},
                {"mode-cantina","Logs/Validation/Cantina-overview.png"},
                {"mode-tiki-resort","Logs/Validation/Tiki-overview.png"},
                {"breakwater-village","ArtDirection/BreakwaterVillage/01-village-before.png"}
            };
            if(names.TryGetValue(id,out var exact)&&File.Exists(exact))return exact;
            if(id.StartsWith("sparse-",StringComparison.Ordinal))
            {
                string candidate="Logs/Validation/Sparse-"+id.Substring(7)+(id.EndsWith("-study",StringComparison.Ordinal)?".png":"-intact.png");
                if(File.Exists(candidate))return candidate;
            }
            return null;
        }
        static void Capture(IslandLobbyCatalog.Map map,IslandLobbyCatalog catalog,string output)
        {
            var scene=map.mapPrefab?EditorSceneManager.NewPreviewScene():EditorSceneManager.OpenPreviewScene(map.scenePath);
            var oldWorld=Services.World;
            try
            {
                GameObject root=map.mapPrefab?PrefabUtility.InstantiatePrefab(map.mapPrefab,scene) as GameObject:null;
                if(root){foreach(var item in root.GetComponentsInChildren<MonoBehaviour>(true))if(item is IRebuildable builder)builder.Rebuild();}
                else{foreach(var item in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)))if(item is IRebuildable builder)builder.Rebuild();}
                var world=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).FirstOrDefault(c=>c is IWorldQuery);
                if(world)Services.World=(IWorldQuery)world;
                // The captured map uses the game's own environment and materials.
                var env=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CandyEnvironment>(true)).FirstOrDefault();
                if(!env&&catalog.environmentPrefab)env=(PrefabUtility.InstantiatePrefab(catalog.environmentPrefab.gameObject,scene) as GameObject)?.GetComponent<CandyEnvironment>();
                if(env)
                {
                    env.applyLighting=false;
                    if(world){var baseline=env.GetComponent<CandyArtBaseline>()??env.gameObject.AddComponent<CandyArtBaseline>();baseline.map=world;var support=world.GetComponentsInChildren<Collider>().Where(c=>!c.attachedRigidbody&&!c.isTrigger).OrderByDescending(c=>c.bounds.size.x*c.bounds.size.z).FirstOrDefault();if(support)env.foundation=support.transform;}
                    env.Refresh();
                }
                var renderers=(root?root.GetComponentsInChildren<Renderer>(true):scene.GetRootGameObjects().Where(g=>!g.GetComponent<CandyEnvironment>()).SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).ToArray()).Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.bounds.size.sqrMagnitude>.001f&&r.bounds.size.magnitude<1800).ToArray();
                var bounds=new Bounds(Vector3.zero,Vector3.one);
                if(renderers.Length>0){bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);}
                else if(world is IMapFootprint footprint){bounds=new Bounds(((IWorldQuery)world).Center,new Vector3(footprint.FootprintHalfSize*2,1,footprint.FootprintHalfSize*2));}
                else throw new InvalidOperationException("Map has neither visible geometry nor an island footprint.");
                float extent=Mathf.Max(bounds.extents.x,bounds.extents.z,bounds.extents.y);
                var go=new GameObject("Map preview camera");SceneManager.MoveGameObjectToScene(go,scene);var camera=go.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.3f,.7f,.93f);camera.fieldOfView=43;camera.nearClipPlane=.1f;camera.farClipPlane=Mathf.Max(1500,extent*12);
                var direction=new Vector3(1.05f,.8f,-1.3f).normalized;camera.transform.position=bounds.center+direction*Mathf.Max(25,extent*3.45f);camera.transform.LookAt(bounds.center);
                var light=new GameObject("Preview sunlight");SceneManager.MoveGameObjectToScene(light,scene);var sun=light.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.6f;sun.transform.rotation=Quaternion.Euler(48,-35,0);
                var target=RenderTexture.GetTemporary(1280,800,24);var previous=RenderTexture.active;var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
                try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(output,image.EncodeToPNG());}
                finally{RenderTexture.active=previous;camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(image);}
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);Services.World=oldWorld;}
        }
    }
}
