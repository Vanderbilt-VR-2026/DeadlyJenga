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
    public sealed class GameExperiencePanel : MonoBehaviour
    {
        ISessionInfo owner;
        IGameExperienceControls controls;
        ILobbyControls lobby;
        Canvas canvas;
        RectTransform panel;
        Text profile,status,readiness,mapName,actionHint;
        RawImage mapImage;
        readonly List<RectTransform> buttons=new();
        readonly List<Text> captions=new();
        readonly List<Func<string>> titles=new();
        readonly List<Func<bool>> available=new();
        readonly List<Action> actions=new();
        InputAction leftSelect,rightSelect;
        Camera eye;
        bool open,returnToMenu,openingVr,openingHand,selectReady;
        int openingInput,closedFrame=-1;
        float refreshAt;
        string mapId;

        public static IGameExperienceControls Resolve(ISessionInfo session)
        {
            var host=session as Component;
            if(session is Component&&!host)return null;
            return session as IGameExperienceControls??(host?host.GetComponent<IGameExperienceControls>():null);
        }
        public static bool CanEditRawChoices(ISessionInfo session)=>Resolve(session)?.Experience!=GameExperience.Game;
        public static bool CanEnableDemo(IGameExperienceControls value)
            =>value!=null&&value.Experience!=GameExperience.Demo&&value.CanConfigureExperience&&value.GamePhase!=RoomGamePhase.LoadingGame&&value.GamePhase!=RoomGamePhase.LoadingLobby;
        public static bool CanChooseExperience(IGameExperienceControls value)
        {
            if(value==null||!value.CanConfigureExperience)return false;
            var phase=value.GamePhase;
            return phase==RoomGamePhase.Idle||phase==RoomGamePhase.Choosing||phase==RoomGamePhase.Selected||phase==RoomGamePhase.WaitingReady||phase==RoomGamePhase.Failed;
        }
        public static bool Showing(ISessionInfo session)
        {
            var host=session as Component;if(!host)return false;
            var view=host.GetComponent<GameExperiencePanel>();
            return view&&view.isActiveAndEnabled&&(view.open||view.closedFrame==Time.frameCount);
        }
        public static void Open(ISessionInfo session)
        {
            var host=session as Component;
            if(!host||!host.gameObject.activeInHierarchy||Resolve(session)==null||!session.ActiveCamera||
                CandyComfortPanel.Showing(session)||CharacterPicker.Showing(session)||CandyWildlifePanel.Showing(session))return;
            var view=host.GetComponent<GameExperiencePanel>();
            if(!view)view=host.gameObject.AddComponent<GameExperiencePanel>();
            if(!view.enabled)view.enabled=true;
            if(view.isActiveAndEnabled)view.Begin(session);
        }
        void Begin(ISessionInfo session)
        {
            if(open)return;
            owner=session;controls=Resolve(session);
            lobby=session as ILobbyControls??(session as Component)?.GetComponent<ILobbyControls>();
            if(!canvas)Build();
            leftSelect.Enable();rightSelect.Enable();
            eye=session.ActiveCamera;openingVr=session.IsVR;openingHand=session.PlayingHand;
            openingInput=InputSignature();returnToMenu=session.MenuOpen;selectReady=false;open=true;
            session.SetMenu(true);
            canvas.gameObject.SetActive(true);
            canvas.renderMode=openingVr?RenderMode.WorldSpace:RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera=openingVr?eye:null;
            panel.localScale=openingVr?Vector3.one*.00165f:Vector3.one;
            if(openingVr)Anchor();else{panel.localRotation=Quaternion.identity;panel.anchoredPosition=Vector2.zero;}
            refreshAt=0;Refresh();
        }
        void Build()
        {
            var root=new GameObject("Game experience",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            root.transform.SetParent(transform,false);
            canvas=root.GetComponent<Canvas>();canvas.sortingOrder=1750;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1200,900);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            panel=new GameObject("Experience panel",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();
            panel.SetParent(root.transform,false);panel.sizeDelta=new Vector2(820,760);
            var background=panel.GetComponent<Image>();background.sprite=CandyHudVisuals.Pill;background.type=Image.Type.Sliced;
            background.color=CandyHudVisuals.Ink;background.raycastTarget=false;
            Label("GAME / DEMO",new Vector2(0,335),new Vector2(750,48),32);
            profile=Label("",new Vector2(0,279),new Vector2(750,68),25);
            status=Label("",new Vector2(0,210),new Vector2(750,60),23);
            var preview=new GameObject("Selected map",typeof(RectTransform),typeof(RawImage));
            preview.transform.SetParent(panel,false);mapImage=preview.GetComponent<RawImage>();mapImage.raycastTarget=false;
            mapImage.rectTransform.anchoredPosition=new Vector2(-190,108);mapImage.rectTransform.sizeDelta=new Vector2(330,130);
            mapName=Label("",new Vector2(-190,27),new Vector2(348,32),22);
            readiness=Label("",new Vector2(190,98),new Vector2(348,150),23);
            Add(new Vector2(-190,-30),()=>"GAME"+(controls.Experience==GameExperience.Game?" / selected":""),()=>CanChooseExperience(controls),()=>controls.SetExperience(GameExperience.Game));
            Add(new Vector2(190,-30),()=>"DEMO"+(controls.Experience==GameExperience.Demo?" / selected":""),()=>CanChooseExperience(controls)||CanEnableDemo(controls),()=>controls.SetExperience(GameExperience.Demo));
            Add(new Vector2(-190,-118),()=>"Automatic rounds: "+(controls.AutomaticRounds?"ON":"OFF"),()=>controls.CanConfigureExperience,()=>controls.SetAutomaticRounds(!controls.AutomaticRounds));
            Add(new Vector2(190,-118),()=>DuringRound?"Leave automatic playlist":controls.LocalReady?"Unready / leave playlist":"Ready / join playlist",
                ()=>controls.GamePhase==RoomGamePhase.Selected||controls.GamePhase==RoomGamePhase.WaitingReady||DuringRound&&controls.AutomaticRounds&&controls.LocalReady,()=>controls.SetReady(!controls.LocalReady));
            Add(new Vector2(-190,-206),()=>"Cancel start",()=>controls.CanCancelGameStart,()=>controls.CancelGameStart());
            Add(new Vector2(190,-206),()=>"Retry transition",()=>controls.CanRetryGameTransition,()=>controls.RetryGameTransition());
            actionHint=Label("",new Vector2(0,-353),new Vector2(748,46),19);
            Add(new Vector2(-190,-294),()=>"Recenter panel",()=>openingVr,Anchor);
            Add(new Vector2(190,-294),()=>"Back",()=>true,()=>Close(true));
            leftSelect=new InputAction("Experience left select",InputActionType.Button,"<XRController>{LeftHand}/trigger");
            rightSelect=new InputAction("Experience right select",InputActionType.Button,"<XRController>{RightHand}/trigger");
            root.SetActive(false);
        }
        Text Label(string value,Vector2 position,Vector2 size,int font)
        {
            var go=new GameObject("Experience label",typeof(RectTransform),typeof(Text));go.transform.SetParent(panel,false);
            var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text=value;text.fontSize=font;text.color=Color.white;text.alignment=TextAnchor.MiddleCenter;
            text.supportRichText=false;text.raycastTarget=false;
            text.rectTransform.anchoredPosition=position;text.rectTransform.sizeDelta=size;return text;
        }
        void Add(Vector2 position,Func<string> title,Func<bool> allowed,Action action)
        {
            var go=new GameObject("Experience action",typeof(RectTransform),typeof(Image));go.transform.SetParent(panel,false);
            var rect=go.GetComponent<RectTransform>();rect.anchoredPosition=position;rect.sizeDelta=new Vector2(352,72);
            var image=go.GetComponent<Image>();image.sprite=CandyHudVisuals.Pill;image.type=Image.Type.Sliced;
            image.color=CandyHudVisuals.Card;image.raycastTarget=false;
            var caption=Label("",Vector2.zero,new Vector2(330,68),24);caption.transform.SetParent(rect,false);
            buttons.Add(rect);captions.Add(caption);titles.Add(title);available.Add(allowed);actions.Add(action);
        }
        bool DuringRound=>controls.GamePhase==RoomGamePhase.Playing||controls.GamePhase==RoomGamePhase.Results;
        void Refresh()
        {
            refreshAt=Time.unscaledTime+.2f;
            profile.text=controls.Experience==GameExperience.Game
                ?"GAME / CURATED PROFILE\n"+controls.ProfileId:"DEMO / MANUAL CONTROLS";
            status.text=controls.GameStatus;actionHint.text=controls.GameActionHint;
            string countdown=controls.CountdownSeconds>0?"\nCountdown: "+Mathf.CeilToInt(controls.CountdownSeconds)+" s":"";
            readiness.text=controls.ReadyCount+" / "+controls.RequiredReadyCount+" ready"+
                "\nPopulation "+controls.MinimumPopulation+" - "+controls.MaximumPopulation+countdown;
            for(int i=0;i<captions.Count;i++)captions[i].text=titles[i]();
            var details=lobby as ILobbyDetails;string next=details?.SelectedMapId;
            if(next!=mapId)
            {
                mapId=next;mapImage.texture=string.IsNullOrEmpty(next)?null:MapPreviewImages.Get(next);
                if(mapImage.texture)
                {
                    float scale=Mathf.Min(330f/mapImage.texture.width,130f/mapImage.texture.height);
                    mapImage.rectTransform.sizeDelta=new Vector2(mapImage.texture.width*scale,mapImage.texture.height*scale);
                }
            }
            mapImage.gameObject.SetActive(mapImage.texture);
            mapName.text=lobby?.MapTitle??"Map preview: lobby only";
        }
        void Anchor()
        {
            if(!eye||!openingVr)return;
            var forward=Vector3.ProjectOnPlane(eye.transform.forward,Vector3.up);
            if(forward.sqrMagnitude<.001f)forward=Vector3.forward;forward.Normalize();
            panel.SetPositionAndRotation(eye.transform.position+forward*1.65f,Quaternion.LookRotation(forward,Vector3.up));
        }
        void Close(bool restore)
        {
            if(!open)return;
            var session=owner;open=false;closedFrame=Time.frameCount;selectReady=false;owner=null;controls=null;eye=null;
            leftSelect?.Disable();rightSelect?.Disable();
            if(canvas)canvas.gameObject.SetActive(false);
            if(restore&&ReferenceEquals(Services.Session,session))session.SetMenu(returnToMenu);
        }
        void Update()
        {
            if(!open)return;
            if(!ReferenceEquals(Services.Session,owner)||!owner.MenuOpen||owner.ActiveCamera!=eye||owner.IsVR!=openingVr||
                owner.PlayingHand!=openingHand||!ReferenceEquals(Resolve(owner),controls)||InputSignature()!=openingInput)
            {Close(false);return;}
            if(openingVr)MenuAim.PlaceTablet(panel,eye,owner,owner.Player?.gameObject.GetComponent<IBlasterControls>()?.LeftHanded==true);
            if(Time.unscaledTime>=refreshAt)Refresh();
            bool held=owner.MenuSelectHeld||leftSelect.IsPressed()||rightSelect.IsPressed()||Mouse.current?.leftButton.isPressed==true;
            for(int i=0;i<buttons.Count;i++){buttons[i].GetComponent<Image>().color=available[i]()?CandyHudVisuals.Card:CandyHudVisuals.Ink;captions[i].color=available[i]()?Color.white:CandyHudVisuals.Muted;}
            if(!selectReady){if(!held)selectReady=true;return;}
            Vector2 point;bool pressed;
            if(openingVr)
            {
                Ray ray=new Ray(eye.transform.position,eye.transform.forward);var controller=Controller();bool left=false;
                if(controller!=null)
                {
                    var rig=eye.GetComponentInParent<XROrigin>();var space=rig&&rig.CameraFloorOffsetObject?rig.CameraFloorOffsetObject.transform:null;
                    if(space)
                    {
                        var position=controller.TryGetChildControl<Vector3Control>("pointerPosition")??controller.devicePosition;
                        var rotation=controller.TryGetChildControl<QuaternionControl>("pointerRotation")??controller.deviceRotation;
                        ray=new Ray(space.TransformPoint(position.ReadValue()),space.TransformDirection(rotation.ReadValue()*Vector3.forward));
                        foreach(var usage in controller.usages)left|=usage==CommonUsages.LeftHand;
                    }
                    else controller=null;
                }
                if(Vector3.Dot(ray.direction,panel.forward)<=.01f||!new Plane(panel.forward,panel.position).Raycast(ray,out float distance)||distance>4)return;
                point=panel.InverseTransformPoint(ray.GetPoint(distance));pressed=controller!=null?(left?leftSelect:rightSelect).WasPressedThisFrame():owner.MenuSelectPressed;
            }
            else
            {
                if(Mouse.current==null||!RectTransformUtility.ScreenPointToLocalPointInRectangle(panel,Mouse.current.position.ReadValue(),null,out point))return;
                pressed=Mouse.current.leftButton.wasPressedThisFrame;
            }
            for(int i=0;i<buttons.Count;i++)
            {
                if(!available[i]()||!buttons[i].rect.Contains(point-buttons[i].anchoredPosition))continue;
                buttons[i].GetComponent<Image>().color=CandyHudVisuals.Mint;captions[i].color=CandyHudVisuals.Ink;
                if(pressed){selectReady=false;actions[i]();if(this&&open&&canvas&&ReferenceEquals(Services.Session,owner)&&ReferenceEquals(Resolve(owner),controls))Refresh();}return;
            }
        }
        XRController Controller()
        {
            bool left=owner.Player?.gameObject.GetComponent<IBlasterControls>()?.LeftHanded==true;XRController fallback=null;
            foreach(var device in InputSystem.devices)
            {
                if(!(device is XRController controller)||!controller.isTracked.isPressed||device.layout.IndexOf("HandInteraction",StringComparison.OrdinalIgnoreCase)>=0)continue;
                fallback??=controller;foreach(var usage in controller.usages)if(usage==(left?CommonUsages.LeftHand:CommonUsages.RightHand))return controller;
            }
            return fallback;
        }
        int InputSignature()
        {
            if(owner==null)return 0;
            int signature=owner.Player?.gameObject.GetComponent<IBlasterControls>()?.LeftHanded==true?1:0;
            var active=owner.PlayingHand?owner.Hand:owner.Player;if(active?.HasHandTracking==true)signature|=2;
            foreach(var device in InputSystem.devices)
                if(device is XRController controller&&controller.isTracked.isPressed&&device.layout.IndexOf("HandInteraction",StringComparison.OrdinalIgnoreCase)<0)
                    signature=unchecked(signature*397^device.deviceId);
            return signature;
        }
        void OnApplicationFocus(bool focused){if(!focused)Close(false);}
        void OnDisable(){Close(false);leftSelect?.Disable();rightSelect?.Disable();}
        void OnDestroy(){Close(false);leftSelect?.Dispose();rightSelect?.Dispose();if(canvas)Destroy(canvas.gameObject);}
    }
}

