using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyWeaponVisual
    {
        sealed class StockPistolArtSection
        {
            public int assembly;
            public Mesh mesh;
            public Material material;
        }

        static StockPistolArtSection[] compactStockPistol;
        static bool checkedStockPistolArt;

        internal static Vector3 StockPistolMuzzle => LoadCompactStockPistol()
            ? new Vector3(0, .105f, .316f) : new Vector3(0, .12f, .51f);

        static bool LoadCompactStockPistol()
        {
            if (!checkedStockPistolArt)
            {
                checkedStockPistolArt = true;
                var asset = Resources.Load<TextAsset>("StockPistolCompactArt");
                if (asset != null)
                    compactStockPistol = ReadStockPistolArt(asset);
            }
            return compactStockPistol != null;
        }

        bool BuildCompactStockPistol(Transform root)
        {
            if (!LoadCompactStockPistol()) return false;
            var feedPart = MakeFeedAssembly(root);
            foreach (var section in compactStockPistol)
                MountFeedMesh(section.mesh, section.material, section.assembly == 2 ? feedPart : root);
            return true;
        }

        // SP01 is a Blender export grouped by material and by receiver or animated feed assembly.
        static StockPistolArtSection[] ReadStockPistolArt(TextAsset asset)
        {
            var sections = new List<StockPistolArtSection>();
            using var stream = new System.IO.MemoryStream(asset.bytes, false);
            using var reader = new System.IO.BinaryReader(stream);
            try
            {
                if (reader.ReadUInt32() != 0x31305053) throw new System.IO.InvalidDataException("Incorrect header");
                int count = reader.ReadInt32();
                if (count < 3 || count > 32) throw new System.IO.InvalidDataException("Incorrect section count");
                var lit = StockPistolSurfaces()[0].shader;
                for (int i = 0; i < count; i++)
                {
                    int assembly = reader.ReadByte();
                    string name = reader.ReadString();
                    var color = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    float metallic = reader.ReadSingle(), smoothness = reader.ReadSingle(), emission = reader.ReadSingle();
                    int vertices = reader.ReadInt32(), indices = reader.ReadInt32();
                    if ((assembly != 0 && assembly != 2) || vertices < 3 || vertices > 65535 ||
                        indices < 3 || indices > 393216 || indices % 3 != 0 ||
                        vertices * 24L + indices * 4L > stream.Length - stream.Position)
                        throw new System.IO.InvalidDataException("Incorrect section records");
                    var points = new Vector3[vertices];
                    var normals = new Vector3[vertices];
                    var triangles = new int[indices];
                    for (int v = 0; v < vertices; v++)
                    {
                        points[v] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        normals[v] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    }
                    for (int t = 0; t < indices; t++)
                    {
                        int value = reader.ReadInt32();
                        if ((uint)value >= (uint)vertices) throw new System.IO.InvalidDataException("Index out of bounds");
                        triangles[t] = value;
                    }
                    var mesh = new Mesh { name = "Stock Pistol | " + name };
                    mesh.vertices = points;
                    mesh.normals = normals;
                    mesh.triangles = triangles;
                    bool walnut = name == "Lacquered black-cherry walnut";
                    var material = walnut ? new Material(StockPistolSurfaces()[3]) : new Material(lit);
                    material.name = name;
                    material.enableInstancing = true;
                    if (walnut)
                    {
                        var uv = new Vector2[points.Length];
                        for (int v = 0; v < points.Length; v++)
                            uv[v] = new Vector2(points[v].z * 1.5f, points[v].y * 2.6f);
                        mesh.uv = uv;
                        mesh.RecalculateTangents();
                        color = new Color(.61f, .28f, .31f);
                    }
                    material.SetColor("_BaseColor", color);
                    material.SetFloat("_Metallic", metallic);
                    material.SetFloat("_Smoothness", smoothness);
                    if (emission > 0)
                    {
                        material.EnableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", color * emission);
                    }
                    sections.Add(new StockPistolArtSection { assembly = assembly, mesh = mesh, material = material });
                }
                if (stream.Position != stream.Length) throw new System.IO.InvalidDataException("Unexpected trailing data");
                return sections.ToArray();
            }
            catch (System.Exception ex) when (ex is System.IO.IOException || ex is System.IO.InvalidDataException)
            {
                foreach (var section in sections)
                {
                    Object.Destroy(section.mesh);
                    Object.Destroy(section.material);
                }
                Debug.LogWarning("Using the original Stock Pistol model: " + ex.Message);
                return null;
            }
        }

    }
}
