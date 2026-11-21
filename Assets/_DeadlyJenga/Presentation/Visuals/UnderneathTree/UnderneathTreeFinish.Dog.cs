using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    // Where the sofa used to be: a giant golden retriever asleep in a plaid dog bed,
    // head on its paws, facing the tree. Scenery only (no collision). Its body breathes slowly.
    public sealed partial class UnderneathTreeFinish
    {
        Transform dogBreath;

        void BuildDogBed()
        {
            var plaid = Fabric("Plaid dog bed", "Cranberry Rug Weave", new Color(1.25f, .85f, .85f), new Vector2(10f, 2f), .1f, .6f);
            var fleece = Fabric("Dog bed fleece", "Fleece Scan", new Color(.95f, .86f, .72f), new Vector2(4f, 6f), .2f, .8f);
            var fur = Fabric("Golden retriever fur", "Fleece Scan", new Color(.95f, .62f, .30f), new Vector2(3f, 3f), .3f, .7f);
            var cream = Fabric("Golden retriever feathering", "Fleece Scan", new Color(1.05f, .80f, .50f), new Vector2(3f, 3f), .3f, .7f);
            var nose = Surface("Dog nose", null, new Color(.05f, .035f, .03f), .75f, Vector2.one);
            var lids = Surface("Sleeping eyelids", null, new Color(.16f, .09f, .05f), .3f, Vector2.one);

            // Head toward the tree (north), turned a little toward the room.
            var bed = Pivot("Dog bed with sleeping retriever", new Vector3(RoomEast - 85f, 0f, SofaZ - 8f), -12f);
            bed.localScale = Vector3.one * 1.15f;

            Under(Primitive("Dog bed base", PrimitiveType.Cylinder, new Vector3(0f, 2f, 0f), new Vector3(128f, 2f, 222f), plaid), bed);
            Under(Primitive("Dog bed cushion", PrimitiveType.Cylinder, new Vector3(0f, 6f, 0f), new Vector3(104f, 4f, 196f), fleece), bed);
            var bolster = AddMesh("Dog bed bolster", Bolster(56f, 102f, 17f, 15f), plaid, ShadowCastingMode.On).transform;
            Under(bolster, bed).localPosition = new Vector3(0f, 15f, 0f);

            var dog = new GameObject("Sleeping golden retriever").transform;
            dog.SetParent(bed, false);
            dogBreath = new GameObject("Breathing body").transform;
            dogBreath.SetParent(dog, false);

            Transform Part(string label, PrimitiveType type, Vector3 at, Vector3 size, Material material, Transform parent, Vector3 turn = default)
            {
                var part = Under(Primitive(label, type, at, size, material), parent);
                part.localRotation = Quaternion.Euler(turn);
                return part;
            }

            // Body, lying on its belly with the hips rolled onto the right side.
            Part("Retriever back", PrimitiveType.Sphere, new Vector3(2f, 33f, -8f), new Vector3(54f, 42f, 96f), fur, dogBreath);
            Part("Retriever chest", PrimitiveType.Sphere, new Vector3(0f, 30f, 36f), new Vector3(48f, 38f, 44f), fur, dogBreath);
            Part("Retriever chest feathering", PrimitiveType.Sphere, new Vector3(-3f, 22f, 44f), new Vector3(40f, 24f, 30f), cream, dogBreath);
            Part("Retriever neck", PrimitiveType.Sphere, new Vector3(0f, 33f, 58f), new Vector3(38f, 34f, 36f), fur, dogBreath);
            Part("Retriever hips", PrimitiveType.Sphere, new Vector3(8f, 31f, -46f), new Vector3(52f, 40f, 52f), fur, dogBreath);
            Part("Retriever back thigh", PrimitiveType.Sphere, new Vector3(26f, 25f, -40f), new Vector3(22f, 30f, 46f), fur, dogBreath, new Vector3(0f, 0f, -18f));
            Part("Retriever back paw", PrimitiveType.Sphere, new Vector3(29f, 14f, -14f), new Vector3(14f, 9f, 20f), cream, dog);

            // Front legs stretched forward, head resting between the paws.
            foreach (int side in new[] { -1, 1 })
            {
                Part("Retriever front leg", PrimitiveType.Capsule, new Vector3(side * 10f, 16f, 58f), new Vector3(12f, 20f, 12f), fur, dog, new Vector3(90f, 0f, 0f));
                Part("Retriever front paw", PrimitiveType.Sphere, new Vector3(side * 10f, 15f, 80f), new Vector3(15f, 10f, 17f), cream, dog);
                Part("Retriever floppy ear", PrimitiveType.Sphere, new Vector3(side * 17f, 29f, 78f), new Vector3(6f, 26f, 18f), fur, dog, new Vector3(0f, 0f, side * 12f));
                Part("Closed eye", PrimitiveType.Sphere, new Vector3(side * 8f, 36f, 95.5f), new Vector3(7f, 1.2f, 2.5f), lids, dog, new Vector3(0f, side * -20f, side * -8f));
            }
            Part("Retriever head", PrimitiveType.Sphere, new Vector3(0f, 30f, 80f), new Vector3(34f, 30f, 36f), fur, dog);
            Part("Retriever muzzle", PrimitiveType.Sphere, new Vector3(0f, 26f, 97f), new Vector3(19f, 16f, 26f), cream, dog);
            Part("Retriever nose", PrimitiveType.Sphere, new Vector3(0f, 29f, 110f), new Vector3(8f, 6f, 6f), nose, dog);

            // A feathery tail curled round the hips toward the paws.
            var tail = new[] { new Vector3(-2f, 26f, -76f), new Vector3(6f, 20f, -90f), new Vector3(20f, 16f, -94f),
                new Vector3(34f, 14f, -86f), new Vector3(44f, 13f, -70f), new Vector3(48f, 12f, -52f) };
            for (int i = 0; i < tail.Length - 1; i++)
            {
                var from = tail[i];
                var to = tail[i + 1];
                float radius = Mathf.Lerp(8f, 5f, i / (tail.Length - 2f));
                var piece = Part("Retriever tail", PrimitiveType.Capsule, (from + to) * .5f,
                    new Vector3(radius * 2f, ((to - from).magnitude + radius * 2f) * .5f, radius * 2f), i == tail.Length - 2 ? cream : fur, dog);
                piece.localRotation = Quaternion.LookRotation(to - from) * Quaternion.Euler(90f, 0f, 0f);
            }
        }

        // A soft oval ring, a little lower at the front (the -x side) where the dog climbs in.
        static Mesh Bolster(float radiusX, float radiusZ, float width, float height)
        {
            const int around = 72, across = 16;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i <= around; i++)
            {
                float u = i / (float)around, angle = u * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var centre = new Vector3(outward.x * radiusX, 0f, outward.z * radiusZ);
                float front = Mathf.Max(0f, -outward.x);
                float tall = height * (1f - .45f * front * front);
                for (int j = 0; j <= across; j++)
                {
                    float v = j / (float)across, turn = v * Mathf.PI * 2f;
                    vertices.Add(centre + outward * (Mathf.Cos(turn) * width) + Vector3.up * (Mathf.Sin(turn) * tall));
                    uv.Add(new Vector2(u * 10f, v));
                    if (i == around || j == across) continue;
                    int a = i * (across + 1) + j, b = a + across + 1;
                    triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                    triangles.Add(b); triangles.Add(a + 1); triangles.Add(b + 1);
                }
            }
            var mesh = new Mesh { name = "Dog bed bolster" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents();
            return mesh;
        }

        void AnimateDog()
        {
            if (!dogBreath) return;
            float breath = Mathf.Sin(Time.time * 1.1f);
            dogBreath.localScale = new Vector3(1f + .012f * breath, 1f + .03f * breath, 1f);
        }
    }
}
