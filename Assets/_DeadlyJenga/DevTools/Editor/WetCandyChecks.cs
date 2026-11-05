using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class WetCandyChecks
    {
        const string Key="WetCandy.Checks";
        static IEnumerator run;
        static int errors,lastRunFrame=-1;
        static string Report=>ProjectPath.Report("WetCandyChecks.txt");
        [Serializable] sealed class Saved
        {
            public SceneSetup[] scenes;public string map,mode;public int bots,hands;
            public bool fromLobby,local,background;
        }
        static WetCandyChecks(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        static bool Own(string path)=>File.Exists(path)&&File.ReadAllText(path).Trim()==System.Diagnostics.Process.GetCurrentProcess().Id.ToString();
        static void Tick()
        {
            if(run==null)
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isUpdating||EditorApplication.isCompiling)return;
                string request=ProjectPath.Report("WetCandyChecks.request");
                if(!Own(request))return;
                if(Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(s=>s.isLoaded&&s.isDirty))return;
                if(!ValidationRunGate.TryAcquire(Key))return;
                File.Delete(request);
                File.WriteAllText(Report,"START "+DateTime.Now.ToString("O")+"\n");
                SessionState.SetString(Key,JsonUtility.ToJson(new Saved
                {
                    scenes=EditorSceneManager.GetSceneManagerSetup(),map=MatchSetup.MapId,mode=MatchSetup.ModeId,
                    bots=MatchSetup.BotTarget,hands=MatchSetup.HandCount,fromLobby=MatchSetup.FromLobby,
                    local=MatchSetup.StartLocalSession,background=Application.runInBackground
                }));
                MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=1;MatchSetup.HandCount=0;
                MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;SessionState.SetBool(Key+"Start",true);
                EditorSceneManager.OpenScene(ProjectPath.Find("MapDrafterEvan1.unity"));
                EditorApplication.EnterPlaymode();return;
            }
            if(EditorApplication.isPlaying)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                if(Time.frameCount==lastRunFrame)return;
                lastRunFrame=Time.frameCount;
            }
            try
            {
                if(run.Current is CustomYieldInstruction wait&&wait.keepWaiting)return;
                if(run.MoveNext())return;
            }
            catch(Exception ex){errors++;File.AppendAllText(Report,"FAIL "+ex+"\n");}
            run=null;Application.logMessageReceived-=Log;
            File.AppendAllText(Report,"COMPLETE errors="+errors+"; actual desktop Editor, same-process scene. Dry-out clock accelerated for the test; Quest pending.\n");
            EditorApplication.ExitPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key+"Start",false)&&ValidationRunGate.Owns(Key))
            {
                SessionState.SetBool(Key+"Start",false);SessionState.SetBool(Key+"Running",true);
                errors=0;lastRunFrame=-1;Application.runInBackground=true;EditorApplication.LockReloadAssemblies();
                AssetDatabase.DisallowAutoRefresh();Application.logMessageReceived+=Log;run=Validate();
            }
            if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key+"Running",false))return;
            SessionState.SetBool(Key+"Running",false);run=null;Application.logMessageReceived-=Log;
            AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
            var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key,""));
            MatchSetup.MapId=saved.map;MatchSetup.ModeId=saved.mode;MatchSetup.BotTarget=saved.bots;MatchSetup.HandCount=saved.hands;
            MatchSetup.FromLobby=saved.fromLobby;MatchSetup.StartLocalSession=saved.local;Application.runInBackground=saved.background;
            try
            {
                if(saved.scenes.Any(s=>s.isLoaded&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
            finally{ValidationRunGate.Release(Key);}
        }
        static void Log(string message,string stack,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Assert&&type!=LogType.Exception)return;
            errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+stack+"\n");
        }
        static void Require(bool okay,string message)
        {
            if(!okay)throw new InvalidOperationException(message);
            File.AppendAllText(Report,"PASS "+message+"\n");
        }
        static IEnumerator Validate()
        {
            float timeout=Time.realtimeSinceStartup+90;
            while(!(Services.Session is CastlePlaySession)&&Time.realtimeSinceStartup<timeout)yield return null;
            var session=Services.Session as CastlePlaySession;
            Require(session&&Services.World!=null,"Real Game session loaded");
            session.SurvivorBots.Clear();session.HandBots.Clear();session.SetMenu(true);
            if(session.Social!=null)session.Social.VoiceEnabled=false;
            var effect=Object.FindFirstObjectByType<CandyWaterContactEffects>();
            Require(effect,"Actual water contact presentation installed");
            var shader=Resources.Load<Shader>("CandyScenery/CandyWetSurface");
            Require(shader&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),"Wet film shader supported and compiled");
            Require(SystemInfo.supportsInstancing,"Desktop test device supports coating instancing");
            yield return null;
            var blocks=Services.World.Blocks.Where(b=>b&&b.GetComponent<IClimbable>()?.Visual).Take(240).ToArray();
            Require(blocks.Length>=200,"At least 200 real blocks are available");
            var selected=blocks.Select(b=>new{body=b,visual=b.GetComponent<IClimbable>().Visual})
                .Select(b=>new{b.body,renderer=b.visual.GetComponentInChildren<MeshRenderer>()})
                .Where(b=>b.renderer&&b.renderer.GetComponent<MeshFilter>()?.sharedMesh.subMeshCount==1)
                .OrderByDescending(b=>b.renderer.forceRenderingOff).First();
            Require(selected.renderer.forceRenderingOff,"Selected block is already drawn through normal distant batching");
            var originalMaterial=selected.renderer.sharedMaterial;
            bool originalProperties=selected.renderer.HasPropertyBlock();
            var cameraGo=new GameObject("Wet coating inspection",typeof(Camera));var camera=cameraGo.GetComponent<Camera>();
            camera.CopyFrom(session.ActiveCamera);camera.enabled=false;
            var bounds=selected.renderer.bounds;
            camera.transform.position=bounds.center+new Vector3(2.8f,2.1f,3.1f)*Mathf.Max(1,bounds.extents.magnitude);
            camera.transform.LookAt(bounds.center);
            try
            {
                Capture(camera,"wet-candy-before.png");
                effect.Soak(selected.body,bounds.center,false);
                yield return null;
                Require(effect.WetRendererCount>0&&effect.BatchedRendererCount>0,"Real single-mesh blocks use the instanced film path");
                Require(selected.renderer.sharedMaterial==originalMaterial&&selected.renderer.HasPropertyBlock()==originalProperties,
                    "Original block material and property-block state preserved");
                Require(!selected.renderer.GetComponentsInChildren<Renderer>().Any(r=>r.name=="Visual - drying water stain"),
                    "Single-mesh coating creates no child renderer");
                Capture(camera,"wet-candy-coated.png");
                // Test the same source transform used by the existing distant block batches.
                var position=selected.body.position;bool kinematic=selected.body.isKinematic;
                try
                {
                    selected.body.isKinematic=true;selected.body.position+=Vector3.up*.7f;Physics.SyncTransforms();
                    yield return null;
                    Capture(camera,"wet-candy-moved.png");
                    Require(effect.BatchedRendererCount>0,"Moving block retains the live coating instance");
                }
                finally{selected.body.position=position;selected.body.isKinematic=kinematic;}

                for(int i=0;i<blocks.Length;i++)
                {
                    effect.Soak(blocks[i],blocks[i].worldCenterOfMass,false);
                    if(i%12==11)yield return null;
                }
                Require(effect.WetRendererCount<=192,"Wet renderer population stays within 192");
                Require(effect.BatchedRendererCount>0,"Normal block material path remains instanced at population scale");
                var field=typeof(CandyWaterContactEffects).GetField("wet",BindingFlags.Instance|BindingFlags.NonPublic);
                var entries=(IDictionary)field.GetValue(effect);
                foreach(var value in entries.Values)value.GetType().GetField("until").SetValue(value,Time.time+4);
                yield return null;
                Capture(camera,"wet-candy-drying.png");
                foreach(var value in entries.Values)value.GetType().GetField("until").SetValue(value,Time.time-.1f);
                yield return null;yield return null;
                Require(effect.WetRendererCount==0&&effect.BatchedRendererCount==0,"Dry-out releases every coating instance");
                Require(selected.renderer.sharedMaterial==originalMaterial,"Dry-out leaves the original material in place");
                Capture(camera,"wet-candy-dry.png");
            }
            finally{Object.Destroy(cameraGo);}
        }
        static void Capture(Camera camera,string file)
        {
            var target=RenderTexture.GetTemporary(1280,800,24);
            var previous=RenderTexture.active;var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();
                File.WriteAllBytes(ProjectPath.Report(file),image.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.Destroy(image);}
        }
    }
}
