using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobbyView
    {
        string setupPage="main",previewId,modePreviewId;
        int pickerOffset;
        readonly MapCategoryPicker lobbyMapPicker=new MapCategoryPicker();
        const int PickerPageSize=10;
        RawImage mapPreview,modePreview;
        Text previewMissing,mapCaption,modeCaption,gameHint,modeMissing,modeDescription,pvpCategory,survivalCategory;
        LobbyOption[] pvpModes=Array.Empty<LobbyOption>(),survivalModes=Array.Empty<LobbyOption>(),allModes=Array.Empty<LobbyOption>(),mapOptions=Array.Empty<LobbyOption>();
        string optionsMode;float nextOptionsRefresh;
        readonly System.Collections.Generic.Dictionary<string,Texture2D> modeTextures=new System.Collections.Generic.Dictionary<string,Texture2D>();
        readonly RawImage[] modePictures=new RawImage[8];
        RawImage browserMapPreview;Text browserMapName,browserMapMissing;string browserMapId;
        readonly Text[] modeDescriptions=new Text[8],modePictureMissing=new Text[8];
        Choice GameChoice(string id,Func<string> title,float y,Action action,float x=0,float width=730,float height=64)
            =>Add(setupStation,id,title,y,action,()=>lobby.CanConfigure,x,width,height,()=>setupPage=="main");
        void BuildGameStation()
        {
            setupStation=Terminal("MODE + MAP",new Vector3(3.05f,StationY,StationZ),0,new Vector2(900,1100),CandyHudVisuals.Peach);setupStation.name="MAP + MODE";setupFace=setupStation.GetComponent<LobbyTerminalFaces>();
            modePreview=Picture(setupStation,"Selected mode picture",-205,300,330,210);
            mapPreview=Picture(setupStation,"Map preview",205,300,330,210);
            modeMissing=Label(setupStation,"PICTURE COMING SOON",300,22,330,210);modeMissing.rectTransform.anchoredPosition=new Vector2(-205,300);modeMissing.color=CandyHudVisuals.Muted;
            previewMissing=Label(setupStation,"MAP PREVIEW COMING SOON",300,22,330,210);previewMissing.rectTransform.anchoredPosition=new Vector2(205,300);previewMissing.color=CandyHudVisuals.Muted;
            modeDescription=Label(setupStation,"",165,23,780,55);modeDescription.color=CandyHudVisuals.Muted;
            modeCaption=Label(setupStation,"1 - GAME MODE",112,21,750,30);modeCaption.color=CandyHudVisuals.Mint;
            GameChoice("mode-previous",()=>"<",65,()=>CycleSetup(false,-1),-365,90);
            GameChoice("mode-current",()=>lobby.ModeTitle+"  v",65,()=>OpenPicker("modes"),0,610);
            GameChoice("mode-next",()=>">",65,()=>CycleSetup(false,1),365,90);
            mapCaption=Label(setupStation,"2 - MAP",3,21,750,30);mapCaption.color=CandyHudVisuals.Mint;
            GameChoice("map-previous",()=>"<",-44,()=>CycleSetup(true,-1),-365,90);
            GameChoice("map-current",()=>lobby.MapTitle+"  v",-44,()=>OpenPicker("maps"),0,610);
            GameChoice("map-next",()=>">",-44,()=>CycleSetup(true,1),365,90);
            GameChoice("island-effects",()=>Details?.Setup.rules.island==IslandRule.Walkable?"ISLAND EFFECTS: OFF · NORMAL MOVEMENT":"ISLAND EFFECTS: ON · "+Details?.Setup.rules.island,-110,()=>{var s=Details.Setup;s.rules.island=s.rules.island==IslandRule.Walkable?IslandRule.Sink:IslandRule.Walkable;Details.ConfigureSetup(s);},0,780,60);
            GameChoice("mode-settings",()=>"MODE SETTINGS / DEMO TUNING",-190,()=>{setupPage="settings";settingsPage=0;},0,780,60);
            var teamChoice=GameChoice("human-team",()=>Details?.SelectedModeId=="mystery"?"MY SECRET ROLE: "+MysteryChoices.RequestedRole:MatchChoices.SameHumanTeam?"HUMANS: SAME TEAM":"HUMANS: RANDOM TEAMS",-265,()=>{if(Details.SelectedModeId=="mystery")MysteryChoices.RequestedRole=(MysteryRole)(((int)MysteryChoices.RequestedRole+1)%4);else{var s=Details.Setup;s.sameHumanTeam=!s.sameHumanTeam;Details.ConfigureSetup(s);}},0,780,50);
            teamChoice.enabled=()=>Details?.SelectedModeId=="mystery"||lobby.CanConfigure;
            GameChoice("player-target",()=>"PLAYERS: "+(Details?.Setup.population??MatchChoices.BotTarget)+"  ·  HUMANS + BOTS",-330,()=>{var s=Details.Setup;int[] list={10,15,25,50,100};s.population=list[(Array.IndexOf(list,s.population)+1)%list.Length];Details.ConfigureSetup(s);},0,780,50);
            GameChoice("launch",()=>(lobby as ILobbyMatchVisitControls)?.VisitingLobby==true?"RETURN TO MATCH":lobby.StartPending?"CANCEL START":"START GAME",-415,lobby.StartOrCancel,0,780,90).enabled=()=>lobby.CanStart;
            setupHint=Label(setupStation,"",-495,20,810,42);setupHint.color=CandyHudVisuals.Muted;
            gameHint=Label(setupStation,"",400,30,780,70);
            for(int i=0;i<PickerPageSize;i++)
            {
                int slot=i;Add(setupStation,"picker-"+i,()=>PickerTitle(slot),275-i*86,()=>Pick(slot),()=>PickerRow(slot)?.map==null||lobby.CanConfigure&&PickerOption(slot)?.available==true,0,780,70,()=>setupPage=="maps"&&PickerRow(slot)!=null);
            }
            BuildModeCards();
            Add(setupStation,"picker-previous",()=>"< PREVIOUS",-390,()=>pickerOffset=Mathf.Max(0,pickerOffset-PageSize),()=>pickerOffset>0,-215,380,60,()=>setupPage=="maps"||setupPage=="modes");
            Add(setupStation,"picker-next",()=>"NEXT >",-390,()=>pickerOffset+=PageSize,()=>PickerPages>pickerOffset/PageSize+1,215,380,60,()=>setupPage=="maps"||setupPage=="modes");
            Add(setupStation,"setup-back",()=>"BACK",-470,()=>setupPage="main",()=>true,0,380,60,()=>setupPage!="main");
            BuildSetupSettings();
        }
        void CycleSetup(bool map,int step)
        {
            if(Details==null||!lobby.CanConfigure)return;
            string selected=map?Details.SelectedMapId:Details.SelectedModeId;
            RefreshModeOptions(true);
            bool experimental=MapBrowseStatus.Experimental(mapOptions.FirstOrDefault(o=>o.id==selected));
            var options=map?mapOptions.Where(o=>MapBrowseStatus.Experimental(o)==experimental).ToArray():allModes;if(options.Length==0)return;
            int at=0;for(int i=0;i<options.Length;i++)if(options[i].id==selected){at=i;break;}
            for(int i=0;i<options.Length;i++){at=(at+step+options.Length)%options.Length;if(!options[at].available)continue;if(map)lobby.SelectMap(options[at].id);else lobby.SelectMode(options[at].id);return;}
        }
        void OpenPicker(string page){RefreshModeOptions(true);setupPage=page;if(page=="maps")browserMapId=null;pickerOffset=page=="maps"?lobbyMapPicker.HeaderIndex/PickerPageSize*PickerPageSize:0;}
        int PageSize=>setupPage=="modes"?4:PickerPageSize;
        int PickerPages=>Mathf.Max(1,Mathf.CeilToInt((setupPage=="modes"?Mathf.Max(ModeColumn(false).Length,ModeColumn(true).Length):lobbyMapPicker.Rows.Count)/(float)PageSize));
        MapCategoryPicker.Row PickerRow(int slot){int index=pickerOffset+slot;return index>=0&&index<lobbyMapPicker.Rows.Count?lobbyMapPicker.Rows[index]:null;}
        LobbyOption PickerOption(int slot)=>PickerRow(slot)?.map;
        string PickerTitle(int slot){var row=PickerRow(slot);return row==null?"":lobbyMapPicker.Title(row);}
        void Pick(int slot)
        {
            var row=PickerRow(slot);if(row==null)return;
            if(row.map==null){lobbyMapPicker.Toggle(row);pickerOffset=lobbyMapPicker.HeaderIndex/PageSize*PageSize;return;}
            if(!lobby.CanConfigure||!row.map.available)return;
            if(lobby.SelectMap(row.map.id))setupPage="main";
        }
        static bool SurvivalMode(string id)=>id=="survival"||id=="infection"||id=="mystery"||id=="demo";
        LobbyOption[] ModeColumn(bool survival)=>survival?survivalModes:pvpModes;
        void RefreshModeOptions(bool force=false)
        {
            var details=Details;if(details==null)return;
            if(!force&&allModes.Length>0&&optionsMode==details.SelectedModeId&&Time.unscaledTime<nextOptionsRefresh)return;
            allModes=details.Modes?.ToArray()??Array.Empty<LobbyOption>();mapOptions=details.Maps?.ToArray()??Array.Empty<LobbyOption>();
            lobbyMapPicker.Update(mapOptions,details.SelectedMapId);
            if(setupPage=="maps")pickerOffset=Mathf.Min(pickerOffset,(PickerPages-1)*PageSize);
            optionsMode=details.SelectedModeId;nextOptionsRefresh=Time.unscaledTime+.5f;var modes=allModes;
            pvpModes=modes.Where(p=>p.available&&!SurvivalMode(p.id)).ToArray();
            survivalModes=modes.Where(p=>p.available&&SurvivalMode(p.id)).OrderBy(p=>p.id=="demo"?1:0).ToArray();
        }
        LobbyOption ModeCardOption(int slot)
        {
            var column=ModeColumn(slot>=4);int index=pickerOffset+slot%4;
            return index>=0&&index<column.Length?column[index]:null;
        }
        void PickModeCard(int slot)
        {
            var option=ModeCardOption(slot);if(option!=null&&option.available)lobby.SelectMode(option.id);
        }
        RawImage Picture(RectTransform parent,string name,float x,float y,float width,float height)
        {
            bool rounded=setupStation&&(parent==setupStation||parent.IsChildOf(setupStation));
            var go=new GameObject(name,typeof(RectTransform),rounded?typeof(LobbyTerminalPicture):typeof(RawImage));go.transform.SetParent(parent,false);
            var image=go.GetComponent<RawImage>();image.raycastTarget=false;image.rectTransform.sizeDelta=new Vector2(width,height);image.rectTransform.anchoredPosition=new Vector2(x,y);return image;
        }
        void BuildModeCards()
        {
            RefreshModeOptions();
            pvpCategory=Label(setupStation,"PvP",330,29,380,45);pvpCategory.rectTransform.anchoredPosition=new Vector2(-205,330);pvpCategory.color=CandyHudVisuals.Peach;
            survivalCategory=Label(setupStation,"SURVIVAL",330,29,380,45);survivalCategory.rectTransform.anchoredPosition=new Vector2(205,330);survivalCategory.color=CandyHudVisuals.Mint;
            for(int i=0;i<8;i++)
            {
                int slot=i;
                var card=Add(setupStation,"mode-card-"+slot,()=>ModeCardTitle(slot),230-(slot%4)*165,()=>PickModeCard(slot),()=>lobby.CanConfigure&&ModeCardOption(slot)?.available==true,slot<4?-205:205,390,145,()=>setupPage=="modes"&&ModeCardOption(slot)!=null);
                var rect=(RectTransform)card.button.transform;
                card.label.rectTransform.sizeDelta=new Vector2(230,49);card.label.rectTransform.anchoredPosition=new Vector2(65,37);card.label.fontSize=25;card.label.resizeTextMaxSize=25;card.label.fontStyle=FontStyle.Bold;
                modePictures[i]=Picture(rect,"Mode picture",-125,0,116,116);
                modePictureMissing[i]=Label(rect,"PICTURE\nCOMING SOON",0,16,116,116);modePictureMissing[i].rectTransform.anchoredPosition=new Vector2(-125,0);modePictureMissing[i].color=CandyHudVisuals.Muted;
                modeDescriptions[i]=Label(rect,"",-26,21,230,72);modeDescriptions[i].rectTransform.anchoredPosition=new Vector2(65,-26);modeDescriptions[i].color=CandyHudVisuals.Muted;
            }
        }
        string ModeCardTitle(int slot){var p=ModeCardOption(slot);return p==null?"":p.id=="demo"?"DEMO / SANDBOX":p.title;}
        void UpdateGameStation()
        {
            RefreshModeOptions();
            bool main=setupPage=="main";
            mapPreview.gameObject.SetActive(main);modePreview.gameObject.SetActive(main);mapCaption.gameObject.SetActive(main);modeCaption.gameObject.SetActive(main);modeDescription.gameObject.SetActive(main);setupHint.gameObject.SetActive(main);
            if(Details!=null&&previewId!=Details.SelectedMapId){previewId=Details.SelectedMapId;SetPicture(mapPreview,MapPreviewImages.Get(previewId),440,138);}
            if(Details!=null&&modePreviewId!=Details.SelectedModeId){modePreviewId=Details.SelectedModeId;SetPicture(modePreview,ModePicture(modePreviewId),440,138);}
            previewMissing.gameObject.SetActive(main&&!mapPreview.texture);modeMissing.gameObject.SetActive(main&&!modePreview.texture);
            modeDescription.text=CandyBrief(allModes.FirstOrDefault(p=>p.id==Details?.SelectedModeId)?.description??"");
            pvpCategory.gameObject.SetActive(setupPage=="modes");survivalCategory.gameObject.SetActive(setupPage=="modes");
            if(setupPage=="modes")for(int i=0;i<modePictures.Length;i++)
            {
                var option=ModeCardOption(i);var texture=option!=null?ModePicture(option.id):null;
                SetPicture(modePictures[i],texture,118,118);
                modePictures[i].uvRect=new Rect(0,0,1,1);
                modePictureMissing[i].gameObject.SetActive(!texture);modeDescriptions[i].text=CandyBrief(option?.description??"",true);
            }
            UpdateMapBrowserPreview();
            gameHint.text=setupPage=="maps"||setupPage=="modes"?(setupPage=="maps"?"2 - CHOOSE MAP":"1 - CHOOSE GAME MODE")+"  -  "+(pickerOffset/PageSize+1)+" / "+PickerPages:setupPage=="settings"?"DEMO / MODE SETTINGS":"";gameHint.gameObject.SetActive(!main);
        }
        void UpdateModeCardFocus(string choiceId)
        {
            int focused=-1;
            if(setupPage=="modes")
            {
                if(choiceId!=null&&choiceId.StartsWith("mode-card-",StringComparison.Ordinal)&&int.TryParse(choiceId.Substring(10),out int slot)&&ModeCardOption(slot)!=null)focused=slot;
                if(focused<0)for(int i=0;i<8;i++)if(ModeCardOption(i)?.id==Details?.SelectedModeId&&ModeCardOption(i)!=null){focused=i;break;}
            }
            for(int i=0;i<8;i++)
            {
                var choice=CandyChoice("mode-card-"+i);if(choice==null)continue;
                var rect=(RectTransform)choice.button.transform;bool focus=i==focused;
                float scale=focus?1.45f:1;
                rect.localScale=Vector3.MoveTowards(rect.localScale,Vector3.one*scale,Time.unscaledDeltaTime*10);
                float x=i<4?-255:255,y=190-i%4*144;
                rect.anchoredPosition3D=focus?new Vector3(i<4?-180:180,Mathf.Clamp(y,-219,161),-12):new Vector3(x,y,0);
                if(focus&&rect.GetSiblingIndex()!=rect.parent.childCount-1)rect.SetAsLastSibling();
            }
        }
        void PreviewMapChoice(string choiceId)
        {
            if(setupPage!="maps"||string.IsNullOrEmpty(choiceId)||!choiceId.StartsWith("picker-",StringComparison.Ordinal))return;
            if(int.TryParse(choiceId.Substring(7),out int slot))
            {
                var option=PickerOption(slot);if(option!=null)UpdateMapBrowserPreview(option.id);
            }
        }
        void UpdateMapBrowserPreview(string id=null)
        {
            if(!browserMapPreview)return;
            bool visible=setupPage=="maps";
            browserMapPreview.gameObject.SetActive(visible);browserMapName.gameObject.SetActive(visible);
            if(!visible){browserMapMissing.gameObject.SetActive(false);return;}
            id=id??browserMapId??Details?.SelectedMapId;
            if(browserMapId!=id||!browserMapPreview.texture)
            {
                browserMapId=id;var texture=MapPreviewImages.Get(id);
                SetPicture(browserMapPreview,texture,940,290);
                if(texture)
                {
                    float aspect=texture.width/(float)texture.height;
                    browserMapPreview.rectTransform.sizeDelta=new Vector2(Mathf.Min(940,290*aspect),Mathf.Min(290,940/aspect));
                    browserMapPreview.uvRect=new Rect(0,0,1,1);
                }
                browserMapName.text=mapOptions.FirstOrDefault(m=>m.id==id)?.title??"Choose a map";
            }
            browserMapMissing.gameObject.SetActive(!browserMapPreview.texture);
        }
        Texture2D ModePicture(string id)
        {
            if(string.IsNullOrEmpty(id))return null;
            if(!modeTextures.TryGetValue(id,out var picture)){picture=Resources.Load<Texture2D>("LobbyModePictures/"+id);modeTextures.Add(id,picture);}
            return picture;
        }
        static void SetPicture(RawImage image,Texture2D texture,float width,float height)
        {
            image.texture=texture;image.enabled=texture;
            if(image is LobbyTerminalPicture){CandyFillPicture(image,width,height);return;}
            if(texture){float aspect=texture.width/(float)texture.height;image.rectTransform.sizeDelta=new Vector2(Mathf.Min(width,height*aspect),Mathf.Min(height,width/aspect));}
        }
    }
}
