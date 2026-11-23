using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class GingerbreadKingdom
    {
        const float HomeStorey = 2.22f;
        const float ClockCrown = 24.89f;
        static SkylineData skyline;

        [Serializable]
        sealed class SkylineData
        {
            public SkylineMesh clock;
            public SkylineHouse[] houses;
        }

        [Serializable]
        sealed class SkylineHouse
        {
            public string name;
            public float width, depth, door;
            public int floors;
            public SkylineMesh shell, roof;
            public int[] boxes;
        }

        [Serializable]
        sealed class SkylineMesh
        {
            public int[] vertices;
            public SkylineTriangles[] groups;
        }

        [Serializable]
        sealed class SkylineTriangles
        {
            public int finish;
            public int[] indices;
        }

        static SkylineData ReadSkyline()
        {
            if (skyline != null) return skyline;

            var asset = Resources.Load<TextAsset>("GingerbreadSkyline");
            if (!asset) throw new InvalidOperationException("Missing Gingerbread Skyline geometry.");
            using var packed = new MemoryStream(asset.bytes, false);
            using var gzip = new GZipStream(packed, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            skyline = JsonUtility.FromJson<SkylineData>(reader.ReadToEnd());
            return skyline;
        }

        void MountSkylineMesh(Transform owner, SkylineMesh data, Vector3 origin)
        {
            var vertices = new Vector3[data.vertices.Length / 3];
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = ReadPoint(data.vertices, i * 3) - origin;

            var mesh = new Mesh
            {
                name = owner.name,
                indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
                vertices = vertices,
                subMeshCount = data.groups.Length,
            };
            var materials = new Material[data.groups.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = finishes[data.groups[i].finish];
                mesh.SetTriangles(data.groups[i].indices, i, false);
            }
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var uv = new Vector2[vertices.Length];
            var normals = mesh.normals;
            for (int i = 0; i < uv.Length; i++)
            {
                var p = vertices[i] + origin;
                var n = normals[i];
                uv[i] = Mathf.Abs(n.y) > .72f ? new Vector2(p.x, p.z) * .6f
                    : new Vector2(Mathf.Abs(n.x) > Mathf.Abs(n.z) ? p.z : p.x, p.y) * .6f;
            }
            mesh.uv = uv;
            var visual = Group("Visual", owner, Vector3.zero);
            visual.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            visual.gameObject.AddComponent<MeshRenderer>().sharedMaterials = materials;
            meshes.Add(mesh);
        }

        static Vector3 ReadPoint(int[] values, int start) =>
            V(values[start], values[start + 1], values[start + 2]) * .001f;

        BoxCollider SkylineBox(Transform owner, Vector3 center, Vector3 size)
        {
            var collider = owner.gameObject.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
            collider.sharedMaterial = friction;
            return collider;
        }

        void BuildSkyline()
        {
            var data = ReadSkyline();
            var clock = Group("Clock Court | Gingerbread Big Ben", town, V(0, 0, -8));
            MountSkylineMesh(clock, data.clock, Vector3.zero);
            // Low colliders occupy only the corners. The roads cross on both axes.
            foreach (int x in new[] { -1, 1 })
            foreach (int z in new[] { -1, 1 })
                SkylineBox(clock, V(1.38f * x, 1.77f, 1.38f * z), V(.72f, 3.54f, .72f));

            SkylineBox(clock, V(0, 3.73f, 0), V(3.75f, .52f, 3.75f));
            SkylineBox(clock, V(0, 8.67f, 0), V(3.5f, 9.36f, 3.5f));
            SkylineBox(clock, V(0, 15.99f, 0), V(4.3f, 5.08f, 4.3f));
            SkylineBox(clock, V(0, 19.48f, 0), V(3.9f, 1.89f, 3.9f));
            SkylineBox(clock, V(0, 21.22f, 0), V(2.2f, 1.60f, 2.2f));
            SkylineBox(clock, V(0, 22.90f, 0), V(.95f, 1.75f, .95f));

            BuildSkylineHouse(data.houses[0], V(-15, 0, -17), 195, 4.2f, 6, V(-11.8f, 0, -18.2f));
            BuildSkylineHouse(data.houses[1], V(-20, 0, -10), -90, 3.8f, 5.6f, V(-15.8f, 0, -9.32f));
            BuildSkylineHouse(data.houses[2], V(22, 0, -7), 90, 4.2f, 5.8f, V(18.4f, 0, -7));

            var stones = new GingerbreadMesh();
            Street(stones, new[] { V(-4.95f, 0, -8), V(0, 0, -8), V(4.96f, 0, -8) });
            Street(stones, new[] { V(0, 0, -11.1f), V(0, 0, -8), V(0, 0, -3.8f), V(-4.1f, 0, -3.2f) });
            Mount(Group("Clock Court street links", town, Vector3.zero), stones);
        }

        void BuildSkylineHouse(SkylineHouse house, Vector3 at, float yaw, float width, float depth, Vector3 street)
        {
            var building = Group(house.name, town, at, yaw);
            building.localScale = V(width / house.width, 1, depth / house.depth);
            MountSkylineMesh(building, house.shell, Vector3.zero);
            for (int i = 0; i < house.boxes.Length; i += 6)
                SkylineBox(building, ReadPoint(house.boxes, i), ReadPoint(house.boxes, i + 3));

            var eave = house.floors * HomeStorey;
            SkylineBox(building, V(0, eave - .0775f, 0), V(house.width, .155f, house.depth));
            var pivot = V(0, eave + 1.10f, 0);
            var roof = Group(house.name + " removable roof", building, pivot);
            SkylineBox(roof, Vector3.zero, V(house.width * .83f, 2.20f, house.depth * .85f));
            MountSkylineMesh(roof, house.roof, pivot);
            var body = roof.gameObject.AddComponent<Rigidbody>();
            body.mass = 27;
            body.solverIterations = 16;
            body.solverVelocityIterations = 6;
            body.isKinematic = replica;
            var block = roof.gameObject.AddComponent<CastleBlock>();
            block.visualRoot = roof.Find("Visual");
            roof.gameObject.AddComponent<GingerbreadBearing>();
            pieces.Add(body);

            var doorstep = at + building.localRotation * V(house.door * building.localScale.x, 0, -depth * .5f - .23f);
            var path = new GingerbreadMesh();
            Vector3 delta = street - doorstep;
            int count = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / .36f));
            for (int i = 0; i < count; i++)
            {
                Vector3 center = doorstep + delta * ((i + .5f) / count) + Vector3.up * .026f;
                path.Box(Stone, center, V(1.32f, .042f, delta.magnitude / count - .016f),
                    Quaternion.LookRotation(delta));
            }
            Mount(Group(house.name + " street threshold", town, Vector3.zero), path);
        }
    }
}
