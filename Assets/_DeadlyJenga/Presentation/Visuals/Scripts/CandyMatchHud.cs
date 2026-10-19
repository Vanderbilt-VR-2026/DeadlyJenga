using System.Collections.Generic;
using System.Text;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    // One canvas for desktop or headset; receives only host-authored snapshot values.
    public sealed class CandyMatchHud : MonoBehaviour
    {
        Canvas canvas;RectTransform root;Text board,teams,claim,status,warning;Image claimFill;Camera camera;bool vr;float sampleAt;
        CanvasGroup healthGroup;Image healthFill;Text healthText;
        float shownHealth=1,lastHealth=1,fullHealthAt=-100;
        readonly StringBuilder text=new StringBuilder(512);
        readonly Dictionary<string,TextMesh> plates=new Dictionary<string,TextMesh>();
        readonly Dictionary<string,float> plateSeen=new Dictionary<string,float>();
        readonly RaycastHit[] plateHits=new RaycastHit[16];
        readonly HashSet<string> active=new HashSet<string>();readonly List<string> remove=new List<string>();
        readonly List<Text> markers=new List<Text>();int markerCount;readonly Dictionary<int,int> previousOwners=new Dictionary<int,int>();float flagPulseAt;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=Attach;
        static void Attach(GameObject go){if(!go.GetComponent<CandyMatchHud>())go.AddComponent<CandyMatchHud>();if(!go.GetComponent<CandyMatchVisuals>())go.AddComponent<CandyMatchVisuals>();if(!go.GetComponent<CandyObjectives>())go.AddComponent<CandyObjectives>();if(!go.GetComponent<CandySeaSpirit>())go.AddComponent<CandySeaSpirit>();}
        void Build()
        {
            var go=new GameObject("Visual - match telemetry",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.sortingOrder=26;root=go.GetComponent<RectTransform>();
            var scale=go.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);
            board=Label("Top ten",new Vector2(312,548),new Vector2(460,102),TextAnchor.UpperLeft,16);
            teams=Label("Team scores",new Vector2(460,44),new Vector2(0,290),TextAnchor.MiddleCenter,18);
            teams.resizeTextForBestFit=true;teams.resizeTextMinSize=10;teams.resizeTextMaxSize=18;
            status=Label("Round and spectators",new Vector2(460,68),new Vector2(0,354),TextAnchor.MiddleCenter,17);
            warning=Label("Disaster warning",new Vector2(460,112),new Vector2(0,204),TextAnchor.MiddleCenter,20);warning.color=CandyHudVisuals.Peach;
            claim=Label("Block ownership",new Vector2(400,36),new Vector2(0,-246),TextAnchor.MiddleCenter,17);
            var fill=new GameObject("Claim juice",typeof(RectTransform),typeof(Image));fill.transform.SetParent(root,false);claimFill=fill.GetComponent<Image>();claimFill.color=new Color(.25f,1,.8f,.8f);claimFill.raycastTarget=false;claimFill.rectTransform.anchoredPosition=new Vector2(-160,-268);claimFill.rectTransform.pivot=new Vector2(0,.5f);claimFill.rectTransform.sizeDelta=new Vector2(0,6);
            var health=new GameObject("Health bar",typeof(RectTransform),typeof(CanvasGroup));health.transform.SetParent(root,false);
            var rect=health.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(330,60);rect.anchoredPosition=new Vector2(0,-196);
            healthGroup=health.GetComponent<CanvasGroup>();healthGroup.alpha=0;healthGroup.blocksRaycasts=false;healthGroup.interactable=false;
            var track=new GameObject("Candy health track",typeof(RectTransform),typeof(Image));track.transform.SetParent(health.transform,false);
            var back=track.GetComponent<Image>();back.sprite=CandyHudVisuals.Pill;back.type=Image.Type.Sliced;back.color=new Color(.055f,.02f,.1f,.9f);back.raycastTarget=false;back.rectTransform.sizeDelta=new Vector2(330,19);
            var juice=new GameObject("Health juice",typeof(RectTransform),typeof(Image));juice.transform.SetParent(track.transform,false);healthFill=juice.GetComponent<Image>();healthFill.sprite=CandyHudVisuals.Pill;healthFill.type=Image.Type.Sliced;healthFill.raycastTarget=false;healthFill.rectTransform.pivot=new Vector2(0,.5f);healthFill.rectTransform.anchoredPosition=new Vector2(-160,0);healthFill.rectTransform.sizeDelta=new Vector2(320,11);
            healthText=Label("Health",new Vector2(330,28),new Vector2(0,25),TextAnchor.MiddleCenter,17);healthText.transform.SetParent(health.transform,false);
        }
        Text Label(string name,Vector2 size,Vector2 position,TextAnchor alignment,int fontSize)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text),typeof(Outline));go.transform.SetParent(root,false);var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=fontSize;t.fontStyle=FontStyle.Bold;t.alignment=alignment;t.color=Color.white;t.supportRichText=true;t.raycastTarget=false;t.rectTransform.sizeDelta=size;t.rectTransform.anchoredPosition=position;go.GetComponent<Outline>().effectColor=new Color(.08f,.025f,.14f,.85f);return t;
        }
        void Update()
        {
            var session=Services.Session;var match=MatchRuntime.Current;if(session==null||match?.Frame==null)return;
            if(!canvas)Build();if(camera!=session.ActiveCamera||vr!=session.IsVR){camera=session.ActiveCamera;vr=session.IsVR;Configure();}
            var spectator=session.Demo as ISpectatorControls;
            bool watching=spectator?.Spectating==true;
            canvas.enabled=camera&&!session.MenuOpen&&(!watching||vr);
            if(watching)
            {
                board.text=teams.text=claim.text=warning.text="";claimFill.enabled=false;
                status.text=vr?"WATCHING "+spectator.WatchedName(0)+"\nRight stick switches players":"";
                foreach(var marker in markers)if(marker)marker.enabled=false;
                foreach(var plate in plates.Values)if(plate)plate.gameObject.SetActive(false);
                return;
            }
            UpdateHealthBar(match.Frame,match.LocalActorId);
            if(Time.unscaledTime<sampleAt)return;sampleAt=Time.unscaledTime+.2f;
            var f=match.Frame;MatchPlayerFrame own=null;foreach(var p in f.players)if(p.id==match.LocalActorId){own=p;break;}
            text.Clear();text.Append("TOP 10  ·  ").Append(f.metricLabel).Append('\n');
            foreach(var p in f.topTen)Row(p,p.id==match.LocalActorId,true);
            bool ownListed=false;foreach(var row in f.topTen)if(own!=null&&row.id==own.id)ownListed=true;
            if(own!=null&&!ownListed)Row(own,true,false);
            if(f.secondaryLeaders!=null&&f.secondaryLeaders.Length>0)
            {
                text.Append("\n").Append(f.secondaryMetricLabel).Append('\n');
                foreach(var row in f.secondaryLeaders)Row(row,row.id==match.LocalActorId,true);
            }
            board.text=text.ToString();text.Clear();
            foreach(var t in f.teams){if(text.Length>0)text.Append("     ");text.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(t.color)).Append('>').Append(t.name).Append("</color>  ").Append(f.rules.mode==GameModeKind.CaptureFlag?t.captures+" captures":f.rules.mode==GameModeKind.TeamTickets?t.tickets+" respawns":t.points.ToString("0")+" pts · "+t.ownedBlocks+" blocks");}
            teams.text=text.ToString();
            status.text=f.phase==RoundPhase.Victory?f.winner+" WINS!":f.phase==RoundPhase.Results?"ROUND RESULTS":f.rules.mode+(f.remaining>=0?"  ·  "+Mathf.CeilToInt(f.remaining)+"s":"  ·  No time limit");
            warning.text=string.IsNullOrEmpty(f.disasterWarning)?"":"WARNING\n"+f.disasterWarning+" in "+Mathf.CeilToInt(f.disasterWarningRemaining)+"s";
            if(own!=null)
            {
                if(f.rules.mode==GameModeKind.KingOfHill){float distance=Vector3.Distance(own.position,f.hill);status.text+="\n"+(own.alive&&distance<=f.rules.hillRadius?"HILL SCORING":"REACH THE HILL  ·  "+Mathf.RoundToInt(distance)+"m");}

                if(!string.IsNullOrEmpty(own.spectating)){MatchPlayerFrame watched=null;foreach(var p in f.players)if(p.id==own.spectating){watched=p;break;}status.text="WATCHING "+(watched?.name??"the build")+"  ·  ← / → switch";}
                else if(own.watching>0)status.text+="\n◉ "+own.watching+" watching you";
                bool show=f.rules.mode==GameModeKind.BlockControl;claim.enabled=show;claimFill.enabled=show;
                if(show)
                {
                    BlockClaimFrame standing=null;foreach(var entry in f.claims)if(entry.block==own.standingBlock){standing=entry;break;}
                    float progress=standing?.progress??0;int claimant=standing?.claimant??-1;
                    bool changing=standing!=null&&progress>0&&claimant!=standing.owner;
                    claim.text=own.standingBlock<0?"STAND ON A BLOCK TO CLAIM":changing?(claimant<0?"CONTESTED":claimant==own.team?"CLAIMING  "+Mathf.RoundToInt(progress*100)+"%":"ENEMY CLAIMING  "+Mathf.RoundToInt(progress*100)+"%"):own.blockOwner==own.team?"YOUR TEAM'S BLOCK":own.blockOwner<0?"UNCLAIMED BLOCK":"ENEMY BLOCK";
                    claim.color=new Color(1,1,1,own.standingBlock<0||own.blockOwner==own.team&&!changing?.4f:1);claimFill.color=claimant>=0?TeamColor(f,claimant):new Color(.9f,.8f,.55f);claimFill.rectTransform.sizeDelta=new Vector2(changing?320*Mathf.Clamp01(progress):0,6);
                    if(own.standingBlock>=0&&own.blockOwner==own.team&&previousOwners.TryGetValue(own.standingBlock,out int oldOwner)&&oldOwner!=own.team)
                    {DeadlyJenga.CandyAudio.CandyFeedback.Pulse(.25f);var audio=DeadlyJenga.CandyAudio.CandyAudioEngine.Instance;if(audio)audio.Play(DeadlyJenga.CandyAudio.CandySound.ClaimWin,own.position,.35f,GetInstanceID(),1,true);}
                }
                if(f.rules.mode!=GameModeKind.BlockControl)previousOwners.Clear();else foreach(var entry in f.claims)previousOwners[entry.block]=entry.owner;
                if(session.Player!=null&&match.RulesFor(session.Player.gameObject).sinking)DeadlyJenga.CandyAudio.CandyFeedback.Pulse(.12f,.25f);
            }
            Markers(f,own);
            Nameplates(f,match.LocalActorId);
        }
        void UpdateHealthBar(MatchFrame frame,string local)
        {
            MatchPlayerFrame own=null;foreach(var player in frame.players)if(player.id==local){own=player;break;}
            bool available=frame.rules.healthEnabled&&own!=null&&own.role!="hand"&&own.role!="spectator"&&own.alive&&!own.dying;
            float target=available?Mathf.Clamp01(own.health):1;
            if(target<.9999f)fullHealthAt=float.PositiveInfinity;
            else if(lastHealth<.9999f)fullHealthAt=Time.unscaledTime;
            shownHealth=Mathf.MoveTowards(shownHealth,target,Time.unscaledDeltaTime*3);lastHealth=target;
            bool visible=available&&(target<.9999f||Time.unscaledTime-fullHealthAt<5);
            healthGroup.alpha=Mathf.MoveTowards(healthGroup.alpha,visible?1:0,Time.unscaledDeltaTime*4);
            healthFill.rectTransform.sizeDelta=new Vector2(320*shownHealth,11);
            if(own!=null)healthFill.color=Color.Lerp(own.color,Color.white,.18f);
            healthText.text="HEALTH  "+Mathf.CeilToInt(target*100)+"%";
        }
        void Row(MatchPlayerFrame p,bool own,bool numbered)
        {if(own)text.Append("<color=#72FFD6>▶</color> ");text.Append(numbered?p.rank.ToString()+". ":"   ").Append(CandyHudLayout.PlayerName(p)).Append("  ").Append(p.metric.ToString("0.0"));text.Append('\n');}
        void Configure()
        {
            CandyHudLayout.Configure(canvas,transform,camera,vr);
        }
        void Nameplates(MatchFrame f,string local)
        {
            active.Clear();if(!camera)return;int shown=0;
            bool teamMode=f.rules.mode==GameModeKind.BlockControl||f.rules.mode==GameModeKind.CaptureFlag||f.rules.mode==GameModeKind.TeamTickets;
            bool competitive=!teamMode&&f.rules.mode!=GameModeKind.Demo&&f.rules.punchEnabled;
            foreach(var p in f.players)
            {
                if(p.id==local||!p.alive||shown>=40)continue;
                var point=p.viewPosition+Vector3.up*.28f;
                var screen=camera.WorldToViewportPoint(point);
                if(screen.z<=0||screen.x<0||screen.x>1||screen.y<0||screen.y>1)continue;
                float distance=Vector3.Distance(camera.transform.position,p.viewPosition);
                if(competitive&&distance>15||teamMode&&distance>65)continue;
                bool visible=NameVisible(p.viewPosition,MatchRuntime.Current.FindParticipantRoot(p.id));
                if(visible)plateSeen[p.id]=Time.unscaledTime;
                if(!teamMode&&!visible&&(competitive||!plateSeen.TryGetValue(p.id,out float seen)||Time.unscaledTime-seen>2))continue;
                shown++;active.Add(p.id);
                if(!plates.TryGetValue(p.id,out var label)||!label){label=CandyNameplate.Create(transform,"Visual - nameplate");plates[p.id]=label;}
                label.text=p.name;label.color=teamMode?p.color:Color.white;label.transform.position=point;label.transform.rotation=Quaternion.LookRotation(label.transform.position-camera.transform.position);label.gameObject.SetActive(true);
                CandyNameplate.RefreshShadow(label);
            }
            remove.Clear();foreach(var pair in plates)if(!active.Contains(pair.Key)){if(pair.Value)pair.Value.gameObject.SetActive(false);if(!Contains(f.players,pair.Key))remove.Add(pair.Key);}
            foreach(var id in remove){if(plates[id])Destroy(plates[id].gameObject);plates.Remove(id);plateSeen.Remove(id);}
        }
        bool NameVisible(Vector3 point,Transform target)
        {
            Vector3 delta=point-camera.transform.position;float distance=delta.magnitude;if(distance<.05f)return true;
            int count=Physics.RaycastNonAlloc(camera.transform.position,delta/distance,plateHits,distance,~(1<<Layers.Survivor),QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++){var hit=plateHits[i].collider.transform;if(target&&(hit==target||hit.IsChildOf(target)))continue;return false;}
            return count<plateHits.Length;
        }
        static bool Contains(MatchPlayerFrame[] players,string id){foreach(var p in players)if(p.id==id)return true;return false;}
        void Markers(MatchFrame f,MatchPlayerFrame own)
        {
            markerCount=0;if(own!=null&&camera)
            {
                if(f.rules.mode==GameModeKind.KingOfHill)Marker(f.hill,new Color(1,.78f,.15f),"HILL");
                foreach(var flag in f.flags)
                {
                    bool dropped=!flag.atHome&&string.IsNullOrEmpty(flag.carrier),near=(flag.position-own.position).sqrMagnitude<9;
                    if(flag.team==own.team&&flag.atHome||dropped||near)Marker(flag.position,TeamColor(f,flag.team),"⚑ "+(flag.team==own.team?"YOUR FLAG":"FLAG"));
                    if(near&&flag.team!=own.team&&Time.unscaledTime>flagPulseAt){DeadlyJenga.CandyAudio.CandyFeedback.Pulse(.13f);flagPulseAt=Time.unscaledTime+.6f;}
                }
                if(f.rules.mode==GameModeKind.Infection)foreach(var p in f.players)
                    if(p.id!=own.id&&p.alive&&(own.role!="infected"&&p.role=="infected"||own.role=="infected"&&f.survivorsLeft<=2&&p.role!="infected"))Marker(p.viewPosition,p.color,"◉ "+p.name);
            }
            for(int i=markerCount;i<markers.Count;i++)markers[i].enabled=false;
        }
        static Color TeamColor(MatchFrame f,int id){foreach(var team in f.teams)if(team.id==id)return team.color;return Color.white;}
        void Marker(Vector3 world,Color color,string name)
        {
            if(markerCount>=24)return;Vector3 p=camera.WorldToViewportPoint(world);if(p.z<0){p.x=1-p.x;p.y=.08f;}
            Text label;if(markerCount==markers.Count){label=Label("Objective blip",new Vector2(210,36),Vector2.zero,TextAnchor.MiddleCenter,15);markers.Add(label);}else label=markers[markerCount];markerCount++;
            label.enabled=true;label.color=color;label.text=name+"  "+Mathf.RoundToInt(Vector3.Distance(camera.transform.position,world))+"m";label.rectTransform.anchoredPosition=CandyHudLayout.MarkerPosition(p,label.rectTransform.sizeDelta);
        }
        void OnDestroy(){if(canvas)Destroy(canvas.gameObject);foreach(var p in plates.Values)if(p)Destroy(p.gameObject);}
    }
}
