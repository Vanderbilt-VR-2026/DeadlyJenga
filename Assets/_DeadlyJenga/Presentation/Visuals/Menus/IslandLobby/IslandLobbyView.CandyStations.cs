using System;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobbyView
    {
        void BuildCandyJoin()
        {
            var caption=CandyReadout(candyJoinContent,"Room code caption","Room code",0,263,350,40,28);
            caption.fontStyle=FontStyle.Bold;
            var code=CandyPlace("code",0,167,940,140);
            code.faceColor=CandyFace;code.label.enabled=false;code.selected=()=>lobby.CodeFocused;
            for(int i=0;i<6;i++)
            {
                var slot=CandyPanel((RectTransform)code.button.transform,"Code character "+i,(i-2.5f)*136,0,128,108,new Color(.12f,.09f,.20f));
                var letter=CandyReadout(slot,"Character","",0,0,122,96,40);letter.fontStyle=FontStyle.Bold;candyCodeLetters.Add(letter);
            }
            var join=CandyPlace("join",0,25,540,100);
            CandyPrimary(join);join.title=()=>"Join room";
            var search=CandyPlace("detect-nearby",0,-76,370,60);
            CandyText(search,12,0,330,52,25);
            CandyChoiceIcon(search,LobbyTerminalGlyph.Symbol.Radar,-150,0,30);
            for(int i=0;i<3;i++)
            {
                var room=CandyPlace("nearby-room-"+i,0,-155-i*67,940,60);CandyText(room,-20,0,850,54,24);
                CandyChoiceIcon(room,LobbyTerminalGlyph.Symbol.Play,425,0,26);
            }
            CandyPlace("nearby-next",250,-345,470,44);CandyPlace("code-instead",-250,-345,470,44);
            var invitation=CandyPlace("accept-invite",0,-210,1000,76);CandyPrimary(invitation);CandyText(invitation,0,0,950,66,26);
            joinHint.rectTransform.anchoredPosition=new Vector2(0,-393);joinHint.rectTransform.sizeDelta=new Vector2(1000,56);
            joinHint.fontSize=20;joinHint.resizeTextMaxSize=20;joinHint.resizeTextMinSize=18;joinHint.color=CandyMuted;
            candyJoinEmpty=CandyReadout(candyJoinContent,"Discovery empty state","",0,-245,960,100,24);candyJoinEmpty.color=CandyMuted;
        }
        void BuildCandyHost()
        {
            var status=CandyPanel(candyHostContent,"Room status",0,294,420,44,CandyFace);status.SetAsFirstSibling();
            roomCount.rectTransform.anchoredPosition=new Vector2(0,294);roomCount.rectTransform.sizeDelta=new Vector2(400,38);
            roomCount.fontSize=23;roomCount.resizeTextMaxSize=23;roomCount.resizeTextMinSize=21;roomCount.color=CandyMint;
            var code=CandyPanel(candyHostContent,"Room code card",0,150,940,200,CandyFace);
            code.SetAsFirstSibling();
            CandyReadout(code,"Code label","Room code",0,57,350,38,28).color=CandyMuted;
            hostCode.rectTransform.anchoredPosition=new Vector2(0,126);hostCode.rectTransform.sizeDelta=new Vector2(850,104);
            hostCode.fontSize=82;hostCode.resizeTextMaxSize=82;hostCode.resizeTextMinSize=42;
            hostCode.fontStyle=FontStyle.Bold;hostCode.color=CandyCream;
            var region=CandyPlace("region",352,57,170,46);
            region.button.transform.SetParent(code,false);
            region.title=()=>lobby.Region.ToUpperInvariant();region.visible=()=>true;
            CandyChoiceIcon(region,LobbyTerminalGlyph.Symbol.Globe,-55,0,25);
            CandyText(region,14,0,125,40,23);
            var create=CandyPlace("host",0,-24,540,84);
            CandyPrimary(create);
            CandyPlace("invite-all",0,-24,540,84);
            CandyPlace("request-host",0,-115,540,60);
            CandyPlace("abandon-host",0,-200,400,64);
            requestLabel.rectTransform.anchoredPosition=new Vector2(-100,-270);requestLabel.rectTransform.sizeDelta=new Vector2(760,58);
            requestLabel.fontSize=24;requestLabel.resizeTextMaxSize=24;requestLabel.color=CandyCream;
            CandyPlace("request-next",420,-270,180,50);CandyPlace("request-accept",-255,-335,490,58);CandyPlace("request-deny",255,-335,490,58);
            CandyPrimary(CandyChoice("request-accept"));
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
            modeCaption.text="Mode";mapCaption.text="Map";modeCaption.color=CandyMint;mapCaption.color=CandyPeach;
            var settings=CandyPlace("mode-settings",0,-115,1000,62);settings.faceColor=CandyLavender;
            CandyChoiceIcon(settings,LobbyTerminalGlyph.Symbol.Gear,-440);CandyText(settings,25,0,850,54,28);
            CandyPlace("mystery-role",0,-200,1000,46);CandyText(CandyChoice("mystery-role"),0,0,950,40,22);
            var launch=CandyPlace("launch",-95,-350,800,86);CandyPrimary(launch);CandyText(launch,25,0,650,72,38);
            CandyChoiceIcon(launch,LobbyTerminalGlyph.Symbol.Play,-310,0,42);
            var room=CandyPanel(candySetupContent,"Room population",430,-350,170,86,CandyFace);candyMainDecor.Add(room.gameObject);
            candyRoomStatus=CandyReadout(room,"Room count","ROOM\n1 / 10",0,0,150,70,23);
            setupHint.rectTransform.anchoredPosition=new Vector2(0,-412);setupHint.rectTransform.sizeDelta=new Vector2(1000,34);setupHint.fontSize=20;setupHint.resizeTextMaxSize=20;setupHint.resizeTextMinSize=18;setupHint.color=CandyMuted;
            gameHint.rectTransform.anchoredPosition=new Vector2(0,312);gameHint.rectTransform.sizeDelta=new Vector2(1000,26);gameHint.fontSize=22;gameHint.resizeTextMaxSize=22;gameHint.resizeTextMinSize=20;gameHint.color=CandyMuted;
            for(int i=0;i<MapPageSize;i++)
            {
                int slot=i;
                var card=CandyPlace("picker-"+i,(i%MapColumns-2)*204,145-i/MapColumns*228,194,214);
                card.selected=()=>MapCard(slot)?.id==Details?.SelectedMapId&&MapCard(slot)!=null;
                CandyText(card,0,-72,172,60,23);
                var rect=(RectTransform)card.button.transform;
                mapCardPictures[i]=Picture(rect,"Map thumbnail",0,28,172,138);
                mapCardPlaceholders[i]=CandyReadout(rect,"Map preview unavailable","Preview unavailable",0,28,168,104,20);
                mapCardPlaceholders[i].color=CandyMuted;
            }
            CandyPlace("picker-previous",-255,-290,270,50);CandyPlace("picker-next",255,-290,270,50);
            CandyPlace("experimental-maps",0,-365,270,46);
            CandyPlace("setup-back",-412,365,192,58);
            CandyPlace("open-room",-412,365,192,58);
            CandyPlace("open-join",412,365,192,58);
            CandyChoiceIcon(CandyChoice("setup-back"),LobbyTerminalGlyph.Symbol.Back,-64,0,22);
            CandyText(CandyChoice("setup-back"),12,0,154,48,25);
            CandyStyleModeCards();
            var confirm=CandyAdd(candySetupContent,"mode-confirm",()=>"Choose map",0,-350,1000,50,()=>OpenPicker("maps"),()=>lobby.CanConfigure,()=>setupPage=="modes");CandyPrimary(confirm);CandyChoiceIcon(confirm,LobbyTerminalGlyph.Symbol.Play,-440);
            BuildSettingRows();
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
    }
}
