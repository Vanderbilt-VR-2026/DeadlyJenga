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
        void BuildCandySetup()
        {
            foreach(string id in new[]{"mode-previous","mode-next","map-previous","map-next"})CandyChoice(id).visible=()=>false;
            var mode=CandyPlace("mode-current",-255,130,480,250);var map=CandyPlace("map-current",255,130,480,250);
            mode.title=()=>lobby.ModeTitle;map.title=()=>lobby.MapTitle;mode.selected=()=>true;map.edgeColor=CandyPeach;
            CandyText(mode,-10,-48,390,56,28);CandyText(map,-10,-48,390,56,28);
            modePreview.rectTransform.SetParent(mode.button.transform,false);modePreview.rectTransform.anchoredPosition=new Vector2(0,55);
            mapPreview.rectTransform.SetParent(map.button.transform,false);mapPreview.rectTransform.anchoredPosition=new Vector2(0,55);
            modeMissing.rectTransform.SetParent(mode.button.transform,false);modeMissing.rectTransform.anchoredPosition=new Vector2(0,55);modeMissing.rectTransform.sizeDelta=new Vector2(440,128);
            previewMissing.rectTransform.SetParent(map.button.transform,false);previewMissing.rectTransform.anchoredPosition=new Vector2(0,55);previewMissing.rectTransform.sizeDelta=new Vector2(440,128);
            modeDescription.rectTransform.SetParent(mode.button.transform,false);modeDescription.rectTransform.anchoredPosition=new Vector2(0,-88);modeDescription.rectTransform.sizeDelta=new Vector2(430,52);
            modeDescription.fontSize=20;modeDescription.resizeTextMaxSize=20;modeDescription.resizeTextMinSize=20;modeDescription.color=CandyMuted;mode.description=modeDescription;
            candyMapDescription=CandyReadout((RectTransform)map.button.transform,"Map description","Choose your arena.",0,-88,430,52,20);candyMapDescription.color=CandyMuted;map.description=candyMapDescription;
            CandyChoiceIcon(mode,LobbyTerminalGlyph.Symbol.Chevron,207,-50,25);CandyChoiceIcon(map,LobbyTerminalGlyph.Symbol.Chevron,207,-50,25);
            modeCaption.rectTransform.anchoredPosition=new Vector2(-255,282);mapCaption.rectTransform.anchoredPosition=new Vector2(255,282);
            modeCaption.rectTransform.sizeDelta=mapCaption.rectTransform.sizeDelta=new Vector2(480,30);
            modeCaption.text="1  MODE";mapCaption.text="2  MAP";modeCaption.color=CandyMint;mapCaption.color=CandyPeach;
            var population=CandyPlace("player-target",0,-55,1000,62);population.title=()=>"PLAYERS";
            CandyText(population,-90,0,650,54,26,TextAnchor.MiddleLeft);CandyChoiceIcon(population,LobbyTerminalGlyph.Symbol.People,-440);
            var value=CandyPanel((RectTransform)population.button.transform,"Population value",130,0,220,52,CandyInk);
            candyPlayerValue=CandyReadout(value,"Player count","10",0,0,200,46,30);candyPlayerValue.fontStyle=FontStyle.Bold;
            var minus=CandyAdd(candySetupContent,"population-minus",()=>"-",340,-55,90,62,()=>AdjustCandyPopulation(-1),()=>lobby.CanConfigure&&Details!=null,()=>setupPage=="main");
            var plus=CandyAdd(candySetupContent,"population-plus",()=>"+",445,-55,90,62,()=>AdjustCandyPopulation(1),()=>lobby.CanConfigure&&Details!=null,()=>setupPage=="main");
            minus.faceColor=plus.faceColor=CandyPeach;minus.edgeColor=plus.edgeColor=CandyPeach;
            ((RectTransform)minus.button.transform).anchoredPosition3D=new Vector3(340,-55,-8);((RectTransform)plus.button.transform).anchoredPosition3D=new Vector3(445,-55,-8);
            var island=CandyPlace("island-effects",0,-130,1000,62);
            island.title=()=>Details?.Setup.rules.island==DeadlyJenga.Core.IslandRule.Walkable?"ISLAND EFFECTS    OFF / NORMAL MOVEMENT":"ISLAND EFFECTS    "+Details?.Setup.rules.island.ToString().ToUpperInvariant();
            CandyText(island,25,0,850,54,25);CandyChoiceIcon(island,LobbyTerminalGlyph.Symbol.Island,-440);
            var settings=CandyPlace("mode-settings",0,-205,1000,62);settings.title=()=>"MODE SETTINGS";settings.faceColor=CandyLavender;
            CandyChoiceIcon(settings,LobbyTerminalGlyph.Symbol.Gear,-440);CandyText(settings,25,0,850,54,28);
            CandyPlace("human-team",0,-270,1000,46);CandyText(CandyChoice("human-team"),0,0,950,40,22);
            var launch=CandyPlace("launch",-95,-350,800,86);CandyPrimary(launch);CandyText(launch,25,0,650,72,38);
            CandyChoiceIcon(launch,LobbyTerminalGlyph.Symbol.Play,-310,0,42);
            var room=CandyPanel(candySetupContent,"Room population",430,-350,170,86,CandyFace);candyMainDecor.Add(room.gameObject);
            candyRoomStatus=CandyReadout(room,"Room count","ROOM\n1 / 10",0,0,150,70,23);
            setupHint.rectTransform.anchoredPosition=new Vector2(0,-412);setupHint.rectTransform.sizeDelta=new Vector2(1000,34);setupHint.fontSize=20;setupHint.resizeTextMaxSize=20;setupHint.resizeTextMinSize=18;setupHint.color=CandyMuted;
            gameHint.rectTransform.anchoredPosition=new Vector2(0,312);gameHint.rectTransform.sizeDelta=new Vector2(1000,26);gameHint.fontSize=22;gameHint.resizeTextMaxSize=22;gameHint.resizeTextMinSize=20;gameHint.color=CandyMuted;
            // Breadcrumb Folder
            mapBreadcrumb = CandyReadout(candySetupContent, "Map breadcrumb", "MAP CATEGORIES", -260, 260, 440, 36, 24);
            mapBreadcrumb.color = new Color(.68f, .58f, .80f);
            mapBreadcrumb.fontStyle = FontStyle.Bold;
            mapBreadcrumb.alignment = TextAnchor.MiddleLeft;

            // Main Tab Buttons (Left Sidebar)
            for (int i = 0; i < 3; i++)
            {
                var tabChoice = CandyPlace("map-tab-" + i, -260, 155 - i * 110, 440, 95);
                CandyText(tabChoice, 0, 0, 400, 75, 28, TextAnchor.MiddleLeft);
                CandyChoiceIcon(tabChoice, LobbyTerminalGlyph.Symbol.Chevron, 185);
            }

            // Back button from sub-sections
            var backBtn = CandyPlace("map-back", -260, 225, 440, 48);
            CandyText(backBtn, 0, 0, 400, 42, 22, TextAnchor.MiddleLeft);
            CandyChoiceIcon(backBtn, LobbyTerminalGlyph.Symbol.Back, -185);

            // Archive Sub-Categories (5 slots)
            for (int i = 0; i < 5; i++)
            {
                var catChoice = CandyPlace("archive-cat-" + i, -260, 160 - i * 60, 440, 54);
                CandyText(catChoice, 0, 0, 400, 46, 22, TextAnchor.MiddleLeft);
                CandyChoiceIcon(catChoice, LobbyTerminalGlyph.Symbol.Chevron, 185);
            }
            var catUp = CandyPlace("cat-scroll-up", -260, 205, 440, 30);
            CandyText(catUp, 0, 0, 400, 26, 18);
            var catDown = CandyPlace("cat-scroll-down", -260, -145, 440, 30);
            CandyText(catDown, 0, 0, 400, 26, 18);

            // Map Items (4 rows)
            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                var c = CandyPlace("picker-" + i, -260, 155 - i * 78, 440, 72);
                c.selected = () => (stagedSelectedMapId ?? Details?.SelectedMapId) == MapItemOption(slot)?.id && MapItemOption(slot) != null;
                c.selectionGlyph = true;
                CandyText(c, 45, 0, 280, 58, 22, TextAnchor.MiddleLeft);
                mapCardPictures[i] = Picture((RectTransform)c.button.transform, "Map card thumbnail", -175, 0, 56, 56);
                mapCardPictures[i].enabled = false;
                CandyChoiceIcon(c, LobbyTerminalGlyph.Symbol.Check, 185);
            }
            // Dormant picker slots 4..9
            for (int i = 4; i < PickerPageSize; i++)
            {
                CandyPlace("picker-" + i, -2000, 0, 10, 10);
            }

            // Map list scroll buttons
            var mapUp = CandyPlace("map-scroll-up", -260, 205, 440, 30);
            CandyText(mapUp, 0, 0, 400, 24, 18);
            var mapDown = CandyPlace("map-scroll-down", -260, -165, 440, 30);
            CandyText(mapDown, 0, 0, 400, 24, 18);

            // Bottom Action Buttons
            var selectBtn = CandyPlace("map-select", -390, -235, 125, 64);
            CandyText(selectBtn, 0, 0, 115, 56, 22);
            var deselectBtn = CandyPlace("map-deselect", -260, -235, 115, 64);
            CandyText(deselectBtn, 0, 0, 105, 56, 20);
            var cancelBtn = CandyPlace("map-cancel", -130, -235, 125, 64);
            CandyText(cancelBtn, 0, 0, 115, 56, 22);

            // Right Side Preview Frame
            var rightFrame = CandyPanel(candySetupContent, "Large map frame", 240, 0, 520, 510, CandyFace);
            mapBrowserRightFrame = rightFrame;
            browserMapPreview = Picture(rightFrame, "Large map browser preview", 0, 55, 490, 320);
            browserMapName = CandyReadout(rightFrame, "Previewed map name", "", 0, -140, 490, 40, 30);
            browserMapName.fontStyle = FontStyle.Bold;
            browserMapName.color = CandyMint;
            browserMapCategory = CandyReadout(rightFrame, "Previewed map category", "", 0, -180, 490, 26, 20);
            browserMapCategory.fontStyle = FontStyle.Bold;
            browserMapCategory.color = CandyPeach;
            browserMapDescription = CandyReadout(rightFrame, "Previewed map description", "", 0, -220, 490, 44, 18);
            browserMapDescription.color = CandyCream;
            browserMapMissing = CandyReadout(rightFrame, "Map preview unavailable", "MAP PREVIEW COMING SOON", 0, 55, 460, 60, 26);
            browserMapMissing.color = CandyMuted;

            // Visual Bridge
            var bridgeGo = new GameObject("Map browser visual bridge", typeof(RectTransform), typeof(Image));
            bridgeGo.transform.SetParent(candySetupContent, false);
            browserBridge = bridgeGo.GetComponent<RectTransform>();
            browserBridge.sizeDelta = new Vector2(40, 48);
            browserBridgeImage = bridgeGo.GetComponent<Image>();
            browserBridgeImage.sprite = candyFaceSprite;
            browserBridgeImage.color = CandyMint;
            browserBridgeImage.raycastTarget = false;
            browserBridge.gameObject.SetActive(false);

            CandyPlace("picker-previous",-255,-345,490,58);CandyPlace("picker-next",255,-345,490,58);CandyPlace("setup-back",0,-410,700,58);
            CandyChoice("picker-previous").visible=()=> setupPage=="modes"&&PickerPages>1;
            CandyChoice("picker-next").visible=()=> setupPage=="modes"&&PickerPages>1;
            CandyChoiceIcon(CandyChoice("setup-back"),LobbyTerminalGlyph.Symbol.Back,-280);
            CandyStyleModeCards();
            for(int i=0;i<6;i++)
            {
                CandyPlace("setting-value-"+i,0,225-i*84,800,70);CandyText(CandyChoice("setting-value-"+i),0,0,760,62,26);
                CandyPlace("setting-minus-"+i,-470,225-i*84,80,70);CandyPlace("setting-plus-"+i,470,225-i*84,80,70);
                setupFills[i].rectTransform.anchoredPosition=new Vector2(-380,-26);
            }
            CandyPlace("settings-prev",-255,-290,490,58);CandyPlace("settings-next",255,-290,490,58);
        }
        void CandyStyleModeCards()
        {
            pvpCategory.rectTransform.anchoredPosition=new Vector2(-255,276);survivalCategory.rectTransform.anchoredPosition=new Vector2(255,276);
            pvpCategory.rectTransform.sizeDelta=survivalCategory.rectTransform.sizeDelta=new Vector2(480,38);pvpCategory.color=CandyPeach;survivalCategory.color=CandyMint;
            pvpCategory.fontStyle=survivalCategory.fontStyle=FontStyle.Bold;
            for(int i=0;i<8;i++)
            {
                int slot=i;var card=CandyPlace("mode-card-"+i,i<4?-255:255,190-(i%4)*144,480,132);
                card.selected=()=>ModeCardOption(slot)?.id==Details?.SelectedModeId&&ModeCardOption(slot)!=null;card.selectionGlyph=true;
                CandyText(card,58,30,270,44,24,TextAnchor.MiddleLeft);
                modePictures[i].rectTransform.anchoredPosition=new Vector2(-172,0);
                modePictureMissing[i].rectTransform.anchoredPosition=new Vector2(-172,0);modePictureMissing[i].rectTransform.sizeDelta=new Vector2(118,118);
                modeDescriptions[i].rectTransform.anchoredPosition=new Vector2(58,-24);modeDescriptions[i].rectTransform.sizeDelta=new Vector2(270,64);modeDescriptions[i].fontSize=20;modeDescriptions[i].resizeTextMaxSize=20;modeDescriptions[i].resizeTextMinSize=19;modeDescriptions[i].alignment=TextAnchor.MiddleLeft;modeDescriptions[i].color=CandyMuted;card.description=modeDescriptions[i];
                CandyChoiceIcon(card,LobbyTerminalGlyph.Symbol.Check,211,36,23);
            }
        }
        void AdjustCandyPopulation(int step)
        {if(Details==null||!lobby.CanConfigure)return;var setup=Details.Setup;setup.population=Mathf.Clamp(setup.population+step,1,100);Details.ConfigureSetup(setup);}
    }
}
