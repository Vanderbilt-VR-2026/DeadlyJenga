using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        Text exitLabel,joinCodeLabel;
        Button exitButton;
        BoxCollider exitTarget;
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
            exitButton=HeaderButton("Exit to Lobby",0,out exitTarget);
            var exitRect=(RectTransform)exitButton.transform;
            exitRect.anchoredPosition=new Vector2(0,-407);
            exitRect.sizeDelta=new Vector2(510,54);
            exitTarget.size=new Vector3(510,54,4);
            exitLabel=exitButton.GetComponentInChildren<Text>();
            exitLabel.rectTransform.sizeDelta=new Vector2(490,48);
            exitLabel.fontSize=22;exitLabel.resizeTextMaxSize=22;
            joinCodeLabel=Text("",new Vector2(0,410),new Vector2(570,35),26);
            joinCodeLabel.color=CandyHudVisuals.Mint;
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
            bool showExit=AtHome&&!hostTools&&MatchRuntime.Current!=null;
            exitButton.gameObject.SetActive(showExit);
            bool exitHover=showExit&&Hit(exitTarget,ray);
            exitButton.image.color=exitHover?CandyHudVisuals.Mint:CandyHudVisuals.Peach;
            if(pressed&&exitHover){MatchRuntime.Current.ReturnToLobby();return true;}
            if(spectatePage){PointSpectatePage(camera,pressed);return true;}
            if(!mapPage)return false;
            HideDisasterSliders();
            var maps=Demo as IDemoMapControls;vrMapPicker.Update(maps?.Maps,maps?.CurrentMapId);var options=vrMapPicker.Rows;
            mapOffset=Mathf.Clamp(mapOffset,0,Mathf.Max(0,(options.Count-1)/12*12));
            heading.text="CHANGE MAP\n"+(maps?.MapStatus??"Maps are unavailable");
            for(int i=0;i<buttons.Length;i++)
            {
                int index=mapOffset+i-1;
                bool map=i>=1&&i<=12&&options!=null&&index<options.Count;
                bool shown=map||i==13&&mapOffset>0||i==14&&options!=null&&mapOffset+12<options.Count;
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
                labels[i].text=i==13?"Previous maps":i==14?"More maps":vrMapPicker.Title(row);
                targets[i].enabled=enabled;bool hover=Hit(targets[i],ray);buttons[i].image.color=hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;labels[i].color=hover?CandyHudVisuals.Ink:enabled?Color.white:CandyHudVisuals.Muted;
                if(hover&&pressed){if(i==13)mapOffset-=12;else if(i==14)mapOffset+=12;else if(row.map==null){vrMapPicker.Toggle(row);mapOffset=vrMapPicker.HeaderIndex/12*12;}else maps.SelectMap(row.map.id);break;}
            }
            help.text="Changing maps restarts the map. Expand category headings to browse. Connected players follow the host.";
            return true;
        }
        void PointSpectatePage(Camera camera,bool pressed)
        {
            HideDisasterSliders();
            var controls=Demo as ISpectatorControls;heading.text="WATCH THE MATCH\nYour character stays in the world";
            var review=MatchRuntime.Current as IMatchReviewControls;
            bool spoilers=review?.DevelopmentReviewEnabled==true&&MatchRuntime.Current.Rules.mode==GameModeKind.MurderMystery;
            var ray=pointerRay;
            for(int i=0;i<buttons.Length;i++)
            {
                bool shown=i>=1&&i<=3||spoilers&&(i==5||i==6);buttons[i].gameObject.SetActive(shown);if(!shown)continue;
                targets[i].enabled=true;var rect=(RectTransform)buttons[i].transform;rect.anchoredPosition=new Vector2(0,277-i*50);rect.sizeDelta=new Vector2(540,48);
                labels[i].text=i==1?(controls?.Spectating==true?"Return to your character":"Start spectating"):i==2?"Previous player":"Next player";
                bool enabled=controls!=null&&(i==1?controls.CanSpectate||controls.Spectating:controls.Spectating);
                string watchedId="";
                if(i==5||i==6)
                {
                    var role=i==5?MysteryRole.Murderer:MysteryRole.Sheriff;watchedId=review.PlayerInRole(role);MatchPlayerFrame player=null;
                    foreach(var p in MatchRuntime.Current.Frame.players)if(p.id==watchedId){player=p;break;}
                    labels[i].text="DEV · "+role+": "+(player?.name??"not assigned");enabled=player!=null&&player.alive&&!player.dying;
                }
                targets[i].enabled=enabled;bool hover=Hit(targets[i],ray);buttons[i].image.color=hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;labels[i].color=hover?CandyHudVisuals.Ink:enabled?Color.white:CandyHudVisuals.Muted;
                if(hover&&pressed)
                {if(i>=5)review.WatchPlayer(watchedId);else if(i==1){controls.SetSpectating(!controls.Spectating);session.SetMenu(false);}else{controls.CycleWatched(i==2?-1:1);session.SetMenu(false);}break;}
            }
            help.text=controls?.Spectating==true?"Watching "+controls.WatchedName(0)+"\nRight stick switches players. Open this menu to return.":"No movement commands reach the watched player.\nYour body can still be affected by the match.";
            
        }
    }
}
