using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobbyView
    {
        string setupPage="main",previewId;
        int pickerOffset;
        RawImage mapPreview;
        Text previewMissing,mapCaption,modeCaption,gameHint;
        LobbyModeIcon modeIcon;
        Choice GameChoice(string id,Func<string> title,float y,Action action,float x=0,float width=730,float height=64)
            =>Add(setupStation,id,title,y,action,()=>lobby.CanConfigure,x,width,height,()=>setupPage=="main");
        void BuildGameStation()
        {
            setupStation=Terminal("MAP + MODE",new Vector3(3.05f,1.62f,1),10,new Vector2(900,1100),CandyHudVisuals.Peach);setupFace=setupStation.GetComponent<LobbyTerminalFaces>();
            var preview=new GameObject("Map preview",typeof(RectTransform),typeof(RawImage));preview.transform.SetParent(setupStation,false);mapPreview=preview.GetComponent<RawImage>();mapPreview.raycastTarget=false;mapPreview.rectTransform.sizeDelta=new Vector2(720,220);mapPreview.rectTransform.anchoredPosition=new Vector2(0,300);
            previewMissing=Label(setupStation,"Coming Soon!",300,40,720,220);previewMissing.color=CandyHudVisuals.Muted;
            mapCaption=Label(setupStation,"MAP",155,21,750,35);mapCaption.color=CandyHudVisuals.Muted;
            GameChoice("map-current",()=>lobby.MapTitle+"  ▾",100,()=>{setupPage="maps";pickerOffset=0;});
            modeCaption=Label(setupStation,"GAME MODE",30,21,750,35);modeCaption.color=CandyHudVisuals.Muted;
            GameChoice("mode-current",()=>lobby.ModeTitle+"  ▾",-25,()=>{setupPage="modes";pickerOffset=0;});
            var icon=new GameObject("Game mode icon",typeof(RectTransform),typeof(CanvasRenderer),typeof(LobbyModeIcon));icon.transform.SetParent(setupStation,false);modeIcon=icon.GetComponent<LobbyModeIcon>();modeIcon.raycastTarget=false;modeIcon.color=CandyHudVisuals.Mint;modeIcon.rectTransform.sizeDelta=new Vector2(80,70);modeIcon.rectTransform.anchoredPosition=new Vector2(0,-110);
            GameChoice("mode-settings",()=>"MODE SETTINGS / DEMO TUNING",-190,()=>{setupPage="settings";settingsPage=0;},0,780,60);
            var teamChoice=GameChoice("human-team",()=>Details?.SelectedModeId=="mystery"?"MY SECRET ROLE: "+MysteryChoices.RequestedRole:MatchChoices.SameHumanTeam?"HUMANS: SAME TEAM":"HUMANS: RANDOM TEAMS",-265,()=>{if(Details.SelectedModeId=="mystery")MysteryChoices.RequestedRole=(MysteryRole)(((int)MysteryChoices.RequestedRole+1)%4);else{var s=Details.Setup;s.sameHumanTeam=!s.sameHumanTeam;Details.ConfigureSetup(s);}},0,780,50);
            teamChoice.enabled=()=>Details?.SelectedModeId=="mystery"||lobby.CanConfigure;
            GameChoice("player-target",()=>"PLAYERS: "+(Details?.Setup.population??MatchChoices.BotTarget)+"  ·  HUMANS + BOTS",-330,()=>{var s=Details.Setup;int[] list={10,15,25,50,100};s.population=list[(Array.IndexOf(list,s.population)+1)%list.Length];Details.ConfigureSetup(s);},0,780,50);
            GameChoice("launch",()=>lobby.StartPending?"CANCEL START":"START GAME",-415,lobby.StartOrCancel,0,780,90).enabled=()=>lobby.CanStart;
            setupHint=Label(setupStation,"",-495,20,810,42);setupHint.color=CandyHudVisuals.Muted;
            gameHint=Label(setupStation,"",400,30,780,70);
            for(int i=0;i<13;i++)
            {
                int slot=i;Add(setupStation,"picker-"+i,()=>PickerTitle(slot),320-i*60,()=>Pick(slot),()=>lobby.CanConfigure&&PickerOption(slot)!=null,0,780,52,()=>(setupPage=="maps"||setupPage=="modes")&&PickerOption(slot)!=null);
            }
            Add(setupStation,"picker-next",()=>"MORE…",-470,()=>pickerOffset+=13,()=>lobby.CanConfigure,240,300,50,()=>(setupPage=="maps"||setupPage=="modes")&&PickerList.Count>pickerOffset+13);
            Add(setupStation,"setup-back",()=>"BACK",-470,()=>{if(pickerOffset>0&&(setupPage=="maps"||setupPage=="modes"))pickerOffset=0;else setupPage="main";},()=>true,-240,300,50,()=>setupPage!="main");
            BuildSetupSettings();
        }
        System.Collections.Generic.IReadOnlyList<LobbyOption> PickerList=>setupPage=="maps"?Details.Maps:Details.Modes;
        LobbyOption PickerOption(int slot){int index=pickerOffset+slot;return Details!=null&&index<PickerList.Count?PickerList[index]:null;}
        string PickerTitle(int slot){var p=PickerOption(slot);return p==null?"":p.title+(p.available?"":" · COMING SOON");}
        void Pick(int slot)
        {
            var p=PickerOption(slot);if(p==null)return;
            if(setupPage=="maps")lobby.SelectMap(p.id);else lobby.SelectMode(p.id);setupPage="main";
        }
        void UpdateGameStation()
        {
            bool main=setupPage=="main";
            mapPreview.gameObject.SetActive(main);mapPreview.enabled=mapPreview.texture;previewMissing.gameObject.SetActive(main&&!mapPreview.texture);mapCaption.gameObject.SetActive(main);modeCaption.gameObject.SetActive(main);modeIcon.gameObject.SetActive(main);setupHint.gameObject.SetActive(main);
            if(Details!=null&&previewId!=Details.SelectedMapId){previewId=Details.SelectedMapId;mapPreview.texture=Resources.Load<Texture2D>("LobbyPreviews/"+previewId);if(mapPreview.texture){float aspect=mapPreview.texture.width/(float)mapPreview.texture.height;mapPreview.rectTransform.sizeDelta=new Vector2(Mathf.Min(720,220*aspect),Mathf.Min(220,720/aspect));}modeIcon.SetMode(Details.SelectedModeId);}
            if(Details!=null)modeIcon.SetMode(Details.SelectedModeId);
            gameHint.text=setupPage=="maps"?"CHOOSE MAP":setupPage=="modes"?"CHOOSE GAME MODE":setupPage=="settings"?"DEMO / MODE SETTINGS":"";gameHint.gameObject.SetActive(!main);
        }
    }
}
