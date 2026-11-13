using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;

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
        readonly RawImage[] mapCardPictures = new RawImage[PickerPageSize];
        readonly string[] mapCardIds = new string[PickerPageSize];
        RawImage browserMapPreview;Text browserMapName,browserMapMissing,browserMapDescription;string browserMapId,hoveredMapId;bool browserMapLoaded;
        readonly Text[] modeDescriptions=new Text[8],modePictureMissing=new Text[8];

        internal enum MapBrowserView { Tabs, ArchiveCategories, MapList }
        MapBrowserView mapBrowserView = MapBrowserView.Tabs;
        MapCategoryPicker.MainTab mapActiveTab = MapCategoryPicker.MainTab.PlayApproved;
        int mapArchiveCategoryIndex = 0;
        int mapListScroll = 0;
        int mapArchiveScroll = 0;
        string stagedSelectedMapId;
        string lastLookedMapId;
        Text mapBreadcrumb;
        Text browserMapCategory;
        RectTransform browserBridge;
        Image browserBridgeImage;

        List<LobbyOption> GetActiveSectionMaps()
        {
            if (mapActiveTab == MapCategoryPicker.MainTab.PlayApproved)
                return lobbyMapPicker.GetMapsForMainTab(MapCategoryPicker.MainTab.PlayApproved);
            if (mapActiveTab == MapCategoryPicker.MainTab.InDevelopment)
                return lobbyMapPicker.GetMapsForMainTab(MapCategoryPicker.MainTab.InDevelopment);
            return lobbyMapPicker.GetMapsForArchiveCategory(mapArchiveCategoryIndex);
        }

        LobbyOption MapItemOption(int slot)
        {
            var list = GetActiveSectionMaps();
            int index = mapListScroll + slot;
            return index >= 0 && index < list.Count ? list[index] : null;
        }

        int ArchiveCatIndex(int slot)
        {
            int index = mapArchiveScroll + slot;
            return index >= 0 && index < MapCategoryPicker.Categories.Length ? index : -1;
        }

        string ArchiveCatTitle(int slot)
        {
            int cat = ArchiveCatIndex(slot);
            if (cat < 0) return "";
            return MapCategoryPicker.Categories[cat] + "  (" + lobbyMapPicker.GetArchiveCategoryCount(cat) + ")";
        }

        void PickArchiveCat(int slot)
        {
            int cat = ArchiveCatIndex(slot);
            if (cat < 0) return;
            mapArchiveCategoryIndex = cat;
            mapBrowserView = MapBrowserView.MapList;
            mapListScroll = 0;
            hoveredMapId = null;
            UpdateMapBrowserPreview();
        }

        string MapBreadcrumbTitle()
        {
            if (mapBrowserView == MapBrowserView.Tabs)
                return "MAP CATEGORIES";
            if (mapBrowserView == MapBrowserView.ArchiveCategories)
                return "MAPS  >  ARCHIVE";
            if (mapActiveTab == MapCategoryPicker.MainTab.PlayApproved)
                return "MAPS  >  PLAY APPROVED";
            if (mapActiveTab == MapCategoryPicker.MainTab.InDevelopment)
                return "MAPS  >  IN DEVELOPMENT";
            return "ARCHIVE  >  " + MapCategoryPicker.Categories[mapArchiveCategoryIndex].ToUpperInvariant();
        }

        void MapBrowserGoBack()
        {
            if (mapBrowserView == MapBrowserView.MapList)
            {
                if (mapActiveTab == MapCategoryPicker.MainTab.Archive)
                    mapBrowserView = MapBrowserView.ArchiveCategories;
                else
                    mapBrowserView = MapBrowserView.Tabs;
            }
            else if (mapBrowserView == MapBrowserView.ArchiveCategories)
            {
                mapBrowserView = MapBrowserView.Tabs;
            }
            mapListScroll = 0;
            hoveredMapId = null;
            UpdateMapBrowserPreview();
        }

        void ConfirmMapSelection()
        {
            if (string.IsNullOrEmpty(stagedSelectedMapId)) return;
            if (!lobby.CanConfigure) return;
            if (lobby.SelectMap(stagedSelectedMapId))
                CloseSetupPage();
        }

        void DeselectMapSelection()
        {
            stagedSelectedMapId = null;
            hoveredMapId = null;
            lastLookedMapId = null;
            UpdateMapBrowserPreview();
        }

        void UpdateBridgePosition(int slot)
        {
            if (!browserBridge) return;
            browserBridge.gameObject.SetActive(true);
            browserBridge.anchoredPosition = new Vector2(-70, 175 - slot * 78);
        }

        void HideBridge()
        {
            if (browserBridge) browserBridge.gameObject.SetActive(false);
        }

        static void SetBlownUpPicture(RawImage image, Texture2D texture, float maxWidth, float maxHeight)
        {
            image.texture = texture;
            image.enabled = texture != null;
            if (!texture) return;
            image.uvRect = new Rect(0, 0, 1, 1);
            float aspect = texture.width / (float)texture.height;
            float width = maxWidth;
            float height = width / aspect;
            if (height > maxHeight)
            {
                height = maxHeight;
                width = height * aspect;
            }
            image.rectTransform.sizeDelta = new Vector2(width, height);
        }

        static void SetSquareThumbnail(RawImage image, Texture2D texture, float size)
        {
            image.texture = texture;
            image.enabled = texture != null;
            if (!texture) return;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            float aspect = texture.width / (float)texture.height;
            if (aspect > 1f)
            {
                float crop = 1f / aspect;
                image.uvRect = new Rect((1f - crop) * 0.5f, 0, crop, 1);
            }
            else
            {
                float crop = aspect;
                image.uvRect = new Rect(0, (1f - crop) * 0.5f, 1, crop);
            }
        }
        Choice GameChoice(string id,Func<string> title,float y,Action action,float x=0,float width=730,float height=64)
            =>Add(setupStation,id,title,y,action,()=>lobby.CanConfigure,x,width,height,()=>setupPage=="main"&&
                (id!="island-effects"&&id!="mode-settings"&&id!="human-team"&&id!="player-target"||
                 GameExperiencePanel.Resolve(session)?.Experience!=GameExperience.Game));
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
            // Main tabs
            Add(setupStation,"map-tab-0",()=>"Play Approved  ("+lobbyMapPicker.GetMapsForMainTab(MapCategoryPicker.MainTab.PlayApproved).Count+")",190,()=>{mapActiveTab=MapCategoryPicker.MainTab.PlayApproved;mapBrowserView=MapBrowserView.MapList;mapListScroll=0;hoveredMapId=null;UpdateMapBrowserPreview();},()=>true,-310,430,86,()=>setupPage=="maps"&&mapBrowserView==MapBrowserView.Tabs);
            Add(setupStation,"map-tab-1",()=>"In Development  ("+lobbyMapPicker.GetMapsForMainTab(MapCategoryPicker.MainTab.InDevelopment).Count+")",75,()=>{mapActiveTab=MapCategoryPicker.MainTab.InDevelopment;mapBrowserView=MapBrowserView.MapList;mapListScroll=0;hoveredMapId=null;UpdateMapBrowserPreview();},()=>true,-310,430,86,()=>setupPage=="maps"&&mapBrowserView==MapBrowserView.Tabs);
            Add(setupStation,"map-tab-2",()=>"Archive  ("+lobbyMapPicker.GetMapsForMainTab(MapCategoryPicker.MainTab.Archive).Count+")",-40,()=>{mapActiveTab=MapCategoryPicker.MainTab.Archive;mapBrowserView=MapBrowserView.ArchiveCategories;mapArchiveScroll=0;hoveredMapId=null;UpdateMapBrowserPreview();},()=>true,-310,430,86,()=>setupPage=="maps"&&mapBrowserView==MapBrowserView.Tabs);

            // Back button
            Add(setupStation,"map-back",()=>"< BACK",285,MapBrowserGoBack,()=>true,-310,430,44,()=>setupPage=="maps"&&mapBrowserView!=MapBrowserView.Tabs);

            // Archive categories
            for(int i=0;i<5;i++)
            {
                int catSlot=i;
                Add(setupStation,"archive-cat-"+catSlot,()=>ArchiveCatTitle(catSlot),215-catSlot*68,()=>PickArchiveCat(catSlot),()=>ArchiveCatIndex(catSlot)>=0,-310,430,58,()=>setupPage=="maps"&&mapBrowserView==MapBrowserView.ArchiveCategories&&ArchiveCatIndex(catSlot)>=0);
            }
            Add(setupStation,"cat-scroll-up",()=>"▲ PREVIOUS",260,()=>mapArchiveScroll=Mathf.Max(0,mapArchiveScroll-5),()=>mapArchiveScroll>0,-310,430,30,()=>setupPage=="maps"&&mapBrowserView==MapBrowserView.ArchiveCategories&&mapArchiveScroll>0);
            Add(setupStation,"cat-scroll-down",()=>"▼ NEXT",-135,()=>mapArchiveScroll=Mathf.Min(4,mapArchiveScroll+5),()=>mapArchiveScroll+5<MapCategoryPicker.Categories.Length,-310,430,30,()=>setupPage=="maps"&&mapBrowserView==MapBrowserView.ArchiveCategories&&mapArchiveScroll+5<MapCategoryPicker.Categories.Length);

            // Map list rows
            for(int i=0;i<PickerPageSize;i++)
            {
                int slot=i;Add(setupStation,"picker-"+i,()=>PickerTitle(slot),175-i*78,()=>Pick(slot),()=>PickerOption(slot)?.available==true,0,780,70,()=>setupPage=="maps"&&mapBrowserView==MapBrowserView.MapList&&slot<4&&MapItemOption(slot)!=null);
            }
            Add(setupStation,"map-scroll-up",()=>"▲ PREVIOUS",235,()=>mapListScroll=Mathf.Max(0,mapListScroll-1),()=>mapListScroll>0,-310,430,28,()=>setupPage=="maps"&&mapBrowserView==MapBrowserView.MapList&&mapListScroll>0);
            Add(setupStation,"map-scroll-down",()=>"▼ NEXT",-145,()=>mapListScroll++,()=>mapListScroll+4<GetActiveSectionMaps().Count,-310,430,28,()=>setupPage=="maps"&&mapBrowserView==MapBrowserView.MapList&&mapListScroll+4<GetActiveSectionMaps().Count);

            // Bottom action buttons
            Add(setupStation,"map-select",()=>"SELECT",-350,ConfirmMapSelection,()=>!string.IsNullOrEmpty(stagedSelectedMapId)&&stagedSelectedMapId!=Details?.SelectedMapId&&lobby.CanConfigure,-440,125,58,()=>setupPage=="maps");
            Add(setupStation,"map-deselect",()=>"DESELECT",-350,DeselectMapSelection,()=>!string.IsNullOrEmpty(stagedSelectedMapId),-310,125,58,()=>setupPage=="maps");
            Add(setupStation,"map-cancel",()=>"CANCEL",-350,CloseSetupPage,()=>true,-180,125,58,()=>setupPage=="maps");
            BuildModeCards();
            Add(setupStation,"picker-previous",()=>"< PREVIOUS",-390,()=>pickerOffset=Mathf.Max(0,pickerOffset-PageSize),()=>pickerOffset>0,-215,380,60,()=>setupPage=="modes");
            Add(setupStation,"picker-next",()=>"NEXT >",-390,()=>pickerOffset+=PageSize,()=>PickerPages>pickerOffset/PageSize+1,215,380,60,()=>setupPage=="modes");
            Add(setupStation,"setup-back",()=>"BACK",-470,CloseSetupPage,()=>true,0,380,60,()=>setupPage!="main"&&setupPage!="maps");
            BuildSetupSettings();
        }
        void CloseSetupPage()
        {
            setupPage = "main";
            stagedSelectedMapId = null;
            hoveredMapId = null;
            lastLookedMapId = null;
            HideBridge();
            if (marina) SetMarinaDetail(setupStation, false);
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
            setupPage = page;
            if (page == "maps")
            {
                mapBrowserView = MapBrowserView.Tabs;
                mapActiveTab = MapCategoryPicker.MainTab.PlayApproved;
                mapArchiveCategoryIndex = 0;
                mapListScroll = 0;
                mapArchiveScroll = 0;
                stagedSelectedMapId = null;
                hoveredMapId = null;
                lastLookedMapId = null;
                UpdateMapBrowserPreview();
            }
            else
            {
                pickerOffset = 0;
            }
        }
        int PageSize=>setupPage=="modes"?4:PickerPageSize;
        int PickerPages=>Mathf.Max(1,Mathf.CeilToInt((setupPage=="modes"?Mathf.Max(ModeColumn(false).Length,ModeColumn(true).Length):lobbyMapPicker.Rows.Count)/(float)PageSize));
        MapCategoryPicker.Row PickerRow(int slot){int index=pickerOffset+slot;return index>=0&&index<lobbyMapPicker.Rows.Count?lobbyMapPicker.Rows[index]:null;}
        LobbyOption PickerOption(int slot)=>(setupPage=="maps"&&slot<4?MapItemOption(slot):null)??PickerRow(slot)?.map;
        string PickerTitle(int slot)
        {
            if (setupPage == "maps" && slot < 4)
            {
                var opt = MapItemOption(slot);
                if (opt != null) return opt.title;
            }
            var row = PickerRow(slot);
            return row == null ? "" : lobbyMapPicker.Title(row);
        }
        void Pick(int slot)
        {
            if (setupPage != "maps") return;
            var map = MapItemOption(slot);
            if (map == null || !map.available) return;
            stagedSelectedMapId = map.id;
            hoveredMapId = map.id;
            lastLookedMapId = map.id;
            UpdateMapBrowserPreview();
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
            var option = ModeCardOption(slot);
            if (!lobby.CanConfigure || option == null || !option.available || !lobby.SelectMode(option.id)) return;
            if (marina) CloseSetupPage();
            else OpenPicker("maps");
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
                modePictureMissing[i].gameObject.SetActive(!texture);modeDescriptions[i].text=CandyBrief(option?.description??"",true);
            }
            UpdateMapBrowserPreview();
            gameHint.text=setupPage=="maps"||setupPage=="modes"?(setupPage=="maps"?"2 - CHOOSE MAP":"1 - CHOOSE GAME MODE")+"  -  "+(pickerOffset/PageSize+1)+" / "+PickerPages:setupPage=="settings"?"DEMO / MODE SETTINGS":
                GameExperiencePanel.Resolve(session)?.Experience==GameExperience.Game?lobby.SetupHint:"";
            gameHint.gameObject.SetActive(!main||!string.IsNullOrEmpty(gameHint.text));
        }
        void PreviewMapChoice(string choiceId)
        {
            if(setupPage!="maps")return;
            string preview=null;
            int hoveredSlot=-1;
            if(choiceId!=null&&choiceId.StartsWith("picker-",StringComparison.Ordinal)&&
                int.TryParse(choiceId.Substring(7),out int slot)&&slot<4)
            {
                preview=MapItemOption(slot)?.id;
                hoveredSlot=slot;
            }
            if(preview!=null)
            {
                hoveredMapId=preview;
                lastLookedMapId=preview;
                UpdateMapBrowserPreview();
                UpdateBridgePosition(hoveredSlot);
            }
            else if(choiceId=="Large map frame"||choiceId=="Large map browser preview"||choiceId=="Map browser visual bridge")
            {
                if(!string.IsNullOrEmpty(lastLookedMapId)&&hoveredMapId!=lastLookedMapId)
                {
                    hoveredMapId=lastLookedMapId;
                    UpdateMapBrowserPreview();
                }
            }
            else if(!string.IsNullOrEmpty(choiceId))
            {
                if(hoveredMapId!=null)
                {
                    hoveredMapId=null;
                    UpdateMapBrowserPreview();
                    HideBridge();
                }
            }
        }
        void UpdateMapBrowserPreview()
        {
            if(!browserMapPreview)return;
            bool visible=setupPage=="maps";
            browserMapPreview.gameObject.SetActive(visible);
            if(browserMapName)browserMapName.gameObject.SetActive(visible);
            if(browserMapDescription)browserMapDescription.gameObject.SetActive(visible);
            if(browserMapCategory)browserMapCategory.gameObject.SetActive(visible);
            if(mapBreadcrumb)
            {
                mapBreadcrumb.gameObject.SetActive(visible);
                if(visible)mapBreadcrumb.text=MapBreadcrumbTitle();
            }
            if(!visible)
            {
                if(browserMapMissing)browserMapMissing.gameObject.SetActive(false);
                HideBridge();
                return;
            }
            string id=hoveredMapId??stagedSelectedMapId??Details?.SelectedMapId;
            if(!browserMapLoaded||browserMapId!=id)
            {
                browserMapLoaded=true;browserMapId=id;var texture=MapPreviewImages.Get(id);
                SetBlownUpPicture(browserMapPreview,texture,540,400);
                var option=mapOptions.FirstOrDefault(m=>m.id==id);
                if(browserMapName)browserMapName.text=option?.title??"Choose a map";
                if(browserMapCategory)
                {
                    string cat=MapCategoryPicker.IsPlayApproved(option)?"PLAY APPROVED":
                        MapCategoryPicker.IsInDevelopment(option)?"IN DEVELOPMENT":
                        "ARCHIVE · "+(option!=null?MapCategoryPicker.Categories[MapCategoryPicker.Category(option)].ToUpperInvariant():"GENERAL");
                    browserMapCategory.text=$"[ {cat} ]";
                }
                if(browserMapDescription)browserMapDescription.text=CandyBrief(string.IsNullOrWhiteSpace(option?.description)?"Point to a map to preview it. Select to use it.":option.description);
            }
            if(browserMapMissing)browserMapMissing.gameObject.SetActive(!browserMapPreview.texture);
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
