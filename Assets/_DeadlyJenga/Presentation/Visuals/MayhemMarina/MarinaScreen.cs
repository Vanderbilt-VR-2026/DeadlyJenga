using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyAudio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeadlyJenga.CandyArt.Marina
{
    // The lobby's front door. Requests still go through the real lobby; deeper pages open the existing terminals.
    // Solo: Play Now / Join a friend / Your party, with mode, map and practice underneath.
    // In a party: the mode and map cards, Start (host) or Ready (guest), and Leave.
    public sealed class MarinaScreen : MonoBehaviour
    {
        static readonly Color Ink=new Color(.075f,.05f,.14f),Face=new Color(.27f,.19f,.40f),Lavender=new Color(.56f,.42f,.80f),
            Mint=new Color(.30f,.95f,.74f),Peach=new Color(1,.66f,.45f),Cream=new Color(1,.96f,.88f),Muted=new Color(.80f,.74f,.90f);

        sealed class Target
        {
            public string id;public RectTransform root,face;public BoxCollider collider;public Image faceImage,lip;public Color color;
            public Action action;public Func<bool> allowed,visible;public float pressedUntil;public Text label,sub;public LobbyTerminalGlyph glyph;
        }
        readonly List<Target> targets=new();
        ILobbyControls lobby;ISessionInfo session;Action<string> detail;IGameExperienceControls readiness;IQuickPlay quick;
        bool requireRelease=true,inputFocused=true;
        bool ModalBlocked=>!inputFocused||GameExperiencePanel.Showing(session)||CandyWildlifePanel.Showing(session)||CandyComfortPanel.Showing(session)||CharacterPicker.Showing(session);
        bool VisitingMatch=>(lobby as ILobbyMatchVisitControls)?.VisitingLobby==true;
        int People=>(lobby as ILobbyDetails)?.ConnectedCount??1;
        bool Party=>VisitingMatch||lobby.Connected&&(People>1||quick?.PublicRoom==true);
        bool Guest=>lobby.Connected&&!lobby.IsHost&&!VisitingMatch;

        RectTransform canvas,soloGroup,partyGroup,busyGroup;
        Text heading,status,hint,busyText,modeTitle,mapTitle,soloMode,soloMap;
        RawImage modePicture,mapPicture;string modeId,mapId;float refreshAt;
        Target play,ready;
        public bool Visible {get=>canvas&&canvas.gameObject.activeSelf;set{if(canvas)canvas.gameObject.SetActive(value);}}

        public void Build(ILobbyControls controls,Action<string> openDetail)
        {
            if(canvas)return;lobby=controls;session=Services.Session;detail=openDetail;
            readiness=lobby as IGameExperienceControls;quick=lobby as IQuickPlay;
            var go=new GameObject("Lobby terminal",typeof(RectTransform),typeof(Canvas),typeof(Image));
            go.transform.SetParent(transform,false);
            canvas=(RectTransform)go.transform;
            canvas.sizeDelta=new Vector2(720,320);
            canvas.position=MayhemMarina.PlayScreen+Vector3.back*.11f;
            canvas.localScale=Vector3.one*.0098f;
            var surface=go.GetComponent<Image>();surface.sprite=CandyHudVisuals.Pill;surface.type=Image.Type.Sliced;surface.color=Ink;surface.raycastTarget=false;
            var c=go.GetComponent<Canvas>();c.renderMode=RenderMode.WorldSpace;c.overrideSorting=true;c.sortingOrder=70;
            MenuAppear.Attach(go);

            heading=Label(canvas,"Deadly Jenga",new Vector2(-160,138),new Vector2(380,34),28,Cream,true,TextAnchor.MiddleLeft);
            var pill=Panel(canvas,"Status pill",new Vector2(212,134),new Vector2(276,34),Face);
            status=Label(pill,"",Vector2.zero,new Vector2(262,30),15,Mint,false);

            // Solo front door
            soloGroup=Group("Solo");
            var quickPlay=Button(soloGroup,"marina-quickplay",new Vector2(-190,8),new Vector2(300,176),Mint,()=>quick?.QuickPlay(),()=>quick?.CanQuickPlay==true);
            quickPlay.label=Label(quickPlay.face,"PLAY NOW",new Vector2(0,10),new Vector2(280,58),46,Ink,true);
            quickPlay.sub=Label(quickPlay.face,"Jump into a public game",new Vector2(0,-38),new Vector2(270,28),17,Ink,false);
            quickPlay.glyph=Glyph(quickPlay.face,LobbyTerminalGlyph.Symbol.Play,new Vector2(0,60),30,Ink);
            var join=Button(soloGroup,"marina-join",new Vector2(165,57),new Vector2(360,78),Face,()=>detail?.Invoke("join"),()=>lobby.JoinEnabled);
            join.glyph=Glyph(join.face,LobbyTerminalGlyph.Symbol.People,new Vector2(-148,0),34,Mint);
            join.label=Label(join.face,"Join a friend",new Vector2(26,12),new Vector2(280,34),26,Cream,true,TextAnchor.MiddleLeft);
            join.sub=Label(join.face,"Enter their party code",new Vector2(26,-16),new Vector2(280,24),15,Muted,false,TextAnchor.MiddleLeft);
            var room=Button(soloGroup,"marina-room",new Vector2(165,-33),new Vector2(360,78),Face,()=>detail?.Invoke("room"),()=>true);
            room.glyph=Glyph(room.face,LobbyTerminalGlyph.Symbol.Island,new Vector2(-148,0),34,Peach);
            room.label=Label(room.face,"Your party",new Vector2(26,12),new Vector2(280,34),26,Cream,true,TextAnchor.MiddleLeft);
            room.sub=Label(room.face,"",new Vector2(26,-16),new Vector2(280,24),15,Muted,false,TextAnchor.MiddleLeft);
            var modes=Chip(soloGroup,"marina-modes",-265,170,()=>detail?.Invoke("modes"),()=>lobby.CanConfigure&&GameExperiencePanel.CanEditRawChoices(session));soloMode=modes.label;
            var maps=Chip(soloGroup,"marina-maps",-85,170,()=>detail?.Invoke("maps"),()=>lobby.CanConfigure&&GameExperiencePanel.CanEditRawChoices(session));soloMap=maps.label;
            var rules=Chip(soloGroup,"marina-settings",70,120,()=>detail?.Invoke("settings"),()=>true);rules.label.text="Rules";
            rules.glyph=Glyph(rules.face,LobbyTerminalGlyph.Symbol.Gear,new Vector2(-38,0),20,Cream);rules.label.rectTransform.anchoredPosition=new Vector2(12,0);
            play=Chip(soloGroup,"marina-play",245,210,lobby.StartOrCancel,()=>lobby.CanStart);
            play.color=Peach;play.glyph=Glyph(play.face,LobbyTerminalGlyph.Symbol.Play,new Vector2(-80,0),20,Ink);play.label.rectTransform.anchoredPosition=new Vector2(12,0);

            // Party view
            partyGroup=Group("Party");
            modePicture=Card(partyGroup,"marina-modes",-176,"Mode",()=>detail?.Invoke("modes"),out modeTitle);
            mapPicture=Card(partyGroup,"marina-maps",176,"Map",()=>detail?.Invoke("maps"),out mapTitle);
            var invite=Chip(partyGroup,"marina-room",-280,124,()=>detail?.Invoke("room"),()=>true,-121);invite.label.text="Invite";
            var leave=Chip(partyGroup,"marina-leave",-146,124,()=>lobby.Leave(),()=>lobby.CanLeave&&!lobby.StartPending,-121);leave.label.text="Leave";
            leave.visible=()=>!VisitingMatch;
            var partyRules=Chip(partyGroup,"marina-settings",0,124,()=>detail?.Invoke("settings"),()=>true,-121);partyRules.label.text="Rules";
            ready=Chip(partyGroup,"marina-ready",250,196,()=>readiness.SetReady(!readiness.LocalReady),()=>lobby.CanLeave&&!lobby.StartPending&&readiness.ReadyCount>0,-121);
            ready.visible=()=>readiness!=null&&Guest;
            var start=Chip(partyGroup,"marina-play",250,196,lobby.StartOrCancel,()=>lobby.CanStart,-121);
            start.visible=()=>!Guest;start.color=Mint;start.glyph=Glyph(start.face,LobbyTerminalGlyph.Symbol.Play,new Vector2(-70,0),20,Ink);start.label.rectTransform.anchoredPosition=new Vector2(12,0);
            partyGroup.gameObject.SetActive(false);

            // Connecting overlay
            busyGroup=Group("Connecting");
            var busy=Panel(busyGroup,"Connecting card",new Vector2(0,-5),new Vector2(560,200),Face);
            busyText=Label(busy,"Connecting…",new Vector2(0,18),new Vector2(520,60),34,Cream,true);
            Label(busy,"Hang tight, this takes a few seconds",new Vector2(0,-36),new Vector2(520,28),17,Muted,false);
            busyGroup.gameObject.SetActive(false);

            hint=Label(canvas,"",new Vector2(-160,111),new Vector2(380,18),13,Muted,false,TextAnchor.MiddleLeft);
            Refresh();
        }

        RectTransform Group(string name)
        {var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(canvas,false);var r=(RectTransform)go.transform;r.sizeDelta=canvas.sizeDelta;return r;}
        static RectTransform Panel(RectTransform parent,string name,Vector2 p,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchoredPosition=p;r.sizeDelta=size;
            var image=go.GetComponent<Image>();image.sprite=CandyHudVisuals.Pill;image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=false;return r;
        }
        // A chunky candy button: a darker lip underneath and a face that dips when pressed.
        Target Button(RectTransform parent,string id,Vector2 p,Vector2 size,Color color,Action action,Func<bool> allowed)
        {
            var go=new GameObject(id,typeof(RectTransform),typeof(BoxCollider));go.layer=5;go.transform.SetParent(parent,false);
            var r=(RectTransform)go.transform;r.anchoredPosition=p;r.sizeDelta=size;
            var lip=Panel(r,"Lip",new Vector2(0,-5),size,Color.black).GetComponent<Image>();
            var face=Panel(r,"Face",Vector2.zero,size,color);
            var hit=go.GetComponent<BoxCollider>();hit.isTrigger=true;hit.size=new Vector3(size.x,size.y+6,6);
            var t=new Target{id=id,root=r,face=face,collider=hit,faceImage=face.GetComponent<Image>(),lip=lip,color=color,action=action,allowed=allowed};
            targets.Add(t);return t;
        }
        Target Chip(RectTransform parent,string id,float x,float width,Action action,Func<bool> allowed,float y=-122)
        {
            var t=Button(parent,id,new Vector2(x,y),new Vector2(width,48),Face,action,allowed);
            t.label=Label(t.face,"",Vector2.zero,new Vector2(width-20,40),18,Cream,true);return t;
        }
        RawImage Card(RectTransform parent,string id,float x,string caption,Action action,out Text title)
        {
            var card=Button(parent,id,new Vector2(x,7),new Vector2(334,170),Face,action,()=>lobby.CanConfigure&&GameExperiencePanel.CanEditRawChoices(session));
            Label(card.face,caption,new Vector2(0,68),new Vector2(310,24),16,Muted,false);
            title=Label(card.face,"",new Vector2(0,-66),new Vector2(316,30),20,Cream,true);
            var go=new GameObject("Preview",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(card.face,false);
            var image=go.GetComponent<RawImage>();image.rectTransform.sizeDelta=new Vector2(310,96);image.rectTransform.anchoredPosition=new Vector2(0,2);image.raycastTarget=false;
            return image;
        }
        static Text Label(Transform parent,string value,Vector2 p,Vector2 size,int font,Color color,bool title,TextAnchor anchor=TextAnchor.MiddleCenter)
        {
            var go=new GameObject("Label · "+value,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var t=go.GetComponent<Text>();
            t.font=title?CandyHudVisuals.TitleFont:CandyHudVisuals.TextFont;t.text=value;t.fontSize=font;t.alignment=anchor;t.color=color;t.raycastTarget=false;
            t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.resizeTextForBestFit=true;t.resizeTextMinSize=Mathf.Min(font,14);t.resizeTextMaxSize=font;
            t.rectTransform.anchoredPosition=p;t.rectTransform.sizeDelta=size;return t;
        }
        static LobbyTerminalGlyph Glyph(RectTransform parent,LobbyTerminalGlyph.Symbol symbol,Vector2 p,float size,Color color)
        {
            var go=new GameObject(symbol+" icon",typeof(RectTransform),typeof(CanvasRenderer),typeof(LobbyTerminalGlyph));go.transform.SetParent(parent,false);
            var g=go.GetComponent<LobbyTerminalGlyph>();g.symbol=symbol;g.color=color;g.raycastTarget=false;g.rectTransform.anchoredPosition=p;g.rectTransform.sizeDelta=new Vector2(size,size);return g;
        }

        void Refresh()
        {
            if(!canvas||lobby==null)return;
            bool busy=lobby.Busy,party=Party;
            busyGroup.gameObject.SetActive(busy);
            soloGroup.gameObject.SetActive(!busy&&!party);
            partyGroup.gameObject.SetActive(!busy&&party);
            busyText.text="Connecting…";
            int count=People;string code=lobby.HostCode;
            heading.text=!party?"Deadly Jenga":VisitingMatch?"Match in progress":quick?.PublicRoom==true?"Public game":Guest?"Friend's party":"Your party";
            status.text=busy?"Connecting…":!lobby.Connected?"Offline":party?$"{count} player{(count==1?"":"s")}  ·  {readiness?.ReadyCount??0}/{readiness?.RequiredReadyCount??0} ready":"Solo  ·  "+code;
            var room=targets.Find(t=>t.id=="marina-room"&&t.root.IsChildOf(soloGroup));
            if(room!=null)room.sub.text=lobby.Connected&&!string.IsNullOrEmpty(code)?"Code  "+code+"  ·  friends join you":"Start a private party";
            soloMode.text="Mode: "+lobby.ModeTitle;soloMap.text="Map: "+lobby.MapTitle;
            play.label.text=VisitingMatch?"Return":lobby.StartPending?"Cancel":"Play solo";
            modeTitle.text=lobby.ModeTitle;mapTitle.text=lobby.MapTitle;
            bool isReady=readiness?.LocalReady==true;
            ready.label.text=isReady?"Not ready":"I'm ready";ready.color=isReady?Face:Mint;
            var start=targets.Find(t=>t.id=="marina-play"&&t.root.IsChildOf(partyGroup));
            if(start!=null)start.label.text=VisitingMatch?"Back to match":lobby.StartPending?"Cancel start":"Start match";
            var details=lobby as ILobbyDetails;
            string nextMode=details?.SelectedModeId??"survival",nextMap=details?.SelectedMapId??"castle";
            if(modeId!=nextMode){modeId=nextMode;modePicture.texture=Resources.Load<Texture2D>("LobbyModePictures/"+modeId);FitPreview(modePicture);}
            if(mapId!=nextMap){mapId=nextMap;mapPicture.texture=MapPreviewImages.Get(mapId);FitPreview(mapPicture);}
            bool vr=session?.Player?.IsVR==true;
            hint.text=busy?"":party&&lobby.Connected?"Party code  "+code+(Guest?"  ·  the host picks mode and map":"  ·  tap a card to change it"):vr?"Point and pull the trigger to choose":"Look at a button, then click or press E";
        }
        static void FitPreview(RawImage image)
        {
            if(!image.texture){image.enabled=false;return;}
            image.enabled=true;float scale=Mathf.Min(310f/image.texture.width,96f/image.texture.height);
            image.rectTransform.sizeDelta=new Vector2(image.texture.width*scale,image.texture.height*scale);
        }
        bool Shown(Target t)=>t.root.parent.gameObject.activeSelf&&(t.visible?.Invoke()??true);
        public bool Invoke(string id)
        {
            if(ModalBlocked||requireRelease)return false;if(id=="marina-host")id="marina-room";
            Refresh();
            foreach(var target in targets)
                if(target.id==id&&Visible&&Shown(target)&&target.allowed()){Press(target);return true;}
            return false;
        }
        void Press(Target target)
        {
            target.pressedUntil=Time.unscaledTime+.14f;
            CandyAudioControls.PlayUiClick(target.root.position);CandyFeedback.Pulse(.35f,.05f);
            target.action();
        }
        Target hovered;
        void Update()
        {
            if(!Visible||ModalBlocked){requireRelease=true;return;}if(Time.unscaledTime>=refreshAt){refreshAt=Time.unscaledTime+.2f;Refresh();}
            var player=session?.Player;if(!NetworkActors.Alive(player)||!player.ViewCamera)return;
            var mouse=Mouse.current;var keyboard=Keyboard.current;
            var camera=player.ViewCamera;
            bool held=mouse?.leftButton.isPressed==true||keyboard?.eKey.isPressed==true;
            bool pressed=mouse?.leftButton.wasPressedThisFrame==true||keyboard?.eKey.wasPressedThisFrame==true;
            Ray ray=session.MenuOpen&&mouse!=null?camera.ScreenPointToRay(mouse.position.ReadValue()):new Ray(camera.transform.position,camera.transform.forward);
            if(player.IsVR)
            {
                bool aimed=MenuPointer.AimAt(camera,player.gameObject.GetComponent<IBlasterControls>()?.LeftHanded==true,
                    candidate=>MenuPointer.PanelHit(candidate,canvas,MenuPointer.LobbyRange),out var input);
                ray=input.Ray;held=aimed&&input.Held;pressed=aimed&&input.Pressed;
            }
            if(requireRelease){if(!held)requireRelease=false;pressed=false;}
            Target nearest=null;float distance=MenuPointer.LobbyRange;
            foreach(var target in targets)
            {
                bool visible=Shown(target);
                if(target.root.gameObject.activeSelf!=visible)target.root.gameObject.SetActive(visible);
                if(!visible)continue;
                target.collider.enabled=target.allowed();
                if(target.collider.enabled&&Vector3.Dot(ray.direction,target.collider.transform.forward)>0&&target.collider.Raycast(ray,out var hit,distance))
                {nearest=target;distance=hit.distance;}
            }
            if(nearest!=hovered){hovered=nearest;if(nearest!=null&&player.IsVR)CandyFeedback.Pulse(.12f,.03f);}
            foreach(var target in targets)
            {
                if(!target.root.gameObject.activeSelf)continue;
                bool usable=target.collider.enabled,hover=target==nearest,down=Time.unscaledTime<target.pressedUntil||hover&&held;
                var color=!usable?Color.Lerp(target.color,Ink,.6f):hover?Color.Lerp(target.color,Color.white,.18f):target.color;
                target.faceImage.color=color;target.lip.color=Color.Lerp(color,Color.black,.45f);
                target.face.anchoredPosition=new Vector2(0,down?-4:hover?2:0);
                float scale=hover&&usable?1.03f:1;target.face.localScale=new Vector3(scale,scale,1);
                var ink=Luminance(color)>.22f?Ink:Cream;
                if(target.label)target.label.color=usable?ink:Muted;
                if(target.sub)target.sub.color=usable?(ink==Ink?new Color(.12f,.08f,.2f,.85f):Muted):Muted;
                if(target.glyph)target.glyph.color=usable?(ink==Ink?Ink:target.glyph.color==Ink?Cream:target.glyph.color):Muted;
            }
            if(pressed&&nearest!=null)Press(nearest);
        }
        static float Luminance(Color c)=>.2126f*Mathf.GammaToLinearSpace(c.r)+.7152f*Mathf.GammaToLinearSpace(c.g)+.0722f*Mathf.GammaToLinearSpace(c.b);
        void OnEnable(){requireRelease=true;}
        void OnApplicationFocus(bool focused){inputFocused=focused;requireRelease=true;}
        void OnDestroy(){if(canvas)Destroy(canvas.gameObject);}
    }
}
