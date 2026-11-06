using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeadlyJenga.CandyArt.Marina
{
    // Requests still go through the real lobby. This is only its simplified front face.
    public sealed class MarinaScreen : MonoBehaviour
    {
        sealed class Target {public string id;public BoxCollider collider;public Image face;public Color color;public Action action;public Func<bool> allowed,visible;}
        readonly List<Target> targets=new();
        ILobbyControls lobby;ISessionInfo session;Action<string> detail;
        bool requireRelease=true,inputFocused=true;
        bool ModalBlocked=>!inputFocused||GameExperiencePanel.Showing(session)||CandyWildlifePanel.Showing(session)||CandyComfortPanel.Showing(session)||CharacterPicker.Showing(session);
        IGameExperienceControls readiness;
        bool VisitingMatch=>(lobby as ILobbyMatchVisitControls)?.VisitingLobby==true;
        bool JoinedLobby=>lobby.Connected&&!lobby.IsHost&&!VisitingMatch;
        RectTransform roomButton,rulesButton;
        Target readyTarget;
        RectTransform canvas;Text modeTitle,mapTitle,playTitle,readyTitle,partyTitle;
        RawImage modePicture,mapPicture;string modeId,mapId;float refreshAt;
        public bool Visible {get=>canvas&&canvas.gameObject.activeSelf;set{if(canvas)canvas.gameObject.SetActive(value);}}
        public void Build(ILobbyControls controls,Action<string> openDetail)
        {
            if(canvas)return;lobby=controls;session=Services.Session;detail=openDetail;
            readiness=lobby as IGameExperienceControls;
            var go=new GameObject("Lobby terminal",typeof(RectTransform),typeof(Canvas),typeof(Image));
            go.transform.SetParent(transform,false);
            canvas=(RectTransform)go.transform;
            canvas.sizeDelta=new Vector2(720,320);
            canvas.position=MayhemMarina.PlayScreen+Vector3.back*.11f;
            canvas.localScale=Vector3.one*.0098f;
            var surface=go.GetComponent<Image>();
            surface.sprite=CandyHudVisuals.Pill;
            surface.type=Image.Type.Sliced;
            surface.color=CandyHudVisuals.Ink;
            surface.raycastTarget=false;
            var c=go.GetComponent<Canvas>();
            c.renderMode=RenderMode.WorldSpace;
            c.overrideSorting=true;
            c.sortingOrder=70;

            Text("Lobby",new Vector2(-274,133),new Vector2(140,38),29,canvas);
            partyTitle=Text("",new Vector2(221,133),new Vector2(252,44),17,canvas);
            modePicture=Card("marina-modes",-176,"Mode",()=>detail?.Invoke("modes"),out modeTitle);
            mapPicture=Card("marina-maps",176,"Map",()=>detail?.Invoke("maps"),out mapTitle);

            roomButton=ActionButton("marina-room","Room",-280,()=>detail?.Invoke("room"),()=>true);
            ActionButton("marina-join","Join",-146,()=>detail?.Invoke("join"),()=>lobby.JoinEnabled,
                ()=>((lobby as ILobbyDetails)?.ConnectedCount??0)<=1);
            var ready=Button("marina-ready",new Vector2(-280,-121),new Vector2(124,52),CandyHudVisuals.ReadyBlue,
                ()=>readiness.SetReady(!readiness.LocalReady),()=>lobby.CanLeave&&!lobby.StartPending&&readiness.ReadyCount>0);
            readyTarget=targets[^1];
            readyTarget.visible=()=>readiness!=null&&JoinedLobby;
            readyTitle=Text("Ready",Vector2.zero,new Vector2(118,50),20,ready);
            rulesButton=ActionButton("marina-settings","Match Rules",122,()=>detail?.Invoke("settings"),()=>true);
            var play=Button("marina-play",new Vector2(269,-121),new Vector2(158,52),new Color(.11f,.42f,.32f),lobby.StartOrCancel,()=>lobby.CanStart);
            targets[^1].visible=()=>lobby.IsHost||VisitingMatch;
            playTitle=Text("Start match",Vector2.zero,new Vector2(150,50),21,play);
            Refresh();
        }

        RectTransform ActionButton(string id,string title,float x,Action action,Func<bool> allowed,Func<bool> visible=null)
        {
            var button=Button(id,new Vector2(x,-121),new Vector2(124,52),CandyHudVisuals.Card,action,allowed);
            targets[^1].visible=visible;
            Text(title,Vector2.zero,new Vector2(118,50),20,button);
            return button;
        }

        RawImage Card(string id,float x,string caption,Action action,out Text title)
        {
            var card=Button(id,new Vector2(x,15),new Vector2(334,208),CandyHudVisuals.Card,action,()=>lobby.CanConfigure&&GameExperiencePanel.CanEditRawChoices(session));
            Text(caption,new Vector2(0,83),new Vector2(310,28),19,card);
            title=Text("",new Vector2(0,-80),new Vector2(316,36),20,card);
            var go=new GameObject("Preview",typeof(RectTransform),typeof(RawImage));
            go.transform.SetParent(card,false);
            var image=go.GetComponent<RawImage>();
            image.rectTransform.sizeDelta=new Vector2(310,126);
            image.raycastTarget=false;
            return image;
        }
        RectTransform Button(string id,Vector2 p,Vector2 size,Color color,Action action,Func<bool> allowed)
        {
            var go=new GameObject(id,typeof(RectTransform),typeof(Image),typeof(BoxCollider));go.layer=5;go.transform.SetParent(canvas,false);var r=go.GetComponent<RectTransform>();r.anchoredPosition=p;r.sizeDelta=size;
            var image=go.GetComponent<Image>();image.sprite=CandyHudVisuals.Pill;image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=false;
            var hit=go.GetComponent<BoxCollider>();hit.isTrigger=true;hit.size=new Vector3(size.x,size.y,6);targets.Add(new Target{id=id,collider=hit,face=image,color=color,action=action,allowed=allowed});return r;
        }
        static Text Text(string value,Vector2 p,Vector2 size,int font,Transform parent)
        {
            var go=new GameObject("Marina label · "+value,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=value;t.fontSize=font;t.fontStyle=FontStyle.Bold;t.alignment=TextAnchor.MiddleCenter;t.color=new Color(1,.97f,.89f);t.raycastTarget=false;t.rectTransform.anchoredPosition=p;t.rectTransform.sizeDelta=size;return t;
        }
        void Refresh()
        {
            if(!canvas||lobby==null)return;
            modeTitle.text=lobby.ModeTitle;
            mapTitle.text=lobby.MapTitle;
            playTitle.text=VisitingMatch?"Return to match":lobby.StartPending?"Cancel start":"Start match";
            bool joined=JoinedLobby;
            roomButton.anchoredPosition=new Vector2(joined?280:-280,-121);
            rulesButton.anchoredPosition=new Vector2(joined?0:122,-121);
            bool ready=readiness?.LocalReady==true;
            readyTitle.text=ready?"Unready":"Ready";
            readyTarget.color=ready?CandyHudVisuals.Card:CandyHudVisuals.ReadyBlue;
            readyTitle.color=ready?Color.white:CandyHudVisuals.Ink;
            partyTitle.text=lobby.Busy?"Connecting…":lobby.Connected
                ?$"{lobby.HostCode}  ·  {readiness?.ReadyCount??0} of {readiness?.RequiredReadyCount??0} ready"
                :"Offline";
            var details=lobby as ILobbyDetails;
            string nextMode=details?.SelectedModeId??"mystery",nextMap=details?.SelectedMapId??"castle";
            if(modeId!=nextMode){modeId=nextMode;modePicture.texture=Resources.Load<Texture2D>("LobbyModePictures/"+modeId);FitPreview(modePicture);}
            if(mapId!=nextMap){mapId=nextMap;mapPicture.texture=MapPreviewImages.Get(mapId);FitPreview(mapPicture);}
        }
        static void FitPreview(RawImage image)
        {
            if(!image.texture)return;
            float scale=Mathf.Min(310f/image.texture.width,126f/image.texture.height);
            image.rectTransform.sizeDelta=new Vector2(image.texture.width*scale,image.texture.height*scale);
        }
        public bool Invoke(string id)
        {if(ModalBlocked||requireRelease)return false;if(id=="marina-host")id="marina-room";foreach(var target in targets)if(target.id==id&&Visible&&(target.visible?.Invoke()??true)&&target.allowed()){target.action();return true;}return false;}
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
                ray=input.Ray;
                held=aimed&&input.Held;
                pressed=aimed&&input.Pressed;
            }
            if(requireRelease){if(!held)requireRelease=false;pressed=false;}
            Target nearest=null;
            float distance=MenuPointer.LobbyRange;
            foreach(var target in targets)
            {
                bool visible=target.visible?.Invoke()??true;
                if(target.collider.gameObject.activeSelf!=visible)target.collider.gameObject.SetActive(visible);
                if(!visible)continue;
                target.collider.enabled=target.allowed();
                if(target.collider.enabled&&Vector3.Dot(ray.direction,target.collider.transform.forward)>0&&target.collider.Raycast(ray,out var hit,distance))
                {nearest=target;distance=hit.distance;}
            }
            foreach(var target in targets)
                target.face.color=!target.collider.enabled?Color.Lerp(target.color,CandyHudVisuals.Ink,.55f):
                    target==nearest?Color.Lerp(target.color,CandyHudVisuals.Mint,.22f):target.color;
            if(pressed&&nearest!=null)Invoke(nearest.id);
        }
        void OnEnable(){requireRelease=true;}

        void OnApplicationFocus(bool focused){inputFocused=focused;requireRelease=true;}
        void OnDestroy(){if(canvas)Destroy(canvas.gameObject);}
    }
}
