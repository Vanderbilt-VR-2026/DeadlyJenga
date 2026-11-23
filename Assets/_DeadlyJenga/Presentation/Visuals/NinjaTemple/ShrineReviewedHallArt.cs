using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.NinjaArt
{
    internal sealed class ShrineReviewedHallArt : IDisposable
    {
        const string Prefix = "Reviewed shrine ";
        readonly Mesh[] parts = new Mesh[47];
        readonly Material finish;
        readonly List<Renderer> hidden = new List<Renderer>();
        readonly List<MeshFilter> attached = new List<MeshFilter>();
        readonly List<MeshRenderer> renderers = new List<MeshRenderer>();

        public ShrineReviewedHallArt(Material lacquer)
        {
            var asset = Resources.Load<TextAsset>("ShrineReviewedHall");
            if (!asset) throw new InvalidOperationException("Missing reviewed shrine mesh export.");
            using var stream = new MemoryStream(asset.bytes);
            using var gzip = new GZipStream(stream,CompressionMode.Decompress);
            using var reader = new BinaryReader(gzip);
            if (reader.ReadUInt32() != 0x32524853 || reader.ReadInt32() != parts.Length)
                throw new InvalidDataException("Unexpected reviewed shrine export.");
            for (int section = 0; section < parts.Length; section++)
            {
                int id = reader.ReadInt32(), count = reader.ReadInt32();
                if (id != section || count <= 0 || count > 450000 || count % 3 != 0)
                    throw new InvalidDataException("Invalid reviewed shrine section.");
                var vertices = new Vector3[count];
                var normals = new Vector3[count];
                var colors = new Color32[count];
                var indices = new int[count];
                Vector3 ReadVector() => new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                for (int i = 0; i < count; i++)
                {
                    vertices[i] = ReadVector();
                    normals[i] = ReadVector();
                    colors[i] = new Color32(reader.ReadByte(),reader.ReadByte(),reader.ReadByte(),reader.ReadByte());
                    indices[i] = i;
                }
                var mesh = new Mesh { name = Prefix+section, indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.colors32 = colors;
                mesh.SetTriangles(indices,0);
                mesh.UploadMeshData(true);
                parts[section] = mesh;
            }
            finish = new Material(lacquer) { name = "Shrine reviewed lacquer" };
            finish.SetFloat("_Cull",0);
        }

        public void Attach(Rigidbody body)
        {
            var origin = body.transform.Find("Reviewed shrine art origin");
            if (origin.TryGetComponent<MeshFilter>(out var current) && current.sharedMesh) return;
            if (!body.name.StartsWith(Prefix,StringComparison.Ordinal) ||
                !int.TryParse(body.name.Substring(Prefix.Length),out int part) || (uint)part >= parts.Length)
                return;
            var visual = body.GetComponent<IClimbable>()?.Visual;
            if (visual && visual != origin)
            {
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                    if (renderer.enabled) { hidden.Add(renderer); renderer.enabled = false; }
            }
            var filter = current ? current : origin.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = parts[part];
            var art = origin.GetComponent<MeshRenderer>();
            if (!art) art = origin.gameObject.AddComponent<MeshRenderer>();
            art.enabled = true;
            art.sharedMaterial = finish;
            attached.Add(filter);
            renderers.Add(art);
        }

        public void Dispose()
        {
            foreach (var filter in attached)
                if (filter) filter.sharedMesh = null;
            foreach (var renderer in renderers)
                if (renderer) renderer.enabled = false;
            foreach (var renderer in hidden)
                if (renderer) renderer.enabled = true;
            foreach (var mesh in parts)
                if (mesh) UnityEngine.Object.Destroy(mesh);
            if (finish) UnityEngine.Object.Destroy(finish);
        }
    }
}
