using UnityEngine;

namespace DeadlyJenga.Presentation
{
    public static class CandyNameplate
    {
        public static TextMesh Create(Transform parent,string name)
        {
            var root=new GameObject(name);root.transform.SetParent(parent,false);
            var text=Text(root);root.GetComponent<MeshRenderer>().sortingOrder=2;
            var shade=new GameObject("Text shadow");shade.transform.SetParent(root.transform,false);shade.transform.localPosition=new Vector3(.007f,-.009f,.006f);
            var shadow=Text(shade);shadow.color=new Color(.025f,.01f,.05f,.85f);shade.GetComponent<MeshRenderer>().sortingOrder=1;
            return text;
        }
        static Sprite background;
        public static TextMesh CreateTag(Transform parent,string name)
        {
            var text=Create(parent,name);
            text.characterSize=.029f;
            text.richText=false;
            var box=new GameObject("Name background");
            box.transform.SetParent(text.transform,false);
            var sprite=box.AddComponent<SpriteRenderer>();
            if(!background)background=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);
            sprite.sprite=background;
            sprite.color=new Color(.04f,.035f,.07f,.78f);
            sprite.sortingOrder=310;
            text.GetComponent<MeshRenderer>().sortingOrder=312;
            text.transform.Find("Text shadow").GetComponent<MeshRenderer>().sortingOrder=311;
            text.gameObject.AddComponent<CandyNameplateFollow>();
            return text;
        }
        public static string BotName(string name)
        {
            if(string.IsNullOrEmpty(name))return name;
            foreach(string divider in BotDividers)
            {
                int end=name.IndexOf(divider,System.StringComparison.Ordinal);
                if(end>0)return name.Substring(0,end);
            }
            return name;
        }
        static readonly string[] BotDividers={" — "," -- "," – "," - "};
        static TextMesh Text(GameObject go)
        {
            var text=go.AddComponent<TextMesh>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");go.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
            text.fontSize=48;text.characterSize=.035f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;return text;
        }
        public static void RefreshShadow(TextMesh text)
        {
            var shadow=text.transform.Find("Text shadow")?.GetComponent<TextMesh>();if(!shadow)return;
            shadow.text=text.text;shadow.characterSize=text.characterSize;
        }
    }
}
