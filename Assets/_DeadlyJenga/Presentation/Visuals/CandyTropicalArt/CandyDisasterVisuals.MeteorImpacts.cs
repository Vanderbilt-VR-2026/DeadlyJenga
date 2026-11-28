using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    // Meteor impacts: jagged Blender shards (MeteorShardKit) scatter instead of round chips, every burst plays a
    // Blender explosion flipbook once (ImpactExplosion: blast -> rolling fireball -> smoke) and leaves a billowing dust
    // plume (ImpactDust). Cosmetic only, bounded pools.
    public sealed partial class CandyDisasterVisuals
    {
        struct Impact { public Vector3 position; public float born, size; public bool planet; public int seed; }
        const int MaxImpacts = 24;
        readonly List<Impact> impacts = new List<Impact>(MaxImpacts);
        readonly List<Matrix4x4> impactFlashes = new List<Matrix4x4>(MaxImpacts), impactDust = new List<Matrix4x4>(MaxImpacts * 3);
        List<Matrix4x4>[] shardDraws;
        Mesh[] shardKit;
        Material flashMaterial, dustMaterial, blastMaterial;
        bool impactKitLoaded;

        [System.Serializable] class ShardMesh { public string name; public float[] vertices, normals; public int[] triangles; }
        [System.Serializable] class ShardKit { public ShardMesh[] meshes; }

        void LoadImpactKit()
        {
            if (impactKitLoaded) return;
            impactKitLoaded = true;
            var json = Resources.Load<TextAsset>("CandyScenery/MeteorShardKit");
            if (json)
            {
                var kit = JsonUtility.FromJson<ShardKit>(json.text);
                shardKit = new Mesh[kit.meshes.Length]; shardDraws = new List<Matrix4x4>[kit.meshes.Length];
                for (int k = 0; k < kit.meshes.Length; k++)
                {
                    var m = kit.meshes[k]; int count = m.vertices.Length / 3;
                    var v = new Vector3[count]; var n = new Vector3[count];
                    for (int i = 0; i < count; i++) { v[i] = new Vector3(m.vertices[i * 3], m.vertices[i * 3 + 1], m.vertices[i * 3 + 2]); n[i] = new Vector3(m.normals[i * 3], m.normals[i * 3 + 1], m.normals[i * 3 + 2]); }
                    shardKit[k] = new Mesh { name = m.name, vertices = v, normals = n, triangles = m.triangles }; shardKit[k].RecalculateBounds();
                    shardDraws[k] = new List<Matrix4x4>(128);
                }
            }
            var blastAtlas = Resources.Load<Texture2D>("CandyScenery/ImpactExplosion");
            var blastShader = Resources.Load<Shader>("CandyScenery/CandyImpactBillboard");
            if (blastAtlas && blastShader && blastShader.isSupported) { blastMaterial = new Material(blastShader) { name = "Meteor impact blast", enableInstancing = true }; blastMaterial.SetTexture("_BlastAtlas", blastAtlas); }
            if (!stormMist) return;
            flashMaterial = new Material(stormMist) { name = "Meteor impact flash" };
            dustMaterial = new Material(stormMist) { name = "Meteor impact dust" };
            var flash = Resources.Load<Texture2D>("CandyScenery/ImpactFlash");
            var dustTex = Resources.Load<Texture2D>("CandyScenery/ImpactDust");
            if (flash) { flashMaterial.SetTexture("_CloudTexture", flash); flashMaterial.SetFloat("_UseCloudTexture", 1); }
            if (dustTex) { dustMaterial.SetTexture("_CloudTexture", dustTex); dustMaterial.SetFloat("_UseCloudTexture", 1); }
            flashMaterial.SetColor("_BaseColor", new Color(1f, .8f, .5f, 1f));
            dustMaterial.SetColor("_BaseColor", new Color(.62f, .54f, .46f, .8f));
        }

        void AddImpact(Vector3 position, float size, bool planet)
        {
            LoadImpactKit();
            if (impacts.Count >= MaxImpacts) impacts.RemoveAt(0);
            impacts.Add(new Impact { position = position, born = Time.time, size = size, planet = planet, seed = impacts.Count * 31 + (int)(position.x * 7) });
        }

        // Shard mesh for a scattering fragment, or null to keep the old rock chip.
        List<Matrix4x4> ShardList(int seed) => shardKit == null || shardKit.Length == 0 ? null : shardDraws[(seed & 0x7fffffff) % shardKit.Length];

        void RenderImpacts(Material chipLook)
        {
            impactFlashes.Clear(); impactDust.Clear();
            for (int i = impacts.Count - 1; i >= 0; i--)
            {
                var impact = impacts[i]; float age = Time.time - impact.born;
                float scale = impact.planet ? Mathf.Clamp(impact.size * .9f, 12, 140) : Mathf.Clamp(impact.size * 3.5f, 2, 30);
                float dustLife = impact.planet ? 6 : 2.6f;
                if (age > dustLife) { impacts.RemoveAt(i); continue; }
                float blastLife = impact.planet ? 3.2f : 1.7f;
                if (blastMaterial && age < blastLife)
                {
                    // Real blast flipbook, played once; the age rides in the Z scale (see CandyImpactBillboard).
                    float blastSize = scale * 1.5f;
                    var cam = DeadlyJenga.Core.Services.Session?.ActiveCamera;
                    Vector3 facing = cam ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up) : Vector3.forward;
                    if (facing.sqrMagnitude < .001f) facing = Vector3.forward;
                    impactFlashes.Add(Matrix4x4.TRS(impact.position + Vector3.up * blastSize * .42f, Quaternion.LookRotation(facing.normalized), new Vector3(blastSize, blastSize, Mathf.Max(.001f, age / blastLife))));
                }
                else if (!blastMaterial && age < .4f)
                {
                    float t = age / .4f, s = scale * (.4f + t * .9f) * (1 - t * t);
                    Puff(impactFlashes, impact.position + Vector3.up * s * .25f, s, s, impact.seed * 13);
                }
                float d = age / dustLife;
                for (int k = 0; k < 3; k++)
                {
                    float spread = scale * (.5f + d * 1.3f) * (1 - d * .25f), lift = scale * (.15f + d * .55f) * (k * .45f + .3f);
                    var offset = new Vector3(Mathf.Sin(impact.seed + k * 2.1f), 0, Mathf.Cos(impact.seed + k * 2.1f)) * spread * .35f * k;
                    float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, 1, d));
                    Puff(impactDust, impact.position + offset + Vector3.up * lift, spread * fade, spread * .8f * fade, impact.seed * 7 + k * 40);
                }
            }
            if (impactDust.Count > 0 && dustMaterial) DrawObjects(impactDust, quad, dustMaterial);
            if (impactFlashes.Count > 0) { var look = blastMaterial ? blastMaterial : flashMaterial; if (look) DrawObjects(impactFlashes, quad, look); }
            if (shardKit != null)
                for (int k = 0; k < shardKit.Length; k++) { if (shardDraws[k].Count > 0) DrawObjects(shardDraws[k], shardKit[k], chipLook); shardDraws[k].Clear(); }
        }

        void DestroyImpacts()
        {
            if (flashMaterial) Destroy(flashMaterial);
            if (blastMaterial) Destroy(blastMaterial);
            if (dustMaterial) Destroy(dustMaterial);
            if (shardKit != null) foreach (var m in shardKit) if (m) Destroy(m);
        }
    }
}
