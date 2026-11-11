using System;

using UnityEngine;

using UnityEngine.UI;

using UnityEngine.Rendering.Universal;

using DeadlyJenga.Core;
using DeadlyJenga.Presentation;



namespace DeadlyJenga.CastlePrototype

{

    [DefaultExecutionOrder(2000)]

    public sealed class LobbySpectatorTVWall : MonoBehaviour

    {

        [Range(1,6)] public float updatesPerScreen=1;

        [Range(128,512)] public int textureWidth=256;

        public bool FeedsEnabled=true;

        public int RenderCount {get;private set;}

        public double RenderMilliseconds {get;private set;}

        public int AssignedChannels {get;private set;}

        public double TotalRenderMilliseconds {get;private set;}

        public double AverageRenderMilliseconds=>RenderCount>0?TotalRenderMilliseconds/RenderCount:0;

        public Camera FeedCamera=>feedCamera;

        public int ReadyChannels {get{int count=0;foreach(var channel in channels)if(channel?.texture&&channel.screen.texture==channel.texture)count++;return count;}}

        public long TextureBytes=>9L*textureWidth*Mathf.RoundToInt(textureWidth*9f/16)*6;

        sealed class Channel

        {

            public RawImage screen;public Text label;public RenderTexture texture;public string id;

            public IModeActor actor;public Renderer[] renderers=Array.Empty<Renderer>();public bool[] hidden=Array.Empty<bool>();

        }

        readonly Channel[] channels=new Channel[9];

        ISessionInfo viewer;Camera feedCamera;Texture2D standby;
        Text matchTimer;
        RectTransform fullScreenPanel;
        RawImage fullScreenImage;
        Text fullScreenLabel;
        RenderTexture fullScreenTexture;
        int fullScreenChannel=-1;
        string fullScreenActorId;
        public bool FullScreen=>fullScreenChannel>=0;

        public bool TryPoint(Ray ray,float range,out int index,out float distance)
        {
            index=-1;distance=range;
            for(int i=0;i<channels.Length;i++)
            {
                var channel=channels[i];
                if(channel==null||!NetworkActors.Alive(channel.actor)||!channel.actor.Alive)continue;
                if(!MenuAim.TryPoint(ray,channel.screen.rectTransform,distance,out _,out var hit))continue;
                index=i;distance=Vector3.Distance(ray.origin,hit);
            }
            return index>=0;
        }

