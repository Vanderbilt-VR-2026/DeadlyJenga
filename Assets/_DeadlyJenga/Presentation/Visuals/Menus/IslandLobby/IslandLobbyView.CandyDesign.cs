using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobbyView
    {
        static readonly Color CandyInk=new Color(.065f,.045f,.12f);
        static readonly Color CandyFace=new Color(.26f,.19f,.39f);
        static readonly Color CandyLavender=new Color(.55f,.39f,.76f);
        static readonly Color CandyMint=new Color(.25f,.96f,.76f);
        static readonly Color CandyPeach=new Color(1,.66f,.45f);
        static readonly Color CandyCream=new Color(1,.96f,.87f);
        static readonly Color CandyMuted=new Color(.79f,.73f,.87f);
        Texture2D candyAtlas;
        Sprite candyFaceSprite,candyRimSprite;
        RectTransform candyJoinContent,candyHostContent,candySetupContent;
        Text candySetupHeading,candyJoinStatus,candyPlayerValue,candyMapDescription,candyRoomStatus,candyJoinEmpty,candyJoinedNote;
        GameObject candyHostHelp,candyJoinCodeFrame,candyHostCodeFrame;
        readonly List<GameObject> candyMainDecor=new List<GameObject>();
        readonly List<Text> candyCodeLetters=new List<Text>();
        readonly List<RectTransform> candyCodeSlots=new List<RectTransform>();
        string candyLastCode;
        readonly Dictionary<string,string> candySummaries=new Dictionary<string,string>();
        readonly Dictionary<string,string> candyShortSummaries=new Dictionary<string,string>();

        void BuildCandyDesign()
        {
            BuildCandyAtlas();
            candyJoinContent=CandyContent(joinStation,CandyLavender,out var joinHeading);joinHeading.text="JOIN A GAME";
            candyHostContent=CandyContent(hostStation,CandyMint,out var hostHeading);hostHeading.text="HOST YOUR GAME";
            candySetupContent=CandyContent(setupStation,CandyPeach,out candySetupHeading);
            foreach(var choice in choices)
                if(!choice.physical&&(choice.button.transform.IsChildOf(joinStation)||choice.button.transform.IsChildOf(hostStation)||choice.button.transform.IsChildOf(setupStation)))
                    CandyStyle(choice);
            BuildCandyJoin();BuildCandyHost();BuildCandySetup();CoatCandyCabinets();
            foreach(var key in choices.Where(c=>c.physical))
            {
                key.keyColor=key.id=="keyboard-enter"?CandyMint:key.id=="backspace"||key.id=="clear-code"?CandyPeach:new Color(.18f,.12f,.28f);
                key.keyGlow.color=CandyMint;key.keyGlowSoft.color=new Color(CandyMint.r,CandyMint.g,CandyMint.b,.25f);
            }
            UpdateCandyDesign();
        }
        RectTransform CandyContent(RectTransform screen,Color accent,out Text heading)
        {
            var children=screen.Cast<Transform>().OfType<RectTransform>().ToArray();
            heading=children.Select(c=>c.GetComponent<Text>()).FirstOrDefault(t=>t);
            var content=new GameObject("Candy Arcade content",typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(screen,false);content.sizeDelta=new Vector2(1100,880);
            content.localScale=new Vector3(screen.rect.width/1100f,screen.rect.height/880f,1);
            foreach(var child in children)child.SetParent(content,false);
            screen.GetComponent<Image>().sprite=candyFaceSprite;screen.GetComponent<Image>().color=CandyInk;
            CandyRim(screen,new Color(.56f,.43f,.72f,.8f));
            if(heading)
            {
                heading.name="Terminal heading";heading.rectTransform.anchoredPosition=new Vector2(0,365);heading.rectTransform.sizeDelta=new Vector2(1020,80);
                heading.fontSize=64;heading.resizeTextMaxSize=64;heading.resizeTextMinSize=42;heading.fontStyle=FontStyle.Bold;heading.color=CandyCream;
                var shadow=heading.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.6f);shadow.effectDistance=new Vector2(0,-3);
            }
            var stripe=content.Find("Candy accent");
            if(stripe){stripe.gameObject.SetActive(true);var r=(RectTransform)stripe;r.anchoredPosition=new Vector2(0,424);r.sizeDelta=new Vector2(1000,7);stripe.GetComponent<Image>().color=accent;}
            return content;
        }
        Choice CandyChoice(string id)=>choices.FirstOrDefault(c=>c.id==id);
        Choice CandyPlace(string id,float x,float y,float width,float height)
        {
            var choice=CandyChoice(id);if(choice==null)return null;
            var rect=(RectTransform)choice.button.transform;rect.anchoredPosition3D=new Vector3(x,y,0);rect.sizeDelta=new Vector2(width,height);
            choice.collider.size=new Vector3(width,height,8);choice.collider.center=Vector3.zero;
            choice.label.rectTransform.anchoredPosition=Vector2.zero;choice.label.rectTransform.sizeDelta=new Vector2(width-44,height-12);
            choice.label.fontSize=height>=90?34:26;choice.label.resizeTextMaxSize=choice.label.fontSize;choice.label.resizeTextMinSize=20;
            choice.label.fontStyle=FontStyle.Bold;choice.label.alignment=TextAnchor.MiddleCenter;
            return choice;
        }
        void CandyStyle(Choice choice)
        {
            choice.candy=true;choice.faceColor=CandyFace;choice.edgeColor=new Color(CandyLavender.r,CandyLavender.g,CandyLavender.b,.8f);
            choice.button.transition=Selectable.Transition.None;choice.button.image.sprite=candyFaceSprite;choice.button.image.type=Image.Type.Sliced;
            choice.rim=CandyRim((RectTransform)choice.button.transform,new Color(CandyLavender.r,CandyLavender.g,CandyLavender.b,.7f));
            choice.rim.transform.SetAsFirstSibling();
            choice.label.color=CandyCream;choice.label.horizontalOverflow=HorizontalWrapMode.Wrap;choice.label.verticalOverflow=VerticalWrapMode.Truncate;
        }
        void CandyPrimary(Choice choice)
        {if(choice!=null){choice.faceColor=CandyMint;choice.edgeColor=CandyMint;choice.label.fontSize=36;choice.label.resizeTextMaxSize=36;}}
        void CandyText(Choice choice,float x,float y,float width,float height,int size,TextAnchor alignment=TextAnchor.MiddleCenter)
        {
            if(choice==null)return;
            var t=choice.label;if(t.rectTransform.anchoredPosition!=new Vector2(x,y))t.rectTransform.anchoredPosition=new Vector2(x,y);if(t.rectTransform.sizeDelta!=new Vector2(width,height))t.rectTransform.sizeDelta=new Vector2(width,height);
            t.fontSize=size;t.resizeTextMaxSize=size;t.resizeTextMinSize=Mathf.Min(size,22);t.alignment=alignment;
        }
        Text CandyReadout(RectTransform parent,string name,string value,float x,float y,float width,float height,int size=24)
        {
            var t=Label(parent,value,y,size,width,height);t.name=name;t.rectTransform.anchoredPosition=new Vector2(x,y);
            t.resizeTextMinSize=Mathf.Min(size,20);t.color=CandyCream;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;
            return t;
        }
        RectTransform CandyPanel(RectTransform parent,string name,float x,float y,float width,float height,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var r=(RectTransform)go.transform;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(width,height);
            var image=go.GetComponent<Image>();image.sprite=candyFaceSprite;image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=false;
            CandyRim(r,new Color(CandyLavender.r,CandyLavender.g,CandyLavender.b,.7f));return r;
        }
        Image CandyRim(RectTransform parent,Color color)
        {
            var go=new GameObject("Candy rim",typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>();image.sprite=candyRimSprite;image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=false;
            image.rectTransform.anchorMin=Vector2.zero;image.rectTransform.anchorMax=Vector2.one;image.rectTransform.offsetMin=Vector2.zero;image.rectTransform.offsetMax=Vector2.zero;return image;
        }
        LobbyTerminalGlyph CandyIcon(RectTransform parent,LobbyTerminalGlyph.Symbol symbol,float x,float y,float size,Color color)
        {
            var go=new GameObject(symbol+" icon",typeof(RectTransform),typeof(LobbyTerminalGlyph));go.transform.SetParent(parent,false);
            var glyph=go.GetComponent<LobbyTerminalGlyph>();glyph.symbol=symbol;glyph.color=color;glyph.raycastTarget=false;
            glyph.rectTransform.anchoredPosition=new Vector2(x,y);glyph.rectTransform.sizeDelta=new Vector2(size,size);return glyph;
        }
        void CandyChoiceIcon(Choice choice,LobbyTerminalGlyph.Symbol symbol,float x,float y=0,float size=34)
        {if(choice!=null)choice.glyph=CandyIcon((RectTransform)choice.button.transform,symbol,x,y,size,CandyCream);}
        Choice CandyAdd(RectTransform parent,string id,Func<string> title,float x,float y,float width,float height,Action action,Func<bool> enabled,Func<bool> visible)
        {var c=Add(parent,id,title,y,action,enabled,x,width,height,visible);CandyStyle(c);CandyPlace(id,x,y,width,height);return c;}

        void PaintCandyChoice(Choice choice,bool hover,bool usable)
        {
            if(choice.physical)
            {choice.label.color=usable?(choice.keyColor.grayscale>.6f?CandyInk:CandyCream):CandyMuted;return;}
            if(!choice.candy)
            {choice.label.color=hover?CandyHudVisuals.Ink:usable?Color.white:CandyHudVisuals.Muted;choice.button.image.color=hover?CandyHudVisuals.Mint:usable?CandyHudVisuals.Card:new Color(.11f,.14f,.2f);return;}
            bool selected=choice.selected!=null&&choice.selected();
            var face=!usable?new Color(.17f,.14f,.23f):hover?CandyMint:Time.unscaledTime<choice.pressedUntil?Color.Lerp(choice.faceColor,CandyInk,.2f):choice.faceColor;
            choice.button.image.color=face;
            var foreground=!usable?CandyMuted:CandyForeground(face);
            choice.label.color=foreground;
            if(choice.description)choice.description.color=CandyForeground(face)==CandyInk?new Color(.10f,.07f,.19f):CandyCream;
            if(choice.rim)choice.rim.color=selected||hover?CandyMint:usable?choice.edgeColor:new Color(CandyLavender.r,CandyLavender.g,CandyLavender.b,.4f);
            if(choice.glyph){choice.glyph.color=selected&&!hover?CandyMint:foreground;choice.glyph.gameObject.SetActive(!choice.selectionGlyph||selected);}
        }
        void UpdateCandyDesign()
        {
            if(!candyAtlas)return;
            setupFace.Opacity=1;
            bool main=setupPage=="main";
            candySetupHeading.text=setupPage=="modes"?"CHOOSE YOUR MODE":setupPage=="maps"?"CHOOSE YOUR MAP":setupPage=="settings"?"MODE SETTINGS":"MODE + MAP";
            foreach(var item in candyMainDecor)item.SetActive(main);
            candyJoinStatus.text=lobby.Busy?"CONNECTING":lobby.Connected?"IN ROOM":lobby.JoinEnabled?"READY TO JOIN":"HOSTING";
            var launch=CandyChoice("launch");launch.faceColor=lobby.StartPending?CandyPeach:CandyMint;launch.edgeColor=launch.faceColor;
            var symbol=lobby.StartPending?LobbyTerminalGlyph.Symbol.Back:LobbyTerminalGlyph.Symbol.Play;
            if(launch.glyph&&launch.glyph.symbol!=symbol){launch.glyph.symbol=symbol;launch.glyph.SetVerticesDirty();}
            CandyChoice("invite-all").faceColor=lobby.Connected?CandyMint:CandyFace;CandyChoice("invite-all").edgeColor=lobby.Connected?CandyMint:CandyLavender;
            candyHostHelp.SetActive(!lobby.Connected&&!lobby.Busy);
            candyHostCodeFrame.SetActive(lobby.Connected&&!lobby.Busy);
            candyJoinedNote.gameObject.SetActive(lobby.Connected&&(Details?.HostRequests.Count??0)==0);
            candyJoinEmpty.gameObject.SetActive(!lobby.Connected&&!lobby.Busy&&lobby.InvitationFrom==null&&(!lobby.ShowingNearby||!lobby.HasNearby(0)));
            candyJoinEmpty.text=lobby.ShowingNearby?"Searching your local network…\n"+lobby.JoinHint:"Have a room code? Enter it above.\nOr search for a nearby island.";
            joinHint.gameObject.SetActive(!lobby.ShowingNearby);
            candyPlayerValue.text=(Details?.Setup.population??MatchChoices.BotTarget).ToString();
            var map=mapOptions.FirstOrDefault(m=>m.id==Details?.SelectedMapId);candyMapDescription.text=string.IsNullOrEmpty(map?.description)?"Choose your arena.":CandyBrief(map.description);
            candyRoomStatus.text="ROOM\n"+Mathf.Max(1,Details?.ConnectedCount??1)+" / "+(Details?.Setup.population??MatchChoices.BotTarget);
            var back=CandyChoice("setup-back");((RectTransform)back.button.transform).anchoredPosition=new Vector2(0,setupPage=="modes"?-410:setupPage=="maps"?-407:-370);
            foreach(string id in new[]{"picker-previous","picker-next"}){var r=(RectTransform)CandyChoice(id).button.transform;r.anchoredPosition=new Vector2(r.anchoredPosition.x,setupPage=="modes"?-260:setupPage=="maps"?-344:-290);}
            if(setupPage=="maps")for(int i=0;i<PickerPageSize;i++)
            {
                var row=PickerRow(i);var choice=CandyChoice("picker-"+i);if(row==null)continue;
                bool header=row.map==null;float indent=header?(row.collection?0:12):24;
                choice.faceColor=header?row.collection?new Color(.37f,.26f,.53f):CandyFace:new Color(.19f,.14f,.29f);
                choice.label.fontStyle=header?FontStyle.Bold:FontStyle.Normal;
                CandyText(choice,indent,0,440-indent*2,44,22,TextAnchor.MiddleLeft);
            }
            if(candyLastCode!=(lobby.EnteredCode??""))
            {
                candyLastCode=lobby.EnteredCode??"";int count=Mathf.Clamp(Mathf.Max(6,candyLastCode.Length),6,12);float cell=Mathf.Min(98,650f/count);
                for(int i=0;i<candyCodeSlots.Count;i++)
                {
                    candyCodeSlots[i].gameObject.SetActive(i<count);candyCodeSlots[i].anchoredPosition=new Vector2((i-(count-1)*.5f)*cell,0);
                    candyCodeSlots[i].sizeDelta=new Vector2(cell-8,76);candyCodeLetters[i].rectTransform.sizeDelta=new Vector2(cell-14,66);
                    candyCodeLetters[i].text=i<candyLastCode.Length?candyLastCode[i].ToString():"";
                }
            }
        }
        void CoatCandyCabinets()
        {
            void Coat(Transform root,Func<string,Color> color)
            {
                if(!root)return;
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    if(renderer.GetComponent<TextMesh>()||renderer.name.StartsWith("Decorative ",StringComparison.Ordinal))continue;
                    var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);properties.SetColor("_BaseColor",color(renderer.name));renderer.SetPropertyBlock(properties);
                }
            }
            Coat(joinStation,n=>n.Contains("bezel")?new Color(.16f,.10f,.26f):CandyInk);
            Coat(transform.Find("Grounded red arcade cabinet"),n=>CandyInk);Coat(transform.Find("JOIN monitor support"),n=>CandyInk);
            Coat(piano,n=>n=="Glossy piano case"?new Color(.13f,.08f,.23f):CandyInk);
            foreach(var key in choices.Where(c=>c.physical))UpdatePhysicalKey(key,false);
            foreach(string name in new[]{"Host controller computer","Mode controller computer"})
                Coat(transform.Find(name),n=>n.Contains("wall")||n.Contains("panel")||n.Contains("Panel")?CandyInk:n.Contains("tick")?CandyPeach:n.Contains("button")?Color.white:new Color(.19f,.16f,.26f));
        }
        string CandyBrief(string value,bool compact=false)
        {
            if(string.IsNullOrWhiteSpace(value))return "";
            var cache=compact?candyShortSummaries:candySummaries;int limit=compact?56:80;
            if(cache.TryGetValue(value,out var summary))return summary;
            int sentence=value.IndexOf(". ",StringComparison.Ordinal);
            summary=sentence>=0?value.Substring(0,sentence+1):value;
            if(summary.Length>limit){int end=summary.LastIndexOf(' ',limit-4,limit-4);summary=summary.Substring(0,end>20?end:limit-4).TrimEnd()+ "…";}
            cache[value]=summary;return summary;
        }
        static Color CandyForeground(Color face)
        {
            float luminance=.2126f*Mathf.GammaToLinearSpace(face.r)+.7152f*Mathf.GammaToLinearSpace(face.g)+.0722f*Mathf.GammaToLinearSpace(face.b);
            return luminance>.22f?CandyInk:CandyCream;
        }
        static void CandyFillPicture(RawImage image,float width,float height)
        {
            if(image.rectTransform.sizeDelta!=new Vector2(width,height))image.rectTransform.sizeDelta=new Vector2(width,height);
            if(!image.texture)return;
            float source=image.texture.width/(float)image.texture.height,target=width/height;
            image.uvRect=source>target?new Rect((1-target/source)*.5f,0,target/source,1):new Rect(0,(1-source/target)*.5f,1,source/target);
        }
        void BuildCandyAtlas()
        {
            candyAtlas=new Texture2D(256,128,TextureFormat.RGBA32,false){name="Candy terminal shared atlas",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[256*128];
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                var q=new Vector2(Mathf.Abs(x-63.5f)-40,Mathf.Abs(y-65.5f)-40);
                float d=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-17;
                float alpha=Mathf.Clamp01(.5f-d);
                float shade=Mathf.Lerp(.80f,1,y/127f);
                if(d>-3)shade=y>64?1:.52f;
                if(y>93&&d<-4)shade=Mathf.Min(1,shade+.07f);
                pixels[y*256+x]=new Color(shade,shade,shade,alpha);
                pixels[y*256+x+128]=new Color(1,1,1,alpha*Mathf.Clamp01(d+2.1f));
            }
            candyAtlas.SetPixels32(pixels);candyAtlas.Apply(false,true);
            candyFaceSprite=Sprite.Create(candyAtlas,new Rect(0,0,128,128),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(28,28,28,28));
            candyRimSprite=Sprite.Create(candyAtlas,new Rect(128,0,128,128),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(28,28,28,28));
        }
        void DisposeCandyDesign()
        {if(candyFaceSprite)Destroy(candyFaceSprite);if(candyRimSprite)Destroy(candyRimSprite);if(candyAtlas)Destroy(candyAtlas);}
    }
}
