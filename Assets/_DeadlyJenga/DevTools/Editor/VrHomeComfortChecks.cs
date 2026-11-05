using UnityEngine.Rendering;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Reflection;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class VrHomeComfortChecks
    {
        const string Key="VrHomeComfortChecks";
        static IEnumerator run;static int errors,checks;static bool failed;
        static string Report=>ProjectPath.Report("VrHomeComfortChecks.txt");
        [Serializable] sealed class Saved{public SceneSetup[] scenes;public bool background;}
        static VrHomeComfortChecks(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        public static void Run()
        {
            string root=Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
            if(Path.GetFullPath(File.ReadAllText(ProjectPath.Report("CandyTerminalDesign.owner")).Trim()).ToLowerInvariant()!=root.ToLowerInvariant())throw new InvalidOperationException("Wrong owned editor.");
            if(EditorApplication.isCompiling||EditorApplication.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Editor busy.");
            if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Unsaved scenes untouched.");
            if(!ValidationRunGate.TryAcquire(Key))return;
            SessionState.SetString(Key+"Saved",JsonUtility.ToJson(new Saved{scenes=EditorSceneManager.GetSceneManagerSetup(),background=Application.runInBackground}));
            SessionState.SetBool(Key,true);File.WriteAllText(Report,"Actual Unity VR Home comfort validation\n"+root+"\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Home.unity"));EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){SessionState.SetBool(Key+"Entered",true);errors=checks=0;failed=false;Application.logMessageReceived+=Log;AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();run=Flatten(Validate());new GameObject("VR Home validation driver").AddComponent<TidalCheckDriver>().StartCoroutine(Drive());}
            if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"Entered",false))
            {
                SessionState.SetBool(Key+"Entered",false);Application.logMessageReceived-=Log;run=null;SessionState.SetBool(Key,false);
                var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Saved",""));Application.runInBackground=saved.background;
                if(saved.scenes.Any(s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
                AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();ValidationRunGate.Release(Key);
                File.AppendAllText(Report,"COMPLETE checks="+checks+" errors="+errors+" failed="+failed+"\nActual Home scene with a scripted headset pose. Physical headset comfort and stereo output remain untested.\n");
            }
        }
        static void Log(string m,string trace,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            // Known independent editor service failures are recorded separately.
            if(trace.Contains("UnityEditor.Search.SearchDatabase")||trace.Contains("UnityEditor.AsyncHTTPClient")){File.AppendAllText(Report,"EDITOR SERVICE "+m+"\n");return;}
            errors++;File.AppendAllText(Report,"ERROR "+m+"\n"+trace+"\n");
        }
        static void Tick()
        {
            if(run==null)
            {
                string request=ProjectPath.Report("VrHomeComfortChecks.request");
                if(!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlaying&&!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(request)&&File.ReadAllText(request).Trim()==System.Diagnostics.Process.GetCurrentProcess().Id.ToString()){if(!ValidationRunGate.TryAcquire(Key))return;File.Delete(request);Run();}
                return;
            }
        }
        static IEnumerator Drive()
        {
            while(run!=null)
            {
                object current;
                try{if(!run.MoveNext())break;current=run.Current;}
                catch(Exception e){failed=true;File.AppendAllText(Report,"FAIL "+e+"\n");break;}
                yield return current;
            }
            (run as IDisposable)?.Dispose();run=null;EditorApplication.ExitPlaymode();
        }
        static void Check(bool ok,string text){checks++;File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+text+"\n");if(!ok)throw new Exception(text);}
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
        static IEnumerator Until(Func<bool> ready,string label,float seconds=40){float until=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;Check(ready(),label);}
        static IEnumerator Flatten(IEnumerator source)
        {
            while(source.MoveNext())
            {
                if(source.Current is IEnumerator nested){var child=Flatten(nested);while(child.MoveNext())yield return child.Current;}
                else if(source.Current is AsyncOperation operation){while(!operation.isDone)yield return null;}
                
                else yield return source.Current;
            }
        }
        static IEnumerator Validate()
        {
            Application.runInBackground=true;
            HomeMenuView native=null;
            yield return Until(()=>(native=Object.FindFirstObjectByType<HomeMenuView>()),"Home scene installs its real desktop view");
            var desktop=native.GetComponentInChildren<Canvas>();
            Check(desktop.renderMode==RenderMode.ScreenSpaceOverlay,"Desktop menu retains its screen-space layout");
            var controls=native.GetComponent<IHomeMenuControls>();
            Check(controls!=null,"Home supplies its real session controls");
            native.gameObject.SetActive(false);
            var root=new GameObject("Scripted VR Home");
            try
            {
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var view=root.AddComponent<HomeMenuView>();
                var type=typeof(HomeMenuView);
                type.GetMethod("Build",flags).Invoke(view,new object[]{controls,true});
                view.enabled=false;
                var eye=root.GetComponentInChildren<Camera>();
                eye.enabled=false;
                eye.GetUniversalAdditionalCameraData().allowXRRendering=false;
                Object.DestroyImmediate(eye.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>());
                var frame=(RectTransform)root.transform.Find("Home menu");
                eye.transform.SetPositionAndRotation(new Vector3(2,1.75f,-1),Quaternion.Euler(35,53,20));
                var late=type.GetMethod("LateUpdate",flags);
                for(int i=0;i<4;i++)late.Invoke(view,null);
                Check(Mathf.Abs(frame.position.y-1.75f)<.001f&&Vector3.Dot(frame.up,Vector3.up)>.9999f,"Opening uses headset height and yaw without pitch or roll");
                var position=frame.position;var rotation=frame.rotation;
                eye.transform.position+=new Vector3(.3f,.12f,-.2f);
                eye.transform.rotation=Quaternion.Euler(-21,82,7);
                for(int i=0;i<12;i++)late.Invoke(view,null);
                Check(frame.position==position&&frame.rotation==rotation,"Head movement leaves the sharp menu anchored");
                var recenter=frame.Find("RECENTER VIEW").GetComponent<UnityEngine.UI.Button>();
                recenter.onClick.Invoke();late.Invoke(view,null);late.Invoke(view,null);
                Check(Vector3.Distance(frame.position,eye.transform.position+frame.forward*3)<.002f&&Mathf.Abs(frame.eulerAngles.y-82)<.01f,"Recenter button reanchors to the current headset pose");
                var sky=eye.GetComponent<Skybox>();
                Check(sky&&sky.material&&!ShaderUtil.ShaderHasError(sky.material.shader)&&eye.clearFlags==CameraClearFlags.Skybox,"VR camera has a compiled private backdrop shader");
                var slide=(RenderTexture)sky.material.GetTexture("_Slide");
                Check(slide&&slide.IsCreated()&&slide.mipmapCount>4,"Backdrop caches a prefiltered copy of the slideshow");
                eye.transform.rotation=frame.rotation*Quaternion.Euler(14,55,6);
                Capture(eye,"VrHome-OffAxis.png",true);
                eye.transform.rotation=frame.rotation*Quaternion.Euler(-14,160,0);
                Capture(eye,"VrHome-Behind.png",true);
                eye.transform.rotation=frame.rotation;
                Capture(eye,"VrHome-Centered.png",false);
            }
            finally
            {
                Object.Destroy(root);
                if(native)native.gameObject.SetActive(true);
            }
        }

        static void Capture(Camera camera,string name,bool expectBackdrop)
        {
            var rt=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;
            Texture2D image=null;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;
                image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);
                image.Apply();
                File.WriteAllBytes(ProjectPath.Report(name),image.EncodeToPNG());
                if(expectBackdrop)
                {
                    var a=image.GetPixel(60,60);var b=image.GetPixel(850,460);var center=image.GetPixel(480,270);
                    Check(Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)>.04f&&
                        Mathf.Max(center.r,center.g,center.b)>.13f,name+" renders slide colors beyond the panel");
                }
            }
            finally
            {
                RenderTexture.active=previous;
                RenderTexture.ReleaseTemporary(rt);
                if(image)Object.Destroy(image);
            }
        }
    }
}
