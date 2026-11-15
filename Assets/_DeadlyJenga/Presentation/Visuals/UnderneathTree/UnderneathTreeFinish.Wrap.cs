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
            bool generated = label == "Wrap" || label == "Visual";
            bool imported = label.StartsWith("Present ") && label.EndsWith(" box");
            if (!generated && !imported)
                return;

            var source = filter.sharedMesh;
            if (generated && !roundedWrap)
                roundedWrap = RoundedWrap(new Bounds(Vector3.zero, Vector3.one));
            originalMeshes[filter] = source;
            filter.sharedMesh = generated ? roundedWrap : RoundedWrap(source.bounds);
        }

        Mesh RoundedWrap(Bounds source)
        {
            const float radius = .027f;
            const float inner = .5f - radius;
            float[] cuts = { -.5f, -.49f, -.478f, -inner, inner, .478f, .49f, .5f };
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
                    vertices.Add(source.center + Vector3.Scale(core + direction * radius, weights));
                    normals.Add(new Vector3(direction.x / weights.x, direction.y / weights.y, direction.z / weights.z).normalized);
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
            mesh.RecalculateTangents();
            meshes.Add(mesh);
            return mesh;
        }
    }
}
