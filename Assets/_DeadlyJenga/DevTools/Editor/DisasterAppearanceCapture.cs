using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyArt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class DisasterAppearanceCapture
    {
        const string Key="DisasterAppearanceCapture";
        static IEnumerator run;
        static readonly Stack<IEnumerator> steps=new Stack<IEnumerator>();
        static string Output=>Path.Combine(Path.GetDirectoryName(Application.dataPath),"ArtDirection","DisasterRedesign-20261026");
        static string Report=>Path.Combine(Output,"Capture.txt");
        const string GameplayKey=Key+"Gameplay";
        static bool gameplayFailed;
        static string GameplayReport=>ProjectPath.Report("TornadoGameplay.txt");

        public static void RunTornadoGameplay()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use the checked batch runner.");
            SessionState.SetBool(GameplayKey,true);
            File.WriteAllText(GameplayReport,"Production Game, Candy Castle, survivor camera, mobile URP settings on desktop.\n");
            MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=1;MatchSetup.HandCount=0;
            MatchSetup.FromLobby=true;MatchSetup.StartLocalSession=false;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }
        static float volume;
        static bool background;
        [Serializable] sealed class Saved {public SceneSetup[] scenes;public string map,mode;public int population,hands;public bool fromLobby,local;}
        static DisasterAppearanceCapture(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        static void Tick()
        {
            if(run==null)
            {
                string request=ProjectPath.Report("DisasterAppearanceCapture.request");
                if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                if(File.ReadAllText(request).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString())return;
                if(!ValidationRunGate.TryAcquire(Key))return;
                File.Delete(request);Directory.CreateDirectory(Output);
                var scenes=EditorSceneManager.GetSceneManagerSetup();
                if(scenes.Any(s=>s.isLoaded&&(string.IsNullOrEmpty(s.path)||SceneManager.GetSceneByPath(s.path).isDirty)))
                {File.WriteAllText(Report,"Stopped: unsaved scenes left untouched.");ValidationRunGate.Release(Key);return;}
                SessionState.SetString(Key+"Saved",JsonUtility.ToJson(new Saved{scenes=scenes,map=MatchSetup.MapId,mode=MatchSetup.ModeId,population=MatchSetup.BotTarget,hands=MatchSetup.HandCount,fromLobby=MatchSetup.FromLobby,local=MatchSetup.StartLocalSession}));
                File.WriteAllText(Report,"Fresh production Game Play Mode captures; current materials and shaders. "+DateTime.Now.ToString("O")+"\n");
                MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=1;MatchSetup.HandCount=0;MatchSetup.FromLobby=true;MatchSetup.StartLocalSession=false;
                SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();return;
            }
            try
            {
                if(steps.Count==0)steps.Push(run);
                while(steps.Count>0)
                {
                    var step=steps.Peek();if(!step.MoveNext()){(step as IDisposable)?.Dispose();steps.Pop();continue;}
                    if(step.Current is IEnumerator nested){steps.Push(nested);continue;}return;
                }
                if (SessionState.GetBool(GameplayKey,false)) File.AppendAllText(GameplayReport,"PASS Gameplay tornado check completed.\n");
                else File.AppendAllText(Report,"COMPLETE: two current disasters, four 1920x1080 captures. No visual implementation changed.\n");
            }
            catch (Exception error)
            {
                if (SessionState.GetBool(GameplayKey,false))
                {
                    gameplayFailed=true;
                    File.AppendAllText(GameplayReport,"FAIL "+error+"\n");
                }
                else File.AppendAllText(Report,"FAILED: "+error+"\n");
            }
            while(steps.Count>0)(steps.Pop() as IDisposable)?.Dispose();run=null;
            AudioListener.volume=volume;Application.runInBackground=background;
            EditorApplication.ExitPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if (SessionState.GetBool(GameplayKey,false))
            {
                if (state==PlayModeStateChange.EnteredPlayMode)
                {
                    gameplayFailed=false;
                    volume=AudioListener.volume;background=Application.runInBackground;
                    AudioListener.volume=0;Application.runInBackground=true;
                    run=CaptureTornadoGameplay();
                }
                else if (state==PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(GameplayKey,false);
                    EditorApplication.Exit(gameplayFailed?2:0);
                }
                return;
            }
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
            {
                SessionState.SetBool(Key,false);SessionState.SetBool(Key+"Running",true);
                EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();
                volume=AudioListener.volume;background=Application.runInBackground;AudioListener.volume=0;Application.runInBackground=true;
                run=Capture();
            }
            if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"Running",false))
            {
                SessionState.SetBool(Key+"Running",false);run=null;
                AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
                var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Saved",""));
                MatchSetup.MapId=saved.map;MatchSetup.ModeId=saved.mode;MatchSetup.BotTarget=saved.population;MatchSetup.HandCount=saved.hands;MatchSetup.FromLobby=saved.fromLobby;MatchSetup.StartLocalSession=saved.local;
                EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);ValidationRunGate.Release(Key);
            }
        }
        static IEnumerator CaptureTornadoGameplay()
        {
            float until=Time.realtimeSinceStartup+65;
            while ((Services.Session?.Demo==null || Services.World==null) && Time.realtimeSinceStartup<until) yield return null;
            var session=Object.FindFirstObjectByType<CastlePlaySession>();
            var art=Object.FindFirstObjectByType<CandyDisasterVisuals>();
            if (!session || !art || session.Network || !session.ActiveCamera) throw new InvalidOperationException("Game or active player camera is unavailable.");
            session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots?.Clear();
            session.SetMenu(false);session.Player.InputSuspended=true;
            var camera=session.ActiveCamera;
            var cameraData=camera.GetUniversalAdditionalCameraData();
            var previousDepth=cameraData.requiresDepthOption;
            var previousPipeline=QualitySettings.renderPipeline;
            const BindingFlags member=BindingFlags.Instance|BindingFlags.NonPublic;
            var visualType=typeof(CandyDisasterVisuals);
            try
            {
                var mobile=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ProjectPath.Find("Mobile_RPAsset.asset"));
                if (!mobile) throw new InvalidOperationException("Mobile URP asset is unavailable.");
                QualitySettings.renderPipeline=mobile;
                cameraData.requiresDepthOption=CameraOverrideOption.Off;
                visualType.GetField("compactStorm",member).SetValue(art,true);
                var material=(Material)visualType.GetField("storm",member).GetValue(art);
                material.SetFloat("_MarchBudget",20);
                if (!material.shader.isSupported || ShaderUtil.ShaderHasError(material.shader))
                    throw new InvalidOperationException("The production funnel shader failed to compile.");
                yield return null;
                var tornado=DemoDisasters.Prefabs.First(prefab=>prefab.name=="Tornado");
                File.AppendAllText(GameplayReport,DemoDisasters.Request(session,tornado)+"\n");
                var source=DisasterStates.Active.FirstOrDefault(s=>s.Capture()?.kind=="Tornado");
                var warning=source?.Capture();
                if (warning==null || warning.warning<=0) throw new InvalidOperationException("The production request did not start the warning.");
                var eye=camera.transform.position;
                var forward=Quaternion.LookRotation(warning.center+Vector3.up*warning.values[0]*.32f-eye);
                camera.transform.SetPositionAndRotation(eye,forward);
                var baseline=ReadTornadoCamera(camera,null);
                until=Time.realtimeSinceStartup+14;
                while ((source.Capture()?.warning??1)>0 && Time.realtimeSinceStartup<until) yield return null;
                yield return null;
                var live=source.Capture();
                if (live==null || live.warning>0 || !cameraData.requiresDepthTexture)
                    throw new InvalidOperationException("The active tornado did not obtain depth on the player camera.");
                camera.transform.SetPositionAndRotation(eye,forward);
                visualType.GetMethod("LateUpdate",member).Invoke(art,null);
                var actual=ReadTornadoCamera(camera,"Tornado-Player-Mobile-URP.png");
                int changed=0;
                for(int i=baseline.Length/2;i<baseline.Length;i++)
                {
                    int difference=Math.Abs(baseline[i].r-actual[i].r)+Math.Abs(baseline[i].g-actual[i].g)+Math.Abs(baseline[i].b-actual[i].b);
                    if(difference>55)changed++;
                }
                File.AppendAllText(GameplayReport,$"Player camera at {eye}; center {live.center}; far clip {camera.farClipPlane}; changed upper-screen pixels {changed}.\n");
                if(changed<450)throw new InvalidOperationException("No substantial tornado silhouette appeared in the player view.");
                until=Time.realtimeSinceStartup+13;
                while ((source.Capture()?.age??0)<8.7f && Time.realtimeSinceStartup<until) yield return null;
                live=source.Capture();
                if(live==null) throw new InvalidOperationException("The tornado ended before the approach capture.");
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(live.center+Vector3.up*live.values[0]*.36f-eye));
                visualType.GetMethod("LateUpdate",member).Invoke(art,null);
                ReadTornadoCamera(camera,"Tornado-Player-Approach.png");
                File.AppendAllText(GameplayReport,$"Approach capture at event age {live.age:F2}s and center {live.center}.\n");
                DemoDisasters.StopAll(session);
                yield return new WaitForSecondsRealtime(1.9f);
                if(cameraData.requiresDepthOption!=CameraOverrideOption.Off)
                    throw new InvalidOperationException("Tornado did not release its camera depth override.");
            }
            finally
            {
                DemoDisasters.StopAll(session);
                cameraData.requiresDepthOption=previousDepth;
                QualitySettings.renderPipeline=previousPipeline;
            }
        }

        static Color32[] ReadTornadoCamera(Camera camera,string filename)
        {
            var target=RenderTexture.GetTemporary(960,540,24);
            var previous=RenderTexture.active;
            var oldTarget=camera.targetTexture;
            var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
                if(filename!=null)File.WriteAllBytes(ProjectPath.Report(filename),texture.EncodeToPNG());
                return texture.GetPixels32();
            }
            finally
            {
                camera.targetTexture=oldTarget;RenderTexture.active=previous;
                RenderTexture.ReleaseTemporary(target);Object.Destroy(texture);
            }
        }

        static IEnumerator Capture()
        {
            float deadline=Time.realtimeSinceStartup+40;
            while(Services.Session?.Demo==null&&Time.realtimeSinceStartup<deadline)yield return null;
            var session=Object.FindFirstObjectByType<CastlePlaySession>();
            if(!session||Services.World==null)throw new InvalidOperationException("Actual Game session unavailable.");
            if(session.Network)throw new InvalidOperationException("Capture refuses an active network session.");
            session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots?.Clear();
            if(session.Social!=null)session.Social.VoiceEnabled=false;
            session.SetRole(true);session.SetMenu(false);session.Player.InputSuspended=true;session.Hand.InputSuspended=true;
            DemoDisasters.StopAll(session);
            var tornado=DemoDisasters.Prefabs.First(p=>p.name=="Tornado");
            var parameters=DemoDisasters.Parameters(tornado);
            for(int i=0;i<parameters.Count;i++)if(parameters[i].id=="warning")DemoDisasters.SetValue(session,tornado,i,0);
            File.AppendAllText(Report,DemoDisasters.Request(session,tornado)+"\n");
            yield return new WaitForSecondsRealtime(1.2f);
            var source=DisasterStates.Active.FirstOrDefault(s=>s.Capture()?.kind=="Tornado");
            if(source==null)throw new InvalidOperationException("Tornado did not start.");
            var f=source.Capture();float height=f.values[0];var world=Services.World;
            var bounds=world.PlayableBounds;bounds.Encapsulate(new Bounds(f.center+Vector3.up*height*.5f,new Vector3(f.values[1]*2.4f,height+f.values[1],f.values[1]*2.4f)));
            var target=bounds.center;float distance=Mathf.Max(bounds.size.y,bounds.size.x*.65f,bounds.size.z*.65f)*1.65f;
            Shot(session,"tornado-current.png",target+new Vector3(1,.65f,-1.15f).normalized*distance,target,52);
            var outwards=Vector3.ProjectOnPlane(f.center-world.Center,Vector3.up).normalized;if(outwards.sqrMagnitude<.1f)outwards=Vector3.back;
            Shot(session,"tornado-current-close.png",f.center+outwards*height*1.25f+Vector3.up*height*.18f,f.center+Vector3.up*height*.45f,52);
            File.WriteAllText(Path.Combine(Output,"tornado-frame.json"),JsonUtility.ToJson(f,true));
            DemoDisasters.StopAll(session);yield return null;
            session.ResetMap();yield return new WaitForSecondsRealtime(.5f);
            var tsunami=DemoDisasters.Prefabs.First(p=>p.name=="Tsunami");
            File.AppendAllText(Report,DemoDisasters.Request(session,tsunami)+"\n");
            deadline=Time.realtimeSinceStartup+30;IWaveState wave=null;
            while(Time.realtimeSinceStartup<deadline)
            {
                wave=Tracked.Waves.FirstOrDefault(w=>w.Wave.active);
                if(wave!=null&&wave.Wave.warningRemaining<=0&&wave.Wave.height>18)break;
                yield return null;
            }
            if(wave==null||wave.Wave.height<5)throw new InvalidOperationException("Tsunami did not form.");
            var w=wave.Wave;world=Services.World;Vector3 side=Vector3.Cross(Vector3.up,w.direction).normalized;
            bounds=world.PlayableBounds;bounds.Encapsulate(new Bounds(w.center+Vector3.up*w.height*.5f,new Vector3(w.width,w.height,w.depth)));
            target=(world.PlayableBounds.center+w.center+Vector3.up*w.height*.35f)*.5f;
            float span=Mathf.Max(w.width,world.Radius*2);distance=span*1.25f;
            Shot(session,"tsunami-current.png",target+w.direction*distance*.9f+side*distance*.65f+Vector3.up*distance*.45f,target,54);
            Shot(session,"tsunami-current-close.png",w.center+w.direction*Mathf.Max(50,w.width*.75f)+side*w.width*.3f+Vector3.up*6,w.center+Vector3.up*w.height*.45f,56);
            File.WriteAllText(Path.Combine(Output,"tsunami-frame.json"),JsonUtility.ToJson(w,true));
            DemoDisasters.StopAll(session);
        }
        static void Shot(CastlePlaySession session,string file,Vector3 position,Vector3 target,float fov)
        {
            var camera=session.ActiveCamera;camera.transform.position=position;camera.transform.LookAt(target);camera.fieldOfView=fov;camera.farClipPlane=1800;
            foreach(var art in Object.FindObjectsByType<CandyDisasterVisuals>(FindObjectsSortMode.None))
                typeof(CandyDisasterVisuals).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(art,null);
            foreach(var art in Object.FindObjectsByType<CandyTsunamiVisuals>(FindObjectsSortMode.None))
                typeof(CandyTsunamiVisuals).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(art,null);
            var rt=RenderTexture.GetTemporary(1920,1080,24);var old=RenderTexture.active;var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1920,1080),0,0);texture.Apply();
                File.WriteAllBytes(Path.Combine(Output,file),texture.EncodeToPNG());
                File.AppendAllText(Report,"Captured "+file+"; position="+position+"; target="+target+"; FOV="+fov+"\n");
            }
            finally{camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Object.Destroy(texture);}
        }
    }
}
