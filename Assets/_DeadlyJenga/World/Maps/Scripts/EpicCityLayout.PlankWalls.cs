using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Candy Sky Ring City: Jenga-style plank walls on every Jenga tower floor.
    // Each floor keeps one body (the Hand still pulls whole floors); the walls are extra shapes of that body.
    // Rows alternate like a real Jenga tower: one row of long planks, then a row of three plank ends with
    // window gaps between them, turned 90 degrees on the neighbouring walls.
    // Walls are added after every bridge exists, and any plank piece that would block a bridge, a stair
    // landing or a street door is left out, so the routes stay exactly as planned.
    public static partial class EpicCityLayout
    {
        public const float PlankRow = .55f, PlankThickness = 1.2f;

        sealed partial class Builder
        {
            // One wall band per floor: the module, its footprint, the floor's base height and height, and street doors.
            readonly List<(MayhemModule floor, float w, float depth, float baseY, float height, bool doors)> jengaFloors = new();

            void RememberFloor(MayhemModule floor, float w, float depth, int level) => jengaFloors.Add((floor, w, depth, 0, 4, level == 0));

            // Transplanted cassettes (the Downtown drop-stop tower, the lower Grape Heights keep) get the same walls,
            // so they read as buildings instead of bare frames. The keep's winged tier stays open: its wings swing
            // down from the spine's sides and players cross between them there.
            void RememberCassettes()
            {
                foreach (var m in plan.modules)
                {
                    int floors; float height;
                    switch (m.purpose)
                    {
                        case "Escape spine": floors = 3; height = 5; break; // built 1.25 times taller
                        case "Two-floor dropping cassette": floors = 2; height = 4; break;
                        case "Independent bookfall support spine": if (m.position.y > 20) continue; floors = 3; height = 4; break;
                        default: continue;
                    }
                    // The first four shapes are the corner posts, 2.5 m square, set in 1.25 m from each edge.
                    var post = m.shapes[0].center; float w = Mathf.Abs(post.x) * 2 + 2.5f, depth = Mathf.Abs(post.z) * 2 + 2.5f;
                    bool street = m.position.y < 2;
                    for (int f = 0; f < floors; f++) jengaFloors.Add((m, w, depth, f * height, height, street && f == 0));
                }
            }

            public void PlankWalls()
            {
                RememberCassettes();
                var keepClear = new List<(Vector3 c, Vector3 h, Quaternion q)>();
                foreach (var m in plan.modules)
                {
                    if (!m.purpose.Contains("crossing") && !m.purpose.Contains("trestle")) continue;
                    foreach (var s in m.shapes)
                    {
                        var q = m.rotation * s.rotation; var center = m.position + m.rotation * s.center; var size = s.size;
                        if (s.vertices != null)
                        {
                            var b = new Bounds(s.vertices[0], Vector3.zero); foreach (var v in s.vertices) b.Encapsulate(v);
                            center += q * b.center; size = b.size;
                        }
                        // Walking room: the deck itself plus 2.3 m of head room and a little to each side.
                        keepClear.Add((center + Vector3.up * 1.1f, size * .5f + new Vector3(.6f, 1.25f, .6f), q));
                    }
                }
                foreach (var f in jengaFloors) WallFloor(f.floor, f.w, f.depth, f.baseY, f.height, f.doors, keepClear);
            }

            void WallFloor(MayhemModule floor, float w, float depth, float baseY, float height, bool doors, List<(Vector3 c, Vector3 h, Quaternion q)> keepClear)
            {
                int rows = Mathf.FloorToInt((height - .7f) / PlankRow); // up to the underside of the deck above
                for (int wall = 0; wall < 4; wall++)
                {
                    bool alongX = wall < 2; float sign = wall % 2 == 0 ? -1 : 1;
                    float length = (alongX ? w : depth) - 4.8f; // between the corner posts
                    float offset = ((alongX ? depth : w) * .5f) - .6f - PlankThickness * .5f;
                    for (int row = 0; row < rows; row++)
                    {
                        float y = baseY + .03f + PlankRow * (row + .5f);
                        bool planks = (row % 2 == 0) == alongX;
                        // Street doors on the ground floor, front and back.
                        bool door = doors && alongX && row < 4;
                        if (planks) Run(floor, alongX, sign, offset, y, -length * .5f, length * .5f, door, keepClear);
                        else foreach (float at in new[] { -length * .25f, 0, length * .25f })
                                Run(floor, alongX, sign, offset, y, at - 1.2f, at + 1.2f, door, keepClear);
                    }
                }
            }

            // One plank (or plank end) along a wall from a to b, split around anything that must stay open.
            void Run(MayhemModule floor, bool alongX, float sign, float offset, float y, float a, float b, bool door,
                List<(Vector3 c, Vector3 h, Quaternion q)> keepClear)
            {
                const float piece = .6f;
                int count = Mathf.Max(1, Mathf.RoundToInt((b - a) / piece)); float step = (b - a) / count;
                var open = new bool[count];
                for (int i = 0; i < count; i++)
                {
                    float x = a + (i + .5f) * step;
                    if (door && Mathf.Abs(x) < 2.6f) { open[i] = true; continue; }
                    var local = Local(alongX, sign, offset, y, x); var half = Half(alongX, step * .5f);
                    var c = floor.position + floor.rotation * local;
                    foreach (var k in keepClear) if (Overlaps(c, half, floor.rotation, k.c, k.h, k.q)) { open[i] = true; break; }
                }
                int start = -1;
                for (int i = 0; i <= count; i++)
                {
                    bool solid = i < count && !open[i];
                    if (solid && start < 0) start = i;
                    if (!solid && start >= 0)
                    {
                        float from = a + start * step, to = a + i * step;
                        if (to - from > .5f) Part(floor, Local(alongX, sign, offset, y, (from + to) * .5f), Half(alongX, (to - from) * .5f) * 2, 0);
                        start = -1;
                    }
                }
            }

            static Vector3 Local(bool alongX, float sign, float offset, float y, float x) => alongX ? new Vector3(x, y, sign * offset) : new Vector3(sign * offset, y, x);
            static Vector3 Half(bool alongX, float halfLength) => alongX ? new Vector3(halfLength, PlankRow * .5f, PlankThickness * .5f) : new Vector3(PlankThickness * .5f, PlankRow * .5f, halfLength);

            // Separating-axis test for two oriented boxes.
            static bool Overlaps(Vector3 ca, Vector3 ha, Quaternion qa, Vector3 cb, Vector3 hb, Quaternion qb)
            {
                var A = new[] { qa * Vector3.right, qa * Vector3.up, qa * Vector3.forward };
                var B = new[] { qb * Vector3.right, qb * Vector3.up, qb * Vector3.forward };
                var d = cb - ca;
                bool Separated(Vector3 axis)
                {
                    if (axis.sqrMagnitude < 1e-6f) return false;
                    axis.Normalize();
                    float ra = ha.x * Mathf.Abs(Vector3.Dot(A[0], axis)) + ha.y * Mathf.Abs(Vector3.Dot(A[1], axis)) + ha.z * Mathf.Abs(Vector3.Dot(A[2], axis));
                    float rb = hb.x * Mathf.Abs(Vector3.Dot(B[0], axis)) + hb.y * Mathf.Abs(Vector3.Dot(B[1], axis)) + hb.z * Mathf.Abs(Vector3.Dot(B[2], axis));
                    return Mathf.Abs(Vector3.Dot(d, axis)) > ra + rb;
                }
                for (int i = 0; i < 3; i++) { if (Separated(A[i]) || Separated(B[i])) return false; }
                for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) if (Separated(Vector3.Cross(A[i], B[j]))) return false;
                return true;
            }
        }
    }
}
