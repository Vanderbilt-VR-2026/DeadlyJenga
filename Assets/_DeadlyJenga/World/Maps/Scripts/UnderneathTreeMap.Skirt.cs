using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class UnderneathTreeMap
    {
        void BuildSkirtStitching()
        {
            if (!skirt)
                return;

            var figures = new StitchMesh();
            var sockets = new StitchMesh();
            var center = transform.InverseTransformPoint(skirt.bounds.center);
            for (int i = 0; i < 14; i++)
            {
                float radians = i * Mathf.PI * 2f / 14f;
                float radius = i % 2 == 0 ? 59f : 66f;
                var origin = center + new Vector3(Mathf.Cos(radians) * radius, 0f, Mathf.Sin(radians) * radius);
                if (!skirt.Raycast(new Ray(transform.TransformPoint(origin + Vector3.up * 4f), -transform.up), out var hit, 8f))
                    continue;
                origin = transform.InverseTransformPoint(hit.point) + Vector3.up * .018f;
                var orientation = Quaternion.Euler(0f, -radians * Mathf.Rad2Deg + 90f, 0f);
                Skull(origin, orientation, figures, sockets);
            }

            AddStitches("Skirt skull stitching", figures, ColoredPaper("Crimson stitching", new Color(.39f, .074f, .085f), .12f));
            AddStitches("Skirt skull eyelets", sockets, ColoredPaper("Shadow red stitching", new Color(.21f, .037f, .045f), .13f));
        }

        static void Skull(Vector3 origin, Quaternion orientation, StitchMesh figures, StitchMesh sockets)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                var a = new Vector2(-1.2f, -1.05f * sign);
                var b = -a;
                var normal = Vector2.Perpendicular(b - a).normalized * .11f;
                figures.Shape(origin, orientation, a + normal, b + normal, b - normal, a - normal);
                figures.Oval(origin, orientation, a, new Vector2(.19f, .19f));
                figures.Oval(origin, orientation, b, new Vector2(.19f, .19f));
            }

            origin.y += .003f;
            figures.Oval(origin, orientation, new Vector2(0f, .20f), new Vector2(.82f, .72f));
            figures.Shape(origin, orientation, new Vector2(-.45f, -.36f), new Vector2(.45f, -.36f),
                new Vector2(.33f, -.82f), new Vector2(-.33f, -.82f));
            origin.y += .003f;
            sockets.Oval(origin, orientation, new Vector2(-.3f, .17f), new Vector2(.16f, .19f));
            sockets.Oval(origin, orientation, new Vector2(.3f, .17f), new Vector2(.16f, .19f));
            sockets.Shape(origin, orientation, new Vector2(0f, -.05f), new Vector2(.105f, -.26f), new Vector2(-.105f, -.26f));
        }

        void AddStitches(string label, StitchMesh stitches, Material material)
        {
            var mesh = new Mesh { name = label };
            mesh.SetVertices(stitches.Vertices);
            mesh.SetTriangles(stitches.Indices, 0);
            mesh.RecalculateNormals();
            generatedMeshes.Add(mesh);
            var art = Child(built, label, Vector3.zero, Quaternion.identity);
            art.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            art.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        sealed class StitchMesh
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int> Indices = new List<int>();

            public void Shape(Vector3 origin, Quaternion rotation, params Vector2[] points)
            {
                int first = Vertices.Count;
                foreach (var point in points)
                    Vertices.Add(origin + rotation * new Vector3(point.x, 0f, point.y));
                bool clockwise = Vector3.Cross(Vertices[first + 1] - Vertices[first],
                                               Vertices[first + 2] - Vertices[first]).y > 0f;
                for (int i = 1; i < points.Length - 1; i++)
                {
                    Indices.Add(first);
                    Indices.Add(first + (clockwise ? i : i + 1));
                    Indices.Add(first + (clockwise ? i + 1 : i));
                }
            }

            public void Oval(Vector3 origin, Quaternion orientation, Vector2 center, Vector2 radius)
            {
                var points = new Vector2[12];
                for (int i = 0; i < points.Length; i++)
                {
                    float angle = i * Mathf.PI * 2f / points.Length;
                    points[i] = center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y);
                }
                Shape(origin, orientation, points);
            }
        }
    }
}
