using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyArt
{
    public sealed class ChristmasTownSnow : MonoBehaviour
    {
        public Transform mapSpace;
        public ParticleSystem flakes;
        public Texture2D shelterHeights;
        public Vector2 mapSize=new Vector2(96,76);
        MaterialPropertyBlock properties;
        ParticleSystemRenderer visual;
        void OnEnable()
        {
            visual=flakes?flakes.GetComponent<ParticleSystemRenderer>():null;RefreshMask();
            if(Application.isPlaying&&flakes)
            {
                var main=flakes.main;main.maxParticles=Services.Session?.IsVR==true?350:700;
                flakes.Play();
            }
        }
        void LateUpdate(){RefreshMask();}
        public bool AllowsSnowAt(Vector3 worldPosition)
        {
            if(!mapSpace||!shelterHeights)return false;
            var p=mapSpace.InverseTransformPoint(worldPosition);float u=p.x/mapSize.x+.5f,v=p.z/mapSize.y+.5f;
            if(u<0||v<0||u>=1||v>=1)return false;
            float roof=shelterHeights.GetPixel(Mathf.FloorToInt(u*shelterHeights.width),Mathf.FloorToInt(v*shelterHeights.height)).r;
            return p.y>roof+.14f;
        }
        public void RefreshMask()
        {
            if(!visual&&flakes)visual=flakes.GetComponent<ParticleSystemRenderer>();
            if(!visual||!mapSpace)return;
            properties??=new MaterialPropertyBlock();properties.SetMatrix("_TownWorldToLocal",mapSpace.worldToLocalMatrix);
            properties.SetVector("_TownBounds",new Vector4(mapSize.x,mapSize.y,0,0));properties.SetTexture("_RoofMap",shelterHeights);visual.SetPropertyBlock(properties);
        }
    }
}
