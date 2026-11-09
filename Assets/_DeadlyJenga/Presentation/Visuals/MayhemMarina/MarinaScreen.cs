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
        sealed class Target {public string id;public BoxCollider collider;public Image face;public Color color;public Action action;public Func<bool> allowed;public Text[] labels;public Outline outline;public Shadow glow;public float pressedUntil;}
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
        RectTransform canvas,mainFace,joinFace,hostFace;
        Text modeTitle,mapTitle,playTitle,hostTitle,focusDescription;
        RawImage modePicture,mapPicture;
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
            var go=new GameObject("Marina · live candy arcade",typeof(RectTransform),typeof(Canvas));
            go.transform.SetParent(transform,false);
            canvas=go.GetComponent<RectTransform>();canvas.sizeDelta=new Vector2(720,320);
            canvas.localPosition=MayhemMarina.PlayScreen+Vector3.back*.20f;canvas.localScale=Vector3.one*.0095f;
            var worldCanvas=go.GetComponent<Canvas>();worldCanvas.renderMode=RenderMode.WorldSpace;
            worldCanvas.overrideSorting=true;worldCanvas.sortingOrder=70;

            mainFace = new GameObject("Marina � map and mode display", typeof(RectTransform)).GetComponent<RectTransform>();
            mainFace.SetParent(canvas,false);
            mainFace.sizeDelta=canvas.sizeDelta;
            var title=Text("PICK YOUR MATCH",new Vector2(0,119),new Vector2(635,54),46,mainFace);
            var depth=title.gameObject.AddComponent<Shadow>();depth.effectDistance=new Vector2(0,-5);depth.effectColor=Coral;
            Ornament(new Vector2(-286,118),Lilac,-12);
            Ornament(new Vector2(286,118),Mint,12);

            modePicture=Card("marina-modes","GAME MODE",new Vector2(-169,1),()=>detail?.Invoke("modes"),out modeTitle);
            mapPicture=Card("marina-maps","MAP",new Vector2(169,1),()=>detail?.Invoke("maps"),out mapTitle);
            var play=Button("marina-play",new Vector2(0,-119),new Vector2(378,42),Coral,lobby.StartOrCancel,()=>lobby.CanStart);
            playTitle=Text("Start match",Vector2.zero,new Vector2(355,41),29,play);
            SmallButton("marina-settings","Settings",new Vector2(-263,-124),()=>detail?.Invoke("settings"));
            SmallButton("marina-style","Style",new Vector2(263,-124),()=>detail?.Invoke("style"));
            var round=Button("marina-experience",new Vector2(0,-152),new Vector2(190,16),Plum,
                ()=>GameExperiencePanel.Open(session),()=>GameExperiencePanel.Resolve(session)!=null);
            Text("Round info",Vector2.zero,new Vector2(180,16),13,round);

            joinFace=CabinetFace("Lavender Join display",false);
            hostFace=CabinetFace("Mint Host display",true);
            CabinetHeading(joinFace,"JOIN FRIENDS",Lilac);
            CabinetHeading(hostFace,"HOST ROOM",Mint);
            CabinetAction(joinFace,"marina-join","Room code",17,Lilac,()=>detail?.Invoke("join-code"));
            CabinetAction(joinFace,"marina-nearby","Find nearby",-27,new Color(.34f,.17f,.45f),()=>detail?.Invoke("join-nearby"),()=>lobby.JoinEnabled);
            hostTitle=CabinetAction(hostFace,"marina-host","Create room",17,Mint,()=>detail?.Invoke("host"));
            CabinetAction(hostFace,"marina-invite","Invite nearby",-27,new Color(.29f,.18f,.41f),()=>detail?.Invoke("host-invite"),()=>lobby.CanInvite);

            focusPanel=new GameObject("Marina · selection preview",typeof(RectTransform)).GetComponent<RectTransform>();
            focusPanel.SetParent(mainFace,false);focusPanel.anchoredPosition=new Vector2(0,-94);
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
            var go=new GameObject(id,typeof(RectTransform),typeof(Image),typeof(BoxCollider));go.layer=5;go.transform.SetParent(parent?parent:mainFace,false);var r=go.GetComponent<RectTransform>();r.anchoredPosition=p;r.sizeDelta=size;
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
            hostTitle.text=lobby.Connected&&lobby.HostActive?"Manage room":"Create room";
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
            var ink = target.face.color.grayscale > .6f ? Plum : Cream;
            foreach (var text in target.labels) text.color = allowed ? ink : CandyHudVisuals.Muted;
        }
        void Update()
        {
            if(!AnyFaceVisible||ModalBlocked){requireRelease=true;if(aimDot)aimDot.gameObject.SetActive(false);if(focusPanel)focusPanel.gameObject.SetActive(false);return;}if(Time.unscaledTime>=refreshAt){refreshAt=Time.unscaledTime+.2f;Refresh();}
            var player=session?.Player;if(!NetworkActors.Alive(player)||!player.ViewCamera)return;
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
