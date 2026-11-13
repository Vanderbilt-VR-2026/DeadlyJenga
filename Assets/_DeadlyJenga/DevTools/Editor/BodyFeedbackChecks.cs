using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class BodyFeedbackChecks
    {
        const string Key="BodyFeedbackChecks";
        static string Output=>Path.GetFullPath("Logs/Validation/BodyFeedback");
        static string Report=>Path.Combine(Output,"report.txt");
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        static double began;
        static int errors;
        [Serializable] sealed class Saved { public SceneSetup[] scenes; }
        static BodyFeedbackChecks() { EditorApplication.update+=Tick; EditorApplication.playModeStateChanged+=State; }
        static void Tick()
        {
            if(SessionState.GetBool(Key,false))
            {
                if(!EditorApplication.isPlaying||began<=0)return;
                if(EditorApplication.timeSinceStartup-began<8)return;
                began=0;
                try { RunChecks();File.AppendAllText(Report,"COMPLETE assertions passed; runtime/editor errors observed: "+errors+"\n"); }
                catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");}
                finally{Time.timeScale=1;EditorApplication.ExitPlaymode();}
                return;
            }
            var path=ProjectPath.Report("BodyFeedback.request");
            if(!File.Exists(path)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(File.ReadAllText(path).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString())return;
            File.Delete(path);Run();
        }
        public static void Run()
        {
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>s.isLoaded&&(string.IsNullOrEmpty(s.path)||EditorSceneManager.GetSceneByPath(s.path).isDirty)))throw new InvalidOperationException("Unsaved scenes left untouched");
            if(!ValidationRunGate.TryAcquire(Key))throw new InvalidOperationException("An existing check owns this editor");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Report,"Production Game.unity and HUD; controlled health/stamina/death states. Mono camera, no Quest hardware evidence.\n");
            SessionState.SetString(Key+"Saved",JsonUtility.ToJson(new Saved{scenes=scenes}));SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){began=EditorApplication.timeSinceStartup;errors=0;Application.logMessageReceived+=OnLog;}
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            Application.logMessageReceived-=OnLog;
            var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Saved",""));
            SessionState.SetBool(Key,false);EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);ValidationRunGate.Release(Key);
            File.AppendAllText(Report,"CLEANUP restored Edit Mode scenes\n");
        }
        static void OnLog(string message,string stack,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            errors++;File.AppendAllText(Report,"OBSERVED "+message+"\n"+stack+"\n");
        }
        static void Check(bool value,string message)
        {
            File.AppendAllText(Report,(value?"PASS ":"FAIL ")+message+"\n");
            if(!value)throw new InvalidOperationException(message);
        }
        static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Flags).GetValue(obj);
        static void Property(object obj,string name,object value)=>obj.GetType().GetProperty(name,Flags).SetValue(obj,value);
        static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Flags).Invoke(obj,args);
        static void RunChecks()
        {
            var session=Object.FindFirstObjectByType<CastlePlaySession>();
            Check(session&&session.Player!=null,"Actual Game scene creates its local player");
            var motor=session.Player.gameObject.GetComponent<CastleSurvivor>();
            var hud=motor.GetComponent<CandyStaminaHud>();
            var match=MatchRuntime.Current;
            Check(hud&&match?.Frame!=null,"Production session attaches shared body HUD and match runtime");
            session.SetMenu(false);session.enabled=false;((Behaviour)match).enabled=false;
            foreach(var transit in Object.FindObjectsByType<CandySessionTravel>(FindObjectsSortMode.None)) Object.DestroyImmediate(transit.gameObject);
            Time.timeScale=0;motor.externallyDriven=false;motor.InputSuspended=false;motor.SendMessage("OnApplicationFocus",true);
            var camera=motor.viewCamera;camera.enabled=true;
            var world=Services.World;
            if(world!=null)
            {
                var center=world.Center;float radius=Mathf.Max(8,world.Radius);
                camera.transform.position=center+new Vector3(radius*.9f,world.TopHeight-center.y+5,-radius*1.2f);
                camera.transform.LookAt(center+Vector3.up*(world.TopHeight-center.y)*.5f);
            }
            var own=match.Frame.players.First(p=>p.id==match.LocalActorId);
            match.Frame.phase=RoundPhase.Playing;match.Rules.healthEnabled=true;
            own.alive=true;own.dying=false;own.role="survivor";own.spectating=null;own.health=1;own.sinking=false;
            Property(motor,"GripRemaining",1f);Property(motor,"SprintRequested",false);Property(motor,"Sprinting",false);
            hud.Present(motor,2);
            Check(Field<Canvas>(hud,"canvas")==null||!Field<Canvas>(hud,"canvas").enabled,"Rested, unhurt player has no overlay draw");
            var baseline=Capture(camera,null,"01-clear.png");
            Property(motor,"GripRemaining",0f);hud.Present(motor,2);
            var canvas=Field<Canvas>(hud,"canvas");var material=Field<Material>(hud,"material");
            Check(canvas&&material&&material.shader.isSupported,"Body feedback shader loads and compiles");
            Check(canvas.GetComponentsInChildren<Image>().Length==1&&canvas.GetComponentsInChildren<Text>().Length==0,"One non-interactive Image, no health/stamina bars or text");
            Check(!canvas.GetComponentInChildren<Image>().raycastTarget,"Overlay cannot consume terminal/menu pointer input");
            var mask=material.GetTexture("_Masks") as Texture2D;
            Check(mask&&mask.width==512&&mask.format==TextureFormat.RGBA32,"Packed feedback masks retain uncompressed precision");
            var exhausted=Capture(camera,canvas,"02-exhausted.png");
            Check(CenterDelta(baseline,exhausted)<.015f,"Fully exhausted center remains clear in actual shader pixels");
            Object.Destroy(exhausted);
            Property(motor,"GripRemaining",1f);hud.Present(motor,.1f);Check(hud.Opacity>.5f,"Recovery unwinds fatigue instead of popping off");
            hud.Present(motor,2);Check(!canvas.enabled,"Full stamina finishes recovery and removes overlay");
            Property(motor,"SprintRequested",true);hud.Present(motor,2);
            Check(Field<Vector4>(hud,"state").z==0,"Sprint armed while stationary does not show speed streaks");
            Property(motor,"Sprinting",true);hud.Present(motor,2);
            Check(Field<Vector4>(hud,"state").z==1,"Active sprint shows separate peripheral streaks");
            var sprint=Capture(camera,canvas,"03-sprinting.png");Object.Destroy(sprint);
            Property(motor,"SprintRequested",false);Property(motor,"Sprinting",false);own.health=.22f;hud.Present(motor,2);
            var damage=Capture(camera,canvas,"04-injured.png");
            Check(CenterDelta(baseline,damage)<.015f,"Low-health ink preserves the aim and navigation center");Object.Destroy(damage);
            var health=motor.GetComponent<CandyHealthFeedback>();hud.Present(motor,10);
            Check(Mathf.Abs(health.Damage-.78f)<.001f,"Ink remains after ten seconds without healing");
            Check(material.GetColor("_Ink")==own.color,"Ink uses the local participant's color");
            own.health=.70f;hud.Present(motor,.1f);Check(health.Damage>.3f&&health.Damage<.78f,"Healing smoothly clears accumulated ink");
            hud.Present(motor,2);own.health=1;hud.Present(motor,2);Check(health.Damage==0,"Full healing clears all damage paint");
            own.sinking=true;own.sinkProgress=.9f;hud.Present(motor,2);
            var sand=Capture(camera,canvas,"05-sinking.png");
            Check(CenterDelta(baseline,sand)<.015f,"Sand warning leaves the center navigable before death");Object.Destroy(sand);
            session.SetMenu(true);hud.Present(motor,2);Check(!canvas.enabled,"Menus immediately suppress effects and restore readability");
            session.SetMenu(false);own.sinking=false;motor.InputSuspended=false;hud.Present(motor,2);
            Check(Field<Vector4>(hud,"state").w==0,"Escaping sand removes its overlay");
            own.alive=false;own.dying=true;hud.Present(motor,2);Check(!canvas.enabled,"Death suppresses living-player feedback");
            GameEvents.RaiseMatchSound(MatchSoundCue.DrowningDeath,motor.transform.position);
            var death=Object.FindFirstObjectByType<CandyDeathOverlay>();
            Check(death&&Field<int>(death,"environment")==2,"Victim drowning cue selects water death, not paint");
            typeof(CandyDeathOverlay).GetField("began",Flags).SetValue(death,Time.unscaledTime-.5f);Call(death,"LateUpdate");
            var drowned=Capture(camera,Field<Canvas>(death,"canvas"),"06-drowned.png");Object.Destroy(drowned);
            CandyDeathOverlay.Show(own.color);Check(Object.FindObjectsByType<CandyDeathOverlay>(FindObjectsSortMode.None).Length==1&&Field<int>(death,"environment")==2,"Snapshot death notification cannot duplicate or replace water with ink");
            Object.DestroyImmediate(death.gameObject);
            GameEvents.RaiseMatchSound(MatchSoundCue.IslandDeath,motor.transform.position);
            death=Object.FindFirstObjectByType<CandyDeathOverlay>();Check(death&&Field<int>(death,"environment")==1,"Victim sand cue selects burial death");
            typeof(CandyDeathOverlay).GetField("began",Flags).SetValue(death,Time.unscaledTime-.5f);Call(death,"LateUpdate");
            var buried=Capture(camera,Field<Canvas>(death,"canvas"),"07-buried.png");Object.Destroy(buried);Object.DestroyImmediate(death.gameObject);
            CandyDeathOverlay.ShowLocalDeath(own.color);death=Object.FindFirstObjectByType<CandyDeathOverlay>();
            Check(death&&Field<int>(death,"environment")==0&&Field<Material>(death,"material").shader.name.Contains("Death"),"Ordinary death retains original player-color splatter shader");
            typeof(CandyDeathOverlay).GetField("began",Flags).SetValue(death,Time.unscaledTime-.5f);Call(death,"LateUpdate");
            var dead=Capture(camera,Field<Canvas>(death,"canvas"),"08-death-paint.png");Object.Destroy(dead);
            match.Frame.phase=RoundPhase.Results;Call(death,"LateUpdate");Check(Field<float>(death,"wipeAt")>=0,"Entering results starts death-overlay cleanup");Object.DestroyImmediate(death.gameObject);
            own.alive=true;own.dying=false;match.Frame.phase=RoundPhase.Playing;
            match.Rules.healthEnabled=false;own.health=.2f;hud.Present(motor,2);
            Check(health.Damage==0,"Health-disabled rules cannot leave damage ink behind");
            own.health=1;match.Rules.healthEnabled=true;
            GameEvents.RaiseMatchSound(MatchSoundCue.NearbyDeath,motor.transform.position);
            Check(!Object.FindFirstObjectByType<CandyDeathOverlay>(),"Bystander death sounds cannot start local death visuals");
            Property(motor,"GripRemaining",.3f);hud.Present(motor,2);
            long start=GC.GetAllocatedBytesForCurrentThread();for(int n=0;n<120;n++)hud.Present(motor,.016f);long allocated=GC.GetAllocatedBytesForCurrentThread()-start;
            File.AppendAllText(Report,"120 steady-state Present calls allocated "+allocated+" managed bytes (Editor)\n");
            var fit=typeof(CandyDeathOverlay).Assembly.GetType("DeadlyJenga.Presentation.CandyScreenPlane").GetMethod("Configure",Flags);
            foreach(float fov in new[]{65f,105f})foreach(float near in new[]{.02f,.25f})
            {
                camera.fieldOfView=fov;camera.nearClipPlane=near;fit.Invoke(null,new object[]{canvas,camera,true});
                var corners=new Vector3[4];((RectTransform)canvas.transform).GetWorldCorners(corners);
                var a=camera.WorldToViewportPoint(corners[0]);var b=camera.WorldToViewportPoint(corners[2]);
                Check(a.x<0&&a.y<0&&b.x>1&&b.y>1,"Mono VR plane covers view at FOV "+fov+", near "+near);
            }
            Object.Destroy(baseline);
            Check(!ShaderUtil.GetShaderMessages(material.shader).Any(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),"No compiled shader errors");
        }
        static Texture2D Capture(Camera camera,Canvas overlay,string name)
        {
            if(overlay){overlay.renderMode=RenderMode.ScreenSpaceCamera;overlay.worldCamera=camera;overlay.planeDistance=camera.nearClipPlane+.1f;Canvas.ForceUpdateCanvases();}
            var previous=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1280,800,24);var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,name),image.EncodeToPNG());return image;}
            finally{camera.targetTexture=previous;RenderTexture.active=active;target.Release();Object.Destroy(target);}
        }
        static float CenterDelta(Texture2D a,Texture2D b)
        {
            float diff=0;
            for(int y=320;y<480;y+=7)for(int x=512;x<768;x+=7)
            {var d=a.GetPixel(x,y)-b.GetPixel(x,y);diff=Mathf.Max(diff,Mathf.Abs(d.r),Mathf.Abs(d.g),Mathf.Abs(d.b));}
            return diff;
        }
    }
}
