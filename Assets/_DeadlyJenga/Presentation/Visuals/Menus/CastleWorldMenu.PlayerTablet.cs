using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        enum TabletPage { Comfort, Controls, Room, Developer }
        sealed class TabletButton
        {
            public Button Button;
            public BoxCollider Target;
            public Text Label;
            public Outline Focus;
            public Func<string> Title;
            public Action Select;
            public Func<bool> Selected, Allowed;
            public Color Accent;
        }

        static readonly Color TabletPlum = new(.20f,.055f,.22f);
        static readonly Color TabletPanel = new(.34f,.15f,.38f);
        static readonly Color TabletCoral = new(.93f,.36f,.35f);
        static readonly Color TabletCream = new(1f,.95f,.83f);
        readonly List<TabletButton> tabletTabs = new();
        readonly List<TabletButton> tabletActions = new();
        readonly RectTransform[] tabletPages = new RectTransform[4];
        RectTransform legacyDeveloper;
        Text tabletInputLayout, leftControllerHelp, rightControllerHelp, tabletRoomStatus;
        Text leftInputTitle, rightInputTitle;
        RawImage leftController, rightController;
        TabletPage tabletPage;
        bool tabletDeveloperDetails;
        float tabletRefreshAt;

        internal const string TabletOpenedPreference = "DeadlyJenga.PlayerTabletOpened";

        bool DeveloperTabAvailable => session is not ILobbyControls && DemoMode &&
            GameExperiencePanel.Resolve(session)?.Experience != GameExperience.Game;

        void BuildPlayerTablet()
        {
            var old = new List<Transform>();
            foreach (Transform child in transform) old.Add(child);
            legacyDeveloper = TabletRoot("Developer detail controls");
            legacyDeveloper.localScale = Vector3.one * .58f;
            legacyDeveloper.anchoredPosition = new Vector2(0,-38);
            foreach (var child in old) child.SetParent(legacyDeveloper,false);

            var nav = TabletRoot("Player tablet tabs");
            const float tabWidth = 194;
            TabletControl(tabletTabs,nav,()=>"Resume",new(-408,316),new(tabWidth,68),()=>session.SetMenu(false));
            AddTab(TabletPage.Comfort,"Comfort",-204);
            AddTab(TabletPage.Controls,"Controls",0);
            AddTab(TabletPage.Room,"Room",204);
            AddTab(TabletPage.Developer,"Developer",408,()=>DeveloperTabAvailable);
            TabletRule(nav,new Vector2(0,270),new Vector2(1010,4),TabletCoral);

            for (int i=0;i<tabletPages.Length;i++)
                tabletPages[i] = TabletRoot(((TabletPage)i)+" page");
            BuildTabletComfort(tabletPages[(int)TabletPage.Comfort]);
            BuildTabletControls(tabletPages[(int)TabletPage.Controls]);
            BuildTabletRoom(tabletPages[(int)TabletPage.Room]);
            BuildTabletDeveloper(tabletPages[(int)TabletPage.Developer]);
            OpenPlayerTabletPage(TabletPage.Comfort);
        }

        void AddTab(TabletPage page,string title,float x,Func<bool> allowed=null)
        {
            var nav=(RectTransform)tabletTabs[0].Button.transform.parent;
            TabletControl(tabletTabs,nav,()=>title,new(x,316),new(194,68),()=>OpenPlayerTabletPage(page),
                ()=>tabletPage==page,allowed);
        }

        void OpenPlayerTabletPage(TabletPage page)
        {
            if (page==TabletPage.Developer&&!DeveloperTabAvailable) return;
            RestoreEquipmentWheelLayout();
            ResetPages();
            menuCategory=menuSection=-1;
            tabletPage=page;
            tabletDeveloperDetails=false;
            tabletRefreshAt=0;
            legacyDeveloper.gameObject.SetActive(false);
            for (int i=0;i<tabletPages.Length;i++)
                tabletPages[i].gameObject.SetActive(i==(int)page);
        }

        RectTransform TabletRoot(string name)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(transform,false);
            return rect;
        }

        Text TabletText(RectTransform parent,string value,Vector2 p,Vector2 size,int font,
            TextAnchor alignment=TextAnchor.MiddleCenter)
        {
            var label=Text(value,p,size,font);
            label.rectTransform.SetParent(parent,false);
            label.rectTransform.anchoredPosition=p;
            label.fontStyle=FontStyle.Bold;
            label.alignment=alignment;
            label.color=TabletCream;
            label.raycastTarget=false;
            return label;
        }

        void TabletRule(RectTransform parent,Vector2 p,Vector2 size,Color color)
        {
            var image=PointerImage("Candy tablet trim",size);
            image.rectTransform.SetParent(parent,false);
            image.rectTransform.anchoredPosition=p;
            image.color=color;
        }

        TabletButton TabletControl(List<TabletButton> controls,RectTransform parent,Func<string> title,Vector2 p,
            Vector2 size,Action select,Func<bool> selected=null,Func<bool> allowed=null,Color? accent=null,int font=30)
        {
            var button=HeaderButton(title(),0,out var target);
            var rect=(RectTransform)button.transform;
            rect.SetParent(parent,false);
            SizeNavigation(button,target,p,size,font);
            var label=button.GetComponentInChildren<Text>();
            label.fontStyle=FontStyle.Bold;
            label.resizeTextForBestFit=true;
            label.resizeTextMinSize=19;
            label.resizeTextMaxSize=font;
            button.image.pixelsPerUnitMultiplier=.44f;
            var outline=button.gameObject.AddComponent<Outline>();
            outline.effectDistance=new Vector2(3,-3);
            outline.effectColor=Color.clear;
            var entry=new TabletButton {Button=button,Target=target,Label=label,Title=title,Focus=outline,
                Select=select,Selected=selected,Allowed=allowed,Accent=accent??TabletPanel};
            controls.Add(entry);
            return entry;
        }

        void BuildTabletComfort(RectTransform page)
        {
            TabletText(page,"COMFORT",new(0,210),new(900,74),54);
            string[] rows={"Play position","Turn style","Dominant hand"};
            for(int i=0;i<rows.Length;i++)
            {
                float y=106-i*99;
                TabletRule(page,new(0,y),new(990,79),new Color(.27f,.10f,.30f));
                TabletText(page,rows[i],new(-327,y),new(310,65),30,TextAnchor.MiddleLeft);
            }
            TabletControl(tabletActions,page,()=>"Seated",new(-30,106),new(185,60),
                ()=>Settings?.UseStationaryPreset(),()=>Settings!=null&&!Settings.StandingPlay,()=>Settings!=null,TabletCoral);
            TabletControl(tabletActions,page,()=>"Standing",new(170,106),new(185,60),
                ()=>Settings?.UseStandingPreset(),()=>Settings?.StandingPlay==true,()=>Settings!=null,TabletCoral);
            var turns=new[]{("Snap","Snap"),("Smooth","Smooth"),("Physical","PhysicalOnly")};
            for(int i=0;i<turns.Length;i++)
            {
                var (label,value)=turns[i];
                TabletControl(tabletActions,page,()=>label,new(-72+i*165,7),new(150,60),
                    ()=>SetTabletTurn(value),()=>Settings?.TurnLabel==value,()=>Settings!=null,TabletCoral);
            }
            TabletControl(tabletActions,page,()=>"Left",new(-30,-92),new(185,60),
                ()=>SetTabletHand(true),()=>DominantLeft,()=>session.Player!=null,TabletCoral);
            TabletControl(tabletActions,page,()=>"Right",new(170,-92),new(185,60),
                ()=>SetTabletHand(false),()=>!DominantLeft,()=>session.Player!=null,TabletCoral);
            TabletControl(tabletActions,page,()=>Settings?.TurnLabel=="Snap"
                ?$"Snap angle  {Settings.SnapAngle:0}°":Settings?.TurnLabel=="PhysicalOnly"
                ?"Physical turning":$"Turn speed  {Settings?.SmoothTurnSpeed:0}°/s",new(-246,-242),new(466,76),
                ()=>{if(Settings?.TurnLabel=="Snap")Settings.CycleSnapAngle();else Settings?.CycleSmoothTurnSpeed();},
                allowed:()=>Settings!=null&&Settings.TurnLabel!="PhysicalOnly",accent:TabletCoral);
            TabletControl(tabletActions,page,()=>"Recenter height",new(246,-242),new(466,76),
                ()=>Settings?.RecalibrateSeatedHeight(),allowed:()=>Settings!=null,accent:TabletCoral);
            TabletControl(tabletActions,page,()=>"More comfort settings",new(0,-328),new(400,48),
                ()=>CandyComfortPanel.Open(session),accent:TabletPlum,font:24);
        }

        void SetTabletTurn(string turn)
        {
            var settings=Settings;
            if(settings==null||settings.TurnLabel==turn) return;
            var rules=JsonUtility.FromJson<MatchRules>(JsonUtility.ToJson(settings.CurrentRules));
            rules.turn=turn;
            settings.ApplyMatchRules(rules);
        }

        void SetTabletHand(bool left)
        {
            var controls=session.Player?.gameObject.GetComponent<IBlasterControls>()??session as IBlasterControls;
            controls?.SetLeftHanded(left);
            tabletRefreshAt=0;
        }

        void BuildTabletControls(RectTransform page)
        {
            TabletText(page,"HOW TO PLAY",new(0,211),new(960,70),54);
            TabletRule(page,new(0,143),new(685,47),TabletCream);
            tabletInputLayout=TabletText(page,"",new(0,143),new(650,44),25);
            tabletInputLayout.color=TabletPlum;
            leftInputTitle=TabletText(page,"LEFT HAND",new(-245,83),new(360,38),30);
            rightInputTitle=TabletText(page,"RIGHT HAND",new(245,83),new(360,38),30);
            leftController=TabletPicture(page,CandyHudVisuals.ControllerDiagram(0,true),new(-245,-28),new(158,175));
            rightController=TabletPicture(page,CandyHudVisuals.ControllerDiagram(0,false),new(245,-28),new(158,175));
            leftControllerHelp=TabletText(page,"",new(-245,-164),new(460,94),29);
            rightControllerHelp=TabletText(page,"",new(245,-164),new(460,94),29);
            if (session is not ILobbyControls)
                TabletControl(tabletActions,page,()=>"Replay quick tutorial",new(0,-271),new(480,63),()=>{
                    (session as Component)?.GetComponent<CandyDemoHud>()?.ReplayIntro();
                    session.SetMenu(false);
                },accent:TabletCream);
        }

        RawImage TabletPicture(RectTransform parent,Texture texture,Vector2 p,Vector2 size)
        {
            var picture=new GameObject("Tablet illustration",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
            picture.rectTransform.SetParent(parent,false);
            picture.rectTransform.anchoredPosition=p;
            picture.rectTransform.sizeDelta=size;
            picture.texture=texture;
            picture.raycastTarget=false;
            return picture;
        }

        void BuildTabletRoom(RectTransform page)
        {
            TabletText(page,"ROOM",new(0,211),new(960,74),54);
            tabletRoomStatus=TabletText(page,"",new(0,105),new(945,104),32);
            TabletControl(tabletActions,page,()=>"Invite nearby",new(-244,-10),new(456,76),
                ()=>session.Multiplayer?.InviteNearby(),allowed:()=>session.Multiplayer?.CanInviteNearby==true);
            TabletControl(tabletActions,page,()=>"Copy room code",new(244,-10),new(456,76),()=>{
                GUIUtility.systemCopyBuffer=MultiplayerPanel.JoinCode(session.Multiplayer);
            },allowed:()=>session.Multiplayer!=null&&!string.IsNullOrEmpty(MultiplayerPanel.JoinCode(session.Multiplayer)));
            TabletControl(tabletActions,page,()=>session is ILobbyControls?"Join by room code":MultiplayerPanel.LobbyAction(session.Multiplayer),
                new(-244,-121),new(456,76),TabletRoomTravel,
                allowed:()=>session is ILobbyControls lobby?lobby.JoinEnabled:MatchRuntime.Current!=null);
            TabletControl(tabletActions,page,()=>{
                var room=session.Multiplayer;
                if(room==null||!room.CanLeave)return "Leave room";
                if(!room.IsClient)return "Leave / transfer host";
                return roomLeave.Pending(room,MultiplayerPanel.JoinCode(room),Time.unscaledTime)?"Confirm disconnect":"Leave room";
            },new(244,-121),new(456,76),()=>{
                var room=session.Multiplayer;
                if(room!=null&&room.CanLeave&&roomLeave.Select(room,MultiplayerPanel.JoinCode(room),room.IsClient,Time.unscaledTime))
                    room.LeaveSession();
            },allowed:()=>session.Multiplayer?.CanLeave==true,accent:TabletCoral);
        }

        void TabletRoomTravel()
        {
            if (session is ILobbyControls)
            {
                session.SetMenu(false);
                (session as Component)?.GetComponent<IslandLobbyView>()?.OpenRoomCodeFromTablet();
            }
            else MatchRuntime.Current?.ReturnToLobby();
        }

        void BuildTabletDeveloper(RectTransform page)
        {
            TabletText(page,"DEVELOPER",new(-232,211),new(520,72),54,TextAnchor.MiddleLeft);
            TabletText(page,"Host tools",new(396,208),new(190,56),27);
            DeveloperTile(page,"Weapons",0,CandyHudIcon.Trigger,()=>{
                giveAllEquipment=false;giveEquipmentPage=true;equipmentOffset=0;
            },()=>CanCommand&&MatchRuntime.Current is IDemoEquipmentControls,
                CandyEquipmentView.Outline(EquipmentKind.Blaster));
            DeveloperTile(page,"Bots",1,CandyHudIcon.Survivor,()=>{menuCategory=1;menuSection=-1;});
            DeveloperTile(page,"Disasters",2,CandyHudIcon.Flight,()=>{disasterPage=true;disasterSelection=-1;},
                picture:Resources.Load<Texture2D>("LobbyModePictures/disasters"));
            DeveloperTile(page,"Map & Mode",3,CandyHudIcon.Home,()=>{menuCategory=2;menuSection=-1;});
            DeveloperTile(page,"Rebuild",4,CandyHudIcon.Turn,()=>session.ResetMap(),()=>session.CanEditWorld,opensDetails:false);
            DeveloperTile(page,"Spectate",5,CandyHudIcon.Look,()=>spectatePage=true);
            TabletControl(tabletActions,page,()=>"More tools",new(0,-281),new(360,62),
                ()=>ShowDeveloperDetails(()=>{}),accent:CandyHudVisuals.Lavender);
        }

        void DeveloperTile(RectTransform page,string label,int slot,CandyHudIcon icon,Action action,
            Func<bool> allowed=null,Texture picture=null,bool opensDetails=true)
        {
            Vector2 position=new(slot%2==0?-257:257,98-slot/2*128);
            var tile=TabletControl(tabletActions,page,()=>label,position,new(490,104),
                ()=>{if(opensDetails)ShowDeveloperDetails(action);else action();},allowed:allowed,font:39);
            tile.Label.rectTransform.anchoredPosition=new Vector2(52,0);
            tile.Label.rectTransform.sizeDelta=new Vector2(345,88);
            var approved=Resources.Load<Texture2D>("PlayerTabletArt/"+(slot==3?"MapMode":label));
            Texture art=approved?approved:picture?picture:CandyHudVisuals.Icon(icon);
            float scale=Mathf.Min(137f/art.width,90f/art.height);
            TabletPicture((RectTransform)tile.Button.transform,art,new(-163,0),
                new(art.width*scale,art.height*scale));
        }

        void ShowDeveloperDetails(Action action)
        {
            if (!DeveloperTabAvailable) return;
            action();
            tabletDeveloperDetails=true;
            tabletPages[(int)TabletPage.Developer].gameObject.SetActive(false);
            legacyDeveloper.gameObject.SetActive(true);
        }

        void PointPlayerTablet(Camera camera,bool pressed,bool held)
        {
            if(tabletPage==TabletPage.Developer&&!DeveloperTabAvailable) OpenPlayerTabletPage(TabletPage.Comfort);
            if(PointTabletButtons(tabletTabs,pressed)) return;
            if(tabletDeveloperDetails){PointPage(camera,pressed,held);return;}
            if(Time.unscaledTime>=tabletRefreshAt)
            {
                tabletRefreshAt=Time.unscaledTime+.2f;
                if(tabletPage==TabletPage.Controls) RefreshTabletControls(camera);
                if(tabletPage==TabletPage.Room)
                {
                    var room=session.Multiplayer;
                    tabletRoomStatus.text=room==null||string.IsNullOrWhiteSpace(room.RoomInfo)?"Local play":room.RoomInfo;
                    if(!string.IsNullOrWhiteSpace(room?.InvitationFrom))
                        tabletRoomStatus.text+="\nInvite from "+room.InvitationFrom;
                }
            }
            PointTabletButtons(tabletActions,pressed);
        }

        bool PointTabletButtons(List<TabletButton> controls,bool pressed)
        {
            foreach(var control in controls)
            {
                if(ReferenceEquals(controls,tabletTabs))
                    control.Button.gameObject.SetActive(control.Allowed?.Invoke()!=false);
                if(!control.Button.gameObject.activeInHierarchy) continue;
                bool allowed=control.Allowed?.Invoke()!=false;
                control.Button.interactable=allowed;
                control.Target.enabled=allowed;
                bool hover=Hit(control.Target,pointerRay),selected=control.Selected?.Invoke()==true;
                bool developerTile=tabletPage==TabletPage.Developer&&ReferenceEquals(controls,tabletActions);
                control.Focus.effectColor=hover?CandyHudVisuals.Mint:Color.clear;
                control.Button.image.color=hover&&developerTile?Color.Lerp(control.Accent,CandyHudVisuals.Mint,.19f):
                    hover||selected?CandyHudVisuals.Mint:
                    allowed?control.Accent:Color.Lerp(control.Accent,TabletPlum,.65f);
                control.Label.color=hover&&!developerTile||selected||control.Accent==TabletCream&&allowed?TabletPlum:
                    allowed?TabletCream:CandyHudVisuals.Muted;
                string title=(selected&&!ReferenceEquals(controls,tabletTabs)?"✓ ":"")+control.Title();
                if(control.Label.text!=title) control.Label.text=title;
                if(!hover||!pressed)continue;
                control.Select();
                MenuAim.Consume();
                return true;
            }
            return false;
        }

        void RefreshTabletControls(Camera camera)
        {
            if (!session.IsVR)
            {
                tabletInputLayout.text="Keyboard + mouse";
                leftInputTitle.text="KEYBOARD"; rightInputTitle.text="MOUSE";
                leftController.enabled=rightController.enabled=true;
                leftController.texture=CandyHudVisuals.Icon(CandyHudIcon.Key);
                rightController.texture=CandyHudVisuals.Icon(CandyHudIcon.Look);
                leftControllerHelp.text="WASD  ·  Move\nShift  ·  Sprint\nSpace  ·  Jump / hold to climb";
                rightControllerHelp.text="Mouse  ·  Look\nLeft click  ·  Use weapon\nEsc  ·  Open menu";
                return;
            }
            bool left=ControllerPose(camera,true,out _,out _),right=ControllerPose(camera,false,out _,out _);
            bool tracked=session.Player?.HasHandTracking==true;
            tabletInputLayout.text=left&&right
                ?"Two controllers  ·  "+(DominantLeft?"Left":"Right")+" dominant"
                :left||right?(left?"Left":"Right")+" controller"+(tracked?" + tracked hand":""):
                tracked?"Hand tracking":"Waiting for tracking";
            leftInputTitle.text="LEFT HAND"; rightInputTitle.text="RIGHT HAND";
            leftController.enabled=left;
            rightController.enabled=right;
            leftController.texture=CandyHudVisuals.ControllerDiagram(left==DominantLeft?3:1,true);
            rightController.texture=CandyHudVisuals.ControllerDiagram(!DominantLeft?3:1,false);
            leftControllerHelp.text=TabletControllerHelp(true,left,right,tracked);
            rightControllerHelp.text=TabletControllerHelp(false,right,left,tracked);
        }

        string TabletControllerHelp(bool left,bool present,bool other,bool tracked)
        {
            if(!present)return tracked?"Tracked hand\nPinch to select":"Controller inactive";
            if(!other)return "Stick · Move\nGrip + stick · Look\n"+(left?"X · Jump  /  Y · Menu":"A · Jump  /  Grip + B · Menu");
            string roles=left==DominantLeft?"Stick · Look\n"+(left?"X":"A")+" · Jump\nTrigger · Use weapon":
                "Stick · Move\nStick click · Sprint";
            return left?roles+"\nY · Open menu":roles;
        }
    }
}
