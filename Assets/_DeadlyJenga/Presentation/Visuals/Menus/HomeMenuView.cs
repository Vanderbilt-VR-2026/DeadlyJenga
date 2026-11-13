using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR;

namespace DeadlyJenga.Presentation
{
    public sealed class HomeMenuView : MonoBehaviour
    {
        IHomeMenuControls controls;
        Texture2D[] slides;
        RawImage background;
        Image white;
        RectTransform frame, content, pointer;
        Text status, region, nearby;
        InputField code;
        Button join;
        GameObject entryPage, mainPage, joinPage, quickPage, inputPage;
        IHomeQuickPlayControls quick;
        Button quickModeButton, quickMapButton, quickStartButton, recenterButton;
        int modeIndex, mapIndex, quickModes, quickMaps;
        Camera eye;
        bool vr;
        float slideTime, nextNearby;
        int fadePhase=2, recenterFrames;
        Material backdrop, blur;
        Canvas homeCanvas;
        EventSystem homeEvents;
        bool previousNavigation;
        int lastPadId=-1;
        Text padHint;
        RenderTexture blurredSlide;
        readonly List<XRInputSubsystem> trackingOrigins = new List<XRInputSubsystem>();
        int slide, nearbyPage;
        readonly List<Button> buttons = new List<Button>();
        readonly List<Button> nearbyButtons = new List<Button>();
        InputAction leftPointSelect,rightPointSelect;
        bool selectReady,comfortWasOpen,inputFocused=true;
        Button gazeFocus;
        Font font;
        public int SlideIndex => slide;
        public float WhiteFade => white ? white.color.a : 0;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen() => GameEvents.HomeStarted += Attach;
        static void Attach(GameObject root)
        {
            var controls = root.GetComponent<IHomeMenuControls>();
            if (controls != null)
            {
                int pref = PlayerPrefs.GetInt("DeadlyJenga.PreferredControlMode", 0);
                bool useVr = (pref == 1 || pref == 4) ? false : (pref == 2 || pref == 3) ? true : XRSettings.isDeviceActive;
                root.AddComponent<HomeMenuView>().Build(controls, useVr);
            }
        }
        void Build(IHomeMenuControls source, bool useVr)
        {
            controls = source; font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            slides = Resources.LoadAll<Texture2D>("HomeSlides").OrderBy(t => t.name).ToArray();
            vr = useVr;
            eye = new GameObject("Home camera").AddComponent<Camera>(); eye.transform.SetParent(transform);
            eye.transform.localPosition = new Vector3(0, 1.6f, 0); eye.clearFlags = CameraClearFlags.SolidColor; eye.backgroundColor = new Color(.04f,.025f,.09f);
            eye.gameObject.tag = "MainCamera"; eye.gameObject.AddComponent<AudioListener>();
            if (vr)
            {
                var pose = eye.gameObject.AddComponent<TrackedPoseDriver>();
                pose.ignoreTrackingState = true;
                pose.positionInput = new InputActionProperty(new InputAction("Home head position", InputActionType.Value, "<XRHMD>/centerEyePosition"));
                pose.rotationInput = new InputActionProperty(new InputAction("Home head rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation"));
            }
            var canvas = new GameObject("Home menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            homeCanvas=canvas;canvas.transform.SetParent(transform, false); frame = (RectTransform)canvas.transform;
            if (vr)
            {
                canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = eye;
                frame.sizeDelta = new Vector2(1920,1080);
                frame.localScale = Vector3.one * .002f;
                AnchorToHead();
                recenterFrames = 4;
                BuildBackdrop();
                SubsystemManager.GetSubsystems(trackingOrigins);
                foreach(var origin in trackingOrigins) origin.trackingOriginUpdated += TrackingOriginUpdated;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler=canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            }
            var bg = Node("Slideshow", frame, Vector2.zero, new Vector2(1920,1080));
            bg.anchorMin=Vector2.zero; bg.anchorMax=Vector2.one; bg.offsetMin=bg.offsetMax=Vector2.zero;
            background=bg.gameObject.AddComponent<RawImage>(); background.raycastTarget=false;
            var fade=Node("White dissolve", frame,Vector2.zero,new Vector2(1920,1080));
            fade.anchorMin=Vector2.zero;fade.anchorMax=Vector2.one;fade.offsetMin=fade.offsetMax=Vector2.zero;
            white=fade.gameObject.AddComponent<Image>(); white.color=new Color(1,1,1,0); white.raycastTarget=false;
            content=Node("Menu", frame,new Vector2(-565,0),new Vector2(690,980));
            var shade=content.gameObject.AddComponent<Image>();shade.color=new Color(.035f,.02f,.085f,.89f);
            Label(content,"DEADLY",new Vector2(0,390),new Vector2(590,100),78,Color.white);
            Label(content,"JENGA",new Vector2(0,305),new Vector2(590,110),102,new Color(.52f,1,.78f));
            Label(content,"BUILD HIGH.  FALL HARD.",new Vector2(0,215),new Vector2(590,50),23,new Color(.77f,.72f,.94f));
            mainPage=Node("Home actions",content,new Vector2(0,-70),new Vector2(620,470)).gameObject;
            quick=source as IHomeQuickPlayControls;
            entryPage=Node("Choose startup experience",content,new Vector2(0,-70),new Vector2(620,470)).gameObject;
            ActionButton(entryPage.transform,"PLAY MODE","Simulate actual player experience for finished game (limited controls)",120,controls.RandomLobby);
            ActionButton(entryPage.transform,"DEVELOPER MODE","Run the game with full controls for testing and debugging.",-33,controls.Demo);
            quickPage=Node("Quick Play",content,new Vector2(0,-70),new Vector2(620,520)).gameObject;
            BuildQuickPlay();
            joinPage=Node("Join a session",content,new Vector2(0,-65),new Vector2(620,520)).gameObject;
            BuildJoin();ShowPage(entryPage);
            BuildInputModeToggle(content, new Vector2(0, -360), new Vector2(580, 52));
            padHint=Label(content,"",new Vector2(0,-403),new Vector2(580,30),16,new Color(.85f,.9f,1));
            status=Label(content,"",new Vector2(0,-450),new Vector2(580,65),22,new Color(.85f,.83f,.95f));
            Label(frame,"PHYSICS. FRIENDS. PURE MAYHEM.",new Vector2(425,-470),new Vector2(900,50),23,Color.white);
            ButtonAt(frame,"CONTROLS / LEFT OR RIGHT HAND",new Vector2(285,448),new Vector2(570,70),()=>CandyComfortPanel.OpenHome(gameObject,eye,vr));
            recenterButton=ButtonAt(frame,"RECENTER VIEW",new Vector2(740,448),new Vector2(260,62),QueueRecenter);
            recenterButton.gameObject.SetActive(vr);
            if (!FindFirstObjectByType<EventSystem>())
            {
                var events=new GameObject("Home input",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(transform);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            homeEvents=FindFirstObjectByType<EventSystem>();
            previousNavigation=homeEvents.sendNavigationEvents;homeEvents.sendNavigationEvents=false;
            leftPointSelect=MenuAim.SelectAction("Home left select",true);
            rightPointSelect=MenuAim.SelectAction("Home right select",false);
            leftPointSelect.Enable();
            rightPointSelect.Enable();
            pointer=Node("XR pointer",frame,Vector2.zero,new Vector2(14,14));var pointerImg=pointer.gameObject.AddComponent<Image>();pointerImg.sprite=DeadlyJenga.CastlePrototype.CandyHudVisuals.Pill;pointerImg.raycastTarget=false;pointerImg.color=new Color(.45f,1,.8f);pointer.gameObject.SetActive(true);
            SetSlide();
        }
        void ShowPage(GameObject page)
        {
            foreach(var candidate in new[]{entryPage,mainPage,joinPage,quickPage,inputPage})if(candidate)candidate.SetActive(candidate==page);
        }
        void BuildQuickPlay()
        {
            var root=quickPage.transform;
            ButtonAt(root,"BACK",new Vector2(-215,228),new Vector2(150,48),()=>ShowPage(mainPage));
            Label(root,"QUICK PLAY",new Vector2(90,228),new Vector2(390,60),30,Color.white);
            Label(root,"1 · GAME MODE",new Vector2(0,148),new Vector2(580,40),22,Color.white);
            quickModeButton=ButtonAt(root,"",new Vector2(0,95),new Vector2(580,66),()=>
            {
                int count=quick?.QuickPlayModes.Length??0;
                if(count==0)return;
                modeIndex=(modeIndex+1)%count;
                quick.SelectQuickPlayMode(modeIndex);
                mapIndex=0;
                RefreshQuickPlay();
            });
            Label(root,"2 · MAP",new Vector2(0,29),new Vector2(580,40),22,Color.white);
            quickMapButton=ButtonAt(root,"",new Vector2(0,-22),new Vector2(580,66),()=>
            {
                int count=quick?.QuickPlayMaps.Length??0;
                if(count==0)return;
                mapIndex=(mapIndex+1)%count;
                RefreshQuickPlay();
            });
            quickStartButton=ButtonAt(root,"START MATCH",new Vector2(0,-123),new Vector2(580,72),()=>quick?.StartQuickPlay(mapIndex));
            ButtonAt(root,"RANDOM START",new Vector2(0,-216),new Vector2(580,65),controls.RandomLobby);
        }
        void ShowQuickPlay()
        {
            ShowPage(quickPage);
            RefreshQuickPlay();
        }
        void RefreshQuickPlay()
        {
            var modes=quick?.QuickPlayModes??Array.Empty<string>();
            var maps=quick?.QuickPlayMaps??Array.Empty<string>();
            quickModes=modes.Length;
            quickMaps=maps.Length;
            quickModeButton.GetComponentInChildren<Text>().text=quickModes>0?modes[modeIndex]+"  ›":"No available modes";
            quickMapButton.GetComponentInChildren<Text>().text=quickMaps>0?maps[mapIndex]+"  ›":"No available maps";
        }
        void BuildJoin()
        {
            var root=joinPage.transform;
            ButtonAt(root,"BACK",new Vector2(-215,225),new Vector2(150,45),()=>ShowJoin(false));
            region=ButtonAt(root,"REGION",new Vector2(140,225),new Vector2(290,45),controls.NextRegion).GetComponentInChildren<Text>();
            var field=Node("Room code",root,new Vector2(-85,155),new Vector2(410,68));field.gameObject.AddComponent<Image>().color=new Color(.94f,.93f,1);
            code=field.gameObject.AddComponent<InputField>();var text=Label(field,"",Vector2.zero,new Vector2(380,60),30,new Color(.08f,.04f,.18f));code.textComponent=text;code.characterLimit=12;
            code.contentType=InputField.ContentType.Standard;code.onValueChanged.AddListener(value=>controls.Code=value);
            var placeholder=Label(field,"ABC234",Vector2.zero,new Vector2(380,60),28,new Color(.45f,.4f,.53f));code.placeholder=placeholder;
            join=ButtonAt(root,"JOIN",new Vector2(220,155),new Vector2(130,68),controls.JoinCode);
            ButtonAt(root,"DETECT NEARBY",new Vector2(-65,70),new Vector2(450,56),()=>{controls.DetectNearby();nextNearby=0;});
            ButtonAt(root,"NEXT",new Vector2(220,70),new Vector2(130,56),()=>{nearbyPage++;nextNearby=0;});
            nearby=Label(root,"",new Vector2(0,15),new Vector2(580,40),19,Color.white);
            for(int i=0;i<2;i++){int slot=i;nearbyButtons.Add(ButtonAt(root,"",new Vector2(0,-35-i*56),new Vector2(580,48),()=>controls.JoinNearby(nearbyPage*2+slot)));}
            const string keys="ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-";
            for(int i=0;i<keys.Length;i++)
            {
                string key=keys[i].ToString();ButtonAt(root,key,new Vector2(-270+(i%10)*60,-160-(i/10)*48),new Vector2(53,42),()=>{if(controls.Code.Length<12){controls.Code+=key;code.SetTextWithoutNotify(controls.Code);}});
            }
            ButtonAt(root,"DEL",new Vector2(180,-304),new Vector2(110,42),()=>{if(controls.Code.Length>0)controls.Code=controls.Code.Substring(0,controls.Code.Length-1);code.SetTextWithoutNotify(controls.Code);});
        }
        public void ShowJoin(bool value)
        {
            ShowPage(value?joinPage:entryPage);
            if(value){code.SetTextWithoutNotify(controls.Code);controls.DetectNearby();nextNearby=0;}
        }
        void Update()
        {
            if (homeAimBeam) homeAimBeam.enabled = false;
            if(controls==null)return;
            RefreshDisplayMode();
            int padId=MenuGamepad.Connected?.deviceId??0;
            if(padId!=lastPadId){lastPadId=padId;UpdateInputModeText();}
            if(!inputFocused){ClearHomeAim();foreach(var button in buttons)button.interactable=false;return;}
            bool comfortOpen=CandyComfortPanel.HomeShowing(gameObject);
            content.gameObject.SetActive(!comfortOpen);
            if(comfortOpen){ClearHomeAim();comfortWasOpen=true;selectReady=false;foreach(var button in buttons)button.interactable=false;return;}
            if(comfortWasOpen){comfortWasOpen=false;selectReady=false;return;}
            pointer.gameObject.SetActive(false);
            if(homeAimBeam)homeAimBeam.enabled=false;
            AnimateSlideshow(Time.unscaledDeltaTime);Crop();
            status.text=controls.Status;
            foreach(var button in buttons)button.interactable=!controls.Busy;
            if(quickPage.activeSelf)
            {
                quickModeButton.interactable=!controls.Busy&&quickModes>1;
                quickMapButton.interactable=!controls.Busy&&quickMaps>1;
                quickStartButton.interactable=!controls.Busy&&quickMaps>0;
            }
            join.interactable=!controls.Busy&&controls.CodeValid;
            region.text="SHORT CODE REGION / "+controls.Region.ToUpperInvariant();
            if(joinPage.activeSelf&&Time.unscaledTime>=nextNearby)
            {
                nextNearby=Time.unscaledTime+.5f;nearbyPage=Mathf.Clamp(nearbyPage,0,Mathf.Max(0,(controls.NearbyCount-1)/2));
                nearby.text=controls.NearbyCount==0?"No nearby rooms yet. You can still enter a code.":controls.NearbyCount+" nearby room(s)";
                for(int i=0;i<2;i++){int index=nearbyPage*2+i;nearbyButtons[i].gameObject.SetActive(index<controls.NearbyCount);nearbyButtons[i].GetComponentInChildren<Text>().text=controls.NearbyTitle(index);}
            }
            if((Keyboard.current?.escapeKey.wasPressedThisFrame==true||!vr&&MenuGamepad.Cancel)&&!controls.Busy&&!entryPage.activeSelf)
                ShowPage(mainPage.activeSelf?entryPage:mainPage);
            if(Keyboard.current?.enterKey.wasPressedThisFrame==true&&joinPage.activeSelf&&!controls.Busy&&controls.CodeValid)controls.JoinCode();
            if(recenterFrames==0)Point();
        }
        void AnimateSlideshow(float delta)
        {
            slideTime+=Mathf.Clamp(delta,0,.1f);
            float alpha=0;
            if(fadePhase==0)
            {
                if(slideTime>=6.7f){fadePhase=1;slideTime=0;}
            }
            else if(fadePhase==1)
            {
                alpha=Mathf.Clamp01(slideTime/.65f);
                if(alpha>=1){slide=slides.Length==0?0:(slide+1)%slides.Length;SetSlide();fadePhase=2;slideTime=0;}
            }
            else
            {
                alpha=1-Mathf.Clamp01(slideTime/.65f);
                if(alpha<=0){fadePhase=0;slideTime=0;}
            }
            white.color=new Color(1,1,1,alpha);
            if(backdrop)backdrop.SetFloat("_Fade",alpha);
        }

        void BuildBackdrop()
        {
            var shader=Resources.Load<Shader>("CandyHomeBackdrop");
            if(!shader || slides.Length==0)return;
            blurredSlide=new RenderTexture(512,288,0,RenderTextureFormat.ARGB32)
            {
                name="Home slideshow blur",
                useMipMap=false,
                autoGenerateMips=false,
                filterMode=FilterMode.Bilinear,
                wrapMode=TextureWrapMode.Clamp
            };
            blurredSlide.Create();
            var blurShader=Resources.Load<Shader>("CandyHomeBlur");
            if(blurShader)blur=new Material(blurShader);
            backdrop=new Material(shader);
            backdrop.SetTexture("_Slide",blurredSlide);
            var sky=eye.GetComponent<Skybox>();
            if(!sky)sky=eye.gameObject.AddComponent<Skybox>();
            sky.material=backdrop;
            eye.clearFlags=CameraClearFlags.Skybox;
            UpdateBackdropDirection();
        }

        void TrackingOriginUpdated(XRInputSubsystem origin)=>QueueRecenter();
        void QueueRecenter()=>recenterFrames=2;

        void LateUpdate()
        {
            if(!vr || recenterFrames==0 || --recenterFrames!=0)return;
            AnchorToHead();
        }

        void AnchorToHead()
        {
            var forward=Vector3.ProjectOnPlane(eye.transform.forward,Vector3.up);
            if(forward.sqrMagnitude<.001f)forward=frame.forward;
            forward.Normalize();
            frame.SetPositionAndRotation(eye.transform.position+forward*3,Quaternion.LookRotation(forward,Vector3.up));
            UpdateBackdropDirection();
        }

        void UpdateBackdropDirection()
        {
            if(!backdrop)return;
            backdrop.SetVector("_Forward",frame.forward);
            backdrop.SetVector("_Right",frame.right);
        }
        LineRenderer homeAimBeam;
        void ClearHomeAim()
        {
            if(pointer)pointer.gameObject.SetActive(false);
            if(homeAimBeam)homeAimBeam.enabled=false;
            if(gazeFocus)
            {
                gazeFocus.GetComponent<HomeButtonFeedback>()?.Focus(false);
            }
            gazeFocus=null;
        }
        void Point()
        {
            if(!eye||MenuAim.InputConsumed){ClearHomeAim();return;}
            Button target=null;
            bool pressed=false;
            if(vr)
            {
                bool preferred=GetComponent<IBlasterControls>()?.LeftHanded==true;
                var ray=MenuAim.ResolveSelect(eye,transform,preferred,leftPointSelect,rightPointSelect,false,out pressed,out bool tracked);
                var plane=new Plane(frame.forward,frame.position);
                if(Vector3.Dot(ray.direction,frame.forward)<=.01f||!plane.Raycast(ray,out float distance)||distance>8){ClearHomeAim();return;}
                pointer.gameObject.SetActive(true);pointer.position=ray.GetPoint(distance);
                foreach(var button in buttons)
                {
                    if(!button.gameObject.activeInHierarchy||!button.interactable)continue;
                    var rect=(RectTransform)button.transform;var local=rect.InverseTransformPoint(pointer.position);
                    if(rect.rect.Contains(new Vector2(local.x,local.y))){target=button;break;}
                }
                MenuAim.PresentBeam(ref homeAimBeam, transform, ray, pointer.position-ray.direction*.015f, tracked, target);
                if(!selectReady){pressed=false;if(!leftPointSelect.IsPressed()&&!rightPointSelect.IsPressed())selectReady=true;}
            }
            else
            {
                var mouse=Mouse.current;
                if(mouse==null&&MenuGamepad.Current==null){ClearHomeAim();return;}
                Vector2 mouseScreen=MenuGamepad.UsingPointer?MenuGamepad.Position:mouse!=null?mouse.position.ReadValue():MenuGamepad.Position;
                if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(frame,mouseScreen,null,out var localPos)){ClearHomeAim();return;}
                pointer.gameObject.SetActive(true);
                pointer.anchoredPosition=localPos;
                foreach(var button in buttons)
                {
                    if(!button.gameObject.activeInHierarchy||!button.interactable)continue;
                    var rect=(RectTransform)button.transform;
                    if(RectTransformUtility.RectangleContainsScreenPoint(rect,mouseScreen,null)){target=button;break;}
                }
                // Mouse clicks are dispatched once by the EventSystem. The virtual pad pointer submits here.
                pressed=MenuGamepad.UsingPointer&&MenuGamepad.Pressed;
            }
            pointer.SetAsLastSibling();
            pointer.GetComponent<Image>().color=target?Color.white:new Color(.45f,1,.8f);
            pointer.sizeDelta=Vector2.one*(target?24:14);
            if(target!=gazeFocus)
            {
                if(gazeFocus)gazeFocus.GetComponent<HomeButtonFeedback>()?.Focus(false);
                gazeFocus=target;
            }
            if(gazeFocus)gazeFocus.GetComponent<HomeButtonFeedback>()?.Focus(vr||MenuGamepad.UsingPointer);
            if(target&&pressed){selectReady=false;MenuAim.Consume();target.onClick.Invoke();}
        }
        void SetSlide()
        {
            if(slides.Length==0)return;
            background.texture=slides[slide];
            Crop();
            if(!blurredSlide)return;
            var uv=background.uvRect;
            Graphics.Blit(slides[slide],blurredSlide,uv.size,uv.position);
            if(blur)
            {
                var temporary=RenderTexture.GetTemporary(blurredSlide.width,blurredSlide.height,0,blurredSlide.format);
                temporary.filterMode=FilterMode.Bilinear;temporary.wrapMode=TextureWrapMode.Clamp;
                try
                {
                    for(int step=0;step<3;step++)
                    {
                        float radius=2<<step;
                        blur.SetVector("_BlurDirection",new Vector4(radius/blurredSlide.width,0,0,0));
                        Graphics.Blit(blurredSlide,temporary,blur);
                        blur.SetVector("_BlurDirection",new Vector4(0,radius/blurredSlide.height,0,0));
                        Graphics.Blit(temporary,blurredSlide,blur);
                    }
                }
                finally{RenderTexture.ReleaseTemporary(temporary);}
            }
        }
        void Crop()
        {
            if(!background.texture)return;
            float view=background.rectTransform.rect.width/Mathf.Max(1,background.rectTransform.rect.height),image=(float)background.texture.width/background.texture.height;
            background.uvRect=view>image?new Rect(0,(1-image/view)*.5f,1,image/view):new Rect((1-view/image)*.5f,0,view/image,1);
        }
        RectTransform Node(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=size;rect.anchoredPosition=position;return rect;
        }
        Text Label(Transform parent,string value,Vector2 position,Vector2 size,int fontSize,Color color)
        {
            var text=Node(value,parent,position,size).gameObject.AddComponent<Text>();text.font=font;text.text=value;text.fontSize=fontSize;text.color=color;text.alignment=TextAnchor.MiddleLeft;text.verticalOverflow=VerticalWrapMode.Overflow;text.raycastTarget=false;return text;
        }
        Button ButtonAt(Transform parent,string title,Vector2 position,Vector2 size,Action action)
        {
            var rect=Node(title,parent,position,size);var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.24f,.16f,.39f,.97f);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;var colors=button.colors;colors.highlightedColor=new Color(.64f,1,.85f);colors.pressedColor=new Color(.35f,.8f,.65f);button.colors=colors;
            var text=Label(rect,title,Vector2.zero,size-new Vector2(28,0),22,Color.white);text.alignment=TextAnchor.MiddleCenter;
            button.gameObject.AddComponent<HomeButtonFeedback>().Initialize(button);
            button.onClick.AddListener(()=>action());buttons.Add(button);return button;
        }
        void ActionButton(Transform parent,string title,string subtitle,float y,Action action)
        {
            var button=ButtonAt(parent,title,new Vector2(0,y),new Vector2(620,120),action);
            var text=button.GetComponentInChildren<Text>();text.alignment=TextAnchor.UpperLeft;text.fontSize=35;text.rectTransform.anchoredPosition=new Vector2(9,-12);
            Label(button.transform,subtitle,new Vector2(10,-30),new Vector2(580,48),21,new Color(.82f,.8f,.92f));
        }
        Button inputModeButton;
        Text inputModeText;
        const string ControlModePrefKey = "DeadlyJenga.PreferredControlMode";
        void BuildInputModeToggle(Transform parent, Vector2 position, Vector2 size)
        {
            var rect = Node("Input mode toggle", parent, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.16f, .10f, .26f, .88f);
            inputModeButton = rect.gameObject.AddComponent<Button>();
            inputModeButton.targetGraphic = image;
            var colors = inputModeButton.colors;
            colors.highlightedColor = new Color(.30f, .22f, .46f, .95f);
            colors.pressedColor = new Color(.45f, .34f, .65f, 1f);
            inputModeButton.colors = colors;
            inputModeText = Label(rect, "", Vector2.zero, size - new Vector2(24, 0), 20, new Color(.78f, .74f, .92f));
            inputModeText.alignment = TextAnchor.MiddleCenter;
            inputModeText.supportRichText = true;
            inputModeButton.gameObject.AddComponent<HomeButtonFeedback>().Initialize(inputModeButton);
            inputModeButton.onClick.AddListener(()=>ShowPage(inputPage));
            inputPage=Node("Choose input",parent,new Vector2(0,-70),new Vector2(620,470)).gameObject;
            ButtonAt(inputPage.transform,"BACK",new Vector2(-215,200),new Vector2(150,48),()=>ShowPage(entryPage));
            string[] modes={"AUTO-DETECT","KEYBOARD & MOUSE","PC XR SIMULATION","VR HEADSET","GAMEPAD"};
            for(int i=0;i<modes.Length;i++)
            {
                int selected=i;
                ButtonAt(inputPage.transform,modes[i],new Vector2(0,125-i*64),new Vector2(580,54),()=>SetInputMode(selected));
            }
            inputPage.SetActive(false);
            buttons.Add(inputModeButton);
            UpdateInputModeText();
        }
        void SetInputMode(int next)
        {
            PlayerPrefs.SetInt(ControlModePrefKey, next);
            PlayerPrefs.Save();
            UpdateInputModeText();
            XRSimulationManager.ApplyMode(next);
            ClearHomeAim();RefreshDisplayMode();ShowPage(entryPage);
        }
        void UpdateInputModeText()
        {
            int mode = PlayerPrefs.GetInt(ControlModePrefKey, 0);
            string modeLabel = mode switch
            {
                1 => "<color=#52ffc6>PC / DESKTOP</color>  (Keyboard & Mouse)",
                2 => "<color=#ffa87d>XR SIMULATION</color>  (Emulated Quest Joysticks)",
                3 => "<color=#7dc5ff>VR HEADSET</color>  (Physical Headset)",
                4 => "<color=#52ffc6>GAMEPAD</color>  · "+MenuGamepad.DeviceName,
                _ => "<color=#d5c8ff>AUTO-DETECT</color>  · "+(vr?"VR headset":MenuGamepad.Connected!=null?MenuGamepad.DeviceName:"Keyboard & mouse")
            };
            if(padHint)padHint.text=MenuGamepad.Current!=null?"L stick move · R stick look · Bottom button jump/climb\nRT fire · LT aim · Left button interact · Start menu":"";
            if (inputModeText)
                inputModeText.text = "<size=17><color=#8d82aa>INPUT MODE · </color></size>" + modeLabel + "  <size=17><color=#8d82aa>›</color></size>";
        }
        void RefreshDisplayMode()
        {
            int pref=PlayerPrefs.GetInt(ControlModePrefKey,0);
            bool next=pref==1||pref==4?false:pref==2||pref==3||XRSettings.isDeviceActive;
            var module=homeEvents?homeEvents.GetComponent<InputSystemUIInputModule>():null;
            if(module)module.enabled=!next;
            if(next==vr)return;
            ClearHomeAim();vr=next;
            if(recenterButton)recenterButton.gameObject.SetActive(vr);
            var pose=eye.GetComponent<TrackedPoseDriver>();
            if(vr)
            {
                if(!pose)
                {
                    pose=eye.gameObject.AddComponent<TrackedPoseDriver>();pose.ignoreTrackingState=true;
                    pose.positionInput=new InputActionProperty(new InputAction("Home head position",InputActionType.Value,"<XRHMD>/centerEyePosition"));
                    pose.rotationInput=new InputActionProperty(new InputAction("Home head rotation",InputActionType.Value,"<XRHMD>/centerEyeRotation"));
                }
                pose.enabled=true;
                homeCanvas.renderMode=RenderMode.WorldSpace;homeCanvas.worldCamera=eye;
                homeCanvas.GetComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
                frame.sizeDelta=new Vector2(1920,1080);frame.localScale=Vector3.one*.002f;
                if(!backdrop)BuildBackdrop();else eye.clearFlags=CameraClearFlags.Skybox;
                if(trackingOrigins.Count==0)
                {
                    SubsystemManager.GetSubsystems(trackingOrigins);
                    foreach(var origin in trackingOrigins)origin.trackingOriginUpdated+=TrackingOriginUpdated;
                }
                AnchorToHead();recenterFrames=4;SetSlide();
            }
            else
            {
                if(pose)pose.enabled=false;
                eye.transform.localPosition=new Vector3(0,1.6f,0);eye.transform.localRotation=Quaternion.identity;
                eye.clearFlags=CameraClearFlags.SolidColor;homeCanvas.renderMode=RenderMode.ScreenSpaceOverlay;
                frame.localScale=Vector3.one;frame.localRotation=Quaternion.identity;
                var scaler=homeCanvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
                recenterFrames=0;
            }
            selectReady=false;UpdateInputModeText();
        }
        void OnApplicationFocus(bool focused){inputFocused=focused;if(!focused){selectReady=false;if(pointer)pointer.gameObject.SetActive(false);}}
        void OnDestroy()
        {
            leftPointSelect?.Dispose();rightPointSelect?.Dispose();
            foreach(var origin in trackingOrigins) origin.trackingOriginUpdated -= TrackingOriginUpdated;
            if(homeEvents){homeEvents.sendNavigationEvents=previousNavigation;var module=homeEvents.GetComponent<InputSystemUIInputModule>();if(module)module.enabled=true;}
            if(backdrop)Destroy(backdrop);
            if(blur)Destroy(blur);
            if(blurredSlide)
            {
                blurredSlide.Release();
                Destroy(blurredSlide);
            }
        }
    }
}
