using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyArt
{
    // Icey: one universal frost look for every block on every map.
    //  - Each frozen block renderer gets an extra material pass (CandyFrostOverlay) that paints the Blender frost
    //    tile in world space, so block shape, UVs and material do not matter. _FrostAmount spreads it in.
    //  - The candy colour is washed slightly toward ice blue and made glossy (property block, as before).
    //    A property block also keeps StructuralMeshBatches from hiding the renderer, so repeated pieces freeze too.
    //  - Blocks with open space below get icicles (Blender icicle kit, instanced) along their long bottom edges.
    public sealed partial class CandyDisasterVisuals
    {
        struct Frozen { public Material[] materials; public MaterialPropertyBlock original; public float since; }
        struct Icicle { public Transform anchor; public Matrix4x4 local; public int mesh; public float since; }

        readonly Dictionary<Renderer, Frozen> frozen = new Dictionary<Renderer, Frozen>();
        readonly List<Icicle> icicles = new List<Icicle>();
        readonly List<Matrix4x4>[] icicleDraws = { new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>() };
        readonly HashSet<Rigidbody> icicleChecked = new HashSet<Rigidbody>();
        Material frostOverlay, icicleMaterial;
        Mesh[] icicleKit;
        MaterialPropertyBlock frostBlock;
        float nextFrostUpdate;
        const int MaxIcicles = 900, MobileMaxIcicles = 360;
        const float FreezeSeconds = 4f;

        [System.Serializable] class KitMesh { public string name; public float[] vertices, normals; public int[] triangles; }
        [System.Serializable] class Kit { public KitMesh[] meshes; }

        void BuildIce()
        {
            if (frostOverlay) return;
            var overlay = Resources.Load<Shader>("CandyScenery/CandyFrostOverlay");
            frostOverlay = new Material(overlay ? overlay : Shader.Find("Universal Render Pipeline/Unlit")) { name = "Frost overlay (runtime)" };
            var tile = Resources.Load<Texture2D>("CandyScenery/FrostTile");
            if (tile) frostOverlay.SetTexture("_FrostTex", tile);
            var iceShader = Resources.Load<Shader>("CandyScenery/CandyIcicle");
            if (iceShader) icicleMaterial = new Material(iceShader) { name = "Icicles (runtime)", enableInstancing = true };
            var json = Resources.Load<TextAsset>("CandyScenery/IcicleKit");
            if (json)
            {
                var kit = JsonUtility.FromJson<Kit>(json.text);
                icicleKit = new Mesh[kit.meshes.Length];
                for (int k = 0; k < kit.meshes.Length; k++)
                {
                    var m = kit.meshes[k]; int count = m.vertices.Length / 3;
                    var v = new Vector3[count]; var n = new Vector3[count];
                    for (int i = 0; i < count; i++) { v[i] = new Vector3(m.vertices[i * 3], m.vertices[i * 3 + 1], m.vertices[i * 3 + 2]); n[i] = new Vector3(m.normals[i * 3], m.normals[i * 3 + 1], m.normals[i * 3 + 2]); }
                    icicleKit[k] = new Mesh { name = "Icicle " + m.name, vertices = v, normals = n, triangles = m.triangles };
                    icicleKit[k].RecalculateBounds();
                }
            }
        }

        void Ice()
        {
            BuildIce();
            if (Time.time >= nextIceScan)
            {
                nextIceScan = Time.time + 1;
                FreezeNewBlocks();
            }
            if (Time.time >= nextFrostUpdate)
            {
                nextFrostUpdate = Time.time + .1f;
                foreach (var pair in frozen)
                {
                    if (!pair.Key) continue;
                    float amount = Mathf.Clamp01((Time.time - pair.Value.since) / FreezeSeconds);
                    if (amount >= 1 && Time.time - pair.Value.since > FreezeSeconds + .2f) continue;   // fully frozen: leave it
                    pair.Key.GetPropertyBlock(frostBlock); frostBlock.SetFloat("_FrostAmount", Mathf.SmoothStep(0, 1, amount)); pair.Key.SetPropertyBlock(frostBlock);
                }
            }
            DrawIcicles();
        }

        void FreezeNewBlocks()
        {
            frostBlock ??= new MaterialPropertyBlock();
            var world = Services.World; if (world == null) return;
            int limit = Application.isMobilePlatform ? MobileMaxIcicles : MaxIcicles;
            foreach (var body in world.Blocks)
            {
                if (!body) continue;
                var visual = body.GetComponent<IClimbable>()?.Visual; if (!visual) continue;
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
                {
                    if (frozen.ContainsKey(renderer) || renderer is ParticleSystemRenderer) continue;
                    var original = new MaterialPropertyBlock(); renderer.GetPropertyBlock(original);
                    var materials = renderer.sharedMaterials;
                    frozen.Add(renderer, new Frozen { materials = materials, original = original, since = Time.time });
                    var material = materials.Length > 0 ? materials[0] : null;
                    Color color = material && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material && material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                    renderer.GetPropertyBlock(frostBlock);
                    var tint = Color.Lerp(color, new Color(.72f, .9f, 1f, color.a), .4f);
                    frostBlock.SetColor("_BaseColor", tint); frostBlock.SetColor("_Color", tint);
                    frostBlock.SetFloat("_Smoothness", .95f); frostBlock.SetFloat("_Glossiness", .95f); frostBlock.SetFloat("_FrostAmount", 0);
                    renderer.SetPropertyBlock(frostBlock);
                    var withFrost = new Material[materials.Length + 1];
                    materials.CopyTo(withFrost, 0); withFrost[materials.Length] = frostOverlay;
                    renderer.sharedMaterials = withFrost;
                }
                if (icicleKit != null && icicleKit.Length > 0 && icicles.Count < limit && icicleChecked.Add(body)) AddIcicles(body, visual, limit);
            }
        }

        void AddIcicles(Rigidbody body, Transform visual, int limit)
        {
            var renderer = visual.GetComponentInChildren<Renderer>(); if (!renderer) return;
            var bounds = renderer.bounds;
            var bottom = new Vector3(bounds.center.x, bounds.min.y + .02f, bounds.center.z);
            int mask = (1 << Layers.Block) | (1 << Layers.Surface);
            if (Physics.Raycast(bottom, Vector3.down, .4f, mask, QueryTriggerInteraction.Ignore)) return;   // resting on something: no room
            bool alongX = bounds.size.x >= bounds.size.z;
            float length = alongX ? bounds.size.x : bounds.size.z, inset = .08f;
            int count = Mathf.Clamp(Mathf.RoundToInt(length / .5f), 1, 8);
            int seed = body.GetInstanceID();
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < count && icicles.Count < limit; i++)
                {
                    float t = (i + .5f) / count - .5f, h = Hash(seed + i * 7 + side * 31);
                    var offset = alongX ? new Vector3(t * (length - inset * 2), 0, side * (bounds.extents.z - inset)) : new Vector3(side * (bounds.extents.x - inset), 0, t * (length - inset * 2));
                    var world = new Vector3(bounds.center.x, bounds.min.y + .01f, bounds.center.z) + offset;
                    float size = .35f + h * .55f;
                    var worldMatrix = Matrix4x4.TRS(world, Quaternion.Euler(0, h * 360, 0), new Vector3(size, size * (.7f + Hash(seed + i) * .8f), size));
                    icicles.Add(new Icicle { anchor = body.transform, local = body.transform.worldToLocalMatrix * worldMatrix, mesh = (int)(h * 97) % icicleKit.Length, since = Time.time + h * 1.5f });
                }
        }

        void DrawIcicles()
        {
            if (icicleKit == null || !icicleMaterial) return;
            foreach (var list in icicleDraws) list.Clear();
            for (int i = icicles.Count - 1; i >= 0; i--)
            {
                var icicle = icicles[i];
                if (!icicle.anchor) { icicles.RemoveAt(i); continue; }
                float grow = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.time - icicle.since) / FreezeSeconds));
                if (grow <= .01f) continue;
                icicleDraws[icicle.mesh % icicleDraws.Length].Add(icicle.anchor.localToWorldMatrix * icicle.local * Matrix4x4.Scale(new Vector3(1, grow, 1)));
            }
            for (int k = 0; k < icicleKit.Length && k < icicleDraws.Length; k++) DrawObjects(icicleDraws[k], icicleKit[k], icicleMaterial);
        }

        void RestoreIce()
        {
            foreach (var pair in frozen)
            {
                if (!pair.Key) continue;
                pair.Key.sharedMaterials = pair.Value.materials;
                pair.Key.SetPropertyBlock(pair.Value.original);
            }
            frozen.Clear(); icicles.Clear(); icicleChecked.Clear(); nextIceScan = 0;
        }

        void DestroyIce()
        {
            if (frostOverlay) Destroy(frostOverlay);
            if (icicleMaterial) Destroy(icicleMaterial);
            if (icicleKit != null) foreach (var m in icicleKit) if (m) Destroy(m);
        }
    }
}
