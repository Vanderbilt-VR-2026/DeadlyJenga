using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    // Where the sofa used to be: a giant golden retriever asleep in a plaid dog bed,
    // chin resting on the edge of the bed, facing the tree. Scenery only (no collision). Its body breathes slowly.
    public sealed partial class UnderneathTreeFinish
    {
        Transform dogBreath;

        void BuildDogBed()
        {
            var plaid = Fabric("Plaid dog bed", "Cranberry Rug Weave", new Color(1.25f, .85f, .85f), new Vector2(10f, 2f), .1f, .6f);
            var fleece = Fabric("Dog bed fleece", "Fleece Scan", new Color(.95f, .86f, .72f), new Vector2(4f, 6f), .2f, .8f);
            var fur = Fabric("Golden retriever fur", "Fleece Scan", new Color(1.15f, .80f, .45f), new Vector2(3f, 3f), .35f, .7f);
            var ears = Fabric("Golden retriever ears", "Fleece Scan", new Color(.90f, .55f, .27f), new Vector2(2f, 2f), .35f, .7f);
            var cream = Fabric("Golden retriever feathering", "Fleece Scan", new Color(1.25f, 1.0f, .72f), new Vector2(3f, 3f), .35f, .7f);
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

            // Body, lying on its belly with the hips rolled toward the room.
            Part("Retriever back", PrimitiveType.Sphere, new Vector3(-2f, 33f, -8f), new Vector3(54f, 42f, 96f), fur, dogBreath);
            Part("Retriever chest", PrimitiveType.Sphere, new Vector3(0f, 31f, 34f), new Vector3(48f, 40f, 46f), fur, dogBreath);
            Part("Retriever chest feathering", PrimitiveType.Sphere, new Vector3(0f, 25f, 48f), new Vector3(38f, 28f, 26f), cream, dogBreath);
            Part("Retriever hips", PrimitiveType.Sphere, new Vector3(-8f, 31f, -46f), new Vector3(52f, 40f, 52f), fur, dogBreath);
            Part("Retriever back thigh", PrimitiveType.Sphere, new Vector3(-26f, 25f, -40f), new Vector3(22f, 32f, 48f), fur, dogBreath, new Vector3(0f, 0f, 18f));
            Part("Retriever back leg", PrimitiveType.Capsule, new Vector3(-30f, 14f, -20f), new Vector3(11f, 13f, 11f), fur, dog, new Vector3(90f, 0f, 0f));
            Part("Retriever back paw", PrimitiveType.Sphere, new Vector3(-31f, 13f, -6f), new Vector3(14f, 10f, 18f), cream, dog);

            // Neck rising to a head that rests its chin on the front of the bolster.
            Part("Retriever neck", PrimitiveType.Sphere, new Vector3(0f, 38f, 64f), new Vector3(32f, 32f, 42f), fur, dog, new Vector3(-25f, 0f, 0f));
            Part("Retriever head", PrimitiveType.Sphere, new Vector3(0f, 45f, 88f), new Vector3(32f, 30f, 32f), fur, dog);
            Part("Retriever brow", PrimitiveType.Sphere, new Vector3(0f, 51f, 94f), new Vector3(24f, 14f, 18f), fur, dog);
            Part("Retriever muzzle", PrimitiveType.Sphere, new Vector3(0f, 40f, 108f), new Vector3(18f, 15f, 30f), cream, dog);
            Part("Retriever chin", PrimitiveType.Sphere, new Vector3(0f, 35f, 104f), new Vector3(14f, 8f, 20f), cream, dog);
            Part("Retriever nose", PrimitiveType.Sphere, new Vector3(0f, 43f, 122.5f), new Vector3(9f, 7f, 7f), nose, dog);
            Part("Mouth line", PrimitiveType.Cube, new Vector3(0f, 37.2f, 113f), new Vector3(12f, .6f, 14f), lids, dog);
            foreach (int side in new[] { -1, 1 })
            {
                Part("Retriever front leg", PrimitiveType.Capsule, new Vector3(side * 11f, 15f, 60f), new Vector3(12f, 20f, 12f), fur, dog, new Vector3(90f, 0f, 0f));
                Part("Retriever front paw", PrimitiveType.Sphere, new Vector3(side * 11f, 14f, 82f), new Vector3(15f, 10f, 18f), cream, dog);
                Part("Retriever floppy ear", PrimitiveType.Sphere, new Vector3(side * 16f, 39f, 88f), new Vector3(6f, 26f, 18f), ears, dog, new Vector3(-8f, 0f, side * 14f));
                Part("Closed eye", PrimitiveType.Cube, new Vector3(side * 8.5f, 48f, 101.5f), new Vector3(6f, .9f, 1.2f), lids, dog, new Vector3(0f, side * -25f, side * -12f));
            }

            // A feathery tail curled round the hips toward the room side.
            var tail = new[] { new Vector3(-2f, 28f, -78f), new Vector3(-10f, 22f, -92f), new Vector3(-24f, 18f, -96f),
                new Vector3(-38f, 15f, -88f), new Vector3(-46f, 14f, -72f), new Vector3(-48f, 13f, -54f) };
            for (int i = 0; i < tail.Length - 1; i++)
            {
                var from = tail[i];
                var to = tail[i + 1];
                float radius = Mathf.Lerp(9f, 6f, i / (tail.Length - 2f));
                var piece = Part("Retriever tail", PrimitiveType.Capsule, (from + to) * .5f,
                    new Vector3(radius * 2f, ((to - from).magnitude + radius * 2f) * .5f, radius * 2f), i >= tail.Length - 3 ? cream : fur, dog);
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
