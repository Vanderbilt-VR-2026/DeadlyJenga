using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace DeadlyJenga.CandyArt
{
    // Scene-specific restore data, deliberately excluded from the portable environment prefab.
    public sealed class CandyArtBaseline : MonoBehaviour
    {
        [Serializable] public class BlockState { public MonoBehaviour block; public ScriptableObject appearance; public Material material; }
        [Serializable] public class RendererState { public Renderer renderer; public bool enabled; }
        [Tooltip("The map (any IMapFootprint component).")]
        [FormerlySerializedAs("generator")] public MonoBehaviour map;
        public GameObject blockPrefab;
        public Material red,green,purple,sky;
        public AmbientMode ambientMode;
        public Color ambientSky,ambientEquator,ambientGround;
        public float ambientIntensity;
        public Light sun;
        public Color sunColor;
        public float sunIntensity;
        public Quaternion sunRotation;
        public List<BlockState> blocks=new List<BlockState>();
        public List<RendererState> renderers=new List<RendererState>();
    }
}
