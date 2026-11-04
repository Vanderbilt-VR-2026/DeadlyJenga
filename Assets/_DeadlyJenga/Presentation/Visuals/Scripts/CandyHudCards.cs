using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    public static class CandyHudCards
    {
        public static readonly Color Ink=new Color(.15f,.07f,.29f);
        public static readonly Color Accent=new Color(.37f,.18f,.61f);
        public static readonly Color Background=new Color(.97f,.92f,1f,.89f);
        static Sprite edgeWash,centerWash;

        public static Sprite Wash(bool centered)
        {
            var sprite=centered?centerWash:edgeWash;
            if(sprite)return sprite;
            const int width=128,height=64;
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {name="Candy HUD feathered backdrop",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[width*height];
            for(int y=0;y<height;y++)
            for(int x=0;x<width;x++)
            {
                float u=x/(width-1f),v=y/(height-1f);
                float left=Mathf.SmoothStep(0,1,u/(centered?.12f:.035f));
                float right=Mathf.SmoothStep(0,1,(1-u)/(centered?.12f:.17f));
                float edge=Mathf.Min(v,1-v);
                float vertical=Mathf.SmoothStep(0,1,edge/.11f);
                pixels[y*width+x]=new Color(1,1,1,left*right*vertical);
            }
            texture.SetPixels32(pixels);texture.Apply(false,true);
            sprite=Sprite.Create(texture,new Rect(0,0,width,height),Vector2.one*.5f,100,0,SpriteMeshType.FullRect);
            sprite.hideFlags=HideFlags.HideAndDontSave;
            if(centered)centerWash=sprite;else edgeWash=sprite;
            return sprite;
        }

        public static void Tint(Text text,Color accent,float strength=.24f)
        {
            accent.a=Background.a;
            text.transform.parent.GetComponent<Image>().color=Color.Lerp(Background,accent,strength);
        }
        public static void Float(Text text,Color color)
        {
            var image=text.transform.parent.GetComponent<Image>();
            image.color=Color.clear;
            text.color=color;
            var shadow=text.gameObject.AddComponent<Outline>();
            shadow.effectColor=new Color(.10f,.045f,.20f,.92f);
            shadow.effectDistance=new Vector2(1,-1);
        }

        public static Text Create(RectTransform parent,string name,Vector2 position,Vector2 size,int font,TextAnchor alignment=TextAnchor.UpperLeft)
        {
            var card=new GameObject(name,typeof(RectTransform),typeof(Image));card.transform.SetParent(parent,false);
            var image=card.GetComponent<Image>();image.color=Background;image.sprite=Wash(alignment==TextAnchor.MiddleCenter);image.raycastTarget=false;
            var rect=card.GetComponent<RectTransform>();rect.sizeDelta=size;rect.anchoredPosition=position;
            var go=new GameObject("Text",typeof(RectTransform),typeof(Text));go.transform.SetParent(card.transform,false);
            var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=font;text.lineSpacing=1.08f;text.fontStyle=FontStyle.Bold;
            text.color=Ink;text.alignment=alignment;text.supportRichText=true;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
            text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=new Vector2(12,8);text.rectTransform.offsetMax=new Vector2(-12,-8);
            return text;
        }
        public static void Visible(Text text,bool visible){if(text&&text.transform.parent.gameObject.activeSelf!=visible)text.transform.parent.gameObject.SetActive(visible);}
        public static void Place(Text text,Vector2 position,Vector2 size)
        {var rect=(RectTransform)text.transform.parent;rect.anchoredPosition=position;rect.sizeDelta=size;}
    }
}
