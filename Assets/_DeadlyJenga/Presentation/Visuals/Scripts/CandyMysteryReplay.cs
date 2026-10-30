using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    // Ten Hz pose history, no recording camera, no video encoder and no replay physics.
    [DefaultExecutionOrder(500)]
    public sealed class CandyMysteryReplay:MonoBehaviour
    {
        const int Samples=102,MaxNodes=4096,MaxDraws=2048,ReplayLayer=31;
        struct PoseState {public Vector3 position,scale;public Quaternion rotation;public bool active;}
        sealed class Sample {public float time;public PoseState[] poses;public bool[] visible;public Dictionary<string,Pose> eyes=new Dictionary<string,Pose>();}
        sealed class Draw {public Renderer source,copy;public Mesh mesh;public Material[] materials;public int node;public string actor;public bool skin;public int[] bones;public MaterialPropertyBlock properties;}
        readonly List<Transform> nodes=new List<Transform>();readonly Dictionary<Transform,int> indices=new Dictionary<Transform,int>();
        readonly List<Draw> draws=new List<Draw>();readonly HashSet<Renderer> registered=new HashSet<Renderer>();
        readonly Sample[] ring=new Sample[Samples];readonly List<Sample> clip=new List<Sample>();
        readonly List<Transform> ghosts=new List<Transform>();
        int cursor,count,lastSequence,lastRound;float nextSample,nextDiscover,lastElapsed=-1,started,nextRender;
        IMatchRuntime owner;MysteryReplayCue cue;GameObject stage;Camera replayCamera;Canvas canvas;RawImage left,right;Text title;Camera eye,maskedEye;int savedMask;RenderTexture a,b;InputAction skip;
        bool playing,vr;int captureNodes,captureDraws;double captureTotal;int captures;
        public bool Playing=>playing;public int SampleCount=>count;public int RecordedNodes=>nodes.Count;
        public double AverageCaptureMilliseconds=>captures>0?captureTotal/captures:0;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=go=>{if(!go.GetComponent<CandyMysteryReplay>())go.AddComponent<CandyMysteryReplay>();};
        void Awake(){skip=new InputAction("Skip replay",InputActionType.Button);skip.AddBinding("<Keyboard>/escape");skip.AddBinding("<Keyboard>/space");skip.AddBinding("<XRController>/gripPressed");skip.Enable();}
        int Node(Transform t)
        {if(!t)return -1;if(indices.TryGetValue(t,out int n))return n;if(nodes.Count>=MaxNodes)return -1;n=nodes.Count;indices.Add(t,n);nodes.Add(t);return n;}
        void Discover(IMatchRuntime match)
        {
            foreach(var p in match.Frame.players)
            {
                var actor=match.FindActor(p.id);if(actor==null)continue;
                foreach(var renderer in actor.gameObject.GetComponentsInChildren<Renderer>(true))Register(renderer,p.id);
            }
            var blocks=Services.World?.Blocks;if(blocks!=null)foreach(var body in blocks)if(body)foreach(var r in body.GetComponentsInChildren<MeshRenderer>(true))Register(r,null);
        }
        void Register(Renderer r,string actor)
        {
            if(!r||registered.Contains(r)||draws.Count>=MaxDraws||r is ParticleSystemRenderer||r.GetComponentInParent<Canvas>())return;
            var skin=r as SkinnedMeshRenderer;var filter=r.GetComponent<MeshFilter>();var mesh=skin?skin.sharedMesh:filter?filter.sharedMesh:null;if(!mesh)return;
            int node=Node(r.transform);if(node<0)return;
            var d=new Draw{source=r,mesh=mesh,materials=r.sharedMaterials,node=node,actor=actor,skin=skin,properties=new MaterialPropertyBlock()};r.GetPropertyBlock(d.properties);
            if(skin){var bones=skin.bones;d.bones=new int[bones.Length];for(int i=0;i<bones.Length;i++){d.bones[i]=Node(bones[i]);if(d.bones[i]<0)return;}}
            registered.Add(r);draws.Add(d);
        }
        void Capture(IMatchRuntime match,float now)
        {
            long began=System.Diagnostics.Stopwatch.GetTimestamp();
            var sample=ring[cursor]??(ring[cursor]=new Sample());sample.time=now;
            if(sample.poses==null||sample.poses.Length<nodes.Count)sample.poses=new PoseState[nodes.Count];
            if(sample.visible==null||sample.visible.Length<draws.Count)sample.visible=new bool[draws.Count];
            for(int i=0;i<nodes.Count;i++){var t=nodes[i];sample.poses[i]=t?new PoseState{position=t.position,rotation=t.rotation,scale=t.lossyScale,active=t.gameObject.activeInHierarchy}:default;}
            for(int i=0;i<draws.Count;i++){var r=draws[i].source;sample.visible[i]=r&&r.enabled&&r.gameObject.activeInHierarchy;}
            sample.eyes.Clear();foreach(var p in match.Frame.players)if(!p.dying)
            {var actor=match.FindActor(p.id);sample.eyes[p.id]=actor!=null?actor.ViewPose:new Pose(p.viewPosition,p.viewRotation);}
            cursor=(cursor+1)%Samples;count=Mathf.Min(count+1,Samples);
            captureTotal+=(System.Diagnostics.Stopwatch.GetTimestamp()-began)*1000.0/System.Diagnostics.Stopwatch.Frequency;captures++;
        }
        void LateUpdate()
        {
            var match=MatchRuntime.Current;var session=Services.Session;
            if(match==null||session==null||match.Rules.mode!=GameModeKind.MurderMystery){Clear();return;}
            if(!ReferenceEquals(owner,match)||match.Frame.elapsed+.5f<lastElapsed){Clear();owner=match;}
            lastElapsed=match.Frame.elapsed;float now=Time.unscaledTime;
            if(match.Frame.phase==RoundPhase.Playing&&now>=nextSample)
            {
                nextSample=now+.1f;if(now>=nextDiscover){nextDiscover=now+1;Discover(match);}Capture(match,now);
            }
            var incoming=(match as IMysteryReplaySource)?.ReplayCue;
            if(incoming!=null&&(incoming.sequence!=lastSequence||incoming.round!=lastRound))
            {
                var local=Array.Find(match.Frame.players,p=>p.id==match.LocalActorId);
                if(incoming.roundEnd||incoming.victim==match.LocalActorId&&local!=null&&(!local.alive||local.dying))
                {lastSequence=incoming.sequence;lastRound=incoming.round;Begin(incoming,now-Mathf.Clamp(match.Frame.elapsed-incoming.elapsed,0,10));}
            }
            if(!playing)return;
            float duration=clip[clip.Count-1].time-clip[0].time,progress=now-started;
            var own=Array.Find(match.Frame.players,p=>p.id==match.LocalActorId);
            if(progress>=duration||progress>.5f&&skip.WasPressedThisFrame()||!cue.roundEnd&&own!=null&&own.alive&&!own.dying){Stop();return;}
            if(eye!=session.ActiveCamera||vr!=session.IsVR){eye=session.ActiveCamera;vr=session.IsVR;CandyHudLayout.Configure(canvas,transform,eye,vr);}
            MaskBackground();
            canvas.enabled=eye;title.text=(cue.roundEnd?"FINAL 10 SECONDS":"KILL CAM")+" · MURDERER"+(string.IsNullOrEmpty(cue.sheriff)?"":"  |  SHERIFF")+"\n"+Mathf.CeilToInt(duration-progress)+"s · "+(session.IsVR?"Grip to skip":"Space / Esc to skip");
            if(now<nextRender)return;nextRender=now+1f/20;
            float time=clip[0].time+progress;int hi=1;while(hi<clip.Count-1&&clip[hi].time<time)hi++;
            var before=clip[hi-1];var after=clip[hi];float t=Mathf.InverseLerp(before.time,after.time,time);
            for(int i=0;i<captureNodes;i++)
            {
                var x=i<before.poses.Length?before.poses[i]:default;var y=i<after.poses.Length?after.poses[i]:x;
                ghosts[i].SetPositionAndRotation(Vector3.Lerp(x.position,y.position,t),Quaternion.Slerp(x.rotation,y.rotation,t));ghosts[i].localScale=Vector3.Lerp(x.scale,y.scale,t);
            }
            stage.SetActive(true);Render(cue.murderer,a,before,after,t);if(b)Render(cue.sheriff,b,before,after,t);stage.SetActive(false);
        }
        void Begin(MysteryReplayCue value,float end)
        {
            Stop();if(count<2)return;cue=value;clip.Clear();
            for(int i=0;i<count;i++)
            {
                var source=ring[(cursor-count+i+Samples)%Samples];if(source.time<end-10||source.time>end+.2f)continue;
                clip.Add(new Sample{time=source.time,poses=(PoseState[])source.poses.Clone(),visible=(bool[])source.visible.Clone(),eyes=new Dictionary<string,Pose>(source.eyes)});
            }
            if(clip.Count<2||!clip.Exists(s=>s.eyes.ContainsKey(value.murderer))){clip.Clear();return;}
            stage=new GameObject("Visual-only historical replay");stage.SetActive(false);captureNodes=nodes.Count;captureDraws=draws.Count;
            for(int i=0;i<captureNodes;i++){var go=new GameObject("Recorded pose");go.layer=ReplayLayer;go.transform.SetParent(stage.transform,false);ghosts.Add(go.transform);}
            for(int i=0;i<captureDraws;i++)
            {
                var d=draws[i];if(!d.mesh)continue;
                Renderer copy;
                if(d.skin){var skin=ghosts[d.node].gameObject.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=d.mesh;var bones=new Transform[d.bones.Length];for(int n=0;n<bones.Length;n++)bones[n]=ghosts[d.bones[n]];skin.bones=bones;skin.updateWhenOffscreen=true;skin.localBounds=new Bounds(Vector3.zero,Vector3.one*10);copy=skin;}
                else{ghosts[d.node].gameObject.AddComponent<MeshFilter>().sharedMesh=d.mesh;copy=ghosts[d.node].gameObject.AddComponent<MeshRenderer>();}
                copy.sharedMaterials=d.materials;copy.SetPropertyBlock(d.properties);copy.shadowCastingMode=ShadowCastingMode.Off;copy.receiveShadows=false;d.copy=copy;
            }
            Background();
            var cameraObject=new GameObject("Replay camera");replayCamera=cameraObject.AddComponent<Camera>();replayCamera.enabled=false;replayCamera.stereoTargetEye=StereoTargetEyeMask.None;replayCamera.cullingMask=1<<ReplayLayer;replayCamera.fieldOfView=85;replayCamera.nearClipPlane=.08f;replayCamera.farClipPlane=600;replayCamera.allowHDR=false;replayCamera.allowMSAA=false;replayCamera.clearFlags=CameraClearFlags.SolidColor;replayCamera.backgroundColor=new Color(.15f,.65f,.88f);
            a=Texture("Murderer replay");if(!string.IsNullOrEmpty(value.sheriff))b=Texture("Sheriff replay");
            BuildScreen();started=Time.unscaledTime;nextRender=0;playing=true;
        }
        void Background()
        {
            int added=0;foreach(var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if(registered.Contains(r)||r.transform.IsChildOf(stage.transform)||!r.enabled||r.GetComponentInParent<Canvas>()||r.GetComponentInParent<Rigidbody>()||r.GetComponentInParent<IModeActor>()!=null)continue;
                var filter=r.GetComponent<MeshFilter>();var mesh=filter?filter.sharedMesh:null;if(!mesh||added++>=512)continue;
                var go=new GameObject("Replay scenery",typeof(MeshFilter),typeof(MeshRenderer));go.layer=ReplayLayer;go.transform.SetParent(stage.transform,false);go.transform.SetPositionAndRotation(r.transform.position,r.transform.rotation);go.transform.localScale=r.transform.lossyScale;go.GetComponent<MeshFilter>().sharedMesh=mesh;var c=go.GetComponent<MeshRenderer>();c.sharedMaterials=r.sharedMaterials;var p=new MaterialPropertyBlock();r.GetPropertyBlock(p);c.SetPropertyBlock(p);c.shadowCastingMode=ShadowCastingMode.Off;
            }
        }
        static RenderTexture Texture(string name){var rt=new RenderTexture(640,360,16){name=name};rt.Create();return rt;}
        void Render(string id,RenderTexture output,Sample before,Sample after,float t)
        {
            if(!before.eyes.TryGetValue(id,out var x)&&!after.eyes.TryGetValue(id,out x))return;
            var y=after.eyes.TryGetValue(id,out var pose)?pose:x;
            for(int i=0;i<captureDraws;i++){var d=draws[i];if(d.copy)d.copy.enabled=i<before.visible.Length&&before.visible[i]&&!(d.actor==id&&(d.skin||(ghosts[d.node].position-x.position).sqrMagnitude<.12f));}
            replayCamera.transform.SetPositionAndRotation(Vector3.Lerp(x.position,y.position,t),Quaternion.Slerp(x.rotation,y.rotation,t));replayCamera.targetTexture=output;replayCamera.Render();
        }
        void BuildScreen()
        {
            var root=new GameObject("Mystery replay screen",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));root.transform.SetParent(transform,false);canvas=root.GetComponent<Canvas>();canvas.sortingOrder=110;
            var bg=Panel("Replay backdrop",root.transform,new Vector2(1190,710),Vector2.zero);bg.color=new Color(.015f,.02f,.04f,.98f);
            left=Panel("Murderer",root.transform,new Vector2(b?560:1120,b?315:630),new Vector2(b?-288:0,-10));left.texture=a;
            if(b){right=Panel("Sheriff",root.transform,new Vector2(560,315),new Vector2(288,-10));right.texture=b;}
            var go=new GameObject("Replay title",typeof(RectTransform),typeof(Text));go.transform.SetParent(root.transform,false);title=go.GetComponent<Text>();title.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");title.fontSize=23;title.alignment=TextAnchor.MiddleCenter;title.raycastTarget=false;title.rectTransform.sizeDelta=new Vector2(1100,70);title.rectTransform.anchoredPosition=new Vector2(0,300);
            foreach(var child in root.GetComponentsInChildren<Transform>(true))child.gameObject.layer=5;
            eye=Services.Session?.ActiveCamera;vr=Services.Session?.IsVR==true;CandyHudLayout.Configure(canvas,transform,eye,vr);MaskBackground();
        }
        static RawImage Panel(string name,Transform parent,Vector2 size,Vector2 position)
        {var go=new GameObject(name,typeof(RectTransform),typeof(RawImage));go.transform.SetParent(parent,false);var image=go.GetComponent<RawImage>();image.raycastTarget=false;image.rectTransform.sizeDelta=size;image.rectTransform.anchoredPosition=position;return image;}
        void Stop()
        {
            RestoreBackground();
            playing=false;if(stage)Destroy(stage);if(replayCamera)Destroy(replayCamera.gameObject);if(canvas)Destroy(canvas.gameObject);foreach(var rt in new[]{a,b})if(rt){rt.Release();Destroy(rt);}a=b=null;ghosts.Clear();clip.Clear();
            foreach(var d in draws)d.copy=null;
        }
        void MaskBackground()
        {
            if(maskedEye==eye)return;RestoreBackground();if(!eye)return;
            maskedEye=eye;savedMask=eye.cullingMask;eye.cullingMask=1<<5;
        }
        void RestoreBackground(){if(maskedEye)maskedEye.cullingMask=savedMask;maskedEye=null;}
        void Clear(){if(owner==null&&count==0&&!playing)return;Stop();nodes.Clear();indices.Clear();draws.Clear();registered.Clear();Array.Clear(ring,0,ring.Length);cursor=count=lastSequence=lastRound=0;nextSample=nextDiscover=0;lastElapsed=-1;owner=null;captureTotal=0;captures=0;}
        void OnDisable(){Clear();skip?.Disable();}void OnEnable()=>skip?.Enable();void OnDestroy(){Clear();skip?.Dispose();}
    }
}
