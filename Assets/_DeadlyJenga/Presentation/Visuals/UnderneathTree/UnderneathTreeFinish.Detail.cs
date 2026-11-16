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
            BuildSofa();
        }

        Material Fabric(string label, string tile, Color tint, Vector2 repeat, float sheen, float bump)
        {
            if (finishes.TryGetValue(label, out var cached))
                return cached;

            var shader = Resources.Load<Shader>(ResourcePath + "Christmas Fabric");
            if (!shader)
                return Surface(label, tile, tint, .17f, repeat, 0f, bump);

            var material = new Material(shader) { name = label, color = tint };
            material.SetTexture("_BaseMap", Resources.Load<Texture2D>(ResourcePath + tile));
            material.SetTexture("_BumpMap", Resources.Load<Texture2D>(ResourcePath + (tile == "Red Velvet" ? "Woven" : tile) + " Normal"));
            material.SetTextureScale("_BaseMap", repeat);
            material.SetFloat("_BumpScale", bump);
            material.SetFloat("_Sheen", sheen);
            materials.Add(material);
            finishes.Add(label, material);
            return material;
        }

        void BuildSofa()
        {
            var velvet = Fabric("Deep red patterned upholstery", "Red Velvet",
                new Color(.92f, .65f, .68f), new Vector2(2.1f, 2.3f), .14f, .48f);
            var lining = Fabric("Dark red velvet sofa frame", "Velour Scan",
                new Color(.57f, .43f, .43f), new Vector2(3.0f, 3.3f), .07f, .4f);
            var piping = Fabric("Red velvet seam piping", "Velour Scan",
                new Color(.74f, .53f, .52f), Vector2.one, .21f, .2f);
            var walnut = Surface("Carved sofa feet", "Walnut Grain", new Color(.40f, .29f, .22f),
                .40f, new Vector2(1.1f, 1.6f), 0f, .28f);
            RoundedDetail("Velvet sofa plinth", new Vector3(-216f, 13f, -109f),
                new Vector3(67f, 16f, 145f), .17f, lining, .025f);
            RoundedDetail("Overstuffed sofa apron", new Vector3(-197f, 20f, -109f),
                new Vector3(30f, 16.3f, 136f), .17f, velvet, .035f);
            var back = RoundedDetail("Tufted sofa back", new Vector3(-242f, 39f, -109f),
                new Vector3(19f, 43f, 147f), .20f, velvet, .034f);
            back.localRotation = Quaternion.Euler(0f, 0f, 8f);
            foreach (float z in new[] { -178.8f, -39.2f })
            {
                RoundedDetail("Velvet rolled arm", new Vector3(-218f, 31f, z),
                    new Vector3(66f, 34f, 16.0f), .30f, velvet, .07f);
                RoundedDetail("Inset arm front", new Vector3(-187.4f, 29f, z),
                    new Vector3(9f, 30f, 14.7f), .33f, velvet, .065f);
                foreach (float x in new[] { -237f, -192f })
                    RoundedDetail("Walnut bun foot", new Vector3(x, 4.7f, z),
                        new Vector3(8f, 8.7f, 8f), .33f, walnut, .055f);
            }

            for (int i = 0; i < 3; i++)
            {
                float z = -152f + 43f * i;
                var seat = RoundedDetail("Stuffed velvet seat cushion", new Vector3(-212f, 28f + (i == 1 ? .33f : 0f), z),
                    new Vector3(48f, 9.7f, 42f), .19f, velvet, .065f);
                SofaSeam(seat, new Vector3(0f, 4.35f, 0f), new Vector2(22.9f, 20.15f), 3.6f, piping);
                var cushion = RoundedDetail("Loose velvet back cushion", new Vector3(-233.1f, 44f, z),
                    new Vector3(16f, 28.0f, 42f), .22f, velvet, .077f);
                cushion.localRotation = Quaternion.Euler(.7f * (i - 1), i == 1 ? .9f : -.7f, 10.5f + i * .8f);
                SofaSeam(cushion, new Vector3(7.4f, 0f, 0f), new Vector2(12.8f, 20.0f),
                    3.45f, piping, true);
            }
        }

        void SofaSeam(Transform cushion, Vector3 center, Vector2 half, float rounding, Material piping,
            bool vertical = false)
        {
            var path = new FirMesh();
            Vector3 previous = default, first = default;
            for (int corner = 0; corner < 4; corner++)
            {
                float angle = corner * Mathf.PI * .5f;
                var cornerCenter = new Vector2(
                    Mathf.Sign(Mathf.Cos(angle + .785398f)) * (half.x - rounding),
                    Mathf.Sign(Mathf.Sin(angle + .785398f)) * (half.y - rounding));
                for (int step = 0; step <= 6; step++)
                {
                    float t = angle + step * Mathf.PI / 12f;
                    var p = cornerCenter + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * rounding;
                    var point = center + (vertical ? new Vector3(0f, p.x, p.y) : new Vector3(p.x, 0f, p.y));
                    if (corner == 0 && step == 0)
                        first = point;
                    else
                        path.Tube(previous, point, .215f, .215f, 6);
                    previous = point;
                }
            }
            path.Tube(previous, first, .215f, .215f, 6);
            AddMesh("Velvet welt seam", path.Build(), piping, ShadowCastingMode.Off).transform.SetParent(cushion, false);
        }

        Transform RoundedDetail(string label, Vector3 position, Vector3 size, float radius, Material material,
            float loft = 0f)
        {
            var mesh = RoundedWrap(new Bounds(Vector3.zero, size), radius, loft);
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
                var varnish = Surface("Polished walnut board " + i, "Walnut Grain",
                    new Color(.75f * tint, .57f * tint, .43f * tint), .58f, new Vector2(.86f, 1.05f), 0f, .35f);
                Combined("Staggered hardwood boards " + i, boards[i], varnish, ShadowCastingMode.Off);
            }
        }

        void BuildFleece()
        {
            const int around = 384, cross = 14;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            var center = new Vector3(-90f, .56f, -110f);

            for (int i = 0; i <= around; i++)
            {
                float u = i / (float)around, angle = u * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var tangent = Vector3.Cross(Vector3.up, outward);
                for (int j = 0; j <= cross; j++)
                {
                    float v = j / (float)cross, spin = v * Mathf.PI * 2f;
                    var position = center + outward * (115.15f + .31f * Mathf.Sin(angle * 11f) + (2.77f + .19f * Mathf.Cos(angle * 96f + spin * 3f)) * Mathf.Cos(spin)) +
                        Vector3.up * Mathf.Max(.04f, .40f + Mathf.Sin(spin) * (1.06f + .16f * Mathf.Cos(angle * 19f)));
                    vertices.Add(position);
                    uv.Add(new Vector2(u, v));
                    if (i == around || j == cross)
                        continue;
                    int a = i * (cross + 1) + j, b = a + cross + 1;
                    triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                    triangles.Add(b); triangles.Add(a + 1); triangles.Add(b + 1);
                }
            }
            var trim = Fabric("Ivory scanned fleece edging", "Fleece Scan", new Color(1.15f, 1.15f, 1.10f),
                new Vector2(36f, 1.22f), .16f, .73f);
            var mesh = new Mesh { name = "Rolled fleece", vertices = vertices.ToArray(),
                uv = uv.ToArray(), triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            AddMesh("Rolled ivory skirt edge", mesh, trim, ShadowCastingMode.TwoSided);
        }

        void AddGiftBow(MeshRenderer renderer)
        {
            var panel = renderer.transform;
            if ((renderer.name != "Visual" && renderer.name != "Curved wrapping") || !panel.parent || panel.parent.name != "Lid")
                return;
            if (panel.parent.parent && panel.parent.parent.name.StartsWith("Rail Gift ", StringComparison.Ordinal))
                return;
            var ribbon = panel.parent.Find("Ribbon lengthwise");
            if (!ribbon || !ribbon.TryGetComponent<MeshRenderer>(out var strip) || !strip.sharedMaterial)
                return;
            if (!giftBow)
            {
                giftBow = RibbonMesh();
                meshes.Add(giftBow);
            }

            var finish = ChooseFinish(strip.name, strip.sharedMaterial) ?? strip.sharedMaterial;
            var bowMaterial = Surface("Bow " + finish.name, "Woven", finish.color, .72f,
                new Vector2(3f, 2f), .1f, .25f);
            bowMaterial.SetFloat("_Cull", 0f);
            var obj = new GameObject("Hand-tied satin bow", typeof(MeshFilter), typeof(MeshRenderer));
            obj.GetComponent<MeshFilter>().sharedMesh = giftBow;
            var skin = obj.GetComponent<MeshRenderer>();
            skin.sharedMaterial = bowMaterial;
            skin.shadowCastingMode = ShadowCastingMode.TwoSided;
            var size = renderer.name == "Curved wrapping"
                ? panel.GetComponent<MeshFilter>().sharedMesh.bounds.size : panel.localScale;
            obj.transform.SetParent(panel.parent, false);
            obj.transform.localPosition = renderer.name == "Curved wrapping"
                ? new Vector3(0f, .5f * size.y + .046f, 0f)
                : new Vector3(0f, .5f * size.y + .046f, 0f);
            obj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            float span = Mathf.Min(size.x, size.z);
            obj.transform.localScale = new Vector3(Mathf.Clamp(span * .28f, .3f, 4.0f),
                Mathf.Clamp(span * .15f, .2f, 2.0f), Mathf.Clamp(span * .22f, .2f, 2.4f));
            var knot = new GameObject("Satin bow knot", typeof(MeshFilter), typeof(MeshRenderer));
            knot.transform.SetParent(obj.transform, false);
            knot.transform.localPosition = new Vector3(0f, 0f, -.14f);
            knot.transform.localScale = new Vector3(.52f, .42f, .50f);
            if (!roundedWrap) roundedWrap = RoundedWrap(new Bounds(Vector3.zero, Vector3.one));
            knot.GetComponent<MeshFilter>().sharedMesh = roundedWrap;
            knot.GetComponent<MeshRenderer>().sharedMaterial = bowMaterial;
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
