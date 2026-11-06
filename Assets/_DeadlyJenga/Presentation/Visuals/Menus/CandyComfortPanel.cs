using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    public sealed class CandyComfortPanel : MonoBehaviour
    {
        ISessionInfo session;
        IBlasterControls weaponHand;
        Canvas canvas;
        RectTransform panel;
        const string Preference = "CandyComfort.";
        static readonly string[] TurnModes = { "Smooth", "Snap", "PhysicalOnly" };
        static readonly float[] SnapAngles = { 15, 30, 45, 60 };
        static readonly float[] TurnSpeeds = { 60, 90, 120, 180, 270, 360 };
        sealed class Control
        {
            public Button button;
            public Text value, label;
            public Func<string> title;
            public Func<bool> enabled;
            public Action select;
            public bool primary;
        }
        readonly List<Control> controls = new List<Control>();
        bool home,homeVr,open,returnToMenu,readyToSelect,wasVr;
        bool initialLeft,draftLeft;
        Camera homeCamera,eye;
        InputAction rightSelect,leftSelect;
        ISurvivorSettings settings;
        MatchRules initial,draft;
        bool PreferredLeft=>weaponHand?.LeftHanded==true;
        bool RulesChanged=>initial!=null&&(draft.turn!=initial.turn||draft.snapAngle!=initial.snapAngle||
            draft.smoothDegrees!=initial.smoothDegrees||draft.movement!=initial.movement);
        bool Changed=>draftLeft!=initialLeft||RulesChanged;

        public static bool HomeShowing(GameObject owner)=>owner&&owner.GetComponent<CandyComfortPanel>()?.open==true;
        public static void OpenHome(GameObject owner,Camera camera,bool vr)
        {
            if(!owner||!camera)return;
            var view=owner.GetComponent<CandyComfortPanel>()??owner.AddComponent<CandyComfortPanel>();
            view.home=true;view.homeCamera=camera;view.homeVr=vr;
            view.weaponHand=owner.GetComponent<IBlasterControls>();
            if(!view.open)view.Begin();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()
        {
            GameEvents.SessionStarted+=Attach;
            GameEvents.LobbyStarted+=Attach;
        }
        static void Attach(GameObject root)
        {
            if(root.GetComponent<CandyComfortPanel>())return;
            var view=root.AddComponent<CandyComfortPanel>();
            view.session=root.GetComponent<ISessionInfo>();
            ApplyPreferences(view.session?.Player as ISurvivorSettings);
        }
        public static bool Showing(ISessionInfo owner)=>(owner as Component)?.GetComponent<CandyComfortPanel>()?.open==true;
        public static void Open(ISessionInfo owner)
        {
            var view=(owner as Component)?.GetComponent<CandyComfortPanel>();
            if(!view||view.open)return;
            view.home=false;
            view.weaponHand=owner.Player?.gameObject.GetComponent<IBlasterControls>();
            view.returnToMenu=owner.MenuOpen;
            owner.SetMenu(true);
            view.Begin();
        }
        void Begin()
        {
            settings=home?null:session.Player as ISurvivorSettings;
            initial=settings?.CurrentRules;
            draft=Copy(initial);
            initialLeft=draftLeft=PreferredLeft;
            if(!canvas)Build();
            open=true;
            readyToSelect=false;
            Configure(true);
            canvas.gameObject.SetActive(true);
            Refresh(false,default);
        }
        static MatchRules Copy(MatchRules source)=>source==null?null:JsonUtility.FromJson<MatchRules>(JsonUtility.ToJson(source));
        static void ApplyPreferences(ISurvivorSettings target)
        {
            if(target==null||!PlayerPrefs.HasKey(Preference+"Turn")&&!PlayerPrefs.HasKey(Preference+"Snap")&&
                !PlayerPrefs.HasKey(Preference+"Speed")&&!PlayerPrefs.HasKey(Preference+"Movement"))return;
            var rules=Copy(target.CurrentRules);
            if(rules==null)return;
            rules.playStyle="";
            rules.turn=PlayerPrefs.GetString(Preference+"Turn",rules.turn);
            rules.snapAngle=PlayerPrefs.GetFloat(Preference+"Snap",rules.snapAngle);
            rules.smoothDegrees=PlayerPrefs.GetFloat(Preference+"Speed",rules.smoothDegrees);
            rules.movement=PlayerPrefs.GetString(Preference+"Movement",rules.movement);
            target.ApplyMatchRules(rules);
        }
        void Save()
        {
            if(!Changed)return;
            if(draftLeft!=initialLeft)weaponHand?.SetLeftHanded(draftLeft);
            if(RulesChanged)
            {
                if(draft.turn!=initial.turn)PlayerPrefs.SetString(Preference+"Turn",draft.turn);
                if(draft.snapAngle!=initial.snapAngle)PlayerPrefs.SetFloat(Preference+"Snap",draft.snapAngle);
                if(draft.smoothDegrees!=initial.smoothDegrees)PlayerPrefs.SetFloat(Preference+"Speed",draft.smoothDegrees);
                if(draft.movement!=initial.movement)PlayerPrefs.SetString(Preference+"Movement",draft.movement);
                ApplyPreferences(settings);
            }
            PlayerPrefs.Save();
            Close(true);
        }
        void Build()
        {
            var root=new GameObject("Controls and comfort",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            root.transform.SetParent(transform,false);
            canvas=root.GetComponent<Canvas>();canvas.sortingOrder=1600;
            var scaler=root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1200,900);
            scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            panel=new GameObject("Comfort panel",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();
            panel.SetParent(root.transform,false);panel.sizeDelta=new Vector2(940,720);
            var back=panel.GetComponent<Image>();back.sprite=CandyHudVisuals.Pill;back.type=Image.Type.Sliced;back.color=CandyHudVisuals.Ink;back.raycastTarget=false;
            Label("Controls & Comfort",new Vector2(0,291),new Vector2(820,64),42).fontStyle=FontStyle.Bold;
            AddRow("Dominant Hand",200,()=>draftLeft?"Left":"Right",()=>draftLeft=!draftLeft,()=>weaponHand!=null);
            AddRow("Turn Mode",110,()=>draft?.turn=="PhysicalOnly"?"Physical Only":draft?.turn,
                ()=>draft.turn=MatchOptions.Next(TurnModes,draft.turn),()=>draft!=null);
            AddRow("Snap Angle",20,()=>draft==null?null:$"{draft.snapAngle:0} Degrees",
                ()=>draft.snapAngle=Next(SnapAngles,draft.snapAngle),()=>draft?.turn=="Snap");
            AddRow("Turn Speed",-70,()=>draft==null?null:$"{draft.smoothDegrees:0} deg/s",
                ()=>draft.smoothDegrees=Next(TurnSpeeds,draft.smoothDegrees),()=>draft?.turn=="Smooth");
            AddRow("Movement",-160,()=>draft?.movement=="BodyRelative"?"Body Relative":draft==null?null:"Head Relative",
                ()=>draft.movement=MatchOptions.Next(MatchOptions.Movements,draft.movement),()=>draft!=null);
            Add(new Vector2(-205,-271),new Vector2(370,70),()=>"Back",()=>Close(false),()=>true);
            Add(new Vector2(205,-271),new Vector2(370,70),()=>"Save & Exit",Save,()=>Changed,true);
            rightSelect=new InputAction("Comfort right select",InputActionType.Button,"<XRController>{RightHand}/trigger");
            leftSelect=new InputAction("Comfort left select",InputActionType.Button,"<XRController>{LeftHand}/trigger");
            rightSelect.Enable();leftSelect.Enable();
        }
        static float Next(float[] options,float current)
        {
            foreach(float value in options)if(value>current)return value;
            return options[0];
        }
        void AddRow(string name,float y,Func<string> value,Action select,Func<bool> enabled)
        {
            var label=Label(name,new Vector2(-190,y),new Vector2(425,64),30);
            label.alignment=TextAnchor.MiddleLeft;
            Add(new Vector2(205,y),new Vector2(370,64),value,select,enabled).label=label;
        }
        void Configure(bool anchor)
        {
            if(!canvas||!home&&session==null)return;
            eye=home?homeCamera:session.ActiveCamera;wasVr=home?homeVr:session.IsVR;
            canvas.renderMode=wasVr?RenderMode.WorldSpace:RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera=wasVr?eye:null;
            var root=(RectTransform)canvas.transform;
            if(wasVr&&eye)
            {
                root.sizeDelta=new Vector2(1200,900);root.localScale=Vector3.one;
                panel.localScale=Vector3.one*.00165f;
                if(anchor)
                {
                    var forward=Vector3.ProjectOnPlane(eye.transform.forward,Vector3.up);
                    if(forward.sqrMagnitude<.001f)forward=Vector3.forward;
                    forward.Normalize();
                    panel.SetPositionAndRotation(eye.transform.position+forward*1.65f,Quaternion.LookRotation(forward,Vector3.up));
                }
            }
            else
            {
                panel.localScale=Vector3.one;panel.localRotation=Quaternion.identity;panel.anchoredPosition=Vector2.zero;
            }
        }
        void Update()
        {
            if(!open)return;
            if(!home&&(session==null||Services.Session!=session||!session.MenuOpen)||home&&!homeCamera){open=false;canvas.gameObject.SetActive(false);return;}
            if(!home&&(eye!=session.ActiveCamera||wasVr!=session.IsVR))Configure(true);
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true){Close(false);return;}
            if(!readyToSelect)
            {
                if(session?.MenuSelectHeld!=true&&!rightSelect.IsPressed()&&!leftSelect.IsPressed()&&Mouse.current?.leftButton.isPressed!=true)readyToSelect=true;
                return;
            }
            bool pressed=false;
            Vector2 point=default;
            bool onPanel;
            if(wasVr)
                onPanel=MenuPointer.OnPanel(eye,panel,PreferredLeft,out point,out pressed);
            else
            {
                onPanel=Mouse.current!=null&&RectTransformUtility.ScreenPointToLocalPointInRectangle(panel,Mouse.current.position.ReadValue(),null,out point);
                pressed=onPanel&&Mouse.current.leftButton.wasPressedThisFrame;
            }
            var selected=Refresh(onPanel,point);
            if(pressed&&selected!=null)
            {
                selected.select();
                readyToSelect=false;
                if(open)Refresh(onPanel,point);
            }
        }
        Control Refresh(bool onPanel,Vector2 point)
        {
            Control hovered=null;
            foreach(var control in controls)
            {
                var rect=(RectTransform)control.button.transform;
                bool enabled=control.enabled();
                bool hover=enabled&&onPanel&&rect.rect.Contains(point-rect.anchoredPosition);
                if(hover)hovered=control;
                control.button.interactable=enabled;
                control.button.image.color=!enabled?CandyHudVisuals.Panel:hover||control.primary?CandyHudVisuals.Mint:CandyHudVisuals.Card;
                control.value.text=control.title()??"—";
                control.value.color=!enabled?CandyHudVisuals.Muted:hover||control.primary?CandyHudVisuals.Ink:Color.white;
                if(control.label)control.label.color=enabled?Color.white:CandyHudVisuals.Muted;
            }
            return hovered;
        }
        void Close(bool saved)
        {
            if(!open)return;
            open=false;
            canvas.gameObject.SetActive(false);
            if(!home&&Services.Session==session)session.SetMenu(!saved&&returnToMenu);
        }
        Text Label(string value,Vector2 position,Vector2 size,int fontSize)
        {
            var rect=new GameObject("Comfort label",typeof(RectTransform),typeof(Text)).GetComponent<RectTransform>();
            rect.SetParent(panel,false);rect.anchoredPosition=position;rect.sizeDelta=size;
            var label=rect.GetComponent<Text>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=fontSize;label.alignment=TextAnchor.MiddleCenter;label.color=Color.white;label.raycastTarget=false;label.text=value;
            return label;
        }
        Control Add(Vector2 position,Vector2 size,Func<string> title,Action action,Func<bool> enabled,bool primary=false)
        {
            var rect=new GameObject("Comfort control",typeof(RectTransform),typeof(Image),typeof(Button)).GetComponent<RectTransform>();
            rect.SetParent(panel,false);rect.anchoredPosition=position;rect.sizeDelta=size;
            var button=rect.GetComponent<Button>();button.image.sprite=CandyHudVisuals.Pill;button.image.type=Image.Type.Sliced;button.image.raycastTarget=false;
            var label=Label("",Vector2.zero,size-new Vector2(20,6),28);label.transform.SetParent(rect,false);
            var control=new Control{button=button,value=label,title=title,select=action,enabled=enabled,primary=primary};
            controls.Add(control);
            return control;
        }
        void OnDestroy()
        {
            rightSelect?.Dispose();leftSelect?.Dispose();
            if(canvas)Destroy(canvas.gameObject);
        }
    }
}

