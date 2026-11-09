using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    public sealed class CandyLobbyPeople : MonoBehaviour
    {
        ILobbyDetails details;ILobbyControls lobby;ISessionInfo session;
        readonly Dictionary<string,TextMesh> labels=new Dictionary<string,TextMesh>();
        readonly Dictionary<string,TextMesh> hostLabels=new Dictionary<string,TextMesh>();
        readonly Dictionary<GameObject,TextMesh> botLabels=new Dictionary<GameObject,TextMesh>();
        readonly HashSet<GameObject> activeBots=new HashSet<GameObject>();
        readonly List<GameObject> removedBots=new List<GameObject>();
        Canvas noticeCanvas;Text notice;Camera eye;float nextNames;string lastNotice;float noticeUntil;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.LobbyStarted+=go=>{if(!go.GetComponent<CandyLobbyPeople>())go.AddComponent<CandyLobbyPeople>();};
        void Start()
        {
            details=GetComponent<ILobbyDetails>();lobby=GetComponent<ILobbyControls>();session=GetComponent<ISessionInfo>();
            var root=new GameObject("Lobby host notification",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));root.transform.SetParent(transform,false);noticeCanvas=root.GetComponent<Canvas>();noticeCanvas.sortingOrder=50;
            var go=new GameObject("Host requests",typeof(RectTransform),typeof(Text),typeof(Outline));go.transform.SetParent(root.transform,false);notice=go.GetComponent<Text>();notice.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");notice.fontSize=22;notice.fontStyle=FontStyle.Bold;notice.color=Color.white;notice.alignment=TextAnchor.MiddleCenter;notice.raycastTarget=false;notice.rectTransform.sizeDelta=new Vector2(850,90);notice.rectTransform.anchoredPosition=new Vector2(0,255);go.GetComponent<Outline>().effectColor=new Color(.06f,.02f,.1f,.9f);
        }
        void Update()
        {
            if(details==null||session==null||!session.ActiveCamera||!ReferenceEquals(Services.Session,session))
            {
                foreach(var label in botLabels.Values)if(label)label.gameObject.SetActive(false);
                return;
            }
            if(eye!=session.ActiveCamera){eye=session.ActiveCamera;var root=(RectTransform)noticeCanvas.transform;if(session.IsVR){root.SetParent(eye.transform,false);noticeCanvas.renderMode=RenderMode.WorldSpace;root.sizeDelta=new Vector2(1280,720);root.localPosition=Vector3.forward*1.2f;root.localScale=Vector3.one*.00065f;}else{noticeCanvas.renderMode=RenderMode.ScreenSpaceOverlay;var scale=noticeCanvas.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);}}
            string message=details.Notification;if(lastNotice!=message){lastNotice=message;noticeUntil=Time.unscaledTime+8;}
            notice.text=message;notice.enabled=!string.IsNullOrEmpty(message)&&(Time.unscaledTime<noticeUntil||lobby.IsHost&&details.HostRequests.Count>0);
            if(Time.unscaledTime<nextNames)return;nextNames=Time.unscaledTime+.1f;
            foreach(var p in details.Participants)
            {
                if(p.id==details.LocalParticipantId)continue;var root=details.ParticipantRoot(p.id);if(!root)continue;var actor=root.GetComponent<IModeActor>();var point=(actor?.ViewPose.position??root.position+Vector3.up*1.6f)+Vector3.up*.3f;
                var screen=eye.WorldToViewportPoint(point);bool inView=screen.z>0&&screen.x>=0&&screen.x<=1&&screen.y>=0&&screen.y<=1;
                bool visible=inView&&(p.host||!Physics.Linecast(eye.transform.position,point,(1<<Layers.Block)|(1<<Layers.Surface),QueryTriggerInteraction.Ignore));
                if(!labels.TryGetValue(p.id,out var tag)){tag=CandyNameplate.Create(transform,"Lobby name · "+p.id);labels[p.id]=tag;var host=CandyNameplate.Create(tag.transform,"Host badge");hostLabels[p.id]=host;host.characterSize=.06f;host.text="HOST";host.fontStyle=FontStyle.Bold;}
                tag.gameObject.SetActive(visible);if(!visible)continue;
                tag.text=p.displayName;tag.color=Color.white;tag.transform.position=point;tag.transform.rotation=Quaternion.LookRotation(point-eye.transform.position);CandyNameplate.RefreshShadow(tag);
                var badge=hostLabels[p.id];badge.gameObject.SetActive(p.host);float scale=Mathf.Max(1,Vector3.Distance(eye.transform.position,point)/12);badge.transform.localPosition=Vector3.up*(.18f*scale);badge.transform.localScale=Vector3.one*scale;CandyNameplate.RefreshShadow(badge);
            }
            foreach(var id in labels.Keys.ToArray())if(!details.Participants.Any(p=>p.id==id)){Destroy(labels[id].gameObject);labels.Remove(id);hostLabels.Remove(id);}
            UpdateBotTags();
        }
        void UpdateBotTags()
        {
            activeBots.Clear();
            var population=session.SurvivorBots;
            if(population!=null)
            {
                foreach(var bot in population.Members)
                {
                    if(!bot||!bot.activeInHierarchy)continue;
                    var actor=bot.GetComponent<IModeActor>();
                    if(actor!=null&&!actor.Alive)continue;
                    activeBots.Add(bot);
                    if(!botLabels.TryGetValue(bot,out var label)||!label)
                    {
                        label=CandyNameplate.Create(transform,"Lobby bot name");
                        botLabels[bot]=label;
                    }
                    var point=(actor?.ViewPose.position??bot.transform.position+Vector3.up*1.6f)+Vector3.up*.28f;
                    var screen=eye.WorldToViewportPoint(point);
                    bool visible=screen.z>0&&screen.z<42&&screen.x>=0&&screen.x<=1&&screen.y>=0&&screen.y<=1&&
                        !Physics.Linecast(eye.transform.position,point,(1<<Layers.Block)|(1<<Layers.Surface),QueryTriggerInteraction.Ignore);
                    label.gameObject.SetActive(visible);
                    if(!visible)continue;
                    label.text=actor?.DisplayName??bot.name;
                    label.color=new Color(.9f,.96f,1);
                    label.transform.position=point;
                    label.transform.rotation=Quaternion.LookRotation(point-eye.transform.position);
                    CandyNameplate.RefreshShadow(label);
                }
            }
            removedBots.Clear();
            foreach(var pair in botLabels)
                if(!pair.Key||!activeBots.Contains(pair.Key))
                {
                    if(pair.Value)Destroy(pair.Value.gameObject);
                    removedBots.Add(pair.Key);
                }
            foreach(var bot in removedBots)botLabels.Remove(bot);
        }

        void OnDestroy()
        {
            if(noticeCanvas)Destroy(noticeCanvas.gameObject);
            foreach(var tag in labels.Values)if(tag)Destroy(tag.gameObject);
            foreach(var tag in botLabels.Values)if(tag)Destroy(tag.gameObject);
        }
    }
}
