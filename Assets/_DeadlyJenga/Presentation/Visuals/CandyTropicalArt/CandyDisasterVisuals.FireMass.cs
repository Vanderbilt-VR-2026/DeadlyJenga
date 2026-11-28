using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyArt
{
    // Fire v2 engulfing look: one big upright billboard per burning cluster, playing the Blender fire-simulation
    // flipbook (FireMassFlipbook: billowing flames + black smoke column). The small flame tongues stay as licking
    // detail on top. Only the nearest clusters get a billboard (overdraw budget for Quest).
    public sealed partial class CandyDisasterVisuals
    {
        const int MaxFireMasses = 16, MobileMaxFireMasses = 6;
        readonly List<Matrix4x4> fireMasses = new List<Matrix4x4>(MaxFireMasses);
        readonly List<Vector3> fireMassCentres = new List<Vector3>(MaxFireMasses);
        readonly List<FlameSubject> fireMassOrder = new List<FlameSubject>(MaxFlameSubjects);
        Material fireMassMaterial;
        bool fireMassChecked;

        void BuildFireMasses()
        {
            fireMasses.Clear(); fireMassCentres.Clear();
            if (!fireMassChecked)
            {
                fireMassChecked = true;
                var atlas = Resources.Load<Texture2D>("CandyScenery/FireMassFlipbook");
                var shader = Resources.Load<Shader>("CandyScenery/CandyFireBillboard");
                if (atlas && shader && shader.isSupported)
                {
                    fireMassMaterial = new Material(shader) { name = "Fire mass billboard (runtime)", enableInstancing = true };
                    fireMassMaterial.SetTexture("_FireAtlas", atlas);
                }
            }
            if (!fireMassMaterial || flameSubjects.Count == 0) return;
            var camera = Services.Session?.ActiveCamera;
            if (!camera) return;
            Vector3 facing = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
            if (facing.sqrMagnitude < .001f) facing = Vector3.forward;
            var upright = Quaternion.LookRotation(facing.normalized, Vector3.up);
            fireMassOrder.Clear(); fireMassOrder.AddRange(flameSubjects); fireMassOrder.Sort(flameDistanceOrder);
            int limit = Application.isMobilePlatform ? MobileMaxFireMasses : MaxFireMasses;
            foreach (var subject in fireMassOrder)
            {
                if (fireMasses.Count >= limit) break;
                var bounds = subject.bounds;
                // One billboard per cluster: skip blocks right next to one that already has a fire mass.
                bool covered = false;
                foreach (var centre in fireMassCentres)
                {
                    var d = centre - bounds.center;
                    if (new Vector2(d.x, d.z).sqrMagnitude < 2.6f * 2.6f && Mathf.Abs(d.y) < 1.4f) { covered = true; break; }
                }
                if (covered) continue;
                float size = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z), 1f, 6f);
                float height = size * 3.4f, width = height * .5f;   // the flipbook cell is 1:2
                var bottom = new Vector3(bounds.center.x, bounds.min.y - .1f, bounds.center.z);
                // Nudge slightly toward the camera so the block's own front face doesn't depth-fade out the flame base.
                var center = bottom + Vector3.up * height * .5f - facing.normalized * (size * .45f);
                fireMasses.Add(Matrix4x4.TRS(center, upright, new Vector3(width, height, 1)));
                fireMassCentres.Add(bounds.center);
            }
            if (fireMasses.Count > 0) DrawObjects(fireMasses, quad, fireMassMaterial);
        }

        void DestroyFireMasses() { if (fireMassMaterial) Destroy(fireMassMaterial); }
    }
}
