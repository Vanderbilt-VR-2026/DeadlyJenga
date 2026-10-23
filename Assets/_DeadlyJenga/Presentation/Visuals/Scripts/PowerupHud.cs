using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class PowerupHud : MonoBehaviour
    {
        Canvas canvas;
        Text speed,jump;
        EffectType speedType,jumpType;
        Camera attached;
        bool vr;
        public float ShownSpeedSeconds {get;private set;}
        public float ShownJumpSeconds {get;private set;}
        void Build()
        {
            var root=new GameObject("Powerup countdowns",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));root.transform.SetParent(transform,false);
            canvas=root.GetComponent<Canvas>();canvas.sortingOrder=32;
            speed=Row(root.transform,"Speed countdown",new Vector2(420,270),CandyHudVisuals.Mint);
            jump=Row(root.transform,"Jump countdown",new Vector2(420,214),CandyHudVisuals.Lavender);
            speedType=Resources.Load<EffectType>("Powerups/Speed2x");jumpType=Resources.Load<EffectType>("Powerups/Jump3x");
        }
        static Text Row(Transform parent,string name,Vector2 position,Color color)
        {
            var panel=new GameObject(name,typeof(RectTransform),typeof(Image));panel.transform.SetParent(parent,false);
            var image=panel.GetComponent<Image>();image.color=new Color(.07f,.04f,.15f,.9f);image.raycastTarget=false;
            image.rectTransform.sizeDelta=new Vector2(360,48);image.rectTransform.anchoredPosition=position;
            var go=new GameObject("Timer",typeof(RectTransform),typeof(Text));go.transform.SetParent(panel.transform,false);
            var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=20;text.fontStyle=FontStyle.Bold;
            text.alignment=TextAnchor.MiddleCenter;text.color=color;text.raycastTarget=false;text.rectTransform.sizeDelta=new Vector2(350,46);return text;
        }
        void Update()
        {
            var view=GetComponent<IPlayerView>();var effects=GetComponent<ITimedEffects>();
            if(view==null||effects==null||!view.ControlledHere||!view.ViewCamera)return;
            if(!canvas)Build();
            if(attached!=view.ViewCamera||vr!=view.IsVR){attached=view.ViewCamera;vr=view.IsVR;CandyHudLayout.Configure(canvas,transform,attached,vr);}
            ShownSpeedSeconds=effects.SecondsRemaining(speedType);ShownJumpSeconds=effects.SecondsRemaining(jumpType);
            Show(speed,"2x SPEED",ShownSpeedSeconds);Show(jump,"3x JUMP",ShownJumpSeconds);
            canvas.enabled=attached.isActiveAndEnabled&&(ShownSpeedSeconds>0||ShownJumpSeconds>0);
        }
        static void Show(Text text,string name,float seconds)
        {text.transform.parent.gameObject.SetActive(seconds>0);text.text=name+"  "+Mathf.CeilToInt(seconds)+"s  |  INFINITE STAMINA";}
        void OnDisable(){if(canvas)canvas.enabled=false;}
        void OnDestroy(){if(canvas)Destroy(canvas.gameObject);}
    }
}
