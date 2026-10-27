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
        float started,duration,finishedAt,finishFrom,fade;
        bool finishing,arrival;float columnHeight=150,arrivalTime,arrivalTick;
        public float FadeAmount=>fade;
        public bool Arriving=>arrival;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()
        {current=null;GameEvents.DepartureStarted-=Begin;GameEvents.DepartureStarted+=Begin;GameEvents.Emoted-=ArrivalCue;GameEvents.Emoted+=ArrivalCue;}
        static void ArrivalCue(GameObject who,string cue)
        {
            if(cue!="SpawnArrival"||who!=Services.Session?.Player?.gameObject)return;
            if(!current)
            {
                var go=new GameObject("Visual - player arrival beacon");DontDestroyOnLoad(go);current=go.AddComponent<CandySessionTravel>();
                current.arrival=true;current.duration=3;current.started=Time.unscaledTime;current.Build();
            }
            else current.StartArrival();
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
        {if(curtain)curtain.enabled=(Services.Session?.IsVR??false)&&camera==Services.Session.ActiveCamera;}
        void Emoted(GameObject who,string emote)
        {if(emote=="TravelArrived"&&!arrival)Finish(true);else if(emote=="DepartureCancelled"||emote=="SpawnArrivalSettled"&&who==Services.Session?.Player?.gameObject)Finish(false);}
        void Lobby(GameObject who)=>Finish(false);
        void Finish(bool arrived)
        {
            if(finishing)return;
            finishing=true;finishedAt=Time.unscaledTime;finishFrom=arrived?1:fade;
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
                foreach(var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                {
                    if(!(behaviour is INetworkActorView actor)||!actor.NetworkActive||actor.NetworkKind!=NetworkKinds.Survivor)continue;
                    AddTarget(behaviour.transform,targets);
                }
            }
            var go=new GameObject("Soft white arrival fade",typeof(RectTransform),typeof(Canvas));
            go.transform.SetParent(transform,false);canvas=go.GetComponent<Canvas>();canvas.sortingOrder=2000;
            var image=new GameObject("White fade",typeof(RectTransform),typeof(Image));image.transform.SetParent(go.transform,false);
            curtain=image.GetComponent<Image>();curtain.raycastTarget=false;
            var rect=curtain.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            AddBots();Render();
        }
        float ArrivalHeight()
        {
            var player=Services.Session?.Player;float height=player?.gameObject.GetComponent<IMovementState>()?.Height??1.8f;
            if(player!=null&&player.IsVR&&player.ViewCamera)height=Mathf.Max(height,player.ViewCamera.transform.position.y-player.transform.position.y+.2f);
            return Mathf.Max(.6f,height+Mathf.Clamp(height*.4f,.25f,.7f));
        }
        void StartArrival()
        {
            foreach(var column in columns)if(column)Destroy(column.gameObject);columns.Clear();
            if(columnMesh)Destroy(columnMesh);
            arrival=true;finishing=false;started=arrivalTick=Time.unscaledTime;arrivalTime=0;duration=3;columnHeight=ArrivalHeight();
            columnMesh=CandySummoningColumn.CreateMesh(columnHeight);
            if(columnMaterial)
            {
                targets.Clear();var player=Services.Session?.Player;
                if(NetworkActors.Alive(player))AddTarget(player.transform,targets);
                foreach(var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                {
                    if(!(behaviour is INetworkActorView actor)||!actor.NetworkActive||actor.NetworkKind!=NetworkKinds.Survivor)continue;
                    AddTarget(behaviour.transform,targets);
                }
            }
            AddBots();Render();
        }
        void AddBots()
        {
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
            var column=go.AddComponent<CandySummoningColumn>();column.Initialize(target,columnMesh,columnMaterial,columns.Count,columnHeight,arrival);
            columns.Add(column);
        }
        void LateUpdate()
        {
            if(!finishing&&Time.unscaledTime>=nextBotRefresh)
            {nextBotRefresh=Time.unscaledTime+.1f;AddBots();}
            if(arrival&&!finishing){float now=Time.unscaledTime;arrivalTime+=Mathf.Clamp(now-arrivalTick,0,.05f);arrivalTick=now;}
            if(!finishing&&(arrival?arrivalTime:Time.unscaledTime-started)>duration+20)Finish(false);
            Render();if(finishing&&Time.unscaledTime-finishedAt>.65f)Destroy(gameObject);
        }
        void Render()
        {
            float elapsed=arrival?arrivalTime:Time.unscaledTime-started,p=Mathf.Clamp01(elapsed/Mathf.Max(.3f,duration));
            float end=finishing?Mathf.Clamp01((Time.unscaledTime-finishedAt)/.6f):0;
            fade=finishing?finishFrom*(1-Mathf.SmoothStep(0,1,end)):arrival?1-Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.55f)):Mathf.SmoothStep(0,1,Mathf.InverseLerp(.82f,.99f,p));
            float strength=finishing?1-Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-finishedAt)/.35f)):arrival?1:Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.12f,p));
            foreach(var column in columns)if(column)column.Render(arrival?-elapsed:elapsed,arrival?1:p,strength);
            var session=Services.Session;var view=session?.ActiveCamera;
            curtain.enabled=session?.IsVR??false;curtain.color=new Color(.985f,.99f,1,fade);
            if(!view)return;
            if(!session.IsVR){canvas.renderMode=RenderMode.ScreenSpaceOverlay;return;}
            canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=view;
            float distance=Mathf.Max(.2f,view.nearClipPlane+.05f);
            float height=2*distance*Mathf.Tan(Mathf.Clamp(view.fieldOfView,100,130)*Mathf.Deg2Rad*.5f)*1.3f;
            var rect=canvas.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(height*Mathf.Max(view.aspect,1.3f),height);
            rect.localScale=Vector3.one;rect.SetPositionAndRotation(view.transform.position+view.transform.forward*distance,view.transform.rotation);
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
            if(columnMaterial)Destroy(columnMaterial);if(columnMesh)Destroy(columnMesh);if(current==this)current=null;
        }
    }
}