        public bool OpenFullScreen(int index)
        {
            if(index<0||index>=channels.Length)return false;
            var channel=channels[index];
            if(channel?.id==null||!NetworkActors.Alive(channel.actor)||!channel.actor.Alive)return false;
            if(!fullScreenPanel)
            {
                var canvas=new GameObject("Full screen spectator television",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();
                canvas.renderMode=RenderMode.WorldSpace;
                canvas.overrideSorting=true;canvas.sortingOrder=300;
                fullScreenPanel=(RectTransform)canvas.transform;
                fullScreenPanel.SetParent(transform,false);
                fullScreenPanel.sizeDelta=new Vector2(1280,720);
                fullScreenImage=new GameObject("Live channel",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
                var image=fullScreenImage.rectTransform;
                image.SetParent(fullScreenPanel,false);image.anchorMin=Vector2.zero;image.anchorMax=Vector2.one;
                image.offsetMin=image.offsetMax=Vector2.zero;fullScreenImage.raycastTarget=false;
                fullScreenLabel=new GameObject("Channel and exit controls",typeof(RectTransform),typeof(Text)).GetComponent<Text>();
                var label=fullScreenLabel.rectTransform;
                label.SetParent(fullScreenPanel,false);label.sizeDelta=new Vector2(1190,50);label.anchoredPosition=new Vector2(0,-320);
                fullScreenLabel.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");fullScreenLabel.fontSize=30;
                fullScreenLabel.fontStyle=FontStyle.Bold;fullScreenLabel.alignment=TextAnchor.MiddleCenter;
                fullScreenLabel.color=Color.white;fullScreenLabel.raycastTarget=false;
                var outline=fullScreenLabel.gameObject.AddComponent<Outline>();outline.effectColor=Color.black;
                MenuAim.PresentInForeground(fullScreenPanel);
            }
            fullScreenChannel=index;fullScreenActorId=channel.id;nextFrame=0;
            fullScreenImage.texture=channel.texture&&channel.screen.texture==channel.texture?channel.texture:standby;
            fullScreenLabel.text=channel.actor.DisplayName+(viewer.IsVR?"   |   Trigger to return":"   |   Click or Esc to return");
            fullScreenPanel.gameObject.SetActive(true);
            return true;
        }

        public void CloseFullScreen()
        {
            fullScreenChannel=-1;fullScreenActorId=null;
            if(fullScreenPanel)fullScreenPanel.gameObject.SetActive(false);
        }

        void PresentFullScreen(Camera eye)
        {
            if(!eye||!eye.isActiveAndEnabled){CloseFullScreen();return;}
            var channel=channels[fullScreenChannel];
            if(channel?.id!=fullScreenActorId)
            {
                int index=0;
                while(index<channels.Length&&channels[index]?.id!=fullScreenActorId)index++;
                if(index==channels.Length){CloseFullScreen();return;}
                fullScreenChannel=index;
                channel=channels[index];
            }
            if(!NetworkActors.Alive(channel.actor)||!channel.actor.Alive){CloseFullScreen();return;}
            float depth=Mathf.Max(.55f,eye.nearClipPlane+.18f);
            float height=2*depth*Mathf.Tan(eye.fieldOfView*Mathf.Deg2Rad*.5f);
            float scale=height*Mathf.Max(1f,eye.aspect/(16f/9))/720f;
            fullScreenPanel.SetPositionAndRotation(eye.transform.position+eye.transform.forward*depth,eye.transform.rotation);
            fullScreenPanel.localScale=Vector3.one*(scale/Mathf.Abs(transform.lossyScale.x));
            if(Time.unscaledTime<nextFrame)return;
            nextFrame=Time.unscaledTime+.0667f;
            if(!fullScreenTexture)
                fullScreenTexture=MakeTexture(640);
            RenderChannel(channel,fullScreenTexture,fullScreenImage);
        }
        public void SetMatchTimer(Text label)=>matchTimer=label;

        float nextRoster,nextFrame;int nextChannel;

        readonly Plane[] planes=new Plane[6];

        public void SetChannel(int index,RawImage screen,Text label)=>channels[index]=new Channel{screen=screen,label=label};

        public void Initialize(ISessionInfo session)

        {

            viewer=session;

            standby=new Texture2D(32,18,TextureFormat.RGB24,false){name="Shared CRT standby",filterMode=FilterMode.Point};

            var pixels=new Color32[32*18];

            for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(5,7,10,255);standby.SetPixels32(pixels);standby.Apply(false,true);

            var cameraRoot=new GameObject("Single time-sliced TV render camera");cameraRoot.transform.SetParent(transform,false);

            feedCamera=cameraRoot.AddComponent<Camera>();feedCamera.enabled=false;

            feedCamera.nearClipPlane=.08f;feedCamera.farClipPlane=400;feedCamera.fieldOfView=76;feedCamera.allowHDR=false;feedCamera.allowMSAA=false;

            feedCamera.cullingMask=~((1<<2)|(1<<5));feedCamera.clearFlags=CameraClearFlags.Skybox;

            var data=feedCamera.GetUniversalAdditionalCameraData();data.allowXRRendering=false;data.renderPostProcessing=false;data.renderShadows=false;

            data.requiresColorOption=CameraOverrideOption.Off;data.requiresDepthOption=CameraOverrideOption.Off;data.antialiasing=AntialiasingMode.None;

            foreach(var channel in channels)if(channel!=null){channel.screen.texture=standby;}

        }

        void LateUpdate()

        {

            if(viewer==null||!feedCamera)return;

            if(Time.unscaledTime>=nextRoster)
            {
                nextRoster=Time.unscaledTime+1;
                RefreshRoster();
                if (matchTimer)
                {
                    var frame=(viewer as ILobbyMatchVisitControls)?.VisitingLobby==true?MatchRuntime.Current?.Frame:null;
                    int seconds=Mathf.CeilToInt(frame?.remaining??0);
                    matchTimer.text=frame==null?"":frame.phase is RoundPhase.Victory or RoundPhase.Results
                        ?"ROUND COMPLETE":$"LIVE MATCH    {seconds/60:00}:{seconds%60:00}";
                }
            }

            var eye=viewer.ActiveCamera;
            if(FullScreen){PresentFullScreen(eye);return;}

            if(!FeedsEnabled||!eye||!eye.enabled||(eye.transform.position-transform.position).sqrMagnitude>324)return;

            if(Vector3.Dot(transform.forward,eye.transform.position-transform.position)>0)return;

            GeometryUtility.CalculateFrustumPlanes(eye,planes);

            if(!GeometryUtility.TestPlanesAABB(planes,new Bounds(transform.TransformPoint(new Vector3(0,1.5f,0)),new Vector3(3.8f,3.1f,3.8f))))return;

            if(Time.unscaledTime<nextFrame)return;

            nextFrame=Time.unscaledTime+1f/(9*Mathf.Clamp(updatesPerScreen,1,2)*(viewer.IsVR?.5f:1));

            for(int attempt=0;attempt<9;attempt++)

            {

                var channel=channels[nextChannel];nextChannel=(nextChannel+1)%9;

                if(channel==null||!NetworkActors.Alive(channel.actor)||!channel.actor.gameObject||!channel.actor.Alive)continue;

                if(!channel.texture)channel.texture=MakeTexture(textureWidth);
                RenderChannel(channel,channel.texture,channel.screen);break;

            }

        }

        void RefreshRoster()

        {

            var runtime=MatchRuntime.Current;var rows=runtime?.Frame.phase==RoundPhase.Playing?runtime.Frame.players:null;AssignedChannels=0;

            for(int i=0;i<9;i++)

            {

                var channel=channels[i];if(channel==null)continue;

                IModeActor actor=null;

                if(rows!=null)foreach(var row in rows)

                {

                    if(!row.alive||row.dying||row.role=="hand"||row.role=="spectator")continue;

                    bool assigned=false;for(int j=0;j<i;j++)if(channels[j]?.id==row.id)assigned=true;

                    if(assigned)continue;var candidate=runtime.FindActor(row.id);if(NetworkActors.Alive(candidate)&&candidate.gameObject&&candidate.Alive){actor=candidate;break;}

                }

                if(channel.actor!=actor)

                {

                    channel.actor=actor;channel.id=actor?.ActorId;

                    channel.renderers=actor!=null&&actor.Visual?actor.Visual.GetComponentsInChildren<Renderer>(true):Array.Empty<Renderer>();

                    channel.hidden=new bool[channel.renderers.Length];channel.screen.texture=standby;

                }

                if(actor!=null)AssignedChannels++;

                string name=actor!=null?actor.DisplayName:"STANDBY";

                channel.label.text="CH "+(i+1)+" | "+name;

            }

        }

        static RenderTexture MakeTexture(int width)
        {
            var texture=new RenderTexture(width,Mathf.RoundToInt(width*9f/16),16,RenderTextureFormat.ARGB32)
                {name="Retro TV feed",antiAliasing=1,useMipMap=false,autoGenerateMips=false};
            texture.Create();
            return texture;
        }

        void RenderChannel(Channel channel,RenderTexture texture,RawImage display)
        {

            var pose=channel.actor.ViewPose;

            feedCamera.transform.SetPositionAndRotation(pose.position,pose.rotation);feedCamera.targetTexture=texture;

            for(int i=0;i<channel.renderers.Length;i++){var renderer=channel.renderers[i];if(!renderer)continue;channel.hidden[i]=renderer.forceRenderingOff;renderer.forceRenderingOff=true;}

            long start=System.Diagnostics.Stopwatch.GetTimestamp();

            try{feedCamera.Render();display.texture=texture;RenderCount++;}

            finally

            {

                RenderMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;

                TotalRenderMilliseconds+=RenderMilliseconds;

                for(int i=0;i<channel.renderers.Length;i++)if(channel.renderers[i])channel.renderers[i].forceRenderingOff=channel.hidden[i];

                feedCamera.targetTexture=null;

                // A slow scene lowers feed rate instead of queuing more work.

                nextFrame=Mathf.Max(nextFrame,Time.unscaledTime+Mathf.Min(.5f,(float)RenderMilliseconds*.025f));

            }

        }

        void OnDestroy()

        {

            foreach(var channel in channels)if(channel?.texture){channel.texture.Release();Destroy(channel.texture);}

            if(fullScreenTexture){fullScreenTexture.Release();Destroy(fullScreenTexture);}
            if(standby)Destroy(standby);if(feedCamera)Destroy(feedCamera.gameObject);

        }

    }

}

