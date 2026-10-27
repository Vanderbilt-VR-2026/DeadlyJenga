using UnityEngine;

namespace DeadlyJenga.Presentation
{
    [RequireComponent(typeof(TextMesh))]
    public sealed class VillageSignLettering : MonoBehaviour
    {
        public Material template;
        Material instance;
        TextMesh text;
        void OnEnable()
        {
            text=GetComponent<TextMesh>();if(!text.font||!template)return;
            instance=new Material(template){name="Village font atlas (runtime)",hideFlags=HideFlags.DontSave};
            GetComponent<MeshRenderer>().sharedMaterial=instance;Font.textureRebuilt+=Atlas;
            text.font.RequestCharactersInTexture(text.text,text.fontSize,FontStyle.Normal);Atlas(text.font);
        }
        void Atlas(Font font){if(instance&&text&&font==text.font)instance.mainTexture=font.material.mainTexture;}
        void OnDisable()
        {
            Font.textureRebuilt-=Atlas;
            if(instance){if(Application.isPlaying)Destroy(instance);else DestroyImmediate(instance);}instance=null;
        }
    }
}
