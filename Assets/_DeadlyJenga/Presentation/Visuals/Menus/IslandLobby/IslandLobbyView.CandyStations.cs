using System;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobbyView
    {
        void BuildCandyJoin()
        {
            foreach(var t in candyJoinContent.GetComponentsInChildren<Text>())if(t.text=="OR")t.gameObject.SetActive(false);
            candyJoinCodeFrame=CandyPanel(candyJoinContent,"Room code frame",0,185,1020,200,CandyInk).gameObject;
            candyJoinCodeFrame.transform.SetAsFirstSibling();
            var caption=CandyReadout(candyJoinContent,"Room code caption","ROOM CODE",-320,260,350,30,23);caption.fontStyle=FontStyle.Bold;caption.alignment=TextAnchor.MiddleLeft;
            candyJoinStatus=CandyReadout(candyJoinContent,"Join connection status","READY TO JOIN",0,294,500,38,23);candyJoinStatus.color=CandyMint;
            var code=CandyPlace("code",-145,185,690,96);code.faceColor=CandyInk;code.label.enabled=false;code.selected=()=>lobby.CodeFocused;
            for (int i = 0; i < 12; i++)
            {
                var slot = CandyPanel((RectTransform)code.button.transform, "Code character " + i,
                    (i - 5.5f) * 54, 0, 48, 76, new Color(.12f, .09f, .20f));
                var letter = CandyReadout(slot, "Character", "", 0, 0, 44, 66, 34);
                letter.fontStyle = FontStyle.Bold;
                candyCodeLetters.Add(letter);
            }
            var join=CandyPlace("join",380,185,250,96);CandyPrimary(join);join.title=()=>"JOIN ROOM";CandyText(join,0,0,226,80,30);
            CandyReadout(candyJoinContent,"Typing help","Use six room characters, or enter a full region code.",0,112,950,28,21).color=CandyMuted;
            CandyReadout(candyJoinContent,"Discovery divider","OR FIND A ROOM",0,60,650,34,23).color=CandyMuted;
            var search=CandyPlace("detect-nearby",0,-10,1000,80);search.faceColor=new Color(.74f,.55f,.96f);CandyText(search,25,13,780,38,31);
            search.description=CandyReadout((RectTransform)search.button.transform,"Search subtitle","Find hosts on your local network.",25,-22,780,28,21);
            CandyChoiceIcon(search,LobbyTerminalGlyph.Symbol.Radar,-440,0,40);
            for(int i=0;i<3;i++)
            {
                var room=CandyPlace("nearby-room-"+i,0,-103-i*78,1000,70);CandyText(room,-20,0,850,64,24);
                CandyChoiceIcon(room,LobbyTerminalGlyph.Symbol.Play,450,0,26);
            }
            CandyPlace("nearby-next",250,-323,470,44);CandyPlace("code-instead",-250,-323,470,44);
            var invitation=CandyPlace("accept-invite",0,-210,1000,76);CandyPrimary(invitation);CandyText(invitation,0,0,950,66,26);
            CandyPlace("leave-guest",0,-315,1000,60);
            joinHint.rectTransform.anchoredPosition=new Vector2(0,-380);joinHint.rectTransform.sizeDelta=new Vector2(1000,56);
            joinHint.fontSize=20;joinHint.resizeTextMaxSize=20;joinHint.resizeTextMinSize=18;joinHint.color=CandyMuted;
            candyJoinEmpty=CandyReadout(candyJoinContent,"Discovery empty state","",0,-245,960,100,24);candyJoinEmpty.color=CandyMuted;
        }
        float copiedHostCodeUntil;
        void BuildCandyHost()
        {
            var status=CandyPanel(candyHostContent,"Room status",0,294,420,44,CandyFace);status.SetAsFirstSibling();
            roomCount.rectTransform.anchoredPosition=new Vector2(0,294);roomCount.rectTransform.sizeDelta=new Vector2(400,38);
            roomCount.fontSize=23;roomCount.resizeTextMaxSize=23;roomCount.resizeTextMinSize=21;roomCount.color=CandyMint;
            var create=CandyPlace("host",0,190,1000,110);CandyPrimary(create);create.title=()=>"CREATE ROOM";CandyText(create,25,17,820,55,40);
            create.description=CandyReadout((RectTransform)create.button.transform,"Create room subtitle","Get a code. Bring your friends.",25,-30,820,30,24);
            CandyChoiceIcon(create,LobbyTerminalGlyph.Symbol.Plus,-430,0,47);
            var code=CandyPanel(candyHostContent,"Connected room code",0,143,1000,150,CandyInk);code.SetAsFirstSibling();candyHostCodeFrame=code.gameObject;
            CandyReadout(code,"Code label","ROOM CODE",0,47,900,39,27).color=CandyMuted;
            hostCode.rectTransform.anchoredPosition=new Vector2(0,119);hostCode.rectTransform.sizeDelta=new Vector2(940,82);hostCode.fontSize=66;hostCode.resizeTextMaxSize=66;hostCode.resizeTextMinSize=42;hostCode.fontStyle=FontStyle.Bold;hostCode.color=CandyCream;
            var copy=CandyAdd(candyHostContent,"copy-code",()=>Time.unscaledTime<copiedHostCodeUntil?"COPIED":"COPY CODE",
                0,-9,1000,90,()=>{GUIUtility.systemCopyBuffer=lobby.HostCode;copiedHostCodeUntil=Time.unscaledTime+1.5f;},
                ()=>lobby.Connected&&!string.IsNullOrEmpty(lobby.HostCode),()=>lobby.Connected&&!string.IsNullOrEmpty(lobby.HostCode));
            CandyPrimary(copy);CandyChoiceIcon(copy,LobbyTerminalGlyph.Symbol.Plus,-377,0,42);CandyText(copy,0,0,760,72,38);
            var region=CandyPlace("region",0,98,1000,45);region.title=()=>"REGION:  "+lobby.Region.ToUpperInvariant()+(lobby.CanHost?"     CHANGE":"");
            region.visible=()=>!lobby.Connected;CandyChoiceIcon(region,LobbyTerminalGlyph.Symbol.Globe,-440,0,34);CandyText(region,25,0,850,54,26);
            var invite=CandyPlace("invite-all",0,-117,1000,86);CandyChoiceIcon(invite,LobbyTerminalGlyph.Symbol.People,-377,0,42);CandyText(invite,0,0,760,70,36);
            CandyPlace("request-host",0,-212,1000,52);CandyPlace("shared-controls",0,-212,1000,52);
            CandyPlace("abandon-host",0,-278,1000,52);
            requestLabel.rectTransform.anchoredPosition=new Vector2(-100,-270);requestLabel.rectTransform.sizeDelta=new Vector2(760,58);
            requestLabel.fontSize=24;requestLabel.resizeTextMaxSize=24;requestLabel.color=CandyCream;
            CandyPlace("request-next",420,-270,180,50);CandyPlace("request-accept",-255,-335,490,58);CandyPlace("request-deny",255,-335,490,58);
            CandyPrimary(CandyChoice("request-accept"));
            candyJoinedNote=CandyReadout(candyHostContent,"Connected instructions","Share the code above.\nChoose your mode and map at the next terminal.",0,-350,980,72,23);candyJoinedNote.color=CandyMuted;
            var help=CandyPanel(candyHostContent,"How hosting works",0,-205,1000,230,CandyFace);help.SetAsFirstSibling();candyHostHelp=help.gameObject;
            CandyReadout(help,"Help heading","HOW IT WORKS",-130,82,680,38,30).fontStyle=FontStyle.Bold;
            string[] steps={"Create your room","Share your join code","Pick a mode and start next door"};
            for(int i=0;i<steps.Length;i++)
            {
                var badge=CandyPanel(help,"Step "+(i+1),-430,26-i*54,44,44,CandyLavender);
                CandyReadout(badge,"Step number",(i+1).ToString(),0,0,36,38,24).fontStyle=FontStyle.Bold;
                var line=CandyReadout(help,"Hosting step",steps[i],-115,26-i*54,560,44,24);line.alignment=TextAnchor.MiddleLeft;
            }
            var art=Picture(help,"Friends illustration",345,-4,240,190);SetPicture(art,ModePicture("teamtickets"),240,190);
            hostHint.rectTransform.anchoredPosition=new Vector2(0,-405);hostHint.rectTransform.sizeDelta=new Vector2(1000,46);
            hostHint.fontSize=20;hostHint.resizeTextMaxSize=20;hostHint.resizeTextMinSize=18;hostHint.color=CandyMuted;
        }
        // MAP + MODE layout on a 1900 x 880 canvas (x -950..950, y -440..440).
        const float SummaryPictureWidth = 490, SummaryPictureHeight = 290, ModeCardPicture = 112;
        const float MapPreviewWidth = 900, MapPreviewHeight = 470, MapThumbSize = 76;
        const float MapRowTop = 150, MapRowStep = 95, SettingFillWidth = 1240;
        const float ListX = -520, ListWidth = 760;
        void BuildCandySetup()
        {
            // Main page: mode and map cards on the left, match options on the right.
            foreach(string id in new[]{"mode-previous","mode-next","map-previous","map-next"})CandyChoice(id).visible=()=>false;
            var mode=CandyPlace("mode-current",-645,30,530,500);var map=CandyPlace("map-current",-95,30,530,500);
            mode.title=()=>lobby.ModeTitle;map.title=()=>lobby.MapTitle;mode.selected=()=>true;map.edgeColor=CandyPeach;
            CandyText(mode,-10,-130,440,60,40);CandyText(map,-10,-130,440,60,40);
            foreach(var picture in new Graphic[]{modePreview,mapPreview,modeMissing,previewMissing})
            {
                picture.rectTransform.SetParent((picture==modePreview||picture==modeMissing?mode:map).button.transform,false);
                picture.rectTransform.anchoredPosition=new Vector2(0,75);
            }
            modeMissing.rectTransform.sizeDelta=previewMissing.rectTransform.sizeDelta=new Vector2(SummaryPictureWidth,SummaryPictureHeight);
            modeDescription.rectTransform.SetParent(mode.button.transform,false);modeDescription.rectTransform.anchoredPosition=new Vector2(0,-195);modeDescription.rectTransform.sizeDelta=new Vector2(490,70);
            modeDescription.fontSize=26;modeDescription.resizeTextMaxSize=26;modeDescription.resizeTextMinSize=22;modeDescription.color=CandyMuted;mode.description=modeDescription;
            candyMapDescription=CandyReadout((RectTransform)map.button.transform,"Map description","Choose your arena.",0,-195,490,70,26);candyMapDescription.color=CandyMuted;map.description=candyMapDescription;
            CandyChoiceIcon(mode,LobbyTerminalGlyph.Symbol.Chevron,230,-130,34);CandyChoiceIcon(map,LobbyTerminalGlyph.Symbol.Chevron,230,-130,34);
            modeCaption.rectTransform.anchoredPosition=new Vector2(-645,305);mapCaption.rectTransform.anchoredPosition=new Vector2(-95,305);
            modeCaption.rectTransform.sizeDelta=mapCaption.rectTransform.sizeDelta=new Vector2(530,40);
            modeCaption.fontSize=mapCaption.fontSize=30;modeCaption.resizeTextMaxSize=mapCaption.resizeTextMaxSize=30;
            modeCaption.text="1  MODE";mapCaption.text="2  MAP";modeCaption.color=CandyMint;mapCaption.color=CandyPeach;
            const float rightX=580,rightWidth=660;
            var population=CandyPlace("player-target",rightX,240,rightWidth,96);population.title=()=>"PLAYERS";
            CandyText(population,-150,0,190,70,32,TextAnchor.MiddleLeft);CandyChoiceIcon(population,LobbyTerminalGlyph.Symbol.People,-285,0,40);
            var value=CandyPanel((RectTransform)population.button.transform,"Population value",20,0,140,72,CandyInk);
            candyPlayerValue=CandyReadout(value,"Player count","10",0,0,130,64,38);candyPlayerValue.fontStyle=FontStyle.Bold;
            var minus=CandyAdd(candySetupContent,"population-minus",()=>"-",rightX+160,240,90,84,()=>AdjustCandyPopulation(-1),()=>lobby.CanConfigure&&Details!=null,()=>setupPage=="main");
            var plus=CandyAdd(candySetupContent,"population-plus",()=>"+",rightX+265,240,90,84,()=>AdjustCandyPopulation(1),()=>lobby.CanConfigure&&Details!=null,()=>setupPage=="main");
            minus.faceColor=plus.faceColor=CandyPeach;minus.edgeColor=plus.edgeColor=CandyPeach;
            CandyText(minus,0,0,70,72,44);CandyText(plus,0,0,70,72,44);
            ((RectTransform)minus.button.transform).anchoredPosition3D=new Vector3(rightX+160,240,-8);((RectTransform)plus.button.transform).anchoredPosition3D=new Vector3(rightX+265,240,-8);
            var island=CandyPlace("island-effects",rightX,128,rightWidth,96);
            island.title=()=>Details?.Setup.rules.island==DeadlyJenga.Core.IslandRule.Walkable?"ISLAND EFFECTS: OFF":"ISLAND EFFECTS: "+Details?.Setup.rules.island.ToString().ToUpperInvariant();
            CandyText(island,35,0,540,80,30);CandyChoiceIcon(island,LobbyTerminalGlyph.Symbol.Island,-285,0,40);
            var settings=CandyPlace("mode-settings",rightX,16,rightWidth,96);settings.title=()=>"MODE SETTINGS";settings.faceColor=CandyLavender;
            CandyChoiceIcon(settings,LobbyTerminalGlyph.Symbol.Gear,-285,0,40);CandyText(settings,35,0,540,80,32);
            CandyPlace("human-team",rightX,-96,rightWidth,96);CandyText(CandyChoice("human-team"),0,0,620,80,28);
            var launch=CandyPlace("launch",-370,-315,1080,110);CandyPrimary(launch);CandyText(launch,25,0,900,92,50);
            CandyChoiceIcon(launch,LobbyTerminalGlyph.Symbol.Play,-420,0,54);
            var room=CandyPanel(candySetupContent,"Room population",rightX,-315,rightWidth,110,CandyFace);candyMainDecor.Add(room.gameObject);
            candyRoomStatus=CandyReadout(room,"Room count","ROOM\n1 / 10",0,0,600,96,32);
            setupHint.rectTransform.anchoredPosition=new Vector2(0,-398);setupHint.rectTransform.sizeDelta=new Vector2(1760,36);setupHint.fontSize=26;setupHint.resizeTextMaxSize=26;setupHint.resizeTextMinSize=20;setupHint.color=CandyMuted;
            gameHint.rectTransform.anchoredPosition=new Vector2(0,312);gameHint.rectTransform.sizeDelta=new Vector2(1000,30);gameHint.fontSize=26;gameHint.resizeTextMaxSize=26;gameHint.resizeTextMinSize=20;gameHint.color=CandyMuted;

            // Map page: list on the left, large preview on the right.
            mapBreadcrumb = CandyReadout(candySetupContent, "Map breadcrumb", "MAP CATEGORIES", ListX, 270, ListWidth, 40, 30);
            mapBreadcrumb.color = new Color(.68f, .58f, .80f);
            mapBreadcrumb.fontStyle = FontStyle.Bold;
            mapBreadcrumb.alignment = TextAnchor.MiddleLeft;
            for (int i = 0; i < 3; i++)
            {
                var tabChoice = CandyPlace("map-tab-" + i, ListX, 180 - i * 130, ListWidth, 110);
                CandyText(tabChoice, -20, 0, 660, 90, 38, TextAnchor.MiddleLeft);
                CandyChoiceIcon(tabChoice, LobbyTerminalGlyph.Symbol.Chevron, 335, 0, 42);
            }
            var backBtn = CandyPlace("map-back", ListX, 215, ListWidth, 56);
            CandyText(backBtn, 30, 0, 660, 48, 28, TextAnchor.MiddleLeft);
            CandyChoiceIcon(backBtn, LobbyTerminalGlyph.Symbol.Back, -340);
            for (int i = 0; i < 5; i++)
            {
                var catChoice = CandyPlace("archive-cat-" + i, ListX, 145 - i * 72, ListWidth, 64);
                CandyText(catChoice, -20, 0, 660, 56, 30, TextAnchor.MiddleLeft);
                CandyChoiceIcon(catChoice, LobbyTerminalGlyph.Symbol.Chevron, 340);
            }
            var catUp = CandyPlace("cat-scroll-up", ListX - 195, -220, 370, 50);
            CandyText(catUp, 0, 0, 350, 44, 26);
            var catDown = CandyPlace("cat-scroll-down", ListX + 195, -220, 370, 50);
            CandyText(catDown, 0, 0, 350, 44, 26);
            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                var c = CandyPlace("picker-" + i, ListX, MapRowTop - i * MapRowStep, ListWidth, 86);
                c.selected = () => (stagedSelectedMapId ?? Details?.SelectedMapId) == MapItemOption(slot)?.id && MapItemOption(slot) != null;
                c.selectionGlyph = true;
                CandyText(c, 40, 0, 560, 74, 32, TextAnchor.MiddleLeft);
                mapCardPictures[i] = Picture((RectTransform)c.button.transform, "Map card thumbnail", -325, 0, MapThumbSize, MapThumbSize);
                mapCardPictures[i].enabled = false;
                CandyChoiceIcon(c, LobbyTerminalGlyph.Symbol.Check, 340, 0, 38);
            }
            for (int i = 4; i < PickerPageSize; i++)
            {
                CandyPlace("picker-" + i, -2000, 0, 10, 10);
            }
            var mapUp = CandyPlace("map-scroll-up", ListX - 195, -220, 370, 50);
            CandyText(mapUp, 0, 0, 350, 44, 26);
            var mapDown = CandyPlace("map-scroll-down", ListX + 195, -220, 370, 50);
            CandyText(mapDown, 0, 0, 350, 44, 26);
            var selectBtn = CandyPlace("map-select", ListX - 255, -315, 240, 84);
            CandyText(selectBtn, 0, 0, 220, 72, 30);
            var deselectBtn = CandyPlace("map-deselect", ListX, -315, 240, 84);
            CandyText(deselectBtn, 0, 0, 220, 72, 30);
            var cancelBtn = CandyPlace("map-cancel", ListX + 255, -315, 240, 84);
            CandyText(cancelBtn, 0, 0, 220, 72, 30);
            var rightFrame = CandyPanel(candySetupContent, "Large map frame", 420, -10, 940, 650, CandyFace);
            mapBrowserRightFrame = rightFrame;
            browserMapPreview = Picture(rightFrame, "Large map browser preview", 0, 70, MapPreviewWidth, MapPreviewHeight);
            browserMapName = CandyReadout(rightFrame, "Previewed map name", "", 0, -200, 900, 50, 40);
            browserMapName.fontStyle = FontStyle.Bold;
            browserMapName.color = CandyMint;
            browserMapCategory = CandyReadout(rightFrame, "Previewed map category", "", 0, -245, 900, 34, 26);
            browserMapCategory.fontStyle = FontStyle.Bold;
            browserMapCategory.color = CandyPeach;
            browserMapDescription = CandyReadout(rightFrame, "Previewed map description", "", 0, -288, 900, 46, 24);
            browserMapDescription.color = CandyCream;
            browserMapMissing = CandyReadout(rightFrame, "Map preview unavailable", "MAP PREVIEW COMING SOON", 0, 70, 800, 70, 36);
            browserMapMissing.color = CandyMuted;
            var bridgeGo = new GameObject("Map browser visual bridge", typeof(RectTransform), typeof(Image));
            bridgeGo.transform.SetParent(candySetupContent, false);
            browserBridge = bridgeGo.GetComponent<RectTransform>();
            browserBridge.sizeDelta = new Vector2(90, 56);
            browserBridgeImage = bridgeGo.GetComponent<Image>();
            browserBridgeImage.sprite = candyFaceSprite;
            browserBridgeImage.color = CandyMint;
            browserBridgeImage.raycastTarget = false;
            browserBridge.gameObject.SetActive(false);

            // Mode page and settings page share the bottom row: previous, back, next.
            CandyPlace("picker-previous",-650,-335,500,76);CandyPlace("picker-next",650,-335,500,76);CandyPlace("setup-back",0,-335,500,76);
            foreach(string id in new[]{"picker-previous","picker-next","setup-back","settings-prev","settings-next"})CandyText(CandyChoice(id),0,0,440,64,32);
            CandyChoice("picker-previous").visible=()=> setupPage=="modes"&&PickerPages>1;
            CandyChoice("picker-next").visible=()=> setupPage=="modes"&&PickerPages>1;
            CandyChoiceIcon(CandyChoice("setup-back"),LobbyTerminalGlyph.Symbol.Back,-200);
            CandyStyleModeCards();
            for(int i=0;i<6;i++)
            {
                CandyPlace("setting-value-"+i,0,230-i*88,1300,78);CandyText(CandyChoice("setting-value-"+i),0,0,1240,66,32);
                CandyPlace("setting-minus-"+i,-730,230-i*88,120,78);CandyPlace("setting-plus-"+i,730,230-i*88,120,78);
                CandyText(CandyChoice("setting-minus-"+i),0,0,100,70,44);CandyText(CandyChoice("setting-plus-"+i),0,0,100,70,44);
                setupFills[i].rectTransform.anchoredPosition=new Vector2(-SettingFillWidth/2,-30);
            }
            CandyPlace("settings-prev",-650,-335,500,76);CandyPlace("settings-next",650,-335,500,76);
            CandyText(CandyChoice("settings-prev"),0,0,440,64,30);CandyText(CandyChoice("settings-next"),0,0,440,64,30);
        }
        void CandyStyleModeCards()
        {
            pvpCategory.rectTransform.anchoredPosition=new Vector2(-470,285);survivalCategory.rectTransform.anchoredPosition=new Vector2(470,285);
            pvpCategory.rectTransform.sizeDelta=survivalCategory.rectTransform.sizeDelta=new Vector2(860,44);pvpCategory.color=CandyPeach;survivalCategory.color=CandyMint;
            pvpCategory.fontSize=survivalCategory.fontSize=34;pvpCategory.resizeTextMaxSize=survivalCategory.resizeTextMaxSize=34;
            pvpCategory.fontStyle=survivalCategory.fontStyle=FontStyle.Bold;
            for(int i=0;i<8;i++)
            {
                int slot=i;var card=CandyPlace("mode-card-"+i,i<4?-470:470,200-(i%4)*138,860,126);
                card.selected=()=>ModeCardOption(slot)?.id==Details?.SelectedModeId&&ModeCardOption(slot)!=null;card.selectionGlyph=true;
                CandyText(card,40,28,620,48,32,TextAnchor.MiddleLeft);
                modePictures[i].rectTransform.anchoredPosition=new Vector2(-355,0);
                modePictureMissing[i].rectTransform.anchoredPosition=new Vector2(-355,0);modePictureMissing[i].rectTransform.sizeDelta=new Vector2(ModeCardPicture,ModeCardPicture);
                modeDescriptions[i].rectTransform.anchoredPosition=new Vector2(40,-24);modeDescriptions[i].rectTransform.sizeDelta=new Vector2(620,56);modeDescriptions[i].fontSize=24;modeDescriptions[i].resizeTextMaxSize=24;modeDescriptions[i].resizeTextMinSize=20;modeDescriptions[i].alignment=TextAnchor.MiddleLeft;modeDescriptions[i].color=CandyMuted;card.description=modeDescriptions[i];
                CandyChoiceIcon(card,LobbyTerminalGlyph.Symbol.Check,390,30,30);
            }
        }
        void AdjustCandyPopulation(int step)
        {if(Details==null||!lobby.CanConfigure)return;var setup=Details.Setup;setup.population=Mathf.Clamp(setup.population+step,1,100);Details.ConfigureSetup(setup);}
    }
}
