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
        Button mapButton,exitButton;
        BoxCollider mapTarget,exitTarget;
        bool mapPage;
        int mapOffset;
        void BuildMapControls()
        {
            mapButton=HeaderButton("Change map",-140,out mapTarget);
            exitButton=HeaderButton("Exit to Lobby",140,out exitTarget);
        }
        Button HeaderButton(string label,float x,out BoxCollider target)
        {
            var go=new GameObject(label,typeof(RectTransform),typeof(Image),typeof(Button),typeof(BoxCollider));go.transform.SetParent(transform,false);
            var rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(260,48);rect.anchoredPosition=new Vector2(x,455);
            var button=go.GetComponent<Button>();button.image.sprite=CandyHudVisuals.Pill;button.image.type=Image.Type.Sliced;
            target=go.GetComponent<BoxCollider>();target.size=new Vector3(260,48,4);target.isTrigger=true;
            var text=Text(label,Vector2.zero,new Vector2(250,48),23);text.transform.SetParent(go.transform,false);text.color=CandyHudVisuals.Ink;
            return button;
        }
        bool PointMapControls(Camera camera,bool pressed)
        {
            var ray=new Ray(camera.transform.position,camera.transform.forward);
            bool exitHover=exitTarget.Raycast(ray,out _,4),mapHover=mapTarget.Raycast(ray,out _,4);
            exitButton.image.color=exitHover?CandyHudVisuals.Mint:CandyHudVisuals.Peach;
            mapButton.image.color=mapHover?CandyHudVisuals.Mint:CandyHudVisuals.Lavender;
            if(pressed&&exitHover){MatchRuntime.Current?.ReturnToLobby();return true;}
            if(pressed&&mapHover){mapPage=!mapPage;mapOffset=0;pressed=false;}
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
