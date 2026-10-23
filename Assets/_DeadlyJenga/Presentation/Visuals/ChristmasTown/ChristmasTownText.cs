using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    // Keep depth-tested world text bound to Unity's current dynamic font atlas.
    [ExecuteAlways, RequireComponent(typeof(TextMesh))]
    public sealed class ChristmasTownText : MonoBehaviour
    {
        TextMesh text;
        Renderer display;
        MaterialPropertyBlock properties;
        void OnEnable()
        {
            text=GetComponent<TextMesh>();display=GetComponent<Renderer>();
            Font.textureRebuilt+=Rebuilt;Refresh();
        }
        void OnDisable() { Font.textureRebuilt-=Rebuilt; }
        void Rebuilt(Font font) { if(text&&text.font==font)Refresh(); }
        void Refresh()
        {
            if(!text||!text.font||!display||!text.font.material.mainTexture)return;
            properties ??=new MaterialPropertyBlock();display.GetPropertyBlock(properties);
            properties.SetTexture("_MainTex",text.font.material.mainTexture);display.SetPropertyBlock(properties);
        }
    }
}
