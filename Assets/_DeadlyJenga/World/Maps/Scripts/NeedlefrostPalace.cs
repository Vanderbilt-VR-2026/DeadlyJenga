using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    internal sealed partial class NeedlefrostLayout
    {
        internal const int FirstBlenderRoof = 100;
        const int CrownBlade = 0, WestBlade = 1, EastBlade = 2;
        const int WestTowerRoof = 3, EastTowerRoof = 4, WestWingRoof = 5, EastWingRoof = 6;

        [Serializable]
        internal sealed class SculptedIce
        {
            public string name;
            public Vector3 position, size;
            public Vector3[] vertices;
            public int[] triangles;
            [NonSerialized] public Mesh mesh;

            public Mesh CreateMesh()
            {
                if (mesh) return mesh;
                mesh = new Mesh { name = name };
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        [Serializable]
        sealed class SculptedIceAsset
        {
            public SculptedIce[] pieces;
        }

        static SculptedIce[] sculpts;

        internal static SculptedIce Sculpture(int course)
        {
            if (sculpts == null)
            {
                var asset = Resources.Load<TextAsset>("NeedlefrostPalaceRoofs");
                if (!asset) throw new InvalidOperationException("Needlefrost Palace sculpted roof asset is missing.");
                sculpts = JsonUtility.FromJson<SculptedIceAsset>(asset.text).pieces;
            }
            return sculpts[course - FirstBlenderRoof];
        }

        void Sculpt(int index)
        {
            int course = FirstBlenderRoof + index;
            var piece = Sculpture(course);
            parts.Add(new CastlePlacement(piece.position, Quaternion.identity, CastleSection.Aqua, course, piece.size));
        }

        internal static void JoinSculptedSeats(List<CastlePlacement> layout, List<Rigidbody> bodies)
        {
            for (int i = 0; i < layout.Count; i++)
            {
                var piece = layout[i];
                if (piece.Course < FirstBlenderRoof) continue;
                var bottom = piece.Position - piece.Size * .5f;
                var top = piece.Position + piece.Size * .5f;
                int seat = -1;
                float largestContact = 0;
                for (int j = 0; j < layout.Count; j++)
                {
                    var bearing = layout[j];
                    if (bearing.Course != (int)Cut.Lintel && bearing.Course != (int)Cut.Column) continue;
                    var lower = bearing.Position - bearing.Size * .5f;
                    var upper = bearing.Position + bearing.Size * .5f;
                    if (Mathf.Abs(upper.y - bottom.y) > .055f) continue;
                    float width = Mathf.Min(top.x, upper.x) - Mathf.Max(bottom.x, lower.x);
                    float depth = Mathf.Min(top.z, upper.z) - Mathf.Max(bottom.z, lower.z);
                    if (width <= 0 || depth <= 0 || width * depth <= largestContact) continue;
                    largestContact = width * depth;
                    seat = j;
                }
                if (seat < 0) continue;

                // The crown and roofs freeze into their moving bearing. The ice seam
                // can shear under load and it never binds the roof to the scene.
                var body = bodies[i];
                var joint = body.gameObject.AddComponent<FixedJoint>();
                joint.connectedBody = bodies[seat];
                joint.anchor = new Vector3(0, -.5f, 0);
                joint.breakForce = Mathf.Clamp(body.mass * 50, 11000, 82000);
                joint.breakTorque = joint.breakForce * .45f;
            }
        }

        // Engaged turrets and roofs are carved into their neighbours. Those pairs start inside each
        // other, and pushing them apart at load throws the tower tops off. Let such pairs pass through
        // each other. Pieces resting on top of each other (a shallow, vertical overlap) still collide.
        internal static List<Vector2Int> ReleaseEngagedMembers(List<Rigidbody> bodies)
        {
            Physics.SyncTransforms();
            var shapes = new Collider[bodies.Count][];
            for (int i = 0; i < bodies.Count; i++)
                shapes[i] = Array.FindAll(bodies[i].GetComponentsInChildren<Collider>(), c => c.enabled && !c.isTrigger);
            var pairs = new List<Vector2Int>();
            for (int i = 0; i < shapes.Length; i++)
            for (int j = i + 1; j < shapes.Length; j++)
            {
                bool engaged = false;
                foreach (var a in shapes[i])
                foreach (var b in shapes[j])
                {
                    if (Physics.GetIgnoreLayerCollision(a.gameObject.layer, b.gameObject.layer) || !a.bounds.Intersects(b.bounds)) continue;
                    if (!Physics.ComputePenetration(a, a.transform.position, a.transform.rotation,
                            b, b.transform.position, b.transform.rotation, out var direction, out float depth)) continue;
                    if (depth <= .04f && (depth <= .01f || Mathf.Abs(direction.y) >= .7f)) continue;
                    Physics.IgnoreCollision(a, b);
                    engaged = true;
                }
                if (engaged) pairs.Add(new Vector2Int(i, j));
            }
            return pairs;
        }

        // Up in the palace, the carved-in pieces are one frame: tie each engaged pair with a
        // breakable joint so an overhanging blade or wing cannot slowly peel away from it.
        internal static void TieEngagedMembers(List<CastlePlacement> layout, List<Rigidbody> bodies, List<Vector2Int> pairs)
        {
            const float palaceFloor = 9;
            foreach (var pair in pairs)
            {
                var a = layout[pair.x];
                var b = layout[pair.y];
                if (a.Position.y - a.Size.y * .5f < palaceFloor || b.Position.y - b.Size.y * .5f < palaceFloor) continue;
                var joint = bodies[pair.x].gameObject.AddComponent<FixedJoint>();
                joint.connectedBody = bodies[pair.y];
                joint.breakForce = Mathf.Clamp(Mathf.Min(bodies[pair.x].mass, bodies[pair.y].mass) * 50, 11000, 82000);
                joint.breakTorque = joint.breakForce * .45f;
            }
        }

        void GrandKeep()
        {
            const float firstFloor = 16.15f, secondFloor = 21.52f, eaves = 26.36f;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 7.69f;
                float crossing = side < 0 ? 17 : 13;
                float deck = side < 0 ? 15 : 16;
                // Separate floor pieces sit on the two sides of the existing upper approach.
                // The original ramp remains the walking surface inside its corridor.
                for (int end = -1; end <= 1; end += 2)
                {
                    float from = end < 0 ? 10.92f : crossing + 1.23f;
                    float to = end < 0 ? crossing - 1.23f : 19.64f;
                    float z = (from + to) * .5f;
                    float depth = to - from;
                    for (int edge = -1; edge <= 1; edge += 2)
                    {
                        Stone(x + edge * 2.27f, 9, z, .88f, deck - 9 - .46f,
                            Mathf.Min(.95f, depth), Cut.Column);
                    }
                    Stone(x, deck - .46f, z, 6, .46f, depth, Cut.Lintel);
                }

                WingStorey(x, deck, secondFloor);
                WingStorey(x, secondFloor, eaves);
                Sculpt(side < 0 ? WestWingRoof : EastWingRoof);

                // The turret bears on the outer court in front of the crossing.
                float turret = side * 11.22f;
                Stone(turret, 9, 10.41f, 1.98f, firstFloor - 9, 1.98f, Cut.Column);
                Stone(turret, firstFloor, 10.41f, 1.88f, 4.5f, 1.88f, Cut.Column);
                Stone(turret, 20.65f, 10.41f, 1.82f, 4.38f, 1.82f, Cut.Column);
                Stone(turret, 25.03f, 10.41f, 2.38f, 7.3f, 2.34f, Cut.Roof);

                float bladeX = side < 0 ? -3.65f : 3.65f;
                var blade = Sculpture(FirstBlenderRoof + (side < 0 ? WestBlade : EastBlade));
                float bladeBottom = blade.position.y - blade.size.y * .5f;
                Stone(bladeX, 21.15f, 15, 1.19f, bladeBottom - 21.15f, 1.42f, Cut.Column);
                Sculpt(side < 0 ? WestBlade : EastBlade);
            }

            Gallery(0, 21.15f, 15, 7.68f, 5.74f, 8.26f);
            Stone(0, 21.15f, 12.4f, 5.3f, 7.05f, .52f, Cut.Portal);
            Stone(0, 21.15f, 17.69f, 5, 7.01f, .55f, Cut.Column);
            for (int i = -2; i <= 2; i++)
                Stone(i * .86f, 21.15f, 17.30f, .55f, 3.42f - .34f * Mathf.Abs(i), .39f, Cut.Window);
            Sculpt(CrownBlade);
        }

        void WingStorey(float x, float floor, float ceiling)
        {
            float height = ceiling - floor;
            // Wide portal leaves an open gallery and carries the roof at the front.
            Stone(x, floor, 11.42f, 5.95f, height - .5f, .67f, Cut.Portal);
            Stone(x, floor, 19.11f, 5.92f, height - .5f, .68f, Cut.Column);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Stone(x + sign * 2.61f, floor, 15.27f, .66f, height - .5f, .73f, Cut.Column);
                Stone(x + sign * 1.56f, floor, 18.66f, .61f, 2.77f, .33f, Cut.Window);
            }
            Stone(x, floor, 18.66f, .67f, 3.06f, .33f, Cut.Window);
            Stone(x, ceiling - .5f, 15.27f, 6.34f, .5f, 8.02f, Cut.Lintel);
        }

        void UpperPavilion(float x, float z, float floor, float height)
        {
            bool west = x < 0;
            Gallery(x, floor, z, 7.50f, 6.98f, height);
            Stone(x, floor, z - 3.02f, 4.63f, height - 1, .48f, Cut.Portal);
            Stone(x, floor, z + 2.84f, 3.62f, height - 1, .58f, Cut.Column);
            for (int i = -1; i <= 1; i++)
                Stone(x + i * .96f, floor, z + 2.47f, .58f, 3.16f + (i == 0 ? .30f : 0), .36f, Cut.Window);
            float eaves = floor + height;
            Sculpt(west ? WestTowerRoof : EastTowerRoof);
            Stone(x, eaves + (west ? 10.4f : 11.25f), z, .72f, 3.24f, .72f, Cut.Blade);
            for (int a = -1; a <= 1; a += 2)
            for (int b = -1; b <= 1; b += 2)
            {
                // Small turrets bear on the deck corners outside the flared roof.
                float px = x + a * 3.52f, pz = z + b * 3.34f;
                Stone(px, eaves, pz, .73f, 1.08f, .73f, Cut.Column);
                Stone(px, eaves + 1.08f, pz, .77f, 3.60f, .77f, Cut.Blade);
            }
        }

        void OuterTerraces()
        {
            // The run from the old coast to each outer bank stays open at the waterline.
            Shelf(-35.1f, -19.44f, 3, 8.84f, 8.1f);
            Shelf(-29.32f, -17.05f, 3, 2.77f, 1.6f);
            Shelf(-43.26f, -18.5f, 3, 7.46f, 7.95f);
            Shelf(-43.1f, -9.7f, 4.29f, 7.52f, 4.22f);
            Shelf(-37.3f, -4.55f, 6.18f, 6.96f, 5.15f);
            Shelf(-31.8f, 1.48f, 8.26f, 5.94f, 5.48f);
            Shelf(-29.7f, -27.2f, 2.74f, 7.74f, 7.2f);
            Shelf(-11.5f, -34.76f, 2.51f, 5.7f, 4.03f);
            Shelf(11.4f, -36.60f, 2.51f, 6.8f, 4.06f);
            Shelf(21.0f, -26.9f, 3, 9.3f, 7.74f);
            Shelf(30.1f, -22, 3, 8.86f, 7.77f);
            Shelf(38.7f, -22, 3, 8.24f, 7.7f);
            Shelf(38.7f, -12.0f, 4.89f, 8.25f, 5.65f);
            Shelf(36.5f, -3.8f, 6.65f, 7.0f, 5.45f);
            Shelf(30.9f, 2.62f, 8.7f, 5.97f, 5.25f);

            TerraceFlight(new Vector3(-43.1f, 3, -14.52f), new Vector3(-43.1f, 4.29f, -11.77f));
            TerraceFlight(new Vector3(-41.1f, 4.29f, -8.32f), new Vector3(-39.4f, 6.18f, -6.16f));
            TerraceFlight(new Vector3(-35.18f, 6.18f, -3.07f), new Vector3(-33.79f, 8.26f, -.72f));
            TerraceFlight(new Vector3(38.7f, 3, -18.18f), new Vector3(38.7f, 4.89f, -14.81f));
            TerraceFlight(new Vector3(37.92f, 4.89f, -9.19f), new Vector3(37.16f, 6.65f, -6.51f));
            TerraceFlight(new Vector3(34.65f, 6.65f, -1.65f), new Vector3(32.37f, 8.7f, .01f));

            foreach (var ledge in new[] { new Vector3(-43.1f, 4.29f, -9.6f), new Vector3(-37.3f, 6.18f, -4.5f),
                         new Vector3(40.05f, 4.89f, -11.44f), new Vector3(38.09f, 6.65f, -3.38f) })
            {
                Stone(ledge.x, ledge.y, ledge.z, 3.87f, 2.3f, .89f, Cut.Portal);
                Stone(ledge.x, ledge.y + 2.3f, ledge.z, 4.04f, .34f, 1.02f, Cut.Lintel);
                Lamp(ledge.x + 1.37f, ledge.y, ledge.z - .66f);
            }
        }

        void Shelf(float x, float z, float top, float width, float depth)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                float center = x + sign * width * .25f;
                Stone(center, -3, z, width * .31f, top + 2.02f, depth * .67f, Cut.Pier);
                Stone(center, top - .98f, z, width * .5f - .024f, .98f, depth - .024f, Cut.Slab);
            }
        }

        void TerraceFlight(Vector3 start, Vector3 finish)
        {
            var delta = finish - start;
            var center = (start + finish) * .5f;
            float run = Mathf.Sqrt(delta.x * delta.x + delta.z * delta.z);
            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            // The open-water flight has a real pier to bear on before its top tread.
            var bearing = start + delta * .68f;
            Stone(bearing.x, -3, bearing.z, .81f, start.y + 3, .53f, Cut.Pier);
            Stone(center.x, start.y, center.z, 2.32f, delta.y, run, Cut.Flight, yaw);
        }
    }
}
