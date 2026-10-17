using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    // Each side has its own readable face; UI text never shows through in reverse.
    public sealed class LobbyTerminalFaces : MonoBehaviour
    {
        Camera eye;
        CanvasGroup front,rear;
        RectTransform rearRect;
        public bool ShowingFront {get;private set;}
        public float Opacity {get;set;}=1;
        public void Build(Camera camera,string title,Color accent,Vector2 size)
        {
            eye=camera;front=gameObject.AddComponent<CanvasGroup>();
            var back=new GameObject(title+" / readable back",typeof(RectTransform),typeof(Canvas),typeof(Image),typeof(CanvasGroup));
            back.transform.SetParent(transform.parent,false);rearRect=back.GetComponent<RectTransform>();rearRect.sizeDelta=size;
            var canvas=back.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.overrideSorting=true;canvas.sortingOrder=100;
            var image=back.GetComponent<Image>();image.sprite=CandyHudVisuals.Pill;image.type=Image.Type.Sliced;image.color=CandyHudVisuals.Ink;image.raycastTarget=false;
            rear=back.GetComponent<CanvasGroup>();
            Label("DEADLY JENGA",90,34,accent);Label(title,20,23,Color.white);Label("CONTROLS ON THE OTHER SIDE",-70,18,CandyHudVisuals.Muted);
            Refresh();
        }
        void Label(string value,float y,int size,Color color)
        {
            var go=new GameObject(value,typeof(RectTransform),typeof(Text));go.transform.SetParent(rearRect,false);var label=go.GetComponent<Text>();
            label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.text=value;label.fontSize=size;label.color=color;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;
            label.rectTransform.sizeDelta=new Vector2(560,55);label.rectTransform.anchoredPosition=new Vector2(0,y);
        }
        void LateUpdate()=>Refresh();
        public void Refresh()
        {
            if(!front||!rear||!eye)return;
            ShowingFront=Vector3.Dot(eye.transform.position-transform.position,transform.forward)<=0;
            front.alpha=ShowingFront?Opacity:0;front.blocksRaycasts=front.interactable=ShowingFront&&Opacity>.5f;
            rear.alpha=ShowingFront?0:1;rear.blocksRaycasts=false;
            rearRect.SetPositionAndRotation(transform.position+transform.forward*.012f,transform.rotation*Quaternion.Euler(0,180,0));
            rearRect.localScale=transform.localScale;rearRect.sizeDelta=((RectTransform)transform).sizeDelta;
        }
        void OnDestroy(){if(rearRect)Destroy(rearRect.gameObject);}
    }
}
