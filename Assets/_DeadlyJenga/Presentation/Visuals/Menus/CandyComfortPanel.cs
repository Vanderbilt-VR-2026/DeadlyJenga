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
        Text mode,bindings,feedback,leftRoles,rightRoles;
        bool home; Camera homeCamera; bool homeVr;
        readonly List<Button> buttons=new List<Button>();
        readonly List<Action> actions=new List<Action>();
        InputAction rightSelect,leftSelect;
        bool open,returnToMenu,readyToSelect,wasVr;
        Camera eye;
        float nextSample,feedbackUntil;
        int controllerCount;
        bool singleLeft;
        ISurvivorSettings Settings=>session?.Player as ISurvivorSettings;
        bool PreferredLeft=>weaponHand?.LeftHanded==true;
        public static bool HomeShowing(GameObject owner)=>owner&&owner.GetComponent<CandyComfortPanel>()?.open==true;
        public static void OpenHome(GameObject owner,Camera camera,bool vr)
        {
            if(!owner||!camera)return;
            var view=owner.GetComponent<CandyComfortPanel>()??owner.AddComponent<CandyComfortPanel>();
            view.home=true;view.homeCamera=camera;view.homeVr=vr;
            view.weaponHand=owner.GetComponent<IBlasterControls>();
            if(!view.canvas)view.Build();
            view.open=true;view.readyToSelect=false;view.nextSample=0;view.Configure(true);
        }

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
            view.home=false;view.weaponHand=owner.Player?.gameObject.GetComponent<IBlasterControls>();
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
            panel.SetParent(root.transform,false);panel.sizeDelta=new Vector2(1040,880);
            var back=panel.GetComponent<Image>();back.sprite=CandyHudVisuals.Pill;back.type=Image.Type.Sliced;back.color=CandyHudVisuals.Ink;back.raycastTarget=false;
            Label("CONTROLS / DOMINANT HAND",new Vector2(0,390),new Vector2(980,48),32);
            mode=Label("",new Vector2(0,345),new Vector2(980,38),21);
            Add(new Vector2(-250,270),()=>"LEFT DOMINANT"+(PreferredLeft?" / SELECTED":""),()=>SetDominant(true));
            Add(new Vector2(250,270),()=>"RIGHT DOMINANT"+(!PreferredLeft?" / SELECTED":""),()=>SetDominant(false));
            ControllerPicture(true,new Vector2(-425,157));ControllerPicture(false,new Vector2(425,157));
            leftRoles=Label("",new Vector2(-190,157),new Vector2(340,112),23);
            rightRoles=Label("",new Vector2(190,157),new Vector2(340,112),23);
            Add(new Vector2(-250,45),()=>"Look turning / "+(Settings?.TurnLabel??"Set in lobby"),()=>Settings?.CycleTurn());
            Add(new Vector2(250,45),()=>"Look speed / "+(Settings?.SmoothTurnSpeed.ToString("0")??"--")+" deg/s",()=>Settings?.CycleSmoothTurnSpeed());
            Add(new Vector2(-250,-40),()=>"Movement / "+(Settings?.MovementLabel??"Set in lobby"),()=>Settings?.CycleMovement());
            Add(new Vector2(250,-40),()=>"Play style / "+(Settings?.StandingPlay==true?"Standing":"Seated"),()=>{if(Settings?.StandingPlay==true)Settings.UseStationaryPreset();else Settings?.UseStandingPreset();});
            Add(new Vector2(-250,-125),()=>"Snap angle / "+(Settings?.SnapAngle.ToString("0")??"--")+" degrees",()=>Settings?.CycleSnapAngle());
            Add(new Vector2(250,-125),()=>"Recalibrate seated height",()=>Settings?.RecalibrateSeatedHeight());
            Add(new Vector2(-250,-210),()=>"Recenter this panel",()=>Configure(true));
            Add(new Vector2(250,-210),()=>"Joystick look / enable",()=>EnableJoystickLook());
            bindings=Label("",new Vector2(0,-287),new Vector2(980,74),19);
            feedback=Label("",new Vector2(0,-340),new Vector2(980,30),20);
            Add(new Vector2(-250,-390),()=>home?"Back to Home":"Back",Close);
            Add(new Vector2(250,-390),()=>home?"Done":"Resume",()=>{Close();session?.SetMenu(false);});
            rightSelect=new InputAction("Comfort right select",InputActionType.Button,"<XRController>{RightHand}/trigger");
            leftSelect=new InputAction("Comfort left select",InputActionType.Button,"<XRController>{LeftHand}/trigger");
            rightSelect.AddBinding("<HandInteraction>{RightHand}/pinchValue");leftSelect.AddBinding("<HandInteraction>{LeftHand}/pinchValue");
            rightSelect.Enable();leftSelect.Enable();
        }
        readonly List<Func<string>> titles=new List<Func<string>>();
        void SetDominant(bool left)
        {
            weaponHand?.SetLeftHanded(left);
            nextSample=0;feedbackUntil=Time.unscaledTime+2;
        }
        void EnableJoystickLook()
        {
            var settings=Settings;if(settings==null)return;
            var rules=JsonUtility.FromJson<MatchRules>(JsonUtility.ToJson(settings.CurrentRules));
            rules.turn="Smooth";settings.ApplyMatchRules(rules);
        }
        void ControllerPicture(bool left,Vector2 position)
        {
            var image=new GameObject(left?"Left controller":"Right controller",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(panel,false);image.rectTransform.anchoredPosition=position;
            image.rectTransform.sizeDelta=new Vector2(86,112);image.texture=CandyHudVisuals.ControllerDiagram(0,left);image.raycastTarget=false;
        }
        string Roles(bool left)
        {
            string side=left?"LEFT":"RIGHT";
            return side+(left==PreferredLeft?" / DOMINANT\nStick: LOOK\n"+(left?"X":"A")+": JUMP / HOLD CLIMB\nTrigger: FIRE":" / NON-DOMINANT\nStick: MOVE\nStick click: SPRINT");
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
            canvas.gameObject.SetActive(true);
            if(Time.unscaledTime>=nextSample)
            {
                nextSample=Time.unscaledTime+.2f;
                var settings=Settings;if(!home)weaponHand=session.Player?.gameObject.GetComponent<IBlasterControls>();
                controllerCount=0;
                foreach(var device in InputSystem.devices)
                    if(PhysicalController(device))
                    {
                        controllerCount++;
                        singleLeft=HasUsage(device,CommonUsages.LeftHand);
                    }
                string input=!wasVr?"Keyboard + mouse":controllerCount==1?"One "+(singleLeft?"LEFT":"RIGHT")+" controller"+(session?.Player?.HasHandTracking==true?" + tracked hand":""):
                    controllerCount>1?"VR controllers":session?.Player?.HasHandTracking==true?"Hand tracking":"VR / waiting for tracking";
                mode.text="Preferred dominant hand: "+(PreferredLeft?"LEFT":"RIGHT")+" / "+input;
                leftRoles.text=Roles(true);rightRoles.text=Roles(false);
                bindings.text=home?"Preference saves for Game and Demo. Look speed and comfort are also available in Lobby / Pause.":
                    controllerCount==1?"One controller: stick moves; grip + stick looks. Your preferred hand stays saved.\nJump: controller primary button. Fire: trigger. Add both controllers for the layout above.":
                    settings?.TurnLabel=="PhysicalOnly"?"JOYSTICK LOOK IS OFF: physical turning selected. Choose Joystick look / enable.\nTurn your head freely. Menu: bare left Y / native; right-only Grip + B.":
                    "Dominant stick looks left/right AND up/down. Non-dominant stick moves.\nJump: dominant X / A. Fire equipped weapon: dominant trigger. Menu: bare left Y / native.";
                for(int i=0;i<buttons.Count;i++)
                {
                    buttons[i].GetComponentInChildren<Text>().text=titles[i]();
                    buttons[i].gameObject.SetActive(!home||i<2||i>=10);
                    buttons[i].interactable=i<2?weaponHand!=null:i>=8||settings!=null;
                }
                buttons[7].interactable=wasVr&&settings!=null&&!settings.StandingPlay;
                feedback.text=Time.unscaledTime<feedbackUntil?"Preference applied locally.":weaponHand==null?"Hand preference provider unavailable.":"Select either dominant hand. This remains available in every game mode.";
            }
            if(!readyToSelect)
            {
                if(session?.MenuSelectHeld!=true&&!rightSelect.IsPressed()&&!leftSelect.IsPressed()&&Mouse.current?.leftButton.isPressed!=true)readyToSelect=true;
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
                pressed=aimed!=null?(HasUsage(aimed,CommonUsages.LeftHand)?leftSelect:rightSelect).WasPressedThisFrame():session?.MenuSelectPressed==true||rightSelect.WasPressedThisFrame()||leftSelect.WasPressedThisFrame();
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
                bool selected=i<2&&(i==0)==PreferredLeft;
                buttons[i].image.color=hover||selected?CandyHudVisuals.Mint:buttons[i].interactable?CandyHudVisuals.Panel:new Color(.18f,.14f,.24f);
                buttons[i].GetComponentInChildren<Text>().color=hover||selected?CandyHudVisuals.Ink:Color.white;
                if(pressed&&hover)
                {
                    actions[i]();readyToSelect=false;nextSample=0;
                    if(i<8)feedbackUntil=Time.unscaledTime+1.5f;
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
            if(!home&&Services.Session==session)session.SetMenu(returnToMenu);
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
            rect.SetParent(panel,false);rect.anchoredPosition=position;rect.sizeDelta=new Vector2(470,76);
            var button=rect.GetComponent<Button>();button.image.sprite=CandyHudVisuals.Pill;button.image.type=Image.Type.Sliced;button.image.color=CandyHudVisuals.Panel;button.image.raycastTarget=false;
            var label=Label(title(),Vector2.zero,new Vector2(448,68),24);label.transform.SetParent(rect,false);
            buttons.Add(button);titles.Add(title);actions.Add(action);
        }
        void OnDestroy()
        {
            rightSelect?.Dispose();leftSelect?.Dispose();
            if(canvas)Destroy(canvas.gameObject);
        }
    }
}

