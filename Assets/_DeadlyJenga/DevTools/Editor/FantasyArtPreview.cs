using System;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class FantasyArtPreview
    {
        static FantasyArtPreview()=>EditorApplication.update+=Tick;
        static void Tick()
        {
            string request=ProjectPath.Report("FantasyArtPreview.request");
            if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(File.ReadAllText(request).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString())return;
            File.Delete(request);try{Capture();}catch(Exception e){File.WriteAllText(ProjectPath.Report("FantasyArtPreview-error.txt"),e.ToString());}
        }
        static void Invoke(FantasyMapArt art,string method){if(art)typeof(FantasyMapArt).GetMethod(method,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(art,null);}
        static void Capture()
        {
            string output=Path.Combine(Path.GetDirectoryName(Application.dataPath),"ArtDirection","ApprovedFantasyMaps-20261026","EditorPreviews");Directory.CreateDirectory(output);
            var previous=Services.World;var report=new StringBuilder("Editor previews only; no simulation.\n");
            foreach(string id in FantasyMapBuild.Ids)
            {
                bool ignored=Physics.GetIgnoreLayerCollision(Layers.Survivor,Layers.Block);var scene=EditorSceneManager.NewPreviewScene();GameObject go=null;
                try
                {
                    go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("Mode_"+id+".prefab")),scene);
                    var map=go.GetComponent<EpicCityMap>();Services.World=map;map.Rebuild();
                    int originalZeros=map.Blocks.Sum(b=>b.GetComponent<IClimbable>().Visual.GetComponent<MeshFilter>().sharedMesh.normals.Count(n=>n.sqrMagnitude<.8f));
                    var art=go.GetComponent<FantasyMapArt>();Invoke(art,"LateUpdate");
                    int bad=map.Blocks.Sum(b=>b.GetComponent<IClimbable>().Visual.GetComponent<MeshFilter>().sharedMesh.normals.Count(n=>!float.IsFinite(n.x)||n.sqrMagnitude<.8f));
                    var lightObject=new GameObject("Preview key light",typeof(Light));SceneManager.MoveGameObjectToScene(lightObject,scene);
                    var light=lightObject.GetComponent<Light>();light.type=LightType.Directional;light.shadows=LightShadows.Soft;
                    light.intensity=art.theme==FantasyMapTheme.Honeycomb?1.65f:art.theme==FantasyMapTheme.Carnival?1.45f:1.7f;
                    light.color=art.theme==FantasyMapTheme.Honeycomb?new Color(1,.95f,.83f):art.theme==FantasyMapTheme.Carnival?new Color(1,.94f,.90f):new Color(1,.98f,.96f);
                    light.transform.rotation=art.theme==FantasyMapTheme.Honeycomb?Quaternion.Euler(48,140,0):art.theme==FantasyMapTheme.Carnival?Quaternion.Euler(18,180,0):Quaternion.Euler(52,140,0);
                    var cameraObject=new GameObject("Preview capture",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,scene);
                    var camera=cameraObject.GetComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=1600;camera.nearClipPlane=.2f;camera.fieldOfView=62;
                    camera.transform.position=new Vector3(72,48,-86);camera.transform.LookAt(new Vector3(0,16,0));art.ConfigureCamera(camera);
                    var rt=RenderTexture.GetTemporary(1600,1000,24);var old=RenderTexture.active;var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);
                    try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,id+"-close.png"),image.EncodeToPNG());}
                    finally{RenderTexture.active=old;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
                    report.AppendLine(id+": source zero normals="+originalZeros+"; final invalid normals="+bad+"; bodies="+map.Blocks.Count);
                }
                finally{if(go){Invoke(go.GetComponent<FantasyMapArt>(),"OnDisable");Object.DestroyImmediate(go);}EditorSceneManager.ClosePreviewScene(scene);Physics.IgnoreLayerCollision(Layers.Survivor,Layers.Block,ignored);Services.World=previous;}
            }
            File.WriteAllText(Path.Combine(output,"Preview.txt"),report.ToString());
        }
    }
}
