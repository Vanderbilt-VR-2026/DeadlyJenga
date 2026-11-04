using System;
using UnityEngine;
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
        bool Vaulting=>Move!=null&&Move.Vaulting;
        string DemoStatus=>Demo!=null?Demo.Status:"";
        int page,selection;Vector2 scroll;bool lastMenu,lastRole,lastVR;float changedAt;string lastContext="";
        float uiSize=1;GUIStyle title,heading,cardHeading,body,small,button,box;GUISkin themed;bool ready;
        Canvas hintCanvas;CanvasGroup hintGroup;RectTransform hintRoot;Text roleText,statusText,menuText;
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
        void Awake(){session=GetComponent<ISessionInfo>();changedAt=Time.unscaledTime;}
        void Update()
        {
            if((session as ILobbyMatchVisitControls)?.VisitingLobby==true){if(hintCanvas)hintCanvas.enabled=false;return;}
            if(MatchRuntime.Current?.Frame.phase==RoundPhase.Results){if(hintCanvas)hintCanvas.enabled=false;return;}
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
                if(sectionPicker)
                {
                    int count=DemoMenuSections.Sections[page].Length;
                    if(k.upArrowKey.wasPressedThisFrame)categoryCursor=(categoryCursor+count-1)%count;
                    if(k.downArrowKey.wasPressedThisFrame)categoryCursor=(categoryCursor+1)%count;
                    if(k.enterKey.wasPressedThisFrame||k.numpadEnterKey.wasPressedThisFrame){subsection=categoryCursor;sectionPicker=false;selection=0;scroll=Vector2.zero;}
                    PresentHints();return;
                }
                if(k.upArrowKey.wasPressedThisFrame){selection=(selection+Mathf.Max(1,navCount)-1)%Mathf.Max(1,navCount);FollowSelection();}
                if(k.downArrowKey.wasPressedThisFrame){selection=(selection+1)%Mathf.Max(1,navCount);FollowSelection();}
                if(k.enterKey.wasPressedThisFrame||k.numpadEnterKey.wasPressedThisFrame)activate=true;
            }
            if(k!=null&&!session.MenuOpen&&k.f1Key.wasPressedThisFrame){hintMode=hintMode==HintMode.Hidden?HintMode.Adaptive:HintMode.Hidden;changedAt=Time.unscaledTime;}
            PresentHints();
        }
        void ChangePage(int next){page=next;subsection=0;sectionPicker=false;giveEquipmentPage=false;selection=0;scroll=Vector2.zero;activate=false;}
        void FollowSelection(){float top=selection<navTops.Count?navTops[selection]:116+selection*104;if(top<scroll.y)scroll.y=top;if(top+100>scroll.y+464)scroll.y=top+100-464;}
        void Styles()
        {
            if(ready)return;ready=true;var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title=new GUIStyle(GUI.skin.label){font=font,fontSize=30,fontStyle=FontStyle.Bold,normal={textColor=Color.white}};
            heading=new GUIStyle(title){fontSize=22};body=new GUIStyle(GUI.skin.label){font=font,fontSize=19,wordWrap=true,normal={textColor=Color.white}};
            cardHeading=new GUIStyle(heading){fontSize=19,wordWrap=true};
            small=new GUIStyle(body){fontSize=18,normal={textColor=new Color(.90f,.87f,.96f)}};
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=19,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(14,12,8,8),border=new RectOffset(10,10,10,10),normal={background=CandyHudVisuals.Rounded,textColor=Color.white},hover={background=CandyHudVisuals.Rounded,textColor=Color.white},active={background=CandyHudVisuals.Rounded,textColor=Color.white}};
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
            if(DeadlyJenga.Presentation.CandyWildlifePanel.Showing(session))return;
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
        {var old=GUI.backgroundColor;GUI.backgroundColor=color;var foreground=color.grayscale>.5f?CandyHudVisuals.Ink:Color.white;button.normal.textColor=button.hover.textColor=button.active.textColor=button.focused.textColor=foreground;bool result=GUI.Button(r,text,button);GUI.backgroundColor=old;return result;}
        void DrawMenu(int id)
        {
            page=Mathf.Clamp(page,0,PageCount-1);
            Panel(new Rect(0,0,1200,736),CandyHudVisuals.Ink);Panel(new Rect(0,0,1200,8),CandyHudVisuals.Lavender);
            Icon(new Rect(26,27,48,48),session.PlayingHand?CandyHudIcon.Hand:CandyHudIcon.Home,CandyHudVisuals.Mint);
            GUI.Label(new Rect(93,23,470,45),"DEADLY JENGA  /  DEMO",title);
            GUI.Label(new Rect(95,71,775,30),(session.PlayingHand?"GIANT HAND":"SURVIVOR")+"   ·   "+InputLabel+"   ·   "+(session.Multiplayer!=null&&session.Multiplayer.IsClient?"Connected to host":"Your hosted playground"),small);
            if(Button(new Rect(963,28,207,51),"Resume  ·  Esc",CandyHudVisuals.Mint))session.SetMenu(false);
            var inspection=CandyThirdPersonInspection.For(session);
            if(DemoMode){bool enabled=GUI.enabled;GUI.enabled=inspection?.CanInspect==true;if(Button(new Rect(963,88,207,30),"Third person",CandyHudVisuals.Lavender))inspection.Begin();GUI.enabled=enabled;}
            if(Button(new Rect(748,28,201,51),"Exit to Lobby",CandyHudVisuals.Peach)){var visit=session as ILobbyMatchVisitControls;if(visit?.CanVisitLobby==true)visit.VisitLobby();else MatchRuntime.Current?.ReturnToLobby();}
            var review=MatchRuntime.Current as IMatchReviewControls;
            if(review?.DevelopmentReviewEnabled==true)
            {bool enabled=GUI.enabled;GUI.enabled=review.CanEndGame;if(Button(new Rect(572,28,164,51),"End Game",CandyHudVisuals.Lavender))review.EndGame();GUI.enabled=enabled;}
            if(HandLooks.Names.Length>0&&Button(new Rect(748,88,201,30),"Hand: "+HandLooks.ChosenName+"  >",CandyHudVisuals.Card))HandLooks.Chosen++;
            for(int i=0;i<PageCount;i++)
            {
                bool pending=i==3&&session.Multiplayer!=null&&session.Multiplayer.HasPending;
                var r=new Rect(20,128+i*75,218,68);if(Button(r,"     "+PageName(i).Replace(" Controls","\n     Controls")+(pending?"  !":""),i==page?new Color(.40f,.28f,.58f):CandyHudVisuals.Panel))ChangePage(i);
                Icon(new Rect(r.x+10,r.y+24,25,25),PageIcon(i),i==page?CandyHudVisuals.Mint:CandyHudVisuals.Lavender);
            }
            DrawSectionSelector();
            Panel(new Rect(258,180,921,494),CandyHudVisuals.Panel);drawIndex=0;
            float contentHeight=SectionHeight;
            bool contentEnabled=GUI.enabled;GUI.enabled=contentEnabled&&!sectionPicker;
            scroll=GUI.BeginScrollView(new Rect(278,194,884,464),scroll,new Rect(0,0,856,contentHeight));
            GUI.Label(new Rect(8,2,830,36),SectionName,heading);GUI.Label(new Rect(8,45,830,60),PageDescription(),small);
            if(!sectionPicker)DrawSection();
            GUI.EndScrollView();GUI.enabled=contentEnabled;navCount=drawIndex;if(navCount>0)selection=Mathf.Clamp(selection,0,navCount-1);
            GUI.Label(new Rect(26,692,1150,30),Time.unscaledTime<feedbackUntil?commandFeedback:"Click: act / Wheel: scroll / Left-Right: tabs / Up-Down: cards / Enter: confirm / Esc: resume",small);
            DrawSectionDropdown();
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

            ActionCard(119,session.PlayingHand?CandyHudIcon.Survivor:CandyHudIcon.Hand,session.PlayingHand?"Become a survivor":"Become a giant hand",session.PlayingHand?"Spawn at the top and explore the castle.":"Fly around the tower and push its candy blocks.",()=>{session.SetRole(!session.PlayingHand);session.SetMenu(false);});
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
                ActionCard(951,CandyHudIcon.Hand,equipment?.Equipment.drawn==true?"Holster weapon":"Draw weapon","V toggles equipment. Controller trigger draws; trigger near its holster stows. Trigger away from holster fires.",()=>equipment.SetEquipped(!equipment.Equipment.drawn),equipment?.Equipment.kind>=EquipmentKind.Blaster);
                ActionCard(1055,CandyHudIcon.Settings,"DOMINANT HAND: "+(gun?.LeftHanded==true?"LEFT":"RIGHT"),"Non-dominant controller trigger jumps while testing is enabled, including when holstered.",()=>gun.SetLeftHanded(!gun.LeftHanded),gun!=null);
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
        void GiveEquipmentPage()
        {
            ActionCard(119,CandyHudIcon.Home,"Items: "+new[]{"ALL","POWERS","RANGED","MELEE"}[arsenalFilter],"Click to filter the item listing.",()=>{arsenalFilter=(arsenalFilter+1)%4;scroll=Vector2.zero;});
            var runtime=MatchRuntime.Current;var controls=runtime as IDemoEquipmentControls;
            var everyone=runtime as IDemoLoadoutControls;
            bool host=DemoMode&&CanCommand&&runtime?.Authority==true;
            bool can=host&&(giveAllEquipment?everyone!=null:!session.PlayingHand&&(Demo as ISpectatorControls)?.Spectating!=true&&controls!=null);
            ActionCard(223,CandyHudIcon.Survivor,"Target: "+(giveAllEquipment?"ALL PLAYERS + BOTS":"MYSELF"),"Click to switch targets. Giant hands and enemy NPCs are excluded.",()=>{page=giveAllEquipment?0:1;subsection=giveAllEquipment?1:2;giveAllEquipment=!giveAllEquipment;scroll=Vector2.zero;},everyone!=null);
            float y=327;
            if(giveAllEquipment)
            {
                ActionCard(y,CandyHudIcon.Play,repeatEquipment?"NOW + EVERY RESPAWN":"THIS LIFE ONLY","Click to switch. Respawn grants also cover newly spawned survivors.",()=>repeatEquipment=!repeatEquipment);y+=104;
                ActionCard(y,CandyHudIcon.Home,"Stop respawn grants","Active: "+(everyone?.RespawnEquipment==EquipmentKind.None?"OFF":DeadlyJenga.Presentation.CandyEquipmentView.Name(everyone?.RespawnEquipment??EquipmentKind.None))+". Stops future grants without taking held items away.",()=>everyone.GiveEveryoneEquipment(EquipmentKind.None,false),host&&everyone!=null&&everyone.RespawnEquipment!=EquipmentKind.None);y+=104;
            }
            int row=0;
            foreach(EquipmentKind kind in System.Enum.GetValues(typeof(EquipmentKind)))
            {
                if(giveAllEquipment&&kind==EquipmentKind.FlyingHand)continue;
                if(kind==EquipmentKind.None||arsenalFilter==1&&!EquipmentSpecifications.Get(kind).power||arsenalFilter==2&&(kind<EquipmentKind.Blaster||EquipmentSpecifications.Get(kind).power||EquipmentSpecifications.Get(kind).melee)||arsenalFilter==3&&!EquipmentSpecifications.Get(kind).melee)continue;var item=kind;
                ActionCard(y+row++*104,CandyHudIcon.Play,"Give "+(giveAllEquipment?"everyone ":"myself ")+DeadlyJenga.Presentation.CandyEquipmentView.Name(item),giveAllEquipment?"Replaces current inventory directly. "+(repeatEquipment?"Also gives this item on future respawns; new survivors included.":"Current living survivors only; disables future respawn grants."):"Replaces your one item slot. Your previous item drops at your feet. Timed powers last 20 seconds.",()=>EquipmentAction(()=>giveAllEquipment?everyone.GiveEveryoneEquipment(item,repeatEquipment):controls.GiveEquipment(item)),can);
            }
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
                    ActionCard(y,CandyHudIcon.Home,demoMapPicker.Title(item),"Load this map with the current mode. The map starts fresh.",()=>maps.SelectMap(id),maps.CanSelectMap&&map.available&&id!=maps.CurrentMapId,preview:MapPreviewImages.Get(id));
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
        float DisasterContentHeight()
        {
            float height=350;
            if(Demo!=null)for(int d=0;d<Demo.DisasterCount;d++)height+=104+Demo.DisasterParameters(d).Count*64+44;
            return height;
        }
        void DisastersPage()
        {
            if(Demo==null){Note(125,"Disasters are getting ready...");return;}
            ActionCard(119,CandyHudIcon.Play,"Stop all disasters","Extinguish fires and stop disasters. Reset the map to clear charring.",()=>Demo.Run(DemoCommand.StopDisasters),CanCommand);
            float y=223;
            for(int d=0;d<Demo.DisasterCount;d++)
            {
                int index=d;var definitions=Demo.DisasterParameters(index);var values=Demo.DisasterValues(index);
                bool toggle=Demo.DisasterIsToggle(index);
                ActionCard(y,CandyHudIcon.Play,Demo.DisasterLabel(index)+(toggle?" · toggle":""),toggle?"Turn slippery surfaces on or off.":"Launch with the settings selected below.",()=>disasterStatus=Demo.LaunchDisaster(index),CanCommand,CandyHudVisuals.Peach);
                y+=104;Panel(new Rect(8,y,824,definitions.Count*64+36),CandyHudVisuals.Card);
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
                GUI.enabled=old;GUI.Label(new Rect(28,y+definitions.Count*64+4,780,26),"Next launch only · Stop all restores temporary changes",small);y+=definitions.Count*64+48;
            }
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
            GUILayout.BeginArea(new Rect(12,115,816,5800));ModeSetupPanel.Draw();GUILayout.EndArea();
            GUI.backgroundColor=background;
        }
        void SettingsPage()
        {
            ActionCard(119,CandyHudIcon.Help,"Gameplay hints: "+hintMode,"Adaptive fades back after a moment. Always keeps hints bright. Hidden removes them.",()=>{hintMode=(HintMode)(((int)hintMode+1)%3);changedAt=Time.unscaledTime;});
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
            SocialPanel.Draw(session.Social);
            GUILayout.EndArea();
            GUI.backgroundColor=background;
        }
        void ControlsPage()
        {
            var inspection=CandyThirdPersonInspection.For(session);
            ActionCard(117,CandyHudIcon.Survivor,"Third person / inspect your avatar",session.IsVR?"Stationary mirror: look at its orbit / zoom buttons, then hold RIGHT trigger or pinch.":"Character stays still. Drag mouse to orbit, scroll to zoom; V returns.",()=>inspection.Begin(),inspection?.CanInspect==true);
            var hints=Hints();for(int i=0;i<3;i++){Panel(new Rect(8,221+i*107,824,94),CandyHudVisuals.Card);Icon(new Rect(25,244+i*107,44,44),hints[i].icon,CandyHudVisuals.Mint);GUI.Label(new Rect(88,233+i*107,720,32),hints[i].key,heading);GUI.Label(new Rect(88,272+i*107,720,38),hints[i].text,body);}
            Note(555,!session.IsVR?"MENU: Esc opens / closes. Click a card, or use arrow keys + Enter. Mouse wheel scrolls. Tab switches survivor / giant hand during play. F8 opens Sound Studio.":"MENU: left Y opens / closes. Look at a button, then squeeze RIGHT trigger. With tracked hands, left thumb + middle finger toggles the menu; RIGHT pinch selects.");
            Note(661,"SURVIVOR: jump toward a block, then press jump again near its edge to vault. Hold jump near a wall to catch it; move to climb or shimmy, then release to drop. Rest on secure ground to recover grip.");
            Note(787,"GIANT HAND: desktop WASD flies, Q lowers, E rises, mouse steers. In VR, LEFT stick flies; LEFT trigger rises, LEFT grip lowers. Hold X and flick LEFT stick sideways to turn. With bare hands, pinch the spare hand and move it from its starting position to steer; release to stop.");
            Note(919,"Input icons reflect the demo bindings. They are not remapping controls. After tracking loss, release your gesture before starting again. Survivor movement uses LEFT stick; hand gestures do not replace that stick.");
        }
        public struct Hint {public CandyHudIcon icon;public string key,text;public Hint(CandyHudIcon i,string k,string t){icon=i;key=k;text=t;}}
        public Hint[] Hints()
        {
            bool vr=session.IsVR,hands=vr&&TrackedHands;bool gesture=vr&&Settings!=null&&Settings.StandingPlay;string jumpGesture=Settings!=null&&Settings.JumpGestureLabel=="Fist"?"RIGHT FIST":"RIGHT PINCH";
            if(session.PlayingHand)return new[]{new Hint(vr?CandyHudIcon.Stick:CandyHudIcon.Move,vr?"L STICK":"W A S D","Fly around the castle"),new Hint(vr?CandyHudIcon.Trigger:CandyHudIcon.Flight,vr?"L TRIGGER / GRIP":"E / Q",vr?"Rise / lower · hold X + L stick turns":"Rise / lower · mouse steers"),new Hint(hands?CandyHudIcon.Pinch:CandyHudIcon.Hand,hands?"TRACKED FINGERS":"POINTING HAND",hands?"Move and curl your real fingers":"Push blocks with your hand")};
            var moveIcon=vr?CandyHudIcon.Stick:CandyHudIcon.Move;
            var jumpIcon=gesture?CandyHudIcon.Pinch:vr?CandyHudIcon.ButtonA:CandyHudIcon.Key;
            if(Climbing)return new[]{new Hint(moveIcon,vr?"L STICK":"W A S D","Climb up, down or sideways"),new Hint(jumpIcon,gesture?"HOLD "+jumpGesture:vr?"HOLD A  ·  RIGHT":"HOLD SPACE","Keep your grip on the wall"),new Hint(jumpIcon,gesture?"RELEASE "+jumpGesture:vr?"RELEASE A":"RELEASE SPACE","Let go and drop")};
            return new[]{new Hint(moveIcon,vr?"L STICK":"W A S D",vr?"Move · headset looks around":"Move · mouse looks"),new Hint(jumpIcon,gesture?jumpGesture:vr?"A  ·  RIGHT":"SPACE","Jump · press again at a ledge to vault"),new Hint(jumpIcon,gesture?"HOLD "+jumpGesture:vr?"HOLD A  ·  RIGHT":"HOLD SPACE","Catch a nearby wall and scramble")};
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
            if(DeadlyJenga.Presentation.CandyMatchHud.Expanded){if(hintCanvas)hintCanvas.enabled=false;return;}
            if(session.Demo is ISpectatorControls observer&&observer.Spectating){if(hintCanvas)hintCanvas.enabled=false;return;}
            if(!hintCanvas)BuildHints();var camera=session.ActiveCamera;
            if(viewVR!=session.IsVR||hintCamera!=camera||hintCanvas.renderMode==RenderMode.ScreenSpaceOverlay&&session.IsVR)
            {
                viewVR=session.IsVR;hintCamera=camera;
                DeadlyJenga.Presentation.CandyHudLayout.Configure(hintCanvas,transform,camera,viewVR);
            }
            string context=(session.PlayingHand?"HAND":Climbing?"CLIMB":Vaulting?"VAULT":"SURVIVOR")+InputLabel;
            if(context!=lastContext)
            {
                lastContext=context;changedAt=Time.unscaledTime;
                var hints=Hints();for(int i=0;i<3;i++){hintKeys[i].text=hints[i].key;hintNames[i].text=hints[i].text;hintIcons[i].texture=CandyHudVisuals.Icon(hints[i].icon);}
            }
            bool hidden=session.MenuOpen||studio&&studio.IsOpen||hintMode==HintMode.Hidden||DeadlyJenga.Presentation.CandyMatchHud.Expanded;
            roleText.transform.parent.gameObject.SetActive(MatchRuntime.Current==null);
            float opacity=hidden?0:hintMode==HintMode.Always||Time.unscaledTime-changedAt<9||!session.PlayingHand&&Climbing?hintOpacity:hintOpacity*.62f;
            hintGroup.alpha=Mathf.MoveTowards(hintGroup.alpha,opacity,Time.unscaledDeltaTime*3);hintCanvas.enabled=hintGroup.alpha>.001f;
            roleText.text=session.PlayingHand?"GIANT HAND":"SURVIVOR";statusText.text=!string.IsNullOrEmpty(DemoStatus)?DemoStatus:session.PlayingHand?InputLabel:session.Player.StatusText;
            if(session.Multiplayer!=null&&session.Multiplayer.InvitationFrom.Length>0)statusText.text=session.IsVR?"Nearby invite · Multiplayer":"Nearby invite · Friends";
            menuText.text=session.IsVR?(TrackedHands?"L THUMB + MIDDLE  ·  MENU":"Y  ·  LEFT  /  MENU"):"ESC  Menu     F1  Hints";
        }
        void OnDisable(){if(hintCanvas)hintCanvas.enabled=false;if(studio)studio.ShowLauncher=true;}
        void OnDestroy(){if(hintCanvas)Destroy(hintCanvas.gameObject);if(themed)Destroy(themed);if(studio)studio.ShowLauncher=true;}
    }
}
