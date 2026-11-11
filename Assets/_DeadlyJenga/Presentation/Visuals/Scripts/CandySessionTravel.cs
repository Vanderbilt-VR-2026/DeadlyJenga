using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    public sealed class CandySessionTravel : MonoBehaviour
    {
        static CandySessionTravel current;
        readonly List<CandySummoningColumn> columns=new List<CandySummoningColumn>();
        readonly HashSet<int> targets=new HashSet<int>();
        float nextBotRefresh;
        Canvas canvas; Image curtain; Mesh columnMesh; Material columnMaterial;
        float started,duration,finishedAt,finishFrom,fade,arrivalFade;
        Camera curtainCamera;
        bool curtainStereo;
        float curtainNear,curtainAspect,curtainFov;
        bool finishing,arrival,visualArrival,replayQueuedArrival;float columnHeight=150,arrivalTime,arrivalTick;
        GameObject queuedVisualArrival;
        public float FadeAmount=>fade;
        public bool Arriving=>arrival;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()
        {current=null;GameEvents.DepartureStarted-=Begin;GameEvents.DepartureStarted+=Begin;GameEvents.Emoted-=ArrivalCue;GameEvents.Emoted+=ArrivalCue;}
        static void ArrivalCue(GameObject who,string cue)
        {
            if((cue!="SpawnArrival"&&cue!="PlayerArrival")||!who||who!=Services.Session?.Player?.gameObject)return;
            bool visualOnly=cue=="PlayerArrival";
            if(visualOnly&&current)
            {
                if(current.arrival&&!current.finishing&&current.targets.Contains(who.transform.GetInstanceID()))return;
                current.visualArrival=true;current.StartArrival();return;
            }
            if(!current)
            {
                var go=new GameObject("Visual - player arrival beacon");DontDestroyOnLoad(go);current=go.AddComponent<CandySessionTravel>();
                current.arrival=true;current.visualArrival=visualOnly;current.duration=visualOnly?1.2f:3;current.started=Time.unscaledTime;current.Build();
            }
            else{current.visualArrival=visualOnly;current.StartArrival();}
        }
        static void Begin(float seconds)
        {
            if(current&&!current.finishing&&!current.arrival)return;
            if(current)
            {
                var obsolete=current;current=null;
                obsolete.gameObject.SetActive(false);Destroy(obsolete.gameObject);
            }
            var go=new GameObject("Visual - player summoning beacon");DontDestroyOnLoad(go);
            current=go.AddComponent<CandySessionTravel>();
            current.started=Time.unscaledTime;current.duration=Mathf.Max(.3f,seconds);current.Build();
        }
        void OnEnable()
        {GameEvents.Emoted+=Emoted;GameEvents.LobbyStarted+=Lobby;GameEvents.HomeStarted+=Lobby;RenderPipelineManager.beginCameraRendering+=ForCamera;}
        void ForCamera(ScriptableRenderContext context,Camera camera)
        {
            if(!curtain)return;
            var session=Services.Session;
            curtain.enabled=fade>.001f&&session?.IsVR==true&&session.ActiveCamera==camera;
            if(!curtain.enabled)return;

            var plane=(RectTransform)canvas.transform;
            plane.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);
            float depth=camera.nearClipPlane+.08f;
            plane.position+=camera.transform.forward*depth;
            if(curtainCamera!=camera||curtainStereo!=camera.stereoEnabled||curtainNear!=camera.nearClipPlane||
                curtainAspect!=camera.aspect||curtainFov!=camera.fieldOfView)
            {
                curtainCamera=camera;curtainStereo=camera.stereoEnabled;curtainNear=camera.nearClipPlane;
                curtainAspect=camera.aspect;curtainFov=camera.fieldOfView;
                Vector2 extent=Vector2.zero;
                for(int eye=0;eye<(curtainStereo?2:1);eye++)
                {
                    var projection=!curtainStereo?Camera.MonoOrStereoscopicEye.Mono:
                        eye==0?Camera.MonoOrStereoscopicEye.Left:Camera.MonoOrStereoscopicEye.Right;
                    for(int corner=0;corner<4;corner++)
                    {
                        var point=camera.ViewportToWorldPoint(new Vector3(corner&1,corner>>1,depth),projection);
                        var local=camera.transform.InverseTransformPoint(point);
                        extent=Vector2.Max(extent,new Vector2(Mathf.Abs(local.x),Mathf.Abs(local.y)));
                    }
                }
                plane.sizeDelta=extent*2.16f+Vector2.one*.01f;
                canvas.worldCamera=camera;
            }
        }
        void Emoted(GameObject who,string emote)
        {if(emote=="TravelArrived"&&!arrival)Finish(true);else if(emote=="DepartureCancelled"&&!arrival||emote=="SpawnArrivalSettled"&&arrival&&!visualArrival&&who==Services.Session?.Player?.gameObject)Finish(false);}
        void Lobby(GameObject who)=>Finish(false);
        void Finish(bool arrived)
        {
            if(finishing)return;
            finishing=true;finishedAt=Time.unscaledTime;finishFrom=fade;
            if(arrived)foreach(var column in columns)if(column)column.SetVisible(false);
        }
        void Build()
        {
            if(arrival){arrivalTime=0;arrivalTick=Time.unscaledTime;}
            var shader=Resources.Load<Shader>("CandyScenery/CandySummoningColumn");
            if(shader)
            {
                columnMaterial=new Material(shader){enableInstancing=true};
                columnHeight=arrival?ArrivalHeight():150;columnMesh=CandySummoningColumn.CreateMesh(columnHeight);
                var player=Services.Session?.Player;
                if(NetworkActors.Alive(player))AddTarget(player.transform,targets);
                if(!visualArrival)foreach(var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                {
                    if(!(behaviour is INetworkActorView actor)||!actor.NetworkActive||actor.NetworkKind!=NetworkKinds.Survivor)continue;
                    AddTarget(behaviour.transform,targets);
                }
            }
            var go=new GameObject("Soft white arrival fade",typeof(RectTransform),typeof(Canvas));
            go.transform.SetParent(transform,false);canvas=go.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=2000;
            var image=new GameObject("White fade",typeof(RectTransform),typeof(Image));image.transform.SetParent(go.transform,false);
            curtain=image.GetComponent<Image>();curtain.raycastTarget=false;
            var rect=curtain.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            AddBots();Render();
        }
        float ArrivalHeight()
        {
            var motion=Services.Session?.Player?.gameObject.GetComponent<IMovementState>();
            return Mathf.Max(.6f,motion?.Height??1.7f)+.75f;
        }
        void StartArrival()
        {
            foreach(var column in columns)if(column){column.SetVisible(false);column.gameObject.SetActive(false);Destroy(column.gameObject);}columns.Clear();
            if(columnMesh)Destroy(columnMesh);
            queuedVisualArrival=null;replayQueuedArrival=false;
            arrivalFade=visualArrival?0:fade;
            arrival=true;finishing=false;started=arrivalTick=Time.unscaledTime;arrivalTime=0;duration=visualArrival?1.2f:3;columnHeight=ArrivalHeight();
            columnMesh=CandySummoningColumn.CreateMesh(columnHeight);
            if(columnMaterial)
            {
                targets.Clear();var player=Services.Session?.Player;
                if(NetworkActors.Alive(player))AddTarget(player.transform,targets);
                if(!visualArrival)foreach(var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                {
                    if(!(behaviour is INetworkActorView actor)||!actor.NetworkActive||actor.NetworkKind!=NetworkKinds.Survivor)continue;
                    AddTarget(behaviour.transform,targets);
                }
            }
            AddBots();Render();
        }
        void AddBots()
        {
            if(visualArrival)return;
            var bots=Services.Session?.SurvivorBots;
            if(bots==null||!columnMesh||!columnMaterial)return;
            foreach(var bot in bots.Members)
            {
                if(!bot||!bot.activeInHierarchy)continue;
                var actor=bot.GetComponent<IModeActor>();
                if(actor!=null&&!actor.Alive)continue;
                AddTarget(bot.transform,targets);
            }
        }
        void AddTarget(Transform target,HashSet<int> targets)
        {
            if(!target||!targets.Add(target.GetInstanceID()))return;
            var go=new GameObject("Summoning column - "+target.name);go.transform.SetParent(transform,false);
            var column=go.AddComponent<CandySummoningColumn>();
            column.Initialize(target,columnMesh,columnMaterial,columns.Count,columnHeight,arrival);
            var localPlayer=Services.Session?.Player;
            if (NetworkActors.Alive(localPlayer)&&localPlayer.transform==target)
                column.SetRiderCamera(localPlayer.ViewCamera);
            columns.Add(column);
        }
        void LateUpdate()
        {
            if(!finishing&&Time.unscaledTime>=nextBotRefresh)
            {nextBotRefresh=Time.unscaledTime+.1f;AddBots();}
            if(arrival&&!finishing){float now=Time.unscaledTime;arrivalTime=visualArrival?now-started:arrivalTime+Mathf.Clamp(now-arrivalTick,0,.05f);arrivalTick=now;}
            if(visualArrival&&arrival&&!finishing&&arrivalTime>=duration)Finish(false);
            if(!finishing&&(arrival?arrivalTime:Time.unscaledTime-started)>duration+20)Finish(false);
            Render();if(finishing&&Time.unscaledTime-finishedAt>.65f){replayQueuedArrival=true;Destroy(gameObject);}
        }
        void Render()
        {
            float elapsed=arrival?arrivalTime:Time.unscaledTime-started,p=Mathf.Clamp01(elapsed/Mathf.Max(.3f,duration));
            float end=finishing?Mathf.Clamp01((Time.unscaledTime-finishedAt)/.6f):0;
            if(finishing)fade=finishFrom*(1-Mathf.SmoothStep(0,1,end));
            else if(arrival)fade=arrivalFade*(1-Mathf.SmoothStep(0,1,elapsed/.48f));
            else fade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(Mathf.Max(0,duration-.36f),duration-.055f,elapsed));
            float strength=finishing?1-Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-finishedAt)/.35f)):arrival?1:Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.12f,p));
            var session = Services.Session;
            var rider = session?.Player;
            foreach (var column in columns)
            {
                if (!column) continue;
                if (NetworkActors.Alive(rider) && column.Target == rider.transform)
                    column.SetRiderCamera(session.ActiveCamera ? session.ActiveCamera : rider.ViewCamera);
                column.Render(arrival ? -elapsed : elapsed, arrival ? 1 : p, strength);
            }
            if(curtain)
            {
                curtain.color=new Color(.985f,.99f,1,fade);
                curtain.enabled=false;
            }
        }
        void OnGUI()
        {
            if(!curtain||(Services.Session?.IsVR??false)||fade<=0)return;
            GUI.depth=-10000;if(Event.current.type!=EventType.Repaint)return;
            var matrix=GUI.matrix;var color=GUI.color;
            try{GUI.matrix=Matrix4x4.identity;GUI.color=new Color(.985f,.99f,1,fade);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);}
            finally{GUI.matrix=matrix;GUI.color=color;}
        }
        void OnDestroy()
        {
            GameEvents.Emoted-=Emoted;GameEvents.LobbyStarted-=Lobby;GameEvents.HomeStarted-=Lobby;
            RenderPipelineManager.beginCameraRendering-=ForCamera;
            if(columnMaterial)Destroy(columnMaterial);if(columnMesh)Destroy(columnMesh);
            if(current==this)
            {
                current=null;
                if(replayQueuedArrival&&Application.isPlaying&&queuedVisualArrival&&queuedVisualArrival.activeInHierarchy&&queuedVisualArrival==Services.Session?.Player?.gameObject)
                    ArrivalCue(queuedVisualArrival,"PlayerArrival");
            }
        }
    }
}
