using System;
using UnityEngine;
using DeadlyJenga.Presentation;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DeadlyJenga.CandyAudio;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Demo presentation only: every action and value goes through Core (ISessionInfo, its Demo, Social and
    // Multiplayer controls, ISurvivorSettings and IMovementState). Nothing here changes gameplay directly.
    [DefaultExecutionOrder(250)]
    [DisallowMultipleComponent]
    public sealed partial class CandyDemoHud : MonoBehaviour
    {
        public enum HintMode { Adaptive, Always, Hidden }
        public HintMode hintMode=HintMode.Adaptive;
        [Range(.5f,1)] public float hintOpacity=.86f;
        ISessionInfo session;CandyAudioStudio studio;
        IPlayableCharacter Active=>session.PlayingHand?session.Hand:session.Player;
        IMovementState Move=>session.Player as IMovementState;
        ISurvivorSettings Settings=>session.Player as ISurvivorSettings;
        IDemoControls Demo=>session.Demo;
        bool Climbing=>Move!=null&&Move.Climbing;
        string DemoStatus=>Demo!=null?Demo.Status:"";
        string DemoAccessReason=>CanCommand?"Demo controls enabled":GameExperiencePanel.Resolve(session)?.Experience==GameExperience.Game?"GAME: enable Demo controls to use testing tools":session.Multiplayer?.IsClient==true?"Host permission required for Demo controls":"Demo controls temporarily unavailable while the room changes";
        void EnableDemoControls()
        {
            var controls=GameExperiencePanel.Resolve(session);
            if(GameExperiencePanel.CanEnableDemo(controls)){controls.SetExperience(GameExperience.Demo);commandFeedback="Requested Demo controls. Waiting for room confirmation.";feedbackUntil=Time.unscaledTime+6;}
            else GameExperiencePanel.Open(session);
        }
        int page,selection;Vector2 scroll;bool lastMenu;float nextHintSample;string lastContext="";
        [Flags]
        enum BasicAction { None = 0, Look = 1, Move = 2, Jump = 4, All = Look | Move | Jump }
        BasicAction completedActions;
        string introKey;
        Transform introPlayer;
        Camera introCamera;
        Quaternion lookStart;
        Vector3 lastMovePosition;
        float walkedDistance;
        bool introWasGrounded;
        RectTransform hintStrip;
        readonly RectTransform[] hintRows = new RectTransform[3];
        float uiSize=1;GUIStyle title,heading,cardHeading,body,small,button,compactButton,box;GUISkin themed;bool ready;
        Canvas hintCanvas;CanvasGroup hintGroup;Text roleText,statusText,menuText;
        readonly Text[] hintKeys=new Text[3],hintNames=new Text[3];readonly RawImage[] hintIcons=new RawImage[3];
        readonly System.Collections.Generic.List<Action> navActions=new System.Collections.Generic.List<Action>();int navCount,drawIndex;bool activate;
        readonly System.Collections.Generic.List<float> navTops=new System.Collections.Generic.List<float>();
        bool HasFriends=>session!=null&&session.Multiplayer!=null;
        bool DemoMode=>Demo!=null&&(Demo.DemoMode||Demo.CanCommand);
        string disasterStatus="";
        string commandFeedback="";float feedbackUntil;
        void EquipmentAction(Func<bool> command)
        {
            bool success=command();
            commandFeedback=success?"Equipment applied.":"Unavailable: use a living survivor, leave spectating, check host access, and leave space for pickups.";
            feedbackUntil=Time.unscaledTime+6;
            if(success)session.SetMenu(false);
        }
        int PageCount=>4;
        string PageName(int index)=>DemoMenuSections.Tabs[Mathf.Clamp(index,0,3)];
        CandyHudIcon PageIcon(int index)=>index==0?CandyHudIcon.Survivor:index==1?CandyHudIcon.Friends:index==2?CandyHudIcon.Home:CandyHudIcon.Settings;
        bool ControlsPageOpen=>SectionName=="Controls";
        public bool GameplayHintsVisible=>hintGroup&&hintGroup.alpha>.01f;
        public Canvas ControlHintCanvas=>hintCanvas;
        public string InputLabel=>session==null||session.Player==null?"Keyboard + mouse":session.IsVR?(TrackedHands?"Hand tracking":"VR controllers"):"Keyboard + mouse";
        bool TrackedHands=>Active!=null&&Active.HasHandTracking;
        bool Ready=>session!=null&&session.Player!=null&&session.Hand!=null;
        void Awake(){session=GetComponent<ISessionInfo>();}
        void Update()
        {
            if(!Ready || (session as ILobbyMatchVisitControls)?.VisitingLobby==true ||
                MatchRuntime.Current?.Frame.phase==RoundPhase.Results ||
                GameExperiencePanel.Showing(session)||CandyComfortPanel.Showing(session)||CharacterPicker.Showing(session))
            {
                if(hintCanvas)hintCanvas.enabled=false;
                introPlayer=null;
                return;
            }
            if(!studio)studio=FindFirstObjectByType<CandyAudioStudio>();
            if(studio)studio.ShowLauncher=false;
            if(!session.MenuOpen&&lastMenu)MultiplayerPanel.ClearLeaveConfirmation();
            if(session.MenuOpen&&!lastMenu){selection=0;desktopWheelSection=null;}
            lastMenu=session.MenuOpen;
            var k=Keyboard.current;
            if(k!=null&&session.MenuOpen&&!session.IsVR&&!(studio&&studio.IsOpen))
            {
                if(k.leftArrowKey.wasPressedThisFrame)ChangePage((page+PageCount-1)%PageCount);
                if(k.rightArrowKey.wasPressedThisFrame)ChangePage((page+1)%PageCount);
                if(k.tabKey.wasPressedThisFrame)
                {
                    int count=DemoMenuSections.Sections[page].Length;
                    SelectSection((subsection+1)%count);
                }
                if(k.upArrowKey.wasPressedThisFrame){selection=(selection+Mathf.Max(1,navCount)-1)%Mathf.Max(1,navCount);FollowSelection();}
                if(k.downArrowKey.wasPressedThisFrame){selection=(selection+1)%Mathf.Max(1,navCount);FollowSelection();}
                if(k.enterKey.wasPressedThisFrame||k.numpadEnterKey.wasPressedThisFrame)activate=true;
            }
            if(k!=null&&!session.MenuOpen&&k.f1Key.wasPressedThisFrame){hintMode=hintMode==HintMode.Hidden?HintMode.Adaptive:HintMode.Hidden;if(hintMode==HintMode.Adaptive)ReplayIntro();}
            PresentHints();
        }
        void ChangePage(int next){page=next;SelectSection(0);}
        void FollowSelection(){float top=selection<navTops.Count?navTops[selection]:116+selection*104;if(top<scroll.y)scroll.y=top;if(top+100>scroll.y+404)scroll.y=top+100-404;}
        void Styles()
        {
            if(ready)return;ready=true;var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title=new GUIStyle(GUI.skin.label){font=font,fontSize=30,fontStyle=FontStyle.Bold,normal={textColor=Color.white}};
            heading=new GUIStyle(title){fontSize=22};body=new GUIStyle(GUI.skin.label){font=font,fontSize=19,wordWrap=true,normal={textColor=Color.white}};
            cardHeading=new GUIStyle(heading){fontSize=19,wordWrap=true};
            small=new GUIStyle(body){fontSize=18,normal={textColor=new Color(.90f,.87f,.96f)}};
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=19,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(14,12,8,8),border=new RectOffset(10,10,10,10),normal={background=CandyHudVisuals.Rounded,textColor=Color.white},hover={background=CandyHudVisuals.Rounded,textColor=Color.white},active={background=CandyHudVisuals.Rounded,textColor=Color.white}};
            compactButton=new GUIStyle(button){fontSize=18,padding=new RectOffset(8,8,3,3)};
            box=new GUIStyle(GUI.skin.box){font=font,fontSize=19,wordWrap=true,border=new RectOffset(10,10,10,10),normal={background=CandyHudVisuals.Rounded,textColor=Color.white}};
            button.padding=new RectOffset(14,12,2,2);button.wordWrap=true;
            themed=Instantiate(GUI.skin);themed.hideFlags=HideFlags.HideAndDontSave;themed.font=font;themed.label=body;themed.button=new GUIStyle(button);themed.box=box;
            foreach(var style in themed.customStyles)style.font=font;
            themed.toggle.font=font;themed.toggle.fontSize=19;themed.toggle.normal.textColor=Color.white;
            foreach(var state in new[]{themed.toggle.normal,themed.toggle.hover,themed.toggle.active,themed.toggle.focused,themed.toggle.onNormal,themed.toggle.onHover,themed.toggle.onActive,themed.toggle.onFocused})state.textColor=Color.white;
            themed.textField=new GUIStyle(button){fontStyle=FontStyle.Normal};
            foreach(var state in new[]{themed.button.normal,themed.button.hover,themed.button.active,themed.button.focused,themed.textField.normal,themed.textField.hover,themed.textField.active,themed.textField.focused}){state.textColor=Color.white;state.background=CandyHudVisuals.Rounded;}
        }
        void OnGUI()
        {
            if(MenuAim.InputConsumed||GameExperiencePanel.Showing(session)||CandyComfortPanel.Showing(session)||CharacterPicker.Showing(session)||DeadlyJenga.Presentation.CandyWildlifePanel.Showing(session))return;
            if((session as ILobbyMatchVisitControls)?.VisitingLobby==true)return;
            DrawSpectatorLabels();
            if(MatchRuntime.Current?.Frame.phase==RoundPhase.Results){if(hintCanvas)hintCanvas.enabled=false;return;}
            // Screen GUI is invisible in a headset and allocates every frame (garbage collection hitches), so skip it in VR.
            if(Ready&&session.IsVR)return;
            if(Ready&&!session.MenuOpen&&session.Social!=null&&session.Social.ShowCaption)SocialPanel.DrawCaption(session.Social);
            if(!Ready||session.IsVR||!session.MenuOpen||studio&&studio.IsOpen)return;
            Styles();var old=GUI.matrix;var skin=GUI.skin;var tint=GUI.color;var contentTint=GUI.contentColor;var backgroundTint=GUI.backgroundColor;bool enabled=GUI.enabled;int depth=GUI.depth;
            GUI.color=GUI.contentColor=GUI.backgroundColor=Color.white;GUI.enabled=true;
            try
            {
                float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f)*uiSize;
                GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-800*scale)/2),Quaternion.identity,Vector3.one*scale);GUI.depth=-100;GUI.skin=themed;
                GUI.ModalWindow(GetInstanceID(),new Rect(40,32,1200,736),DrawMenu,GUIContent.none,GUIStyle.none);
            }
            finally{GUI.matrix=old;GUI.skin=skin;GUI.color=tint;GUI.contentColor=contentTint;GUI.backgroundColor=backgroundTint;GUI.enabled=enabled;GUI.depth=depth;}
        }
        void Panel(Rect r,Color c){var old=GUI.backgroundColor;GUI.backgroundColor=c;GUI.Box(r,GUIContent.none,box);GUI.backgroundColor=old;}
        void Icon(Rect r,CandyHudIcon icon,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,CandyHudVisuals.Icon(icon));GUI.color=old;}
        bool Button(Rect r,string text,Color color)
        {var old=GUI.backgroundColor;GUI.backgroundColor=color;var foreground=color.grayscale>.5f?CandyHudVisuals.Ink:Color.white;var style=r.height<=34?compactButton:button;style.normal.textColor=style.hover.textColor=style.active.textColor=style.focused.textColor=foreground;bool result=GUI.Button(r,text,style);GUI.backgroundColor=old;return result;}
        void DrawMenu(int id)
        {
            page=Mathf.Clamp(page,0,PageCount-1);
            Panel(new Rect(0,0,1200,736),CandyHudVisuals.Ink);Panel(new Rect(0,0,1200,8),CandyHudVisuals.Lavender);
            Icon(new Rect(26,27,48,48),session.PlayingHand?CandyHudIcon.Hand:CandyHudIcon.Home,CandyHudVisuals.Mint);
            if(GameExperiencePanel.Resolve(session)!=null){if(Button(new Rect(93,23,470,45),"GAME / DEMO - ROUND SETUP",CandyHudVisuals.Card))GameExperiencePanel.Open(session);}
            else GUI.Label(new Rect(93,23,470,45),"DEADLY JENGA  /  DEMO",title);
            GUI.Label(new Rect(95,71,775,22),(session.PlayingHand?"GIANT HAND":"SURVIVOR")+"   ·   "+InputLabel+"   ·   "+DemoAccessReason,small);
            if(Button(new Rect(963,28,207,51),"Resume  ·  Esc",CandyHudVisuals.Mint))session.SetMenu(false);
            if(Button(new Rect(26,94,218,28),CanCommand?"DEMO CONTROLS: ON":"ENABLE DEMO CONTROLS",CanCommand?CandyHudVisuals.Mint:CandyHudVisuals.Peach))EnableDemoControls();
            if(Button(new Rect(256,94,220,28),"CONTROLS / HAND",CandyHudVisuals.Lavender))CandyComfortPanel.Open(session);
            var inspection=CandyThirdPersonInspection.For(session);
            if(DemoMode){bool enabled=GUI.enabled;GUI.enabled=inspection?.CanInspect==true;if(Button(new Rect(963,94,207,28),"Third person",CandyHudVisuals.Lavender))inspection.Begin();GUI.enabled=enabled;}
            if(Button(new Rect(748,28,201,51),MultiplayerPanel.LobbyAction(session.Multiplayer),CandyHudVisuals.Peach))MatchRuntime.Current?.ReturnToLobby();
            string joinCode=MultiplayerPanel.JoinCode(session.Multiplayer);
            if(joinCode.Length>0&&Button(new Rect(488,94,248,28),"Code: "+joinCode+" / Copy",CandyHudVisuals.Card))GUIUtility.systemCopyBuffer=joinCode;
            var review=MatchRuntime.Current as IMatchReviewControls;
            if(review?.DevelopmentReviewEnabled==true)
            {bool enabled=GUI.enabled;GUI.enabled=review.CanEndGame;if(Button(new Rect(572,28,164,51),MultiplayerPanel.EndAction(session.Multiplayer),CandyHudVisuals.Lavender))review.EndGame();GUI.enabled=enabled;}
            if(HandLooks.Names.Length>0&&Button(new Rect(748,94,201,28),"Hand Appearance",CandyHudVisuals.Card))CharacterPicker.Open(session);
            for(int i=0;i<PageCount;i++)
            {
                bool pending=i==3&&session.Multiplayer!=null&&session.Multiplayer.HasPending;
                var r=new Rect(20,122+i*77,218,67);if(Button(r,"",i==page?new Color(.40f,.28f,.58f):CandyHudVisuals.Panel))ChangePage(i);
                GUI.Label(new Rect(r.x+50,r.y+5,160,28),PageName(i)+(pending?"  !":""),heading);
                GUI.Label(new Rect(r.x+50,r.y+33,160,24),DemoMenuSections.TabDescriptions[i],small);
                Icon(new Rect(r.x+10,r.y+24,25,25),PageIcon(i),i==page?CandyHudVisuals.Mint:CandyHudVisuals.Lavender);
            }
            DrawSectionSelector();
            Panel(new Rect(258,244,921,430),CandyHudVisuals.Panel);drawIndex=0;
            scroll=GUI.BeginScrollView(new Rect(278,254,884,404),scroll,new Rect(0,0,856,SectionHeight));
            var picture=DesktopEquipmentWheelVisible?null:SectionPicture();
            float headerWidth=picture?610:830;
            GUI.Label(new Rect(8,2,headerWidth,36),DesktopEquipmentWheelVisible?"EQUIPMENT WHEEL":PageName(page)+" / "+SectionName,heading);
            string description=DesktopEquipmentWheelVisible?"Select an icon, then Give. Current respawn grant: "+EquipmentSpecifications.Get((MatchRuntime.Current as IDemoLoadoutControls)?.RespawnEquipment??EquipmentKind.None).name:PageDescription();
            GUI.Label(new Rect(8,45,headerWidth,DesktopEquipmentWheelVisible?24:60),description,small);
            if(picture)GUI.DrawTexture(new Rect(650,2,177,106),picture,ScaleMode.ScaleToFit);
            DrawSection();
            GUI.EndScrollView();navCount=drawIndex;if(navCount>0)selection=Mathf.Clamp(selection,0,navCount-1);
            GUI.Label(new Rect(26,692,1150,30),Time.unscaledTime<feedbackUntil?commandFeedback:"Tab: section   ·   ←/→: tab   ·   ↑/↓: action   ·   Enter: select   ·   Esc: resume",small);
            if(activate&&Event.current.type==EventType.Repaint){activate=false;if(selection<navCount)navActions[selection]?.Invoke();}
        }
        string PageTitle()=>SectionName=="Spectate"?"Watch the match":SectionName=="Maps"?"Change map":SectionName=="Disasters"?"Natural disasters":SectionName=="Modes"?"Game modes and world rules":SectionName=="Play"?"Make a little mayhem":SectionName=="Voice"?"AI players and voice":SectionName=="Survivors"?"Bring the island to life":SectionName=="Giant hands"?"A few extra hands":SectionName=="Settings"?"Your local controls":SectionName=="Friends"?"Multiplayer session":"A little guide to surviving the chaos";
        string PageDescription()=>SectionName=="Character"?"Your character, viewpoint, equipment hand and local testing controls.":SectionName=="Equipment"||SectionName=="Bot equipment"?"Filter the arsenal, choose the target and select an item. Shared grants require the host.":SectionName=="World"?"Rebuild the map, deliver structures or finish the round.":SectionName=="Spectate"?"Use the same eye pose as the shared survivor character, with no commands sent to the watched player.":SectionName=="Maps"?((Demo as IDemoMapControls)?.MapStatus??"Maps are unavailable."):SectionName=="Disasters"?(!string.IsNullOrEmpty(DemoStatus)?DemoStatus:string.IsNullOrEmpty(disasterStatus)?"Tune a disaster below its button, then launch it. Ice toggles on / off. New settings apply on the next launch.":disasterStatus):SectionName=="Voice"?"Microphone is opt-in. Each AI keeps its own voice and volume.":SectionName=="Survivors"||SectionName=="Giant hands"?"The host manages the shared AI population.":SectionName=="Settings"?"Tune local controls while keeping lobby match setup available.":SectionName=="Play"?"Switch roles, get back on top, or start the tower over.":SectionName=="Friends"?"Your current connection and session controls.":"Hints match the inputs this demo actually supports.";
        bool EditWorld=>session.CanEditWorld;
        bool CanCommand=>Demo!=null&&Demo.CanCommand;
        void ActionCard(float y,CandyHudIcon icon,string name,string description,Action action,bool enabled=true,Color? accent=null,Texture preview=null)
        {
            int index=drawIndex++;if(index>=navTops.Count)navTops.Add(y);else navTops[index]=y;if(index>=navActions.Count)navActions.Add(null);navActions[index]=enabled?action:null;var r=new Rect(8,y,824,91);var color=accent??CandyHudVisuals.Lavender;
            if(index==selection)Panel(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),CandyHudVisuals.Mint);
            bool old=GUI.enabled;GUI.enabled=old&&enabled;
            if(Button(r,"",CandyHudVisuals.Card)){selection=index;action?.Invoke();}
            GUI.enabled=old;
            float width=preview?555:715;bool wrap=heading.CalcSize(new GUIContent(name)).x>width;
            Icon(new Rect(25,y+24,40,40),icon,color);GUI.Label(new Rect(84,y+(wrap?1:8),width,wrap?44:32),name,wrap?cardHeading:heading);GUI.Label(new Rect(84,y+(wrap?46:42),preview?555:720,wrap?42:46),description,small);if(preview)GUI.DrawTexture(new Rect(665,y+8,154,75),preview,ScaleMode.ScaleToFit);
        }
        void Note(float y,string text){GUI.Label(new Rect(10,y,821,80),text,small);}
        bool giveEquipmentPage;
        void PlayPage()
        {
            if(giveEquipmentPage&&DemoMode){GiveEquipmentPage();return;}
            var match=MatchRuntime.Current;bool watching=false;if(match?.Frame!=null)foreach(var row in match.Frame.players)if(row.id==match.LocalActorId&&row.role=="spectator")watching=true;
            if(watching){ActionCard(119,CandyHudIcon.Survivor,"Previous survivor","Switch to another living player's view.",()=>{match.NextSpectator(-1);session.SetMenu(false);});ActionCard(223,CandyHudIcon.Survivor,"Next survivor","Your team is shown while teammates remain alive.",()=>{match.NextSpectator(1);session.SetMenu(false);});return;}

            ActionCard(119,session.PlayingHand?CandyHudIcon.Survivor:CandyHudIcon.Hand,session.PlayingHand?"Become Survivor":"Become Flying Hand",session.PlayingHand?"Spawn at the top and explore the castle.":"Fly around the tower and push its candy blocks.",()=>{session.SetRole(!session.PlayingHand);session.SetMenu(false);});
            ActionCard(223,CandyHudIcon.Home,"Back to the top","Respawn as a survivor on the highest upright block.",()=>{session.SetRole(false);session.SetMenu(false);});
            ActionCard(327,CandyHudIcon.Home,"Rebuild the castle",EditWorld?"Restore the whole tower and reposition the demo actors.":"The host controls the shared castle.",()=>{session.ResetMap();session.SetMenu(false);},EditWorld,CandyHudVisuals.Peach);
            if (Demo is IBuildUpControls buildUp && buildUp.Available)
                ActionCard(431,CandyHudIcon.Play,buildUp.Delivering?"Delivering the next tier...":buildUp.ClimbSecondsRemaining>0?"Climb window · "+Mathf.CeilToInt(buildUp.ClimbSecondsRemaining)+" seconds":buildUp.Stage>=buildUp.StageCount-1?"All scaffold tiers delivered":"Deliver next scaffold tier",
                    "Modules descend and snap to the grid. Grates stay climbable for 20 seconds after delivery.",()=>{buildUp.AdvanceStage();session.SetMenu(false);},buildUp.CanAdvance,CandyHudVisuals.Mint);
            if(DemoMode)
            {
                bool can=CanCommand&&!session.PlayingHand&&(Demo as ISpectatorControls)?.Spectating!=true;
                ActionCard(535,CandyHudIcon.Play,"Give weapons / powerups · myself or everyone","Choose an item for yourself or all survivor players and bots, for this life or every respawn.",()=>{giveEquipmentPage=true;scroll=Vector2.zero;},can&&match is IDemoEquipmentControls,CandyHudVisuals.Mint);
                ActionCard(639,CandyHudIcon.Play,"Spawn 2x speed orb","20 seconds of double speed and unlimited stamina. Walk into the glass ball.",()=>{Demo.Run(DemoCommand.SpawnSpeedPowerup);session.SetMenu(false);},can,CandyHudVisuals.Mint);
                ActionCard(743,CandyHudIcon.Play,"Spawn 3x jump orb","20 seconds of triple jump height and unlimited stamina. Recollect to refresh.",()=>{Demo.Run(DemoCommand.SpawnJumpPowerup);session.SetMenu(false);},can,CandyHudVisuals.Lavender);
                var gun=session.Player.gameObject.GetComponent<IBlasterControls>();var equipment=session.Player.gameObject.GetComponent<IEquipmentControls>();
                ActionCard(847,CandyHudIcon.Play,"BLASTER TESTING: "+(gun?.TestingEnabled==true?"ON":"OFF"),"Enable plasma weapons for this Demo session. Host controls the testing toggle.",()=>gun.SetTestingEnabled(!gun.TestingEnabled),gun!=null&&MatchRuntime.Current?.Authority==true,CandyHudVisuals.Mint);
                ActionCard(951,CandyHudIcon.Hand,equipment?.Equipment.drawn==true?"Holster weapon":"Draw weapon","V toggles equipment. In VR use Grip + left Y or right B; trigger fires. Drop equipment with Delete or the Drop menu action.",()=>equipment.SetEquipped(!equipment.Equipment.drawn),equipment?.Equipment.kind>=EquipmentKind.Blaster);
                ActionCard(1055,CandyHudIcon.Settings,"DOMINANT HAND: "+(gun?.LeftHanded==true?"LEFT":"RIGHT"),"Sets the weapon hand and the movement stick.",()=>gun.SetLeftHanded(!gun.LeftHanded),gun!=null);
                int weaponRow=0;
                foreach(EquipmentKind kind in new[]{EquipmentKind.Blaster,EquipmentKind.MurderSword,EquipmentKind.SheriffHammer,EquipmentKind.DualRifle,EquipmentKind.BoxingGlove,EquipmentKind.PoweredArmor})
                {
                    var item=kind;ActionCard(1159+weaponRow++*104,CandyHudIcon.Play,"Spawn "+EquipmentNames(item),"One weapon or powerup per player. Walk near it and confirm a swap.",()=>{(match as IEquipmentRuntime)?.SpawnEquipment(item);session.SetMenu(false);},can&&match?.Authority==true);
                }
                Note(1790,Demo.Status);
                var inspection=CandyThirdPersonInspection.For(session);
                ActionCard(1895,CandyHudIcon.Survivor,"Inspect your character in third person","Leave your character still. Drag to orbit, scroll to zoom; V returns to first person.",()=>inspection.Begin(),inspection!=null&&inspection.CanInspect);
            }
            if(match is IPowerupSpawnControls)
                ActionCard(DemoMode?2010:535,CandyHudIcon.Settings,"Random block powerup settings","Spawn interval, active limit, lifetime, and weighted types / percentages.",()=>DeadlyJenga.Presentation.CandyPowerupSpawnPanel.Open());
        }
        bool giveAllEquipment,repeatEquipment;
        bool DesktopEquipmentWheelVisible=>SectionName=="Equipment"||SectionName=="Bot equipment"||giveEquipmentPage;
        int desktopWheelCategory,desktopWheelOffset;
        EquipmentKind desktopWheelSelected=EquipmentKind.Blaster;
        string desktopWheelSection;
        GUIStyle desktopWheelName,desktopWheelCenter;
        static readonly string[] DesktopWheelCategories={"RANGED","MELEE","POWERUPS"};
        static readonly EquipmentKind[][] DesktopWheelItems={BuildDesktopWheelItems(0),BuildDesktopWheelItems(1),BuildDesktopWheelItems(2)};
        static readonly Vector2[] DesktopWheelCenters={new Vector2(270,110),new Vector2(378.253f,172.5f),new Vector2(378.253f,297.5f),new Vector2(270,360),new Vector2(161.747f,297.5f),new Vector2(161.747f,172.5f)};
        static EquipmentKind[] BuildDesktopWheelItems(int category)
        {
            var items=new System.Collections.Generic.List<EquipmentKind>();
            for(int i=1;i<EquipmentSpecifications.All.Length;i++)
            {
                var kind=(EquipmentKind)i;var specification=EquipmentSpecifications.Get(kind);
                bool power=specification.power||kind==EquipmentKind.PoweredArmor;
                if(category==2?power:category==1?!power&&specification.melee:!power&&!specification.melee)items.Add(kind);
            }
            return items.ToArray();
        }
        void DesktopWheelButton(Rect rect,string text,Action action,bool enabled=true,Color? accent=null)
        {
            int index=drawIndex++;
            if(index>=navTops.Count)navTops.Add(0);else navTops[index]=0;
            if(index>=navActions.Count)navActions.Add(null);navActions[index]=enabled?action:null;
            if(index==selection)Panel(new Rect(rect.x-2,rect.y-2,rect.width+4,rect.height+4),CandyHudVisuals.Mint);
            bool previous=GUI.enabled;GUI.enabled=previous&&enabled;
            if(Button(rect,text,accent??CandyHudVisuals.Card)){selection=index;action?.Invoke();}
            GUI.enabled=previous;
        }
        static void DesktopWheelPicture(Rect box,EquipmentKind kind,bool alternate=false)
        {
            var sprite=CandyWeaponIcons.Get(kind,alternate);var previous=GUI.color;GUI.color=Color.white;
            if(sprite)
            {
                var rect=sprite.rect;float scale=Mathf.Min(box.width/rect.width,box.height/rect.height);
                var fit=new Rect(box.center.x-rect.width*scale*.5f,box.center.y-rect.height*scale*.5f,rect.width*scale,rect.height*scale);
                GUI.DrawTextureWithTexCoords(fit,sprite.texture,new Rect(rect.x/sprite.texture.width,rect.y/sprite.texture.height,rect.width/sprite.texture.width,rect.height/sprite.texture.height));
            }
            else
            {
                var icon=kind==EquipmentKind.Jump?CandyHudIcon.Jump:kind==EquipmentKind.Speed?CandyHudIcon.Move:kind==EquipmentKind.FlyingHand?CandyHudIcon.Hand:CandyHudIcon.Key;
                GUI.color=kind==EquipmentKind.Jump?new Color(.25f,1,.3f):kind==EquipmentKind.Speed?new Color(.25f,.7f,1):CandyHudVisuals.Mint;
                GUI.DrawTexture(box,CandyHudVisuals.Icon(icon),ScaleMode.ScaleToFit);
            }
            GUI.color=previous;
        }
        void GiveEquipmentPage()
        {
            if(desktopWheelSection!=SectionName)
            {
                desktopWheelSection=SectionName;giveAllEquipment=SectionName=="Bot equipment";
                repeatEquipment=false;desktopWheelCategory=desktopWheelOffset=0;desktopWheelSelected=EquipmentKind.Blaster;scroll=Vector2.zero;
            }
            desktopWheelName??=new GUIStyle(cardHeading){fontSize=20,wordWrap=true};
            desktopWheelCenter??=new GUIStyle(small){fontSize=14,alignment=TextAnchor.MiddleCenter,wordWrap=true};
            var runtime=MatchRuntime.Current;var self=runtime as IDemoEquipmentControls;var everyone=runtime as IDemoLoadoutControls;
            bool host=DemoMode&&CanCommand&&runtime?.Authority==true;
            bool canSelf=host&&!session.PlayingHand&&(Demo as ISpectatorControls)?.Spectating!=true&&self!=null;
            bool canEveryone=host&&everyone!=null&&desktopWheelSelected!=EquipmentKind.FlyingHand;
            if(!giveAllEquipment||desktopWheelSelected==EquipmentKind.FlyingHand)repeatEquipment=false;
            var items=DesktopWheelItems[desktopWheelCategory];int pages=Mathf.Max(1,(items.Length+5)/6);
            desktopWheelOffset=Mathf.Clamp(desktopWheelOffset/6,0,pages-1)*6;
            for(int category=0;category<3;category++)
            {
                int chosen=category;DesktopWheelButton(new Rect(470+category*116,82,110,32),DesktopWheelCategories[category],()=>{desktopWheelCategory=chosen;desktopWheelOffset=0;desktopWheelSelected=DesktopWheelItems[chosen][0];},true,desktopWheelCategory==category?CandyHudVisuals.Lavender:CandyHudVisuals.Card);
            }
            string pointed="";
            for(int i=0;i<6;i++)
            {
                int at=desktopWheelOffset+i;if(at>=items.Length)continue;var item=items[at];var center=DesktopWheelCenters[i];
                var card=new Rect(center.x-52,center.y-37,104,74);bool hover=card.Contains(Event.current.mousePosition);
                DesktopWheelButton(card,"",()=>desktopWheelSelected=item,true,hover?CandyHudVisuals.Mint:item==desktopWheelSelected?CandyHudVisuals.Lavender:CandyHudVisuals.Card);
                DesktopWheelPicture(new Rect(card.x+4,card.y+4,96,66),item);
                if(hover)pointed=EquipmentSpecifications.Get(item).name;
            }
            Panel(new Rect(216,184,108,102),CandyHudVisuals.Ink);
            bool alternate=desktopWheelSelected==EquipmentKind.DualRifle&&Mathf.FloorToInt(Time.unscaledTime/1.5f)%2==1;
            DesktopWheelPicture(new Rect(224,190,92,52),desktopWheelSelected,alternate);
            string centerName=desktopWheelSelected==EquipmentKind.DualRifle?"DUAL RIFLE\n"+(alternate?"SHOTGUN VIEW":"RIFLE VIEW"):EquipmentSpecifications.Get(desktopWheelSelected).name;
            GUI.Label(new Rect(220,246,100,36),centerName,desktopWheelCenter);
            GUI.Label(new Rect(470,123,350,28),string.IsNullOrEmpty(pointed)?EquipmentSpecifications.Get(desktopWheelSelected).name:pointed,desktopWheelName);
            bool canGive=giveAllEquipment?canEveryone:canSelf;
            string giveTitle=!host?"HOST / DEMO ACCESS REQUIRED":giveAllEquipment&&desktopWheelSelected==EquipmentKind.FlyingHand?"FLYING HAND: SELF ONLY":"GIVE SELECTED / "+(giveAllEquipment?"EVERYONE":"MYSELF");
            DesktopWheelButton(new Rect(470,157,350,51),giveTitle,()=>
            {
                bool accepted=giveAllEquipment?everyone.GiveEveryoneEquipment(desktopWheelSelected,repeatEquipment):self.GiveEquipment(desktopWheelSelected);
                commandFeedback=accepted?"Equipment request accepted.":"Request not completed: current rules or eligible recipients may have changed.";
                feedbackUntil=Time.unscaledTime+6;if(accepted)session.SetMenu(false);
            },canGive,CandyHudVisuals.Mint);
            DesktopWheelButton(new Rect(470,219,350,35),"Target: "+(giveAllEquipment?"EVERYONE":"MYSELF"),()=>{giveAllEquipment=!giveAllEquipment;if(!giveAllEquipment)repeatEquipment=false;},host&&everyone!=null);
            DesktopWheelButton(new Rect(470,262,350,35),giveAllEquipment?repeatEquipment?"NOW + EVERY RESPAWN":"THIS LIFE ONLY":"SELF / THIS LIFE ONLY",()=>repeatEquipment=!repeatEquipment,host&&everyone!=null&&giveAllEquipment&&desktopWheelSelected!=EquipmentKind.FlyingHand);
            DesktopWheelButton(new Rect(470,305,350,35),"STOP EVERYONE RESPAWN GRANTS",()=>
            {
                bool accepted=everyone.GiveEveryoneEquipment(EquipmentKind.None,false);
                commandFeedback=accepted?"Respawn grant stop accepted.":"Stop request not accepted; check the actual respawn grant above.";
                feedbackUntil=Time.unscaledTime+6;
            },host&&everyone!=null&&everyone.RespawnEquipment!=EquipmentKind.None,CandyHudVisuals.Peach);
            DesktopWheelButton(new Rect(470,352,100,35),"< PREV",()=>{desktopWheelOffset-=6;desktopWheelSelected=items[desktopWheelOffset];},desktopWheelOffset>0);
            GUI.Label(new Rect(580,352,130,35),(desktopWheelOffset/6+1)+" / "+pages,desktopWheelCenter);
            DesktopWheelButton(new Rect(720,352,100,35),"NEXT >",()=>{desktopWheelOffset+=6;desktopWheelSelected=items[desktopWheelOffset];},desktopWheelOffset+6<items.Length);
            DesktopWheelButton(new Rect(8,350,100,35),"BACK",()=>SelectSection(0));
        }
        static string EquipmentNames(EquipmentKind item)=>item==EquipmentKind.PoweredArmor?"powered iron suit":item==EquipmentKind.DualRifle?"dual rifle / shotgun":item==EquipmentKind.MurderSword?"murderer sword":item==EquipmentKind.SheriffHammer?"sheriff revolver":item==EquipmentKind.BoxingGlove?"giant boxing glove":"alien blaster";
        readonly MapCategoryPicker demoMapPicker=new MapCategoryPicker();
        float MapContentHeight()
        {
            var maps=Demo as IDemoMapControls;demoMapPicker.Update(maps?.Maps,maps?.CurrentMapId);
            return 200+demoMapPicker.Rows.Count*104;
        }
        void MapsPage()
        {
            if(!(Demo is IDemoMapControls maps)){Note(119,"Maps are unavailable.");return;}
            demoMapPicker.Update(maps.Maps,maps.CurrentMapId);
            float y=119;MapCategoryPicker.Row toggle=null;
            foreach(var row in demoMapPicker.Rows)
            {
                var item=row;
                if(item.map==null)
                    ActionCard(y,CandyHudIcon.Home,demoMapPicker.Title(item),item.collection?(item.experimental?"Studies, prototypes and unfinished maps, organized by category.":"Expand to browse the main map categories."):"Expand or collapse this category.",()=>toggle=item,true,item.collection&&item.experimental?CandyHudVisuals.Peach:CandyHudVisuals.Lavender);
                else
                {
                    var map=item.map;string id=map.id;
                    ActionCard(y,CandyHudIcon.Home,demoMapPicker.Title(item),"Load this map with the current mode. The map starts fresh.",()=>maps.SelectMap(id),GameExperiencePanel.CanEditRawChoices(session)&&maps.CanSelectMap&&map.available&&id!=maps.CurrentMapId,preview:MapPreviewImages.Get(id));
                }
                y+=104;
            }
            if(toggle!=null)demoMapPicker.Toggle(toggle);
        }
        void SpectatePage()
        {
            var controls=Demo as ISpectatorControls;
            if(controls==null){Note(119,"Spectator controls are getting ready...");return;}
            var review=MatchRuntime.Current as IMatchReviewControls;
            bool spoilers=review?.DevelopmentReviewEnabled==true&&MatchRuntime.Current.Rules.mode==GameModeKind.MurderMystery;
            if(spoilers){ReviewRoleCard(119,MysteryRole.Murderer,review);ReviewRoleCard(223,MysteryRole.Sheriff,review);}
            float offset=spoilers?208:0;
            ActionCard(119+offset,CandyHudIcon.Survivor,controls.Spectating?"Return to your character":"Watch players / bots","Leave your character in place and watch without controlling the target.",()=>{controls.SetSpectating(!controls.Spectating);session.SetMenu(false);},controls.Spectating||controls.CanSpectate);
            ActionCard(223+offset,CandyHudIcon.Settings,controls.FourViews?"View: four quadrants":"View: single POV",controls.CanUseFourViews?"Switch between one POV and four different living players.":"Headsets use one full-screen view with physical head looking.",()=>controls.SetFourViews(!controls.FourViews),controls.CanUseFourViews);
            ActionCard(327+offset,CandyHudIcon.Survivor,"Previous player","Switch the watched player; four-view mode rotates the group.",()=>{controls.CycleWatched(-1);session.SetMenu(false);},controls.Spectating);
            ActionCard(431+offset,CandyHudIcon.Survivor,"Next player","Dead or removed targets are replaced automatically.",()=>{controls.CycleWatched(1);session.SetMenu(false);},controls.Spectating);
            Note(spoilers?743:535,"Your body stays in the world and can still be hit. Arrow keys switch targets; Esc opens this menu. Extra POV cameras render only while spectating.");
        }
        void ReviewRoleCard(float y,MysteryRole role,IMatchReviewControls review)
        {
            string id=review.PlayerInRole(role);MatchPlayerFrame player=null;
            foreach(var row in MatchRuntime.Current.Frame.players)if(row.id==id){player=row;break;}
            string name=player?.name??"not assigned";
            ActionCard(y,CandyHudIcon.Survivor,role.ToString().ToUpperInvariant()+": "+name,"DEVELOPMENT REVIEW ONLY · "+(player!=null&&!player.alive?"Fallen":id==MatchRuntime.Current.LocalActorId?"Return to your own view":"Watch this player’s first-person view"),()=>review.WatchPlayer(id),player!=null&&player.alive&&!player.dying);
        }
        GUIStyle spectatorLabel;
        void DrawSpectatorLabels()
        {
            if(!Ready||session.IsVR||session.MenuOpen||!(Demo is ISpectatorControls controls)||!controls.Spectating)return;
            spectatorLabel??=new GUIStyle(GUI.skin.box){font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),fontSize=16,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(12,12,4,4),normal={background=CandyHudVisuals.Rounded,textColor=Color.white}};
            var old=GUI.backgroundColor;GUI.backgroundColor=new Color(.08f,.03f,.15f,.9f);
            for(int i=0;i<(controls.FourViews?4:1);i++)
            {
                float width=controls.FourViews?Screen.width*.5f:Screen.width;
                float x=controls.FourViews?(i%2)*width:0,y=controls.FourViews?(i/2)*Screen.height*.5f:0;
                GUI.Box(new Rect(x+8,y+8,Mathf.Max(100,width-16),32),controls.WatchedName(i)+"  ·  SPECTATING",spectatorLabel);
            }
            GUI.Box(new Rect(8,Screen.height-34,Mathf.Min(Screen.width-16,490),26),"← / → switch players   ·   Esc spectator menu",spectatorLabel);GUI.backgroundColor=old;
        }
        int desktopDisaster, desktopDisasterOffset;

        Rect DesktopChoiceSlot(int index)
        {
            var direction = ModeSetupPanel.WheelDirection(index);
            var center = new Vector2(271 + 163 * direction.x, 254 - 125 * direction.y);
            return new Rect(center.x - 55, center.y - 25, 110, 50);
        }

        void DesktopChoiceCard(int index, string title, bool selected, Action select, bool enabled = true)
        {
            Rect rect = DesktopChoiceSlot(index);
            Color tint = rect.Contains(Event.current.mousePosition) && enabled
                ? CandyHudVisuals.Mint : selected ? CandyHudVisuals.Lavender : CandyHudVisuals.Card;
            DesktopWheelButton(rect, "", select, enabled, tint);
            desktopWheelCenter ??= new GUIStyle(small){fontSize=15,alignment=TextAnchor.MiddleCenter,wordWrap=true};
            var style = desktopWheelCenter;
            var oldColor = style.normal.textColor;
            style.normal.textColor = tint.grayscale > .5f ? CandyHudVisuals.Ink : Color.white;
            GUI.Label(new Rect(rect.x + 4, rect.y + 2, rect.width - 8, rect.height - 4), title, style);
            style.normal.textColor = oldColor;
        }

        float DisasterContentHeight()
        {
            if(Demo==null||Demo.DisasterCount==0)return 420;
            int selected=Mathf.Clamp(desktopDisaster,0,Demo.DisasterCount-1);
            return 445+Demo.DisasterParameters(selected).Count*64;
        }
        void DisastersPage()
        {
            if(Demo==null||Demo.DisasterCount==0){Note(125,"Disasters are getting ready...");return;}
            const int size=ModeSetupPanel.ChoiceWheelSize;
            desktopDisaster=Mathf.Clamp(desktopDisaster,0,Demo.DisasterCount-1);
            desktopDisasterOffset=Mathf.Clamp(desktopDisasterOffset/size,0,(Demo.DisasterCount-1)/size)*size;
            for(int i=0;i<size&&desktopDisasterOffset+i<Demo.DisasterCount;i++)
            {
                int choice=desktopDisasterOffset+i;
                DesktopChoiceCard(i,Demo.DisasterLabel(choice),choice==desktopDisaster,()=>desktopDisaster=choice);
            }
            int index=desktopDisaster;
            Panel(new Rect(203,215,136,78),CandyHudVisuals.Panel);
            GUI.Label(new Rect(207,217,128,74),Demo.DisasterLabel(index),desktopWheelCenter);
            GUI.Label(new Rect(490,124,330,48),"DISASTER CONTROLS",heading);
            DesktopWheelButton(new Rect(490,180,330,48),Demo.DisasterIsToggle(index)?"TOGGLE SELECTED":"LAUNCH SELECTED",()=>disasterStatus=Demo.LaunchDisaster(index),CanCommand,CandyHudVisuals.Peach);
            DesktopWheelButton(new Rect(490,238,330,42),"STOP ALL DISASTERS",()=>Demo.Run(DemoCommand.StopDisasters),CanCommand);
            DesktopWheelButton(new Rect(490,297,154,36),"PREVIOUS",()=>{desktopDisasterOffset-=size;desktopDisaster=desktopDisasterOffset;},desktopDisasterOffset>0);
            DesktopWheelButton(new Rect(666,297,154,36),"NEXT",()=>{desktopDisasterOffset+=size;desktopDisaster=desktopDisasterOffset;},desktopDisasterOffset+size<Demo.DisasterCount);
            GUI.Label(new Rect(490,342,330,64),disasterStatus??"",small);
            var definitions=Demo.DisasterParameters(index);var values=Demo.DisasterValues(index);
            float y=422;
            Panel(new Rect(8,y,824,definitions.Count*64+23),CandyHudVisuals.Card);
                bool old=GUI.enabled;GUI.enabled=old&&CanCommand;
                for(int i=0;i<definitions.Count;i++)
                {
                    var option=definitions[i];
                    if(option.whole&&option.min==0&&option.max==1)
                    {
                        bool on=values[i]>.5f;
                        if(Button(new Rect(28,y+12+i*64,780,40),option.label+"  ·  "+(on?"ON":"OFF"),on?CandyHudVisuals.Mint:CandyHudVisuals.Panel))Demo.SetDisasterValue(index,i,on?0:1);
                        continue;
                    }
                    var p=definitions[i];GUI.Label(new Rect(28,y+6+i*64,780,26),p.label+" · "+values[i].ToString(p.whole?"F0":"F1")+" "+p.unit,body);
                    float v=GUI.HorizontalSlider(new Rect(28,y+37+i*64,780,20),values[i],p.min,p.max);if(!Mathf.Approximately(v,values[i]))Demo.SetDisasterValue(index,i,v);
                }
                GUI.enabled=old;GUI.Label(new Rect(28,y+definitions.Count*64,780,23),"Settings apply on the next launch",small);
        }
        void BotsPage()
        {
            if(Demo==null){Note(125,"Survivors are getting ready...");return;}
            ActionCard(119,CandyHudIcon.Survivor,"Add a survivor  ·  "+Demo.SurvivorCount+" active","Spawn a bot using the selected personality.",()=>Demo.Run(DemoCommand.AddSurvivor),CanCommand);
            ActionCard(223,CandyHudIcon.Friends,"Fill to "+Demo.SurvivorTarget+" players","Count human players too; fill the remaining survivor slots.",()=>Demo.Run(DemoCommand.FillSurvivors),CanCommand);
            ActionCard(327,CandyHudIcon.Settings,"Next personality: "+Demo.SurvivorPersonality,"Click to cycle the personality used for new survivors.",()=>Demo.Run(DemoCommand.NextSurvivor),CanCommand);
            ActionCard(431,CandyHudIcon.Survivor,"Clear bot survivors","Remove the survivor bots and stop automatic filling.",()=>Demo.Run(DemoCommand.ClearSurvivors),CanCommand,CandyHudVisuals.Peach);
            var enemies=session.SurvivorBots as IEnemyPopulation;bool canEnemies=enemies!=null&&MatchRuntime.Current?.Authority==true;
            ActionCard(535,CandyHudIcon.Survivor,"Spawn jaguar enemy  ·  "+(enemies?.EnemyCount??0)+" active","Infected-style pursuit and normal bot movement. Spawns at the perimeter; 20 damage per hit.",()=>enemies.SpawnEnemy(new EnemySpawnSettings()),canEnemies,CandyHudVisuals.Peach);
            ActionCard(639,CandyHudIcon.Survivor,"Clear jaguar enemies","Remove enemies while keeping ordinary survivor bots.",()=>enemies.ClearEnemies(),canEnemies);
            Note(749,Demo.SurvivorStatus+(CanCommand?"":"  Only the host can add or remove bots."));
        }
        void HandsPage()
        {
            if(Demo==null){Note(125,"Giant hands are getting ready...");return;}
            ActionCard(119,CandyHudIcon.Hand,"Add a giant hand  ·  "+Demo.HandCount+" active","Spawn an AI hand with the current hand prefab and personality.",()=>Demo.Run(DemoCommand.AddHand),CanCommand);
            ActionCard(223,CandyHudIcon.Settings,"Next personality: "+Demo.HandPersonality,"Click to cycle the behavior of the next hand you add.",()=>Demo.Run(DemoCommand.NextHand),CanCommand);
            ActionCard(327,CandyHudIcon.Hand,"Clear AI hands","Remove the AI hands; keep the human players.",()=>Demo.Run(DemoCommand.ClearHands),CanCommand,CandyHudVisuals.Peach);Note(437,Demo.HandStatus+(CanCommand?"":"  Only the host can manage AI hands."));
        }
        void FriendsPage()
        {
            // Keep all discovery / pending / cancellation conditions owned by the network component.
            var color=GUI.backgroundColor;GUI.backgroundColor=CandyHudVisuals.Card;
            GUILayout.BeginArea(new Rect(10,113,816,515));if(session.Multiplayer!=null)MultiplayerPanel.Draw(session.Multiplayer);else GUILayout.Label("Local discovery is starting...");GUILayout.EndArea();GUI.backgroundColor=color;
        }
        void ModesPage()
        {
            var background=GUI.backgroundColor;GUI.backgroundColor=CandyHudVisuals.Card;
            bool editable=GUI.enabled;
            var match=MatchRuntime.Current;
            bool canPick=editable&&match?.Authority==true&&GameExperiencePanel.CanEditRawChoices(session);
            if(match!=null)
            {
                for(int i=0;i<ModeSetupPanel.WheelModes.Length;i++)
                {
                    var choice=ModeSetupPanel.WheelModes[i];
                    DesktopChoiceCard(i,CandyHudContent.Mode(choice),choice==match.Rules.mode,
                        ()=>{if(choice!=match.Rules.mode)match.Configure(ModeDefaults.Create(choice));},canPick);
                }
                var picture=ModeSetupPanel.WheelModePicture(match.Rules.mode);
                if(picture)GUI.DrawTexture(new Rect(203,209,136,90),picture,ScaleMode.ScaleToFit);
                GUI.Label(new Rect(490,133,330,48),CandyHudContent.Mode(match.Rules.mode),heading);
                GUI.Label(new Rect(490,188,326,72),canPick?"Select a mode, then start a round. Settings are below.":"The host selects the mode.",body);
                DesktopWheelButton(new Rect(490,275,330,48),"START / RESTART ROUND",()=>match.StartRound(),canPick,CandyHudVisuals.Peach);
            }
            GUI.enabled=editable&&GameExperiencePanel.CanEditRawChoices(session);
            GUILayout.BeginArea(new Rect(12,422,816,5550));ModeSetupPanel.Draw();GUILayout.EndArea();GUI.enabled=editable;
            GUI.backgroundColor=background;
        }
        void SettingsPage()
        {
            ActionCard(119,CandyHudIcon.Help,"Gameplay hints: "+hintMode,"Adaptive clears each first-run hint when you do the action. Always shows more controls.",()=>{hintMode=(HintMode)(((int)hintMode+1)%3);});
            GUI.Label(new Rect(12,230,820,29),"Hint opacity  ·  "+Mathf.RoundToInt(hintOpacity*100)+"%",body);hintOpacity=GUI.HorizontalSlider(new Rect(12,270,815,24),hintOpacity,.5f,1);
            Note(310,"Tip: F1 toggles gameplay hints. Use a larger Game view for a larger menu. Settings here apply to this running session.");
            var color=GUI.backgroundColor;GUI.backgroundColor=CandyHudVisuals.Card;
            GUILayout.BeginArea(new Rect(12,410,816,1800));SurvivorSettingsPanel.Draw(Settings,EditWorld,Demo!=null?Demo.ApplyStaminaRules:(Action<string>)null);
            GUILayout.EndArea();GUI.backgroundColor=color;
        }
        void VoicePage()
        {
            var background=GUI.backgroundColor;GUI.backgroundColor=CandyHudVisuals.Card;
            GUILayout.BeginArea(new Rect(12,115,816,1250));
            SocialPanel.Draw(session.Social, DemoMode && CanCommand);
            GUILayout.EndArea();
            GUI.backgroundColor=background;
        }
        void ControlsPage()
        {
            var inspection=CandyThirdPersonInspection.For(session);
            ActionCard(117,CandyHudIcon.Survivor,"Third person / inspect your avatar",session.IsVR?"Stationary mirror: look at its orbit / zoom buttons, then hold RIGHT trigger or pinch.":"Character stays still. Drag mouse to orbit, scroll to zoom; V returns.",()=>inspection.Begin(),inspection?.CanInspect==true);
            Note(550,session.IsVR?"PICKUP: Look at the weapon, reach and squeeze grip or pinch. Swapping drops the item in your slot.":
                "PICKUP: Aim at the weapon and press E. Swapping drops the item in your slot. Delete drops it directly.");
            var hints=Hints();for(int i=0;i<hints.Length;i++){Panel(new Rect(8,221+i*107,824,94),CandyHudVisuals.Card);Icon(new Rect(25,244+i*107,44,44),hints[i].icon,CandyHudVisuals.Mint);GUI.Label(new Rect(88,233+i*107,720,32),hints[i].key,heading);GUI.Label(new Rect(88,272+i*107,720,38),hints[i].text,body);}
            Note(609,!session.IsVR?"MENU: Esc opens / closes. Click a card, or use arrow keys + Enter. Mouse wheel scrolls. Use Character to switch roles. F8 opens Sound Studio.":"MENU: bare left Y or native menu opens / closes. Right-only: Grip + B opens; B closes. Left thumb + middle also toggles. Aim at a button and press its controller trigger, or use the selecting pinch.");
            Note(704,"SURVIVOR: jump, then press jump again by a ledge to vault. Hold jump by a wall to catch it; move to climb or shimmy; release to drop. Rest on secure ground to recover grip. AIM: dominant grip (VR) / RMB (desktop) raises gun / sight; release lowers it.");
            Note(807,"GIANT HAND: desktop WASD flies, Q lowers, E rises, mouse steers. In VR, the movement stick flies. With one controller, hold the stick click and tilt to turn or look. Trigger rises; grip lowers. With bare hands, pinch the spare hand and move it from its starting position to steer; release to stop.");
            Note(919,"SURVIVOR SPRINT: click the movement stick or tap Left Shift to toggle while grounded. Jumping or climbing ends sprint. Single-controller weapon mode: Grip + X / A; two controllers: support trigger. Grip + stick click opens emotes. Delete or Drop drops equipment. STANDINGS: with two controllers, hold bare look-stick click, or pin via menu. VR pages cycle every 5s.");
            Note(1051,"SUIT: V takes off / lands; WASD flies; Q lowers / E rises; left click fires. In VR the flight controller stick flies, trigger rises and grip lowers. Grip + Y toggles left; B toggles right. The tracked other hand trigger or pinch fires. Release input after menu or tracking changes.");
        }
        public struct Hint
        {
            public CandyHudIcon icon;
            public string key, text;
            internal ControllerPart part;
            internal bool left;

            public Hint(CandyHudIcon icon, string key, string text)
            {
                this.icon=icon;this.key=key;this.text=text;
                part=ControllerPart.None;left=false;
            }
        }

        internal enum ControllerPart { None, Stick, Primary, Trigger, Grip, Secondary }

        static Hint Controller(bool left, ControllerPart part, string action, string prefix="")
        {
            string side=left?"Left":"Right";
            string button=part==ControllerPart.Primary?(left?"X":"A"):part==ControllerPart.Secondary?(left?"Y":"B"):part.ToString();
            return new Hint(CandyHudIcon.Key,prefix+side+" "+button,action){left=left,part=part};
        }

        static void Controllers(out bool left,out bool right)
        {
            left=right=false;
            foreach(var device in InputSystem.devices)
            {
                if(!(device is UnityEngine.InputSystem.XR.XRController controller) ||
                    device.layout.IndexOf("HandInteraction",StringComparison.OrdinalIgnoreCase)>=0 ||
                    !controller.isTracked.isPressed)continue;
                foreach(var usage in device.usages)
                {
                    left|=usage==UnityEngine.InputSystem.CommonUsages.LeftHand;
                    right|=usage==UnityEngine.InputSystem.CommonUsages.RightHand;
                }
            }
        }

        public Hint[] Hints()
        {
            bool vr=session.IsVR;
            bool left=false,right=false;
            if(vr)Controllers(out left,out right);
            bool single=left!=right;
            bool dominantLeft=single?left:session.Player?.gameObject.GetComponent<IBlasterControls>()?.LeftHanded==true;
            Hint move=vr?Controller(session.PlayingHand?left||!right:single?left:!dominantLeft,ControllerPart.Stick,session.PlayingHand?"Fly":"Move"):
                new Hint(CandyHudIcon.Move,"W A S D",session.PlayingHand?"Fly":"Move");
            if(session.PlayingHand)
            {
                if(!vr)return new[]{move,new Hint(CandyHudIcon.Key,"Q / E","Descend / rise"),new Hint(CandyHudIcon.Look,"MOUSE","Steer")};
                if(!left&&!right)return TrackedHands?new[]{
                    new Hint(CandyHudIcon.Hand,"HAND TRACKING","Gesture flight"),
                    new Hint(CandyHudIcon.Pinch,"SPARE HAND PINCH","Steer / release to stop"),
                    new Hint(CandyHudIcon.Help,"OPEN CONTROLS","Hand-tracking help")}:new[]{
                    new Hint(CandyHudIcon.Help,"TRACK A CONTROLLER","Flight input unavailable"),
                    new Hint(CandyHudIcon.Help,"NO TRACKED TRIGGER","Rise unavailable"),
                    new Hint(CandyHudIcon.Help,"NO TRACKED GRIP","Descend unavailable")};
                bool pilotLeft=single?left:dominantLeft;
                return new[]{Controller(single?left:!pilotLeft,ControllerPart.Stick,single?"Fly / click-hold to look":"Fly"),
                    Controller(pilotLeft,ControllerPart.Trigger,"Rise"),Controller(pilotLeft,ControllerPart.Grip,"Descend")};
            }

            var controls=session.Player.gameObject.GetComponent<IEquipmentControls>();
            var equipment=controls?.Equipment;
            bool armed=equipment!=null&&equipment.kind>=EquipmentKind.Blaster&&equipment.kind!=EquipmentKind.FlyingHand;
            bool drawn=armed&&equipment.drawn;
            bool modeSwitch=drawn&&EquipmentSpecifications.Get(equipment.kind).modeSwitch;
            bool standing=vr&&Settings?.StandingPlay==true;
            var jump=!vr?new Hint(CandyHudIcon.Key,"SPACE","Jump / vault"):
                standing&&TrackedHands&&!right?new Hint(CandyHudIcon.Pinch,Settings.JumpGestureLabel=="Fist"?"RIGHT FIST":"RIGHT PINCH","Jump / vault"):
                Controller(dominantLeft,ControllerPart.Primary,"Jump / vault");
            if(Climbing)
            {
                move.text="Climb";
                var release=jump;
                jump.key="Hold "+jump.key;jump.text="Keep your grip";
                release.key="Release "+release.key;release.text="Let go";
                return new[]{move,jump,release};
            }

            if(armed&&equipment.kind==EquipmentKind.PoweredArmor)
            {
                if(vr&&!left&&!right)return new[]{
                    new Hint(CandyHudIcon.Hand,"TRACK A CONTROLLER","Controller needed"),
                    new Hint(CandyHudIcon.Move,"NO TRACKED STICK","Flight unavailable"),
                    new Hint(CandyHudIcon.Help,"NO TRACKED BUTTON","Toggle unavailable")};
                bool pilotLeft=single?left:!equipment.leftHanded;
                bool mixedFlightSide=vr&&left&&right&&TrackedHands;
                if(!drawn)return new[]{move,jump,vr?(mixedFlightSide?new Hint(CandyHudIcon.Key,"DOM: Grip+Y / B","Activate suit"):Controller(single?left:equipment.leftHanded,ControllerPart.Secondary,"Activate suit",(single?left:equipment.leftHanded)?"Grip + ":"")):
                    new Hint(CandyHudIcon.Key,"V","Activate suit")};
                var fingers=session.Player.gameObject.GetComponent<IFingerTips>();
                bool otherController=pilotLeft?right:left;
                bool otherTracked=fingers!=null&&fingers.TryIndexTip(!pilotLeft,out _);
                string fire=otherController?(pilotLeft?"R trig":"L trig"):
                    otherTracked?(pilotLeft?"R palm":"L palm"):"";
                string land=equipment.leftHanded?"Grip+Y":"B";
                if(mixedFlightSide)return new[]{
                    new Hint(CandyHudIcon.Move,"NON-DOM STICK","Fly"),
                    new Hint(CandyHudIcon.Key,"FLIGHT TRIG / GRIP","Rise / descend"),
                    new Hint(CandyHudIcon.Key,"DOM HAND / Grip+Y,B","Fire / land")};
                return vr?new[]{Controller(pilotLeft,ControllerPart.Stick,"Fly"),
                    new Hint(CandyHudIcon.Key,pilotLeft?"L trigger / grip":"R trigger / grip","Rise / descend"),
                    new Hint(CandyHudIcon.Key,fire.Length>0?fire+" / "+land:land,fire.Length>0?"Fire / land":"Land")}:
                    new[]{new Hint(CandyHudIcon.Move,"W A S D","Fly"),
                        new Hint(CandyHudIcon.Key,"Q / E","Descend / rise"),
                        new Hint(CandyHudIcon.Key,"L CLICK / V","Fire / land")};
            }
            if(armed)
            {
                bool weaponLeft=single?left:equipment.leftHanded;
                var spec=EquipmentSpecifications.Get(equipment.kind);
                bool swing=spec.melee&&!(modeSwitch&&equipment.shotgun);
                bool bash=drawn&&!swing&&MatchRuntime.Current?.Rules?.punchEnabled==true;
                var attack=!vr?new Hint(CandyHudIcon.Key,drawn?"LEFT CLICK":"V",drawn?(swing?"Attack":bash?"Fire / T bash":"Fire"):"Draw"):
                    swing&&drawn?new Hint(CandyHudIcon.Hand,weaponLeft?"LEFT HAND":"RIGHT HAND","Swing weapon"):
                    Controller(weaponLeft,drawn?ControllerPart.Trigger:ControllerPart.Secondary,drawn?(!bash?"Fire":single?"Fire / jab to bash":weaponLeft?"Fire / A bash":"Fire / X bash"):"Draw",!drawn&&weaponLeft?"Grip + ":"");
                var extra=modeSwitch?
                    (vr?(single?Controller(left,ControllerPart.Primary,"Switch mode","Grip + "):Controller(!weaponLeft,ControllerPart.Trigger,"Switch mode")):
                    new Hint(CandyHudIcon.Key,"R","Switch mode")):
                    (drawn?vr?Controller(weaponLeft,ControllerPart.Secondary,"Holster",weaponLeft?"Grip + ":""):
                    new Hint(CandyHudIcon.Key,"V","Holster"):jump);
                var aim=vr?Controller(weaponLeft,ControllerPart.Grip,"Aim / release to lower"):
                    new Hint(CandyHudIcon.Look,"RMB","Aim / release to lower");
                return new[]{drawn&&!swing&&!spec.power?aim:controls?.BlocksTraversal==true?move:jump,attack,extra};
            }

            var climb=jump;climb.key="Hold "+jump.key;climb.text="Catch a wall";
            return new[]{move,jump,climb};
        }

        internal void ReplayIntro()
        {
            hintMode=HintMode.Adaptive;
            completedActions=BasicAction.None;
            PlayerPrefs.DeleteKey(session.IsVR ? "CandyJenga.BasicActions.VR" : "CandyJenga.BasicActions.Desktop");
            introKey=null;
            introPlayer=null;
            lastContext="";
            nextHintSample=0;
        }

        void ObserveBasicActions(Camera camera)
        {
            var player=session.Player;
            var motion=Move;
            if(motion==null || motion.InputPaused) { introPlayer=null; return; }
            Vector3 position=player.transform.position;
            var rotation=camera.transform.rotation;
            if(introPlayer!=player.transform || introCamera!=camera)
            {
                introPlayer=player.transform;
                introCamera=camera;
                lookStart=rotation;
                lastMovePosition=position;
                walkedDistance=0;
                introWasGrounded=motion.Grounded;
                return;
            }

            var previous=completedActions;
            if(Quaternion.Angle(lookStart,rotation)>=12) completedActions|=BasicAction.Look;
            var step=position-lastMovePosition;
            step.y=0;
            // Require both movement input and displacement. A moving platform alone is not walking.
            if(motion.Grounded && step.sqrMagnitude<1 && player is IMenuInputState input && input.MovementHeld)
                walkedDistance+=step.magnitude;
            if(walkedDistance>=.4f) completedActions|=BasicAction.Move;
            if(introWasGrounded && !motion.Grounded && !motion.Climbing && !motion.Vaulting && motion.VerticalSpeed>.8f)
                completedActions|=BasicAction.Jump;
            lastMovePosition=position;
            introWasGrounded=motion.Grounded;
            if(previous==completedActions)return;
            PlayerPrefs.SetInt(introKey,(int)completedActions);
            nextHintSample=0;
        }

        Hint[] IntroHints()
        {
            if(!session.IsVR)return new[]{
                new Hint(CandyHudIcon.Look,"MOUSE","Look"),new Hint(CandyHudIcon.Move,"W A S D","Move"),new Hint(CandyHudIcon.Key,"SPACE","Jump")};
            Controllers(out bool left,out bool right);
            bool dominantLeft=left!=right?left:session.Player?.gameObject.GetComponent<IBlasterControls>()?.LeftHanded==true;
            bool handsOnly=!left&&!right;
            var jump=Settings?.StandingPlay==true&&TrackedHands&&!right?
                new Hint(CandyHudIcon.Pinch,Settings.JumpGestureLabel=="Fist"?"RIGHT FIST":"RIGHT PINCH","Jump"):
                Controller(dominantLeft,ControllerPart.Primary,"Jump");
            var move=handsOnly?new Hint(CandyHudIcon.Move,"CONNECT CONTROLLER","Move"):
                Controller(left!=right?left:!dominantLeft,ControllerPart.Stick,"Move");
            return new[]{new Hint(CandyHudIcon.Look,"TURN YOUR HEAD","Look around"),move,jump};
        }

        void BuildHints()
        {
            var go=new GameObject("Candy control hints",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup),typeof(CanvasScaler));
            go.transform.SetParent(transform,false);
            hintCanvas=go.GetComponent<Canvas>();hintCanvas.sortingOrder=20;
            hintGroup=go.GetComponent<CanvasGroup>();hintGroup.alpha=0;
            hintGroup.blocksRaycasts=false;hintGroup.interactable=false;
            var role=Pill(go.transform,new Vector2(252,45),new Vector2(22,-20),new Vector2(0,1),CandyHudCards.Background);
            roleText=Text(role,"",new Vector2(226,21),new Vector2(0,9),16);roleText.fontStyle=FontStyle.Bold;roleText.color=CandyHudCards.Ink;roleText.GetComponent<Outline>().enabled=false;
            statusText=Text(role,"",new Vector2(226,18),new Vector2(0,-9),14);statusText.color=CandyHudCards.Accent;statusText.GetComponent<Outline>().enabled=false;
            hintStrip=Pill(go.transform,new Vector2(225,151),new Vector2(18,18),Vector2.zero,new Color(.94f,.87f,1,.96f));
            for(int i=0;i<3;i++)
            {
                var card=new GameObject("Control row",typeof(RectTransform)).GetComponent<RectTransform>();
                hintRows[i]=card;
                card.SetParent(hintStrip,false);card.sizeDelta=new Vector2(222,48);
                card.anchoredPosition=new Vector2(3,(1-i)*46);
                var raw=new GameObject("Control diagram",typeof(RectTransform),typeof(RawImage));
                raw.transform.SetParent(card,false);
                hintIcons[i]=raw.GetComponent<RawImage>();hintIcons[i].rectTransform.sizeDelta=new Vector2(42,42);
                hintIcons[i].rectTransform.anchoredPosition=new Vector2(-86,0);hintIcons[i].raycastTarget=false;
                hintNames[i]=Text(card,"",new Vector2(163,23),new Vector2(23,10),17);hintNames[i].fontStyle=FontStyle.Bold;hintNames[i].color=CandyHudCards.Ink;hintNames[i].GetComponent<Outline>().enabled=false;
                hintKeys[i]=Text(card,"",new Vector2(163,20),new Vector2(23,-11),15);hintKeys[i].color=CandyHudCards.Accent;hintKeys[i].GetComponent<Outline>().enabled=false;
            }
            var menu=Pill(go.transform,new Vector2(218,32),new Vector2(-22,22),new Vector2(1,0),Color.clear);
            menuText=Text(menu,"",new Vector2(198,29),Vector2.zero,15);menuText.alignment=TextAnchor.MiddleCenter;
        }
        RectTransform Pill(Transform parent,Vector2 size,Vector2 position,Vector2 anchor,Color color)
        {
            var go=new GameObject("Candy hint pill",typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.sprite=CandyHudCards.Wash(anchor.x>.5f);image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=false;
            var r=image.rectTransform;r.sizeDelta=size;r.anchorMin=r.anchorMax=anchor;r.pivot=anchor;r.anchoredPosition=position;return r;
        }
        Text Text(Transform parent,string value,Vector2 size,Vector2 position,int fontSize)
        {
            var go=new GameObject("Hint label",typeof(RectTransform),typeof(Text),typeof(Outline));go.transform.SetParent(parent,false);var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.text=value;text.fontSize=fontSize;text.alignment=TextAnchor.MiddleLeft;text.color=new Color(.99f,.96f,1);
            var edge=go.GetComponent<Outline>();edge.effectColor=new Color(.10f,.045f,.20f,.9f);edge.effectDistance=new Vector2(1,-1);text.raycastTarget=false;text.rectTransform.sizeDelta=size;text.rectTransform.anchoredPosition=position;return text;
        }
        bool viewVR;Camera hintCamera;
        void PresentHints()
        {
            if(DeadlyJenga.Presentation.CandyMatchHud.Expanded ||
                session.Demo is ISpectatorControls observer&&observer.Spectating)
            {
                if(hintCanvas)hintCanvas.enabled=false;
                introPlayer=null;
                return;
            }
            if(!hintCanvas)BuildHints();var camera=session.ActiveCamera;
            if(viewVR!=session.IsVR||hintCamera!=camera||hintCanvas.renderMode==RenderMode.ScreenSpaceOverlay&&session.IsVR)
            {
                viewVR=session.IsVR;hintCamera=camera;
                DeadlyJenga.Presentation.CandyHudLayout.Configure(hintCanvas,transform,camera,viewVR);
            }
            bool adaptive=hintMode==HintMode.Adaptive;
            string key=session.IsVR?"CandyJenga.BasicActions.VR":"CandyJenga.BasicActions.Desktop";
            if(introKey!=key)
            {
                introKey=key;
                completedActions=(BasicAction)PlayerPrefs.GetInt(key,0)&BasicAction.All;
                introPlayer=null;
                nextHintSample=0;
            }
            var phase=MatchRuntime.Current?.Frame.phase;
            bool playable=phase==null||phase==RoundPhase.Playing;
            bool hidden=!camera||session.MenuOpen||studio&&studio.IsOpen||hintMode==HintMode.Hidden||
                adaptive&&(!playable||session.PlayingHand||session.Player.InputSuspended||completedActions==BasicAction.All);
            if(adaptive&&!hidden)
            {
                ObserveBasicActions(camera);
                hidden=completedActions==BasicAction.All;
            }
            else introPlayer=null;
            if(adaptive){roleText.transform.parent.gameObject.SetActive(false);menuText.transform.parent.gameObject.SetActive(false);}
            else menuText.transform.parent.gameObject.SetActive(true);
            if(!hidden&&Time.unscaledTime>=nextHintSample)
            {
                nextHintSample=Time.unscaledTime+.12f;
                var hints=adaptive?IntroHints():Hints();
                string context=(adaptive?"intro|"+(int)completedActions+"|":"full|")+InputLabel;
                foreach(var hint in hints)context+="|"+hint.key+"|"+hint.text;
                if(context!=lastContext)
                {
                    lastContext=context;
                    int visibleCount=0;
                    for(int i=0;i<hints.Length;i++)
                    {
                        bool shown=!adaptive||(completedActions&(BasicAction)(1<<i))==0;
                        hintRows[i].gameObject.SetActive(shown);
                        if(shown)visibleCount++;
                    }
                    hintStrip.sizeDelta=new Vector2(225,13+visibleCount*46);
                    int row=0;
                    for(int i=0;i<hints.Length;i++)
                    {
                        if(!hintRows[i].gameObject.activeSelf)continue;
                        hintRows[i].anchoredPosition=new Vector2(3,(visibleCount-1)*23-row++*46);
                        var hint=hints[i];
                        hintKeys[i].text=hint.key;hintNames[i].text=hint.text;
                        hintIcons[i].texture=hint.part==ControllerPart.None?CandyHudVisuals.Icon(hint.icon):
                            CandyHudVisuals.ControllerDiagram((int)hint.part,hint.left);
                        hintIcons[i].color=hint.part==ControllerPart.None?CandyHudCards.Accent:Color.white;
                        hintIcons[i].rectTransform.sizeDelta=Vector2.one*(hint.part==ControllerPart.None?26:42);
                    }
                }
                var multiplayer=session.Multiplayer;
                bool invite=!string.IsNullOrEmpty(multiplayer?.InvitationFrom);
                roleText.transform.parent.gameObject.SetActive(!adaptive&&(MatchRuntime.Current==null||invite));
                roleText.text=invite?"Nearby invite":session.PlayingHand?"Giant hand":"Survivor";
                statusText.text=invite?"Open Multiplayer":InputLabel;
                Controllers(out bool menuLeft,out bool menuRight);
                menuText.text=session.IsVR?(menuLeft?"Y / native  Menu":menuRight?"Grip+B  Menu":TrackedHands?"Thumb + middle  Menu":"Track controller  Menu"):"Esc  Menu     F1  Hints";
            }
            float opacity=hidden?0:hintOpacity*(adaptive?.76f:1);
            hintGroup.alpha=Mathf.MoveTowards(hintGroup.alpha,opacity,Time.unscaledDeltaTime*5);
            hintCanvas.enabled=hintGroup.alpha>.001f;
        }
        void OnDisable(){if(hintCanvas)hintCanvas.enabled=false;if(studio)studio.ShowLauncher=true;}
        void OnDestroy(){if(hintCanvas)Destroy(hintCanvas.gameObject);if(themed)Destroy(themed);if(studio)studio.ShowLauncher=true;}
    }
}
