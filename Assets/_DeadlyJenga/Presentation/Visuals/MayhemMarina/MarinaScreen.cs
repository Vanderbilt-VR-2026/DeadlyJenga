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
        sealed class Target {public string id;public BoxCollider collider;public Image face;public Color color;public Action action;public Func<bool> allowed;public Text[] labels;public Outline outline;public Shadow glow;}
        readonly List<Target> targets=new();
        ILobbyControls lobby;ISessionInfo session;Action<string> detail;
        bool requireRelease=true,inputFocused=true;
        bool ModalBlocked=>MenuAim.InputConsumed||!inputFocused||GameExperiencePanel.Showing(session)||CandyWildlifePanel.Showing(session)||CandyComfortPanel.Showing(session)||CharacterPicker.Showing(session);
        static readonly Color Plum=new(.12f,.055f,.19f);
        static readonly Color Cream=new(1,.96f,.83f);
        static readonly Color Coral=new(.88f,.25f,.33f);
        static readonly Color Lilac=new(.67f,.36f,.87f);
        static readonly Color Mint=new(.26f,.84f,.64f);
        InputAction select,leftSelect;
        Image aimDot;
        RectTransform canvas,joinFace,hostFace;
        Text modeTitle,mapTitle,playTitle,hostTitle,focusDescription;
        RawImage modePicture,mapPicture;
        string modeId,mapId,focusTarget,focusMode,focusMap;
        float refreshAt;
        RectTransform focusPanel;
        public bool Visible {get=>canvas&&canvas.gameObject.activeSelf;set{if(canvas)canvas.gameObject.SetActive(value);}}
        public void Build(ILobbyControls controls,Action<string> openDetail)
        {
            if(canvas)return;
            lobby=controls;session=Services.Session;detail=openDetail;
            var go=new GameObject("Marina · live candy arcade",typeof(RectTransform),typeof(Canvas));
            go.transform.SetParent(transform,false);
            canvas=go.GetComponent<RectTransform>();canvas.sizeDelta=new Vector2(720,320);
            canvas.localPosition=MayhemMarina.PlayScreen+Vector3.back*.14f;canvas.localScale=Vector3.one*.0095f;
            var worldCanvas=go.GetComponent<Canvas>();worldCanvas.renderMode=RenderMode.WorldSpace;
            worldCanvas.overrideSorting=true;worldCanvas.sortingOrder=70;

            var title=Text("PICK YOUR MATCH",new Vector2(0,119),new Vector2(635,54),46,canvas);
            var depth=title.gameObject.AddComponent<Shadow>();depth.effectDistance=new Vector2(0,-5);depth.effectColor=Coral;
            Ornament(new Vector2(-286,118),Lilac,-12);
            Ornament(new Vector2(286,118),Mint,12);

            modePicture=Card("marina-modes","GAME MODE",new Vector2(-169,1),()=>detail?.Invoke("modes"),out modeTitle);
            mapPicture=Card("marina-maps","MAP",new Vector2(169,1),()=>detail?.Invoke("maps"),out mapTitle);
            var play=Button("marina-play",new Vector2(0,-124),new Vector2(378,48),Coral,lobby.StartOrCancel,()=>lobby.CanStart);
            playTitle=Text("Start match",Vector2.zero,new Vector2(355,47),29,play);
            SmallButton("marina-settings","Settings",new Vector2(-263,-124),()=>detail?.Invoke("settings"));
            SmallButton("marina-style","Style",new Vector2(263,-124),()=>detail?.Invoke("style"));
            var round=Button("marina-experience",new Vector2(0,-151),new Vector2(190,18),Plum,
                ()=>GameExperiencePanel.Open(session),()=>GameExperiencePanel.Resolve(session)!=null);
            Text("Round info",Vector2.zero,new Vector2(180,18),13,round);

            joinFace=CabinetFace("Lavender Join display",-4.87f);
            hostFace=CabinetFace("Mint Host display",4.87f);
            CabinetHeading(joinFace,"JOIN FRIENDS",Lilac);
            CabinetHeading(hostFace,"HOST ROOM",Mint);
            CabinetAction(joinFace,"marina-join","Room code",17,Lilac,()=>detail?.Invoke("join-code"));
            CabinetAction(joinFace,"marina-nearby","Find nearby",-27,new Color(.34f,.17f,.45f),()=>detail?.Invoke("join-nearby"));
            hostTitle=CabinetAction(hostFace,"marina-host","Create room",17,Mint,()=>detail?.Invoke("host"));
            CabinetAction(hostFace,"marina-invite","Invite nearby",-27,new Color(.29f,.18f,.41f),()=>detail?.Invoke("host-invite"));

            focusPanel=new GameObject("Marina · selection preview",typeof(RectTransform)).GetComponent<RectTransform>();
            focusPanel.SetParent(canvas,false);focusPanel.anchoredPosition=new Vector2(0,-94);
            focusDescription=Text("",Vector2.zero,new Vector2(660,18),13,focusPanel);
            focusPanel.gameObject.SetActive(false);
            foreach(var target in targets)target.labels=target.face.GetComponentsInChildren<Text>(true);
            select=new InputAction("Marina select",InputActionType.Button,"<XRController>{RightHand}/trigger");select.Enable();
            leftSelect=new InputAction("Marina left select",InputActionType.Button,"<XRController>{LeftHand}/trigger");leftSelect.Enable();
            aimDot=MenuAim.CreateReticle(canvas);Refresh();
        }
        static Image Panel(string name,RectTransform parent,Vector2 position,Vector2 size,Color color)
        {
            var panel=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            panel.transform.SetParent(parent,false);panel.rectTransform.anchoredPosition=position;panel.rectTransform.sizeDelta=size;
            panel.sprite=CandyHudVisuals.Pill;panel.type=Image.Type.Sliced;panel.color=color;panel.raycastTarget=false;
            return panel;
        }
        void Ornament(Vector2 position,Color color,float angle)
        {
            var ornament=Panel("Marina · candy accent",canvas,position,new Vector2(29,24),color);
            ornament.rectTransform.localRotation=Quaternion.Euler(0,0,angle);
        }
        void SmallButton(string id,string title,Vector2 position,Action action)
        {
            var button=Button(id,position,new Vector2(122,34),new Color(.32f,.17f,.40f),action,()=>true);
            Text(title,Vector2.zero,new Vector2(113,32),17,button);
        }
        RectTransform CabinetFace(string name,float worldX)
        {
            var face=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            face.SetParent(canvas,false);
            face.sizeDelta=new Vector2(175,157);
            face.localPosition=new Vector3(worldX/.0095f,8f,-50f);
            return face;
        }
        static void CabinetHeading(RectTransform parent,string title,Color trim)
        {
            Panel("Marina · cabinet marquee",parent,new Vector2(0,59),new Vector2(167,34),trim);
            var label=Text(title,new Vector2(0,59),new Vector2(165,34),21,parent);
            label.color=Plum;
        }
        Text CabinetAction(RectTransform parent,string id,string title,float y,Color color,Action action)
        {
            var button=Button(id,new Vector2(0,y),new Vector2(154,36),color,action,()=>true,parent);
            return Text(title,Vector2.zero,new Vector2(147,36),18,button);
        }
        RawImage Card(string id,string section,Vector2 position,Action action,out Text title)
        {
            var button=Button(id,position,new Vector2(321,170),Lilac,action,()=>GameExperiencePanel.CanEditRawChoices(session));
            Panel("Marina · illustration panel",button,Vector2.zero,new Vector2(313,162),Plum);
            var crop=Panel("Marina · illustration crop",button,new Vector2(0,9),new Vector2(309,137),Color.white);
            crop.gameObject.AddComponent<Mask>().showMaskGraphic=false;
            var go=new GameObject("Live destination image",typeof(RectTransform),typeof(RawImage));
            go.transform.SetParent(crop.transform,false);var image=go.GetComponent<RawImage>();
            image.rectTransform.sizeDelta=crop.rectTransform.sizeDelta;image.raycastTarget=false;
            Panel("Marina · category badge",button,new Vector2(-91,62),new Vector2(117,23),Plum);
            Text(section,new Vector2(-91,62),new Vector2(113,23),13,button);
            title=Text("",new Vector2(0,-70),new Vector2(299,29),22,button);
            title.resizeTextForBestFit=true;title.resizeTextMinSize=14;title.resizeTextMaxSize=22;
            return image;
        }
        RectTransform Button(string id,Vector2 p,Vector2 size,Color color,Action action,Func<bool> allowed,RectTransform parent=null)
        {
            var go=new GameObject(id,typeof(RectTransform),typeof(Image),typeof(BoxCollider));go.layer=5;go.transform.SetParent(parent?parent:canvas,false);var r=go.GetComponent<RectTransform>();r.anchoredPosition=p;r.sizeDelta=size;
            var image=go.GetComponent<Image>();image.sprite=CandyHudVisuals.Pill;image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=false;
            var hit=go.GetComponent<BoxCollider>();hit.isTrigger=true;hit.size=new Vector3(size.x,size.y,6);var outline=go.AddComponent<Outline>();outline.effectDistance=new Vector2(4,-4);outline.effectColor=Color.clear;
            var glow=go.AddComponent<Shadow>();glow.effectDistance=new Vector2(0,-8);glow.effectColor=Color.clear;
            targets.Add(new Target{id=id,collider=hit,face=image,color=color,action=action,allowed=allowed,outline=outline,glow=glow});return r;
        }
        static Text Text(string value,Vector2 p,Vector2 size,int font,Transform parent)
        {
            var go=new GameObject("Marina label · "+value,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=value;t.fontSize=font;t.fontStyle=FontStyle.Bold;t.alignment=TextAnchor.MiddleCenter;t.color=new Color(1,.97f,.89f);t.raycastTarget=false;t.rectTransform.anchoredPosition=p;t.rectTransform.sizeDelta=size;return t;
        }
        void Refresh()
        {
            if(!canvas||lobby==null)return;
            modeTitle.text=lobby.ModeTitle;mapTitle.text=lobby.MapTitle;
            var experience=GameExperiencePanel.Resolve(session);
            bool rotating=experience?.Experience==GameExperience.Game&&experience.AutomaticRounds&&!lobby.CanStart;
            playTitle.text=rotating&&experience.GamePhase==RoomGamePhase.Selected?
                "Next match in "+Mathf.CeilToInt(experience.CountdownSeconds)+" s":lobby.StartPending?"Cancel start":"Start match";
            hostTitle.text=lobby.HostActive?"Manage room":"Create room";
            var details=lobby as ILobbyDetails;
            string nextMode=details?.SelectedModeId??"mystery",nextMap=details?.SelectedMapId??"castle";
            if(modeId!=nextMode)
            {
                modeId=nextMode;
                modePicture.texture=Resources.Load<Texture2D>("LobbyModePictures/"+modeId)??Resources.Load<Texture2D>("LobbyModePictures/demo");
                FitPreview(modePicture);
            }
            if(mapId!=nextMap){mapId=nextMap;mapPicture.texture=MapPreviewImages.Get(mapId);FitPreview(mapPicture);}
        }
        static void FitPreview(RawImage image)
        {
            if(!image.texture)return;
            float source=(float)image.texture.width/image.texture.height;
            float target=image.rectTransform.sizeDelta.x/image.rectTransform.sizeDelta.y;
            float width=Mathf.Min(1,target/source),height=Mathf.Min(1,source/target);
            image.uvRect=new Rect((1-width)*.5f,(1-height)*.5f,width,height);
        }
        public bool Invoke(string id)
        {if(ModalBlocked||requireRelease)return false;foreach(var target in targets)if(target.id==id&&Visible&&target.allowed()){MenuAim.Consume();target.action();return true;}return false;}
        void Update()
        {
            if(!Visible||ModalBlocked){requireRelease=true;if(aimDot)aimDot.gameObject.SetActive(false);if(focusPanel)focusPanel.gameObject.SetActive(false);return;}if(Time.unscaledTime>=refreshAt){refreshAt=Time.unscaledTime+.2f;Refresh();}
            var player=session?.Player;if(!NetworkActors.Alive(player)||!player.ViewCamera)return;
            var mouse=Mouse.current;var keyboard=Keyboard.current;
            bool held=select?.IsPressed()==true||leftSelect?.IsPressed()==true||player.SelectHeld||keyboard?.eKey.isPressed==true||mouse?.leftButton.isPressed==true;
            if(requireRelease){if(!held)requireRelease=false;return;}
            bool pressed=select!=null&&select.WasPressedThisFrame()||leftSelect?.WasPressedThisFrame()==true||player.SelectPressed||keyboard!=null&&keyboard.eKey.wasPressedThisFrame||mouse!=null&&mouse.leftButton.wasPressedThisFrame;
            var camera=player.ViewCamera;
            Ray ray=player.IsVR?MenuAim.Resolve(camera,camera.transform.parent,player.gameObject.GetComponent<IBlasterControls>()?.LeftHanded==true,leftSelect.WasPressedThisFrame(),select.WasPressedThisFrame(),out _,out _):session.MenuOpen&&mouse!=null?camera.ScreenPointToRay(mouse.position.ReadValue()):new Ray(camera.transform.position,camera.transform.forward);
            Target nearest=null;float distance=11;
            foreach(var target in targets)
            {
                target.collider.enabled=target.allowed();
                if(target.collider.enabled&&MenuAim.TryPoint(ray,target.face.rectTransform,distance,out _,out var hit)){nearest=target;distance=Vector3.Distance(ray.origin,hit);}
            }
            foreach(var target in targets)
            {
                bool hover=target==nearest,allowed=target.allowed();target.face.color=hover?Color.Lerp(target.color,Cream,.35f):allowed?target.color:Color.Lerp(target.color,Plum,.6f);
                target.face.rectTransform.localScale=Vector3.Lerp(target.face.rectTransform.localScale,Vector3.one*(hover?1.035f:1),1-Mathf.Exp(-22*Time.unscaledDeltaTime));
                target.outline.effectColor=new Color(CandyHudVisuals.Mint.r,CandyHudVisuals.Mint.g,CandyHudVisuals.Mint.b,hover?1:0);
                target.glow.effectColor=new Color(CandyHudVisuals.Mint.r,CandyHudVisuals.Mint.g,CandyHudVisuals.Mint.b,hover?.5f:0);
                foreach(var text in target.labels)text.color=allowed?(target.color.grayscale>.6f?Plum:Cream):CandyHudVisuals.Muted;
            }
            PreviewDestination(nearest?.id);
            if(MenuAim.TryPoint(ray,nearest!=null?nearest.face.rectTransform:canvas,11,out _,out var point)||
                MenuAim.TryPoint(ray,joinFace,11,out _,out point)||MenuAim.TryPoint(ray,hostFace,11,out _,out point))MenuAim.Present(aimDot,point,true,nearest!=null);else aimDot.gameObject.SetActive(false);
            if(pressed&&nearest!=null){Invoke(nearest.id);requireRelease=true;}
        }
        void PreviewDestination(string id)
        {
            bool map=id=="marina-maps",mode=id=="marina-modes";
            focusPanel.gameObject.SetActive(map||mode);if(!map&&!mode)return;
            if(focusTarget==id&&focusMode==modeId&&focusMap==mapId)return;
            focusTarget=id;focusMode=modeId;focusMap=mapId;
            var details=lobby as ILobbyDetails;string selected=map?mapId:modeId;
            var options=map?details?.Maps:details?.Modes;string description="";
            if(options!=null)foreach(var option in options)if(option.id==selected){description=option.description;break;}
            if(string.IsNullOrWhiteSpace(description))description=map?"Browse maps. Point to a map for its picture and description; select to use it.":"Browse modes. Point to a mode to preview it; select to use it and choose a map.";
            focusDescription.text=description.Length>150?description.Substring(0,147)+"...":description;
        }
        void OnEnable(){requireRelease=true;select?.Enable();leftSelect?.Enable();}
        void OnApplicationFocus(bool focused){inputFocused=focused;requireRelease=true;}
        void OnDisable(){select?.Disable();leftSelect?.Disable();if(aimDot)aimDot.gameObject.SetActive(false);}
        void OnDestroy(){select?.Dispose();leftSelect?.Dispose();if(canvas)Destroy(canvas.gameObject);}
    }
}
