using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyArt
{
    // Meteor v2 entry plume: a long fiery streak behind each meteor, aligned to its flight and turned to face the
    // camera around that axis, playing the Blender plume flipbook (MeteorTailFlipbook). Length scales with size;
    // the Planet Killer's plume is longer and fatter. Icy meteors tint it to white-blue vapour.
    public sealed partial class CandyDisasterVisuals
    {
        readonly List<Matrix4x4> meteorTails = new List<Matrix4x4>(128);
        Material tailMaterial;
        bool tailChecked;

        bool HasMeteorTail()
        {
            if (!tailChecked)
            {
                tailChecked = true;
                var atlas = Resources.Load<Texture2D>("CandyScenery/MeteorTailFlipbook");
                var shader = Resources.Load<Shader>("CandyScenery/CandyMeteorTail");
                if (atlas && shader && shader.isSupported) { tailMaterial = new Material(shader) { name = "Meteor plume (runtime)", enableInstancing = true }; tailMaterial.SetTexture("_TailAtlas", atlas); }
            }
            return tailMaterial;
        }

        void AddMeteorTail(Vector3 position, Vector3 velocity, float size, bool planet)
        {
            if (velocity.sqrMagnitude < .01f) return;
            var camera = Services.Session?.ActiveCamera; if (!camera) return;
            Vector3 dir = velocity.normalized;
            float length = size * (planet ? 6f : 8f), width = size * (planet ? 2.2f : 1.7f);
            Vector3 centre = position - dir * (length * .5f - size * .55f);   // the head sits just in front of the rock
            Vector3 toCamera = (camera.transform.position - centre).normalized;
            Vector3 up = Vector3.Cross(toCamera, dir); if (up.sqrMagnitude < .0001f) up = Vector3.Cross(Vector3.up, dir);
            up.Normalize(); Vector3 normal = Vector3.Cross(dir, up);
            var m = new Matrix4x4();
            m.SetColumn(0, dir * length); m.SetColumn(1, up * width); m.SetColumn(2, normal); m.SetColumn(3, new Vector4(centre.x, centre.y, centre.z, 1));
            meteorTails.Add(m);
        }

        void RenderMeteorTails()
        {
            if (meteorTails.Count > 0 && tailMaterial)
            {
                tailMaterial.SetColor("_TailTint", IcyMeteors() ? new Color(.62f, .85f, 1f, .85f) : Color.white);
                DrawObjects(meteorTails, quad, tailMaterial);
            }
            meteorTails.Clear();
        }

        void DestroyMeteorTails() { if (tailMaterial) Destroy(tailMaterial); }
    }
}
