using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    public sealed class CandyComfortPanel : MonoBehaviour
    {
        ISessionInfo session;
        IBlasterControls weaponHand;
        Canvas canvas;
        RectTransform panel;
        Text mode,bindings,feedback;
        readonly List<Button> buttons=new List<Button>();
        readonly List<Action> actions=new List<Action>();
        InputAction rightSelect,leftSelect;
        bool open,returnToMenu,readyToSelect,wasVr;
        Camera eye;
        float nextSample,feedbackUntil;
        int controllerCount;
        bool singleLeft;
        ISurvivorSettings Settings=>session?.Player as ISurvivorSettings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()
        {
            GameEvents.SessionStarted+=Attach;
            GameEvents.LobbyStarted+=Attach;
        }
        static void Attach(GameObject root)
        {
            if(!root.GetComponent<CandyComfortPanel>())
                root.AddComponent<CandyComfortPanel>().session=root.GetComponent<ISessionInfo>();
        }
        public static bool Showing(ISessionInfo owner)=>(owner as Component)?.GetComponent<CandyComfortPanel>()?.open==true;
        public static void Open(ISessionInfo owner)
        {
            var view=(owner as Component)?.GetComponent<CandyComfortPanel>();
            if(!view)return;
            if(!view.canvas)view.Build();
            view.returnToMenu=owner.MenuOpen;
            view.open=true;
            view.readyToSelect=false;
            view.nextSample=0;
            owner.SetMenu(true);
            view.Configure(true);
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
            panel.SetParent(root.transform,false);panel.sizeDelta=new Vector2(820,820);
            var back=panel.GetComponent<Image>();back.sprite=CandyHudVisuals.Pill;back.type=Image.Type.Sliced;back.color=CandyHudVisuals.Ink;back.raycastTarget=false;
            Label("CONTROLS & COMFORT",new Vector2(0,361),new Vector2(750,54),31);
            mode=Label("",new Vector2(0,306),new Vector2(750,46),22);
            Add(new Vector2(-190,225),()=>HandTitle(),()=>weaponHand?.SetLeftHanded(!weaponHand.LeftHanded));
            Add(new Vector2(190,225),()=>"Movement basis\n"+(Settings?.MovementLabel??"Unavailable"),()=>Settings?.CycleMovement());
            Add(new Vector2(-190,133),()=>"Look turning\n"+(Settings?.TurnLabel??"Unavailable"),()=>Settings?.CycleTurn());
            Add(new Vector2(190,133),()=>"Smooth speed\n"+(Settings?.SmoothTurnSpeed.ToString("0")??"--")+" deg / sec",()=>Settings?.CycleSmoothTurnSpeed());
            Add(new Vector2(-190,41),()=>"Snap angle\n"+(Settings?.SnapAngle.ToString("0")??"--")+" degrees",()=>Settings?.CycleSnapAngle());
            Add(new Vector2(190,41),()=>"Play style\n"+(Settings?.StandingPlay==true?"Standing / physical turn":"Seated"),()=>{if(Settings?.StandingPlay==true)Settings.UseStationaryPreset();else Settings?.UseStandingPreset();});
            Add(new Vector2(-190,-51),()=>"Recalibrate\nseated eye height",()=>Settings?.RecalibrateSeatedHeight());
            Add(new Vector2(190,-51),()=>"Recenter\nthis panel",()=>Configure(true));
            bindings=Label("",new Vector2(0,-170),new Vector2(748,142),21);
            feedback=Label("",new Vector2(0,-277),new Vector2(748,40),20);
            Add(new Vector2(-190,-347),()=>"Back",Close);
            Add(new Vector2(190,-347),()=>"Resume",()=>{Close();session.SetMenu(false);});
            rightSelect=new InputAction("Comfort right select",InputActionType.Button,"<XRController>{RightHand}/trigger");
            leftSelect=new InputAction("Comfort left select",InputActionType.Button,"<XRController>{LeftHand}/trigger");
            rightSelect.Enable();leftSelect.Enable();
        }
        readonly List<Func<string>> titles=new List<Func<string>>();
        string HandTitle()=>controllerCount==1
            ?"Active hand\n"+(singleLeft?"LEFT":"RIGHT")+" controller"
            :"Dominant hand\n"+(weaponHand?.LeftHanded==true?"LEFT":"RIGHT");
        void Configure(bool anchor)
        {
            if(!canvas||session==null)return;
            eye=session.ActiveCamera;wasVr=session.IsVR;
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
            if(session==null||Services.Session!=session||!session.MenuOpen){open=false;canvas.gameObject.SetActive(false);return;}
            if(eye!=session.ActiveCamera||wasVr!=session.IsVR)Configure(true);
            canvas.gameObject.SetActive(true);
            if(Time.unscaledTime>=nextSample)
            {
                nextSample=Time.unscaledTime+.2f;
                var settings=Settings;weaponHand=session.Player?.gameObject.GetComponent<IBlasterControls>();
                controllerCount=0;
                foreach(var device in InputSystem.devices)
                    if(PhysicalController(device))
                    {
                        controllerCount++;
                        singleLeft=HasUsage(device,CommonUsages.LeftHand);
                    }
                string input=!wasVr?"Keyboard + mouse":controllerCount==1?"One "+(singleLeft?"LEFT":"RIGHT")+" controller"+(session.Player?.HasHandTracking==true?" + tracked hand":""):
                    controllerCount>1?"VR controllers":session.Player?.HasHandTracking==true?"Hand tracking":"VR / waiting for tracking";
                mode.text=input+"  ·  "+(session.PlayingHand?"Survivor preferences while flying":"Survivor");
                bindings.text=!wasVr?"WASD moves; mouse looks; Space jumps / holds to climb.\nTap Left Shift toggles sprint on ground. V toggles equipment.\nR switches mode; Delete or the menu drops equipment. Esc opens menu.\nVR movement and turning preferences are saved here.":
                    (controllerCount==1?"Move stick: move / click sprint; grip + stick: look.\n":settings?.ControlDescription+"; click movement stick: sprint.\n")+
                    "Menu: Y / native; right-only Grip+B. Equipment: Grip+Y / B.\nSingle mode: Grip+X/A. Emotes: Grip+stick click. Drop: Delete/menu.\nTurn your head to look; aim + trigger or tracked pinch selects.";
                for(int i=0;i<buttons.Count;i++)
                {
                    buttons[i].GetComponentInChildren<Text>().text=titles[i]();
                    buttons[i].interactable=i>=7||settings!=null;
                }
                buttons[0].interactable=weaponHand!=null&&controllerCount!=1;
                buttons[6].interactable=wasVr&&settings!=null&&!settings.StandingPlay;
                feedback.text=Time.unscaledTime<feedbackUntil?"Local preference applied.":"";
            }
            if(!readyToSelect)
            {
                if(!session.MenuSelectHeld&&!rightSelect.IsPressed()&&!leftSelect.IsPressed()&&Mouse.current?.leftButton.isPressed!=true)readyToSelect=true;
                return;
            }
            bool pressed=false;Vector2 point;
            if(wasVr)
            {
                if(!eye)return;
                var ray=new Ray(eye.transform.position,eye.transform.forward);
                XRController aimed=null;
                var origin=eye.GetComponentInParent<XROrigin>();
                var space=origin&&origin.CameraFloorOffsetObject?origin.CameraFloorOffsetObject.transform:null;
                if(space)
                {
                    bool left=weaponHand?.LeftHanded==true;
                    foreach(var device in InputSystem.devices)
                        if(PhysicalController(device)&&HasUsage(device,left?CommonUsages.LeftHand:CommonUsages.RightHand)){aimed=(XRController)device;break;}
                    if(aimed==null)foreach(var device in InputSystem.devices)if(PhysicalController(device)){aimed=(XRController)device;break;}
                    if(aimed!=null)
                    {
                        var pos=aimed.TryGetChildControl<Vector3Control>("pointerPosition")??aimed.devicePosition;
                        var rot=aimed.TryGetChildControl<QuaternionControl>("pointerRotation")??aimed.deviceRotation;
                        ray=new Ray(space.TransformPoint(pos.ReadValue()),space.TransformDirection(rot.ReadValue()*Vector3.forward));
                    }
                }
                if(Vector3.Dot(ray.direction,panel.forward)<=.01f||!new Plane(panel.forward,panel.position).Raycast(ray,out float distance)||distance>4)return;
                point=panel.InverseTransformPoint(ray.GetPoint(distance));
                pressed=aimed!=null?(HasUsage(aimed,CommonUsages.LeftHand)?leftSelect:rightSelect).WasPressedThisFrame():session.MenuSelectPressed;
            }
            else
            {
                if(Mouse.current==null||!RectTransformUtility.ScreenPointToLocalPointInRectangle(panel,Mouse.current.position.ReadValue(),null,out point))return;
                pressed=Mouse.current.leftButton.wasPressedThisFrame;
            }
            for(int i=0;i<buttons.Count;i++)
            {
                var rect=(RectTransform)buttons[i].transform;
                var local=point-rect.anchoredPosition;
                bool hover=rect.rect.Contains(local)&&buttons[i].interactable;
                buttons[i].image.color=hover?CandyHudVisuals.Mint:buttons[i].interactable?CandyHudVisuals.Panel:new Color(.18f,.14f,.24f);
                if(pressed&&hover)
                {
                    actions[i]();readyToSelect=false;nextSample=0;
                    if(i<7)feedbackUntil=Time.unscaledTime+1.5f;
                    return;
                }
            }
        }
        static bool PhysicalController(InputDevice device)=>device is XRController controller&&controller.isTracked.isPressed&&device.layout.IndexOf("HandInteraction",StringComparison.OrdinalIgnoreCase)<0;
        static bool HasUsage(InputDevice device,UnityEngine.InputSystem.Utilities.InternedString usage)
        {foreach(var value in device.usages)if(value==usage)return true;return false;}
        void Close()
        {
            if(!open)return;
            open=false;
            if(canvas)canvas.gameObject.SetActive(false);
            if(Services.Session==session)session.SetMenu(returnToMenu);
        }
        Text Label(string value,Vector2 position,Vector2 size,int fontSize)
        {
            var rect=new GameObject("Comfort label",typeof(RectTransform),typeof(Text)).GetComponent<RectTransform>();
            rect.SetParent(panel,false);rect.anchoredPosition=position;rect.sizeDelta=size;
            var label=rect.GetComponent<Text>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=fontSize;label.alignment=TextAnchor.MiddleCenter;label.color=Color.white;label.raycastTarget=false;label.text=value;
            return label;
        }
        void Add(Vector2 position,Func<string> title,Action action)
        {
            var rect=new GameObject("Comfort control",typeof(RectTransform),typeof(Image),typeof(Button)).GetComponent<RectTransform>();
            rect.SetParent(panel,false);rect.anchoredPosition=position;rect.sizeDelta=new Vector2(354,78);
            var button=rect.GetComponent<Button>();button.image.sprite=CandyHudVisuals.Pill;button.image.type=Image.Type.Sliced;button.image.color=CandyHudVisuals.Panel;button.image.raycastTarget=false;
            var label=Label(title(),Vector2.zero,new Vector2(326,70),24);label.transform.SetParent(rect,false);
            buttons.Add(button);titles.Add(title);actions.Add(action);
        }
        void OnDestroy()
        {
            rightSelect?.Dispose();leftSelect?.Dispose();
            if(canvas)Destroy(canvas.gameObject);
        }
    }
}

