using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    public sealed partial class UnderneathTreeFinish
    {
        Mesh giftBow;

        void BuildRoomDetails()
        {
            BuildFloorboards();
            BuildFleece();
            var mohair = Surface("Burgundy sofa cushions", "Woven", new Color(.39f, .103f, .155f),
                .26f, new Vector2(4, 4));
            for (int i = 0; i < 3; i++)
            {
                float z = -152f + 42f * i;
                RoundedDetail("Upholstered seat cushion", new Vector3(-207f, 31.3f, z),
                    new Vector3(45f, 8f, 40.5f), .15f, mohair);
                var back = RoundedDetail("Upholstered back cushion", new Vector3(-228f, 43f, z),
                    new Vector3(14f, 29f, 40.5f), .19f, mohair);
                back.localRotation = Quaternion.Euler(0f, 0f, 8f);
            }
        }

        Transform RoundedDetail(string label, Vector3 position, Vector3 size, float radius, Material material)
        {
            var mesh = RoundedWrap(new Bounds(Vector3.zero, size), radius);
            var detail = AddMesh(label, mesh, material, ShadowCastingMode.On).transform;
            detail.localPosition = position;
            return detail;
        }

        void BuildFloorboards()
        {
            var random = new System.Random(2419);
            var boards = new List<CombineInstance>[5];
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            for (int i = 0; i < boards.Length; i++)
                boards[i] = new List<CombineInstance>();
            for (int row = 0; row < 43; row++)
            {
                float x = -254.1f + row * 7.9f;
                for (float z = -260f; z < 76.8f;)
                {
                    float length = Mathf.Min(77f - z, Range(random, 23f, 42f));
                    boards[random.Next(boards.Length)].Add(Part(cube,
                        new Vector3(x, .008f, z + length * .5f), new Vector3(7.82f, .051f, length - .12f)));
                    z += length;
                }
            }
            for (int i = 0; i < boards.Length; i++)
            {
                float tint = Mathf.Lerp(.76f, 1.2f, i / (float)(boards.Length - 1));
                var varnish = Surface("Polished mahogany board " + i, "Mahogany",
                    new Color(.39f * tint, .19f * tint, .102f * tint), .61f, new Vector2(.72f, 1.5f), 0f, .27f);
                Combined("Staggered hardwood boards " + i, boards[i], varnish, ShadowCastingMode.Off);
            }
        }

        void BuildFleece()
        {
            const int around = 192, cross = 14;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            var center = new Vector3(-35f, .56f, -35f);
            var fibers = new FirMesh();
            var random = new System.Random(198);
            for (int i = 0; i <= around; i++)
            {
                float u = i / (float)around, angle = u * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var tangent = Vector3.Cross(Vector3.up, outward);
                for (int j = 0; j <= cross; j++)
                {
                    float v = j / (float)cross, spin = v * Mathf.PI * 2f;
                    var position = center + outward * (80.15f + .31f * Mathf.Sin(angle * 11f) + 3.09f * Mathf.Cos(spin)) +
                        Vector3.up * Mathf.Max(.04f, .45f + Mathf.Sin(spin) * (1.33f + .20f * Mathf.Cos(angle * 19f)));
                    vertices.Add(position);
                    uv.Add(new Vector2(u, v));
                    if (i == around || j == cross)
                        continue;
                    int a = i * (cross + 1) + j, b = a + cross + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(b); triangles.Add(b + 1); triangles.Add(a + 1);
                    if (spin > Mathf.PI)
                        continue;
                    for (int t = 0; t < 3; t++)
                    {
                        var basePoint = position + tangent * Range(random, -.85f, .85f);
                        var end = basePoint + Vector3.up * Range(random, .15f, .54f) +
                            tangent * Range(random, -.37f, .37f);
                        fibers.Needle(basePoint, end, tangent, Range(random, .055f, .09f));
                    }
                }
            }
            var trim = Surface("Ivory fleece edging", "Woven", new Color(.92f, .87f, .79f),
                .19f, new Vector2(34f, 2f), 0f, .4f);
            trim.SetFloat("_Cull", 0f);
            var mesh = new Mesh { name = "Rolled fleece", vertices = vertices.ToArray(),
                uv = uv.ToArray(), triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            AddMesh("Rolled ivory skirt edge", mesh, trim, ShadowCastingMode.TwoSided);
            var threads = Surface("Tufted ivory wool", "Woven", new Color(.98f, .92f, .84f),
                .19f, Vector2.one, 0f, .25f);
            threads.SetFloat("_Cull", 0f);
            AddMesh("Wool fibers", fibers.Build(), threads, ShadowCastingMode.Off);
        }

        void AddGiftBow(MeshRenderer renderer)
        {
            string label = renderer.name;
            if (label != "Wrap" && !(label.StartsWith("Present ", StringComparison.Ordinal) && label.EndsWith(" box", StringComparison.Ordinal)))
                return;
            if (!renderer.TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh)
                return;
            if (!giftBow)
            {
                giftBow = RibbonMesh();
                meshes.Add(giftBow);
            }

            var finish = Surface("Silk bow in warm gold", "Woven", new Color(.86f, .46f, .16f),
                .73f, new Vector2(3, 2), .19f, .32f);
            finish.SetFloat("_Cull", 0f);
            var obj = new GameObject("Hand-tied satin bow", typeof(MeshFilter), typeof(MeshRenderer));
            obj.GetComponent<MeshFilter>().sharedMesh = giftBow;
            var skin = obj.GetComponent<MeshRenderer>();
            skin.sharedMaterial = finish;
            skin.shadowCastingMode = ShadowCastingMode.TwoSided;
            var bounds = filter.sharedMesh.bounds;
            obj.transform.SetParent(renderer.transform, false);
            obj.transform.localPosition = bounds.center + new Vector3(0f, .20f * bounds.size.y, -.502f * bounds.size.z);
            obj.transform.localScale = new Vector3(bounds.size.x * .23f, bounds.size.y * .19f, bounds.size.z * .12f);
            mapDressings.Add(obj);
        }

        static Mesh RibbonMesh()
        {
            const int steps = 32, across = 5;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var faces = new List<int>();
            for (int strip = 0; strip < 4; strip++)
            {
                float sign = strip % 2 == 0 ? -1f : 1f;
                bool loop = strip < 2;
                int first = v.Count;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float arc = Mathf.Sin(t * Mathf.PI);
                    Vector3 center = loop
                        ? new Vector3(sign * 1.52f * arc, .2f * arc, -.53f * arc - .22f * Mathf.Sin(t * Mathf.PI * 2f))
                        : new Vector3(sign * (.07f + .9f * t), -1.42f * t, -.14f - .42f * arc);
                    float width = loop ? Mathf.Lerp(.13f, .48f, arc) : Mathf.Lerp(.14f, .31f, t);
                    for (int j = 0; j <= across; j++)
                    {
                        float u = j / (float)across, edge = u * 2f - 1f;
                        var point = center + (loop ? Vector3.up : Vector3.right) * (width * edge);
                        point.z -= .21f * (1f - edge * edge) + .09f * edge * Mathf.Sin(t * Mathf.PI * 2.7f);
                        if (!loop)
                            point.y += Mathf.Max(0f, (t - .8f) / .2f) * (.31f * (1f - Mathf.Abs(edge)));
                        v.Add(point);
                        uv.Add(new Vector2(t, u));
                        if (i == steps || j == across)
                            continue;
                        int a = first + i * (across + 1) + j;
                        int b = a + across + 1;
                        faces.Add(a); faces.Add(a + 1); faces.Add(b);
                        faces.Add(a + 1); faces.Add(b + 1); faces.Add(b);
                    }
                }
            }
            var mesh = new Mesh { name = "Folded satin bow", vertices = v.ToArray(), uv = uv.ToArray(), triangles = faces.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
