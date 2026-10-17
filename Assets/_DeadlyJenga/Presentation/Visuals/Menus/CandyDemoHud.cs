using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DeadlyJenga.CandyAudio;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Demo presentation only: actions go to the session, bots and multiplayer through Core.
    [DefaultExecutionOrder(250)]
    [DisallowMultipleComponent]
    public sealed class CandyDemoHud : MonoBehaviour, ISessionHud
    {
        public enum HintMode { Adaptive, Always, Hidden }
        public HintMode hintMode=HintMode.Adaptive;
        [Range(.5f,1)] public float hintOpacity=.86f;
        ISessionInfo session;CandyAudioStudio studio;
        IPlayableCharacter Active=>session.PlayingHand?session.Hand:session.Player;
        bool Climbing=>session.Player is IMovementState m&&m.Climbing;
        bool Vaulting=>session.Player is IMovementState m&&m.Vaulting;
        int page,selection;Vector2 scroll;bool lastMenu,lastRole,lastVR;float changedAt;string lastContext="";
        float uiSize=1;GUIStyle title,heading,body,small,button,box;GUISkin themed;bool ready;
        Canvas hintCanvas;CanvasGroup hintGroup;RectTransform hintRoot;Text roleText,statusText,menuText;
        readonly Text[] hintKeys=new Text[3],hintNames=new Text[3];readonly RawImage[] hintIcons=new RawImage[3];
        readonly Action[] navActions=new Action[40];int navCount,drawIndex;bool activate;
        bool HasFriends=>session!=null&&session.Multiplayer!=null;
        int PageCount=>HasFriends?3:2;
        string PageName(int index)=>HasFriends?(index==0?"Play":index==1?"Friends":"Controls"):(index==0?"Play":"Controls");
        CandyHudIcon PageIcon(int index)=>PageName(index)=="Friends"?CandyHudIcon.Friends:PageName(index)=="Controls"?CandyHudIcon.Help:CandyHudIcon.Play;
        bool ControlsPageOpen=>page==PageCount-1;
        public bool GameplayHintsVisible=>hintGroup&&hintGroup.alpha>.01f;
        public Canvas ControlHintCanvas=>hintCanvas;
        public string InputLabel=>session==null||session.Player==null?"Keyboard + mouse":session.IsVR?(TrackedHands?"Hand tracking":"VR controllers"):"Keyboard + mouse";
        bool TrackedHands=>Active!=null&&Active.HasHandTracking;
        bool Ready=>session!=null&&session.Player!=null&&session.Hand!=null;
        void Awake(){session=GetComponent<ISessionInfo>();changedAt=Time.unscaledTime;}
        void Update()
        {
            if(!Ready)return;
            if(!studio)studio=FindFirstObjectByType<CandyAudioStudio>();
            if(studio)studio.ShowLauncher=false;
            if(lastRole!=session.PlayingHand||lastVR!=session.IsVR){changedAt=Time.unscaledTime;lastRole=session.PlayingHand;lastVR=session.IsVR;}
            if(session.MenuOpen&&!lastMenu){selection=0;changedAt=Time.unscaledTime;}
            lastMenu=session.MenuOpen;
            var k=Keyboard.current;
            if(k!=null&&session.MenuOpen&&!session.IsVR&&!(studio&&studio.IsOpen))
            {
                if(k.leftArrowKey.wasPressedThisFrame)ChangePage((page+PageCount-1)%PageCount);
                if(k.rightArrowKey.wasPressedThisFrame)ChangePage((page+1)%PageCount);
                if(k.upArrowKey.wasPressedThisFrame){selection=(selection+Mathf.Max(1,navCount)-1)%Mathf.Max(1,navCount);FollowSelection();}
                if(k.downArrowKey.wasPressedThisFrame){selection=(selection+1)%Mathf.Max(1,navCount);FollowSelection();}
                if(k.enterKey.wasPressedThisFrame||k.numpadEnterKey.wasPressedThisFrame)activate=true;
            }
            if(k!=null&&!session.MenuOpen&&k.f1Key.wasPressedThisFrame){hintMode=hintMode==HintMode.Hidden?HintMode.Adaptive:HintMode.Hidden;changedAt=Time.unscaledTime;}
            PresentHints();
        }
        void ChangePage(int next){page=next;selection=0;scroll=Vector2.zero;activate=false;}
        void FollowSelection(){float top=116+selection*104;if(top<scroll.y)scroll.y=top;if(top+100>scroll.y+510)scroll.y=top+100-510;}
        void Styles()
        {
            if(ready)return;ready=true;var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title=new GUIStyle(GUI.skin.label){font=font,fontSize=30,fontStyle=FontStyle.Bold,normal={textColor=Color.white}};
            heading=new GUIStyle(title){fontSize=22};body=new GUIStyle(GUI.skin.label){font=font,fontSize=19,wordWrap=true,normal={textColor=Color.white}};
            small=new GUIStyle(body){fontSize=16,normal={textColor=CandyHudVisuals.Muted}};
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=19,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(14,12,8,8),border=new RectOffset(10,10,10,10),normal={background=CandyHudVisuals.Rounded,textColor=Color.white},hover={background=CandyHudVisuals.Rounded,textColor=Color.white},active={background=CandyHudVisuals.Rounded,textColor=Color.white}};
            box=new GUIStyle(GUI.skin.box){border=new RectOffset(10,10,10,10),normal={background=CandyHudVisuals.Rounded}};
            themed=Instantiate(GUI.skin);themed.hideFlags=HideFlags.HideAndDontSave;themed.label=body;themed.button=button;themed.box=box;
            themed.toggle.font=font;themed.toggle.fontSize=19;themed.toggle.normal.textColor=Color.white;
        }
        void OnGUI()
        {
            if(!Ready||session.IsVR||!session.MenuOpen||studio&&studio.IsOpen)return;
            Styles();var old=GUI.matrix;var skin=GUI.skin;var tint=GUI.color;int depth=GUI.depth;
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f)*uiSize;
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-800*scale)/2),Quaternion.identity,Vector3.one*scale);GUI.depth=-100;GUI.skin=themed;
            GUI.ModalWindow(GetInstanceID(),new Rect(40,32,1200,736),DrawMenu,GUIContent.none,GUIStyle.none);
            GUI.matrix=old;GUI.skin=skin;GUI.color=tint;GUI.depth=depth;
        }
        void Panel(Rect r,Color c){var old=GUI.backgroundColor;GUI.backgroundColor=c;GUI.Box(r,GUIContent.none,box);GUI.backgroundColor=old;}
        void Icon(Rect r,CandyHudIcon icon,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,CandyHudVisuals.Icon(icon));GUI.color=old;}
        bool Button(Rect r,string text,Color color)
        {var old=GUI.backgroundColor;GUI.backgroundColor=color;var foreground=color.grayscale>.5f?CandyHudVisuals.Ink:Color.white;button.normal.textColor=button.hover.textColor=foreground;bool result=GUI.Button(r,text,button);GUI.backgroundColor=old;return result;}
        void DrawMenu(int id)
        {
            Panel(new Rect(0,0,1200,736),CandyHudVisuals.Ink);Panel(new Rect(0,0,1200,8),CandyHudVisuals.Lavender);
            Icon(new Rect(26,27,48,48),session.PlayingHand?CandyHudIcon.Hand:CandyHudIcon.Home,CandyHudVisuals.Mint);
            GUI.Label(new Rect(93,23,700,45),"DEADLY JENGA  /  DEMO",title);
            GUI.Label(new Rect(95,71,775,30),(session.PlayingHand?"GIANT HAND":"SURVIVOR")+"   ·   "+InputLabel+"   ·   "+(session.Multiplayer!=null&&session.Multiplayer.IsClient?"Connected to host":"Your hosted playground"),small);
            if(Button(new Rect(963,28,207,51),"Resume  ·  Esc",CandyHudVisuals.Mint))session.SetMenu(false);
            for(int i=0;i<PageCount;i++)
            {
                bool pending=PageName(i)=="Friends"&&session.Multiplayer!=null&&session.Multiplayer.HasPending;
                var r=new Rect(20,128+i*74,218,62);if(Button(r,"       "+PageName(i)+(pending?"  !":""),i==page?new Color(.40f,.28f,.58f):CandyHudVisuals.Panel))ChangePage(i);
                Icon(new Rect(r.x+14,r.y+15,30,30),PageIcon(i),i==page?CandyHudVisuals.Mint:CandyHudVisuals.Lavender);
            }
            Panel(new Rect(258,126,921,548),CandyHudVisuals.Panel);drawIndex=0;
            float contentHeight=ControlsPageOpen?920:640;
            scroll=GUI.BeginScrollView(new Rect(278,142,884,516),scroll,new Rect(0,0,856,contentHeight));
            GUI.Label(new Rect(8,2,830,36),PageTitle(),heading);GUI.Label(new Rect(8,45,830,60),PageDescription(),small);
            if(page==0)PlayPage();else if(HasFriends&&page==1)FriendsPage();else ControlsPage();
            GUI.EndScrollView();navCount=drawIndex;if(navCount>0)selection=Mathf.Clamp(selection,0,navCount-1);
            GUI.Label(new Rect(26,692,1150,30),"CLICK a card to act  ·  SCROLL for more  ·  ← → sections  ·  ↑ ↓ choose a card  ·  ENTER confirm  ·  ESC resume",small);
            if(activate&&Event.current.type==EventType.Repaint){activate=false;if(selection<navCount)navActions[selection]?.Invoke();}
        }
        string PageTitle()=>page==0?"Make a little mayhem":HasFriends&&page==1?"Multiplayer session":"A little guide to surviving the chaos";
        string PageDescription()=>page==0?"Switch roles, get back on top, or start the tower over. Match setup stays in the lobby.":HasFriends&&page==1?"Your current connection and controls for leaving the session.":"Hints match the inputs this demo actually supports. A regular gamepad is not a desktop movement binding yet.";
        bool EditWorld=>session.CanEditWorld;
        void ActionCard(float y,CandyHudIcon icon,string name,string description,Action action,bool enabled=true,Color? accent=null)
        {
            int index=drawIndex++;navActions[index]=enabled?action:null;var r=new Rect(8,y,824,91);var color=accent??CandyHudVisuals.Lavender;
            if(index==selection)Panel(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),CandyHudVisuals.Mint);
            bool old=GUI.enabled;GUI.enabled=enabled;
            if(Button(r,"",CandyHudVisuals.Card)){selection=index;action?.Invoke();}
            Icon(new Rect(25,y+24,40,40),icon,color);GUI.Label(new Rect(84,y+10,715,31),name,heading);GUI.Label(new Rect(84,y+47,720,36),description,small);GUI.enabled=old;
        }
        void Note(float y,string text){GUI.Label(new Rect(10,y,821,80),text,small);}
        void PlayPage()
        {
            ActionCard(119,session.PlayingHand?CandyHudIcon.Survivor:CandyHudIcon.Hand,session.PlayingHand?"Become a survivor":"Become a giant hand",session.PlayingHand?"Spawn at the top and explore the castle.":"Fly around the tower and push its candy blocks.",()=>{session.SetRole(!session.PlayingHand);session.SetMenu(false);});
            ActionCard(223,CandyHudIcon.Home,"Back to the top","Respawn as a survivor on the highest upright block.",()=>{session.SetRole(false);session.SetMenu(false);});
            ActionCard(327,CandyHudIcon.Home,"Rebuild the castle",EditWorld?"Restore the whole tower and reposition the demo actors.":"The host controls the shared castle.",()=>{session.ResetMap();session.SetMenu(false);},EditWorld,CandyHudVisuals.Peach);
        }
        void BotsPage()
        {
            var bots=session.SurvivorBots;if(bots==null){Note(125,"Survivors are getting ready...");return;}
            ActionCard(119,CandyHudIcon.Survivor,"Add a survivor  ·  "+bots.Count+" / "+bots.Capacity,"Spawn a bot using the selected personality.",()=>bots.SpawnOne(),EditWorld&&bots.Count<bots.Capacity);
            ActionCard(223,CandyHudIcon.Friends,"Fill to "+bots.TargetPopulation+" players","Count human players too; fill the remaining survivor slots.",()=>bots.FillToTarget(),EditWorld);
            ActionCard(327,CandyHudIcon.Settings,"Next personality: "+bots.SelectedName,"Click to cycle the personality used for new survivors.",()=>bots.CyclePersonality(),EditWorld);
            ActionCard(431,CandyHudIcon.Survivor,"Clear bot survivors","Remove the survivor bots and stop automatic filling.",()=>bots.Clear(),EditWorld,CandyHudVisuals.Peach);Note(541,bots.Status+(EditWorld?"":"  Only the host can add or remove bots."));
        }
        void HandsPage()
        {
            var bots=session.HandBots;if(bots==null){Note(125,"Giant hands are getting ready...");return;}
            ActionCard(119,CandyHudIcon.Hand,"Add a giant hand  ·  "+bots.Count+" / "+bots.Capacity,"Spawn an AI hand with the current hand prefab and personality.",()=>bots.SpawnOne(),EditWorld&&bots.Count<bots.Capacity);
            ActionCard(223,CandyHudIcon.Settings,"Next personality: "+bots.SelectedName,"Click to cycle the behavior of the next hand you add.",()=>bots.CyclePersonality(),EditWorld);
            ActionCard(327,CandyHudIcon.Hand,"Clear AI hands","Remove the AI hands; keep the human players.",()=>bots.Clear(),EditWorld,CandyHudVisuals.Peach);Note(437,bots.Status+(EditWorld?"":"  Only the host can manage AI hands."));
        }
        void FriendsPage()
        {
            // Keep all discovery / pending / cancellation conditions owned by the network component.
            var color=GUI.backgroundColor;GUI.backgroundColor=CandyHudVisuals.Card;
            GUILayout.BeginArea(new Rect(10,113,816,515));if(session.Multiplayer!=null)session.Multiplayer.DrawMenu();else GUILayout.Label("Local discovery is starting...");GUILayout.EndArea();GUI.backgroundColor=color;
        }
        void SettingsPage()
        {
            ActionCard(119,CandyHudIcon.Help,"Gameplay hints: "+hintMode,"Adaptive fades back after a moment. Always keeps hints bright. Hidden removes them.",()=>{hintMode=(HintMode)(((int)hintMode+1)%3);changedAt=Time.unscaledTime;});
            GUI.Label(new Rect(12,230,820,29),"Hint opacity  ·  "+Mathf.RoundToInt(hintOpacity*100)+"%",body);hintOpacity=GUI.HorizontalSlider(new Rect(12,270,815,24),hintOpacity,.5f,1);
            Note(310,"Tip: F1 toggles gameplay hints. Use a larger Game view for a larger menu. Settings here apply to this running session.");
            var color=GUI.backgroundColor;GUI.backgroundColor=CandyHudVisuals.Card;
            GUILayout.BeginArea(new Rect(12,410,816,900));(session.Player as ISurvivorSettings)?.DrawSettings();GUILayout.EndArea();GUI.backgroundColor=color;
        }
        void ControlsPage()
        {
            var hints=Hints();for(int i=0;i<3;i++){Panel(new Rect(8,117+i*107,824,94),CandyHudVisuals.Card);Icon(new Rect(25,140+i*107,44,44),hints[i].icon,CandyHudVisuals.Mint);GUI.Label(new Rect(88,129+i*107,720,32),hints[i].key,heading);GUI.Label(new Rect(88,168+i*107,720,38),hints[i].text,body);}
            Note(451,!session.IsVR?"MENU: Esc opens / closes. Click a card, or use arrow keys + Enter. Mouse wheel scrolls. Tab switches survivor / giant hand during play. F8 opens Sound Studio.":"MENU: left Y opens / closes. Look at a button, then squeeze RIGHT trigger. With tracked hands, left thumb + middle finger toggles the menu; RIGHT pinch selects.");
            Note(557,"SURVIVOR: jump toward a block, then press jump again near its edge to vault. Hold jump near a wall to catch it; move to climb or shimmy, then release to drop. Rest on secure ground to recover grip.");
            Note(683,"GIANT HAND: desktop WASD flies, Q lowers, E rises, mouse steers. In VR, left stick flies, right stick turns, RIGHT trigger rises and LEFT trigger lowers. With bare hands, tracked fingers shape the hand; gestures do not replace flight-stick controls.");
            Note(815,"Input icons reflect the demo bindings. They are not remapping controls. A lost hand-tracking signal falls back to controller prompts; no extra gesture locomotion is implied.");
        }
        public struct Hint {public CandyHudIcon icon;public string key,text;public Hint(CandyHudIcon i,string k,string t){icon=i;key=k;text=t;}}
        public Hint[] Hints()
        {
            bool vr=session.IsVR,hands=vr&&TrackedHands;
            if(session.PlayingHand)return new[]{new Hint(vr?CandyHudIcon.Stick:CandyHudIcon.Move,vr?"L STICK":"W A S D","Fly around the castle"),new Hint(vr?CandyHudIcon.Trigger:CandyHudIcon.Flight,vr?"R / L TRIGGER":"E / Q",vr?"Rise / lower · R stick turns":"Rise / lower · mouse steers"),new Hint(hands?CandyHudIcon.Pinch:CandyHudIcon.Hand,hands?"TRACKED FINGERS":"POINTING HAND",hands?"Move and curl your real fingers":"Push blocks with your hand")};
            var moveIcon=hands?CandyHudIcon.Pinch:vr?CandyHudIcon.Stick:CandyHudIcon.Move;
            var jumpIcon=hands?CandyHudIcon.Pinch:vr?CandyHudIcon.ButtonA:CandyHudIcon.Key;
            if(Climbing)return new[]{new Hint(moveIcon,hands?"LEFT PINCH":vr?"L STICK":"W A S D",hands?"Climb upward":"Climb up, down or sideways"),new Hint(jumpIcon,hands?"HOLD RIGHT PINCH":vr?"HOLD A  ·  RIGHT":"HOLD SPACE","Keep your grip on the wall"),new Hint(jumpIcon,hands?"RELEASE RIGHT PINCH":vr?"RELEASE A":"RELEASE SPACE","Let go and drop")};
            return new[]{new Hint(moveIcon,hands?"LEFT PINCH":vr?"L STICK":"W A S D",hands?"Move forward":vr?"Move · headset looks around":"Move · mouse looks"),new Hint(jumpIcon,hands?"RIGHT PINCH":vr?"A  ·  RIGHT":"SPACE","Jump · press again at a ledge to vault"),new Hint(jumpIcon,hands?"HOLD RIGHT PINCH":vr?"HOLD A  ·  RIGHT":"HOLD SPACE","Catch a nearby wall and scramble")};
        }
        void BuildHints()
        {
            var go=new GameObject("Candy control hints • presentation",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup),typeof(CanvasScaler));go.transform.SetParent(transform,false);
            hintCanvas=go.GetComponent<Canvas>();hintCanvas.sortingOrder=20;hintGroup=go.GetComponent<CanvasGroup>();hintGroup.blocksRaycasts=false;hintGroup.interactable=false;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,800);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            hintRoot=go.GetComponent<RectTransform>();
            var role=Pill(go.transform,new Vector2(360,65),new Vector2(22,-20),new Vector2(0,1),new Color(.10f,.05f,.20f,.75f));
            roleText=Text(role,"",new Vector2(330,28),new Vector2(0,12),20);roleText.fontStyle=FontStyle.Bold;
            statusText=Text(role,"",new Vector2(330,25),new Vector2(0,-14),16);statusText.color=CandyHudVisuals.Muted;
            for(int i=0;i<3;i++)
            {
                var card=Pill(go.transform,new Vector2(370,65),new Vector2(22,38+(2-i)*73),new Vector2(0,0),new Color(.10f,.05f,.20f,.78f));
                var raw=new GameObject("Control icon",typeof(RectTransform),typeof(RawImage));raw.transform.SetParent(card,false);hintIcons[i]=raw.GetComponent<RawImage>();hintIcons[i].rectTransform.sizeDelta=new Vector2(35,35);hintIcons[i].rectTransform.anchoredPosition=new Vector2(-153,0);hintIcons[i].color=CandyHudVisuals.Mint;hintIcons[i].raycastTarget=false;
                hintKeys[i]=Text(card,"",new Vector2(292,25),new Vector2(24,13),18);hintKeys[i].fontStyle=FontStyle.Bold;
                hintNames[i]=Text(card,"",new Vector2(292,28),new Vector2(24,-13),16);
            }
            var menu=Pill(go.transform,new Vector2(245,42),new Vector2(-22,38),new Vector2(1,0),new Color(.10f,.05f,.20f,.78f));menuText=Text(menu,"",new Vector2(225,36),Vector2.zero,18);
        }
        RectTransform Pill(Transform parent,Vector2 size,Vector2 position,Vector2 anchor,Color color)
        {
            var go=new GameObject("Candy hint pill",typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.sprite=CandyHudVisuals.Pill;image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=false;
            var r=image.rectTransform;r.sizeDelta=size;r.anchorMin=r.anchorMax=anchor;r.pivot=anchor;r.anchoredPosition=position;return r;
        }
        Text Text(Transform parent,string value,Vector2 size,Vector2 position,int fontSize)
        {
            var go=new GameObject("Hint label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.text=value;text.fontSize=fontSize;text.alignment=TextAnchor.MiddleLeft;text.color=Color.white;text.raycastTarget=false;text.rectTransform.sizeDelta=size;text.rectTransform.anchoredPosition=position;return text;
        }
        bool viewVR;Camera hintCamera;
        void PresentHints()
        {
            if(!hintCanvas)BuildHints();var camera=session.ActiveCamera;
            if(viewVR!=session.IsVR||hintCamera!=camera||hintCanvas.renderMode==RenderMode.ScreenSpaceOverlay&&session.IsVR)
            {
                viewVR=session.IsVR;hintCamera=camera;
                if(viewVR){hintRoot.SetParent(camera.transform,false);hintCanvas.renderMode=RenderMode.WorldSpace;hintCanvas.worldCamera=camera;hintRoot.sizeDelta=new Vector2(1280,800);hintRoot.localPosition=new Vector3(0,0,1.25f);hintRoot.localRotation=Quaternion.identity;hintRoot.localScale=Vector3.one*.00085f;}
                else{hintRoot.SetParent(transform,false);hintCanvas.renderMode=RenderMode.ScreenSpaceOverlay;hintRoot.localScale=Vector3.one;}
            }
            string context=(session.PlayingHand?"HAND":Climbing?"CLIMB":Vaulting?"VAULT":"SURVIVOR")+InputLabel;
            if(context!=lastContext)
            {
                lastContext=context;changedAt=Time.unscaledTime;
                var hints=Hints();for(int i=0;i<3;i++){hintKeys[i].text=hints[i].key;hintNames[i].text=hints[i].text;hintIcons[i].texture=CandyHudVisuals.Icon(hints[i].icon);}
            }
            bool hidden=session.MenuOpen||studio&&studio.IsOpen||hintMode==HintMode.Hidden;
            float opacity=hidden?0:hintMode==HintMode.Always||Time.unscaledTime-changedAt<9||!session.PlayingHand&&Climbing?hintOpacity:hintOpacity*.62f;
            hintGroup.alpha=Mathf.MoveTowards(hintGroup.alpha,opacity,Time.unscaledDeltaTime*3);hintCanvas.enabled=hintGroup.alpha>.001f;
            roleText.text=session.PlayingHand?"GIANT HAND":"SURVIVOR";statusText.text=session.PlayingHand?InputLabel:session.Player.StatusText;
            menuText.text=session.IsVR?(TrackedHands?"L THUMB + MIDDLE  ·  MENU":"Y  ·  LEFT  /  MENU"):"ESC  Menu     F1  Hints";
        }
        void OnDisable(){if(hintCanvas)hintCanvas.enabled=false;if(studio)studio.ShowLauncher=true;}
        void OnDestroy(){if(hintCanvas)Destroy(hintCanvas.gameObject);if(themed)Destroy(themed);if(studio)studio.ShowLauncher=true;}
    }
}
