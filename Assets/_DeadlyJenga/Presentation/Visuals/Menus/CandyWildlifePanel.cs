using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    public sealed class CandyWildlifePanel : MonoBehaviour
    {
        ISessionInfo session;
        bool open,tame,selectReady;
        Image aimDot;
        bool previousHeld;
        int pointerIdentity=int.MinValue;
        Vector3 openingForward=Vector3.forward;
        Canvas canvas;
        RectTransform panel;
        Text status;
        readonly System.Collections.Generic.List<Button> buttons=new System.Collections.Generic.List<Button>();
        readonly System.Collections.Generic.List<System.Action> actions=new System.Collections.Generic.List<System.Action>();
        IWildlifeControls Controls => GetComponent<IWildlifeControls>();
        public bool IsOpen => open;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen(){GameEvents.SessionStarted+=Attach;GameEvents.LobbyStarted+=Attach;}
        static void Attach(GameObject root)
        {
            if(!root||root.GetComponent<CandyWildlifePanel>())return;
            root.AddComponent<CandyWildlifePanel>().session=root.GetComponent<ISessionInfo>();
        }
        public static bool Showing(ISessionInfo owner)=>(owner as Component)?.GetComponent<CandyWildlifePanel>()?.open==true;
        public static void Open(ISessionInfo owner)
        {
            var view=(owner as Component)?.GetComponent<CandyWildlifePanel>();
            if(view)
            {
                view.open=true;view.selectReady=false;var camera=owner.ActiveCamera;
                view.openingForward=camera?camera.transform.forward:Vector3.forward;view.openingForward.y=0;
                if(view.openingForward.sqrMagnitude<.01f)view.openingForward=Vector3.forward;
                view.openingForward.Normalize();owner.SetMenu(true);
            }
        }
        void Update()
        {
            if(session==null)return;
            if(Services.Session!=session){open=false;if(canvas)canvas.gameObject.SetActive(false);return;}
            if(Keyboard.current?.f6Key.wasPressedThisFrame==true&&Services.Session==session){if(open)open=false;else Open(session);}
            if(open&&!session.MenuOpen)open=false;
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true&&open){open=false;MenuAim.Consume();}
            if(!canvas&&open)Build();
            if(!canvas)return;
            canvas.gameObject.SetActive(open&&session.MenuOpen&&session.Player.gameObject.activeInHierarchy);
            if(!open)return;
            var controls=Controls;status.text=controls==null?"Wildlife is loading":controls.Status+"\n"+controls.Count+" / 24 animals · "+(tame?"COMPANION: follows you":"WILD: roams nearby");
            for(int i=0;i<buttons.Count;i++)buttons[i].interactable=i<3||controls?.Available==true;
            buttons[0].GetComponentInChildren<Text>().text=tame?"Companions [ON]":"Wild roaming [ON]";
            if(session.IsVR)
            {
                canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=session.ActiveCamera;
                var camera=session.ActiveCamera;if(!camera)return;
                bool left=session.Player?.gameObject.GetComponent<IBlasterControls>()?.LeftHanded==true;
                MenuAim.PlaceTablet(panel,camera,session,left);
                var ray=MenuAim.Resolve(camera,camera.transform.parent,left,false,false,out bool physical,out bool chosenLeft);
                bool held=session.MenuSelectHeld,pressed=session.MenuSelectPressed;
                if(physical&&MenuAim.PhysicalPose(camera,chosenLeft,out _,out var controller))
                {
                    var trigger=controller.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("triggerPressed");
                    var axis=controller.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("trigger");
                    held=trigger!=null?trigger.isPressed:axis!=null&&axis.ReadValue()>.65f;
                    pressed=trigger!=null?trigger.wasPressedThisFrame:axis!=null&&axis.ReadValue()>.65f&&!previousHeld;
                }
                int identity=(left?1:0)^MenuAim.PointerIdentity(camera,left)*397;
                if(pointerIdentity!=identity){pointerIdentity=identity;selectReady=false;}
                previousHeld=held;
                if(!selectReady){if(!held)selectReady=true;pressed=false;}
                if(MenuAim.InputConsumed)pressed=false;
                if(aimDot)aimDot.gameObject.SetActive(false);
                if(new Plane(panel.forward,panel.position).Raycast(ray,out float distance))
                {
                    Vector2 point=panel.InverseTransformPoint(ray.GetPoint(distance));
                    MenuAim.Present(aimDot,ray.GetPoint(distance),panel.rect.Contains(point),false);
                    for(int i=0;i<buttons.Count;i++)
                    {
                        var rect=(RectTransform)buttons[i].transform;bool hover=rect.rect.Contains(point-(Vector2)rect.localPosition);
                        buttons[i].image.color=hover?new Color(.15f,.85f,.65f):new Color(.25f,.14f,.39f);
                        if(hover&&buttons[i].interactable)MenuAim.Present(aimDot,ray.GetPoint(distance),true,true);
                        if(hover&&pressed&&buttons[i].interactable){MenuAim.Consume();selectReady=false;actions[i]();break;}
                    }
                }
            }
            else
            {
                canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1500;panel.localScale=Vector3.one;panel.anchoredPosition=Vector2.zero;panel.localRotation=Quaternion.identity;
                if(Mouse.current!=null&&RectTransformUtility.ScreenPointToLocalPointInRectangle(panel,Mouse.current.position.ReadValue(),null,out var point))
                {
                    for(int i=0;i<buttons.Count;i++)
                    {
                        var rect=(RectTransform)buttons[i].transform;bool hover=rect.rect.Contains(point-(Vector2)rect.localPosition);
                        buttons[i].image.color=hover?new Color(.15f,.85f,.65f):new Color(.25f,.14f,.39f);
                        if(hover&&Mouse.current.leftButton.wasPressedThisFrame&&buttons[i].interactable){actions[i]();break;}
                    }
                }
            }
        }
        void Build()
        {
            var root=new GameObject("Wildlife testing menu",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));root.transform.SetParent(transform,false);
            canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1200,900);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=1;
            var plate=new GameObject("Wildlife panel",typeof(RectTransform),typeof(Image));plate.transform.SetParent(root.transform,false);panel=plate.GetComponent<RectTransform>();panel.sizeDelta=new Vector2(1000,800);plate.GetComponent<Image>().color=new Color(.055f,.025f,.11f,.99f);
            Label("ISLAND WILDLIFE",new Vector2(0,350),new Vector2(930,60),34);
            Add("Wild roaming [ON]",new Vector2(-320,275),new Vector2(300,58),()=>tame=!tame);
            Add("Close",new Vector2(320,275),new Vector2(300,58),()=>open=false);
            Add("Clear wildlife",new Vector2(0,275),new Vector2(300,58),()=>Controls?.Clear());
            Add("Spawn all twelve",new Vector2(0,205),new Vector2(940,55),()=>{if(Controls==null)return;foreach(var id in Controls.Species)Controls.Spawn(id,tame);});
            var ids=Controls?.Species;
            if(ids!=null)for(int i=0;i<ids.Count;i++)
            {
                string id=ids[i];string title=System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id);
                Add("+ "+title,new Vector2(i%2==0?-240:240,128-(i/2)*66),new Vector2(450,56),()=>Controls?.Spawn(id,tame));
            }
            status=Label("",new Vector2(0,-325),new Vector2(940,95),21);
            aimDot=MenuAim.CreateReticle(panel);
        }
        Text Label(string text,Vector2 position,Vector2 size,int font)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(panel,false);
            var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=font;t.color=Color.white;t.text=text;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.rectTransform.sizeDelta=size;t.rectTransform.anchoredPosition=position;return t;
        }
        void Add(string text,Vector2 position,Vector2 size,System.Action action)
        {
            var go=new GameObject(text,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(panel,false);
            var rect=go.GetComponent<RectTransform>();rect.sizeDelta=size;rect.anchoredPosition=position;
            var button=go.GetComponent<Button>();button.image.color=new Color(.25f,.14f,.39f);
            var label=Label(text,Vector2.zero,size,26);label.transform.SetParent(go.transform,false);
            button.onClick.AddListener(()=>action());buttons.Add(button);actions.Add(action);
        }
        void OnDestroy(){if(canvas)Destroy(canvas.gameObject);}
    }
}
