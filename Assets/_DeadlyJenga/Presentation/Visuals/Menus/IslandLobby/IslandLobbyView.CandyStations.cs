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
            for(int i=0;i<12;i++)
            {
                var slot=CandyPanel((RectTransform)code.button.transform,"Code character "+i,0,0,88,76,new Color(.12f,.09f,.20f));
                candyCodeSlots.Add(slot);var letter=CandyReadout(slot,"Character","",0,0,78,66,40);letter.fontStyle=FontStyle.Bold;candyCodeLetters.Add(letter);
            }
            var join=CandyPlace("join",380,185,250,96);CandyPrimary(join);join.title=()=>"JOIN ROOM";CandyText(join,0,0,226,80,30);
            CandyReadout(candyJoinContent,"Typing help","Type below or use your keyboard.",0,103,950,28,21).color=CandyMuted;
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
            joinHint.rectTransform.anchoredPosition=new Vector2(0,-393);joinHint.rectTransform.sizeDelta=new Vector2(1000,56);
            joinHint.fontSize=20;joinHint.resizeTextMaxSize=20;joinHint.resizeTextMinSize=18;joinHint.color=CandyMuted;
            candyJoinEmpty=CandyReadout(candyJoinContent,"Discovery empty state","",0,-245,960,100,24);candyJoinEmpty.color=CandyMuted;
        }
        void BuildCandyHost()
        {
            var status=CandyPanel(candyHostContent,"Room status",0,294,420,44,CandyFace);status.SetAsFirstSibling();
            roomCount.rectTransform.anchoredPosition=new Vector2(0,294);roomCount.rectTransform.sizeDelta=new Vector2(400,38);
            roomCount.fontSize=23;roomCount.resizeTextMaxSize=23;roomCount.resizeTextMinSize=21;roomCount.color=CandyMint;
            var create=CandyPlace("host",0,190,1000,110);CandyPrimary(create);create.title=()=>"CREATE ROOM";CandyText(create,25,17,820,55,40);
            create.description=CandyReadout((RectTransform)create.button.transform,"Create room subtitle","Get a code. Bring your friends.",25,-30,820,30,24);
            CandyChoiceIcon(create,LobbyTerminalGlyph.Symbol.Plus,-430,0,47);
            var code=CandyPanel(candyHostContent,"Connected room code",0,190,1000,140,CandyFace);code.SetAsFirstSibling();candyHostCodeFrame=code.gameObject;
            CandyReadout(code,"Code label","JOIN CODE",0,44,900,30,22).color=CandyMuted;
            hostCode.rectTransform.anchoredPosition=new Vector2(0,170);hostCode.rectTransform.sizeDelta=new Vector2(940,66);hostCode.fontSize=49;hostCode.resizeTextMaxSize=49;hostCode.resizeTextMinSize=32;hostCode.fontStyle=FontStyle.Bold;hostCode.color=CandyCream;
            var region=CandyPlace("region",0,81,1000,62);region.title=()=>"REGION:  "+lobby.Region.ToUpperInvariant()+(lobby.CanHost?"     CHANGE":"");
            region.visible=()=>true;CandyChoiceIcon(region,LobbyTerminalGlyph.Symbol.Globe,-440,0,34);CandyText(region,25,0,850,54,26);
            var invite=CandyPlace("invite-all",0,-12,1000,80);CandyChoiceIcon(invite,LobbyTerminalGlyph.Symbol.People,-440,0,40);CandyText(invite,25,0,850,68,30);
            CandyPlace("request-host",0,-110,1000,60);CandyPlace("shared-controls",0,-110,1000,60);
            CandyPlace("abandon-host",0,-190,1000,60);
            requestLabel.rectTransform.anchoredPosition=new Vector2(-100,-270);requestLabel.rectTransform.sizeDelta=new Vector2(760,58);
            requestLabel.fontSize=24;requestLabel.resizeTextMaxSize=24;requestLabel.color=CandyCream;
            CandyPlace("request-next",420,-270,180,50);CandyPlace("request-accept",-255,-335,490,58);CandyPlace("request-deny",255,-335,490,58);
            CandyPrimary(CandyChoice("request-accept"));
            candyJoinedNote=CandyReadout(candyHostContent,"Connected instructions","Share the code above.\nChoose your mode and map at the next terminal.",0,-295,980,100,24);candyJoinedNote.color=CandyMuted;
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
            for(int i=0;i<PickerPageSize;i++)
            {
                int slot=i;var c=CandyPlace("picker-"+i,i<5?-255:255,-75-i%5*52,480,48);
                c.selected=()=>PickerOption(slot)?.id==Details?.SelectedMapId&&PickerOption(slot)!=null;
                c.title=()=>PickerTitle(slot).Replace("[current]","").Trim();
                CandyText(c,0,0,440,44,22,TextAnchor.MiddleLeft);
            }
            browserMapPreview=Picture(candySetupContent,"Large map browser preview",0,140,940,290);
            browserMapName=CandyReadout(candySetupContent,"Previewed map name","",0,-25,960,30,25);
            browserMapName.fontStyle=FontStyle.Bold;browserMapName.color=CandyMint;
            browserMapMissing=CandyReadout(candySetupContent,"Map preview unavailable","MAP PREVIEW COMING SOON",0,140,900,60,28);browserMapMissing.color=CandyMuted;
            CandyPlace("picker-previous",-255,-290,490,58);CandyPlace("picker-next",255,-290,490,58);CandyPlace("setup-back",0,-370,700,58);
            CandyChoice("picker-previous").visible=()=> (setupPage=="maps"||setupPage=="modes")&&PickerPages>1;
            CandyChoice("picker-next").visible=()=> (setupPage=="maps"||setupPage=="modes")&&PickerPages>1;
            CandyChoiceIcon(CandyChoice("setup-back"),LobbyTerminalGlyph.Symbol.Back,-280);
            CandyStyleModeCards();
            var confirm=CandyAdd(candySetupContent,"mode-confirm",()=>"USE THIS MODE",0,-350,1000,50,()=>OpenPicker("maps"),()=>lobby.CanConfigure,()=>setupPage=="modes");CandyPrimary(confirm);CandyChoiceIcon(confirm,LobbyTerminalGlyph.Symbol.Play,-440);
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
