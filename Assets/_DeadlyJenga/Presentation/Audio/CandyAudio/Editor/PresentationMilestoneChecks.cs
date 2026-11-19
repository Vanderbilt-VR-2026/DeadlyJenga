using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using DeadlyJenga.CandyArt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.Presentation.Editor
{
    public static class PresentationMilestoneChecks
    {
        const string Pending="DeadlyJenga.PresentationMilestone";
        static readonly Queue<(float delay,Action action)> work=new Queue<(float,Action)>();
        static readonly StringBuilder report=new StringBuilder();
        static readonly List<string> errors=new List<string>();
        static double due;static int passed,failed,spawnAttempts;static IMatchRuntime original;static Preview preview;static Camera overview;static Hazard hazard;
        static Renderer[] blocks;static Material[] originalBlockMaterials;static Color winnerColor=new Color(.035f,.27f,1);
        [MenuItem("Tools/Deadly Jenga/Presentation/Check Milestone Rendering")]
        public static void BeginPlay()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before rendering checks.");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]static void Resume()=>EditorApplication.playModeStateChanged+=state=>
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false)){SessionState.SetBool(Pending,false);EditorApplication.delayCall+=Start;}};
        static void Start()
        {
            report.Clear();report.AppendLine("Actual Game scene + Core stand-in presentation state checks. Real graphics rendering; no headset or network peer.");passed=failed=spawnAttempts=0;errors.Clear();
            Application.logMessageReceived+=Log;work.Clear();Queue(4,Setup);due=EditorApplication.timeSinceStartup+4;EditorApplication.update+=Tick;
        }
        static void Log(string message,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error)errors.Add(message+"\n"+stack);}
        static void Queue(float delay,Action action)=>work.Enqueue((delay,action));
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup<due)return;
            if(work.Count==0){Finish();return;}
            var item=work.Dequeue();try{item.action();}catch(Exception e){Check(false,"Exception: "+e);}
            due=EditorApplication.timeSinceStartup+(work.Count>0?work.Peek().delay:.1f);
        }
        static void Check(bool ok,string label){report.AppendLine((ok?"PASS ":"FAIL ")+label);if(ok)passed++;else failed++;}
        static void Setup()
        {
            original=MatchRuntime.Current;Check(original!=null&&Services.Session?.ActiveCamera&&Services.World!=null,"Game starts with session, match, camera and world");
            if(original==null||Services.World==null){Queue(.1f,()=>{});return;}
            if(original.Frame.players.Length<2&&spawnAttempts++<3){var demo=Services.Session.Demo;report.AppendLine($"INFO spawn attempt{spawnAttempts}: canCommand={demo.CanCommand}, demoMode={demo.DemoMode}, botCount={demo.SurvivorCount}, status={demo.SurvivorStatus}");demo.Run(DemoCommand.AddSurvivor);Queue(2,Setup);return;}
            report.AppendLine($"INFO spawn result: canCommand={Services.Session.Demo.CanCommand}, botCount={Services.Session.Demo.SurvivorCount}, actors={original.Frame.players.Length}, status={Services.Session.Demo.SurvivorStatus}");
            Check(Services.Session.Demo.SurvivorCount>0&&original.Frame.players.Length>1,"Actual Demo AddSurvivor command creates registered bot");
            var listeners=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Where(l=>l.enabled&&l.gameObject.activeInHierarchy).ToArray();Check(listeners.Length==1&&listeners[0].transform==Services.Session.ActiveCamera.transform,"Exactly one spatial audio listener follows active player view");
            Services.Session.SetMenu(false);preview=new Preview(original);MatchRuntime.Current=preview;
            var bounds=Services.World.PlayableBounds;overview=new GameObject("QA overview camera").AddComponent<Camera>();overview.enabled=false;overview.fieldOfView=55;overview.farClipPlane=5000;
            
            blocks=Services.World.Blocks.Where(b=>b).SelectMany(b=>b.GetComponent<IClimbable>()?.Visual?.GetComponentsInChildren<Renderer>()??Array.Empty<Renderer>()).ToArray();originalBlockMaterials=blocks.Select(b=>b.sharedMaterial).ToArray();
            var artBounds=blocks[0].bounds;foreach(var r in blocks)artBounds.Encapsulate(r.bounds);var environment=UnityEngine.Object.FindFirstObjectByType<CandyEnvironment>();if(environment&&environment.sandRenderer)artBounds.Encapsulate(environment.sandRenderer.bounds);
            float distance=artBounds.extents.magnitude/Mathf.Sin(overview.fieldOfView*.5f*Mathf.Deg2Rad)*1.12f;overview.transform.position=artBounds.center+new Vector3(1,.65f,1).normalized*distance;overview.transform.LookAt(artBounds.center);
            Check(blocks.Length>100,"Actual castle has renderable block population");var glow=Services.Session.Player.gameObject.GetComponent<CandyVaultGlow>();Check(glow&&glow.tint.a>=.7f&&glow.brightness>=2,"Actual survivor prefab receives brighter blue vault preset");
            foreach(string shader in new[]{"CandyScenicSky","CandyScenicSurface","CandyAvatarHealth","CandyNeon","CandySpirit","CandyLiquidCurtain","CandyObjectiveBeacon"})
            {var s=Resources.Load<Shader>("CandyScenery/"+shader);Check(s&&s.isSupported&&!ShaderUtil.ShaderHasError(s),shader+" compiles and is supported by graphics device");}
            NeonPixelChecks.Check(Check);
            string[] skins={"tropical","night","sunset","volcano","cloud","desert","ice"};
            for(int i=0;i<skins.Length;i++){int index=i;Queue(.35f,()=>{preview.frame.rules.environmentSkin=skins[index];preview.frame.revision++;});Queue(.35f,()=>{var env=UnityEngine.Object.FindFirstObjectByType<CandyEnvironment>();Check(env&&env.profile&&env.waterRenderer.sharedMaterial&&env.waterRenderer.sharedMaterial.shader.isSupported,skins[index]+" scenery renders with live profile");Check(blocks.Select((b,j)=>b.sharedMaterial==originalBlockMaterials[j]).All(v=>v),skins[index]+" leaves all block materials untouched");Capture("Skin-"+skins[index],index==0||index==3||index==6);});}
            Queue(.2f,Claim);Queue(.4f,()=>{Check(Near(blocks[0].sharedMaterial.GetColor("_BaseColor"),Color.red),"Claim snapshot recolors standing block");preview.frame.phase=RoundPhase.Victory;preview.frame.winnerTeam=1;preview.frame.winner="BLUE";preview.frame.revision++;});
            for(int i=0;i<6;i++)Queue(i==0?.4f:1,()=>{preview.frame.revision++;Check(blocks.All(b=>Near(b.sharedMaterial.GetColor("_BaseColor"),winnerColor)),"Victory tint persists on every block at t="+Time.unscaledTime.ToString("0.0"));var env=UnityEngine.Object.FindFirstObjectByType<CandyEnvironment>();var mpb=new MaterialPropertyBlock();env.waterRenderer.GetPropertyBlock(mpb);Check(Near(mpb.GetColor("_Shallow"),winnerColor),"Victory ocean tint persists");});
            Queue(.2f,()=>{preview.frame.phase=RoundPhase.Playing;preview.frame.revision++;});Queue(.4f,()=>{Check(Near(blocks[0].sharedMaterial.GetColor("_BaseColor"),Color.red),"Leaving victory restores territory ownership");hazard=new Hazard{frame=new DisasterFrame{kind="PowerOutage",warning=0,remaining=30,values=new[]{12f,9f,0f,30f},blocks=OutageBlocks()}};DisasterStates.Active.Add(hazard);});
            Queue(.7f,()=>{Check(RenderSettings.ambientSkyColor.maxColorComponent<.2f,"Power outage uses dark purple ambient");var ocean=UnityEngine.Object.FindFirstObjectByType<CandyEnvironment>();var waterProps=new MaterialPropertyBlock();ocean.waterRenderer.GetPropertyBlock(waterProps);Check(waterProps.GetColor("_Deep").maxColorComponent<.05f,"Outage darkens self-lit ocean as well as sky");
                var position=overview.transform.position;var rotation=overview.transform.rotation;var target=Services.World.Center+Vector3.up*(Services.World.PlayableBounds.size.y*.55f);overview.transform.position=target+new Vector3(8,3,8);overview.transform.LookAt(target);Capture("Power-Outage",true);overview.transform.SetPositionAndRotation(position,rotation);DisasterStates.Active.Remove(hazard);});
            Queue(.4f,()=>{hazard.frame=new DisasterFrame{kind="Vampire",warning=0,remaining=30,values=new[]{5f,.2f,90f,0f,30f},blocks=Array.Empty<int>()};DisasterStates.Active.Add(hazard);});
            Queue(.7f,()=>{Check(Shader.GetGlobalFloat("_CandyHeat")>.1f,"Vampire publishes exposure to visible non-warp heat overlay");Check(UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).All(l=>float.IsFinite(l.intensity)),"Vampire transition retains finite lighting");DisasterStates.Active.Remove(hazard);});
            Queue(.3f,Health);Queue(.4f,CheckHealth);Queue(.2f,()=>{hazard.frame=new DisasterFrame{kind="PowerOutage",warning=0,remaining=5,values=new[]{12f,9f,0f,5f},blocks=OutageBlocks()};DisasterStates.Active.Add(hazard);});Queue(.6f,()=>{DisasterStates.Active.Remove(hazard);preview.frame.rules.healthEnabled=false;preview.sink=false;preview.frame.revision++;});Queue(.5f,()=>{var actor=preview.FindActor(preview.LocalActorId);Check(actor?.Visual!=null&&actor.Visual.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterial&&!r.sharedMaterial.shader.name.Contains("Avatar Health")),"Outage teardown plus health-off restores valid original avatar materials");});Queue(.4f,()=>Check(!blocks.Any(b=>b.sharedMaterial.shader.name.Contains("Avatar Health")),"Health overlays never replace block art"));
            Queue(.2f,()=>{var own=preview.frame.players.FirstOrDefault(p=>p.id==preview.LocalActorId);if(own!=null){own.dying=true;own.alive=false;own.deathProgress=.5f;}preview.frame.revision++;});
            Queue(.35f,()=>{Check(UnityEngine.Object.FindObjectsByType<CandyMelt>(FindObjectsSortMode.None).Length>0,"Dying avatar creates dissolving candy ghost");Check(UnityEngine.Object.FindObjectsByType<CandyMelt>(FindObjectsSortMode.None).All(m=>m.GetComponentsInChildren<Collider>().Length==0),"Death ghosts contain no gameplay colliders");preview.frame.phase=RoundPhase.Results;preview.frame.revision++;});
            Queue(.4f,()=>{var hud=UnityEngine.Object.FindFirstObjectByType<DeadlyJenga.CastlePrototype.CandyDemoHud>();Check(hud==null||!hud.ControlHintCanvas||!hud.ControlHintCanvas.enabled,"Results hides demo control hints");CaptureResults();});
            Queue(.4f,()=>{preview.frame.phase=RoundPhase.Playing;foreach(var p in preview.frame.players){p.alive=true;p.dying=false;}preview.frame.revision++;Services.Session.SetMenu(false);});
            Queue(.4f,()=>{var board=CandyTabletop.Current;Check(board,"Tabletop controller attached to actual game session");if(board){board.Begin();Check(!board.Passthrough,"Editor does not falsely report Quest passthrough");report.AppendLine("INFO tabletop: "+board.Status);board.End();}});
        }
        static void Claim(){preview.frame.rules.mode=GameModeKind.BlockControl;preview.frame.rules.environmentSkin="tropical";preview.frame.teams=new[]{new MatchTeamFrame{id=0,color=Color.red,name="RED"},new MatchTeamFrame{id=1,color=winnerColor,name="BLUE"}};preview.frame.claims=new[]{new BlockClaimFrame{block=0,owner=0}};preview.frame.phase=RoundPhase.Playing;preview.frame.revision++;}
        static void Health(){preview.frame.rules.healthEnabled=true;preview.sink=true;for(int i=0;i<preview.frame.players.Length;i++){preview.frame.players[i].health=.5f;preview.frame.players[i].color=preview.frame.players[i].id==preview.LocalActorId?Color.red:Color.blue;}preview.frame.revision++;}
        static void CheckHealth(){var actor=preview.FindActor(preview.LocalActorId);Check(actor?.Visual!=null,"Actual local avatar exposes visual root");if(actor?.Visual==null)return;var r=actor.Visual.GetComponentInChildren<Renderer>();var mpb=new MaterialPropertyBlock();r.GetPropertyBlock(mpb);Check(r.sharedMaterial.shader.name.Contains("Avatar Health")&&Mathf.Abs(mpb.GetFloat("_Health")-.5f)<.01f,"Half-health top-down shader applied to actual player");Check(mpb.GetFloat("_Sink")>.4f,"Sinking readout lowers visual without modifying motor");var other=preview.frame.players.FirstOrDefault(p=>p.id!=preview.LocalActorId);var otherActor=other!=null?preview.FindActor(other.id):null;if(otherActor?.Visual!=null){var second=otherActor.Visual.GetComponentInChildren<Renderer>();var secondProps=new MaterialPropertyBlock();second.GetPropertyBlock(secondProps);Check(second.sharedMaterial==r.sharedMaterial,"Two actors share one health shader material");Check(!Near(secondProps.GetColor("_BaseColor"),mpb.GetColor("_BaseColor")),"Shared health material retains independent actor colors");}else Check(false,"Second actor available for material isolation check");}
        static int[] OutageBlocks(){var candidates=Services.World.Blocks.Select((b,i)=>(b,i)).Where(p=>p.b&&p.b.position.x+p.b.position.z>0).OrderBy(p=>p.b.position.y).ToArray();int step=Mathf.Max(1,candidates.Length/45);return candidates.Where((p,i)=>i%step==0).Select(p=>p.i).ToArray();}
        static void CaptureResults()
        {
            var hud=UnityEngine.Object.FindFirstObjectByType<CandyResultsHud>();var panel=hud.GetComponentsInChildren<Canvas>(true).FirstOrDefault(c=>c.name=="Visual - round results");if(!panel){Check(false,"Results canvas created");return;}Check(panel.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t=>t.text=="RETURN TO LOBBY"),"Results return-to-lobby control has visible text");
            var t=panel.transform;var oldPosition=t.position;var oldRotation=t.rotation;var oldScale=t.localScale;bool active=panel.gameObject.activeSelf;
            panel.gameObject.SetActive(true);t.SetPositionAndRotation(overview.transform.position+overview.transform.forward*1.8f,overview.transform.rotation);t.localScale=Vector3.one*.002f;Canvas.ForceUpdateCanvases();Capture("Results",true);t.SetPositionAndRotation(oldPosition,oldRotation);t.localScale=oldScale;panel.gameObject.SetActive(active);
        }
        static bool Near(Color a,Color b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)<.05f;
        static void Capture(string name,bool save){var descriptor=new RenderTextureDescriptor(960,600,RenderTextureFormat.ARGB32,24){msaaSamples=4,sRGB=QualitySettings.activeColorSpace==ColorSpace.Linear};var target=RenderTexture.GetTemporary(descriptor);overview.allowMSAA=true;var old=RenderTexture.active;overview.targetTexture=target;overview.Render();RenderTexture.active=target;var image=new Texture2D(960,600,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,600),0,0);image.Apply();var pixels=image.GetPixels();var avg=pixels.Aggregate(Color.black,(a,b)=>a+b)/pixels.Length;float variance=pixels.Sum(p=>(p.r-avg.r)*(p.r-avg.r)+(p.g-avg.g)*(p.g-avg.g)+(p.b-avg.b)*(p.b-avg.b))/pixels.Length;Check(variance>.001f,name+" produces nonblank rendered image, variance="+variance.ToString("0.0000"));if(save)File.WriteAllBytes(ProjectPath.Report("Presentation-"+name+".png"),image.EncodeToPNG());overview.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.Destroy(image);}
        static void Finish(){EditorApplication.update-=Tick;Application.logMessageReceived-=Log;if(hazard!=null)DisasterStates.Active.Remove(hazard);if(original!=null)MatchRuntime.Current=original;Check(errors.Count==0,"Actual Game and all presentation transitions produce zero error/exception logs");foreach(var error in errors)report.AppendLine(error);report.AppendLine($"TOTAL {passed} passed, {failed} failed. NOT VERIFIED: Quest passthrough, stereo comfort, device haptics/performance, multiplayer peers.");File.WriteAllText(ProjectPath.Report("PresentationMilestone.txt"),report.ToString());Debug.Log(report.ToString());if(Application.isBatchMode)EditorApplication.Exit(failed==0?0:1);else EditorApplication.ExitPlaymode();}
        sealed class Hazard:IDisasterState{public DisasterFrame frame;public DisasterFrame Capture()=>frame;}
        sealed class Preview:IMatchRuntime
        {
            readonly IMatchRuntime source;public MatchFrame frame;public bool sink;public Preview(IMatchRuntime source){this.source=source;frame=JsonUtility.FromJson<MatchFrame>(JsonUtility.ToJson(source.Frame));}
            public ModeRules Rules=>frame.rules;public MatchFrame Frame=>frame;public string LocalActorId=>source.LocalActorId;public bool Authority=>true;public IModeActor FindActor(string id)=>source.FindActor(id);public Transform FindParticipantRoot(string id)=>source.FindParticipantRoot(id);
            public ModeRules RulesFor(GameObject actor){var r=frame.rules.Copy();r.sinking=sink;r.sinkProgress=sink?.6f:0;return r;}public bool CanClimb(GameObject actor,IClimbable climbable)=>source.CanClimb(actor,climbable);
            public bool RequestPunch(GameObject source,Vector3 origin,Vector3 direction,float speed)=>false;public void Configure(ModeRules rules)=>frame.rules=rules;public void StartRound(){}public void ReturnToLobby(){}public void NextSpectator(int step){}public void ActivateHandPower(float seconds){}
        }
    }
}
