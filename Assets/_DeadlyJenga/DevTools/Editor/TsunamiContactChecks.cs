using System;
using System.Collections.Generic;
using DeadlyJenga.CandyArt;
using DeadlyJenga.CandyAudio;
using DeadlyJenga.World.Hazards;
using Unity.Profiling;
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
    [InitializeOnLoad] public static class TsunamiContactChecks
    {
        const string Key="Tsunami.ContactChecks";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        static IEnumerator run; static int errors,lastRunFrame=-1;
        static string Report=>ProjectPath.Report("TsunamiContactChecks.txt");
        [Serializable] sealed class Saved {public SceneSetup[] scenes;public string map,mode;public int bots,hands;public bool fromLobby,local,background,defaultScene;public float volume;}
        static TsunamiContactChecks(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        static void Tick()
        {
            if(run==null)
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isUpdating||EditorApplication.isCompiling)return;
                string close=ProjectPath.Report("TsunamiContactChecks.close");
                if(Own(close)){File.Delete(close);EditorApplication.Exit(0);return;}
                string request=ProjectPath.Report("TsunamiContactChecks.request");
                if(!Own(request))return;
                var scenes=EditorSceneManager.GetSceneManagerSetup();
                if(Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(s=>s.isLoaded&&s.isDirty)){File.WriteAllText(Report,"Deferred: unsaved scene left untouched.");return;}
                if(!ValidationRunGate.TryAcquire(Key))return;
                File.Delete(request);File.WriteAllText(Report,"Tsunami contact checks started "+DateTime.Now.ToString("O")+"\n");
                SessionState.SetString(Key+"Saved",JsonUtility.ToJson(new Saved{scenes=scenes,defaultScene=SceneManager.GetActiveScene().rootCount>0,map=MatchSetup.MapId,mode=MatchSetup.ModeId,bots=MatchSetup.BotTarget,hands=MatchSetup.HandCount,fromLobby=MatchSetup.FromLobby,local=MatchSetup.StartLocalSession,background=Application.runInBackground,volume=AudioListener.volume}));
                MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=1;MatchSetup.HandCount=0;MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;
                SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(ProjectPath.Find("MapDrafterEvan1.unity"));EditorApplication.EnterPlaymode();return;
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
            catch(Exception e){errors++;File.AppendAllText(Report,"FAIL "+e+"\n");}
            run=null;Application.logMessageReceived-=Log;
            File.AppendAllText(Report,"COMPLETE; errors="+errors+". Actual desktop Editor; controlled contact/eye fixtures followed by a real dense-map wave. No Quest verification.\n");
            EditorApplication.ExitPlaymode();
        }
        static bool Own(string file)=>File.Exists(file)&&File.ReadAllText(file).Trim()==System.Diagnostics.Process.GetCurrentProcess().Id.ToString();
        static void State(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)&&ValidationRunGate.Owns(Key))
            {
                SessionState.SetBool(Key,false);SessionState.SetBool(Key+"Running",true);errors=0;lastRunFrame=-1;
                EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();Application.logMessageReceived+=Log;run=Validate();
            }
            if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key+"Running",false))return;
            SessionState.SetBool(Key+"Running",false);Application.logMessageReceived-=Log;run=null;
            AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
            var s=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Saved",""));
            MatchSetup.MapId=s.map;MatchSetup.ModeId=s.mode;MatchSetup.BotTarget=s.bots;MatchSetup.HandCount=s.hands;MatchSetup.FromLobby=s.fromLobby;MatchSetup.StartLocalSession=s.local;Application.runInBackground=s.background;AudioListener.volume=s.volume;
            try{if(s.scenes!=null&&s.scenes.Any(x=>x.isLoaded&&!string.IsNullOrEmpty(x.path)))EditorSceneManager.RestoreSceneManagerSetup(s.scenes);else EditorSceneManager.NewScene(s.defaultScene?NewSceneSetup.DefaultGameObjects:NewSceneSetup.EmptyScene,NewSceneMode.Single);}
            finally{ValidationRunGate.Release(Key);}
        }
        static void Log(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+stack+"\n");}}
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);File.AppendAllText(Report,"PASS "+message+"\n");}

        sealed class FixtureWave:IWaveState { public WaveFrame Wave{get;set;} }
        sealed class FixtureWorld:IWorldQuery
        {
            public Bounds PlayableBounds{get;set;}
            public Vector3 Center=>PlayableBounds.center;
            public float Radius=>25;
            public float TopHeight=>PlayableBounds.max.y;
            public float StepHeight=>1;
            public IReadOnlyList<Rigidbody> Blocks=>Array.Empty<Rigidbody>();
            public float WaterHeight(Vector3 point)=>Center.y-8;
            public bool IsSheltered(Vector3 point)=>false;
            public Vector3 SafeSpawnPoint()=>Center;
        }
        static GameObject Obstacle(string name,Vector3 position,Vector3 size)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name=name;go.layer=Layers.Block;go.transform.position=position;go.transform.localScale=size;return go;
        }
        static IEnumerator Validate()
        {
            Application.runInBackground=true;
            float timeout=Time.realtimeSinceStartup+90;
            while(!(Services.Session is CastlePlaySession)&&Time.realtimeSinceStartup<timeout)yield return null;
            var session=Services.Session as CastlePlaySession;
            Require(session&&Services.World!=null,"Actual dense-map Game session loaded");
            session.SurvivorBots.Clear();session.HandBots.Clear();
            if(session.Social!=null)session.Social.VoiceEnabled=false;
            session.SetMenu(true);
            var active=session.ActiveCamera;
            Require(active,"Actual player camera available");
            var cameraObject=new GameObject("Tsunami inspection camera",typeof(Camera));
            var camera=cameraObject.GetComponent<Camera>();camera.CopyFrom(active);camera.enabled=false;
            var effect=Object.FindFirstObjectByType<CandyWaveContactVisual>();
            Require(effect,"Contact presentation installed on active environment");
            var shader=Shader.Find("Candy Tropical/Contact Tsunami");
            Require(shader&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),"Contact wave shader supported and compiled");
            var old=Services.World;var initial=active.transform.position;var initialRot=active.transform.rotation;
            var motor=session.Survivor();bool motorEnabled=motor.enabled;
            var feet=motor.transform.position;var facing=motor.transform.rotation;var eyeOffset=initial-feet;
            motor.enabled=false;
            void PoseEye(Vector3 position,Vector3 focus)
            {
                motor.Teleport(position-eyeOffset,Quaternion.identity);
                active.transform.position=position;active.transform.LookAt(focus);
            }
            var at=new Vector3(500,old.WaterHeight(old.Center),500);
            var fixture=new FixtureWorld{PlayableBounds=new Bounds(at+Vector3.up*8,new Vector3(36,16,25))};
            var wave=new FixtureWave();
            var frame=new WaveFrame{active=true,center=at,direction=Vector3.forward,width=44,depth=28,height=14,waterHeight=at.y,speed=8};
            var solid=Obstacle("Tall face",at+new Vector3(-9,7,-1),new Vector3(8,14,4));
            var low=Obstacle("Low slab",at+new Vector3(9,1,-9),new Vector3(8,2,3));
            var supports=new[]{
                Obstacle("Left edge of open bay",at+new Vector3(-3.7f,5,-2),new Vector3(1.7f,10,3)),
                Obstacle("Right edge of open bay",at+new Vector3(3.7f,5,-2),new Vector3(1.7f,10,3))};
            PoseEye(at+new Vector3(20,12,26),at+Vector3.up*5);
            Services.World=fixture;wave.Wave=frame;Tracked.Waves.Add(wave);Physics.SyncTransforms();
            try
            {
                timeout=Time.realtimeSinceStartup+90;
                int initialFrame=Time.frameCount;
                int observed=0;
                while(Time.realtimeSinceStartup<timeout)
                {
                    observed=Mathf.Max(observed,effect.LastProbeCount);
                    if(effect.HasContactAt(solid.transform.position+Vector3.up*3)&&effect.HasContactAt(low.transform.position))break;
                    yield return null;
                }
                File.AppendAllText(Report,"DIAGNOSTIC fixture player frames="+(Time.frameCount-initialFrame)+
                    " observed probes="+observed+" component enabled="+effect.isActiveAndEnabled+
                    " tracked waves="+Tracked.Waves.Count+"\n");
                Require(Time.frameCount>initialFrame&&observed>0,"Contact fixture reaches an advancing player loop and sampler");
                Require(observed<=24,"Probes are capped at 24 per update");
                Require(effect.HasContactAt(solid.transform.position+Vector3.up*3),"Tall face interrupts upper water");
                Require(effect.HasContactAt(low.transform.position),"Low obstruction registers below its top");
                Require(!effect.HasContactAt(low.transform.position+Vector3.up*4),"Water remains free above the low slab");
                Require(!effect.HasContactAt(at+Vector3.up*5),"Open bay stays open between pillars");
                camera.transform.position=at+new Vector3(16,11,23);camera.transform.LookAt(at+Vector3.up*5);
                Capture(camera,"wave-contact-wall-gap.png");
                effect.EnabledEffects=false;yield return null;Capture(camera,"wave-contact-off.png");
                effect.EnabledEffects=true;
                Require(CandyWaveContactVisual.ContainsEye(frame,at+new Vector3(0,4,-10)),"Eye within the wave body is wet");
                Require(!CandyWaveContactVisual.ContainsEye(frame,at+new Vector3(0,3,8)),"Air ahead and below the curl is dry");
                Require(!CandyWaveContactVisual.ContainsEye(frame,at+new Vector3(0,23,0)),"Above the crest is dry");
                var immersionShader=Resources.Load<Shader>("CandyScenery/CandyWaveImmersion");
                Require(immersionShader&&immersionShader.isSupported&&!ShaderUtil.ShaderHasError(immersionShader),"Underwater composite shader compiled");
                var cameraData=active.GetUniversalAdditionalCameraData();
                var depthOption=cameraData.requiresDepthOption;
                var wetEye=at+new Vector3(-7,4,-10);
                var lookAt=at+new Vector3(-9,5,4);
                try
                {
                    effect.EnabledEffects=false;
                    PoseEye(wetEye,lookAt);
                    yield return null;
                    float dryReference=Capture(active,"wave-interior-reference.png");
                    effect.EnabledEffects=true;
                    yield return null;
                    Capture(active,"wave-interior-entry.png");
                    timeout=Time.realtimeSinceStartup+30;
                    while(effect.Immersion<.99f&&Time.realtimeSinceStartup<timeout)yield return null;
                    Require(effect.Immersion>=.99f&&CandyAudioEngine.WaveImmersion>=.99f,"Actual camera enters and applies audio immersion");
                    yield return new WaitForSecondsRealtime(.55f);
                    float first=Capture(active,"wave-interior-full.png");
                    Require(first<.48f&&first<dryReference*.87f,"Full submersion darkens the same camera pose: "+dryReference.ToString("F3")+" to "+first.ToString("F3"));
                    Require(Playing("Wave pressure and trapped breath"),"Submerged breathing and current loop plays through pooled audio");
                    var audioProbe=new GameObject("Underwater world-audio probe");
                    audioProbe.transform.position=wetEye+Vector3.forward;
                    try
                    {
                        var engine=CandyAudioEngine.Instance;
                        bool FilteredWorldSound()=>engine.GetComponentsInChildren<AudioSource>().Any(source=>
                            source.isPlaying&&source.spatialBlend>.5f&&
                            source.TryGetComponent<AudioLowPassFilter>(out var filter)&&filter.enabled&&filter.cutoffFrequency<1000);
                        timeout=Time.realtimeSinceStartup+15;
                        while(!FilteredWorldSound()&&Time.realtimeSinceStartup<timeout)
                        {
                            engine.Loop(CandySound.WaveSpray,audioProbe.transform,.25f);
                            yield return null;
                        }
                        Require(FilteredWorldSound(),"Real pooled spatial world sound reaches submerged low-pass cutoff");
                    }
                    finally{Object.Destroy(audioProbe);}

                    Capture(camera,"wave-interior-exterior-during.png");
                    Require(Shader.GetGlobalFloat("_DJWaveInside")==0,"Exterior camera does not inherit underwater wave attenuation");

                    yield return new WaitForSecondsRealtime(.17f);
                    Capture(active,"wave-interior-current.png");
                    cameraData.requiresDepthOption=CameraOverrideOption.Off;
                    float noDepth=Capture(active,"wave-interior-no-depth.png");
                    Require(noDepth<.52f,"Camera without a depth copy still renders the dark underwater effect");
                    cameraData.requiresDepthOption=depthOption;

                    PoseEye(at+new Vector3(0,23,0),at+Vector3.up*5);
                    yield return null;
                    Capture(active,"wave-interior-surface.png");
                    Require(Playing("Wave surface gasp"),"Surfacing after immersion plays the recovery breath");
                    timeout=Time.realtimeSinceStartup+30;
                    while((effect.Immersion>.01f||Playing("Wave pressure and trapped breath"))&&Time.realtimeSinceStartup<timeout)yield return null;
                    Require(effect.Immersion<=.01f&&CandyAudioEngine.WaveImmersion<=.01f,"Surfacing clears visual and audio immersion");
                    Require(!Playing("Wave pressure and trapped breath"),"Surfacing releases the submerged loop");
                    Capture(active,"wave-interior-exit.png");
                }
                finally{cameraData.requiresDepthOption=depthOption;}
                solid.SetActive(false);low.SetActive(false);foreach(var p in supports)p.SetActive(false);Physics.SyncTransforms();
                timeout=Time.realtimeSinceStartup+30;
                while(effect.ContactCount>0&&Time.realtimeSinceStartup<timeout)yield return null;
                Require(effect.ContactCount==0&&Shader.GetGlobalFloat("_DJWaveContactActive")==0,"Removed obstacles leave no ghost contact");

            }
            finally
            {
                Tracked.Waves.Remove(wave);Services.World=old;
                Object.Destroy(solid);Object.Destroy(low);foreach(var p in supports)Object.Destroy(p);
                motor.Teleport(feet,facing);active.transform.SetPositionAndRotation(initial,initialRot);motor.enabled=motorEnabled;
            }
            yield return null;
            Require(Services.World.Blocks.Count>=1000,"Real dense map loaded: "+Services.World.Blocks.Count+" blocks");
            for(int i=0;i<32;i++)session.SurvivorBots.SpawnOne();
            Require(session.SurvivorBots.Count>0,"Real survivor bots spawned: "+session.SurvivorBots.Count);
            var hazard=Object.Instantiate(Resources.Load<GameObject>("DemoDisasters/Tsunami")).GetComponent<TsunamiHazard>();
            hazard.randomDirection=false;hazard.approachHeading=0;hazard.warningTime=0;hazard.offshoreDistance=0;hazard.riseTime=.1f;hazard.Begin();
            var bounds=Services.World.PlayableBounds;
            camera.transform.position=bounds.center+new Vector3(bounds.size.x*.65f,bounds.size.y*.66f,-bounds.size.z*.7f);camera.transform.LookAt(bounds.center);
            try
            {
                int maxContacts=0,maxProbes=0,maxImpacts=0;
                using var recorder=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"DJ.WaveContacts",64);
                for(int i=0;i<24;i++)
                {
                    yield return null;
                    maxContacts=Mathf.Max(maxContacts,effect.ContactCount);maxProbes=Mathf.Max(maxProbes,effect.LastProbeCount);
                    maxImpacts=Mathf.Max(maxImpacts,hazard.LastTargetCount);
                    if(i==13)
                    {
                        var waveRoot=GameObject.Find("Visual - Tsunami");
                        var waveFilter=waveRoot?waveRoot.GetComponent<MeshFilter>():null;
                        var oldMesh=waveFilter?waveFilter.sharedMesh:null;
                        Mesh staged=null;
                        if(oldMesh&&oldMesh.name=="Tsunami static GPU surface")
                        {
                            staged=CaptureMesh();staged.bounds=oldMesh.bounds;waveFilter.sharedMesh=staged;
                        }
                        try{Capture(camera,"wave-dense-bots.png");}
                        finally{if(staged){waveFilter.sharedMesh=oldMesh;Object.Destroy(staged);}}
                    }
                }
                Require(hazard.Active&&maxImpacts>0,"Actual hazard applies contacts in dense structure: peak "+maxImpacts);
                Require(maxProbes<=24,"Dense map respects the same probe budget");
                Require(maxContacts>0,"Dense structure produces presentation contacts: peak "+maxContacts);
                var samples=new List<ProfilerRecorderSample>();if(recorder.Valid)recorder.CopyTo(samples);
                if(samples.Count>0)File.AppendAllText(Report,"MEASURE DJ.WaveContacts editor CPU ns per rendered frame: count="+samples.Count+" mean="+samples.Average(x=>(double)x.Value)+" max="+samples.Max(x=>x.Value)+"\n");
            }
            finally{hazard.End();Object.Destroy(hazard.gameObject);Object.Destroy(cameraObject);}
            yield return null;yield return null;
            Require(effect.Immersion==0&&effect.ContactCount==0&&CandyAudioEngine.WaveImmersion==0,"Wave stop clears contact, overlay and audio state");
        }
        static Mesh CaptureMesh()
        {
            var vertices=new List<Vector3>();var indices=new List<int>();
            foreach(int kind in new[]{0,2,1})
            {
                const int nx=96,ny=80;int start=vertices.Count;
                for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++)
                {
                    vertices.Add(new Vector3((x/(float)nx-.5f)*(kind==1?1.5f:1.3f),kind,y/(float)ny-.5f));
                    if(y==ny||x==nx)continue;
                    int a=start+y*(nx+1)+x;indices.AddRange(new[]{a,a+nx+1,a+1,a+1,a+nx+1,a+nx+2});
                }
            }
            var mesh=new Mesh{name="Staged canonical surface layout"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);return mesh;
        }
        static bool Playing(string clip)=>Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)
            .Any(source=>source.isPlaying&&source.clip&&source.clip.name==clip);
        static float Capture(Camera camera,string name)
        {
            var rt=RenderTexture.GetTemporary(1500,900,24);var old=camera.targetTexture;var previous=RenderTexture.active;
            var image=new Texture2D(1500,900,TextureFormat.RGB24,false);
            try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1500,900),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report(name),image.EncodeToPNG());
                var pixels=image.GetPixels32();double sum=0;
                for(int i=0;i<pixels.Length;i+=13)sum+=.2126*pixels[i].r+.7152*pixels[i].g+.0722*pixels[i].b;
                return (float)(sum/((pixels.Length+12)/13*255));}
            finally{camera.targetTexture=old;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);}
        }
    }
}
