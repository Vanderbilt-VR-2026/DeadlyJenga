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
                File.AppendAllText(Report,"COMPLETE: two current disasters, four 1920x1080 captures. No visual implementation changed.\n");
            }
            catch(Exception error){File.AppendAllText(Report,"FAILED: "+error+"\n");}
            while(steps.Count>0)(steps.Pop() as IDisposable)?.Dispose();run=null;
            AudioListener.volume=volume;Application.runInBackground=background;
            EditorApplication.ExitPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
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
