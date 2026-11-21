using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    // Where the sofa used to be: a golden retriever asleep in a plaid dog bed, head on its paws,
    // facing the tree. Scenery only (no collision). Its body breathes slowly.
    public sealed partial class UnderneathTreeFinish
    {
        Transform dogBreath;
        const float DogScale = 95f; // the model is in metres; about 95 map units to a metre beside the furniture

        void BuildDogBed()
        {
            var plaid = Fabric("Plaid dog bed", "Cranberry Rug Weave", new Color(1.25f, .85f, .85f), new Vector2(8f, 2f), .1f, .6f);
            var fleece = Fabric("Dog bed fleece", "Fleece Scan", new Color(.95f, .86f, .72f), new Vector2(3f, 4f), .2f, .8f);
            var fur = Fabric("Golden retriever fur", "Fleece Scan", new Color(1.35f, .95f, .52f), new Vector2(4f, 4f), .35f, .7f);
            var ears = Fabric("Golden retriever ears", "Fleece Scan", new Color(1.0f, .62f, .30f), new Vector2(2f, 2f), .35f, .7f);
            var nose = Surface("Dog nose", null, new Color(.05f, .035f, .03f), .75f, Vector2.one);

            // Real dog size beside the furniture; head toward the tree, turned a little toward the room.
            var bed = Pivot("Dog bed with sleeping retriever", new Vector3(RoomEast - 85f, 0f, SofaZ - 8f), -12f);
            Under(Primitive("Dog bed base", PrimitiveType.Cylinder, new Vector3(0f, 2f, 0f), new Vector3(120f, 2f, 192f), plaid), bed);
            Under(Primitive("Dog bed cushion", PrimitiveType.Cylinder, new Vector3(0f, 6f, 0f), new Vector3(102f, 3f, 172f), fleece), bed);
            var bolster = AddMesh("Dog bed bolster", Bolster(51f, 81f, 12f, 11f), plaid, ShadowCastingMode.On).transform;
            Under(bolster, bed).localPosition = new Vector3(0f, 12f, 0f);

            // The retriever itself was modelled in Blender (Retriever Body/Ears/Nose.obj).
            dogBreath = new GameObject("Sleeping golden retriever").transform;
            dogBreath.SetParent(bed, false);
            dogBreath.localPosition = new Vector3(0f, 8.5f, -6f);
            dogBreath.localScale = Vector3.one * DogScale;
            foreach (var (model, look) in new[] { ("Retriever Body", fur), ("Retriever Ears", ears), ("Retriever Nose", nose) })
            {
                var mesh = Resources.Load<Mesh>(ResourcePath + model);
                if (!mesh) continue;
                // Project files, so not added to the list of meshes destroyed on cleanup.
                var part = new GameObject(model);
                part.transform.SetParent(dogBreath, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterial = look;
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
            dogBreath.localScale = new Vector3(1f + .01f * breath, 1f + .025f * breath, 1f) * DogScale;
        }
    }
}
