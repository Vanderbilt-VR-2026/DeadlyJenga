using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class UnderneathTreeMap
    {
        void BuildRoundGift(MeshFilter drum, MeshFilter lid)
        {
            var drumBounds = drum.sharedMesh.bounds;
            var lidBounds = lid.sharedMesh.bounds;
            var center = transform.InverseTransformPoint(drum.transform.TransformPoint(drumBounds.center));
            var drumSize = ModelSize(drum);
            var lidSize = ModelSize(lid);
            var lidCenter = transform.InverseTransformPoint(lid.transform.TransformPoint(lidBounds.center));
            float floor = Mathf.Max(GiftFloor(center), center.y - drumSize.y * .5f);
            float top = lidCenter.y + lidSize.y * .5f;
            float lidHeight = lidSize.y;
            float wallTop = top - lidHeight - .018f;
            float baseThickness = Mathf.Min(.36f, (wallTop - floor) * .15f);
            float radius = (drumSize.x + drumSize.z) * .25f;
            float lidRadius = (lidSize.x + lidSize.z) * .25f;
            float thickness = Mathf.Clamp(radius * .06f, .22f, .51f);
            var frame = Child(built, drum.name.Replace(" drum", ""), new Vector3(center.x, floor, center.z), Quaternion.identity);
            var wrap = drum.GetComponent<MeshRenderer>().sharedMaterial;
            var ribbon = lid.GetComponent<MeshRenderer>().sharedMaterial;
            var shell = new UnderneathTreeGiftShell();

            AddRoundPiece(frame, shell, "Bottom", GiftPieceRole.Base, baseThickness * .5f, Quaternion.identity,
                RoundMesh(radius, 0f, baseThickness, Mathf.PI * 2f, 24), wrap, radius * radius * baseThickness);
            var lidCollider = AddRoundPiece(frame, shell, "Lid", GiftPieceRole.Lid, top - floor - lidHeight * .5f, Quaternion.identity,
                RoundMesh(lidRadius, 0f, lidHeight, Mathf.PI * 2f, 32), ribbon, lidRadius * lidRadius * lidHeight);
            spawnTops.Add(lidCollider);

            float shellHeight = wallTop - floor - baseThickness - .018f;
            const int count = 8;
            float arc = Mathf.PI * 2f / count;
            for (int i = 0; i < count; i++)
            {
                var mesh = RoundMesh(radius, radius - thickness, shellHeight, arc - .003f, 5);
                AddRoundPiece(frame, shell, "Curved wall " + i, GiftPieceRole.Wall,
                    baseThickness + .018f + shellHeight * .5f, Quaternion.Euler(0f, -i * 360f / count, 0f), mesh, wrap,
                    radius * thickness * shellHeight * arc);
            }
        }

        Collider AddRoundPiece(Transform parent, UnderneathTreeGiftShell shell, string name, GiftPieceRole role,
            float y, Quaternion rotation, Mesh mesh, Material material, float volume)
        {
            var piece = Child(parent, name, Vector3.up * y, rotation);
            piece.gameObject.layer = Layers.Block;
            var shape = piece.gameObject.AddComponent<MeshCollider>();
            shape.sharedMesh = mesh;
            shape.convex = true;
            var visual = Child(piece, "Curved wrapping", Vector3.zero, Quaternion.identity);
            visual.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            visual.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            RegisterPanel(piece.gameObject, visual, shell, role, volume);
            generatedMeshes.Add(mesh);
            return shape;
        }

        static Mesh RoundMesh(float radius, float inner, float height, float arc, int segments)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            Vector3 Point(float r, float theta, float y) => new Vector3(r * Mathf.Cos(theta), y, r * Mathf.Sin(theta));
            void Face(Vector3 outward, params Vector3[] points)
            {
                int first = vertices.Count;
                bool flip = Vector3.Dot(Vector3.Cross(points[1] - points[0], points[2] - points[0]), outward) < 0f;
                for (int j = 0; j < points.Length; j++)
                {
                    vertices.Add(points[j]);
                    uv.Add(new Vector2(j == 0 || j == 3 ? 0f : 1f, j < 2 ? 0f : 1f));
                }
                for (int j = 1; j < points.Length - 1; j++)
                {
                    triangles.Add(first);
                    triangles.Add(first + (flip ? j + 1 : j));
                    triangles.Add(first + (flip ? j : j + 1));
                }
            }

            float top = height * .5f;
            for (int i = 0; i < segments; i++)
            {
                float a = arc * i / segments;
                float b = arc * (i + 1) / segments;
                var normal = Point(1f, (a + b) * .5f, 0f);
                var obA = Point(radius, a, -top);
                var obB = Point(radius, b, -top);
                var otA = Point(radius, a, top);
                var otB = Point(radius, b, top);
                Face(normal, obA, obB, otB, otA);
                if (inner == 0f)
                {
                    Face(Vector3.up, Vector3.up * top, otA, otB);
                    Face(Vector3.down, Vector3.down * top, obA, obB);
                    continue;
                }

                var ibA = Point(inner, a, -top);
                var ibB = Point(inner, b, -top);
                var itA = Point(inner, a, top);
                var itB = Point(inner, b, top);
                Face(-normal, ibA, ibB, itB, itA);
                Face(Vector3.up, itA, otA, otB, itB);
                Face(Vector3.down, ibA, obA, obB, ibB);
                if (i == 0)
                    Face(Vector3.back, ibA, obA, otA, itA);
                if (i == segments - 1)
                    Face(new Vector3(-Mathf.Sin(b), 0, Mathf.Cos(b)), ibB, obB, otB, itB);
            }
            var mesh = new Mesh { name = "Breakaway cylindrical gift piece" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            return mesh;
        }

    }
}
