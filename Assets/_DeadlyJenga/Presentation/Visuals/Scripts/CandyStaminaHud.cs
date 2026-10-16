using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Entirely cosmetic. No colliders, rigidbodies or writes to player movement.
    [DisallowMultipleComponent]
    public sealed class CandyStaminaHud : MonoBehaviour
    {
        Canvas canvas;CanvasGroup group;RectTransform panel,fill;Image juice;Text label;
        bool vr;Camera attachedCamera;float displayed=1;
        static Sprite rounded;
        public float Opacity=>group?group.alpha:0;
        public static float TargetOpacity(bool climbing,float grip,bool hidden)=>hidden?0:climbing?1:grip<.9999f?.46f:0;
        static Sprite Rounded
        {
            get
            {
                if(rounded)return rounded;
                var texture=new Texture2D(32,32,TextureFormat.RGBA32,false){name="Candy UI rounded edge",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
                for(int y=0;y<32;y++)for(int x=0;x<32;x++)
                {float a=Mathf.Clamp01(15.5f-Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f)));texture.SetPixel(x,y,new Color(1,1,1,a));}
                texture.Apply();rounded=Sprite.Create(texture,new Rect(0,0,32,32),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(15,15,15,15));rounded.hideFlags=HideFlags.HideAndDontSave;return rounded;
            }
        }
        Image Pill(string name,Transform parent,Vector2 size,Vector2 position,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>();image.sprite=Rounded;image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=false;
            image.rectTransform.sizeDelta=size;image.rectTransform.anchoredPosition=position;return image;
        }
        void Build()
        {
            var root=new GameObject("Candy stamina • presentation only",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup),typeof(CanvasScaler));root.transform.SetParent(transform,false);
            canvas=root.GetComponent<Canvas>();canvas.sortingOrder=25;group=root.GetComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;group.alpha=0;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            panel=Pill("Soft shadow",root.transform,new Vector2(342,72),Vector2.zero,new Color(.08f,.025f,.16f,.45f)).rectTransform;
            Pill("Lavender shell",panel,new Vector2(330,58),new Vector2(0,4),new Color(.78f,.59f,1));
            Pill("Grape well",panel,new Vector2(316,44),new Vector2(0,4),new Color(.16f,.055f,.29f));
            juice=Pill("Candy juice",panel,new Vector2(308,36),new Vector2(-154,4),new Color(.26f,1,.8f));fill=juice.rectTransform;fill.pivot=new Vector2(0,.5f);
            Pill("Sugar shine",fill,new Vector2(1,9),Vector2.zero,new Color(1,1,1,.32f)).rectTransform.anchorMin=new Vector2(.05f,.63f);
            var shine=fill.GetChild(0).GetComponent<RectTransform>();shine.anchorMax=new Vector2(.95f,.63f);shine.sizeDelta=new Vector2(0,8);shine.anchoredPosition=Vector2.zero;
            var text=new GameObject("Stamina label",typeof(RectTransform),typeof(Text));text.transform.SetParent(panel,false);label=text.GetComponent<Text>();
            label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=16;label.fontStyle=FontStyle.Bold;label.alignment=TextAnchor.MiddleCenter;label.color=Color.white;label.raycastTarget=false;
            label.rectTransform.sizeDelta=new Vector2(310,40);label.rectTransform.anchoredPosition=new Vector2(0,4);
        }
        // Reads the character only through Core (IMovementState + IPlayerView).
        IMovementState owner;
        void Update()
        {
            if (!(owner is Object alive && alive)) owner = GetComponent<IMovementState>();
            var view = owner as IPlayerView;
            if (view != null && view.ControlledHere && view.ViewCamera) Present(owner, Time.unscaledDeltaTime);
        }
        public void Present(IMovementState motor,float dt)
        {
            var view=motor as IPlayerView;var body=motor as Behaviour;
            if(view==null||!body)return;
            var viewCamera=view.ViewCamera;
            if(!canvas)Build();
            if(attachedCamera!=viewCamera||vr!=view.IsVR||canvas.renderMode==RenderMode.ScreenSpaceOverlay&&view.IsVR)
            {attachedCamera=viewCamera;vr=view.IsVR;ConfigureView();}
            // First desktop frame also needs anchors set.
            if(!vr&&panel.anchorMin!=new Vector2(.5f,0))ConfigureView();
            bool hidden=!body.isActiveAndEnabled||motor.InputPaused||!viewCamera||!viewCamera.isActiveAndEnabled;
            group.alpha=Mathf.MoveTowards(group.alpha,TargetOpacity(motor.Climbing,motor.Stamina,hidden),Mathf.Max(0,dt)*4);
            displayed=Mathf.MoveTowards(displayed,motor.Stamina,Mathf.Max(0,dt)*3);
            fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,308*displayed);
            juice.color=Color.Lerp(new Color(1,.26f,.32f),new Color(.22f,1,.76f),Mathf.SmoothStep(0,1,displayed));
            label.text=(view.StaminaExhausted?"EXHAUSTED":motor.Climbing?"GRIP":view.StaminaRecovering?"CATCHING YOUR BREATH":"FIND SECURE FOOTING")+"  "+Mathf.CeilToInt(motor.Stamina*100)+"%";
            canvas.enabled=group.alpha>0;
        }
        void ConfigureView()
        {
            var rect=canvas.GetComponent<RectTransform>();
            if(vr&&attachedCamera)
            {
                canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=attachedCamera;rect.SetParent(attachedCamera.transform,false);
                rect.sizeDelta=new Vector2(360,90);rect.localPosition=new Vector3(0,-.22f,.8f);rect.localRotation=Quaternion.identity;rect.localScale=Vector3.one*.00065f;
                panel.anchorMin=panel.anchorMax=new Vector2(.5f,.5f);panel.anchoredPosition=Vector2.zero;
            }
            else
            {
                rect.SetParent(transform,false);canvas.renderMode=RenderMode.ScreenSpaceOverlay;rect.localScale=Vector3.one;
                panel.anchorMin=panel.anchorMax=new Vector2(.5f,0);panel.anchoredPosition=new Vector2(0,80);
            }
        }
        void OnDisable(){if(canvas)canvas.enabled=false;}
        void OnDestroy(){if(canvas)Destroy(canvas.gameObject);}
    }
}
