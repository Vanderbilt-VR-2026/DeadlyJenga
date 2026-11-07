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
        const int MapColumns=5, MapPageSize=MapColumns*2;
        bool MapPickerOpen=>setupPage=="maps"||setupPage=="experimental-maps";
        LobbyOption[] mainMaps=Array.Empty<LobbyOption>(),experimentalMaps=Array.Empty<LobbyOption>();
        LobbyOption[] CurrentMapCards=>setupPage=="experimental-maps"?experimentalMaps:mainMaps;
        readonly RawImage[] mapCardPictures=new RawImage[MapPageSize];
        readonly Text[] mapCardPlaceholders=new Text[MapPageSize];
        RawImage mapPreview,modePreview;
        Text previewMissing,mapCaption,modeCaption,gameHint,modeMissing,modeDescription,pvpCategory,survivalCategory;
        LobbyOption[] pvpModes=Array.Empty<LobbyOption>(),survivalModes=Array.Empty<LobbyOption>(),allModes=Array.Empty<LobbyOption>(),mapOptions=Array.Empty<LobbyOption>();
        string optionsMode;float nextOptionsRefresh;
        readonly System.Collections.Generic.Dictionary<string,Texture2D> modeTextures=new System.Collections.Generic.Dictionary<string,Texture2D>();
        readonly RawImage[] modePictures=new RawImage[8];
        readonly Text[] modeDescriptions=new Text[8],modePictureMissing=new Text[8];
        Choice GameChoice(string id,Func<string> title,float y,Action action,float x=0,float width=730,float height=64)
            =>Add(setupStation,id,title,y,action,()=>lobby.CanConfigure,x,width,height,()=>setupPage=="main");
        void BuildGameStation()
        {
            setupStation=Terminal("MODE + MAP",new Vector3(0,StationY,StationZ),0,new Vector2(900,1100),CandyHudVisuals.Peach);setupStation.name="MAP + MODE";setupFace=setupStation.GetComponent<LobbyTerminalFaces>();
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
            GameChoice("mode-settings",()=>"Match Rules",-145,OpenMatchSettings,0,780,60);
            Add(setupStation,"mystery-role",()=>"My secret role: "+MysteryChoices.RequestedRole,-240,
                ()=>MysteryChoices.RequestedRole=(MysteryRole)(((int)MysteryChoices.RequestedRole+1)%4),
                ()=>true,0,780,50,()=>setupPage=="main"&&Details?.SelectedModeId=="mystery");
            bool VisitingMatch()=>(lobby as ILobbyMatchVisitControls)?.VisitingLobby==true;
            Add(setupStation,"launch",()=>VisitingMatch()?"Return to match":lobby.StartPending?"Cancel start":"Start match",
                -415,lobby.StartOrCancel,()=>lobby.CanStart,0,780,90,()=>setupPage=="main"&&(lobby.IsHost||VisitingMatch()));
            var readiness=lobby as IGameExperienceControls;
            Add(setupStation,"lobby-ready",()=>readiness?.LocalReady==true?"Unready":"Ready",-415,
                ()=>readiness.SetReady(!readiness.LocalReady),()=>lobby.CanLeave&&!lobby.StartPending&&readiness.ReadyCount>0,
                0,780,90,()=>setupPage=="main"&&readiness!=null&&lobby.Connected&&!lobby.IsHost&&!VisitingMatch());
            setupHint=Label(setupStation,"",-495,20,810,42);setupHint.color=CandyHudVisuals.Muted;
            gameHint=Label(setupStation,"",400,30,780,70);
            for(int i=0;i<MapPageSize;i++)
            {
                int slot=i;
                Add(setupStation,"picker-"+slot,()=>MapCard(slot)?.title,0,()=>PickMap(slot),
                    ()=>lobby.CanConfigure&&MapCard(slot)?.available==true,visible:()=>MapPickerOpen&&MapCard(slot)!=null);
            }
            BuildModeCards();
            Add(setupStation,"picker-previous",()=>"Previous",-390,()=>pickerOffset-=PageSize,()=>pickerOffset>0,-215,380,60,()=>PickerPages>1);
            Add(setupStation,"picker-next",()=>"Next",-390,()=>pickerOffset+=PageSize,()=>PickerPages>pickerOffset/PageSize+1,215,380,60,()=>PickerPages>1);
            Add(setupStation,"experimental-maps",()=>"Experimental maps",-365,()=>OpenPicker("experimental-maps"),
                ()=>true,width:270,height:46,visible:()=>setupPage=="maps"&&experimentalMaps.Length>0);
            Add(setupStation,"setup-back",()=>"Back",-470,BackFromSetup,()=>true,0,380,60,()=>setupPage!="main");
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
        void OpenPicker(string page)
        {
            RefreshModeOptions(true);
            setupPage=page;
            pickerOffset=0;
        }
        void BackFromSetup()
        {
            if(setupPage=="dev-settings"){OpenMatchSettings();return;}
            if(setupPage=="experimental-maps"){OpenPicker("maps");return;}
            if(marina)CloseMarinaDetail();else setupPage="main";
        }
        int PageSize=>setupPage=="modes"?4:MapPageSize;
        int PickerPages
        {
            get
            {
                int count=setupPage=="modes"?Mathf.Max(pvpModes.Length,survivalModes.Length):MapPickerOpen?CurrentMapCards.Length:0;
                return Mathf.Max(1,Mathf.CeilToInt(count/(float)PageSize));
            }
        }
        LobbyOption MapCard(int slot)
        {
            int index=pickerOffset+slot;
            return index<CurrentMapCards.Length?CurrentMapCards[index]:null;
        }
        void PickMap(int slot)
        {
            var map=MapCard(slot);
            if(!MapPickerOpen||map?.available!=true||!lobby.CanConfigure)return;
            if(lobby.SelectMap(map.id)){if(marina)CloseMarinaDetail();else setupPage="main";}
        }
        static bool SurvivalMode(string id)=>id=="survival"||id=="infection"||id=="mystery"||id=="demo";
        LobbyOption[] ModeColumn(bool survival)=>survival?survivalModes:pvpModes;
        void RefreshModeOptions(bool force=false)
        {
            var details=Details;if(details==null)return;
            if(!force&&allModes.Length>0&&optionsMode==details.SelectedModeId&&Time.unscaledTime<nextOptionsRefresh)return;
            allModes=details.Modes?.ToArray()??Array.Empty<LobbyOption>();mapOptions=details.Maps?.ToArray()??Array.Empty<LobbyOption>();
            mainMaps=mapOptions.Where(map=>!MapBrowseStatus.Experimental(map)).ToArray();
            experimentalMaps=mapOptions.Where(MapBrowseStatus.Experimental).ToArray();
            optionsMode=details.SelectedModeId;nextOptionsRefresh=Time.unscaledTime+.5f;var modes=allModes;
            pvpModes=modes.Where(p=>p.available&&!SurvivalMode(p.id)).ToArray();
            survivalModes=modes.Where(p=>p.available&&SurvivalMode(p.id)).OrderBy(p=>p.id=="demo"?1:0).ToArray();
            pickerOffset=Mathf.Min(pickerOffset,(PickerPages-1)*PageSize);
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
                modePictureMissing[i]=Label(rect,"Preview unavailable",0,16,116,116);modePictureMissing[i].rectTransform.anchoredPosition=new Vector2(-125,0);modePictureMissing[i].color=CandyHudVisuals.Muted;
                modeDescriptions[i]=Label(rect,"",-26,21,230,72);modeDescriptions[i].rectTransform.anchoredPosition=new Vector2(65,-26);modeDescriptions[i].color=CandyHudVisuals.Muted;
            }
        }
        string ModeCardTitle(int slot){var p=ModeCardOption(slot);return p==null?"":p.id=="demo"?"Sandbox":p.title;}
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
            if(MapPickerOpen)
                for(int i=0;i<mapCardPictures.Length;i++)
                {
                    var picture=MapPreviewImages.Get(MapCard(i)?.id);
                    SetPicture(mapCardPictures[i],picture,172,138);
                    mapCardPlaceholders[i].gameObject.SetActive(!picture);
                }
            bool paged=PickerPages>1;
            gameHint.text=paged?$"Page {pickerOffset/PageSize+1} of {PickerPages}":"";
            gameHint.gameObject.SetActive(paged);
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
