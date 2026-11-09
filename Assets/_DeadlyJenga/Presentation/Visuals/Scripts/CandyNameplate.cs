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
