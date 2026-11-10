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
        static readonly System.Collections.Generic.Dictionary<string,Sprite> emblems=new();

        public static Sprite Wash(bool centered)
        {
            var sprite=centered?centerWash:edgeWash;
            if(sprite)return sprite;
            const int width=64,height=64;
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {name="Candy HUD enamel",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[width*height];
            for(int y=0;y<height;y++)
            for(int x=0;x<width;x++)
            {
                var point=new Vector2(x,y);
                float radius=centered?18:13;
                var core=new Vector2(Mathf.Clamp(x,radius+2,61-radius),Mathf.Clamp(y,radius+3,61-radius));
                float distance=Vector2.Distance(point,core)-radius;
                Color surface=Color.Lerp(new Color(.89f,.81f,.99f),Color.white,Mathf.SmoothStep(0,1,y/62f));
                if(distance>-.8f)surface=new Color(.63f,.48f,.78f);
                else if(y<14)surface=new Color(.76f,.63f,.91f);
                float opacity=Mathf.Clamp01(.6f-distance);
                if(y<16&&distance>-.9f)opacity*=.82f;
                surface.a=opacity;
                pixels[y*width+x]=surface;
            }
            texture.SetPixels32(pixels);texture.Apply(false,true);
            sprite=Sprite.Create(texture,new Rect(0,0,width,height),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(22,22,22,22));
            sprite.hideFlags=HideFlags.HideAndDontSave;
            if(centered)centerWash=sprite;else edgeWash=sprite;
            return sprite;
        }

        public static void Tint(Text text,Color accent,float strength=.24f)
        {
            accent.a=Background.a;
            text.transform.parent.GetComponent<Image>().color=Color.Lerp(Background,accent,strength);
        }
        public static void Badge(Text text)
        {
            var image=text.transform.parent.GetComponent<Image>();
            image.color=new Color(.95f,.91f,1f,.96f);
            image.type=Image.Type.Sliced;
            text.color=Ink;

        }

        public static void Emblem(Text text,string shape,Color tint,bool waypoint=false)
        {
            var parent=(RectTransform)text.transform.parent;
            var go=new GameObject("Candy objective emblem",typeof(RectTransform),typeof(Image));
            go.transform.SetParent(parent,false);
            var badge=go.GetComponent<Image>();
            badge.sprite=GetEmblem(shape);badge.color=tint;badge.raycastTarget=false;
            badge.rectTransform.anchorMin=badge.rectTransform.anchorMax=new Vector2(waypoint?.5f:0,1);
            badge.rectTransform.anchoredPosition=waypoint?new Vector2(0,-9):new Vector2(13,-14);
            badge.rectTransform.sizeDelta=Vector2.one*(waypoint?20:28);
            if(waypoint)
            {
                parent.GetComponent<Image>().color=Color.clear;
                text.rectTransform.offsetMax=new Vector2(-12,-19);
                text.rectTransform.offsetMin=new Vector2(12,3);
                text.color=new Color(1,.96f,1);
                var outline=text.gameObject.AddComponent<Outline>();
                outline.effectColor=new Color(.20f,.08f,.34f,.95f);
                outline.effectDistance=new Vector2(1,-1);
            }
            else text.rectTransform.offsetMin=new Vector2(34,8);
        }

        static Sprite GetEmblem(string shape)
        {
            if(emblems.TryGetValue(shape,out var sprite)&&sprite)return sprite;
            const int size=48;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {name="HUD "+shape,hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)
            for(int x=0;x<size;x++)
            {
                float diamond=Mathf.Abs(x-23.5f)*.86f+Mathf.Abs(y-23.5f);
                float alpha=Mathf.Clamp01(22.5f-diamond);
                Color color=y>23?new Color(.99f,.94f,1):new Color(.67f,.52f,.84f);
                bool glyph=shape=="flag"?x>=16&&x<=19&&y>=12&&y<=34||
                    x>=19&&x<=33&&y>=24&&y<=34-(x-19)/4:
                    shape=="clue"?Mathf.Abs(Vector2.Distance(new Vector2(x,y),new Vector2(21,28))-7)<1.5f||
                    x>=24&&x<=33&&Mathf.Abs(x+y-47)<2:
                    Mathf.Abs(x-24)+Mathf.Abs(y-24)<9;
                if(glyph)color=new Color(.22f,.10f,.35f);
                color.a=alpha;
                pixels[y*size+x]=color;
            }
            texture.SetPixels32(pixels);texture.Apply(false,true);
            sprite=Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,100);
            sprite.hideFlags=HideFlags.HideAndDontSave;emblems[shape]=sprite;
            return sprite;
        }

        public static Text Create(RectTransform parent,string name,Vector2 position,Vector2 size,int font,TextAnchor alignment=TextAnchor.UpperLeft)
        {
            var card=new GameObject(name,typeof(RectTransform),typeof(Image));card.transform.SetParent(parent,false);
            var image=card.GetComponent<Image>();image.color=Background;image.sprite=Wash(alignment==TextAnchor.MiddleCenter);image.type=Image.Type.Sliced;image.raycastTarget=false;
            var rect=card.GetComponent<RectTransform>();rect.sizeDelta=size;rect.anchoredPosition=position;
            var go=new GameObject("Text",typeof(RectTransform),typeof(Text));go.transform.SetParent(card.transform,false);
            var text=go.GetComponent<Text>();text.font=DeadlyJenga.CastlePrototype.CandyHudVisuals.TextFont;text.fontSize=font;text.lineSpacing=1.08f;text.fontStyle=FontStyle.Bold;
            text.color=Ink;text.alignment=alignment;text.supportRichText=true;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
            text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=new Vector2(12,8);text.rectTransform.offsetMax=new Vector2(-12,-8);
            return text;
        }
        public static void Visible(Text text,bool visible){if(text&&text.transform.parent.gameObject.activeSelf!=visible)text.transform.parent.gameObject.SetActive(visible);}
        public static void Place(Text text,Vector2 position,Vector2 size)
        {var rect=(RectTransform)text.transform.parent;rect.anchoredPosition=position;rect.sizeDelta=size;}
    }
}
