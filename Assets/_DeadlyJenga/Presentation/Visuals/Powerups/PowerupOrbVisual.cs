using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    [ExecuteAlways]
    public sealed class PowerupOrbVisual : MonoBehaviour
    {
        public Transform rings,label;
        TextMesh text;
        MaterialPropertyBlock properties;
        void OnEnable(){text=label?label.GetComponent<TextMesh>():null;Font.textureRebuilt+=Bind;Bind(text?text.font:null);}
        void OnDisable(){Font.textureRebuilt-=Bind;}
        void Bind(Font font)
        {
            if(!text||!font||font!=text.font||!font.material.mainTexture)return;
            properties??=new MaterialPropertyBlock();properties.SetTexture("_MainTex",font.material.mainTexture);text.GetComponent<Renderer>().SetPropertyBlock(properties);
        }
        void Update()
        {
            if(!Application.isPlaying)return;
            if(rings)rings.Rotate(0,25*Time.deltaTime,12*Time.deltaTime,Space.Self);
            var camera=Services.Session?.ActiveCamera;
            if(label&&camera)label.rotation=Quaternion.LookRotation(label.position-camera.transform.position);
        }
    }
}
