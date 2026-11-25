using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyArt
{
    // How the wave reacts to what it hits (presentation only, cheap enough for Quest):
    //  CPU, 20 times a second: blocks inside the wave are drawn into a small grid in wave space
    //   (128 across x 64 along). A block already riding the wave counts for nothing (it is a victim, not an
    //   obstacle); an anchored or heavy block is a solid obstacle. Top height is stored too.
    //  GPU, 60 steps a second: SurgeRipple.shader piles water up on each obstacle's upstream face, leaves a
    //   dip in its lee, and the damped wave equation lets the surrounding water refill the gap.
    //  The tsunami shader adds that ripple height, foams it and hides water inside obstacles; the strongest
    //   contacts throw spray from the existing pooled particles. Clients build this locally from the
    //   replicated block positions, so it costs no network traffic.
    public sealed partial class CandyTsunamiVisuals
    {
        const int GridX = 128, GridY = 64, MaxContacts = 8;
        Texture2D obstacleTexture;
        Color32[] obstaclePixels;
        RenderTexture rippleA, rippleB;
        Material rippleMaterial;
        float nextScan, rippleClock;
        bool rippleFailed;
        readonly Dictionary<Rigidbody, Collider> blockColliders = new Dictionary<Rigidbody, Collider>();
        readonly Vector3[] contactPoint = new Vector3[MaxContacts];
        readonly float[] contactStrength = new float[MaxContacts];
        int contactCount;
        static readonly int ObstaclesId = Shader.PropertyToID("_SurgeObstacles"), RippleId = Shader.PropertyToID("_SurgeRipple"),
            RippleReadyId = Shader.PropertyToID("_SurgeRippleReady"), RippleObstaclesId = Shader.PropertyToID("_Obstacles");

        bool BuildRipple()
        {
            if (rippleMaterial) return true;
            if (rippleFailed) return false;
            var shader = Resources.Load<Shader>("Tsunami/SurgeRipple");
            if (!shader || !shader.isSupported || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf)) { rippleFailed = true; return false; }
            rippleMaterial = new Material(shader) { name = "Tsunami ripple step (runtime)" };
            obstacleTexture = new Texture2D(GridX, GridY, TextureFormat.RGBA32, false, true)
                { name = "Tsunami obstacles", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            obstaclePixels = new Color32[GridX * GridY];
            rippleA = NewRipple("Tsunami ripple A"); rippleB = NewRipple("Tsunami ripple B");
            Shader.SetGlobalTexture(ObstaclesId, obstacleTexture);
            return true;
        }

        static RenderTexture NewRipple(string name)
        {
            var texture = new RenderTexture(GridX, GridY, 0, RenderTextureFormat.RGHalf) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.Create();
            var previous = RenderTexture.active; RenderTexture.active = texture; GL.Clear(false, true, Color.clear); RenderTexture.active = previous;
            return texture;
        }

        void UpdateRipple(bool shown)
        {
            if (!shown || !BuildRipple()) { Shader.SetGlobalFloat(RippleReadyId, 0); if (shown == false) ClearRipple(); return; }
            if (Time.time >= nextScan) { nextScan = Time.time + .05f; ScanObstacles(); EmitContacts(); }
            rippleMaterial.SetTexture(RippleObstaclesId, obstacleTexture);
            rippleMaterial.SetFloat("_Damping", .982f);
            rippleMaterial.SetFloat("_Speed", .2f);
            rippleMaterial.SetFloat("_Force", .11f * Mathf.Clamp01(wave.height / 8f));
            rippleClock = Mathf.Min(rippleClock + Time.deltaTime, 3 / 60f);
            while (rippleClock >= 1 / 60f)
            {
                rippleClock -= 1 / 60f;
                Graphics.Blit(rippleA, rippleB, rippleMaterial);
                (rippleA, rippleB) = (rippleB, rippleA);
            }
            Shader.SetGlobalTexture(RippleId, rippleA);
            Shader.SetGlobalFloat(RippleReadyId, 1);
        }

        void ClearRipple()
        {
            if (!rippleA) return;
            var previous = RenderTexture.active;
            foreach (var texture in new[] { rippleA, rippleB }) { RenderTexture.active = texture; GL.Clear(false, true, Color.clear); }
            RenderTexture.active = previous;
        }

        // Highest water (fraction of wave height) at a normalised wave-space point.
        float WaterFraction(float along, float lateral)
        {
            if (profile == null)
            {
                float t = along + .5f;
                float shape = t < .72f ? Mathf.SmoothStep(0, 1, t / .72f) : 1 - Mathf.SmoothStep(0, 1, (t - .72f) / .28f);
                return Mathf.Abs(lateral) > .5f || t <= 0 || t >= 1 ? 0 : shape;
            }
            float a = Mathf.Clamp01(along + .5f) * (profile.alongSamples - 1), l = Mathf.Clamp01(lateral + .5f) * (profile.lateralSamples - 1);
            int a0 = Mathf.Min((int)a, profile.alongSamples - 2), l0 = Mathf.Min((int)l, profile.lateralSamples - 2);
            float fa = a - a0, fl = l - l0;
            float E(int lat, int pos) => profile.envelope[lat * profile.alongSamples + pos];
            return Mathf.Lerp(Mathf.Lerp(E(l0, a0), E(l0, a0 + 1), fa), Mathf.Lerp(E(l0 + 1, a0), E(l0 + 1, a0 + 1), fa), fl);
        }

        void ScanObstacles()
        {
            System.Array.Clear(obstaclePixels, 0, obstaclePixels.Length);
            contactCount = 0;
            var world = Services.World;
            if (world != null && wave.width > 1 && wave.depth > 1)
            {
                Vector3 forward = Vector3.ProjectOnPlane(wave.direction, Vector3.up).normalized, across = Vector3.Cross(Vector3.up, forward);
                float speed = Mathf.Max(1, wave.speed), height = Mathf.Max(.1f, wave.height);
                var blocks = world.Blocks;
                for (int i = 0; i < blocks.Count; i++)
                {
                    var body = blocks[i];
                    if (!body) continue;
                    Vector3 offset = body.position - wave.center;
                    float along = Vector3.Dot(offset, forward) / wave.depth, lateral = Vector3.Dot(offset, across) / wave.width;
                    if (along < -.55f || along > .55f || lateral < -.55f || lateral > .55f) continue;
                    if (!blockColliders.TryGetValue(body, out var collider)) blockColliders[body] = collider = body.GetComponentInChildren<Collider>();
                    if (!collider) continue;
                    var bounds = collider.bounds;
                    float water = wave.waterHeight + height * WaterFraction(along, lateral);
                    if (bounds.min.y >= water) continue;
                    // Riding the wave = victim, not obstacle.
                    float carried = body.isKinematic ? 0 : Mathf.Clamp01(Vector3.Dot(body.linearVelocity, forward) / speed);
                    float weight = 1 - carried;
                    if (weight < .15f) continue;
                    Vector3 extents = bounds.extents;
                    float halfAlong = Mathf.Abs(extents.x * forward.x) + Mathf.Abs(extents.z * forward.z);
                    float halfLateral = Mathf.Abs(extents.x * across.x) + Mathf.Abs(extents.z * across.z);
                    int x0 = Mathf.Max(0, Mathf.FloorToInt((lateral - halfLateral / wave.width + .5f) * GridX));
                    int x1 = Mathf.Min(GridX - 1, Mathf.CeilToInt((lateral + halfLateral / wave.width + .5f) * GridX));
                    int y0 = Mathf.Max(0, Mathf.FloorToInt((along - halfAlong / wave.depth + .5f) * GridY));
                    int y1 = Mathf.Min(GridY - 1, Mathf.CeilToInt((along + halfAlong / wave.depth + .5f) * GridY));
                    byte w = (byte)(weight * 255), top = (byte)(Mathf.Clamp01((bounds.max.y - wave.waterHeight) / height) * 255);
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                        {
                            ref var pixel = ref obstaclePixels[y * GridX + x];
                            if (w > pixel.r) pixel.r = w;
                            if (top > pixel.g) pixel.g = top;
                        }
                    // Keep the strongest contacts for spray: heavy obstacles deep in fast water.
                    float strength = weight * Mathf.Clamp01((water - bounds.min.y) / 2f) * Mathf.Clamp01(1.2f - Mathf.Abs(along - .3f) * 1.4f);
                    if (strength > .12f)
                    {
                        var face = body.position - forward * halfAlong;   // upstream face, at the water line
                        face.y = Mathf.Min(water, bounds.max.y);
                        AddContact(face, strength);
                    }
                }
            }
            obstacleTexture.SetPixels32(obstaclePixels);
            obstacleTexture.Apply(false, false);
        }

        void AddContact(Vector3 point, float strength)
        {
            int slot = contactCount < MaxContacts ? contactCount++ : -1;
            if (slot < 0)
            {
                int weakest = 0;
                for (int i = 1; i < MaxContacts; i++) if (contactStrength[i] < contactStrength[weakest]) weakest = i;
                if (contactStrength[weakest] >= strength) return;
                slot = weakest;
            }
            contactPoint[slot] = point; contactStrength[slot] = strength;
        }

        // Splash-back: spray thrown up and back off the strongest obstacle faces.
        // A tall white column per contact: big mist sheets (spindrift system) shooting up the face and falling
        // back, plus a burst of droplets. Strength scales the height; the wave's own height scales everything.
        void EmitContacts()
        {
            if (!spray || contactCount == 0) return;
            if (!spray.isPlaying) spray.Play();
            if (spindrift && !spindrift.isPlaying) spindrift.Play();
            Vector3 forward = Vector3.ProjectOnPlane(wave.direction, Vector3.up).normalized, across = Vector3.Cross(Vector3.up, forward);
            float scale = Mathf.Clamp(wave.height / 12f, .5f, 2f);
            for (int c = 0; c < contactCount; c++)
            {
                float s = contactStrength[c];
                for (int k = 0; k < 9; k++)
                {
                    int seed = emissionSeed++;
                    spray.Emit(new ParticleSystem.EmitParams
                    {
                        position = contactPoint[c] + across * (FoamJitter(seed, 1, 3) - .5f) * 3 + Vector3.up * FoamJitter(seed, 2, 3),
                        velocity = (Vector3.up * (9 + 15 * s * FoamJitter(seed, 4, 5)) - forward * (3 + 7 * FoamJitter(seed, 5, 5)) + across * (FoamJitter(seed, 3, 3) - .5f) * 6) * scale,
                        startLifetime = 1.4f + FoamJitter(seed, 6, 2) * 1.1f,
                        startSize = (.3f + FoamJitter(seed, 7, 3) * .9f) * scale,
                        rotation = FoamJitter(seed, 8, 8) * 360
                    }, 1);
                }
                if (!spindrift) continue;
                for (int k = 0; k < 3; k++)
                {
                    int seed = emissionSeed++;
                    spindrift.Emit(new ParticleSystem.EmitParams
                    {
                        position = contactPoint[c] + across * (FoamJitter(seed, 1, 3) - .5f) * 4 + Vector3.up * FoamJitter(seed, 2, 3) * 2,
                        velocity = (Vector3.up * (8 + 12 * s * FoamJitter(seed, 4, 5)) - forward * (2 + 4 * FoamJitter(seed, 5, 5))) * scale,
                        startLifetime = 1.6f + FoamJitter(seed, 6, 2) * 1.2f,
                        startSize = (2.5f + FoamJitter(seed, 7, 3) * 3.5f) * scale,
                        rotation = FoamJitter(seed, 8, 8) * 360
                    }, 1);
                }
            }
        }

        void ReleaseRipple()
        {
            Shader.SetGlobalFloat(RippleReadyId, 0);
            if (rippleA) rippleA.Release();
            if (rippleB) rippleB.Release();
            Release(rippleA); Release(rippleB); Release(obstacleTexture); Release(rippleMaterial);
        }
    }
}
