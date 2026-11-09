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
        InputAction select,leftSelect;Image aimDot;RectTransform canvas;Text modeTitle,mapTitle,playTitle,partyTitle,gameExperienceTitle,demoExperienceTitle,browseTitle;
        RawImage modePicture,mapPicture,otherPicture;string modeId,mapId,focusTarget,focusMode,focusMap;float refreshAt;Text focusDescription;RectTransform focusPanel;
        public bool Visible {get=>canvas&&canvas.gameObject.activeSelf;set{if(canvas)canvas.gameObject.SetActive(value);}}
        public void Build(ILobbyControls controls,Action<string> openDetail)
        {
            if(canvas)return;lobby=controls;session=Services.Session;detail=openDetail;
            var go=new GameObject("Marina · live play screen",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(transform,false);canvas=go.GetComponent<RectTransform>();canvas.sizeDelta=new Vector2(720,320);canvas.localPosition=MayhemMarina.PlayScreen+Vector3.back*.11f;canvas.localScale=Vector3.one*.0098f;
            var c=go.GetComponent<Canvas>();c.renderMode=RenderMode.WorldSpace;c.overrideSorting=true;c.sortingOrder=70;
            Text("MODE",new Vector2(-236,135),new Vector2(225,40),30,canvas);Text("MAP",new Vector2(236,135),new Vector2(220,40),30,canvas);
            modePicture=Card("marina-modes",new Vector2(-236,30),()=>detail?.Invoke("modes"));
            otherPicture=Card("marina-more-modes",new Vector2(0,30),()=>detail?.Invoke("modes"));
            mapPicture=Card("marina-maps",new Vector2(236,30),()=>detail?.Invoke("maps"));
            modeTitle=Text("",new Vector2(-236,-68),new Vector2(225,27),17,canvas);
            browseTitle=Text("BROWSE MODES",new Vector2(0,-68),new Vector2(225,27),17,canvas);
            mapTitle=Text("",new Vector2(236,-68),new Vector2(225,27),17,canvas);
            AttachCardTitle(modeTitle,modePicture);AttachCardTitle(browseTitle,otherPicture);AttachCardTitle(mapTitle,mapPicture);
            var play=Button("marina-play",new Vector2(0,-111),new Vector2(330,55),new Color(.15f,.62f,.14f),()=>lobby.StartOrCancel(),()=>lobby.CanStart);
            playTitle=Text("PLAY",Vector2.zero,new Vector2(300,53),42,play);
            string[] ids={"join","host","settings","style"};string[] captions={"JOIN","HOST","SETTINGS","STYLE"};
            for(int i=0;i<4;i++){string id=ids[i];var b=Button("marina-"+id,new Vector2(-270+i*180,-153),new Vector2(165,25),new Color(.16f,.36f,.34f),()=>detail?.Invoke(id),()=>true);Text(captions[i],Vector2.zero,new Vector2(160,24),16,b);}
            partyTitle=Text("",new Vector2(0,92),new Vector2(200,20),15,canvas);partyTitle.color=new Color(.86f,.98f,.88f);
            var experience=GameExperiencePanel.Resolve(session);
            if(experience!=null)
            {
                var game=Button("marina-experience-game",new Vector2(-62,135),new Vector2(110,32),new Color(.16f,.36f,.34f),()=>GameExperiencePanel.Resolve(session)?.SetExperience(GameExperience.Game),()=>GameExperiencePanel.CanChooseExperience(GameExperiencePanel.Resolve(session)));
                gameExperienceTitle=Text("GAME",Vector2.zero,new Vector2(106,30),22,game);
                var demo=Button("marina-experience-demo",new Vector2(62,135),new Vector2(110,32),new Color(.16f,.36f,.34f),()=>GameExperiencePanel.Resolve(session)?.SetExperience(GameExperience.Demo),()=>GameExperiencePanel.CanChooseExperience(GameExperiencePanel.Resolve(session)));
                demoExperienceTitle=Text("DEMO",Vector2.zero,new Vector2(106,30),22,demo);
                var round=Button("marina-experience",new Vector2(-270,-111),new Vector2(165,55),new Color(.16f,.36f,.34f),()=>GameExperiencePanel.Open(session),()=>GameExperiencePanel.Resolve(session)!=null);
                Text("ROUND INFO",Vector2.zero,new Vector2(156,52),24,round);
                partyTitle.rectTransform.anchoredPosition=new Vector2(270,-111);partyTitle.rectTransform.sizeDelta=new Vector2(165,55);partyTitle.fontSize=23;
            }
            var description=new GameObject("Focused destination description",typeof(RectTransform),typeof(Image));description.transform.SetParent(canvas,false);
            focusPanel=description.GetComponent<RectTransform>();focusPanel.anchoredPosition=new Vector2(0,-23);focusPanel.sizeDelta=new Vector2(700,44);
            var backing=description.GetComponent<Image>();backing.sprite=CandyHudVisuals.Pill;backing.type=Image.Type.Sliced;backing.color=new Color(.06f,.11f,.17f,.94f);backing.raycastTarget=false;
            focusDescription=Text("",Vector2.zero,new Vector2(676,40),17,focusPanel);focusDescription.fontStyle=FontStyle.Normal;focusPanel.gameObject.SetActive(false);
            foreach(var target in targets)target.labels=target.face.GetComponentsInChildren<Text>(true);
            select=new InputAction("Marina select",InputActionType.Button,"<XRController>{RightHand}/trigger");select.Enable();leftSelect=new InputAction("Marina left select",InputActionType.Button,"<XRController>{LeftHand}/trigger");leftSelect.Enable();aimDot=MenuAim.CreateReticle(canvas);Refresh();
        }
        static void AttachCardTitle(Text title,RawImage picture)
        {
            title.transform.SetParent(picture.transform.parent,false);title.rectTransform.anchoredPosition=new Vector2(0,-90);title.rectTransform.sizeDelta=new Vector2(220,24);
        }
        RawImage Card(string id,Vector2 p,Action action)
        {
            var r=Button(id,p,new Vector2(220,156),new Color(1,.95f,.84f),action,()=>GameExperiencePanel.CanEditRawChoices(session));
            var go=new GameObject("Live destination image",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(r,false);var image=go.GetComponent<RawImage>();image.rectTransform.sizeDelta=new Vector2(211,147);image.raycastTarget=false;return image;
        }
        RectTransform Button(string id,Vector2 p,Vector2 size,Color color,Action action,Func<bool> allowed)
        {
            var go=new GameObject(id,typeof(RectTransform),typeof(Image),typeof(BoxCollider));go.layer=5;go.transform.SetParent(canvas,false);var r=go.GetComponent<RectTransform>();r.anchoredPosition=p;r.sizeDelta=size;
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
            if(!canvas||lobby==null)return;modeTitle.text=lobby.ModeTitle.ToUpperInvariant();mapTitle.text=lobby.MapTitle.ToUpperInvariant();playTitle.text=lobby.StartPending?"CANCEL":"PLAY";
            var details=lobby as ILobbyDetails;partyTitle.text=details==null?"":details.ConnectedCount+" PLAYER"+(details.ConnectedCount==1?"":"S");
            var experience=GameExperiencePanel.Resolve(session);
            if(experience!=null&&gameExperienceTitle&&demoExperienceTitle)
            {
                gameExperienceTitle.text=experience.Experience==GameExperience.Game?"GAME [X]":"GAME";
                demoExperienceTitle.text=experience.Experience==GameExperience.Demo?"DEMO [X]":"DEMO";
                gameExperienceTitle.color=demoExperienceTitle.color=GameExperiencePanel.CanChooseExperience(experience)?Color.white:CandyHudVisuals.Muted;
                partyTitle.text=experience.GamePhase+"\n"+experience.ReadyCount+" / "+experience.RequiredReadyCount+" ready";
                browseTitle.text=experience.Experience==GameExperience.Game?"CURATED PROFILE":"BROWSE MODES";
            }
            string nextMode=details?.SelectedModeId??"mystery",nextMap=details?.SelectedMapId??"castle";
            if(modeId!=nextMode){modeId=nextMode;modePicture.texture=Resources.Load<Texture2D>("LobbyModePictures/"+modeId);otherPicture.texture=Resources.Load<Texture2D>("LobbyModePictures/"+(modeId=="ctf"?"disasters":"ctf"));FitPreview(modePicture);FitPreview(otherPicture);}
            if(mapId!=nextMap){mapId=nextMap;mapPicture.texture=MapPreviewImages.Get(mapId);FitPreview(mapPicture);}
        }
        static void FitPreview(RawImage image)
        {
            if(!image.texture)return;
            float scale=Mathf.Min(211f/image.texture.width,147f/image.texture.height);
            image.rectTransform.sizeDelta=new Vector2(image.texture.width*scale,image.texture.height*scale);
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
            if (Physics.Raycast(ray, out var obstruction, distance, 1 << Layers.Surface, QueryTriggerInteraction.Ignore))
                distance = obstruction.distance;
            float screenRange = distance;
            foreach(var target in targets)
            {
                target.collider.enabled=target.allowed();
                if(target.collider.enabled&&MenuAim.TryPoint(ray,target.face.rectTransform,distance,out _,out var hit)){nearest=target;distance=Vector3.Distance(ray.origin,hit);}
            }
            foreach(var target in targets)
            {
                bool hover=target==nearest,allowed=target.allowed();target.face.color=hover?CandyHudVisuals.Mint:allowed?target.color:CandyHudVisuals.Card;
                target.outline.effectColor=new Color(CandyHudVisuals.Mint.r,CandyHudVisuals.Mint.g,CandyHudVisuals.Mint.b,hover?1:0);
                target.glow.effectColor=new Color(CandyHudVisuals.Mint.r,CandyHudVisuals.Mint.g,CandyHudVisuals.Mint.b,hover?.5f:0);
                foreach(var text in target.labels)text.color=hover?CandyHudVisuals.Ink:allowed?Color.white:CandyHudVisuals.Muted;
            }
            PreviewDestination(nearest?.id);
            if(MenuAim.TryPoint(ray,canvas,screenRange,out _,out var point))MenuAim.Present(aimDot,point,true,nearest!=null);else aimDot.gameObject.SetActive(false);
            if(pressed&&nearest!=null){Invoke(nearest.id);requireRelease=true;}
        }
        void PreviewDestination(string id)
        {
            bool map=id=="marina-maps",mode=id=="marina-modes"||id=="marina-more-modes";
            focusPanel.gameObject.SetActive(map||mode);if(!map&&!mode)return;
            if(focusTarget==id&&focusMode==modeId&&focusMap==mapId)return;
            focusTarget=id;focusMode=modeId;focusMap=mapId;
            var details=lobby as ILobbyDetails;string selected=map?mapId:id=="marina-more-modes"?(modeId=="ctf"?"disasters":"ctf"):modeId;
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
