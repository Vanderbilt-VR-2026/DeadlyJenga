using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyArt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Explicit, isolated render of the production disaster shaders and visual builders.
    [InitializeOnLoad]
    public static class FogWaterDisasterPreview
    {
        const string Key="FogWaterDisasterPreview";
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        sealed class FrozenWave:IWaveState {public WaveFrame frame;public WaveFrame Wave=>frame;}
        static double runtimeBegan,readyAt;
        static int runtimeErrors,motionStage,motionFrames;
        static double motionBegan,nextMotionShot;
        static Camera motionCamera;
        static FogWaterDisasterPreview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        public static void ExportTsunamiAudio()
        {
            try
            {
                string output=Path.Combine(Path.GetDirectoryName(Application.dataPath),"ArtDirection","TsunamiSounds-20261027");
                Directory.CreateDirectory(output);
                var bank=Resources.Load<DeadlyJenga.CandyAudio.CandyAudioBank>("CandyAudio/Default");
                var report=new StringBuilder("sound\tseconds\tsamples\tchannels\tfrequency\tloop\tsource\n");
                foreach(var sound in new[]{DeadlyJenga.CandyAudio.CandySound.DisasterHorn,DeadlyJenga.CandyAudio.CandySound.DisasterTension,DeadlyJenga.CandyAudio.CandySound.WaveImpact,DeadlyJenga.CandyAudio.CandySound.WaveSpray,DeadlyJenga.CandyAudio.CandySound.PuddleDrain})
                {
                    var authored=bank?Array.Find(bank.cues,c=>c!=null&&c.sound==sound):null;
                    var cue=authored??DeadlyJenga.CandyAudio.CandyDisasterSounds.DefaultCue(sound);
                    if(cue.variants.Length==0||!cue.variants[0])throw new Exception("Missing sound: "+sound);
                    var clip=cue.variants[0];var pcm=new float[clip.samples*clip.channels];
                    if(!clip.GetData(pcm,0))throw new Exception("Could not read PCM: "+sound);
                    using(var writer=new BinaryWriter(File.Create(Path.Combine(output,sound+".wav"))))
                    {
                        writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+pcm.Length*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                        writer.Write(16);writer.Write((short)1);writer.Write((short)clip.channels);writer.Write(clip.frequency);writer.Write(clip.frequency*clip.channels*2);writer.Write((short)(clip.channels*2));writer.Write((short)16);
                        writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(pcm.Length*2);
                        foreach(float sample in pcm)writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample,-1,1)*32767));
                    }
                    report.AppendLine(sound+"\t"+clip.length.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"\t"+clip.samples+"\t"+clip.channels+"\t"+clip.frequency+"\t"+cue.loop+"\t"+(authored!=null?"authored bank":"current synthesized fallback"));
                }
                File.WriteAllText(Path.Combine(output,"Export.tsv"),report.ToString());
                File.WriteAllText(ProjectPath.Report("TsunamiAudioExport.txt"),"PASS: exported five current Unity AudioClip PCM buffers to WAV. Raw source clips; no spatial/mix effects.\n"+report);
                EditorApplication.Exit(0);
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
        public static void RunBatch()
        {
            try
            {
                Capture();
                SessionState.SetBool(Key+"Batch",true);
                File.WriteAllText(ProjectPath.Report(Key+"-Runtime.txt"),"Starting actual Game solo smoke check.\n");
                MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=1;MatchSetup.HandCount=0;
                EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
            }
            catch(Exception error){File.WriteAllText(ProjectPath.Report(Key+"-error.txt"),error.ToString());EditorApplication.Exit(1);}
        }
        static void RuntimeLog(string message,string stack,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            runtimeErrors++;File.AppendAllText(ProjectPath.Report(Key+"-Runtime.txt"),type+": "+message+"\n"+stack+"\n");
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key+"Batch",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                runtimeBegan=EditorApplication.timeSinceStartup;readyAt=0;runtimeErrors=0;motionStage=0;motionFrames=0;motionBegan=0;motionCamera=null;Application.runInBackground=true;AudioListener.volume=0;Application.logMessageReceived+=RuntimeLog;
            }
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key+"Batch",false);Application.logMessageReceived-=RuntimeLog;EditorApplication.Exit(SessionState.GetBool(Key+"Passed",false)?0:1);
            }
        }
        static void Tick()
        {
            if(SessionState.GetBool(Key+"Batch",false)&&EditorApplication.isPlaying)
            {
                bool ready=Services.Session?.Player!=null&&Services.World?.Blocks.Count>0&&Object.FindFirstObjectByType<CandyDisasterVisuals>();
                if(ready&&readyAt==0)readyAt=EditorApplication.timeSinceStartup;
                if(readyAt>0&&EditorApplication.timeSinceStartup-readyAt>4&&motionStage<2)RuntimeMotion();
                if((readyAt>0&&motionStage>=2)||EditorApplication.timeSinceStartup-runtimeBegan>90)
                {
                    bool passed=readyAt>0&&motionStage>=2&&runtimeErrors==0;SessionState.SetBool(Key+"Passed",passed);
                    File.AppendAllText(ProjectPath.Report(Key+"-Runtime.txt"),(passed?"PASS":"FAIL")+": Game entered solo with player, world and production disaster visuals. runtimeErrors="+runtimeErrors+"\n");
                    EditorApplication.ExitPlaymode();
                }
                return;
            }
            string request=ProjectPath.Report(Key+".request");
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(File.ReadAllText(request).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString()||!ValidationRunGate.TryAcquire(Key))return;
            File.Delete(request);
            try{Capture();}catch(Exception e){File.WriteAllText(ProjectPath.Report(Key+"-error.txt"),e.ToString());}
            finally{ValidationRunGate.Release(Key);}
        }
        static void RuntimeMotion()
        {
            var demo=Services.Session?.Demo;if(demo==null)return;
            string label=motionStage==0?"tornado":"tsunami";
            if(motionBegan==0)
            {
                demo.Run(DemoCommand.StopDisasters);
                int id=-1;for(int i=0;i<demo.DisasterCount;i++)if(demo.DisasterLabel(i).IndexOf(label,StringComparison.OrdinalIgnoreCase)>=0){id=i;break;}
                if(id<0)throw new Exception("Runtime disaster missing: "+label);
                {
                    var parameters=demo.DisasterParameters(id);
                    for(int p=0;p<parameters.Count;p++)
                    {
                        if(motionStage==1&&parameters[p].id=="height")demo.SetDisasterValue(id,p,18);
                        if(parameters[p].id=="warning")demo.SetDisasterValue(id,p,1);
                        if(parameters[p].id=="force")demo.SetDisasterValue(id,p,0);
                    }
                }
                string result=demo.LaunchDisaster(id);motionBegan=EditorApplication.timeSinceStartup;nextMotionShot=motionBegan+1;motionFrames=0;
                File.AppendAllText(ProjectPath.Report(Key+"-Runtime.txt"),"Launched "+label+" (appearance check, force=0): "+result+"\n");
                return;
            }
            if(!motionCamera)
            {
                var go=new GameObject("Runtime disaster validation camera",typeof(Camera));motionCamera=go.GetComponent<Camera>();motionCamera.enabled=false;
                motionCamera.fieldOfView=55;motionCamera.farClipPlane=1600;motionCamera.depthTextureMode=DepthTextureMode.Depth;
            }
            bool shown=false;
            if(motionStage==0)
            {
                foreach(var source in DisasterStates.Active)
                {
                    var f=source.Capture();if(f==null||f.kind!="Tornado"||f.warning>0)continue;
                    float size=f.values[1],height=f.values[0];Aim(motionCamera,f.center+new Vector3(size*3,height*.55f,size*3),f.center+Vector3.up*height*.55f);shown=true;break;
                }
            }
            else foreach(var source in Tracked.Waves)
            {
                var f=source.Wave;if(!f.active||f.height<8)continue;
                var right=Vector3.Cross(Vector3.up,f.direction);Aim(motionCamera,f.center+right*f.width*.75f-f.direction*f.depth*.35f+Vector3.up*f.height*.6f,f.center+Vector3.up*f.height*.5f);shown=true;break;
            }
            if(shown&&EditorApplication.timeSinceStartup>=nextMotionShot)
            {
                string output=Path.Combine(Path.GetDirectoryName(Application.dataPath),"ArtDirection","DisasterOuterSwell-20261027");Directory.CreateDirectory(output);
                Shot(motionCamera,Path.Combine(output,label+"-runtime-"+motionFrames+".png"));motionFrames++;nextMotionShot=EditorApplication.timeSinceStartup+.35;
                if(motionFrames>=3)
                {
                    demo.Run(DemoCommand.StopDisasters);File.AppendAllText(ProjectPath.Report(Key+"-Runtime.txt"),"PASS: "+label+" launched through demo controls and captured in 3 live Game frames.\n");motionStage++;motionBegan=0;
                }
            }
        }
        static object Field(object target,string name)=>target.GetType().GetField(name,Private).GetValue(target);
        static void Invoke(object target,string name,params object[] values)=>target.GetType().GetMethod(name,Private).Invoke(target,values);
        static GameObject Draw(Scene scene,GameObject parent,Mesh mesh,Material material,Matrix4x4 matrix)
        {
            var go=new GameObject("Production visual snapshot",typeof(MeshFilter),typeof(MeshRenderer));
            SceneManager.MoveGameObjectToScene(go,scene);go.transform.SetParent(parent.transform,false);
            go.transform.SetPositionAndRotation(matrix.GetColumn(3),matrix.rotation);go.transform.localScale=matrix.lossyScale;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return go;
        }
        static void Shot(Camera camera,string path)
        {
            var rt=RenderTexture.GetTemporary(1920,1080,24);var old=RenderTexture.active;var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
        }
        static void Aim(Camera camera,Vector3 position,Vector3 target){camera.transform.position=position;camera.transform.LookAt(target);}
        static void ReleaseArt(CandyDisasterVisuals art)
        {
            foreach(var field in art.GetType().GetFields(Private))
                if(field.FieldType==typeof(Material)||field.FieldType==typeof(Mesh)||field.FieldType==typeof(Texture3D))
                {
                    var value=field.GetValue(art) as Object;field.SetValue(art,null);
                    if(value&&!AssetDatabase.Contains(value))Object.DestroyImmediate(value);
                }
            Object.DestroyImmediate(art);
        }
        static void Capture()
        {
            File.WriteAllText(ProjectPath.Report(Key+".txt"),"Starting isolated production fog/water render.\n");
            string project=Path.GetDirectoryName(Application.dataPath),output=Path.Combine(project,"ArtDirection","DisasterOuterSwell-20261027");Directory.CreateDirectory(output);
            var world=Services.World;var session=Services.Session;var waves=Tracked.Waves.ToArray();bool ignored=Physics.GetIgnoreLayerCollision(Layers.Survivor,Layers.Block);
            var report=new StringBuilder("Actual Unity URP Editor renders of revised production visuals. Isolated Game scene; fixed disaster poses; no physics simulation or AI-generated images.\n");
            var scene=EditorSceneManager.OpenPreviewScene(ProjectPath.Find("Game.unity"));
            try
            {
                var roots=scene.GetRootGameObjects();var map=roots.SelectMany(r=>r.GetComponentsInChildren<CastleGenerator>(true)).First();Services.World=map;Services.Session=null;
                var environment=roots.SelectMany(r=>r.GetComponentsInChildren<CandyEnvironment>(true)).First();environment.Refresh();
                var cameraObject=new GameObject("Fog and water review camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,scene);
                var camera=cameraObject.GetComponent<Camera>();camera.enabled=false;camera.depthTextureMode=DepthTextureMode.Depth;camera.scene=scene;camera.clearFlags=CameraClearFlags.Skybox;camera.fieldOfView=52;camera.nearClipPlane=.15f;camera.farClipPlane=1600;
                var art=environment.gameObject.AddComponent<CandyDisasterVisuals>();Invoke(art,"Awake");
                var frame=JsonUtility.FromJson<DisasterFrame>(File.ReadAllText(Path.Combine(project,"ArtDirection","DisasterRedesign-20261026","tornado-frame.json")));
                frame.center=new Vector3(32,frame.center.y,4);
                foreach(string view in new[]{"wide","close","through-fog","lightning"})
                {
                    bool wide=view=="wide"||view=="lightning";
                    Aim(camera,wide?new Vector3(152,66,125):view=="close"?new Vector3(72,22,48):new Vector3(83,18,12),wide?new Vector3(24,39,0):view=="close"?new Vector3(32,28,5):new Vector3(8,26,2));
                    art.GetType().GetField("previewStormFlash",Private).SetValue(art,view=="lightning"?1f:0f);
                    Invoke(art,"BeginObjects");art.GetType().GetField("billboard",Private).SetValue(art,camera.transform.rotation);Invoke(art,"PrepareTornado",frame);
                    var fixture=new GameObject("Tornado frozen draw submissions");SceneManager.MoveGameObjectToScene(fixture,scene);
                    try
                    {
                        Draw(scene,fixture,(Mesh)Field(art,"funnel"),(Material)Field(art,"storm"),Matrix4x4.TRS(frame.center,Quaternion.identity,Vector3.one));
                        Draw(scene,fixture,(Mesh)Field(art,"stormRibbons"),(Material)Field(art,"stormRibbon"),Matrix4x4.TRS(frame.center,Quaternion.identity,Vector3.one));
                        foreach(var pair in new[]{("windPuffs","stormMist"),("stormBolts","stormFlash"),("dustPuffs","dust"),("chips","chipMaterial")})
                        {
                            var mesh=(Mesh)Field(art,pair.Item1=="chips"?"rock":"quad");
                            foreach(var matrix in (List<Matrix4x4>)Field(art,pair.Item1))Draw(scene,fixture,mesh,(Material)Field(art,pair.Item2),matrix);
                        }
                        Shot(camera,Path.Combine(output,"tornado-"+view+".png"));
                    }
                    finally{Object.DestroyImmediate(fixture);}
                }
                int mist=((List<Matrix4x4>)Field(art,"windPuffs")).Count;report.AppendLine("Tornado: real 3D density volume, 96-step self-shadowed ray march, helical advection, irregular volumetric storm canopy, 32 irregular cloud wisps, and six continuous helical vapor streams. Graphite blue/violet shading and silver-blue rims; lightning view freezes the production flash at full intensity to inspect it. Other posed views have the flash disabled. Preview freezes the same production volume and cloud draw submissions.");
                ReleaseArt(art);
                var wave=JsonUtility.FromJson<WaveFrame>(File.ReadAllText(Path.Combine(project,"ArtDirection","DisasterRedesign-20261026","tsunami-frame.json")));
                Tracked.Waves.Clear();var source=new FrozenWave{frame=wave};Tracked.Waves.Add(source);
                var water=environment.gameObject.AddComponent<CandyTsunamiVisuals>();Invoke(water,"Awake");Invoke(water,"LateUpdate");
                var sea=environment.waterRenderer.GetComponent<CandyOceanSurface>();
                if(!sea||sea.ActiveExtent<180)throw new Exception("Storm sea detail does not reach the rear/sides of the wave");
                float stormExtent=sea.ActiveExtent;
                report.AppendLine("PASS: active storm ocean detailed extent="+stormExtent+"m; 128x128 grid reaches beyond rear footprint.");
                var visual=(GameObject)Field(water,"visual");SceneManager.MoveGameObjectToScene(visual,scene);
                var spray=(ParticleSystem)Field(water,"spray");spray.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);spray.Play();
                for(int step=0;step<44;step++){Invoke(water,"EmitCrest",.025f);spray.Simulate(.025f,true,false,true);}
                foreach(string view in new[]{"wide","close","waterline","rear","overhead"})
                {
                    switch(view)
                    {
                        case "wide":Aim(camera,new Vector3(-96,32,-82),new Vector3(-24,12,0));break;
                        case "close":Aim(camera,new Vector3(-47,9,-65),new Vector3(-40,10,0));break;
                        case "rear":Aim(camera,new Vector3(-130,30,55),new Vector3(-65,12,0));break;
                        case "overhead":Aim(camera,new Vector3(-63,100,-12),new Vector3(-60,3,0));break;
                        default:Aim(camera,new Vector3(-69,3,-20),new Vector3(-48,3,2));break;
                    }
                    Shot(camera,Path.Combine(output,"tsunami-"+view+".png"));
                }
                var material=(Material)Field(water,"material");var ocean=environment.waterRenderer.sharedMaterial;
                var sprayMaterial=(Material)Field(water,"sprayMaterial");var spindrift=(ParticleSystem)Field(water,"spindrift");
                if(sprayMaterial.renderQueue<=material.renderQueue||spray.particleCount==0||spindrift.particleCount==0)throw new Exception("Breaking spray missing or drawn before water");
                report.AppendLine("PASS: spray queue follows water; fine spray="+spray.particleCount+", spindrift="+spindrift.particleCount+" (384 cap each)." );
                foreach(var property in new[]{"_Deep","_Shallow","_Foam","_Reflection","_Horizon"})
                {
                    if(material.GetColor(property)!=ocean.GetColor(property))throw new Exception("Ocean material mismatch: "+property);
                    report.AppendLine(property+" matches ocean: "+material.GetColor(property));
                }
                report.AppendLine("Tsunami: production surface grid "+((Mesh)Field(water,"mesh")).vertexCount+" vertices; surrounding wave patch; shared ocean/wave swells and normal displacement; production ocean colors/reflections. Production lip-following spray is included after 44 emission/simulation steps (1.1 seconds). The hollow curling face joins a broad cubic rear swell with a rounded rear footprint; the rear foot extends 1.6 wave depths behind the origin. Triangulated side sections close the water volume around the hollow barrel; separate whitewater sheet. Visual/collision alignment deferred by user.");
                var block=new MaterialPropertyBlock();environment.waterRenderer.GetPropertyBlock(block);
                if(Mathf.Abs(block.GetFloat("_SurgeHeight")-wave.height)>.001f)throw new Exception("Ocean did not receive wave state");
                source.frame=default;Invoke(water,"LateUpdate");environment.waterRenderer.GetPropertyBlock(block);
                if(block.GetFloat("_SurgeHeight")!=0||visual.GetComponent<MeshRenderer>().enabled)throw new Exception("Wave visual did not stop cleanly");
                if(sea.ActiveExtent>=stormExtent)throw new Exception("Ocean storm extent did not restore after stop");
                report.AppendLine("PASS: stop restores normal ocean detail extent="+sea.ActiveExtent+"m.");
                report.AppendLine("PASS: stop clears ocean wave state and hides the wave patch.");
                Object.DestroyImmediate(water);
                foreach(string name in new[]{"Candy Tropical/Storm Funnel","Candy Tropical/Storm Mist","Candy Tropical/Ocean","Candy Tropical/Tsunami","Candy Tropical/Tsunami Spray"})
                {
                    var shader=Shader.Find(name);if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Shader failure: "+name);
                    report.AppendLine("PASS: shader supported, no errors: "+name);
                }
                report.AppendLine("Visual-only revision; prior force envelope intentionally unchanged. No new colliders or Core changes. No Quest/FPS claim. Complete.");File.WriteAllText(Path.Combine(output,"Capture.txt"),report.ToString());File.WriteAllText(ProjectPath.Report(Key+".txt"),report.ToString());
            }
            finally{foreach(var root in scene.GetRootGameObjects())foreach(var art in root.GetComponentsInChildren<CandyDisasterVisuals>(true))ReleaseArt(art);EditorSceneManager.ClosePreviewScene(scene);Services.World=world;Services.Session=session;Tracked.Waves.Clear();Tracked.Waves.AddRange(waves);Physics.IgnoreLayerCollision(Layers.Survivor,Layers.Block,ignored);}
        }
    }
}
