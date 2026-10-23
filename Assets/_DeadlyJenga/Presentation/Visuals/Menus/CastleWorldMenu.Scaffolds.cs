using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        Button scaffoldButton;
        BoxCollider scaffoldTarget;
        Text scaffoldLabel;
        Button mapButton,exitButton,spectateButton,endButton,inspectionButton;
        BoxCollider mapTarget,exitTarget,spectateTarget,endTarget,inspectionTarget;
        bool mapPage,spectatePage;
        int mapOffset;
        void BuildMapControls()
        {
            inspectionButton=HeaderButton("Third person",0,out inspectionTarget);
            mapButton=HeaderButton("Maps",-232,out mapTarget);
            spectateButton=HeaderButton("Spectate",-116,out spectateTarget);
            endButton=HeaderButton("End Game",116,out endTarget);
            exitButton=HeaderButton("Exit to Lobby",232,out exitTarget);
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
            var ray=new Ray(camera.transform.position,camera.transform.forward);
            var inspection=CandyThirdPersonInspection.For(session);bool canInspect=inspection?.CanInspect==true;
            inspectionButton.gameObject.SetActive(DemoMode);inspectionTarget.enabled=canInspect;
            bool inspectHover=DemoMode&&canInspect&&inspectionTarget.Raycast(ray,out _,4);
            inspectionButton.image.color=inspectHover?CandyHudVisuals.Mint:canInspect?CandyHudVisuals.Lavender:CandyHudVisuals.Card;
            if(pressed&&inspectHover){inspection.Begin();return true;}
            var review=MatchRuntime.Current as IMatchReviewControls;bool endVisible=review?.DevelopmentReviewEnabled==true;
            endButton.gameObject.SetActive(endVisible);endTarget.enabled=endVisible&&review.CanEndGame;
            bool endHover=endVisible&&review.CanEndGame&&endTarget.Raycast(ray,out _,4);
            endButton.image.color=endHover?CandyHudVisuals.Mint:CandyHudVisuals.Lavender;
            if(pressed&&endHover){review.EndGame();return true;}
            bool exitHover=exitTarget.Raycast(ray,out _,4),mapHover=mapTarget.Raycast(ray,out _,4),spectateHover=spectateTarget.Raycast(ray,out _,4);
            exitButton.image.color=exitHover?CandyHudVisuals.Mint:CandyHudVisuals.Peach;
            mapButton.image.color=mapHover?CandyHudVisuals.Mint:CandyHudVisuals.Lavender;
            spectateButton.image.color=spectateHover?CandyHudVisuals.Mint:CandyHudVisuals.Lavender;
            if(pressed&&exitHover){MatchRuntime.Current?.ReturnToLobby();return true;}
            if(pressed&&mapHover){mapPage=!mapPage;spectatePage=false;mapOffset=0;pressed=false;}
            if(pressed&&spectateHover){spectatePage=!spectatePage;mapPage=false;pressed=false;}
            if(spectatePage){PointSpectatePage(camera,pressed);return true;}
            if(!mapPage)return false;
            scaffoldButton.gameObject.SetActive(false);HideDisasterSliders();
            var maps=Demo as IDemoMapControls;var options=maps?.Maps;
            heading.text="CHANGE MAP\n"+(maps?.MapStatus??"Maps are unavailable");
            for(int i=0;i<buttons.Length;i++)
            {
                int index=mapOffset+i-1;
                bool map=i>=1&&i<=12&&options!=null&&index<options.Count;
                bool shown=i==0||map||i==13&&mapOffset>0||i==14&&options!=null&&mapOffset+12<options.Count;
                buttons[i].gameObject.SetActive(shown);if(!shown)continue;
                targets[i].enabled=true;
                var rect=(RectTransform)buttons[i].transform;rect.anchoredPosition=new Vector2(0,277-i*50);rect.sizeDelta=new Vector2(540,46);
                bool enabled=!map||maps.CanSelectMap&&options[index].available&&options[index].id!=maps.CurrentMapId;
                labels[i].text=i==0?"Back":i==13?"Previous maps":i==14?"More maps":options[index].title+(options[index].id==maps.CurrentMapId?" · current":"");
                bool hover=enabled&&targets[i].Raycast(ray,out _,4);buttons[i].image.color=hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;labels[i].color=hover?CandyHudVisuals.Ink:enabled?Color.white:CandyHudVisuals.Muted;
                if(hover&&pressed){if(i==0)mapPage=false;else if(i==13)mapOffset-=12;else if(i==14)mapOffset+=12;else maps.SelectMap(options[index].id);break;}
            }
            help.text="Changing maps restarts the map. Connected players follow the host.";
            transform.position=camera.transform.position+openingForward*1.6f;Physics.SyncTransforms();return true;
        }
        void PointSpectatePage(Camera camera,bool pressed)
        {
            scaffoldButton.gameObject.SetActive(false);HideDisasterSliders();
            var controls=Demo as ISpectatorControls;heading.text="WATCH THE MATCH\nYour character stays in the world";
            var review=MatchRuntime.Current as IMatchReviewControls;
            bool spoilers=review?.DevelopmentReviewEnabled==true&&MatchRuntime.Current.Rules.mode==GameModeKind.MurderMystery;
            var ray=new Ray(camera.transform.position,camera.transform.forward);
            for(int i=0;i<buttons.Length;i++)
            {
                bool shown=i<=4||spoilers&&i<=6||i==7&&Demo?.DemoMode==true;buttons[i].gameObject.SetActive(shown);if(!shown)continue;
                targets[i].enabled=true;var rect=(RectTransform)buttons[i].transform;rect.anchoredPosition=new Vector2(0,277-i*50);rect.sizeDelta=new Vector2(540,46);
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
                bool hover=enabled&&targets[i].Raycast(ray,out _,4);buttons[i].image.color=hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;labels[i].color=hover?CandyHudVisuals.Ink:enabled?Color.white:CandyHudVisuals.Muted;
                if(hover&&pressed)
                {if(i==0)spectatePage=false;else if(i==7)CandyThirdPersonInspection.For(session)?.Begin();else if(i>=5)review.WatchPlayer(watchedId);else if(i==1){controls.SetSpectating(!controls.Spectating);session.SetMenu(false);}else if(i==4)controls.SetFourViews(!controls.FourViews);else{controls.CycleWatched(i==2?-1:1);session.SetMenu(false);}break;}
            }
            help.text=controls?.Spectating==true?"Watching "+controls.WatchedName(0)+"\nRight stick switches players. Open this menu to return.":"No movement commands reach the watched player.\nYour body can still be affected by the match.";
            transform.position=camera.transform.position+openingForward*1.6f;Physics.SyncTransforms();
        }
        void BuildScaffoldButton()
        {
            var go=new GameObject("Deliver scaffold tier",typeof(RectTransform),typeof(Image),typeof(Button),typeof(BoxCollider));go.transform.SetParent(transform,false);
            var rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(540,46);rect.anchoredPosition=new Vector2(0,-473);
            scaffoldButton=go.GetComponent<Button>();scaffoldButton.image.sprite=CandyHudVisuals.Pill;scaffoldButton.image.type=Image.Type.Sliced;
            scaffoldTarget=go.GetComponent<BoxCollider>();scaffoldTarget.size=new Vector3(540,46,4);scaffoldTarget.isTrigger=true;
            scaffoldLabel=Text("Deliver next scaffold tier",Vector2.zero,new Vector2(520,46),21);scaffoldLabel.transform.SetParent(go.transform,false);go.SetActive(false);
        }
        void PointScaffold(Camera camera,bool pressed)
        {
            var controls=Demo as IBuildUpControls;
            bool shown=!networkPage&&!comfortPage&&!tuningPage&&!modePage&&!tabletopPage&&!disasterPage&&controls!=null&&controls.Available;
            scaffoldButton.gameObject.SetActive(shown);
            var rect=GetComponent<RectTransform>();rect.sizeDelta=new Vector2(600,shown?1140:1040);
            help.rectTransform.anchoredPosition=new Vector2(0,shown?-535:-480);
            if(!shown)return;
            scaffoldLabel.text=controls.Delivering?"Next tier arriving":controls.ClimbSecondsRemaining>0?"Climb window: "+Mathf.CeilToInt(controls.ClimbSecondsRemaining)+"s":controls.Stage>=controls.StageCount-1?"All tiers delivered":"Deliver next scaffold tier";
            bool hover=controls.CanAdvance&&scaffoldTarget.Raycast(new Ray(camera.transform.position,camera.transform.forward),out _,4);
            scaffoldButton.image.color=hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;scaffoldLabel.color=hover?CandyHudVisuals.Ink:controls.CanAdvance?Color.white:CandyHudVisuals.Muted;
            if(hover&&pressed){controls.AdvanceStage();session.SetMenu(false);}
        }
    }
}
