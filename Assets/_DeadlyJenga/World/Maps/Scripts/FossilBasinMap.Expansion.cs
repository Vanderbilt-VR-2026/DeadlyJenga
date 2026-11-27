using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.World.Maps
{
    public sealed partial class FossilBasinMap
    {
        [Serializable] sealed class ExpansionBox { public Vector3 p, size, forward, up; }
        [Serializable] sealed class ExpansionMember { public string name, kind; public float mass; public Vector3 p; public ExpansionBox[] boxes; }
        [Serializable] sealed class ExpansionJoint { public string a, b; public Vector3 at; public float breakForce; }
        [Serializable] sealed class ExpansionLayout { public ExpansionMember[] members; public ExpansionJoint[] joints; }

        void BuildExpansion()
        {
            var layoutAsset = Resources.Load<TextAsset>("FossilBasinExpansion");
            var meshAsset = Resources.Load<TextAsset>("FossilBasinExpansionMeshes");
            if (!layoutAsset || !meshAsset) return;

            var layout = JsonUtility.FromJson<ExpansionLayout>(layoutAsset.text);
            if (layout?.members == null) return;

            using var compressed = new MemoryStream(meshAsset.bytes);
            using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
            using var reader = new BinaryReader(gzip);
            int meshCount = reader.ReadInt32();
            if (meshCount != layout.members.Length)
                Debug.LogWarning($"FossilBasinExpansion mesh count {meshCount} != layout members {layout.members.Length}");

            var anchor = new GameObject("Expansion Bearing Anchor");
            anchor.transform.SetParent(structure, false);
            var anchorBody = anchor.AddComponent<Rigidbody>();
            anchorBody.isKinematic = true;

            var byName = new Dictionary<string, FossilMember>(layout.members.Length, StringComparer.Ordinal);
            for (int i = 0; i < layout.members.Length; i++)
            {
                var spec = layout.members[i];
                var member = Piece(spec.name, spec.p, Mathf.Max(3f, spec.mass), c =>
                {
                    if (spec.boxes == null) return;
                    foreach (var box in spec.boxes)
                    {
                        var rot = box.forward.sqrMagnitude > 1e-6f && box.up.sqrMagnitude > 1e-6f
                            ? Quaternion.LookRotation(box.forward, box.up)
                            : Quaternion.identity;
                        c.Proxy(box.p, rot, box.size);
                    }
                });
                if (i < meshCount) ReadExpansionMesh(member, reader);
                if (spec.kind == "deck") decks.Add(member);
                byName[spec.name] = member;
            }

            if (layout.joints == null) return;
            foreach (var j in layout.joints)
            {
                if (!byName.TryGetValue(j.a, out var a)) continue;
                Rigidbody b = null;
                if (j.b == "bearing" || j.b == "map") b = anchorBody;
                else if (byName.TryGetValue(j.b, out var mb)) b = mb.Body;
                if (b) Socket(a.Body, b, j.at, j.breakForce);
            }
        }

        void ReadExpansionMesh(FossilMember member, BinaryReader reader)
        {
            int vc = reader.ReadInt32(), ic = reader.ReadInt32();
            var verts = new Vector3[vc];
            var norms = new Vector3[vc];
            var cols = new Color32[vc];
            var tris = new int[ic];
            for (int v = 0; v < vc; v++)
            {
                verts[v] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                norms[v] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                cols[v] = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            }
            for (int t = 0; t < ic; t++) tris[t] = reader.ReadInt32();

            var mesh = new Mesh { name = member.name + " expansion mesh" };
            if (vc > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.colors32 = cols;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            member.mesh = mesh;
            member.art.GetComponent<MeshFilter>().sharedMesh = mesh;
            member.art.GetComponent<MeshRenderer>().sharedMaterial = stone;
        }
    }
}
