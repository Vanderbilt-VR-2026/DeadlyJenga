using System.Collections.Generic;
using System.Text;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

namespace DeadlyJenga.Presentation
{
    // One canvas for desktop or headset; receives only host-authored snapshot values.
    public sealed class CandyMatchHud : MonoBehaviour
    {
        Canvas canvas;RectTransform root;Text board,teams,claim,status,objective;Image claimFill;Camera camera;bool vr;float sampleAt, objectiveUntil;RoundPhase lastPhase=(RoundPhase)(-1);GameModeKind lastMode=(GameModeKind)(-1);
        CandyWaypointLayer waypoints;int boardPage;
        CandyEliminationFeed eliminationFeed;
        CandyDisasterWarningCard warning;
        Text sinkWarning;
        Image sinkMeter;
        bool sinkUrgent;
        float nextSinkPulse, sinkEscapeUntil;
        string sinkActor;
        int sinkSeconds = -1;
        string sinkHint;
        const int BoardPageSize = 6;
        sealed class StandingsRow
        {
            public Image card;
            public Text rank, name, stats, initial;
            public RawImage portrait;
        }
        readonly StandingsRow[] standingsRows = new StandingsRow[BoardPageSize];
        CandyScorePortraits scorePortraits;
        CandyScorePodium podium;
        Text standingsFooter;
        Text standingsHint;
        string standingsControl;
        float nextBoardPageAt;
        void OnDisable(){Expanded=false;podium?.Hide();}
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
            teams=CandyHudCards.Create(root,"Team scores",new Vector2(0,311),new Vector2(384,36),17,TextAnchor.MiddleCenter);
            status=CandyHudCards.Create(root,"Round and spectators",new Vector2(0,362),new Vector2(334,56),17,TextAnchor.MiddleCenter);
            warning=new CandyDisasterWarningCard(root);
            sinkWarning = CandyHudCards.Create(root, "Sinking escape warning", new Vector2(0, 130),
                new Vector2(480, 74), 20, TextAnchor.MiddleCenter);
            CandyHudCards.Badge(sinkWarning);
            var sinkTrack = new GameObject("Sink countdown", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            sinkTrack.transform.SetParent(sinkWarning.transform.parent, false);
            sinkTrack.rectTransform.anchoredPosition = new Vector2(0, -31);
            sinkTrack.rectTransform.sizeDelta = new Vector2(410, 5);
            sinkTrack.color = new Color(.19f, .08f, .13f, .35f);
            sinkTrack.raycastTarget = false;
            sinkMeter = new GameObject("Time remaining", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            sinkMeter.transform.SetParent(sinkTrack.transform, false);
            sinkMeter.rectTransform.pivot = new Vector2(0, .5f);
            sinkMeter.rectTransform.anchoredPosition = new Vector2(-205, 0);
            sinkMeter.raycastTarget = false;
            CandyHudCards.Visible(sinkWarning, false);
            objective=CandyHudCards.Create(root,"Your objective",new Vector2(-420,215),new Vector2(380,190),19);
            CandyHudCards.Badge(status);CandyHudCards.Tint(status,CandyHudVisuals.Lavender,.29f);
            CandyHudCards.Badge(teams);CandyHudCards.Tint(teams,CandyHudVisuals.Mint,.31f);
            CandyHudCards.Emblem(objective,"flag",new Color(.79f,.69f,1));
            waypoints=new CandyWaypointLayer(root);
            standingsHint=Label("Standings shortcut",new Vector2(400,28),new Vector2(396,348),TextAnchor.MiddleRight,15);
            scorePortraits = CandyScorePortraits.For(gameObject);
            eliminationFeed = new CandyEliminationFeed(root, scorePortraits);
            BuildStandingsRows();
            claim=Label("Block ownership",new Vector2(400,36),new Vector2(0,-246),TextAnchor.MiddleCenter,17);
            var fill=new GameObject("Claim juice",typeof(RectTransform),typeof(Image));fill.transform.SetParent(root,false);claimFill=fill.GetComponent<Image>();claimFill.color=new Color(.25f,1,.8f,.8f);claimFill.raycastTarget=false;claimFill.rectTransform.anchoredPosition=new Vector2(-160,-268);claimFill.rectTransform.pivot=new Vector2(0,.5f);claimFill.rectTransform.sizeDelta=new Vector2(0,6);
            var health=new GameObject("Health bar",typeof(RectTransform),typeof(CanvasGroup));health.transform.SetParent(root,false);
            var rect=health.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(230,42);rect.anchoredPosition=new Vector2(0,-278);
            healthGroup=health.GetComponent<CanvasGroup>();healthGroup.alpha=0;healthGroup.blocksRaycasts=false;healthGroup.interactable=false;
            var track=new GameObject("Candy health track",typeof(RectTransform),typeof(Image));track.transform.SetParent(health.transform,false);
            var back=track.GetComponent<Image>();back.sprite=CandyHudVisuals.Pill;back.type=Image.Type.Sliced;back.color=new Color(.055f,.02f,.1f,.9f);back.raycastTarget=false;back.rectTransform.sizeDelta=new Vector2(230,12);
            var juice=new GameObject("Health juice",typeof(RectTransform),typeof(Image));juice.transform.SetParent(track.transform,false);healthFill=juice.GetComponent<Image>();healthFill.sprite=CandyHudVisuals.Pill;healthFill.type=Image.Type.Sliced;healthFill.raycastTarget=false;healthFill.rectTransform.pivot=new Vector2(0,.5f);healthFill.rectTransform.anchoredPosition=new Vector2(-110,0);healthFill.rectTransform.sizeDelta=new Vector2(220,6);
            healthText=Label("Health",new Vector2(230,23),new Vector2(0,18),TextAnchor.MiddleCenter,15);healthText.transform.SetParent(health.transform,false);
        }
        Text Label(string name,Vector2 size,Vector2 position,TextAnchor alignment,int fontSize)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text),typeof(Outline));go.transform.SetParent(root,false);var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=fontSize;t.fontStyle=FontStyle.Bold;t.alignment=alignment;t.color=Color.white;t.supportRichText=true;t.raycastTarget=false;t.rectTransform.sizeDelta=size;t.rectTransform.anchoredPosition=position;go.GetComponent<Outline>().effectColor=new Color(.08f,.025f,.14f,.85f);return t;
        }
        // All sources are replicated presentation views or host snapshots; no local scheduling.
        internal static string IncomingDisaster(MatchFrame frame,out float seconds)
        {
            string name=null;float next=float.PositiveInfinity;
            void Offer(string label,float remaining)
            {
                if(!string.IsNullOrWhiteSpace(label)&&remaining>0&&remaining<next){name=label;next=remaining;}
            }
            Offer(frame?.disasterWarning,frame?.disasterWarningRemaining??0);
            foreach(var view in Tracked.Waves)
            {
                if(view==null||view is Object obj&&!obj)continue;
                var wave=view.Wave;
                if(wave.active)Offer("Tsunami",wave.warningRemaining);
            }
            foreach(var view in DisasterStates.Active)
            {
                if(view is Object obj&&!obj)continue;
                var disaster=view?.Capture();
                if(disaster!=null&&!disaster.residualOnly)Offer(disaster.label,disaster.warning);
            }
            seconds=name!=null?next:0;return name;
        }
        IMatchRuntime soundMatch;
        RoundPhase soundPhase;
        int soundTeam=-1, soundCaptures;
        bool soundSinking;
        void ObserveMatchSound(IMatchRuntime match,MatchFrame frame,MatchPlayerFrame own)
        {
            var team=own==null?null:System.Array.Find(frame.teams,row=>row.id==own.team);
            bool same=ReferenceEquals(soundMatch,match);
            if(same&&soundPhase!=frame.phase&&frame.phase==RoundPhase.Results)
                DeadlyJenga.CandyAudio.CandySoundSuite.Local(DeadlyJenga.CandyAudio.CandySound.RoundResults);
            if(same&&soundPhase==RoundPhase.Playing&&frame.phase==RoundPhase.Playing&&team!=null&&soundTeam==team.id&&team.captures>soundCaptures)
                DeadlyJenga.CandyAudio.CandySoundSuite.Local(DeadlyJenga.CandyAudio.CandySound.PointScored);
            if (!same) ResetSinkFeedback();
            soundMatch=match;soundPhase=frame.phase;soundTeam=team?.id??-1;
            soundCaptures=team?.captures??0;
        }

        void ResetSinkFeedback()
        {
            soundSinking = sinkUrgent = false;
            nextSinkPulse = sinkEscapeUntil = 0;
            sinkSeconds = -1;
            sinkHint = null;
            if (sinkWarning) CandyHudCards.Visible(sinkWarning, false);
        }

        void UpdateSinkFeedback(IMatchRuntime match, MatchPlayerFrame own, ISessionInfo session, bool watching)
        {
            if (sinkActor != own?.id) { ResetSinkFeedback(); sinkActor = own?.id; }
            var player = session.Player;
            if (!canvas.enabled || watching || session.PlayingHand || player == null || player.InputSuspended ||
                match.Frame.phase != RoundPhase.Playing || own == null || !own.alive || own.dying)
            {
                ResetSinkFeedback();
                return;
            }

            var movement = player.gameObject.GetComponent<IMovementState>();
            bool escaping = movement != null && (movement.Climbing || movement.Vaulting);
            bool sinking = own.sinking && !escaping;
            float now = Time.unscaledTime;
            if (!sinking)
            {
                if (soundSinking && (escaping || movement?.Grounded == true))
                {
                    sinkEscapeUntil = now + .95f;
                    CandySoundSuite.Local(CandySound.StaminaUsable);
                    if (vr) CandyFeedback.Pulse(.18f, .07f);
                }
                soundSinking = sinkUrgent = false;
                sinkSeconds = -1;
                bool recovered = now < sinkEscapeUntil && (escaping || movement?.Grounded == true);
                if (recovered)
                {
                    sinkWarning.text = movement?.Climbing == true ? "GRIP CAUGHT · KEEP CLIMBING" :
                        movement?.Vaulting == true ? "VAULTING OUT" : "CLEAR OF THE SINKING SAND";
                    CandyHudCards.Tint(sinkWarning, CandyHudVisuals.Mint, .64f);
                    sinkMeter.rectTransform.sizeDelta = new Vector2(0, 5);
                }
                CandyHudCards.Visible(sinkWarning, recovered);
                return;
            }

            float progress = Mathf.Clamp01(own.sinkProgress);
            if (!soundSinking)
            {
                CandySoundSuite.Local(CandySound.IslandSinking);
                nextSinkPulse = now;
                sinkUrgent = progress >= .68f;
            }
            else if (!sinkUrgent && progress >= .68f)
            {
                sinkUrgent = true;
                CandyAudioEngine.Instance?.Play(CandySound.IslandSinking, Vector3.zero, .72f, GetInstanceID(), 1.23f, true);
                nextSinkPulse = now;
            }
            soundSinking = true;
            if (vr && now >= nextSinkPulse)
            {
                CandyFeedback.Pulse(Mathf.Lerp(.20f, .48f, progress), .10f);
                nextSinkPulse = now + Mathf.Lerp(.90f, .28f, progress);
            }

            var rules = match.RulesFor(player.gameObject);
            int seconds = Mathf.CeilToInt(Mathf.Max(0, rules.sinkGraceSeconds) * (1 - progress));
            string hint = rules.canClimb && rules.canVault ? (vr ? "HOLD JUMP TO CLIMB · TAP TO VAULT" : "HOLD SPACE TO CLIMB · TAP TO VAULT") :
                rules.canVault ? (vr ? "TAP JUMP TO VAULT A LEDGE" : "TAP SPACE TO VAULT A LEDGE") :
                rules.canClimb ? (vr ? "HOLD JUMP TO CLIMB" : "HOLD SPACE TO CLIMB") : "MOVE TOWARD SOLID FOOTING";
            if (seconds != sinkSeconds || hint != sinkHint)
            {
                sinkSeconds = seconds;
                sinkHint = hint;
                sinkWarning.text = $"SINKING SAND  ·  {seconds}s\n{hint}";
            }
            var urgency = Color.Lerp(CandyHudVisuals.Peach, new Color(1, .35f, .29f), progress);
            CandyHudCards.Tint(sinkWarning, urgency, .65f);
            sinkMeter.color = Color.Lerp(new Color(.62f,.28f,.06f), new Color(.75f,.08f,.09f), progress);
            sinkMeter.rectTransform.sizeDelta = new Vector2(410 * (1 - progress), 5);
            CandyHudCards.Visible(sinkWarning, true);
        }
        void Update()
        {
            var session=Services.Session;var match=MatchRuntime.Current;
            if(session==null||match?.Frame==null||(session as ILobbyMatchVisitControls)?.VisitingLobby==true)
            {
                Expanded = false;
                soundMatch=null;ResetSinkFeedback();
                if(canvas)canvas.enabled=false;
                podium?.Hide();
                eliminationFeed?.Update(null, false);
                return;
            }
            if(!canvas)Build();if(camera!=session.ActiveCamera||vr!=session.IsVR){camera=session.ActiveCamera;vr=session.IsVR;Configure();}
            var spectator=session.Demo as ISpectatorControls;
            bool watching=spectator?.Spectating==true;
            bool results=match.Frame.phase==RoundPhase.Results;
            bool wasExpanded=Expanded;
            bool heldBoard=vr?VrStandingsHeld(out standingsControl):Keyboard.current?.f2Key.isPressed==true;
            Expanded=!watching&&!results&&!session.MenuOpen&&!EmoteWheel.IsOpen&&(ScoreboardPinned||heldBoard);
            if(Expanded&&!wasExpanded)
            {
                boardPage=0;sampleAt=0;nextBoardPageAt=Time.unscaledTime+5;
                DeadlyJenga.CandyAudio.CandySoundSuite.Local(DeadlyJenga.CandyAudio.CandySound.ScoreboardOpen);
            }
            var audioFrame=match.Frame;
            var audioPlayer=System.Array.Find(audioFrame.players,row=>row.id==match.LocalActorId);
            ObserveMatchSound(match,audioFrame,audioPlayer);
            if(wasExpanded&&!Expanded)sampleAt=0;
            CandyHudCards.Visible(board,Expanded);
            if(Expanded&&!vr&&Keyboard.current!=null)
            {
                if(Keyboard.current.pageDownKey.wasPressedThisFrame){boardPage++;sampleAt=0;}
                if(Keyboard.current.pageUpKey.wasPressedThisFrame){boardPage=Mathf.Max(0,boardPage-1);sampleAt=0;}
            }
            if(Expanded&&vr&&Time.unscaledTime>=nextBoardPageAt)
            {
                boardPage=(boardPage+1)%Mathf.Max(1,Mathf.CeilToInt(StandingsParticipants(match.Frame,session as IGameExperienceControls,out _).Count()/(float)BoardPageSize));
                sampleAt=0;
                nextBoardPageAt=Time.unscaledTime+5;
            }
            if(!session.MenuOpen&&!Expanded&&!vr&&match.Rules.mode==GameModeKind.CaptureFlag&&Keyboard.current?.gKey.wasPressedThisFrame==true)FlagFocusIndex++;
            canvas.enabled=camera&&!session.MenuOpen&&!EmoteWheel.IsOpen&&!results&&(!watching||vr);
            UpdateSinkFeedback(match, audioPlayer, session, watching);
            eliminationFeed.Update(match, canvas.enabled&&!watching&&!Expanded);
            standingsHint.enabled = !watching && !Expanded && (!vr || !string.IsNullOrEmpty(standingsControl));
            standingsHint.text = vr ? "HOLD " + standingsControl + " · STANDINGS" : "HOLD F2 · STANDINGS";
            if(watching)
            {
                podium?.Hide();
                board.text=teams.text=claim.text="";claimFill.enabled=false;
                status.text=vr?"WATCHING "+spectator.WatchedName(0)+"\nRight stick switches players":"";
                foreach(var plate in plates.Values)if(plate)plate.gameObject.SetActive(false);
                waypoints.Begin(false);waypoints.End();CandyHudCards.Visible(board,false);CandyHudCards.Visible(teams,false);warning.Hide();CandyHudCards.Visible(objective,false);
                return;
            }
            UpdateHealthBar(match.Frame,match.LocalActorId);
            healthGroup.gameObject.SetActive(!Expanded);
            if (!Expanded || !canvas.enabled) podium?.Hide();
            else podium?.Draw();
            if(Time.unscaledTime<sampleAt)return;sampleAt=Time.unscaledTime+.2f;
            var f=match.Frame;MatchPlayerFrame own=null;foreach(var p in f.players)if(p.id==match.LocalActorId){own=p;break;}
            if(lastPhase!=f.phase||lastMode!=f.rules.mode)
            {
                lastPhase=f.phase;lastMode=f.rules.mode;objectiveUntil=Time.unscaledTime+7;
            }
            if(Expanded)UpdateBoard(f,own);else if(board.text.Length>0)board.text="";
            text.Clear();
            foreach(var t in f.teams.OrderBy(t=>t.id==own?.team?0:1).Take(2)){if(text.Length>0)text.Append("   |   ");text.Append(CandyHudContent.Safe(t.name,f.teams.Length>2?6:14)).Append(t.id==own?.team?" (YOU) ":" ").Append(f.rules.mode==GameModeKind.CaptureFlag?t.captures+(f.teams.Length>2?"":" captures"):f.rules.mode==GameModeKind.TeamTickets?t.tickets+(f.teams.Length>2?"":" respawns"):t.points.ToString("0")+" pts");}
            if(f.teams.Length>2)text.Append(" · +").Append(f.teams.Length-2).Append(" teams");
            teams.text=text.ToString();
            status.text=f.phase==RoundPhase.Victory?CandyHudContent.Safe(f.winner)+" WINS!":f.phase==RoundPhase.Results?"ROUND RESULTS":CandyHudContent.Mode(f.rules.mode)+"  ·  "+(f.phase==RoundPhase.Countdown?"STARTING · ":"")+CandyHudContent.Clock(f.remaining);
            if(f.phase==RoundPhase.Playing&&(f.rules.mode==GameModeKind.Survival||f.rules.mode==GameModeKind.Infection))status.text+="\n"+f.survivorsLeft+" SURVIVORS REMAIN";
            if(!Expanded&&canvas.enabled&&f.phase==RoundPhase.Playing)
            {
                var next=IncomingDisaster(f,out float seconds);
                warning.Show(next,seconds);
            }
            else warning.Hide();
            CandyHudCards.Visible(board,Expanded);CandyHudCards.Visible(teams,!Expanded&&f.teams.Length>0);CandyHudCards.Visible(status,!Expanded);
            bool showObjective=!Expanded&&own!=null&&f.rules.mode!=GameModeKind.MurderMystery&&
                (f.rules.mode==GameModeKind.CaptureFlag||f.rules.mode!=GameModeKind.Demo&&Time.unscaledTime<objectiveUntil);
            if(showObjective)UpdateObjective(f,own);
            CandyHudCards.Visible(objective,showObjective);
            if(own!=null)
            {
                if(f.rules.mode==GameModeKind.KingOfHill){float distance=Vector3.Distance(own.position,f.hill);status.text+="\n"+(own.alive&&distance<=f.rules.hillRadius?"HILL SCORING":"REACH THE HILL  ·  "+Mathf.RoundToInt(distance)+"m");}

                if(!string.IsNullOrEmpty(own.spectating)){MatchPlayerFrame watched=null;foreach(var p in f.players)if(p.id==own.spectating){watched=p;break;}status.text="WATCHING "+(watched?.name??"the build")+"  ·  ← / → switch";}

                bool show=f.rules.mode==GameModeKind.BlockControl;claim.enabled=show;claimFill.enabled=show;
                if(show)
                {
                    BlockClaimFrame standing=null;foreach(var entry in f.claims)if(entry.block==own.standingBlock){standing=entry;break;}
                    float progress=standing?.progress??0;int claimant=standing?.claimant??-1;
                    bool changing=standing!=null&&progress>0&&claimant!=standing.owner;
                    claim.text=own.standingBlock<0?"STAND ON A BLOCK TO CLAIM":changing?(claimant<0?"CONTESTED":claimant==own.team?"CLAIMING  "+Mathf.RoundToInt(progress*100)+"%":"ENEMY CLAIMING  "+Mathf.RoundToInt(progress*100)+"%"):own.blockOwner==own.team?"YOUR TEAM'S BLOCK":own.blockOwner<0?"UNCLAIMED BLOCK":"ENEMY BLOCK";
                    claim.color=new Color(1,1,1,own.standingBlock<0||own.blockOwner==own.team&&!changing?.4f:1);claimFill.color=claimant>=0?TeamColor(f,claimant):new Color(.9f,.8f,.55f);claimFill.rectTransform.sizeDelta=new Vector2(changing?320*Mathf.Clamp01(progress):0,6);
                    if(own.standingBlock>=0&&own.blockOwner==own.team&&previousOwners.TryGetValue(own.standingBlock,out int oldOwner)&&oldOwner!=own.team)
                    {DeadlyJenga.CandyAudio.CandyFeedback.Pulse(.25f);var audio=DeadlyJenga.CandyAudio.CandyAudioEngine.Instance;if(audio)audio.Play(DeadlyJenga.CandyAudio.CandySound.PointScored,own.position,.35f,GetInstanceID(),1,true);}
                }
                if(f.rules.mode!=GameModeKind.BlockControl)previousOwners.Clear();else foreach(var entry in f.claims)previousOwners[entry.block]=entry.owner;
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
            healthFill.rectTransform.sizeDelta=new Vector2(220*shownHealth,6);
            if(own!=null)healthFill.color=Color.Lerp(own.color,Color.white,.18f);
            healthText.text="HEALTH  "+Mathf.CeilToInt(target*100)+"%";
        }
        // Movement-stick click is sprint; bare look-stick click holds the board with two controllers.
        static bool VrStandingsHeld(out string control)
        {
            bool leftTracked = false, rightTracked = false, leftHeld = false, rightHeld = false;
            bool gripHeld = false;
            foreach (var device in InputSystem.devices)
            {
                if (!(device is XRController controller) || !controller.isTracked.isPressed) continue;
                var click = controller.TryGetChildControl<ButtonControl>("thumbstickClicked")
                    ?? controller.TryGetChildControl<ButtonControl>("primary2DAxisClick")
                    ?? controller.TryGetChildControl<ButtonControl>("joystickClicked");
                if (click == null) continue;
                bool gripping = controller.TryGetChildControl<ButtonControl>("gripPressed")?.isPressed == true;
                gripHeld |= gripping;
                foreach (var usage in controller.usages)
                {
                    if (usage == CommonUsages.LeftHand) { leftTracked = true; leftHeld |= click.isPressed && !gripping; }
                    if (usage == CommonUsages.RightHand) { rightTracked = true; rightHeld |= click.isPressed && !gripping; }
                }
            }
            bool leftDominant = PlayerPrefs.GetInt("CandyBlaster.LeftHanded", 0) != 0;
            bool available = leftTracked && rightTracked;
            control = available ? leftDominant ? "LEFT LOOK STICK CLICK" : "RIGHT LOOK STICK CLICK" : "";
            return available && !gripHeld && !EmoteWheel.IsOpen && (leftDominant ? leftHeld : rightHeld);
        }

        internal static IEnumerable<MatchPlayerFrame> StandingsParticipants(MatchFrame frame,
            IGameExperienceControls controls, out bool curated)
        {
            curated = controls != null && controls.Experience == GameExperience.Game
                && (controls.GamePhase == RoomGamePhase.LoadingGame || controls.GamePhase == RoomGamePhase.Playing
                    || controls.GamePhase == RoomGamePhase.Results)
                && (frame.rules.mode == GameModeKind.KingOfHill || frame.rules.mode == GameModeKind.Infection);
            return curated ? frame.players.Where(player => player.role != "spectator") : frame.players;
        }

        internal static string StandingsCount(int count, bool curated)
            => count + (curated ? " COMPETITORS" : " PLAYERS");

        internal static string StandingsPlace(MatchPlayerFrame player, int index, bool curated)
        {
            if (index >= 0) return "YOU #" + (index + 1);
            if (curated && player?.role == "spectator") return "YOU SPECTATING";
            return "";
        }

        void BuildStandingsRows()
        {
            var parent = board.transform.parent;
            podium?.Dispose();
            podium = new CandyScorePodium(transform, parent, new Vector2(405, -12));
            for (int i = 0; i < standingsRows.Length; i++)
            {
                var go = new GameObject("Standings player", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                var card = go.GetComponent<Image>();
                card.sprite = CandyHudVisuals.Pill; card.type = Image.Type.Sliced; card.raycastTarget = false;
                card.rectTransform.sizeDelta = new Vector2(776, 74);
                card.rectTransform.anchoredPosition = new Vector2(-202, 179 - i * 79);
                var rank = Label("Rank", new Vector2(40, 50), new Vector2(-363, 0), TextAnchor.MiddleCenter, 23);
                rank.transform.SetParent(go.transform, false);
                var icon = new GameObject("Avatar portrait", typeof(RectTransform), typeof(RawImage));
                icon.transform.SetParent(go.transform, false);
                var portrait = icon.GetComponent<RawImage>(); portrait.raycastTarget = false;
                portrait.rectTransform.sizeDelta = new Vector2(70, 70);
                portrait.rectTransform.anchoredPosition = new Vector2(-303, 0);
                var initial = Label("Portrait initial", new Vector2(70, 70), Vector2.zero, TextAnchor.MiddleCenter, 34);
                initial.transform.SetParent(icon.transform, false);
                var name = Label("Player name", new Vector2(631, 34), new Vector2(56, 14), TextAnchor.MiddleLeft, 28);
                name.transform.SetParent(go.transform, false); name.supportRichText = false;
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                var stats = Label("Public mode stats", new Vector2(631, 26), new Vector2(56, -18), TextAnchor.MiddleLeft, 19);
                stats.transform.SetParent(go.transform, false); stats.supportRichText = false;
                standingsRows[i] = new StandingsRow { card = card, rank = rank, name = name, stats = stats, portrait = portrait, initial = initial };
            }
            standingsFooter = Label("Standings page and controls", new Vector2(776, 54), new Vector2(-202, -284), TextAnchor.MiddleCenter, 18);
            standingsFooter.transform.SetParent(parent, false); standingsFooter.color = CandyHudCards.Ink;
        }

        void UpdateBoard(MatchFrame f, MatchPlayerFrame own)
        {
            bool mystery = f.rules.mode == GameModeKind.MurderMystery;
            bool statusRoster = (f.rules.mode == GameModeKind.Survival || f.rules.mode == GameModeKind.Infection) && f.rules.metric == ScoreMetric.SurvivalTime;
            var participants = StandingsParticipants(f, Services.Session as IGameExperienceControls, out bool curated);
            var rows = mystery ? participants.OrderBy(p => p.name).ToArray() : participants.OrderByDescending(p => p.metric).ThenBy(p => p.id, System.StringComparer.Ordinal).ToArray();
            int pages = Mathf.Max(1, Mathf.CeilToInt(rows.Length / (float)BoardPageSize));
            boardPage = Mathf.Clamp(boardPage, 0, pages - 1);
            CandyHudCards.Place(board, new Vector2(0, 10), new Vector2(1220, 660)); board.fontSize = 22;
            board.rectTransform.offsetMin = new Vector2(mystery ? 222 : 20, 552);
            board.rectTransform.offsetMax = new Vector2(mystery ? -222 : -424, -18);
            podium.SetPlayers(MatchRuntime.Current, rows, !mystery && canvas.enabled);
            standingsFooter.rectTransform.anchoredPosition = new Vector2(mystery ? 0 : -202, -284);
            text.Clear(); text.Append(mystery ? "PLAYER ROSTER · ROLES PRIVATE" : statusRoster ? "PARTICIPANTS · CURRENT STATUS" : "STANDINGS · " + CandyHudContent.Safe(f.metricLabel, 28));
            text.Append(" · ").Append(StandingsCount(rows.Length, curated));
            if (f.teams.Length > 0) text.Append('\n').Append(string.Join(" | ", f.teams.Select(t => CandyHudContent.Safe(t.name, 10) + " " + (f.rules.mode == GameModeKind.CaptureFlag ? t.captures : f.rules.mode == GameModeKind.TeamTickets ? t.tickets : Mathf.RoundToInt(t.points)))));
            board.text = text.ToString();
            for (int i = 0; i < standingsRows.Length; i++)
            {
                int rank = boardPage * BoardPageSize + i; var row = standingsRows[i]; bool show = rank < rows.Length;
                row.card.gameObject.SetActive(show); if (!show) continue;
                var player = rows[rank]; bool local = player.id == own?.id;
                row.card.rectTransform.anchoredPosition = new Vector2(mystery ? 0 : -202, 179 - i * 79);
                row.card.color = local ? new Color(.70f, 1f, .88f, .96f) : new Color(.91f, .85f, 1f, .82f);
                row.rank.color = row.name.color = row.stats.color = CandyHudCards.Ink;
                row.rank.text = mystery ? "·" : (rank + 1).ToString();
                scorePortraits.SetName(row.name, (local ? "YOU · " : "") + player.name);
                if (mystery) row.stats.text = f.mystery.bodies.Any(body => body.reported && body.id == player.id) ? "REPORTED" : "";
                else if (statusRoster || f.rules.metric == ScoreMetric.AliveStatus) row.stats.text = (player.role == "infected" ? "INFECTED" : player.alive && !player.dying ? "ACTIVE" : "ELIMINATED") + " · " + CandyHudContent.ResultStats(player, f.rules.mode);
                else row.stats.text = player.metric.ToString("0.#") + " · " + CandyHudContent.ResultStats(player, f.rules.mode);
                CandyScorePortraits.Show(row.portrait, row.initial, scorePortraits.Get(MatchRuntime.Current, player), player);
            }
            text.Clear(); if (own != null) text.Append(StandingsPlace(own, System.Array.FindIndex(rows, p => p.id == own.id), curated)).Append(" · ");
            text.Append("PAGE ").Append(boardPage + 1).Append(" / ").Append(pages);
            if (f.rules.mode == GameModeKind.Infection && f.secondaryLeaders.Length > 0) text.Append(" · Infections leader: ").Append(CandyHudContent.Safe(f.secondaryLeaders[0].name, 18)).Append(" · ").Append(f.secondaryLeaders[0].infections);
            text.Append('\n').Append(vr ? ScoreboardPinned ? "Pages cycle every 5s · Hide in Modes menu" : "Pages cycle every 5s · Release " + standingsControl + " to close" : ScoreboardPinned ? "PgUp / PgDn · Hide in Modes menu" : "PgUp / PgDn · Release F2 to close");
            standingsFooter.text = text.ToString();
        }
        void UpdateObjective(MatchFrame f,MatchPlayerFrame own)
        {
            if(own==null)return;
            const float width=306, top=307;
            bool flags=f.rules.mode==GameModeKind.CaptureFlag;
            objective.fontSize=17;
            CandyHudCards.Place(objective,new Vector2(-462,245),new Vector2(width,110));
            text.Clear();text.Append(f.rules.mode==GameModeKind.CaptureFlag&&f.flags.Length>2?(vr?"FLAG FOCUS · Modes menu\n":"FLAG FOCUS · G: NEXT ENEMY\n"):"<color=#61369B>Objective</color>\n");
            if(f.rules.mode==GameModeKind.CaptureFlag)
            {
                var focus=CandyHudContent.FocusedFlag(f,own,FlagFocusIndex);
                foreach(var flag in f.flags.Where(flag=>flag.team==own.team||flag==focus))
                {
                    string state=flag.atHome?"At base":flag.carrier==own.id?"In hand":
                        string.IsNullOrEmpty(flag.carrier)?"Dropped":"Taken";
                    text.Append(flag.team==own.team?"Your flag":f.teams.Length>2?CandyHudContent.Team(f,flag.team)+" flag":"Enemy flag").Append("  ·  ").Append(state).Append((char)10);
                }
                bool carrying=f.flags.Any(flag=>flag.carrier==own.id);
                bool ownFlagHome=f.flags.Any(flag=>flag.team==own.team&&flag.atHome);
                text.Append(carrying?(ownFlagHome?"Return to your base":"Recover your flag to score"):"Capture the enemy flag");
                text.Append("\nFirst to ").Append(f.rules.flagTarget).Append(f.rules.flagWinByTwo?" · Win by 2":"");
            }
            else if(f.rules.mode==GameModeKind.KingOfHill)text.Append("Stand inside the hill to score.\nThe highest safe region can move.");
            else if(f.rules.mode==GameModeKind.BlockControl)text.Append("Stand on blocks to claim them.\nYour team earns points while it owns them.");
            else if(f.rules.mode==GameModeKind.TeamTickets)text.Append("Each team shares its respawns.\nEliminate rivals; protect your remaining lives.");
            else if(f.rules.mode==GameModeKind.Infection)text.Append(own.role=="infected"?"Tag survivors with a punch.\nInfect everyone before time runs out.":"Avoid infected players.\nEliminated survivors join the infected.");
            else if(f.rules.mode==GameModeKind.Survival)text.Append("Stay above the ocean.\nOutlast the other survivors.");
            else text.Append("Explore, build and try the sandbox tools.");
            objective.text=text.ToString();
            float height=Mathf.Clamp(objective.preferredHeight+18,flags?92:64,192);
            CandyHudCards.Place(objective,new Vector2(-462,top-height/2),new Vector2(width,height));
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
                var root=MatchRuntime.Current.FindParticipantRoot(p.id);
                var flyer=root?root.GetComponent<IFlyingState>():null;
                var focus=flyer?.ContactBodies!=null&&flyer.ContactBodies.Count>0&&flyer.ContactBodies[0]?flyer.ContactBodies[0].position:flyer?.Anchor?flyer.Anchor.position:p.viewPosition;
                var point=focus+Vector3.up*(flyer!=null?.6f:.28f);
                var screen=camera.WorldToViewportPoint(point);
                if(screen.z<=0||screen.x<0||screen.x>1||screen.y<0||screen.y>1)continue;
                float distance=Vector3.Distance(camera.transform.position,focus);
                if(competitive&&distance>15||teamMode&&distance>65)continue;
                bool visible=NameVisible(focus,root);
                if(visible)plateSeen[p.id]=Time.unscaledTime;
                if(!teamMode&&!visible&&(competitive||!plateSeen.TryGetValue(p.id,out float seen)||Time.unscaledTime-seen>2))continue;
                shown++;active.Add(p.id);
                if(!plates.TryGetValue(p.id,out var label)||!label){label=CandyNameplate.Create(transform,"Visual - nameplate");plates[p.id]=label;}
                label.text=CandyNameplate.ActorLabel(p.name,p.bot);label.color=teamMode?p.color:Color.white;label.transform.position=point;label.transform.rotation=Quaternion.LookRotation(label.transform.position-camera.transform.position);label.gameObject.SetActive(true);
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
                    if(flag.carrier==own.id){var home=f.flags.FirstOrDefault(x=>x.team==own.team);if(home!=null)Marker(home.home,TeamColor(f,own.team),home.atHome?"DELIVER HERE":"FLAG NOT HOME");}
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
        void OnDestroy(){podium?.Dispose();if(canvas)Destroy(canvas.gameObject);foreach(var p in plates.Values)if(p)Destroy(p.gameObject);}
    }
}
