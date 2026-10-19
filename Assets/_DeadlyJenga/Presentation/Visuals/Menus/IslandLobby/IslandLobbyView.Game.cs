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
        const int PickerPageSize=7;
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
            GameChoice("map-previous",()=>"◀",100,()=>CycleSetup(true,-1),-365,90);
            GameChoice("map-current",()=>lobby.MapTitle+"  ▾",100,()=>{setupPage="maps";pickerOffset=0;},0,610);
            GameChoice("map-next",()=>"▶",100,()=>CycleSetup(true,1),365,90);
            modeCaption=Label(setupStation,"GAME MODE",30,21,750,35);modeCaption.color=CandyHudVisuals.Muted;
            GameChoice("mode-previous",()=>"◀",-25,()=>CycleSetup(false,-1),-365,90);
            GameChoice("mode-current",()=>lobby.ModeTitle+"  ▾",-25,()=>{setupPage="modes";pickerOffset=0;},0,610);
            GameChoice("mode-next",()=>"▶",-25,()=>CycleSetup(false,1),365,90);
            var icon=new GameObject("Game mode icon",typeof(RectTransform),typeof(CanvasRenderer),typeof(LobbyModeIcon));icon.transform.SetParent(setupStation,false);modeIcon=icon.GetComponent<LobbyModeIcon>();modeIcon.raycastTarget=false;modeIcon.color=CandyHudVisuals.Mint;modeIcon.rectTransform.sizeDelta=new Vector2(80,70);modeIcon.rectTransform.anchoredPosition=new Vector2(0,-110);
            GameChoice("mode-settings",()=>"MODE SETTINGS / DEMO TUNING",-190,()=>{setupPage="settings";settingsPage=0;},0,780,60);
            var teamChoice=GameChoice("human-team",()=>Details?.SelectedModeId=="mystery"?"MY SECRET ROLE: "+MysteryChoices.RequestedRole:MatchChoices.SameHumanTeam?"HUMANS: SAME TEAM":"HUMANS: RANDOM TEAMS",-265,()=>{if(Details.SelectedModeId=="mystery")MysteryChoices.RequestedRole=(MysteryRole)(((int)MysteryChoices.RequestedRole+1)%4);else{var s=Details.Setup;s.sameHumanTeam=!s.sameHumanTeam;Details.ConfigureSetup(s);}},0,780,50);
            teamChoice.enabled=()=>Details?.SelectedModeId=="mystery"||lobby.CanConfigure;
            GameChoice("player-target",()=>"PLAYERS: "+(Details?.Setup.population??MatchChoices.BotTarget)+"  ·  HUMANS + BOTS",-330,()=>{var s=Details.Setup;int[] list={10,15,25,50,100};s.population=list[(Array.IndexOf(list,s.population)+1)%list.Length];Details.ConfigureSetup(s);},0,780,50);
            GameChoice("launch",()=>lobby.StartPending?"CANCEL START":"START GAME",-415,lobby.StartOrCancel,0,780,90).enabled=()=>lobby.CanStart;
            setupHint=Label(setupStation,"",-495,20,810,42);setupHint.color=CandyHudVisuals.Muted;
            gameHint=Label(setupStation,"",400,30,780,70);
            for(int i=0;i<PickerPageSize;i++)
            {
                int slot=i;Add(setupStation,"picker-"+i,()=>PickerTitle(slot),275-i*86,()=>Pick(slot),()=>lobby.CanConfigure&&PickerOption(slot)?.available==true,0,780,70,()=>(setupPage=="maps"||setupPage=="modes")&&PickerOption(slot)!=null);
            }
            Add(setupStation,"picker-previous",()=>"◀ PREVIOUS",-355,()=>pickerOffset=Mathf.Max(0,pickerOffset-PickerPageSize),()=>pickerOffset>0,-215,380,60,()=>setupPage=="maps"||setupPage=="modes");
            Add(setupStation,"picker-next",()=>"NEXT ▶",-355,()=>pickerOffset+=PickerPageSize,()=>Details!=null&&PickerList.Count>pickerOffset+PickerPageSize,215,380,60,()=>setupPage=="maps"||setupPage=="modes");
            Add(setupStation,"setup-back",()=>"BACK",-470,()=>setupPage="main",()=>true,0,380,60,()=>setupPage!="main");
            BuildSetupSettings();
        }
        void CycleSetup(bool map,int step)
        {
            if(Details==null||!lobby.CanConfigure)return;
            var options=map?Details.Maps:Details.Modes;if(options==null||options.Count==0)return;
            string selected=map?Details.SelectedMapId:Details.SelectedModeId;
            int at=0;for(int i=0;i<options.Count;i++)if(options[i].id==selected){at=i;break;}
            for(int i=0;i<options.Count;i++){at=(at+step+options.Count)%options.Count;if(!options[at].available)continue;if(map)lobby.SelectMap(options[at].id);else lobby.SelectMode(options[at].id);return;}
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
            gameHint.text=setupPage=="maps"||setupPage=="modes"?(setupPage=="maps"?"CHOOSE MAP":"CHOOSE GAME MODE")+"  ·  "+(pickerOffset/PickerPageSize+1)+" / "+Mathf.Max(1,Mathf.CeilToInt((Details==null?0:PickerList.Count)/(float)PickerPageSize)):setupPage=="settings"?"DEMO / MODE SETTINGS":"";gameHint.gameObject.SetActive(!main);
        }
    }
}
