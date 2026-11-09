using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    [DefaultExecutionOrder(260)]
    public sealed class CandyMysteryView:MonoBehaviour
    {
        Canvas canvas;RectTransform root;Text role,prompt,choices,reports,notice;
        CandyMysteryBodyGuide bodyGuide;
        InputAction hudSelect;
        void OnEnable()=>hudSelect=CandyHudContent.CreateSelectInput();
        void OnDisable(){hudSelect?.Dispose();hudSelect=null;}
        readonly HashSet<string> reportedBodies=new HashSet<string>();
        readonly Dictionary<string,int> bodyNumbers=new Dictionary<string,int>();
        int nextBodyNumber;float lastElapsed=-1,noticeUntil;string noticeBody,privateNotice;
        float privateNoticeUntil;
        public static int ReportPage;
        readonly List<MysteryBodyFrame> allReports=new List<MysteryBodyFrame>();
        readonly CandyMysteryReportFocus reportFocus=new CandyMysteryReportFocus();
        string autoBodyId;
        float trackingLostUntil;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetReports()=>ReportPage=0;
        Camera eye;bool vr;
        float nextCommand;string currentRevive;
        readonly Dictionary<int,List<MysteryTrailFrame>> trailsByBlock=new Dictionary<int,List<MysteryTrailFrame>>();
        int cachedTrails;MysteryTrailFrame firstTrail;
        readonly HashSet<string> announcedSheriffs=new HashSet<string>();
        float sheriffNoticeUntil;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=go=>{if(!go.GetComponent<CandyMysteryView>())go.AddComponent<CandyMysteryView>();};
        void Build()
        {
            var go=new GameObject("Visual - mystery clues",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(transform,false);canvas=go.GetComponent<Canvas>();canvas.sortingOrder=30;root=go.GetComponent<RectTransform>();var scale=go.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);
            role=CandyHudCards.Create(root,"Your private role",new Vector2(-452,262),new Vector2(316,124),16);
            prompt=CandyHudCards.Create(root,"Context action",new Vector2(-452,146),new Vector2(316,96),16);
            choices=CandyHudCards.Create(root,"Private disaster choices",new Vector2(0,95),new Vector2(440,100),19,TextAnchor.MiddleCenter);
            reports=CandyHudCards.Create(root,"Recent body reports",new Vector2(-452,25),new Vector2(316,134),16);
            notice=CandyHudCards.Create(root,"Body report alert",new Vector2(0,183),new Vector2(344,72),17,TextAnchor.MiddleCenter);
            CandyHudCards.Tint(role,new Color(.81f,.69f,1),.32f);
            CandyHudCards.Tint(notice,new Color(1,.60f,.68f),.43f);
            CandyHudCards.Tint(choices,new Color(1,.79f,.45f),.33f);
            CandyHudCards.Emblem(role,"gem",new Color(.86f,.70f,1));
            CandyHudCards.Emblem(reports,"clue",new Color(.87f,.71f,1));
            bodyGuide=new CandyMysteryBodyGuide(root);
        }
        void Update()
        {
            var match=MatchRuntime.Current;var mystery=match as IMysteryControls;var session=Services.Session;
            if(mystery==null||session==null){if(canvas)canvas.enabled=false;return;}
            if(!canvas)Build();canvas.enabled=mystery.Active&&!session.MenuOpen&&!CandyMatchHud.Expanded;
            if(eye!=session.ActiveCamera||vr!=session.IsVR)
            {
                eye=session.ActiveCamera;vr=session.IsVR;
                CandyHudLayout.Configure(canvas,transform,eye,vr);
            }
            if(!mystery.Active){bodyGuide.Hide();ResetClues();lastElapsed=-1;return;}
            if(!eye){bodyGuide.Hide();return;}
            if(match.Frame.elapsed<lastElapsed)ResetClues();
            lastElapsed=match.Frame.elapsed;
            if(mystery.Clues.bodies.Length==0){announcedSheriffs.Clear();sheriffNoticeUntil=0;}
            var own=mystery.Personal;
            bool blocked=session.MenuOpen||CandyMatchHud.Expanded;
            allReports.Clear();
            foreach(var body in mystery.Clues.bodies)if(body.reported)allReports.Add(body);
            allReports.Sort((a,b)=>a.reportedAge!=b.reportedAge?a.reportedAge.CompareTo(b.reportedAge):string.CompareOrdinal(a.id,b.id));
            for(int i=allReports.Count-1;i>=0;i--)BodyNumber(allReports[i].id);
            var newest=allReports.FirstOrDefault(b=>!b.lost&&!reportedBodies.Contains(b.id)&&b.reportedAge<3);
            if(newest!=null){noticeBody=newest.id;noticeUntil=Time.unscaledTime+9;if(!blocked)DeadlyJenga.CandyAudio.CandyFeedback.Pulse(.2f);}

            if(reportFocus.RemoveUnavailable(allReports))trackingLostUntil=Time.unscaledTime+4;
            if(!blocked&&match.Frame.phase==RoundPhase.Playing&&!DeadlyJenga.CastlePrototype.EmoteWheel.IsOpen)
            {
                if(!vr&&Keyboard.current?.bKey.wasPressedThisFrame==true)ReportPage++;
                if(ReportPage>0)
                {
                    reportFocus.Cycle(allReports,ReportPage);
                    trackingLostUntil=0;
                    ReportPage=0;
                }
            }
            var reportPage=reportFocus.Find(allReports);
            var selectedReport=reportPage?.lost==false?reportPage:null;
            role.text="<color=#61369B><size=18>Private role · "+own.role+"</size></color>\n"+CandyHudContent.RoleGoal(own.role)+"\n"+CandyHudContent.RoleAttack(own,vr);
            if(own.role==MysteryRole.Sheriff)role.text+="\nWrong special hits: "+own.mistakes+" / "+match.Rules.mystery.sheriffMistakes;
            bool pressed=vr?CandyHudContent.SelectPressed(hudSelect):Keyboard.current?.eKey.wasPressedThisFrame==true;
            bool held=vr?CandyHudContent.SelectHeld(hudSelect):Keyboard.current?.eKey.isPressed==true;
            if(!blocked&&Keyboard.current?.qKey.wasPressedThisFrame==true)mystery.SetSpecialAttack(!own.specialAttack);
            float interaction=match.Rules.mystery.interactDistance;
            prompt.text="";currentRevive=null;float nearest=interaction;MysteryBodyFrame nearbyBody=null;
            MysteryBodyFrame trackedBody=null,previousAutoBody=null,visibleDiscovery=null;
            float trackedDistance=float.PositiveInfinity,previousDistance=float.PositiveInfinity;
            float discoveryDistance=interaction*CandyMysteryBodyGuide.NearbyMultiplier;
            foreach(var body in mystery.Clues.bodies)
            {
                if(body.lost)continue;
                if(body.sheriff&&announcedSheriffs.Add(body.id))sheriffNoticeUntil=Time.unscaledTime+5;
                float distance=Vector3.Distance(eye.transform.position,body.position+Vector3.up*.4f);
                bool close=distance<interaction&&Visible(body.position+Vector3.up*.3f);
                if(close&&distance<nearest){nearest=distance;nearbyBody=body;}
                if(body.reported)reportedBodies.Add(body.id);
                if(!body.reported&&!body.sheriff)
                {
                    // A nearby body can be noticed in view, without making it discoverable through cover.
                    var point=body.position+Vector3.up*.4f;
                    if(!blocked&&selectedReport==null&&distance<discoveryDistance&&!close&&InView(point)&&Visible(point,.12f))
                    {
                        visibleDiscovery=body;
                        discoveryDistance=distance;
                    }
                    continue;
                }
                if(body.id==autoBodyId){previousAutoBody=body;previousDistance=distance;}
                if(distance<trackedDistance){trackedDistance=distance;trackedBody=body;}
            }
            if(previousAutoBody!=null&&previousDistance<=trackedDistance+3)trackedBody=previousAutoBody;
            autoBodyId=trackedBody?.id;
            MatchPlayerFrame trackedRevive=null,reachableRevive=null;
            float reviveDistance=float.PositiveInfinity,reachableReviveDistance=interaction;
            foreach(string id in mystery.Clues.downed)
            {
                if(id==match.LocalActorId)continue;
                var p=match.Frame.players.FirstOrDefault(x=>x.id==id);if(p==null)continue;
                var point=p.position+Vector3.up*.4f;
                float distance=Vector3.Distance(eye.transform.position,point);
                if(distance<reviveDistance){reviveDistance=distance;trackedRevive=p;}
                if(distance<reachableReviveDistance&&Visible(point)){reachableReviveDistance=distance;reachableRevive=p;}
            }
            currentRevive=reachableRevive?.id;
            if(!blocked)
            {
                if(currentRevive!=null){prompt.text=vr?"Hold trigger / pinch to revive":"HOLD E TO REVIVE";if(Time.unscaledTime>=nextCommand){nextCommand=Time.unscaledTime+.15f;mystery.Revive(currentRevive,held);}}
                else if(nearbyBody!=null&&(!nearbyBody.reported||nearbyBody.sheriff&&own.role==MysteryRole.Innocent))
                {
                    string action=nearbyBody.sheriff?"REPORT SHERIFF":"REPORT NEARBY BODY";
                    prompt.text=(vr?"TRIGGER / PINCH":"E")+" · "+action;
                    if(nearbyBody.sheriff&&own.role==MysteryRole.Innocent)prompt.text+="\nCollect the nearby revolver";
                    if(pressed)mystery.ReportBody(nearbyBody.id);
                }
                else if(nearbyBody?.reported==true&&nearbyBody.reportedAge<=match.Rules.mystery.suspicionSeconds)
                {
                    var trail=NearestTrail(mystery,nearbyBody.position);
                    if(trail!=null){prompt.text=(vr?"TRIGGER / PINCH":"E")+" · SUSPECT "+CandyHudContent.ColorName(trail.color).ToUpperInvariant()+" TRAIL\nA suspicion is not proof of guilt.";if(pressed)mystery.Suspect(nearbyBody.id,trail.sequence);}
                }
            }
            if(nearbyBody!=null){var reportLock=own.reportLocks?.FirstOrDefault(l=>l.body==nearbyBody.id);if(reportLock!=null&&reportLock.remaining>0)prompt.text="REPORT READY IN "+Mathf.CeilToInt(reportLock.remaining)+"s";}
            if(string.IsNullOrEmpty(prompt.text))prompt.text=nearbyBody?.reported==true?"REPORTED BODY #"+BodyNumber(nearbyBody.id)+"\n"+CandyHudContent.Suspects(nearbyBody)+"\nLook at a nearby trail to mark suspicion.":"";
            if(!string.IsNullOrEmpty(own.notice)&&own.notice!=privateNotice){privateNotice=own.notice;privateNoticeUntil=Time.unscaledTime+7;}
            if(Time.unscaledTime<privateNoticeUntil)prompt.text=CandyHudContent.Safe(privateNotice,120);
            bool localDowned=mystery.Clues.downed.Contains(match.LocalActorId);
            if(localDowned)prompt.text="YOU ARE DOWNED\nAnother player must hold "+(vr?"trigger / pinch":"E")+" near you to revive you.";
            CandyHudCards.Visible(prompt,!string.IsNullOrEmpty(prompt.text));
            UpdateReports(match,reportPage,trackedBody);
            DrawChoices(mystery,blocked);
            bool hideGuide=blocked||localDowned||match.Frame.phase!=RoundPhase.Playing||
                (session.Demo as ISpectatorControls)?.Spectating==true||DeadlyJenga.CastlePrototype.EmoteWheel.IsOpen;
            if(hideGuide)bodyGuide.Hide();
            else ShowBodyGuide(interaction,own.disasterChoices.Length>0,selectedReport,nearbyBody,visibleDiscovery??trackedBody,reachableRevive,trackedRevive);
        }
        bool InView(Vector3 point)
        {
            var p=eye.WorldToViewportPoint(point);
            return p.z>0&&p.x>=0&&p.x<=1&&p.y>=0&&p.y<=1;
        }
        bool Visible(Vector3 point,float maxEndGap=float.PositiveInfinity)
        {
            var delta=point-eye.transform.position;
            float distance=delta.magnitude;
            float endGap=Mathf.Min(distance*.1f,maxEndGap);
            return !Physics.Raycast(eye.transform.position,delta.normalized,distance-endGap,
                (1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore);
        }
        MysteryTrailFrame NearestTrail(IMysteryControls mystery,Vector3 body)
        {
            if(mystery.Trails.Count<cachedTrails||mystery.Trails.Count>0&&firstTrail!=mystery.Trails[0]){cachedTrails=0;trailsByBlock.Clear();}
            while(cachedTrails<mystery.Trails.Count){var t=mystery.Trails[cachedTrails++];firstTrail=mystery.Trails[0];if(!trailsByBlock.TryGetValue(t.block,out var list)){list=new List<MysteryTrailFrame>();trailsByBlock[t.block]=list;}list.Add(t);}
            MysteryTrailFrame best=null;float closest=3;var blocks=Services.World?.Blocks;if(blocks==null||!Physics.Raycast(eye.transform.position,eye.transform.forward,out var hit,3,1<<Layers.Block,QueryTriggerInteraction.Ignore)||!hit.rigidbody)return null;
            int index=-1;for(int i=0;i<blocks.Count;i++)if(blocks[i]==hit.rigidbody){index=i;break;}
            if(!trailsByBlock.TryGetValue(index,out var candidates))return null;
            foreach(var t in candidates)
            {
                if(t.block<0||t.block>=blocks.Count||!blocks[t.block])continue;
                var point=blocks[t.block].transform.TransformPoint(t.point);if((point-body).sqrMagnitude>9.4f)continue;
                var delta=point-eye.transform.position;float distance=delta.magnitude;if(distance>closest||Vector3.Dot(delta.normalized,eye.transform.forward)<.85f)continue;
                closest=distance;best=t;
            }
            return best;
        }
        void DrawChoices(IMysteryControls mystery,bool menu)
        {
            var p=mystery.Personal;choices.text=p.disasterChoices.Length==0?"":(p.veto?"VETO ONE":"CHOOSE ONE")+" · "+Mathf.CeilToInt(p.choiceRemaining)+"s\n"+string.Join("      ",p.disasterChoices.Select((s,i)=>(i+1)+" · "+s));
            CandyHudCards.Visible(choices,p.disasterChoices.Length>0);
            if(menu)return;
            for(int i=0;i<p.disasterChoices.Length;i++)if(Keyboard.current!=null&&Keyboard.current[(Key)((int)Key.Digit1+i)].wasPressedThisFrame)mystery.ChooseDisaster(p.disasterChoices[i]);
            if(vr&&CandyHudContent.SelectPressed(hudSelect)&&p.disasterChoices.Length>0)
            {
                var plane=new Plane(root.forward,root.position);if(plane.Raycast(new Ray(eye.transform.position,eye.transform.forward),out float d))
                {var local=choices.rectTransform.InverseTransformPoint(eye.transform.position+eye.transform.forward*d);var rect=choices.rectTransform.rect;if(rect.Contains(new Vector2(local.x,local.y))){int index=Mathf.Clamp(Mathf.FloorToInt((local.x-rect.xMin)/rect.width*p.disasterChoices.Length),0,p.disasterChoices.Length-1);mystery.ChooseDisaster(p.disasterChoices[index]);}}
            }
        }
        void ShowBodyGuide(float interaction,bool choices,MysteryBodyFrame selected,MysteryBodyFrame nearby,
            MysteryBodyFrame known,MatchPlayerFrame reachableRevive,MatchPlayerFrame knownRevive)
        {
            var body=selected??nearby??known;
            var revive=selected==null?(reachableRevive??(body==null?knownRevive:null)):null;
            if(revive!=null)
            {
                var point=revive.position+Vector3.up*.4f;
                bodyGuide.Show(eye,point,"REVIVE "+CandyHudContent.Safe(revive.name,14),CandyMysteryBodyGuide.Kind.Revive,
                    interaction,reachableRevive!=null,Visible(point,.12f),choices);
            }
            else if(body!=null)
            {
                var point=body.position+Vector3.up*.4f;
                string label=body.reported?(body.sheriff?"SHERIFF · BODY #":"REPORTED BODY #")+BodyNumber(body.id):
                    body.sheriff?"FALLEN SHERIFF":"UNREPORTED BODY";
                bool reachable=nearby?.id==body.id&&reachableRevive==null;
                bodyGuide.Show(eye,point,label,body.sheriff?CandyMysteryBodyGuide.Kind.Sheriff:CandyMysteryBodyGuide.Kind.Evidence,
                    interaction,reachable,Visible(point,.12f),choices);
            }
            else bodyGuide.Hide();
        }

        void ResetClues()
        {
            reportedBodies.Clear();
            bodyNumbers.Clear();
            announcedSheriffs.Clear();
            reportFocus.Clear();
            autoBodyId=null;
            nextBodyNumber=ReportPage=0;
            noticeBody=null;
            trackingLostUntil=noticeUntil=sheriffNoticeUntil=0;
        }

        int BodyNumber(string id)
        {if(!bodyNumbers.TryGetValue(id,out int number)){number=++nextBodyNumber;bodyNumbers[id]=number;}return number;}
        void UpdateReports(IMatchRuntime match,MysteryBodyFrame selected,MysteryBodyFrame automatic)
        {
            var body=selected??(automatic?.reported==true?automatic:null)??allReports.FirstOrDefault();
            string instruction=vr?"Modes: next report":"B: next report";
            string header=selected==null?"Auto tracking":(selected.lost?"Past report #":"Tracking #")+BodyNumber(selected.id);
            var text=new System.Text.StringBuilder("<color=#61369B>"+header+"</color> · "+instruction);
            if(Time.unscaledTime<trackingLostUntil)text.Append("\nTracked body lost. Auto resumed.");
            if(body!=null)
            {
                var finder=match.Frame.players.FirstOrDefault(p=>p.id==body.finder);
                text.Append("\n#").Append(BodyNumber(body.id)).Append(' ').Append(CandyHudContent.Safe(body.name,18)).Append(" · ").Append(Mathf.FloorToInt(body.reportedAge)).Append("s ago");
                text.Append("\n").Append(body.lost?"Body lost · location unavailable":"Found by "+CandyHudContent.Safe(finder?.name??"a player",22));
                if(allReports.Count>1)text.Append("\n").Append(allReports.Count).Append(" reports");
            }
            reports.text=text.ToString();
            float height=Mathf.Clamp(reports.preferredHeight+18,84,172);
            CandyHudCards.Place(reports,new Vector2(-452,92-height/2),new Vector2(316,height));
            CandyHudCards.Visible(reports,allReports.Count>0||Time.unscaledTime<trackingLostUntil);
            var latest=allReports.FirstOrDefault(b=>b.id==noticeBody);
            bool bodyAlert=latest!=null&&Time.unscaledTime<noticeUntil;
            bool sheriffAlert=Time.unscaledTime<sheriffNoticeUntil;
            CandyHudCards.Visible(notice,bodyAlert||sheriffAlert);
            if(bodyAlert)notice.text="BODY #"+BodyNumber(latest.id)+" REPORTED · "+CandyHudContent.Safe(latest.name,24)+"\n"+(latest.lost?"Body lost":selected?.lost==false&&selected.id==latest.id?"TRACKING THIS REPORT":instruction);
            else if(sheriffAlert)notice.text="SHERIFF HAS FALLEN\nFind and pick up the blue revolver.";
        }
        void OnDestroy(){if(root)Destroy(root.gameObject);}
    }
}
