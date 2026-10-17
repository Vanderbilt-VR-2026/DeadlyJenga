using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.CandyArt
{
    // Scene-specific restore data, deliberately excluded from the portable environment prefab.
    public sealed class CandyArtBaseline : MonoBehaviour
    {
        [Serializable] public class BlockState { public CastleBlock block; public CastleBlockAppearance appearance; public Material material; }
        [Serializable] public class RendererState { public Renderer renderer; public bool enabled; }
        public CastleGenerator generator;
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
