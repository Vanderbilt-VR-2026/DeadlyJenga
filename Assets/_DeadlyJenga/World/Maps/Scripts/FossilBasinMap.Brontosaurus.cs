using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CastlePrototype
{
    // The brontosaurus west of the arena, exported from "Brontosaurus Stairway.blend".
    // Every bone group, platform, pylon, stair and link bridge is its own breakaway member.
    public sealed partial class FossilBasinMap
    {
        [Serializable] sealed class ExhibitLayout { public ExhibitMember[] members; public ExhibitJoint[] joints; }
        [Serializable] sealed class ExhibitMember { public string name, kind; public float mass; public Vector3 p; public ExhibitBox[] boxes; }
        [Serializable] sealed class ExhibitBox { public Vector3 p, size, forward, up; }
        [Serializable] sealed class ExhibitJoint { public string a, b; public Vector3 at; public float breakForce; }

        void BuildExhibit(string layoutName, string meshName, int fossilId)
        {
            var layoutAsset = Resources.Load<TextAsset>(layoutName);
            var meshAsset = Resources.Load<TextAsset>(meshName);
            if (!layoutAsset || !meshAsset) return;
            var layout = JsonUtility.FromJson<ExhibitLayout>(layoutAsset.text);
            using var compressed = new GZipStream(new MemoryStream(meshAsset.bytes), CompressionMode.Decompress);
            using var reader = new BinaryReader(compressed);
            if (reader.ReadInt32() != layout.members.Length) throw new InvalidDataException(layoutName + " mesh and layout counts differ");
            var parts = new Dictionary<string, FossilMember>(StringComparer.Ordinal);
            foreach (var m in layout.members)
            {
                var member = Piece(m.name, m.p, m.mass, c =>
                {
                    foreach (var box in m.boxes)
                        c.Proxy(box.p, Quaternion.LookRotation(box.forward, box.up), box.size);
                });
                if (m.kind == "bone") member.fossil = fossilId;
                FillColoredMesh(member.mesh, reader);
                parts[m.name] = member;
            }
            foreach (var j in layout.joints)
            {
                if (!parts.TryGetValue(j.a, out var a)) continue;
                if (j.b == "bearing") Socket(a.Body, Bearing(j.at), j.at, j.breakForce);
                else if (j.b == "map") { var seat = FindFaceoffSeat(j.at, 2.5f); if (seat) Socket(a, seat, j.at, j.breakForce); }
                else if (parts.TryGetValue(j.b, out var b)) Socket(a, b, j.at, j.breakForce);
            }
        }

        // A kinematic pad under a pier, like the arena's submerged bearings.
        Rigidbody Bearing(Vector3 at)
        {
            var pad = new GameObject("Exhibit submerged bearing");
            pad.transform.SetParent(structure, false);
            pad.transform.localPosition = at - Vector3.up * .21f;
            pad.AddComponent<BoxCollider>().size = new Vector3(1.2f, .4f, 1.2f);
            var body = pad.AddComponent<Rigidbody>();
            body.isKinematic = true;
            return body;
        }

        static void FillColoredMesh(Mesh mesh, BinaryReader reader)
        {
            int vertexCount = reader.ReadInt32(), indexCount = reader.ReadInt32();
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var colors = new Color32[vertexCount];
            var indices = new int[indexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                vertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                normals[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                colors[i] = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            }
            for (int i = 0; i < indexCount; i++) indices[i] = reader.ReadInt32();
            if (vertexCount > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors32 = colors;
            mesh.triangles = indices;
            mesh.RecalculateBounds();
        }
    }
}
