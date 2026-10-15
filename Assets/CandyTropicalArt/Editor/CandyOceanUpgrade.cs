using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.CastlePrototype;
using Object=UnityEngine.Object;

namespace DeadlyJenga.CandyArt.Editor
{
    [InitializeOnLoad]
    public static class CandyOceanUpgrade
    {
        const string Request="ArtDirection/CandyTropical/upgrade-ocean.request";
        const string Output="ArtDirection/CandyTropical/Results/";
        const string PlayKey="CandyOceanPlayCheck";
        static CandyOceanUpgrade()
        {
            EditorApplication.update+=Watch;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(PlayKey,false))
                {SessionState.SetBool(PlayKey,false);EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(PlayCheck());}
            };
        }
        static void Watch()
        {
            if(Application.isPlaying||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
            File.Move(Request,Request+".consumed-"+DateTime.UtcNow.Ticks);
            try{Upgrade();}catch(Exception e){Directory.CreateDirectory(Output);File.WriteAllText(Output+"OceanError.txt",e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Tools/Candy Tropical/7 Upgrade Rolling Ocean")]
        public static void Upgrade()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Upgrade in Edit Mode.");
            var blocks=Object.FindObjectsByType<CastleBlock>(FindObjectsSortMode.None);
            var snapshots=blocks.Select(b=>new[]{EditorJsonUtility.ToJson(b.transform),EditorJsonUtility.ToJson(b.GetComponent<Rigidbody>()),EditorJsonUtility.ToJson(b.GetComponent<BoxCollider>())}).ToArray();
            CandyArtBuilder.Build();var p=CandyArtBuilder.Profile;
            // The upgrade tunes materials once; subsequent applications preserve live artist settings.
            string marker="ArtDirection/CandyTropical/ocean-installed.txt";
            if(!File.Exists(marker))
            {
                p.waterResolution=96;p.waveExtent=72;p.waveHeight=.32f;p.waveSpeed=.9f;p.waveChop=.4f;
                p.water.SetColor("_Shallow",new Color(.045f,.76f,.73f));p.water.SetColor("_Deep",new Color(.012f,.29f,.55f));
                p.water.SetColor("_Foam",new Color(.88f,1,.97f));p.water.SetFloat("_Glints",.2f);
                p.water.SetFloat("_RippleScale",.75f);p.water.SetFloat("_Speed",.35f);p.water.SetFloat("_Shine",.7f);p.water.SetFloat("_FoamStrength",.28f);
                EditorUtility.SetDirty(p);EditorUtility.SetDirty(p.water);
            }
            string prefabPath=CandyArtBuilder.Root+"Prefabs/Candy Environment.prefab";
            var prefab=PrefabUtility.LoadPrefabContents(prefabPath);
            try{Install(prefab.GetComponent<CandyEnvironment>(),p);PrefabUtility.SaveAsPrefabAsset(prefab,prefabPath);}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            var environments=Object.FindObjectsByType<CandyEnvironment>(FindObjectsSortMode.None);
            foreach(var environment in environments)
            {Install(environment,p);EditorUtility.SetDirty(environment);EditorSceneManager.MarkSceneDirty(environment.gameObject.scene);EditorSceneManager.SaveScene(environment.gameObject.scene);}
            AssetDatabase.SaveAssets();Directory.CreateDirectory(Output);
            var results=new List<string>();
            void Check(bool ok,string description){results.Add((ok?"PASS ":"FAIL ")+description);}
            Check(environments.Length>0,"Installed rolling ocean in the active scene.");
            Check(!ShaderUtil.ShaderHasError(p.water.shader),"Ocean shader compiles without errors.");
            Check(blocks.Select((b,i)=>snapshots[i].SequenceEqual(new[]{EditorJsonUtility.ToJson(b.transform),EditorJsonUtility.ToJson(b.GetComponent<Rigidbody>()),EditorJsonUtility.ToJson(b.GetComponent<BoxCollider>())})).All(x=>x),"All "+blocks.Length+" crate transforms, colliders and Rigidbody settings are unchanged.");
            foreach(var environment in environments)
            {
                var surface=environment.waterRenderer.GetComponent<CandyOceanSurface>();var mesh=surface.GetComponent<MeshFilter>().sharedMesh;
                Check(mesh.vertexCount==(p.waterResolution+1)*(p.waterResolution+9)&&mesh.triangles.Length/3==2*p.waterResolution*p.waterResolution+8*p.waterResolution,"Grid topology counts match profile: "+mesh.vertexCount+" vertices / "+mesh.triangles.Length/3+" triangles.");
                surface.Refresh();Check(mesh==surface.GetComponent<MeshFilter>().sharedMesh,"Repeated refresh reuses the mesh.");
                Check(surface.GetComponentsInChildren<Collider>().Length==0&&surface.GetComponentsInChildren<Rigidbody>().Length==0,"Ocean has no collider or Rigidbody.");
            }
            File.WriteAllLines(Output+"OceanValidation.txt",results);
            Capture();CandyArtBuilder.Export();File.WriteAllText(marker,"GPU ocean installed. Profile controls quality and motion; no physics changes.\n");
            Debug.Log("Rolling candy ocean installed and captured. "+string.Join("\n",results));
            SessionState.SetBool(PlayKey,true);EditorApplication.EnterPlaymode();
        }
        static IEnumerator PlayCheck()
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            var session=Object.FindFirstObjectByType<CastlePlaySession>();session.SetMenu(true);
            yield return new WaitForSeconds(2);
            var environment=Object.FindFirstObjectByType<CandyEnvironment>();var surface=environment.waterRenderer.GetComponent<CandyOceanSurface>();
            var mesh=surface.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;
            Capture();File.Copy(Output+"candy-ocean-waves.png",Output+"candy-ocean-wave-frame-a.png",true);
            yield return new WaitForSeconds(1.2f);
            Capture();File.Copy(Output+"candy-ocean-waves.png",Output+"candy-ocean-wave-frame-b.png",true);
            var report=new List<string>();
            report.Add((mesh==surface.GetComponent<MeshFilter>().sharedMesh&&vertices.SequenceEqual(mesh.vertices)?"PASS ":"FAIL ")+"Animated water retains the same mesh and vertex buffer.");
            report.Add((!File.ReadAllBytes(Output+"candy-ocean-wave-frame-a.png").SequenceEqual(File.ReadAllBytes(Output+"candy-ocean-wave-frame-b.png"))?"PASS ":"FAIL ")+"Fixed-camera rendered wave frames change over time.");
            var times=new List<float>();for(int i=0;i<90;i++){yield return null;times.Add(Time.unscaledDeltaTime*1000);}
            times.Sort();report.Add("OBSERVATION Editor median frame interval with ocean: "+times[times.Count/2].ToString("F2")+" ms. Includes editor, castle physics, avatar and other applications; not an isolated GPU benchmark or headset guarantee.");
            File.WriteAllLines(Output+"OceanPlayValidation.txt",report);Application.runInBackground=background;
            File.WriteAllText(Output+"OCEAN_COMPLETE.txt","Ocean installation and Play Mode checks completed. Read validation reports; hardware performance remains unverified.");
            EditorApplication.isPlaying=false;
        }
        static void Install(CandyEnvironment environment,CandyArtProfile profile)
        {
            var surface=environment.waterRenderer.GetComponent<CandyOceanSurface>();
            if(!surface)surface=environment.waterRenderer.gameObject.AddComponent<CandyOceanSurface>();
            surface.profile=profile;surface.Refresh();environment.Refresh();EditorUtility.SetDirty(surface);
        }
        static void Capture()
        {
            var environment=Object.FindFirstObjectByType<CandyEnvironment>();if(!environment)return;
            var origin=environment.transform.position;var go=new GameObject("Temporary ocean evidence camera");var camera=go.AddComponent<Camera>();
            camera.clearFlags=CameraClearFlags.Skybox;camera.fieldOfView=58;camera.farClipPlane=1200;camera.nearClipPlane=.05f;
            var texture=new RenderTexture(1600,1000,24);camera.targetTexture=texture;var previous=RenderTexture.active;
            try
            {
                void Shot(string name,Vector3 position,Vector3 target)
                {
                    camera.transform.position=origin+position;camera.transform.LookAt(origin+target);camera.Render();RenderTexture.active=texture;
                    var png=new Texture2D(1600,1000,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1600,1000),0,0);png.Apply();File.WriteAllBytes(Output+name+".png",png.EncodeToPNG());Object.DestroyImmediate(png);
                }
                Shot("candy-ocean-wide",new Vector3(72,48,-78),new Vector3(0,18,0));
                Shot("candy-ocean-waves",new Vector3(48,1.8f,-36),new Vector3(30,0,-15));
                Shot("candy-ocean-shallows",new Vector3(29,4,-32),new Vector3(12,0,-15));
            }
            finally{RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(go);Object.DestroyImmediate(texture);}
        }
    }
}
