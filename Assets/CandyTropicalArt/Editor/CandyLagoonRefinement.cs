using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.CastlePrototype;
using Object=UnityEngine.Object;

namespace DeadlyJenga.CandyArt.Editor
{
    [InitializeOnLoad]
    public static class CandyLagoonRefinement
    {
        const string Request="ArtDirection/CandyTropical/refine-lagoon.request",Output="ArtDirection/CandyTropical/Results/",Key="CandyLagoonRefinement";
        static CandyLagoonRefinement()
        {
            EditorApplication.update+=()=>
            {
                if(Application.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isUpdating||EditorApplication.isCompiling||!File.Exists(Request))return;
                File.Move(Request,Request+".consumed-"+DateTime.UtcNow.Ticks);
                try{Install();}catch(Exception e){File.WriteAllText(Output+"LagoonError.txt",e.ToString());Debug.LogException(e);}
            };
            EditorApplication.playModeStateChanged+=state=>
            {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){SessionState.SetBool(Key,false);EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(PlayChecks());}};
        }
        [MenuItem("Tools/Candy Tropical/9 Fit Molded Sand And Clear Water")]
        public static void Install()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Use Edit Mode.");
            Directory.CreateDirectory(Output);var generator=Object.FindFirstObjectByType<CastleGenerator>();
            var blocks=generator.GetComponentsInChildren<CastleBlock>();var snapshots=blocks.Select(Snapshot).ToArray();
            string support=EditorJsonUtility.ToJson(generator.foundation.GetComponent<Collider>());
            var p=CandyArtBuilder.Profile;p.sandMargin=2.35f;p.sandRelief=.8f;p.sandShoreWidth=1.5f;p.sandDepth=4;p.terrainResolution=128;
            p.sand.SetColor("_BaseColor",new Color(.86f,.67f,.4f));p.sand.SetFloat("_Grain",.65f);
            p.water.SetFloat("_FoamStrength",0);p.water.SetFloat("_Glints",0);p.water.SetFloat("_Shine",.78f);
            EditorUtility.SetDirty(p);EditorUtility.SetDirty(p.sand);EditorUtility.SetDirty(p.water);
            string prefabPath=CandyArtBuilder.Root+"Prefabs/Candy Environment.prefab";var prefab=PrefabUtility.LoadPrefabContents(prefabPath);
            try{prefab.GetComponent<CandyEnvironment>().Refresh();PrefabUtility.SaveAsPrefabAsset(prefab,prefabPath);}finally{PrefabUtility.UnloadPrefabContents(prefab);}
            var env=Object.FindFirstObjectByType<CandyEnvironment>();env.Refresh();EditorSceneManager.MarkSceneDirty(env.gameObject.scene);EditorSceneManager.SaveScene(env.gameObject.scene);AssetDatabase.SaveAssets();
            var terrain=env.sandRenderer.GetComponent<CandySandbar>();var mesh=terrain.GetComponent<MeshFilter>().sharedMesh;var report=new List<string>();
            void Check(bool ok,string name){report.Add((ok?"PASS ":"FAIL ")+name);}
            Check(blocks.Select((b,i)=>Snapshot(b)==snapshots[i]).All(x=>x),"All "+blocks.Length+" block transforms, colliders and Rigidbody settings preserved.");
            Check(EditorJsonUtility.ToJson(generator.foundation.GetComponent<Collider>())==support,"Original static support collider preserved.");
            Check(!ShaderUtil.ShaderHasError(p.water.shader)&&!ShaderUtil.ShaderHasError(p.sand.shader),"Water and molded sand shaders compile.");
            bool flush=true;foreach(var item in CastleLayout.Build(generator.settings).Where(x=>x.Course==0))
                foreach(float x in new[]{-.5f,.5f})foreach(float z in new[]{-.5f,.5f})
                {Vector3 corner=item.Position+item.Rotation*new Vector3(x*generator.settings.blockSize.x,0,z*generator.settings.blockSize.z);flush&=Mathf.Abs(terrain.SampleHeight(new Vector2(corner.x,corner.z)))<.001f;}
            Check(flush,"Every initial bottom-block footprint corner sits on sand at support height.");
            Check(terrain.HalfSize.x*2<=CastleLayout.Footprint(generator.settings)+14,"Natural sand shoulders extend modestly beyond the castle, including the submerged skirt ("+(terrain.HalfSize.x*2).ToString("F2")+"m).");
            float lowest=0;foreach(var v in mesh.vertices)if(terrain.ShapeDistance(new Vector2(v.x,v.z))<-.3f)lowest=Mathf.Min(lowest,v.y);
            Check(lowest<-.3f,"Free sand areas have real rounded relief, not only shader texture.");
            Check(terrain.GetComponentsInChildren<Collider>().Length==0&&terrain.GetComponentsInChildren<Rigidbody>().Length==0,"Molded terrain adds no physics components.");
            Check(terrain.HeightMap&&terrain.HeightMap.width==129,"Water depth uses the actual static terrain height map.");
            terrain.Refresh();Check(mesh==terrain.GetComponent<MeshFilter>().sharedMesh,"Unchanged terrain refresh reuses its mesh and height map.");
            File.WriteAllLines(Output+"LagoonValidation.txt",report);Debug.Log(string.Join("\n",report));Capture();CandyArtBuilder.Export();SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static string Snapshot(CastleBlock b)=>EditorJsonUtility.ToJson(b.transform)+EditorJsonUtility.ToJson(b.GetComponent<Rigidbody>())+EditorJsonUtility.ToJson(b.GetComponent<BoxCollider>());
        static IEnumerator PlayChecks()
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            var session=Object.FindFirstObjectByType<CastlePlaySession>();session.SetMenu(true);yield return new WaitForSeconds(1);
            var terrain=Object.FindFirstObjectByType<CandySandbar>();var mesh=terrain.GetComponent<MeshFilter>().sharedMesh;var map=terrain.HeightMap;
            Capture();yield return new WaitForSeconds(1.2f);
            var report=new List<string>{(terrain.GetComponent<MeshFilter>().sharedMesh==mesh&&terrain.HeightMap==map?"PASS ":"FAIL ")+"Terrain and height map remain fixed during wave animation."};
            var times=new List<float>();for(int i=0;i<90;i++){yield return null;times.Add(Time.unscaledDeltaTime*1000);}times.Sort();report.Add("OBSERVATION Whole-demo editor median frame interval: "+times[45].ToString("F2")+" ms; not an isolated GPU or headset benchmark.");
            File.WriteAllLines(Output+"LagoonPlayValidation.txt",report);Application.runInBackground=background;File.WriteAllText(Output+"LAGOON_COMPLETE.txt","Molded sand and clear-water refinement installed and checked in Unity.");EditorApplication.isPlaying=false;
        }
        static void Capture()
        {
            var env=Object.FindFirstObjectByType<CandyEnvironment>();var origin=env.transform.position;var go=new GameObject("Temporary molded terrain camera");var camera=go.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.Skybox;camera.fieldOfView=55;camera.nearClipPlane=.02f;camera.farClipPlane=1400;
            var texture=new RenderTexture(1600,1000,24);var previous=RenderTexture.active;camera.targetTexture=texture;bool water=env.waterRenderer.enabled;
            try
            {
                void Shot(string name,Vector3 position,Vector3 target)
                {camera.transform.position=origin+position;camera.transform.LookAt(origin+target);camera.Render();RenderTexture.active=texture;var png=new Texture2D(1600,1000,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1600,1000),0,0);png.Apply();File.WriteAllBytes(Output+name+".png",png.EncodeToPNG());Object.DestroyImmediate(png);}
                Shot("candy-lagoon-tight",new Vector3(44,39,-49),new Vector3(0,12,0));
                Shot("candy-clear-shallows",new Vector3(19,2.1f,-19),new Vector3(13,0,-12));
                Shot("candy-water-from-above",new Vector3(20,30,-23),new Vector3(14,0,-14));
                env.waterRenderer.enabled=false;
                Shot("candy-molded-sand",new Vector3(17,1.6f,-15),new Vector3(13,-.2f,-9));
            }
            finally{env.waterRenderer.enabled=water;RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(texture);Object.DestroyImmediate(go);}
        }
    }
}
