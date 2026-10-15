using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPlayers.Editor;
using Object=UnityEngine.Object;

namespace DeadlyJenga.CandyArt.Editor
{
    [InitializeOnLoad]
    public static class CandyPolishUpgrade
    {
        const string Request="ArtDirection/CandyTropical/polish.request",Output="ArtDirection/CandyTropical/Results/";
        static CandyPolishUpgrade(){EditorApplication.update+=Watch;}
        static void Watch()
        {
            if(Application.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
            string command=File.ReadAllText(Request).Trim();File.Move(Request,Request+".consumed-"+DateTime.UtcNow.Ticks);
            try
            {
                if(command=="export-only"){CandyArtVerification.Capture();CandyArtBuilder.Export();CandyPlayerBuilder.Export();File.WriteAllText(Output+"POLISH_EXPORT_COMPLETE.txt","Current art and player packages exported separately.");}
                else Upgrade();
            }
            catch(Exception e){Directory.CreateDirectory(Output);File.WriteAllText(Output+"PolishError.txt",e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Tools/Candy Tropical/8 Smooth Players And Solid Lagoon")]
        public static void Upgrade()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Use Edit Mode.");
            var blocks=Object.FindObjectsByType<CastleBlock>(FindObjectsSortMode.None);
            var before=blocks.Select(b=>Snapshot(b)).ToArray();
            var foundation=Object.FindFirstObjectByType<CastleGenerator>().foundation;
            string supportBefore=EditorJsonUtility.ToJson(foundation.GetComponent<Collider>());
            CandyPlayerBuilder.RebuildVisual();var p=CandyArtBuilder.Profile;
            p.waterHeight=.08f;p.alignSandToFoundation=true;p.sandPlateau=.8f;p.sandDepth=3.2f;p.sandSegments=72;
            p.sand.shader=Shader.Find("Candy Tropical/Sand");p.sand.SetColor("_BaseColor",new Color(.94f,.81f,.56f));
            p.water.SetColor("_Shallow",new Color(.035f,.82f,.78f));p.water.SetColor("_Deep",new Color(.015f,.32f,.65f));
            p.water.SetFloat("_Shine",.85f);p.water.SetFloat("_FoamStrength",.2f);p.water.SetFloat("_RippleScale",1.25f);p.water.SetFloat("_Speed",.42f);p.water.SetFloat("_Glints",.3f);
            p.water.enableInstancing=true;EditorUtility.SetDirty(p);EditorUtility.SetDirty(p.sand);EditorUtility.SetDirty(p.water);
            string path=CandyArtBuilder.Root+"Prefabs/Candy Environment.prefab";var prefab=PrefabUtility.LoadPrefabContents(path);
            try{InstallTerrain(prefab.GetComponent<CandyEnvironment>(),p);PrefabUtility.SaveAsPrefabAsset(prefab,path);}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            var environments=Object.FindObjectsByType<CandyEnvironment>(FindObjectsSortMode.None);
            foreach(var env in environments)
            {
                InstallTerrain(env,p);
                var baseline=env.GetComponent<CandyArtBaseline>();if(baseline)foreach(var state in baseline.renderers)if(state.renderer)state.renderer.enabled=false;
                EditorUtility.SetDirty(env);EditorSceneManager.MarkSceneDirty(env.gameObject.scene);EditorSceneManager.SaveScene(env.gameObject.scene);
            }
            AssetDatabase.SaveAssets();Directory.CreateDirectory(Output);
            var report=new List<string>();void Check(bool ok,string name){report.Add((ok?"PASS ":"FAIL ")+name);}
            Check(blocks.Select((b,i)=>Snapshot(b)==before[i]).All(x=>x),"All "+blocks.Length+" block transforms, Rigidbody settings and colliders preserved.");
            Check(EditorJsonUtility.ToJson(foundation.GetComponent<Collider>())==supportBefore,"Original static support collider preserved.");
            Check(!ShaderUtil.ShaderHasError(p.water.shader)&&!ShaderUtil.ShaderHasError(p.sand.shader),"Water and sand shaders compile.");
            foreach(var env in environments)
            {
                var terrain=env.sandRenderer.GetComponent<CandySandbar>();var mesh=terrain.GetComponent<MeshFilter>().sharedMesh;
                Check(mesh.bounds.size.y>3,"Sandbar is a volumetric terrain mesh with a sloping seabed.");
                Check(Mathf.Abs(env.sandRenderer.transform.position.y-foundation.GetComponent<Collider>().bounds.max.y)<.001f,"Level sand support is flush with the physics foundation top.");
                Check(terrain.GetComponentsInChildren<Collider>().Length==0&&terrain.GetComponentsInChildren<Rigidbody>().Length==0,"Terrain adds no physics components.");
                Check(env.GetComponent<CandyArtBaseline>().renderers.All(s=>!s.renderer||!s.renderer.enabled),"Legacy ocean and gray foundation rendering remain hidden.");
            }
            var visual=AssetDatabase.LoadAssetAtPath<DeadlyJenga.CandyPlayers.CandyPlayerRig>(CandyPlayerBuilder.PrefabPath);
            Check(visual.BoneCount==44,"All 44 body and finger bones preserved.");
            Check(visual.bodyRenderer.sharedMesh.boneWeights.Any(w=>w.weight1>.01f),"Continuous skin blends across tracked joints.");
            File.WriteAllLines(Output+"PolishValidation.txt",report);Debug.Log(string.Join("\n",report));
            CaptureTerrain();
            CandyArtBuilder.Export();CandyPlayerBuilder.Export();
            // Full tracking/vault checks also capture the repaired first-person body and hand poses.
            CandyPlayerValidation.Run();
        }
        static string Snapshot(CastleBlock b)=>EditorJsonUtility.ToJson(b.transform)+EditorJsonUtility.ToJson(b.GetComponent<Rigidbody>())+EditorJsonUtility.ToJson(b.GetComponent<BoxCollider>());
        static void CaptureTerrain()
        {
            var env=Object.FindFirstObjectByType<CandyEnvironment>();var origin=env.transform.position;
            var go=new GameObject("Temporary terrain evidence camera");var camera=go.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.Skybox;camera.fieldOfView=58;camera.nearClipPlane=.02f;camera.farClipPlane=1400;
            var texture=new RenderTexture(1600,1000,24);camera.targetTexture=texture;var previous=RenderTexture.active;bool water=env.waterRenderer.enabled;
            try
            {
                void Shot(string filename,Vector3 position,Vector3 target)
                {
                    camera.transform.position=origin+position;camera.transform.LookAt(origin+target);camera.Render();RenderTexture.active=texture;
                    var png=new Texture2D(1600,1000,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1600,1000),0,0);png.Apply();File.WriteAllBytes(Output+filename+".png",png.EncodeToPNG());Object.DestroyImmediate(png);
                }
                Shot("candy-lagoon",new Vector3(48,7,-56),new Vector3(6,-.5f,-7));
                env.waterRenderer.enabled=false;
                Shot("candy-terrain-cutaway",new Vector3(65,4,-72),new Vector3(0,-1,0));
            }
            finally{env.waterRenderer.enabled=water;RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(texture);Object.DestroyImmediate(go);}
        }
        static void InstallTerrain(CandyEnvironment env,CandyArtProfile p)
        {var terrain=env.sandRenderer.GetComponent<CandySandbar>();if(!terrain)terrain=env.sandRenderer.gameObject.AddComponent<CandySandbar>();terrain.profile=p;terrain.Refresh();env.Refresh();EditorUtility.SetDirty(terrain);}
    }
}
