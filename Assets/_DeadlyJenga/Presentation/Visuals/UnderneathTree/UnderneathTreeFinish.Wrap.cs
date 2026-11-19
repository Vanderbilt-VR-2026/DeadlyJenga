using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    public sealed partial class UnderneathTreeFinish
    {
        void SoftenGiftEdges(MeshRenderer renderer)
        {
            string label = renderer.name;
            if (!renderer.TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh)
                return;
            // Gift panels are plain cubes called "Visual"; toys also keep their model in "Visual", so leave those alone.
            bool generated = (label == "Wrap" || label == "Visual") && filter.sharedMesh.name.StartsWith("Cube");
            bool imported = label.StartsWith("Present ") && label.EndsWith(" box");
            if (!generated && !imported)
                return;

            var source = filter.sharedMesh;
            if (generated && !roundedWrap)
                roundedWrap = RoundedWrap(new Bounds(Vector3.zero, Vector3.one));
            originalMeshes[filter] = source;
            filter.sharedMesh = generated ? roundedWrap : RoundedWrap(source.bounds);
        }

        void MapRugTexture(MeshRenderer renderer)
        {
            if (!renderer.name.Contains("kill floor") || !renderer.TryGetComponent<MeshFilter>(out var filter)) return;
            var mesh = Instantiate(filter.sharedMesh);
            originalMeshes[filter] = filter.sharedMesh;
            var vertices = mesh.vertices;
            var coordinates = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var point = art.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
                coordinates[i] = new Vector2((point.x + 205f) / 230f, (point.z + 225f) / 230f);
            }
            mesh.uv = coordinates;
            mesh.RecalculateTangents();
            meshes.Add(mesh);
            filter.sharedMesh = mesh;
        }

        Mesh RoundedWrap(Bounds source, float radius = .027f, float loft = 0f)
        {
            float inner = .5f - radius;
            float[] cuts = loft > 0f
                ? new[] { -.5f, -.5f + radius * .27f, -.5f + radius * .64f, -inner, -inner * .6f,
                    -inner * .3f, 0f, inner * .3f, inner * .6f, inner, .5f - radius * .64f, .5f - radius * .27f, .5f }
                : new[] { -.5f, -.5f + radius * .32f, -.5f + radius * .77f,
                    -inner, inner, .5f - radius * .77f, .5f - radius * .32f, .5f };
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            var faces = new[] { Vector3.forward, Vector3.back, Vector3.up, Vector3.down, Vector3.left, Vector3.right };
            Vector3 weights = source.size;
            for (int f = 0; f < faces.Length; f++)
            {
                var normal = faces[f];
                var along = Vector3.Cross(Mathf.Abs(normal.y) > .9f ? Vector3.forward : Vector3.up, normal);
                var up = Vector3.Cross(normal, along);
                int first = vertices.Count;
                for (int row = 0; row < cuts.Length; row++)
                for (int col = 0; col < cuts.Length; col++)
                {
                    var frame = normal * .5f + along * cuts[col] + up * cuts[row];
                    var core = new Vector3(Mathf.Clamp(frame.x, -inner, inner), Mathf.Clamp(frame.y, -inner, inner),
                        Mathf.Clamp(frame.z, -inner, inner));
                    var direction = (frame - core).normalized;
                    float u = cuts[col], v = cuts[row];
                    float volume = loft * Mathf.Max(0f, 1f - 4f * u * u) * Mathf.Max(0f, 1f - 4f * v * v);
                    if (loft > 0f)
                    {
                        float seamPull = Mathf.Exp(-Mathf.Pow((Mathf.Abs(u) - .34f) / .11f, 2f));
                        volume *= 1f - .35f * seamPull * Mathf.Pow(Mathf.Sin(v * 27f + u * 9f), 2f);
                    }
                    vertices.Add(source.center + Vector3.Scale(core + direction * radius + normal * volume, weights));
                    var inflated = direction + (along * u + up * v) * (volume * 5.4f);
                    normals.Add(new Vector3(inflated.x / weights.x, inflated.y / weights.y, inflated.z / weights.z).normalized);
                    uv.Add(new Vector2(cuts[col] + .5f, cuts[row] + .5f));
                    if (row == cuts.Length - 1 || col == cuts.Length - 1)
                        continue;
                    int a = first + row * cuts.Length + col;
                    int b = a + cuts.Length;
                    triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                    triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
                }
            }
            var mesh = new Mesh { name = "Soft folded wrapping edges" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            if (loft > 0f) mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            meshes.Add(mesh);
            return mesh;
        }
    }
}
