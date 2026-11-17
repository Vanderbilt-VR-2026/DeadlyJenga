using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    // Distant toy tug boats (classic 3D-printer test-boat shape) sailing around the Castle/Tycoon coast.
    // Glossy candy white body with red trim. Visual only: no colliders, built from primitives once.
    internal sealed class CandyBenchyFleet
    {
        sealed class Boat { public Transform root; public float radius, angle, speed, bob; public bool clockwise; }

        readonly GameObject root;
        readonly List<Boat> boats = new();
        readonly Material white, red, dark;

        public CandyBenchyFleet(Transform parent)
        {
            root = new GameObject("Toy boat fleet · visual only");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            white = Candy(lit, new Color(.97f, .96f, .95f));
            red = Candy(lit, new Color(.86f, .06f, .09f));
            dark = Candy(lit, new Color(.08f, .1f, .14f));
            // Far rings only, so boats never come near the island or its reefs.
            (float radius, float scale, float speed, float start, bool clockwise)[] fleet =
            {
                (175, 4.2f, 2.6f, 20, false), (210, 5.0f, 2.2f, 140, true), (245, 5.6f, 1.9f, 250, false), (195, 3.6f, 2.9f, 310, true)
            };
            int i = 0;
            foreach (var f in fleet)
            {
                var boat = Build("Toy boat " + (++i), f.scale);
                boat.SetParent(root.transform, false);
                boats.Add(new Boat { root = boat, radius = f.radius, angle = f.start * Mathf.Deg2Rad, speed = f.speed, bob = i * 1.7f, clockwise = f.clockwise });
            }
        }

        static Material Candy(Shader shader, Color color)
        {
            var m = new Material(shader) { name = "Candy toy boat", enableInstancing = true };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", .94f);
            m.SetFloat("_Metallic", .02f);
            return m;
        }

        Transform Build(string name, float s)
        {
            var boat = new GameObject(name).transform;
            void Part(PrimitiveType type, Vector3 position, Vector3 size, Material material, Vector3 euler = default)
            {
                var part = GameObject.CreatePrimitive(type);
                Object.Destroy(part.GetComponent<Collider>());
                part.name = name + " part";
                part.transform.SetParent(boat, false);
                part.transform.localPosition = position * s;
                part.transform.localRotation = Quaternion.Euler(euler);
                part.transform.localScale = size * s;
                var r = part.GetComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            // Rounded hull with a raised bow, red waterline stripe and deck rail.
            Part(PrimitiveType.Capsule, new(0, .35f, 0), new(1.5f, 1.5f, 1.25f), white, new(90, 0, 0));
            Part(PrimitiveType.Sphere, new(0, .65f, 1.05f), new(1.35f, .8f, 1.1f), white);
            Part(PrimitiveType.Cube, new(0, .05f, 0), new(1.52f, .12f, 2.7f), red);
            Part(PrimitiveType.Cube, new(0, 1.0f, 0), new(1.55f, .1f, 2.8f), red);
            // Cabin with round windows, a doorway and the slanted roof.
            Part(PrimitiveType.Cube, new(0, 1.55f, -.25f), new(1.1f, 1.0f, 1.25f), white);
            Part(PrimitiveType.Cube, new(0, 2.12f, -.15f), new(1.3f, .14f, 1.6f), red, new(-8, 0, 0));
            for (int side = -1; side <= 1; side += 2)
            {
                Part(PrimitiveType.Cylinder, new(side * .56f, 1.62f, -.05f), new(.32f, .02f, .32f), dark, new(0, 0, 90));
                Part(PrimitiveType.Cylinder, new(side * .56f, 1.62f, -.55f), new(.32f, .02f, .32f), dark, new(0, 0, 90));
            }
            Part(PrimitiveType.Cube, new(0, 1.5f, .37f), new(.5f, .7f, .02f), dark);
            Part(PrimitiveType.Cube, new(0, 1.5f, -.88f), new(.5f, .7f, .02f), dark);
            // Smokestack with a red band, plus a little bow box.
            Part(PrimitiveType.Cylinder, new(0, 2.45f, -.75f), new(.32f, .35f, .32f), white);
            Part(PrimitiveType.Cylinder, new(0, 2.8f, -.75f), new(.34f, .06f, .34f), red);
            Part(PrimitiveType.Cube, new(0, 1.15f, .95f), new(.4f, .22f, .4f), red);
            return boat;
        }

        public void Animate()
        {
            if (!root) return;
            float dt = Time.deltaTime, time = Time.time;
            foreach (var b in boats)
            {
                float sign = b.clockwise ? -1 : 1;
                b.angle += sign * b.speed / b.radius * dt;
                var position = new Vector3(Mathf.Cos(b.angle) * b.radius, 0, Mathf.Sin(b.angle) * b.radius);
                var forward = new Vector3(-Mathf.Sin(b.angle), 0, Mathf.Cos(b.angle)) * sign;
                position.y = Mathf.Sin(time * .9f + b.bob) * .08f;
                b.root.localPosition = position;
                b.root.localRotation = Quaternion.LookRotation(forward) *
                    Quaternion.Euler(Mathf.Sin(time * .7f + b.bob) * 1.5f, 0, Mathf.Sin(time * .9f + b.bob) * 2.5f);
            }
        }

        public void Dispose()
        {
            if (root) Object.Destroy(root);
            Object.Destroy(white); Object.Destroy(red); Object.Destroy(dark);
        }
    }
}
