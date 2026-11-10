using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyAudio;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    // "What am I doing?" card: shown at the start of every round and whenever your role changes.
    // Big, centred, world-locked about 1.5 m away in the headset (re-centres gently if you turn away);
    // centred on screen on desktop. Goal and the two controls that matter for your role.
    public sealed class CandyRoundCard : MonoBehaviour
    {
        const float ShowSeconds=6.5f,Distance=1.5f,Scale=.0011f;
        static readonly Color Ink=new Color(.075f,.05f,.14f),Face=new Color(.20f,.14f,.31f,.97f),Cream=new Color(1,.96f,.88f),Muted=new Color(.82f,.76f,.92f),
            Mint=new Color(.30f,.95f,.74f),Peach=new Color(1,.62f,.42f),Pink=new Color(1,.45f,.66f);

        public static bool Showing {get;private set;}
        static bool replayRequested;
        public static void Replay()=>replayRequested=true;
        ISessionInfo session;Canvas canvas;RectTransform root;CanvasGroup group;
        Image band,timer;RawImage icon;Text kicker,title,goal,note,count,controlA,controlB;
        RoundPhase lastPhase=(RoundPhase)(-1);string lastRole;float until,shownAt;bool vrPlaced,vr,recentering;Vector3 anchor;Quaternion facing;

        void Start(){session=GetComponent<ISessionInfo>();Build();}
        void Build()
        {
            var go=new GameObject("Round card",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup),typeof(CanvasScaler));
            go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.sortingOrder=40;canvas.enabled=false;
            group=go.GetComponent<CanvasGroup>();group.alpha=0;group.blocksRaycasts=false;group.interactable=false;
            root=(RectTransform)go.transform;
            var card=Panel(root,"Card",Vector2.zero,new Vector2(760,360),Face);
            band=Panel(card,"Role band",new Vector2(0,122),new Vector2(740,104),Mint).GetComponent<Image>();
            var disc=Panel(band.rectTransform,"Icon disc",new Vector2(-300,0),new Vector2(84,84),Ink);
            var iconGo=new GameObject("Role icon",typeof(RectTransform),typeof(RawImage));iconGo.transform.SetParent(disc,false);
            icon=iconGo.GetComponent<RawImage>();icon.rectTransform.sizeDelta=new Vector2(56,56);icon.raycastTarget=false;
            kicker=Label(band.rectTransform,"YOU ARE A",new Vector2(70,30),new Vector2(560,30),24,Ink,false,TextAnchor.MiddleLeft);
            title=Label(band.rectTransform,"",new Vector2(70,-12),new Vector2(560,66),62,Ink,true,TextAnchor.MiddleLeft);
            count=Label(band.rectTransform,"",new Vector2(318,0),new Vector2(90,90),64,Ink,true);
            goal=Label(card,"",new Vector2(0,26),new Vector2(700,84),34,Cream,true);
            note=Label(card,"",new Vector2(0,-30),new Vector2(700,34),24,Muted,false);
            var a=Panel(card,"Control A",new Vector2(-180,-104),new Vector2(340,70),new Color(1,1,1,.08f));
            var b=Panel(card,"Control B",new Vector2(180,-104),new Vector2(340,70),new Color(1,1,1,.08f));
            controlA=Label(a,"",Vector2.zero,new Vector2(320,62),24,Cream,false);controlA.supportRichText=true;
            controlB=Label(b,"",Vector2.zero,new Vector2(320,62),24,Cream,false);controlB.supportRichText=true;
            timer=Panel(card,"Time left",new Vector2(-370,-168),new Vector2(740,6),Mint).GetComponent<Image>();
            timer.rectTransform.pivot=new Vector2(0,.5f);
        }
        static RectTransform Panel(RectTransform parent,string name,Vector2 p,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchoredPosition=p;r.sizeDelta=size;
            var image=go.GetComponent<Image>();image.sprite=CandyHudVisuals.Pill;image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=false;return r;
        }
        static Text Label(Transform parent,string value,Vector2 p,Vector2 size,int font,Color color,bool bold,TextAnchor anchor=TextAnchor.MiddleCenter)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var t=go.GetComponent<Text>();
            t.font=bold?CandyHudVisuals.TitleFont:CandyHudVisuals.TextFont;t.text=value;t.fontSize=font;t.alignment=anchor;t.color=color;t.raycastTarget=false;
            t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.resizeTextForBestFit=true;t.resizeTextMinSize=Mathf.Min(font,18);t.resizeTextMaxSize=font;
            t.rectTransform.anchoredPosition=p;t.rectTransform.sizeDelta=size;return t;
        }

        void LateUpdate()
        {
            var match=MatchRuntime.Current;var f=match?.Frame;
            if(!canvas||session==null||f==null){Hide();return;}
            MatchPlayerFrame own=null;foreach(var p in f.players)if(p.id==match.LocalActorId){own=p;break;}
            bool watching=session.Demo is ISpectatorControls s&&s.Spectating;
            if(own==null||own.role=="spectator"||watching||f.phase==RoundPhase.Victory||f.phase==RoundPhase.Results){lastPhase=f.phase;lastRole=own?.role;Hide();return;}
            bool roundStarted=(f.phase==RoundPhase.Countdown||f.phase==RoundPhase.Playing)&&lastPhase!=RoundPhase.Countdown&&lastPhase!=RoundPhase.Playing;
            bool roleChanged=lastRole!=null&&own.role!=lastRole&&f.phase==RoundPhase.Playing;
            bool firstSight=lastPhase==(RoundPhase)(-1)&&(f.phase==RoundPhase.Countdown||f.phase==RoundPhase.Playing);
            if(roundStarted||roleChanged||firstSight||replayRequested){replayRequested=false;Show(f,own,roleChanged);}
            lastPhase=f.phase;lastRole=own.role;
            if(Time.unscaledTime>=until&&f.phase!=RoundPhase.Countdown){Hide();return;}
            bool hidden=session.MenuOpen||!session.ActiveCamera;
            count.text=f.phase==RoundPhase.Countdown&&f.remaining>0?Mathf.CeilToInt(f.remaining).ToString():"";
            if(f.phase==RoundPhase.Countdown)until=Mathf.Max(until,Time.unscaledTime+2.5f);
            float life=Mathf.Clamp01((until-Time.unscaledTime)/ShowSeconds);
            timer.rectTransform.sizeDelta=new Vector2(740*life,6);
            group.alpha=Mathf.MoveTowards(group.alpha,hidden?0:1,Time.unscaledDeltaTime*(hidden?8:4));
            canvas.enabled=group.alpha>.01f;Showing=canvas.enabled;
            if(canvas.enabled)Place();
        }
        void Hide(){if(!canvas)return;group.alpha=Mathf.MoveTowards(group.alpha,0,Time.unscaledDeltaTime*5);canvas.enabled=group.alpha>.01f;Showing=canvas.enabled;if(canvas.enabled)Place();}
        void OnDisable(){Showing=false;}
        void Show(MatchFrame f,MatchPlayerFrame own,bool roleChanged)
        {
            bool hand=own.role=="hand"||own.role=="infected";
            var mode=f.rules.mode;
            string name,line,detail;Color color=Mint;CandyHudIcon picture=CandyHudIcon.Survivor;
            if(hand)
            {
                picture=CandyHudIcon.Hand;color=Pink;
                name=mode==GameModeKind.Infection?"INFECTED":"GIANT HAND";
                line=mode==GameModeKind.Infection?"Tag every survivor before time runs out.":"Knock the survivors off the tower.";
                detail="Push, pull and swipe blocks to make them fall.";
            }
            else switch(mode)
            {
                case GameModeKind.Survival:name="SURVIVOR";line="Stay on the tower. The water knocks you out.";detail="Last survivor standing wins.";break;
                case GameModeKind.Infection:name="SURVIVOR";line="Stay away from the infected hands.";detail="Get caught and you join them.";break;
                case GameModeKind.KingOfHill:name="KING OF THE HILL";line="Stand inside the glowing hill to score.";detail="Push rivals off the summit.";color=Peach;break;
                case GameModeKind.BlockControl:name="COLOR CONTROL";line="Stand on blocks to paint them your color.";detail="Your team scores while it owns them.";color=Peach;break;
                case GameModeKind.CaptureFlag:name="CAPTURE THE FLAG";line="Grab the enemy flag and bring it home.";detail="Protect your own flag too.";color=Peach;break;
                case GameModeKind.TeamTickets:name="LAST TEAM STANDING";line="Your team shares its respawns.";detail="Be the last team left on the tower.";color=Peach;break;
                case GameModeKind.MurderMystery:name="MURDER MYSTERY";line="Find the murderer before it's too late.";detail="Your secret role is on your wrist.";color=new Color(.78f,.64f,1);break;
                case GameModeKind.Ninja:name="SHADOW LANTERN";line="Steal the spirit lantern. Hold it to score.";detail="Hits knock it loose · it escapes after a few seconds";color=new Color(1,.58f,.32f);break;
                default:name="SANDBOX";line="Try everything. No pressure.";detail="Open the menu for sandbox tools.";color=new Color(.78f,.64f,1);break;
            }
            kicker.text=roleChanged?"NOW YOU ARE A":hand||mode==GameModeKind.Survival||mode==GameModeKind.Infection?"YOU ARE A":"THIS ROUND";
            title.text=name;goal.text=line;note.text=detail;band.color=color;timer.color=color;
            icon.texture=CandyHudVisuals.Icon(picture);icon.color=color;
            Controls(hand);
            until=Time.unscaledTime+ShowSeconds;shownAt=Time.unscaledTime;vrPlaced=false;
            if(session.ActiveCamera)CandyAudioControls.PlayUiClick(session.ActiveCamera.transform.position);
        }
        void Controls(bool hand)
        {
            string Key(string k)=>"<b><color=#4EF2BC>"+k+"</color></b>";
            if(!session.IsVR)
            {
                controlA.text=Key("W A S D")+"  "+(hand?"Fly":"Move");
                controlB.text=hand?Key("Q / E")+"  Down / up":Key("SPACE")+"  Jump · hold to climb";
                return;
            }
            bool left=session.Player?.gameObject.GetComponent<IBlasterControls>()?.LeftHanded==true;
            controlA.text=Key("LEFT STICK")+"  "+(hand?"Fly":"Move");
            controlB.text=hand?Key("L TRIGGER / GRIP")+"  Up / down":Key(left?"X":"A")+"  Jump · hold to climb";
        }
        void Place()
        {
            var view=session.ActiveCamera;if(!view)return;
            if(vr!=session.IsVR||canvas.renderMode==RenderMode.ScreenSpaceOverlay&&session.IsVR||canvas.renderMode==RenderMode.WorldSpace&&!session.IsVR)
            {
                vr=session.IsVR;vrPlaced=false;
                if(vr){root.SetParent(null,false);canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=view;root.sizeDelta=new Vector2(780,380);root.localScale=Vector3.one*Scale;}
                else
                {
                    root.SetParent(transform,false);canvas.renderMode=RenderMode.ScreenSpaceOverlay;root.localScale=Vector3.one;
                    var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
                }
            }
            if(!vr)return;
            var head=view.transform;var flat=Vector3.ProjectOnPlane(head.forward,Vector3.up);if(flat.sqrMagnitude<.01f)flat=head.up;flat.Normalize();
            var target=head.position+flat*Distance+Vector3.down*.12f;
            // World-locked: only drift back when you look well away or move off.
            float off=Vector3.Angle(flat,Vector3.ProjectOnPlane(anchor-head.position,Vector3.up)),gap=Vector3.Distance(anchor,target);
            if(!vrPlaced){anchor=target;vrPlaced=true;recentering=false;}
            if(off>32||gap>.9f)recentering=true;else if(off<4&&gap<.12f)recentering=false;
            if(recentering)anchor=Vector3.Lerp(anchor,target,1-Mathf.Exp(-Time.unscaledDeltaTime*3));
            facing=Quaternion.LookRotation(anchor-head.position,Vector3.up);
            root.SetPositionAndRotation(anchor,facing);
        }
        void OnDestroy(){Showing=false;if(root)Destroy(root.gameObject);}
    }
}
