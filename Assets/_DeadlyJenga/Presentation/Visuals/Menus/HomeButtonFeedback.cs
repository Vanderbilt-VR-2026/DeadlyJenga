using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    public sealed class HomeButtonFeedback:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        Button button;
        Image face;
        Canvas canvas;
        Outline rim;
        Color resting;
        bool pointed,mouse;
        public void Initialize(Button value)
        {
            button=value;face=value.image;resting=face.color;canvas=GetComponentInParent<Canvas>();
            button.transition=Selectable.Transition.None;
            rim=gameObject.AddComponent<Outline>();rim.effectColor=new Color(.45f,1,.8f,1);
            rim.effectDistance=new Vector2(6,-6);rim.enabled=false;
        }
        public void Focus(bool value){pointed=value;Refresh();}
        public void OnPointerEnter(PointerEventData data){mouse=true;Refresh();}
        public void OnPointerExit(PointerEventData data){mouse=false;Refresh();}
        void LateUpdate()=>Refresh();
        void Refresh()
        {
            if(!button||!face)return;
            bool nativeMouse=mouse&&!MenuGamepad.UsingPointer&&canvas?.renderMode!=RenderMode.WorldSpace;
            bool highlight=button.IsInteractable()&&(pointed||nativeMouse);
            face.color=!button.IsInteractable()?Color.Lerp(resting,Color.gray,.55f):highlight?new Color(.58f,.16f,.42f,1):resting;
            rim.enabled=highlight;
            transform.localScale=Vector3.one*(highlight?1.045f:1);
        }
        void OnDisable(){pointed=mouse=false;if(rim)rim.enabled=false;transform.localScale=Vector3.one;}
    }
}
