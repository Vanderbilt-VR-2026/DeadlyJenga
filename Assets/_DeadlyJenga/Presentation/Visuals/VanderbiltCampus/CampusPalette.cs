using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    // Paints the campus map. The map's models use gray World materials named "mat_<id>";
    // in Play mode this swaps every one of them for a single palette material (so the campus can batch)
    // and puts the originals back when disabled, so removing Presentation leaves the gray box.
    // Play mode only: the World prefab is never saved with our material.
    [DisallowMultipleComponent]
    public sealed class CampusPalette : MonoBehaviour
    {
        public enum Look { Candy, Plain }

        public Material paletteMaterial;   // CampusPalette.mat (DeadlyJenga/Campus Palette shader)
        public Texture2D candyPalette;     // 32x1 strips; built from CampusPaletteColors when empty
        public Texture2D plainPalette;
        public Look look = Look.Candy;
        [Tooltip("Seconds between checks for newly loaded map tiles.")]
        public float rescanSeconds = 3;

        const string WorldMaterialPrefix = "mat_";
        static readonly int PaletteId = Shader.PropertyToID("_Palette");

        readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        readonly List<Renderer> found = new List<Renderer>();
        Material runtimeMaterial;
        Texture2D builtCandy, builtPlain;
        Transform mapRoot;
        float nextScan;

        public void SetLook(Look value)
        {
            look = value;
            ApplyLook();
        }

        [ContextMenu("Use candy look")] void UseCandy() => SetLook(Look.Candy);
        [ContextMenu("Use plain look")] void UsePlain() => SetLook(Look.Plain);

        void OnValidate() { if (runtimeMaterial) ApplyLook(); }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            // A copy, so changing the look never edits the material asset.
            if (paletteMaterial) runtimeMaterial = new Material(paletteMaterial);
            else
            {
                var shader = Shader.Find("DeadlyJenga/Campus Palette");
                if (!shader) { Debug.LogWarning("CampusPalette: no palette material or shader; the campus stays gray.", this); return; }
                runtimeMaterial = new Material(shader) { enableInstancing = true };
            }
            runtimeMaterial.name = "Campus palette (runtime)";
            ApplyLook();
            nextScan = 0;
        }

        void Update()
        {
            if (!runtimeMaterial || Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + Mathf.Max(.5f, rescanSeconds);
            Scan();
        }

        void ApplyLook()
        {
            if (!runtimeMaterial) return;
            runtimeMaterial.SetTexture(PaletteId, PaletteFor(look));
        }

        Texture2D PaletteFor(Look which)
        {
            if (which == Look.Plain)
            {
                if (plainPalette) return plainPalette;
                if (!builtPlain) builtPlain = CampusPaletteColors.CreateTexture(true);
                return builtPlain;
            }
            if (candyPalette) return candyPalette;
            if (!builtCandy) builtCandy = CampusPaletteColors.CreateTexture(false);
            return builtCandy;
        }

        // Finds map renderers we have not painted yet (tiles may load after the map appears).
        void Scan()
        {
            if (!mapRoot)
            {
                var map = GetComponentInParent<IMapFootprint>() as Component;
                if (!map) return;
                mapRoot = map.transform;
            }
            ForgetDestroyedRenderers();
            mapRoot.GetComponentsInChildren(true, found);
            foreach (var renderer in found)
            {
                if (!renderer || originals.ContainsKey(renderer) || renderer.transform.IsChildOf(transform)) continue;
                var materials = renderer.sharedMaterials;
                Material[] painted = null;
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (!material || !material.name.StartsWith(WorldMaterialPrefix, StringComparison.Ordinal)) continue;
                    painted ??= (Material[])materials.Clone();
                    painted[i] = runtimeMaterial;
                }
                if (painted == null) continue;
                originals[renderer] = materials;
                renderer.sharedMaterials = painted;
            }
            found.Clear();
        }

        void ForgetDestroyedRenderers()
        {
            found.Clear();
            foreach (var renderer in originals.Keys) if (!renderer) found.Add(renderer);
            foreach (var renderer in found) originals.Remove(renderer);
            found.Clear();
        }

        void OnDisable()
        {
            foreach (var item in originals) if (item.Key) item.Key.sharedMaterials = item.Value;
            originals.Clear();
            mapRoot = null;
            if (runtimeMaterial) Destroy(runtimeMaterial);
            if (builtCandy) Destroy(builtCandy);
            if (builtPlain) Destroy(builtPlain);
            runtimeMaterial = null; builtCandy = null; builtPlain = null;
        }
    }
}
