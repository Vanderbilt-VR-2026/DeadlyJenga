using System.Collections.Generic;
using System.Text;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace DeadlyJenga.Presentation
{
    // One canvas for desktop or headset; receives only host-authored snapshot values.
    public sealed class CandyMatchHud : MonoBehaviour
    {
        Canvas canvas;RectTransform root;Text board,teams,claim,status,warning,objective;Image claimFill;Camera camera;bool vr;float sampleAt;
        CandyWaypointLayer waypoints;int boardPage;
        InputAction hudSelect;
        void OnEnable()=>hudSelect=CandyHudContent.CreateSelectInput();
        void OnDisable(){hudSelect?.Dispose();hudSelect=null;Expanded=false;}
        public static bool ScoreboardPinned;
        public static int FlagFocusIndex;
        public static bool Expanded {get;private set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBoard(){ScoreboardPinned=false;Expanded=false;FlagFocusIndex=0;}
        CanvasGroup healthGroup;Image healthFill;Text healthText;
        float shownHealth=1,lastHealth=1,fullHealthAt=-100;
        readonly StringBuilder text=new StringBuilder(512);
        readonly Dictionary<string,TextMesh> plates=new Dictionary<string,TextMesh>();
        readonly Dictionary<string,float> plateSeen=new Dictionary<string,float>();
        readonly RaycastHit[] plateHits=new RaycastHit[16];
        readonly HashSet<string> active=new HashSet<string>();readonly List<string> remove=new List<string>();
        readonly Dictionary<int,int> previousOwners=new Dictionary<int,int>();float flagPulseAt;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=Attach;
        static void Attach(GameObject go){if(!go.GetComponent<CandyMatchHud>())go.AddComponent<CandyMatchHud>();if(!go.GetComponent<CandyMatchVisuals>())go.AddComponent<CandyMatchVisuals>();if(!go.GetComponent<CandyObjectives>())go.AddComponent<CandyObjectives>();if(!go.GetComponent<CandySeaSpirit>())go.AddComponent<CandySeaSpirit>();}
        void Build()
        {
            var go=new GameObject("Visual - match telemetry",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.sortingOrder=26;root=go.GetComponent<RectTransform>();
            var scale=go.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);
            board=CandyHudCards.Create(root,"Standings",new Vector2(438,88),new Vector2(350,350),20);
            teams=CandyHudCards.Create(root,"Team scores",new Vector2(0,286),new Vector2(440,46),19,TextAnchor.MiddleCenter);
            status=CandyHudCards.Create(root,"Round and spectators",new Vector2(0,354),new Vector2(440,68),20,TextAnchor.MiddleCenter);
            warning=CandyHudCards.Create(root,"Disaster warning",new Vector2(0,214),new Vector2(440,70),20,TextAnchor.MiddleCenter);warning.color=CandyHudVisuals.Peach;
            objective=CandyHudCards.Create(root,"Your objective",new Vector2(-420,215),new Vector2(380,190),19);
            waypoints=new CandyWaypointLayer(root);
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
            bool heldBoard=match.Rules.mode==GameModeKind.Demo?Keyboard.current?.f2Key.isPressed==true:Keyboard.current?.tabKey.isPressed==true;
            Expanded=!watching&&(ScoreboardPinned||!vr&&heldBoard);
            if(Expanded&&!session.MenuOpen&&Keyboard.current!=null)
            {if(Keyboard.current.pageDownKey.wasPressedThisFrame)boardPage++;if(Keyboard.current.pageUpKey.wasPressedThisFrame)boardPage=Mathf.Max(0,boardPage-1);}
            if(Expanded&&vr&&!session.MenuOpen&&CandyHudContent.SelectPressed(hudSelect))boardPage=(boardPage+1)%Mathf.Max(1,Mathf.CeilToInt(match.Frame.players.Length/(match.Frame.teams.Length>2?6f:10f)));
            if(!session.MenuOpen&&!Expanded&&!vr&&match.Rules.mode==GameModeKind.CaptureFlag&&Keyboard.current?.gKey.wasPressedThisFrame==true)FlagFocusIndex++;
            canvas.enabled=camera&&!session.MenuOpen&&(!watching||vr);
            if(watching)
            {
                board.text=teams.text=claim.text=warning.text="";claimFill.enabled=false;
                status.text=vr?"WATCHING "+spectator.WatchedName(0)+"\nRight stick switches players":"";
                foreach(var plate in plates.Values)if(plate)plate.gameObject.SetActive(false);
                waypoints.Begin(false);waypoints.End();CandyHudCards.Visible(board,false);CandyHudCards.Visible(teams,false);CandyHudCards.Visible(warning,false);CandyHudCards.Visible(objective,false);
                return;
            }
            UpdateHealthBar(match.Frame,match.LocalActorId);
            healthGroup.gameObject.SetActive(!Expanded);
            if(Time.unscaledTime<sampleAt)return;sampleAt=Time.unscaledTime+.2f;
            var f=match.Frame;MatchPlayerFrame own=null;foreach(var p in f.players)if(p.id==match.LocalActorId){own=p;break;}
            UpdateBoard(f,own);text.Clear();
            foreach(var t in f.teams.OrderBy(t=>t.id==own?.team?0:1).Take(2)){if(text.Length>0)text.Append("   |   ");text.Append(CandyHudContent.Safe(t.name,f.teams.Length>2?6:14)).Append(t.id==own?.team?" (YOU) ":" ").Append(f.rules.mode==GameModeKind.CaptureFlag?t.captures+(f.teams.Length>2?"":" captures"):f.rules.mode==GameModeKind.TeamTickets?t.tickets+(f.teams.Length>2?"":" respawns"):t.points.ToString("0")+" pts");}
            if(f.teams.Length>2)text.Append(" · +").Append(f.teams.Length-2).Append(" teams");
            teams.text=text.ToString();
            status.text=f.phase==RoundPhase.Victory?CandyHudContent.Safe(f.winner)+" WINS!":f.phase==RoundPhase.Results?"ROUND RESULTS":CandyHudContent.Mode(f.rules.mode)+"  ·  "+(f.phase==RoundPhase.Countdown?"STARTING · ":"")+CandyHudContent.Clock(f.remaining);
            if(f.phase==RoundPhase.Playing&&(f.rules.mode==GameModeKind.Survival||f.rules.mode==GameModeKind.Infection))status.text+="\n"+f.survivorsLeft+" SURVIVORS REMAIN";
            warning.text=string.IsNullOrEmpty(f.disasterWarning)?"":"WARNING\n"+f.disasterWarning+" in "+Mathf.CeilToInt(f.disasterWarningRemaining)+"s";
            CandyHudCards.Visible(board,Expanded||CandyHudContent.LiveStandings(f.rules.mode));CandyHudCards.Visible(teams,!Expanded&&f.teams.Length>0);CandyHudCards.Visible(status,!Expanded);CandyHudCards.Visible(warning,!Expanded&&!string.IsNullOrEmpty(warning.text));
            UpdateObjective(f,own);CandyHudCards.Visible(objective,!Expanded&&f.rules.mode!=GameModeKind.MurderMystery&&own!=null);
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
            if(!Expanded)Markers(f,own);else waypoints.Begin(false);
            waypoints.End();
            if(Expanded){claim.enabled=false;claimFill.enabled=false;}
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
        void Row(MatchPlayerFrame p,bool own,int rank,bool whole)
        {if(own)text.Append("YOU · ");text.Append(rank).Append(". ").Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(p.color)).Append(">●</color> ").Append(CandyHudContent.Safe(p.name,22)).Append("  ").Append(p.metric.ToString(whole?"0":"0.0"));text.Append('\n');}
        void UpdateBoard(MatchFrame f,MatchPlayerFrame own)
        {
            bool mystery=f.rules.mode==GameModeKind.MurderMystery;
            bool statusRoster=(f.rules.mode==GameModeKind.Survival||f.rules.mode==GameModeKind.Infection)&&f.rules.metric==ScoreMetric.SurvivalTime;
            // Snapshots only number the top ten. Order the remaining public scores the same way.
            var rows=mystery?f.players.OrderBy(p=>p.name).ToArray():f.players.OrderByDescending(p=>p.metric).ThenBy(p=>p.id,System.StringComparer.Ordinal).ToArray();
            bool whole=f.rules.metric==ScoreMetric.TeamPoints||f.rules.metric==ScoreMetric.BlocksOwned||f.rules.metric==ScoreMetric.InfectedKills||f.rules.metric==ScoreMetric.Eliminations||f.rules.metric==ScoreMetric.FlagCaptures||f.rules.metric==ScoreMetric.BlocksClaimed||f.rules.metric==ScoreMetric.AliveStatus;
            int pageSize=f.teams.Length>2?6:10;
            int pages=Mathf.Max(1,Mathf.CeilToInt(rows.Length/(float)pageSize));boardPage=Mathf.Clamp(boardPage,0,pages-1);
            CandyHudCards.Place(board,Expanded?new Vector2(0,10):new Vector2(438,88),Expanded?new Vector2(760,580):new Vector2(350,350));board.fontSize=Expanded?23:19;
            text.Clear();text.Append(mystery?"PLAYER ROSTER · ROLES PRIVATE":statusRoster?"PARTICIPANTS · CURRENT STATUS":"STANDINGS · "+CandyHudContent.Safe(f.metricLabel,28)+(f.rules.metric==ScoreMetric.TeamPoints?" (team)":"")).Append("\n\n");
            if(Expanded&&f.teams.Length>2)text.Append("TEAM SCORES\n").Append(string.Join(" | ",f.teams.Select(t=>CandyHudContent.Safe(t.name,10)+" "+(f.rules.mode==GameModeKind.CaptureFlag?t.captures:f.rules.mode==GameModeKind.TeamTickets?t.tickets:Mathf.RoundToInt(t.points))))).Append("\n\n");
            var shown=Expanded?rows.Skip(boardPage*pageSize).Take(pageSize).ToArray():rows.Take(5).ToArray();
            foreach(var p in shown)
            {
                if(mystery)text.Append(p.id==own?.id?"YOU · ":"").Append(CandyHudContent.Safe(p.name,26)).Append(f.mystery.bodies.Any(b=>b.reported&&b.id==p.id)?" · REPORTED":"").Append('\n');
                else if(statusRoster||f.rules.metric==ScoreMetric.AliveStatus)text.Append(p.id==own?.id?"YOU · ":"").Append(CandyHudContent.Safe(p.name,22)).Append(p.role=="infected"?" · INFECTED":p.alive&&!p.dying?" · ACTIVE":" · ELIMINATED").Append('\n');
                else Row(p,p.id==own?.id,System.Array.IndexOf(rows,p)+1,whole);
            }
            if(own!=null&&!shown.Any(p=>p.id==own.id)){text.Append("\nYOUR POSITION\n");if(mystery||statusRoster||f.rules.metric==ScoreMetric.AliveStatus)text.Append(CandyHudContent.Safe(own.name)).Append('\n');else Row(own,true,System.Array.IndexOf(rows,own)+1,whole);}
            if(f.rules.mode==GameModeKind.Infection&&!mystery&&f.secondaryLeaders.Length>0)text.Append("\nInfections leader: ").Append(CandyHudContent.Safe(f.secondaryLeaders[0].name,20)).Append(" · ").Append(f.secondaryLeaders[0].infections).Append('\n');
            string boardKey=f.rules.mode==GameModeKind.Demo?"F2":"TAB";
            text.Append(Expanded?"\nPAGE "+(boardPage+1)+" / "+pages+(vr?" · Trigger / pinch: next page\n":" · PgUp / PgDn\n"):"\n").Append(vr?"Show / hide full standings in Modes menu":Expanded?ScoreboardPinned?"Pinned · Hide in Modes menu":"Release "+boardKey+" to close":"Hold "+boardKey+" for full standings");board.text=text.ToString();
            if(!Expanded){float height=Mathf.Clamp(board.preferredHeight+20,180,350);CandyHudCards.Place(board,new Vector2(438,263-height/2),new Vector2(350,height));}
        }
        void UpdateObjective(MatchFrame f,MatchPlayerFrame own)
        {
            if(own==null)return;
            CandyHudCards.Place(objective,new Vector2(-420,f.rules.mode==GameModeKind.CaptureFlag?203:215),new Vector2(380,f.rules.mode==GameModeKind.CaptureFlag?214:190));
            text.Clear();text.Append(f.rules.mode==GameModeKind.CaptureFlag&&f.flags.Length>2?(vr?"FLAG FOCUS · Modes menu\n":"FLAG FOCUS · G: NEXT ENEMY\n"):"YOUR OBJECTIVE\n");
            if(f.rules.mode==GameModeKind.CaptureFlag)
            {
                var focus=CandyHudContent.FocusedFlag(f,own,FlagFocusIndex);
                foreach(var flag in f.flags.Where(flag=>flag.team==own.team||flag==focus)){text.Append(flag.team==own.team?"YOUR FLAG":"FLAG · "+CandyHudContent.Team(f,flag.team)).Append("\n").Append(CandyHudContent.FlagStatus(f,flag,own.id)).Append('\n');}
                var carrying=f.flags.FirstOrDefault(flag=>flag.carrier==own.id);
                text.Append(carrying!=null?"Bring it to your base. Your flag must be home.":"Take an enemy flag to your base.");
                text.Append("\nFirst to ").Append(f.rules.flagTarget).Append(" captures").Append(f.rules.flagWinByTwo?" · win by 2":"");
            }
            else if(f.rules.mode==GameModeKind.KingOfHill)text.Append("Stand inside the hill to score.\nThe highest safe region can move.");
            else if(f.rules.mode==GameModeKind.BlockControl)text.Append("Stand on blocks to claim them.\nYour team earns points while it owns them.");
            else if(f.rules.mode==GameModeKind.TeamTickets)text.Append("Each team shares its respawns.\nEliminate rivals; protect your remaining lives.");
            else if(f.rules.mode==GameModeKind.Infection)text.Append(own.role=="infected"?"Tag survivors with a punch.\nInfect everyone before time runs out.":"Avoid infected players.\nEliminated survivors join the infected.");
            else if(f.rules.mode==GameModeKind.Survival)text.Append("Stay above the ocean.\nOutlast the other survivors.");
            else text.Append("Explore, build and try the sandbox tools.");
            objective.text=text.ToString();
        }
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
            waypoints.Begin(false);
            if(own!=null&&camera)
            {
                var focus=CandyHudContent.FocusedFlag(f,own,FlagFocusIndex);
                if(f.rules.mode==GameModeKind.KingOfHill)Marker(f.hill,new Color(1,.78f,.15f),"HILL");
                foreach(var flag in f.flags)
                {
                    if(f.rules.mode==GameModeKind.CaptureFlag&&flag.team!=own.team&&flag!=focus)continue;
                    bool near=(flag.position-own.position).sqrMagnitude<9;
                    if(f.rules.mode==GameModeKind.CaptureFlag&&flag.carrier!=own.id)Marker(flag.position+Vector3.up*.8f,TeamColor(f,flag.team),CandyHudContent.FlagTask(f,flag,own));
                    if(flag.carrier==own.id){var home=f.flags.FirstOrDefault(x=>x.team==own.team);if(home!=null)Marker(home.home,TeamColor(f,own.team),home.atHome?"DELIVER HERE":"BASE · FLAG NOT HOME");}
                    if(near&&flag.team!=own.team&&Time.unscaledTime>flagPulseAt){DeadlyJenga.CandyAudio.CandyFeedback.Pulse(.13f);flagPulseAt=Time.unscaledTime+.6f;}
                }
                if(f.rules.mode==GameModeKind.Infection)foreach(var p in f.players)
                    if(p.id!=own.id&&p.alive&&(own.role!="infected"&&p.role=="infected"||own.role=="infected"&&f.survivorsLeft<=2&&p.role!="infected"))Marker(p.viewPosition,p.color,"◉ "+p.name);
            }
        }
        static Color TeamColor(MatchFrame f,int id){foreach(var team in f.teams)if(team.id==id)return team.color;return Color.white;}
        void Marker(Vector3 world,Color color,string name)
        {
            waypoints.Show(camera,world,name,color);
        }
        void OnDestroy(){if(canvas)Destroy(canvas.gameObject);foreach(var p in plates.Values)if(p)Destroy(p.gameObject);}
    }
}
