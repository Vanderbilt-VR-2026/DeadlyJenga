using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyAvatarWardrobe
    {
        sealed class SuitSection
        {
            public byte bone;
            public byte finish;
            public Mesh mesh;
        }

        static readonly Tone[] SuitTones = { Tone.Armor, Tone.Dark, Tone.Silver, Tone.Glow };
        static readonly Material[] SuitSurfaces = new Material[4];
        static SuitSection[] fittedSuit;
        static bool checkedFittedSuit;

        bool BuildFittedSuit(CandyPlayerRig rig)
        {
            if (!checkedFittedSuit)
            {
                checkedFittedSuit = true;
                var asset = Resources.Load<TextAsset>("PoweredIronSuitArt");
                if (asset) fittedSuit = ReadFittedSuit(asset);
            }
            if (fittedSuit == null || rig.BoneCount < 14) return false;
            foreach (var part in fittedSuit)
            {
                var go = new GameObject(part.mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(rig.AppearanceBone(part.bone), false);
                go.GetComponent<MeshFilter>().sharedMesh = part.mesh;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = FittedSuitSurface(part.finish);
                pieces.Add(new Piece { renderer = renderer, bone = part.bone, tone = SuitTones[part.finish], armor = true });
            }

            MountArmorEmitter(rig, "Left palm", 4, new Vector3(0, -.047f, .031f), 0);
            MountArmorEmitter(rig, "Right palm", 7, new Vector3(0, -.047f, .031f), 1);
            MountArmorEmitter(rig, "Left sole", 10, new Vector3(0, -.093f, .057f), 2);
            MountArmorEmitter(rig, "Right sole", 13, new Vector3(0, -.093f, .057f), 3);
            return true;
        }

        static Material FittedSuitSurface(int index)
        {
            if (SuitSurfaces[index]) return SuitSurfaces[index];
            var material = new Material(ArmorSurface(SuitTones[index])) { name = "Fitted Powered Iron Suit " + SuitTones[index] };
            material.SetFloat("_Metallic", index == 0 ? .62f : index == 2 ? .74f : .08f);
            material.SetFloat("_Smoothness", index == 1 ? .57f : .72f);
            SuitSurfaces[index] = material;
            return material;
        }

        // IS01 stores evaluated Blender meshes in local coordinates for the production armor bones.
        static SuitSection[] ReadFittedSuit(TextAsset asset)
        {
            var result = new List<SuitSection>();
            using var stream = new MemoryStream(asset.bytes, false);
            using var reader = new BinaryReader(stream);
            try
            {
                if (reader.ReadUInt32() != 0x31305349) throw new InvalidDataException("Incorrect mesh header");
                int count = reader.ReadInt32();
                if (count < 14 || count > 56) throw new InvalidDataException("Incorrect mesh section count");
                for (int i = 0; i < count; i++)
                {
                    byte bone = reader.ReadByte(), finish = reader.ReadByte();
                    int vertices = reader.ReadInt32(), indices = reader.ReadInt32();
                    if (bone >= 14 || finish >= 4 || vertices < 3 || vertices > 65535 ||
                        indices < 3 || indices % 3 != 0 || vertices * 24L + indices * 2L > stream.Length - stream.Position)
                        throw new InvalidDataException("Incorrect mesh section record");
                    var points = new Vector3[vertices];
                    var normals = new Vector3[vertices];
                    for (int v = 0; v < vertices; v++)
                    {
                        points[v] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        normals[v] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    }
                    var triangles = new int[indices];
                    for (int t = 0; t < indices; t++)
                    {
                        int vertex = reader.ReadUInt16();
                        if (vertex >= vertices) throw new InvalidDataException("Triangle index is out of range");
                        triangles[t] = vertex;
                    }
                    var mesh = new Mesh
                    {
                        name = "Fitted Powered Iron Suit " + bone + " " + SuitTones[finish],
                        vertices = points,
                        normals = normals,
                        triangles = triangles
                    };
                    result.Add(new SuitSection { bone = bone, finish = finish, mesh = mesh });
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected mesh data");
                return result.ToArray();
            }
            catch (System.Exception ex) when (ex is IOException || ex is InvalidDataException)
            {
                foreach (var section in result) Destroy(section.mesh);
                Debug.LogWarning("Using the original Powered Iron Suit: " + ex.Message);
                return null;
            }
        }
    }
}
