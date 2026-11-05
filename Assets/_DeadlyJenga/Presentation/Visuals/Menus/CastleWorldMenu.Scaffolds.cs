using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        Button wildlifeButton;BoxCollider wildlifeTarget;
        Button scaffoldButton;
        BoxCollider scaffoldTarget;
        Text scaffoldLabel,exitLabel,endLabel;
        RoomConnectionTile connectionTile;
        Button mapButton,exitButton,spectateButton,endButton,inspectionButton;
        BoxCollider mapTarget,exitTarget,spectateTarget,endTarget,inspectionTarget;
        bool mapPage,spectatePage;
        int mapOffset;
        readonly MapCategoryPicker vrMapPicker=new MapCategoryPicker();
        readonly RawImage[] mapThumbnails=new RawImage[15];
        void BuildMapControls()
        {
            for(int i=0;i<buttons.Length;i++)
            {
                var image=new GameObject("Map thumbnail",typeof(RectTransform),typeof(RawImage));image.transform.SetParent(buttons[i].transform,false);mapThumbnails[i]=image.GetComponent<RawImage>();mapThumbnails[i].raycastTarget=false;mapThumbnails[i].rectTransform.anchoredPosition=new Vector2(209,0);image.SetActive(false);
            }
            wildlifeButton=HeaderButton("Wildlife",0,out wildlifeTarget);
            ((RectTransform)wildlifeButton.transform).anchoredPosition=new Vector2(0,510);
            inspectionButton=HeaderButton("Third person",0,out inspectionTarget);
            mapButton=HeaderButton("Maps",-232,out mapTarget);
            spectateButton=HeaderButton("Spectate",-116,out spectateTarget);
            endButton=HeaderButton("End Game",116,out endTarget);endLabel=endButton.GetComponentInChildren<Text>();
            exitButton=HeaderButton("Exit to Lobby",232,out exitTarget);
            exitLabel=exitButton.GetComponentInChildren<Text>();
            connectionTile=RoomConnectionTile.Create(transform);
            connectionTile.SetCompact(new Vector2(0,410));
        }
        Button HeaderButton(string label,float x,out BoxCollider target)
        {
            var go=new GameObject(label,typeof(RectTransform),typeof(Image),typeof(Button),typeof(BoxCollider));go.transform.SetParent(transform,false);
            var rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(112,48);rect.anchoredPosition=new Vector2(x,455);
            var button=go.GetComponent<Button>();button.image.sprite=CandyHudVisuals.Pill;button.image.type=Image.Type.Sliced;
            target=go.GetComponent<BoxCollider>();target.size=new Vector3(112,48,4);target.isTrigger=true;
            var text=Text(label,Vector2.zero,new Vector2(108,48),17);text.transform.SetParent(go.transform,false);text.color=CandyHudVisuals.Ink;
            return button;
        }
        bool PointMapControls(Camera camera,bool pressed)
        {
            for(int i=0;i<mapThumbnails.Length;i++){mapThumbnails[i].gameObject.SetActive(false);labels[i].rectTransform.sizeDelta=new Vector2(472,46);labels[i].rectTransform.anchoredPosition=new Vector2(20,0);}
            if(DeadlyJenga.Presentation.CandyWildlifePanel.Showing(session))return true;
            var ray=pointerRay;
            bool wildlifeHover=Hit(wildlifeTarget,ray);
            wildlifeButton.image.color=wildlifeHover?CandyHudVisuals.Mint:CandyHudVisuals.Lavender;
            if(pressed&&wildlifeHover){DeadlyJenga.Presentation.CandyWildlifePanel.Open(session);return true;}
            var inspection=CandyThirdPersonInspection.For(session);bool canInspect=inspection?.CanInspect==true;
            inspectionButton.gameObject.SetActive(DemoMode);inspectionTarget.enabled=canInspect;
            bool inspectHover=DemoMode&&canInspect&&Hit(inspectionTarget,ray);
            inspectionButton.image.color=inspectHover?CandyHudVisuals.Mint:canInspect?CandyHudVisuals.Lavender:CandyHudVisuals.Card;
            if(pressed&&inspectHover){inspection.Begin();return true;}
            var review=MatchRuntime.Current as IMatchReviewControls;bool endVisible=review?.DevelopmentReviewEnabled==true;
            endLabel.text=MultiplayerPanel.EndAction(session.Multiplayer);
            endButton.gameObject.SetActive(endVisible);endTarget.enabled=endVisible&&review.CanEndGame;
            bool endHover=endVisible&&review.CanEndGame&&Hit(endTarget,ray);
            endButton.image.color=endHover?CandyHudVisuals.Mint:CandyHudVisuals.Lavender;
            if(pressed&&endHover){review.EndGame();return true;}
            bool exitHover=Hit(exitTarget,ray),mapHover=Hit(mapTarget,ray),spectateHover=Hit(spectateTarget,ray);
            exitButton.image.color=exitHover?CandyHudVisuals.Mint:CandyHudVisuals.Peach;
            mapButton.image.color=mapHover?CandyHudVisuals.Mint:CandyHudVisuals.Lavender;
            spectateButton.image.color=spectateHover?CandyHudVisuals.Mint:CandyHudVisuals.Lavender;
            if(pressed&&exitHover){MatchRuntime.Current?.ReturnToLobby();return true;}
            if(pressed&&mapHover){mapPage=!mapPage;spectatePage=false;mapOffset=0;pressed=false;}
            if(pressed&&spectateHover){spectatePage=!spectatePage;mapPage=false;pressed=false;}
            if(spectatePage){PointSpectatePage(camera,pressed);return true;}
            if(!mapPage)return false;
            scaffoldButton.gameObject.SetActive(false);HideDisasterSliders();
            var maps=Demo as IDemoMapControls;vrMapPicker.Update(maps?.Maps,maps?.CurrentMapId);var options=vrMapPicker.Rows;
            mapOffset=Mathf.Clamp(mapOffset,0,Mathf.Max(0,(options.Count-1)/12*12));
            heading.text="CHANGE MAP\n"+(maps?.MapStatus??"Maps are unavailable");
            for(int i=0;i<buttons.Length;i++)
            {
                int index=mapOffset+i-1;
                bool map=i>=1&&i<=12&&options!=null&&index<options.Count;
                bool shown=i==0||map||i==13&&mapOffset>0||i==14&&options!=null&&mapOffset+12<options.Count;
                buttons[i].gameObject.SetActive(shown);if(!shown)continue;
                targets[i].enabled=true;
                var rect=(RectTransform)buttons[i].transform;rect.anchoredPosition=new Vector2(0,277-i*50);rect.sizeDelta=new Vector2(540,48);
                var row=map?options[index]:null;
                mapThumbnails[i].gameObject.SetActive(false);
                labels[i].rectTransform.sizeDelta=new Vector2(500,46);labels[i].rectTransform.anchoredPosition=Vector2.zero;
                if(row?.map!=null)
                {
                    var picture=MapPreviewImages.Get(row.map.id);mapThumbnails[i].texture=picture;mapThumbnails[i].gameObject.SetActive(picture);
                    if(picture){float fit=Mathf.Min(100f/picture.width,40f/picture.height);mapThumbnails[i].rectTransform.sizeDelta=new Vector2(picture.width*fit,picture.height*fit);labels[i].rectTransform.sizeDelta=new Vector2(350,46);labels[i].rectTransform.anchoredPosition=new Vector2(-40,0);}
                }
                bool enabled=!map||row.map==null||DeadlyJenga.Presentation.GameExperiencePanel.CanEditRawChoices(session)&&maps.CanSelectMap&&row.map.available&&row.map.id!=maps.CurrentMapId;
                labels[i].text=i==0?"Back":i==13?"Previous maps":i==14?"More maps":vrMapPicker.Title(row);
                targets[i].enabled=enabled;bool hover=Hit(targets[i],ray);buttons[i].image.color=hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;labels[i].color=hover?CandyHudVisuals.Ink:enabled?Color.white:CandyHudVisuals.Muted;
                if(hover&&pressed){if(i==0)mapPage=false;else if(i==13)mapOffset-=12;else if(i==14)mapOffset+=12;else if(row.map==null){vrMapPicker.Toggle(row);mapOffset=vrMapPicker.HeaderIndex/12*12;}else maps.SelectMap(row.map.id);break;}
            }
            help.text="Changing maps restarts the map. Expand category headings to browse. Connected players follow the host.";
            return true;
        }
        void PointSpectatePage(Camera camera,bool pressed)
        {
            scaffoldButton.gameObject.SetActive(false);HideDisasterSliders();
            var controls=Demo as ISpectatorControls;heading.text="WATCH THE MATCH\nYour character stays in the world";
            var review=MatchRuntime.Current as IMatchReviewControls;
            bool spoilers=review?.DevelopmentReviewEnabled==true&&MatchRuntime.Current.Rules.mode==GameModeKind.MurderMystery;
            var ray=pointerRay;
            for(int i=0;i<buttons.Length;i++)
            {
                bool shown=i<=4||spoilers&&i<=6||i==7&&DemoMode;buttons[i].gameObject.SetActive(shown);if(!shown)continue;
                targets[i].enabled=true;var rect=(RectTransform)buttons[i].transform;rect.anchoredPosition=new Vector2(0,277-i*50);rect.sizeDelta=new Vector2(540,48);
                labels[i].text=i==0?"Back":i==1?(controls?.Spectating==true?"Return to your character":"Start spectating"):i==2?"Previous player":i==3?"Next player":controls?.FourViews==true?"Four views (desktop only)":"Single POV · four views on desktop";
                bool enabled=i==0||controls!=null&&(i==1?(controls.CanSpectate||controls.Spectating):i==4?controls.CanUseFourViews:controls.Spectating);
                string watchedId="";
                if(i==5||i==6)
                {
                    var role=i==5?MysteryRole.Murderer:MysteryRole.Sheriff;watchedId=review.PlayerInRole(role);MatchPlayerFrame player=null;
                    foreach(var p in MatchRuntime.Current.Frame.players)if(p.id==watchedId){player=p;break;}
                    labels[i].text="DEV · "+role+": "+(player?.name??"not assigned");enabled=player!=null&&player.alive&&!player.dying;
                }
                if(i==7){labels[i].text="Inspect your avatar · third person mirror";enabled=CandyThirdPersonInspection.For(session)?.CanInspect==true;}
                targets[i].enabled=enabled;bool hover=Hit(targets[i],ray);buttons[i].image.color=hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;labels[i].color=hover?CandyHudVisuals.Ink:enabled?Color.white:CandyHudVisuals.Muted;
                if(hover&&pressed)
                {if(i==0)spectatePage=false;else if(i==7)CandyThirdPersonInspection.For(session)?.Begin();else if(i>=5)review.WatchPlayer(watchedId);else if(i==1){controls.SetSpectating(!controls.Spectating);session.SetMenu(false);}else if(i==4)controls.SetFourViews(!controls.FourViews);else{controls.CycleWatched(i==2?-1:1);session.SetMenu(false);}break;}
            }
            help.text=controls?.Spectating==true?"Watching "+controls.WatchedName(0)+"\nRight stick switches players. Open this menu to return.":"No movement commands reach the watched player.\nYour body can still be affected by the match.";
            
        }
        void BuildScaffoldButton()
        {
            var go=new GameObject("Deliver scaffold tier",typeof(RectTransform),typeof(Image),typeof(Button),typeof(BoxCollider));go.transform.SetParent(transform,false);
            var rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(540,48);rect.anchoredPosition=new Vector2(0,-473);
            scaffoldButton=go.GetComponent<Button>();scaffoldButton.image.sprite=CandyHudVisuals.Pill;scaffoldButton.image.type=Image.Type.Sliced;
            scaffoldTarget=go.GetComponent<BoxCollider>();scaffoldTarget.size=new Vector3(540,46,4);scaffoldTarget.isTrigger=true;
            scaffoldLabel=Text("Deliver next scaffold tier",Vector2.zero,new Vector2(520,46),21);scaffoldLabel.transform.SetParent(go.transform,false);go.SetActive(false);
        }
        void PointScaffold(Camera camera,bool pressed)
        {
            var controls=Demo as IBuildUpControls;
            bool shown=!networkPage&&!comfortPage&&!tuningPage&&!modePage&&!tabletopPage&&!disasterPage&&controls!=null&&controls.Available;
            scaffoldButton.gameObject.SetActive(shown);
            var rect=GetComponent<RectTransform>();rect.sizeDelta=new Vector2(600,shown?1140:1080);
            help.rectTransform.anchoredPosition=new Vector2(0,shown?-535:-480);
            if(!shown)return;
            scaffoldLabel.text=controls.Delivering?"Next tier arriving":controls.ClimbSecondsRemaining>0?"Climb window: "+Mathf.CeilToInt(controls.ClimbSecondsRemaining)+"s":controls.Stage>=controls.StageCount-1?"All tiers delivered":"Deliver next scaffold tier";
            bool hover=controls.CanAdvance&&Hit(scaffoldTarget,pointerRay);
            scaffoldButton.image.color=hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;scaffoldLabel.color=hover?CandyHudVisuals.Ink:controls.CanAdvance?Color.white:CandyHudVisuals.Muted;
            if(hover&&pressed){controls.AdvanceStage();session.SetMenu(false);}
        }
    }
}
