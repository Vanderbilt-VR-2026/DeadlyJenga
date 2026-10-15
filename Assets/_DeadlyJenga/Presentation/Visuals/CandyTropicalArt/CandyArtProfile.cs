using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    [CreateAssetMenu(menuName = "Candy Tropical/Art Profile")]
    public sealed class CandyArtProfile : ScriptableObject
    {
        [Header("Crate artwork (metres, independent of collider)")]
        [Range(.015f,.25f)] public float bevel = .12f;
        [Range(0f,.12f)] public float panelDepth = .035f;
        public bool cornerButtons = true;
        public Material red, green, purple;
        [Header("Environment")]
        public Material water, sand, sky;
        public float waterHeight = -.12f;
        public float sandHeight = -.32f;
        [Min(1)] public float islandRadius = 26;
        public Vector2 islandAspect = new Vector2(1, .92f);
        [Min(100)] public float oceanSize = 1600;
        public bool fitIslandToFoundation = true;
        [Header("Solid sandbar (visual terrain only)")]
        public bool alignSandToFoundation=true;
        [Range(.65f,.9f)] public float sandPlateau=.8f;
        [Range(1,8)] public float sandDepth=3.2f;
        [Range(24,96)] public int sandSegments=72;
        [Min(.25f)] public float sandMargin=2.35f;
        [Range(0,1.5f)] public float sandRelief=.7f;
        [Range(.5f,4)] public float sandShoreWidth=1.5f;
        [Range(48,160)] public int terrainResolution=128;
        [Header("GPU ocean waves")]
        [Range(24,128)] public int waterResolution=96;
        [Min(16)] public float waveExtent=72;
        [Range(0,.5f)] public float waveHeight=.32f;
        [Range(0,2)] public float waveSpeed=.9f;
        [Range(0,1)] public float waveChop=.4f;
        [Header("Sunlight")]
        public Color sunColor = new Color(1f,.94f,.83f);
        [Range(0,4)] public float sunIntensity = 1.65f;
        public Vector3 sunEuler = new Vector3(48,-32,0);
    }
}
