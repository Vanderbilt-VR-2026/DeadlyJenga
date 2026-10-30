using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    public static class CandyHudCards
    {
        public static readonly Color Background=new Color(.025f,.035f,.065f,.94f);
        public static Text Create(RectTransform parent,string name,Vector2 position,Vector2 size,int font,TextAnchor alignment=TextAnchor.UpperLeft)
        {
            var card=new GameObject(name,typeof(RectTransform),typeof(Image));card.transform.SetParent(parent,false);
            var image=card.GetComponent<Image>();image.color=Background;image.raycastTarget=false;
            var rect=card.GetComponent<RectTransform>();rect.sizeDelta=size;rect.anchoredPosition=position;
            var go=new GameObject("Text",typeof(RectTransform),typeof(Text));go.transform.SetParent(card.transform,false);
            var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=font;text.lineSpacing=1.08f;text.fontStyle=FontStyle.Bold;
            text.color=Color.white;text.alignment=alignment;text.supportRichText=true;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
            text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=new Vector2(12,8);text.rectTransform.offsetMax=new Vector2(-12,-8);
            return text;
        }
        public static void Visible(Text text,bool visible){if(text&&text.transform.parent.gameObject.activeSelf!=visible)text.transform.parent.gameObject.SetActive(visible);}
        public static void Place(Text text,Vector2 position,Vector2 size)
        {var rect=(RectTransform)text.transform.parent;rect.anchoredPosition=position;rect.sizeDelta=size;}
    }
}
