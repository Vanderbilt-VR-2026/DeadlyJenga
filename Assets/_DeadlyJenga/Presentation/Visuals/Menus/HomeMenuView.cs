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
        GameObject mainPage, joinPage;
        Camera eye;
        bool vr;
        float slideTime, nextNearby;
        int fadePhase=2, vrFrames;
        int slide, nearbyPage;
        readonly List<Button> buttons = new List<Button>();
        readonly List<Button> nearbyButtons = new List<Button>();
        InputAction trigger, handPosition, handRotation, tracked;
        Font font;
        public int SlideIndex => slide;
        public float WhiteFade => white ? white.color.a : 0;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen() => GameEvents.HomeStarted += Attach;
        static void Attach(GameObject root)
        {
            var controls = root.GetComponent<IHomeMenuControls>();
            if (controls != null) root.AddComponent<HomeMenuView>().Build(controls);
        }
        void Build(IHomeMenuControls source)
        {
            controls = source; font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            slides = Resources.LoadAll<Texture2D>("HomeSlides").OrderBy(t => t.name).ToArray();
            vr = XRSettings.isDeviceActive;
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
            canvas.transform.SetParent(transform, false); frame = (RectTransform)canvas.transform;
            if (vr)
            {
                canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = eye;
                frame.sizeDelta = new Vector2(1920,1080); frame.position = new Vector3(0,1.6f,3); frame.localScale = Vector3.one * .002f;
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
            ActionButton(mainPage.transform,"JOIN", "Room code or nearby friends",160,()=>ShowJoin(true));
            ActionButton(mainPage.transform,"HOST", "Create a private lobby / no bots",40,controls.Host);
            ActionButton(mainPage.transform,"JOIN RANDOM SESSION", "Jump into a lobby / bots fill to 10",-80,controls.RandomLobby);
            ActionButton(mainPage.transform,"DEMO", "Straight to Candy Castle / sandbox",-200,controls.Demo);
            joinPage=Node("Join a session",content,new Vector2(0,-65),new Vector2(620,520)).gameObject;
            BuildJoin();joinPage.SetActive(false);
            status=Label(content,"",new Vector2(0,-450),new Vector2(580,65),22,new Color(.85f,.83f,.95f));
            Label(frame,"PHYSICS. FRIENDS. PURE MAYHEM.",new Vector2(425,-470),new Vector2(900,50),23,Color.white);
            if (!FindFirstObjectByType<EventSystem>())
            {
                var events=new GameObject("Home input",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(transform);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            trigger=new InputAction("Home select",InputActionType.Button,"<XRController>{RightHand}/trigger");trigger.Enable();
            handPosition=new InputAction("Home pointer position",InputActionType.Value,"<XRController>{RightHand}/devicePosition");handPosition.Enable();
            handRotation=new InputAction("Home pointer rotation",InputActionType.Value,"<XRController>{RightHand}/deviceRotation");handRotation.Enable();
            tracked=new InputAction("Home pointer tracked",InputActionType.Button,"<XRController>{RightHand}/isTracked");tracked.Enable();
            pointer=Node("XR pointer",frame,Vector2.zero,new Vector2(14,14));pointer.gameObject.AddComponent<Image>().color=new Color(.45f,1,.8f);pointer.gameObject.SetActive(vr);
            SetSlide();
        }
        void BuildJoin()
        {
            var root=joinPage.transform;
            ButtonAt(root,"BACK",new Vector2(-215,225),new Vector2(150,45),()=>ShowJoin(false));
            region=ButtonAt(root,"REGION",new Vector2(140,225),new Vector2(290,45),controls.NextRegion).GetComponentInChildren<Text>();
            var field=Node("Room code",root,new Vector2(-85,155),new Vector2(410,68));field.gameObject.AddComponent<Image>().color=new Color(.94f,.93f,1);
            code=field.gameObject.AddComponent<InputField>();var text=Label(field,"",Vector2.zero,new Vector2(380,60),30,new Color(.08f,.04f,.18f));code.textComponent=text;code.characterLimit=12;
            code.contentType=InputField.ContentType.Standard;code.onValueChanged.AddListener(value=>controls.Code=value);
            var placeholder=Label(field,"US-ABC234",Vector2.zero,new Vector2(380,60),28,new Color(.45f,.4f,.53f));code.placeholder=placeholder;
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
            mainPage.SetActive(!value);joinPage.SetActive(value);
            if(value){code.SetTextWithoutNotify(controls.Code);controls.DetectNearby();nextNearby=0;}
        }
        void Update()
        {
            if(controls==null)return;
            AnimateSlideshow(Time.unscaledDeltaTime);Crop();
            status.text=controls.Status;
            foreach(var button in buttons)button.interactable=!controls.Busy;
            join.interactable=!controls.Busy&&controls.CodeValid;
            region.text="REGION / "+controls.Region;
            if(joinPage.activeSelf&&Time.unscaledTime>=nextNearby)
            {
                nextNearby=Time.unscaledTime+.5f;nearbyPage=Mathf.Clamp(nearbyPage,0,Mathf.Max(0,(controls.NearbyCount-1)/2));
                nearby.text=controls.NearbyCount==0?"No nearby rooms yet. You can still enter a code.":controls.NearbyCount+" nearby room(s)";
                for(int i=0;i<2;i++){int index=nearbyPage*2+i;nearbyButtons[i].gameObject.SetActive(index<controls.NearbyCount);nearbyButtons[i].GetComponentInChildren<Text>().text=controls.NearbyTitle(index);}
            }
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true&&!controls.Busy)ShowJoin(false);
            if(Keyboard.current?.enterKey.wasPressedThisFrame==true&&joinPage.activeSelf&&!controls.Busy&&controls.CodeValid)controls.JoinCode();
            if(vr)Point();
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
        }
        void LateUpdate()
        {
            if(!vr||!eye||++vrFrames!=4)return;
            var forward=Vector3.ProjectOnPlane(eye.transform.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f)forward=Vector3.forward;
            frame.position=eye.transform.position+forward*3;
            frame.rotation=Quaternion.LookRotation(forward,Vector3.up);
        }
        void Point()
        {
            bool hand=tracked.IsPressed();var ray=new Ray(hand?handPosition.ReadValue<Vector3>():eye.transform.position,hand?handRotation.ReadValue<Quaternion>()*Vector3.forward:eye.transform.forward);
            var plane=new Plane(frame.forward,frame.position);
            if(!plane.Raycast(ray,out float distance)){pointer.gameObject.SetActive(false);return;}
            pointer.gameObject.SetActive(true);pointer.position=ray.GetPoint(distance);
            Button target=null;
            foreach(var button in buttons)
            {
                if(!button.gameObject.activeInHierarchy||!button.interactable)continue;
                var rect=(RectTransform)button.transform;var local=rect.InverseTransformPoint(pointer.position);
                if(rect.rect.Contains(new Vector2(local.x,local.y))){target=button;break;}
            }
            pointer.GetComponent<Image>().color=target?Color.white:new Color(.45f,1,.8f);
            if(target&&trigger.WasPressedThisFrame())target.onClick.Invoke();
        }
        void SetSlide(){if(slides.Length>0)background.texture=slides[slide];Crop();}
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
            button.onClick.AddListener(()=>action());buttons.Add(button);return button;
        }
        void ActionButton(Transform parent,string title,string subtitle,float y,Action action)
        {
            var button=ButtonAt(parent,title,new Vector2(0,y),new Vector2(580,100),action);
            var text=button.GetComponentInChildren<Text>();text.alignment=TextAnchor.UpperLeft;text.fontSize=29;text.rectTransform.anchoredPosition=new Vector2(9,-14);
            Label(button.transform,subtitle,new Vector2(10,-23),new Vector2(550,36),19,new Color(.82f,.8f,.92f));
        }
        void OnDestroy(){trigger?.Dispose();handPosition?.Dispose();handRotation?.Dispose();tracked?.Dispose();}
    }
}
