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
        sealed class Target {public string id;public BoxCollider collider;public Image face;public Color color;public Action action;public Func<bool> allowed;public Text actionLabel;public Outline outline;public Shadow glow;public float pressedUntil;}
        readonly List<Target> targets=new();
        ILobbyControls lobby;ISessionInfo session;Action<string> detail;
        bool requireRelease=true,inputFocused=true;
        bool ModalBlocked=>MenuAim.InputConsumed||!inputFocused||GameExperiencePanel.Showing(session)||CandyWildlifePanel.Showing(session)||CandyComfortPanel.Showing(session)||CharacterPicker.Showing(session);
        static readonly Color Plum=new(.12f,.055f,.19f);
        static readonly Color Cream=new(1,.96f,.83f);
        static readonly Color ProjectionInk=new(.15f,.19f,.26f);
        static readonly Color ProjectionGold=new(.94f,.78f,.49f);
        static readonly Color Lilac=new(.67f,.36f,.87f);
        static readonly Color Mint=new(.26f,.84f,.64f);
        InputAction select,leftSelect;
        Image aimDot;
        RectTransform canvas,mainFace,joinFace,hostFace;
        Text modeTitle,mapTitle,playTitle,hostTitle,focusDescription;
        Text modeMissing,mapMissing,joinStatus,hostStatus;
        RawImage modePicture,mapPicture;
        Texture2D projectionBackdrop;
        string modeId,mapId,focusTarget,focusMode,focusMap;
        float refreshAt;
        RectTransform focusPanel;
        public bool Visible
        {
            get => mainFace && mainFace.gameObject.activeSelf;
            set
            {
                if (!mainFace || mainFace.gameObject.activeSelf == value) return;
                if (value) Refresh();
                mainFace.gameObject.SetActive(value);
            }
        }
        bool AnyFaceVisible => Visible || (joinFace && joinFace.gameObject.activeSelf) ||
            (hostFace && hostFace.gameObject.activeSelf);

        internal static Vector3 CabinetScreen(bool host) => new Vector3(host ? 4.87f : -4.87f, 3.93f, 10.48f);

        public void SetCabinetVisibility(bool join, bool host)
        {
            if (joinFace) joinFace.gameObject.SetActive(join);
            if (hostFace) hostFace.gameObject.SetActive(host);
        }
        public void Build(ILobbyControls controls,Action<string> openDetail)
        {
            if(canvas)return;
            lobby=controls;session=Services.Session;detail=openDetail;
            var go=new GameObject("Marina · live cinema terminal",typeof(RectTransform),typeof(Canvas));
            go.transform.SetParent(transform,false);
            canvas=go.GetComponent<RectTransform>();canvas.sizeDelta=new Vector2(720,320);
            canvas.localPosition=MayhemMarina.PlayScreen+Vector3.back*.20f;canvas.localScale=Vector3.one*.0095f;
            var worldCanvas=go.GetComponent<Canvas>();worldCanvas.renderMode=RenderMode.WorldSpace;
            worldCanvas.overrideSorting=true;worldCanvas.sortingOrder=70;

            mainFace = new GameObject("Marina · map and mode display", typeof(RectTransform)).GetComponent<RectTransform>();
            mainFace.SetParent(canvas,false);
            mainFace.sizeDelta=canvas.sizeDelta;
            BuildProjectedBackdrop();
            var marquee=Text("N O W   S H O W I N G",new Vector2(0,140),new Vector2(500,22),15,mainFace);
            marquee.color=ProjectionGold;
            Text("PICK YOUR MATCH",new Vector2(0,113),new Vector2(635,54),40,mainFace);
            var ruleColor=new Color(.94f,.78f,.49f,.62f);
            for(int side=-1;side<=1;side+=2)
            {
                Panel("Cinema title rule",canvas,new Vector2(side*260,113),new Vector2(103,2),ruleColor);
                Panel("Cinema title diamond",canvas,new Vector2(side*321,113),new Vector2(7,7),ProjectionGold)
                    .rectTransform.localRotation=Quaternion.Euler(0,0,45);
            }

            modePicture=Card("marina-modes","GAME MODE",new Vector2(-169,12),()=>detail?.Invoke("modes"),out modeTitle,out modeMissing);
            mapPicture=Card("marina-maps","MAP",new Vector2(169,12),()=>detail?.Invoke("maps"),out mapTitle,out mapMissing);
            var play=Button("marina-play",new Vector2(0,-111),new Vector2(378,36),ProjectionGold,lobby.StartOrCancel,()=>lobby.CanStart);
            playTitle=ActionText("Start match",new Vector2(355,36),26,play);
            SmallButton("marina-settings","Settings",new Vector2(-263,-111),()=>detail?.Invoke("settings"));
            SmallButton("marina-style","Style",new Vector2(263,-111),()=>detail?.Invoke("style"));
            var round=Button("marina-experience",new Vector2(0,-146),new Vector2(190,24),ProjectionInk,
                ()=>GameExperiencePanel.Open(session),()=>GameExperiencePanel.Resolve(session)!=null);
            ActionText("Round info",new Vector2(180,24),16,round);

            joinFace=CabinetFace("Lavender Join display",false);
            hostFace=CabinetFace("Mint Host display",true);
            CabinetHeading(joinFace,"JOIN FRIENDS",Lilac);
            CabinetHeading(hostFace,"HOST ROOM",Mint);
            CabinetAction(joinFace,"marina-join","Room code",17,Lilac,()=>detail?.Invoke("join-code"));
            CabinetAction(joinFace,"marina-nearby","Find nearby",-27,new Color(.34f,.17f,.45f),()=>detail?.Invoke("join-nearby"),()=>lobby.JoinEnabled);
            hostTitle=CabinetAction(hostFace,"marina-host","Create room",17,Mint,()=>detail?.Invoke("host"));
            CabinetAction(hostFace,"marina-invite","Invite nearby",-27,new Color(.29f,.18f,.41f),()=>detail?.Invoke("host-invite"),()=>lobby.CanInvite);

            joinStatus=Text("",new Vector2(0,-62),new Vector2(165,23),14,joinFace);
            hostStatus=Text("",new Vector2(0,-62),new Vector2(165,23),14,hostFace);
            focusPanel=new GameObject("Marina · selection preview",typeof(RectTransform)).GetComponent<RectTransform>();
            focusPanel.SetParent(mainFace,false);focusPanel.anchoredPosition=new Vector2(0,-78);
            Panel("Marina · preview backing",focusPanel,Vector2.zero,new Vector2(674,23),Plum);
            focusDescription=Text("",Vector2.zero,new Vector2(660,22),14,focusPanel);
            focusDescription.resizeTextForBestFit=true;focusDescription.resizeTextMinSize=12;focusDescription.resizeTextMaxSize=14;
            focusPanel.gameObject.SetActive(false);
            select=new InputAction("Marina select",InputActionType.Button,"<XRController>{RightHand}/trigger");select.Enable();
            leftSelect=new InputAction("Marina left select",InputActionType.Button,"<XRController>{LeftHand}/trigger");leftSelect.Enable();
            aimDot=MenuAim.CreateReticle(canvas);Refresh();
        }
        void BuildProjectedBackdrop()
        {
            const int width=144,height=64;
            projectionBackdrop=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {
                name="Marina projection vignette",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear
            };
            var pixels=new Color[width*height];
            var grain=new System.Random(1917);
            var edge=new Color(.085f,.10f,.15f,1);
            var center=new Color(.28f,.31f,.37f,1);
            for(int y=0;y<height;y++)
            for(int x=0;x<width;x++)
            {
                float u=x/(width-1f)*2-1;
                float v=y/(height-1f)*2-1;
                float light=Mathf.Clamp01(1-.49f*u*u-.44f*v*v);
                float noise=((float)grain.NextDouble()-.5f)*.032f;
                pixels[y*width+x]=Color.Lerp(edge,center,light*light)+new Color(noise,noise,noise,0);
            }
            projectionBackdrop.SetPixels(pixels);
            projectionBackdrop.Apply(false,true);

            var backdrop=new GameObject("Marina · projected picture",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
            backdrop.transform.SetParent(mainFace,false);
            backdrop.rectTransform.sizeDelta=new Vector2(720,320);
            backdrop.texture=projectionBackdrop;
            backdrop.raycastTarget=false;
        }
        static Image Panel(string name,RectTransform parent,Vector2 position,Vector2 size,Color color)
        {
            var panel=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            panel.transform.SetParent(parent,false);panel.rectTransform.anchoredPosition=position;panel.rectTransform.sizeDelta=size;
            panel.sprite=CandyHudVisuals.Pill;panel.type=Image.Type.Sliced;panel.color=color;panel.raycastTarget=false;
            return panel;
        }
        void SmallButton(string id,string title,Vector2 position,Action action)
        {
            var button=Button(id,position,new Vector2(122,34),ProjectionInk,action,()=>true);
            ActionText(title,new Vector2(113,32),17,button);
        }
        RectTransform CabinetFace(string name,bool host)
        {
            var face=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            face.SetParent(canvas,false);
            face.sizeDelta=new Vector2(175,157);
            face.localPosition=(CabinetScreen(host)-canvas.localPosition)/canvas.localScale.x;
            return face;
        }
        static void CabinetHeading(RectTransform parent,string title,Color trim)
        {
            Panel("Marina · cabinet marquee",parent,new Vector2(0,59),new Vector2(167,34),trim);
            var label=Text(title,new Vector2(0,59),new Vector2(165,34),21,parent);
            label.color=Plum;
        }
        Text CabinetAction(RectTransform parent,string id,string title,float y,Color color,Action action,Func<bool> allowed=null)
        {
            var button=Button(id,new Vector2(0,y),new Vector2(154,36),color,action,allowed??(()=>true),parent);
            return ActionText(title,new Vector2(147,36),18,button);
        }
        RawImage Card(string id,string section,Vector2 position,Action action,out Text title,out Text missing)
        {
            var button=Button(id,position,new Vector2(321,152),new Color(.57f,.47f,.34f),action,()=>GameExperiencePanel.CanEditRawChoices(session));
            Panel("Marina · illustration panel",button,Vector2.zero,new Vector2(313,144),ProjectionInk);
            var crop=Panel("Marina · illustration crop",button,new Vector2(0,12),new Vector2(309,116),Color.white);
            crop.gameObject.AddComponent<Mask>().showMaskGraphic=false;
            var go=new GameObject("Live destination image",typeof(RectTransform),typeof(RawImage));
            go.transform.SetParent(crop.transform,false);var image=go.GetComponent<RawImage>();
            image.rectTransform.sizeDelta=crop.rectTransform.sizeDelta;image.raycastTarget=false;
            image.color=new Color(.96f,.93f,.86f);
            missing=Text("Preview unavailable",new Vector2(0,12),new Vector2(285,32),17,button);
            Panel("Marina · category badge",button,new Vector2(-91,56),new Vector2(117,23),ProjectionInk);
            Text(section,new Vector2(-91,56),new Vector2(113,23),13,button);
            title=Text("",new Vector2(0,-59),new Vector2(299,25),22,button);
            title.resizeTextForBestFit=true;title.resizeTextMinSize=14;title.resizeTextMaxSize=22;
            return image;
        }
        RectTransform Button(string id,Vector2 p,Vector2 size,Color color,Action action,Func<bool> allowed,RectTransform parent=null)
        {
            var go=new GameObject(id,typeof(RectTransform),typeof(Image),typeof(BoxCollider));go.layer=5;go.transform.SetParent(parent?parent:mainFace,false);var r=go.GetComponent<RectTransform>();r.anchoredPosition=p;r.sizeDelta=size;
            var image=go.GetComponent<Image>();image.sprite=CandyHudVisuals.Pill;image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=false;
            var hit=go.GetComponent<BoxCollider>();hit.isTrigger=true;hit.size=new Vector3(size.x,size.y,6);var outline=go.AddComponent<Outline>();outline.effectDistance=new Vector2(4,-4);outline.effectColor=Color.clear;
            var glow=go.AddComponent<Shadow>();glow.effectDistance=new Vector2(0,-8);glow.effectColor=Color.clear;
            targets.Add(new Target{id=id,collider=hit,face=image,color=color,action=action,allowed=allowed,outline=outline,glow=glow});return r;
        }
        Text ActionText(string value,Vector2 size,int font,RectTransform button)
        {
            var label=Text(value,Vector2.zero,size,font,button);
            var target=targets.Find(item=>item.face.rectTransform==button);
            if(target!=null)target.actionLabel=label;
            label.resizeTextForBestFit=true;label.resizeTextMinSize=12;label.resizeTextMaxSize=font;
            return label;
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
            playTitle.text=(lobby as ILobbyMatchVisitControls)?.VisitingLobby==true?"Return to match":
                rotating&&experience.GamePhase==RoomGamePhase.Selected?
                "Next match in "+Mathf.CeilToInt(experience.CountdownSeconds)+" s":lobby.StartPending?"Cancel start":"Start match";
            hostTitle.text=lobby.Connected?(lobby.IsHost?"Manage room":"Room options"):"Create room";
            joinStatus.text=lobby.Busy?"Connecting…":lobby.Connected?"In a room":"Find your friends";
            hostStatus.text=lobby.Busy?"Working…":lobby.IsHost&&lobby.Connected?"You are host":lobby.Connected?"Joined as guest":"Host your friends";
            var details=lobby as ILobbyDetails;
            string nextMode=details?.SelectedModeId??"mystery",nextMap=details?.SelectedMapId??"castle";
            if(modeId!=nextMode)
            {
                modeId=nextMode;
                modePicture.texture=Resources.Load<Texture2D>("LobbyModePictures/"+modeId)??Resources.Load<Texture2D>("LobbyModePictures/demo");
                FitPreview(modePicture,modeMissing);
            }
            if(mapId!=nextMap){mapId=nextMap;mapPicture.texture=MapPreviewImages.Get(mapId);FitPreview(mapPicture,mapMissing);}
        }
        static void FitPreview(RawImage image,Text missing)
        {
            image.enabled=image.texture;
            missing.gameObject.SetActive(!image.texture);
            if(!image.texture)return;
            float source=(float)image.texture.width/image.texture.height;
            float target=image.rectTransform.sizeDelta.x/image.rectTransform.sizeDelta.y;
            float width=Mathf.Min(1,target/source),height=Mathf.Min(1,source/target);
            image.uvRect=new Rect((1-width)*.5f,(1-height)*.5f,width,height);
        }
        public bool Invoke(string id) => Activate(targets.Find(target => target.id == id), 0);

        bool Activate(Target target, int pointerDevice)
        {
            if (ModalBlocked || requireRelease || target == null ||
                !target.face.gameObject.activeInHierarchy || !target.allowed()) return false;
            target.pressedUntil = Time.unscaledTime + .16f;
            PaintTarget(target, true, true);
            requireRelease = true;
            MenuAim.Consume();
            MenuAim.PulsePress(pointerDevice);
            target.action();
            return true;
        }

        static void PaintTarget(Target target, bool hover, bool allowed)
        {
            bool pressed = allowed && Time.unscaledTime < target.pressedUntil;
            target.face.color = !allowed ? Color.Lerp(target.color, Plum, .70f) :
                pressed ? Color.Lerp(target.color, Mint, .70f) :
                hover ? Color.Lerp(target.color, Cream, .35f) : target.color;
            var rim = pressed ? Cream : CandyHudVisuals.Mint;
            float outline = allowed && (hover || pressed) ? 1 : 0;
            target.outline.effectColor = new Color(rim.r, rim.g, rim.b, outline);
            target.glow.effectColor = new Color(rim.r, rim.g, rim.b, outline * (pressed ? .85f : .50f));
            if(target.actionLabel)
            {
                Color face=target.face.color.linear;
                float luminance=.2126f*face.r+.7152f*face.g+.0722f*face.b;
                target.actionLabel.color=allowed?(luminance>.22f?Plum:Cream):CandyHudVisuals.Muted;
            }
        }
        void Update()
        {
            if(!AnyFaceVisible||ModalBlocked){requireRelease=true;if(aimDot)aimDot.gameObject.SetActive(false);if(focusPanel)focusPanel.gameObject.SetActive(false);return;}if(Time.unscaledTime>=refreshAt){refreshAt=Time.unscaledTime+.2f;Refresh();}
            var player=session?.Player;if(!NetworkActors.Alive(player)||!player.ViewCamera){aimDot.gameObject.SetActive(false);return;}
            var mouse=Mouse.current;var keyboard=Keyboard.current;
            bool held=player.IsVR ? select.IsPressed() || leftSelect.IsPressed() || player.SelectHeld :
                !lobby.CodeFocused && keyboard?.eKey.isPressed==true || mouse?.leftButton.isPressed==true;
            if(requireRelease){if(!held)requireRelease=false;return;}
            bool pressed=player.IsVR ? player.SelectPressed :
                !lobby.CodeFocused && keyboard?.eKey.wasPressedThisFrame==true || mouse?.leftButton.wasPressedThisFrame==true;
            int pointerDevice = 0;
            var camera=player.ViewCamera;
            Ray ray;
            if (player.IsVR)
            {
                bool preferredLeft=player.gameObject.GetComponent<IBlasterControls>()?.LeftHanded==true;
                ray=MenuAim.Resolve(camera,camera.transform.parent,preferredLeft,false,false,out _,out _);
                var controllerAction=leftSelect.WasPressedThisFrame()?leftSelect:select.WasPressedThisFrame()?select:
                    leftSelect.IsPressed()&&(!select.IsPressed()||preferredLeft)?leftSelect:select;
                if (controllerAction.IsPressed() || controllerAction.WasPressedThisFrame())
                {
                    if (MenuAim.TryActionPointer(camera,controllerAction,out var controllerRay,out pointerDevice))
                    {
                        ray=controllerRay;
                        pressed=controllerAction.WasPressedThisFrame();
                    }
                    else pressed=false;
                }
            }
            else
                ray=session.MenuOpen&&mouse!=null?camera.ScreenPointToRay(mouse.position.ReadValue()):new Ray(camera.transform.position,camera.transform.forward);
            float screenRange = 11;
            if (Physics.Raycast(ray, out var obstruction, screenRange, 1 << Layers.Surface, QueryTriggerInteraction.Ignore))
                screenRange = obstruction.distance;
            var nearest = PickTarget(ray, screenRange);
            foreach (var target in targets)
                if (target.face.gameObject.activeInHierarchy) PaintTarget(target, target == nearest, target.collider.enabled);
            PreviewDestination(nearest?.id);
            if (nearest != null && MenuAim.TryPoint(ray, nearest.face.rectTransform, screenRange, out _, out var point) ||
                VisibleFacePoint(ray, mainFace, screenRange, out point) ||
                VisibleFacePoint(ray, joinFace, screenRange, out point) ||
                VisibleFacePoint(ray, hostFace, screenRange, out point))
                MenuAim.Present(aimDot, point, true, nearest != null && nearest.collider.enabled);
            else aimDot.gameObject.SetActive(false);
            if (pressed && nearest != null) Activate(nearest, pointerDevice);
        }
        Target PickTarget(Ray ray, float range)
        {
            Target nearest = null;
            foreach (var target in targets)
            {
                if (!target.face.gameObject.activeInHierarchy) continue;
                target.collider.enabled = target.allowed();
                // A disabled control still owns its area. Never click through it to another button.
                if (!MenuAim.TryPoint(ray, target.face.rectTransform, range, out _, out var hit)) continue;
                nearest = target;
                range = Vector3.Distance(ray.origin, hit);
            }
            return nearest;
        }

        static bool VisibleFacePoint(Ray ray, RectTransform face, float distance, out Vector3 point)
        {
            point = default;
            return face.gameObject.activeInHierarchy && MenuAim.TryPoint(ray, face, distance, out _, out point);
        }

        void PreviewDestination(string id)
        {
            bool map=id=="marina-maps",mode=id=="marina-modes";
            bool blockedStart=id=="marina-play"&&!lobby.CanStart;
            focusPanel.gameObject.SetActive(map||mode||blockedStart);
            if(blockedStart)
            {
                focusTarget=null;
                focusDescription.text=lobby.Busy?"Waiting for the room to finish connecting.":
                    !lobby.IsHost?"Only the host can start the match.":"The match is not ready to start.";
                return;
            }
            if(!map&&!mode)return;
            if(!GameExperiencePanel.CanEditRawChoices(session))
            {
                focusTarget=null;
                focusDescription.text=lobby.IsHost?"This match chooses its mode and map automatically.":"The host chooses the mode and map.";
                return;
            }
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
        void OnDestroy(){select?.Dispose();leftSelect?.Dispose();if(canvas)Destroy(canvas.gameObject);if(projectionBackdrop)Destroy(projectionBackdrop);}
    }
}
