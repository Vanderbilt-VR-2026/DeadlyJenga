using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    // Smooth sea-life bodies built in code: a tube through a list of stations (a centre and two radii each),
    // used for bodies, fins, flukes and wings. Each quad is painted into a submesh (back, belly, ...).
    // The Blender preview of the sea life is built from these same shape tables.
    internal sealed class SeaLifeMesh
    {
        readonly List<Vector3> vertices = new();
        readonly List<int>[] parts;

        public SeaLifeMesh(int partCount)
        {
            parts = new List<int>[partCount];
            for (int i = 0; i < partCount; i++) parts[i] = new List<int>();
        }

        // paint(sideIndex, stationFraction, outward) returns the submesh for that quad.
        public void Loft(Vector3[] centres, float[] side, float[] up, Vector3 upHint, int sides, System.Func<int, float, Vector3, int> paint)
        {
            int count = centres.Length, start = vertices.Count;
            var outward = new Vector3[count * sides];
            for (int i = 0; i < count; i++)
            {
                var tangent = (centres[Mathf.Min(i + 1, count - 1)] - centres[Mathf.Max(i - 1, 0)]).normalized;
                var s = Vector3.Cross(upHint, tangent);
                if (s.sqrMagnitude < 1e-6f) s = Vector3.Cross(Vector3.forward, tangent);
                s.Normalize();
                var u = Vector3.Cross(tangent, s);
                for (int j = 0; j < sides; j++)
                {
                    float a = j * Mathf.PI * 2 / sides;
                    var dir = s * Mathf.Cos(a) + u * Mathf.Sin(a);
                    outward[i * sides + j] = dir;
                    vertices.Add(centres[i] + s * (Mathf.Cos(a) * side[i]) + u * (Mathf.Sin(a) * up[i]));
                }
            }
            for (int i = 0; i < count - 1; i++)
                for (int j = 0; j < sides; j++)
                {
                    int k = (j + 1) % sides;
                    int a = start + i * sides + j, b = start + i * sides + k, c = start + (i + 1) * sides + k, d = start + (i + 1) * sides + j;
                    var o = (outward[i * sides + j] + outward[i * sides + k]).normalized;
                    int part = paint(j, (i + .5f) / (count - 1), o);
                    Triangle(part, a, b, c, o);
                    Triangle(part, a, c, d, o);
                }
            // Close both ends with a fan.
            for (int end = 0; end < 2; end++)
            {
                int i = end == 0 ? 0 : count - 1;
                var axis = (centres[end == 0 ? 0 : count - 1] - centres[end == 0 ? 1 : count - 2]).normalized;
                int centre = vertices.Count;
                vertices.Add(centres[i]);
                int part = paint(0, end, axis);
                for (int j = 0; j < sides; j++)
                    Triangle(part, centre, start + i * sides + j, start + i * sides + (j + 1) % sides, axis);
            }
        }

        // Winding is chosen so every face points outward, whichever way the tube runs.
        void Triangle(int part, int a, int b, int c, Vector3 outward)
        {
            var n = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            var list = parts[part];
            if (Vector3.Dot(n, outward) >= 0) { list.Add(a); list.Add(b); list.Add(c); }
            else { list.Add(a); list.Add(c); list.Add(b); }
        }

        public void Ellipsoid(Vector3 centre, Vector3 radii, int part, int rings = 8, int sides = 12)
        {
            var c = new Vector3[rings + 1];
            var s = new float[rings + 1];
            var u = new float[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings, a = Mathf.Lerp(-Mathf.PI / 2, Mathf.PI / 2, t);
                c[i] = centre + Vector3.forward * (Mathf.Sin(a) * radii.z);
                s[i] = Mathf.Max(.0005f, Mathf.Cos(a) * radii.x);
                u[i] = Mathf.Max(.0005f, Mathf.Cos(a) * radii.y);
            }
            Loft(c, s, u, Vector3.up, sides, (j, f, o) => part);
        }

        // A tube through stations given as (along, side radius, up radius, height) rows, smoothed with
        // Catmull-Rom into `count` stations. along and height are fractions of length, along 0 = tail, 1 = nose.
        public void Body(float length, float[,] rows, int count, int sides, System.Func<int, float, Vector3, int> paint)
        {
            int n = rows.GetLength(0);
            var c = new Vector3[count];
            var s = new float[count];
            var u = new float[count];
            for (int i = 0; i < count; i++)
            {
                float f = i / (float)(count - 1) * (n - 1);
                int k = Mathf.Min((int)f, n - 2);
                float t = f - k;
                float Row(int col) => CatmullRom(rows[Mathf.Max(k - 1, 0), col], rows[k, col], rows[k + 1, col], rows[Mathf.Min(k + 2, n - 1), col], t);
                c[i] = new Vector3(0, Row(3) * length, (Row(0) - .5f) * length);
                s[i] = Mathf.Max(.002f, Row(1) * length);
                u[i] = Mathf.Max(.002f, Row(2) * length);
            }
            Loft(c, s, u, Vector3.up, sides, paint);
        }

        // A flat blade (fin, fluke, wing): spine points with chord and thickness radii; upHint is the
        // direction the blade's thickness faces.
        public void Blade(Vector3[] spine, float[] chord, float[] thickness, Vector3 upHint, System.Func<int, float, Vector3, int> paint)
            => Loft(spine, chord, thickness, upHint, 10, paint);

        static float CatmullRom(float p0, float p1, float p2, float p3, float t)
            => .5f * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t);

        public Mesh Mesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.subMeshCount = parts.Length;
            for (int i = 0; i < parts.Length; i++) mesh.SetTriangles(parts[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    // Shape tables for the dolphin and seagull (the humpback is authored in Blender, see HumpbackWhale). Sizes are in metres; animals face +z, up is +y.
    internal static class SeaLifeShapes
    {
        // Bottlenose dolphin. Parts: 0 back, 1 belly, 2 eye.
        public static Mesh Dolphin(float L)
        {
            var m = new SeaLifeMesh(3);
            var rows = new float[,]
            {
                { 0, .012f, .020f, 0 }, { .08f, .030f, .050f, 0 }, { .20f, .060f, .085f, .005f }, { .35f, .095f, .110f, .012f },
                { .50f, .110f, .120f, .012f }, { .62f, .108f, .115f, .010f }, { .72f, .095f, .100f, .006f }, { .80f, .080f, .085f, .002f },
                { .85f, .065f, .070f, 0 }, { .88f, .040f, .035f, -.020f }, { .94f, .030f, .025f, -.025f }, { 1, .012f, .012f, -.028f },
            };
            m.Body(L, rows, 36, 24, (j, f, o) => o.y < -.15f ? 1 : 0);
            for (int s = -1; s <= 1; s += 2)
            {
                m.Blade(new[] { new Vector3(s * .01f, 0, -.49f) * L, new Vector3(s * .10f, .003f, -.52f) * L, new Vector3(s * .17f, .005f, -.56f) * L },
                    new[] { .040f * L, .030f * L, .008f * L }, new[] { .008f * L, .006f * L, .002f * L }, Vector3.up, (j, f, o) => 0);
                m.Blade(new[] { new Vector3(s * .08f, -.06f, .18f) * L, new Vector3(s * .14f, -.09f, .12f) * L, new Vector3(s * .18f, -.11f, .07f) * L },
                    new[] { .035f * L, .028f * L, .008f * L }, new[] { .008f * L, .006f * L, .002f * L }, Vector3.up, (j, f, o) => 0);
                m.Ellipsoid(new Vector3(s * .068f, .005f, .33f) * L, Vector3.one * (.009f * L), 2);
            }
            m.Blade(new[] { new Vector3(0, .10f, 0) * L, new Vector3(0, .15f, -.04f) * L, new Vector3(0, .18f, -.10f) * L },
                new[] { .070f * L, .040f * L, .008f * L }, new[] { .012f * L, .008f * L, .002f * L }, Vector3.right, (j, f, o) => 0);
            return m.Mesh("Dolphin");
        }

        // Seagull body (wings are separate so they can flap). Parts: 0 white, 1 grey, 2 black, 3 beak, 4 eye.
        public static Mesh GullBody()
        {
            var m = new SeaLifeMesh(5);
            var rows = new float[,]
            {
                { 0, .020f, .015f, 0 }, { .15f, .070f, .050f, 0 }, { .35f, .130f, .110f, 0 }, { .55f, .140f, .120f, .010f },
                { .72f, .110f, .100f, .030f }, { .80f, .080f, .085f, .070f }, { .88f, .095f, .095f, .100f }, { .96f, .070f, .070f, .100f }, { 1, .020f, .020f, .095f },
            };
            m.Body(.62f, rows, 26, 18, (j, f, o) => o.y > .55f && f > .15f && f < .75f ? 1 : 0);
            m.Blade(new[] { new Vector3(0, .062f, .29f), new Vector3(0, .058f, .34f), new Vector3(0, .052f, .40f) },
                new[] { .018f, .012f, .003f }, new[] { .016f, .010f, .003f }, Vector3.up, (j, f, o) => 3);
            for (int s = -1; s <= 1; s += 2) m.Ellipsoid(new Vector3(s * .042f, .075f, .25f), Vector3.one * .012f, 4, 6, 8);
            m.Blade(new[] { new Vector3(0, .005f, -.26f), new Vector3(0, .008f, -.36f), new Vector3(0, .01f, -.42f) },
                new[] { .045f, .07f, .08f }, new[] { .012f, .008f, .004f }, Vector3.up, (j, f, o) => o.y > 0 ? 1 : 0);
            return m.Mesh("Seagull body");
        }

        // One wing segment, built out to +x (mirrored with a negative x scale for the left wing).
        // inner = arm (shoulder to wrist), otherwise the hand with its black tip.
        public static Mesh GullWing(bool inner)
        {
            var m = new SeaLifeMesh(5);
            if (inner)
                m.Blade(new[] { Vector3.zero, new Vector3(.28f, .01f, -.01f), new Vector3(.55f, 0, -.02f) },
                    new[] { .14f, .14f, .13f }, new[] { .025f, .022f, .018f }, Vector3.up, (j, f, o) => o.y > 0 ? 1 : 0);
            else
                m.Blade(new[] { Vector3.zero, new Vector3(.25f, 0, -.04f), new Vector3(.45f, -.01f, -.09f), new Vector3(.58f, -.02f, -.13f) },
                    new[] { .13f, .11f, .07f, .025f }, new[] { .018f, .014f, .010f, .005f }, Vector3.up, (j, f, o) => f > .55f ? 2 : (o.y > 0 ? 1 : 0));
            return m.Mesh(inner ? "Seagull arm" : "Seagull hand");
        }

    }
}
