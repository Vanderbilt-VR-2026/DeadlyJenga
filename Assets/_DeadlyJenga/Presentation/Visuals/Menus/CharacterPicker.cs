using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class CharacterPicker : MonoBehaviour
    {
        static CharacterPicker instance;
        Canvas panel;
        Text nameLabel;
        readonly List<RectTransform> buttons=new List<RectTransform>();
        readonly List<Action> actions=new List<Action>();
        ISessionInfo owner;
        Camera openingCamera;
        bool open,openingHand,openingVr,returnToMenu,selectReady;
        InputAction leftSelect,rightSelect;
        int openingInput;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetInstance()=>instance=null;

        public static bool Showing(ISessionInfo session)=>instance&&instance.isActiveAndEnabled&&instance.open&&ReferenceEquals(instance.owner,session);

        public static void Open(ISessionInfo session)
        {
            if(session?.ActiveCamera==null||HandLooks.Names.Length==0)return;
            if(!instance)instance=FindFirstObjectByType<CharacterPicker>();
            if(!instance)
            {
                var root=new GameObject("Character picker");
                DontDestroyOnLoad(root);
                instance=root.AddComponent<CharacterPicker>();
            }
            if(instance.isActiveAndEnabled)instance.Begin(session);
        }
        void Awake()
        {
            if(instance&&instance!=this){Destroy(gameObject);return;}
            instance=this;
        }
        void Begin(ISessionInfo session)
        {
            if(open&&ReferenceEquals(owner,session))return;
            if(!panel)Build();
            leftSelect.Enable();rightSelect.Enable();
            owner=session;openingCamera=session.ActiveCamera;
            openingHand=session.PlayingHand;openingVr=session.IsVR;
            returnToMenu=session.MenuOpen;open=true;selectReady=false;
            openingInput=InputSignature(session);
            owner.SetMenu(true);
            panel.gameObject.SetActive(true);
            panel.worldCamera=openingVr?openingCamera:null;
            panel.renderMode=openingVr?RenderMode.WorldSpace:RenderMode.ScreenSpaceOverlay;
            var rect=(RectTransform)panel.transform;
            rect.localScale=openingVr?Vector3.one*.0018f:Vector3.one;
            if(openingVr)Anchor();
            else{rect.anchoredPosition=Vector2.zero;rect.localRotation=Quaternion.identity;}
        }
        void Build()
        {
            var root=new GameObject("Hand appearance",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            root.transform.SetParent(transform,false);
            panel=root.GetComponent<Canvas>();panel.sortingOrder=1700;
            var scaler=root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1200,900);
            scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var rect=(RectTransform)root.transform;rect.sizeDelta=new Vector2(520,300);
            var image=root.AddComponent<Image>();
            image.sprite=CandyHudVisuals.Pill;image.type=Image.Type.Sliced;image.color=CandyHudVisuals.Ink;image.raycastTarget=false;
            Label("HAND APPEARANCE",new Vector2(0,107),new Vector2(480,44),27);
            nameLabel=Label("",new Vector2(0,32),new Vector2(332,62),32);
            Add("Previous",new Vector2(-210,32),new Vector2(68,72),"<",()=>Pick(-1));
            Add("Next",new Vector2(210,32),new Vector2(68,72),">",()=>Pick(1));
            Label("Choose a look for your flying hand",new Vector2(0,-34),new Vector2(474,40),20).color=CandyHudVisuals.Muted;
            Add("Recenter",new Vector2(-123,-103),new Vector2(226,62),"Recenter",Anchor);
            Add("Close",new Vector2(123,-103),new Vector2(226,62),"Done",()=>Close(true));
            leftSelect=new InputAction("Hand appearance left select",InputActionType.Button,"<XRController>{LeftHand}/trigger");
            rightSelect=new InputAction("Hand appearance right select",InputActionType.Button,"<XRController>{RightHand}/trigger");
            leftSelect.Enable();rightSelect.Enable();
            panel.gameObject.SetActive(false);
        }
        Text Label(string value,Vector2 position,Vector2 size,int fontSize)
        {
            var rect=new GameObject("Appearance label",typeof(RectTransform),typeof(Text)).GetComponent<RectTransform>();
            rect.SetParent(panel.transform,false);rect.anchoredPosition=position;rect.sizeDelta=size;
            var text=rect.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text=value;text.fontSize=fontSize;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.raycastTarget=false;
            return text;
        }
        void Add(string name,Vector2 position,Vector2 size,string title,Action action)
        {
            var rect=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(panel.transform,false);rect.anchoredPosition=position;rect.sizeDelta=size;
            var image=rect.GetComponent<Image>();image.sprite=CandyHudVisuals.Pill;image.type=Image.Type.Sliced;image.color=CandyHudVisuals.Card;image.raycastTarget=false;
            var text=Label(title,Vector2.zero,size-new Vector2(10,4),26);text.transform.SetParent(rect,false);
            buttons.Add(rect);actions.Add(action);
        }
        void Pick(int step)
        {
            HandLooks.Chosen+=step;
            var audio=CandyAudioEngine.Instance;
            if(audio&&audio.Listener)audio.Play(CandySound.UiClick,audio.Listener.position,.5f,GetInstanceID(),1,true);
        }
        void Anchor()
        {
            if(!openingCamera||!openingVr)return;
            var forward=Vector3.ProjectOnPlane(openingCamera.transform.forward,Vector3.up);
            if(forward.sqrMagnitude<.001f)forward=Vector3.forward;
            forward.Normalize();
            panel.transform.SetPositionAndRotation(openingCamera.transform.position+forward*1.5f,Quaternion.LookRotation(forward,Vector3.up));
        }
        void Close(bool restoreMenu)
        {
            if(!open)return;
            var session=owner;
            open=false;selectReady=false;owner=null;openingCamera=null;
            if(panel)panel.gameObject.SetActive(false);
            if(restoreMenu&&ReferenceEquals(Services.Session,session))session.SetMenu(returnToMenu);
        }
        void LateUpdate()
        {
            if(!open)return;
            bool valid=ReferenceEquals(Services.Session,owner)&&owner.MenuOpen&&owner.ActiveCamera==openingCamera&&
                owner.PlayingHand==openingHand&&owner.IsVR==openingVr&&HandLooks.Names.Length>0&&InputSignature(owner)==openingInput;
            var match=MatchRuntime.Current;
            if(valid&&match?.Frame!=null&&!openingHand)
                foreach(var actor in match.Frame.players)
                    if(actor.id==match.LocalActorId&&(!actor.alive||actor.dying)){valid=false;break;}
            if(!valid){Close(false);return;}
            nameLabel.text=HandLooks.ChosenName;
            foreach(var rect in buttons)rect.GetComponent<Image>().color=CandyHudVisuals.Card;
            bool held=owner.MenuSelectHeld||leftSelect.IsPressed()||rightSelect.IsPressed()||Mouse.current?.leftButton.isPressed==true;
            if(!selectReady){if(!held)selectReady=true;return;}
            Vector2 point;bool pressed;
            if(openingVr)
            {
                if(!DeadlyJenga.Presentation.MenuPointer.OnPanel(openingCamera, (RectTransform)panel.transform, owner.Player?.gameObject.GetComponent<IBlasterControls>()?.LeftHanded==true, out point, out pressed)) return;
            }
            else
            {
                if(Mouse.current==null||!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)panel.transform,Mouse.current.position.ReadValue(),null,out point))return;
                pressed=Mouse.current.leftButton.wasPressedThisFrame;
            }
            for(int i=0;i<buttons.Count;i++)
            {
                if(!buttons[i].rect.Contains(point-buttons[i].anchoredPosition))continue;
                buttons[i].GetComponent<Image>().color=CandyHudVisuals.Mint;
                if(pressed){selectReady=false;actions[i]();}
                return;
            }
        }
        static int InputSignature(ISessionInfo session)
        {
            var active=session.PlayingHand?session.Hand:session.Player;
            int signature=active?.HasHandTracking==true?1:0;
            if(session.Player?.gameObject.GetComponent<IBlasterControls>()?.LeftHanded==true)signature|=2;
            foreach(var device in InputSystem.devices)
                if(device is XRController controller&&controller.isTracked.isPressed&&
                    device.layout.IndexOf("HandInteraction",StringComparison.OrdinalIgnoreCase)<0)
                    signature=unchecked(signature*397^device.deviceId);
            return signature;
        }
        void OnApplicationFocus(bool focused){if(!focused)Close(false);}
        void OnDisable()
        {
            Close(false);
            leftSelect?.Disable();rightSelect?.Disable();
        }
        void OnDestroy()
        {
            leftSelect?.Dispose();rightSelect?.Dispose();
            if(panel)Destroy(panel.gameObject);
            if(instance==this)instance=null;
        }
    }
}

