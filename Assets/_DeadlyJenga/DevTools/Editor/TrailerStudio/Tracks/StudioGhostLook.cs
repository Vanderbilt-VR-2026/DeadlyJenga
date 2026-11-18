using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // How a played-back actor looks to someone performing next to it:
    // a normal character, a see-through ghost in the actor's color, or hidden.
    // It can also be made pass-through, so the performer does not bump into it.
    public sealed class StudioGhostLook : IDisposable
    {
        Material ghost;
        int applied = -1, counted = -1;
        readonly List<Renderer> renderers = new List<Renderer>();
        readonly List<Collider> colliders = new List<Collider>();

        public void Apply(GameObject root, StudioTrackNotes notes, Color color)
        {
            // The copy builds its body a little after it is created, so look again when the count changes.
            int count = root.transform.hierarchyCount;
            if (count != counted)
            {
                counted = count; applied = -1;
                renderers.Clear(); colliders.Clear();
                root.GetComponentsInChildren(true, renderers);
                root.GetComponentsInChildren(true, colliders);
            }
            if (notes.passThrough) foreach (var collider in colliders) if (collider && collider.enabled) collider.enabled = false;
            if (notes.ghost == applied) return;
            applied = notes.ghost;
            if (notes.ghost == 0) return;   // normal look: leave the character as the game made it
            if (notes.ghost == 1 && !ghost)
            {
                ghost = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Studio ghost", hideFlags = HideFlags.HideAndDontSave };
                ghost.SetFloat("_Surface", 1);
                ghost.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                ghost.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                ghost.SetInt("_ZWrite", 0);
                ghost.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                ghost.renderQueue = 3000;
                ghost.SetColor("_BaseColor", new Color(color.r, color.g, color.b, .38f));
            }
            foreach (var renderer in renderers)
            {
                if (!renderer || renderer is ParticleSystemRenderer) continue;
                if (notes.ghost == 2) { renderer.forceRenderingOff = true; continue; }
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = ghost;
                renderer.sharedMaterials = materials;
            }
        }

        public void Dispose()
        {
            if (ghost) UnityEngine.Object.Destroy(ghost);
        }
    }
}
