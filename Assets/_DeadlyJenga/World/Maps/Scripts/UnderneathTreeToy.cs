using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Put this on the root of a toy prefab and the model under a child called "Visual".
    // When the map places the toy it fits a box around the model, so a new model or size needs no setup.
    // To use your own collision shape instead, add colliders to the prefab yourself.
    // A toy set (a tower, a house) is built from parts whose names start with "Piece": each part
    // gets a collision shape wrapped around it and falls separately, so the set can collapse and
    // its rooms can be walked through. Keep each part a simple solid shape (no hollows or L-shapes).
    public sealed class UnderneathTreeToy : MonoBehaviour
    {
        [Tooltip("Heavier toys take a harder hit to knock over.")]
        [Range(.2f, 4f)] public float weight = 1f;

        // Solver passes for the parts of a set (Unity's default of 6 lets tall stacks sag).
        public const int StackSolverIterations = 16;

        // The prefab this toy was made from (set by the map, read by the toy placing tool).
        [HideInInspector] public UnderneathTreeToy source;

        // The separate parts of a toy set, or none for a single solid toy.
        public List<Transform> Pieces()
        {
            var pieces = new List<Transform>();
            foreach (var part in GetComponentsInChildren<Transform>())
                if (part != transform && part.name.StartsWith("Piece", System.StringComparison.Ordinal))
                    pieces.Add(part);
            return pieces;
        }

        // Makes sure the toy has collision and returns its size in its own space.
        public Bounds PrepareCollision()
        {
            var pieces = Pieces();
            if (pieces.Count > 0)
            {
                var all = new List<Collider>();
                foreach (var piece in pieces)
                {
                    var shape = piece.GetComponent<Collider>();
                    if (!shape && piece.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh)
                    {
                        // A wrapped shape that follows the part (a curved wall, a round floor, a step),
                        // so the rooms between parts stay open to walk through.
                        var hull = piece.gameObject.AddComponent<MeshCollider>();
                        hull.sharedMesh = filter.sharedMesh;
                        hull.convex = true;
                        shape = hull;
                    }
                    if (!shape)
                        continue;
                    piece.gameObject.layer = Layers.Block;
                    all.Add(shape);
                }
                return all.Count > 0 ? LocalBounds(all.ToArray()) : new Bounds(Vector3.up * .5f, Vector3.one);
            }
            var own = GetComponentsInChildren<Collider>();
            foreach (var shape in own)
                shape.gameObject.layer = Layers.Block;
            if (own.Length > 0)
                return LocalBounds(own);

            var fit = gameObject.AddComponent<BoxCollider>();
            var model = ModelBounds();
            fit.center = model.center;
            fit.size = Vector3.Max(model.size, Vector3.one * .2f);
            gameObject.layer = Layers.Block;
            return new Bounds(fit.center, fit.size);
        }

        // What one part of a set rests on, worked out by Settle.
        public sealed class PieceSupport
        {
            public Transform piece;
            public readonly List<Transform> under = new List<Transform>();
            // Where each part underneath touches it, seen from above in the toy's own space.
            public readonly List<Rect> areas = new List<Rect>();
            public Vector2 centre;
            public bool onGround;
            // Nothing under it close enough to rest on (a modelling mistake to fix).
            public bool floating;
        }

        // Stacks the parts of a set so each one rests exactly on what is under it.
        // A floor made thicker, thinner, bigger or smaller in the model would otherwise overlap
        // the walls (and burst apart when it comes loose) or float above them. This lifts or lowers
        // each part, from the bottom up, so everything above follows the change.
        public List<PieceSupport> Settle(List<Transform> pieces)
        {
            var boxes = new Dictionary<Transform, Bounds>();
            foreach (var piece in pieces)
                if (TryPieceBox(piece, out var box))
                    boxes[piece] = box;
            var order = new List<Transform>(boxes.Keys);
            order.Sort((a, b) => boxes[a].min.y.CompareTo(boxes[b].min.y));
            var result = new List<PieceSupport>();
            if (order.Count == 0)
                return result;
            float ground = boxes[order[0]].min.y;
            var original = new Dictionary<Transform, Bounds>(boxes);
            var outline = new Dictionary<Transform, Vector2[]>();
            foreach (var piece in order)
                outline[piece] = Footprint(piece);

            var placed = new List<Transform>();
            foreach (var piece in order)
            {
                var box = boxes[piece];
                var support = new PieceSupport { piece = piece, centre = new Vector2(box.center.x, box.center.z) };
                result.Add(support);
                float height = box.size.y;
                if (box.min.y <= ground + Mathf.Min(.05f, height * .1f))
                {
                    support.onGround = true;
                    placed.Add(piece);
                    continue;
                }

                // What it rests on is read from the model as built: parts underneath that it sinks
                // into by at most half its thickness (a floor made thicker) or hovers over by a small
                // gap (a floor made thinner). Parts beside it in the same storey never count.
                var start = original[piece];
                var holders = new List<Transform>();
                foreach (var lower in placed)
                {
                    var below = original[lower];
                    if (!Touches(outline[piece], outline[lower]) ||
                        below.max.y > start.min.y + Mathf.Max(.05f, Mathf.Min(height, below.size.y) * .75f) ||
                        start.min.y - below.max.y > Mathf.Max(.6f, height * .5f))
                        continue;
                    holders.Add(lower);
                }
                if (holders.Count == 0)
                {
                    support.floating = true;
                    placed.Add(piece);
                    continue;
                }

                // Those parts may have moved already (a thicker floor lower down lifts everything
                // above it), so it goes onto wherever the highest of them is now.
                float rest = float.MinValue;
                foreach (var lower in holders)
                    rest = Mathf.Max(rest, boxes[lower].max.y);
                // An upright part lands on the highest. A tilted part (a roof slope) has a box that
                // hangs below its real underside, so it just moves as far as what holds it moved.
                bool tilted = Tilted(piece);
                float lift = float.MinValue;
                foreach (var lower in holders)
                    lift = Mathf.Max(lift, boxes[lower].max.y - original[lower].max.y);
                {
                    float shift = tilted ? lift : rest + .01f - box.min.y;
                    piece.position += transform.TransformVector(Vector3.up * shift);
                    box.center += Vector3.up * shift;
                    boxes[piece] = box;
                }
                foreach (var lower in holders)
                {
                    if (!tilted && boxes[lower].max.y < rest - .05f)
                        continue;
                    support.under.Add(lower);
                    support.areas.Add(Overlap(box, boxes[lower]));
                }
                placed.Add(piece);
            }
            return result;
        }

        // Parts that still push into each other side by side (a floor made wider than the gap
        // between its neighbours, floor slices meeting in the middle of a round tower) would shove
        // each other apart when they come loose, even by a hair, so they are told to ignore each
        // other. Returns how many pairs that was.
        public int IgnoreOverlaps(List<Transform> pieces)
        {
            Physics.SyncTransforms();
            var shapes = new List<Collider>();
            foreach (var piece in pieces)
                if (piece.TryGetComponent<Collider>(out var shape))
                    shapes.Add(shape);
            int pairs = 0;
            for (int i = 0; i < shapes.Count; i++)
                for (int j = i + 1; j < shapes.Count; j++)
                {
                    var a = shapes[i];
                    var b = shapes[j];
                    if (!a.bounds.Intersects(b.bounds) || !Physics.ComputePenetration(
                            a, a.transform.position, a.transform.rotation,
                            b, b.transform.position, b.transform.rotation, out _, out float depth) || depth < .001f)
                        continue;
                    Physics.IgnoreCollision(a, b);
                    pairs++;
                }
            return pairs;
        }

        // True while the parts still holding a piece up surround its middle (seen from above).
        // A floor on four walls stays up when one wall goes, and drops when the middle loses its hold.
        public static bool StillHeld(Vector2 centre, List<Rect> areas)
        {
            if (areas.Count == 0)
                return false;
            var hold = areas[0];
            foreach (var area in areas)
                hold = Rect.MinMaxRect(Mathf.Min(hold.xMin, area.xMin), Mathf.Min(hold.yMin, area.yMin),
                    Mathf.Max(hold.xMax, area.xMax), Mathf.Max(hold.yMax, area.yMax));
            float give = Mathf.Max(hold.width, hold.height) * .05f + .05f;
            return centre.x > hold.xMin - give && centre.x < hold.xMax + give &&
                   centre.y > hold.yMin - give && centre.y < hold.yMax + give;
        }

        // The part's outline seen from above, in the toy's own space: the four corners of its own
        // box, so a part turned at an angle (a stair step, a tower wall) is not treated as wider.
        Vector2[] Footprint(Transform piece)
        {
            var box = piece.GetComponent<MeshFilter>().sharedMesh.bounds;
            var toToy = transform.worldToLocalMatrix * piece.localToWorldMatrix;
            // Use whichever two of the part's own axes lie flat, so a part on its side still works.
            var flat = FlatAxes(piece);
            var corners = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                var local = box.center + Vector3.Scale(box.extents,
                    (i == 1 || i == 2 ? 1 : -1) * flat.a + (i >= 2 ? 1 : -1) * flat.b);
                var point = toToy.MultiplyPoint3x4(local);
                corners[i] = new Vector2(point.x, point.z);
            }
            return corners;
        }

        // The two of the part's own axes (in its own space) that are closest to lying flat in the toy.
        (Vector3 a, Vector3 b) FlatAxes(Transform piece)
        {
            var up = piece.InverseTransformDirection(transform.up);
            var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
            int vertical = 0;
            for (int i = 1; i < 3; i++)
                if (Mathf.Abs(up[i]) > Mathf.Abs(up[vertical]))
                    vertical = i;
            return (axes[(vertical + 1) % 3], axes[(vertical + 2) % 3]);
        }

        // True when two outlines overlap by more than a hair (parts only touching side by side do not).
        static bool Touches(Vector2[] a, Vector2[] b)
        {
            const float hair = .05f;
            foreach (var shape in new[] { a, b })
                for (int i = 0; i < 4; i++)
                {
                    var edge = shape[(i + 1) % 4] - shape[i];
                    var normal = new Vector2(-edge.y, edge.x).normalized;
                    float aMin = float.MaxValue, aMax = float.MinValue, bMin = float.MaxValue, bMax = float.MinValue;
                    foreach (var point in a)
                    {
                        float d = Vector2.Dot(point, normal);
                        aMin = Mathf.Min(aMin, d);
                        aMax = Mathf.Max(aMax, d);
                    }
                    foreach (var point in b)
                    {
                        float d = Vector2.Dot(point, normal);
                        bMin = Mathf.Min(bMin, d);
                        bMax = Mathf.Max(bMax, d);
                    }
                    if (Mathf.Min(aMax, bMax) - Mathf.Max(aMin, bMin) <= hair)
                        return false;
                }
            return true;
        }

        // True when none of the part's own axes points straight up in the toy.
        bool Tilted(Transform piece)
        {
            var up = transform.up;
            foreach (var axis in new[] { piece.right, piece.up, piece.forward })
                if (Mathf.Abs(Vector3.Dot(axis, up)) > .999f)
                    return false;
            return true;
        }

        // Where two parts overlap seen from above (ignores faces that only touch side by side).
        static Rect Overlap(Bounds a, Bounds b)
        {
            const float touch = .05f;
            float x0 = Mathf.Max(a.min.x, b.min.x), x1 = Mathf.Min(a.max.x, b.max.x);
            float z0 = Mathf.Max(a.min.z, b.min.z), z1 = Mathf.Min(a.max.z, b.max.z);
            if (x1 - x0 <= touch || z1 - z0 <= touch)
                return Rect.zero;
            return Rect.MinMaxRect(x0, z0, x1, z1);
        }

        // A part's box in the toy's own space.
        public bool TryPieceBox(Transform piece, out Bounds bounds)
        {
            bounds = default;
            if (!piece.TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh)
                return false;
            var box = filter.sharedMesh.bounds;
            var toToy = transform.worldToLocalMatrix * piece.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = toToy.MultiplyPoint3x4(box.center + Vector3.Scale(box.extents,
                    new Vector3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1) * 2f - Vector3.one));
                if (corner == 0)
                    bounds = new Bounds(point, Vector3.zero);
                else
                    bounds.Encapsulate(point);
            }
            return true;
        }

        // The box around every mesh in the toy, in the toy's own space.
        Bounds ModelBounds()
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (var filter in GetComponentsInChildren<MeshFilter>())
            {
                if (!filter.sharedMesh)
                    continue;
                var box = filter.sharedMesh.bounds;
                var toToy = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = toToy.MultiplyPoint3x4(box.center + Vector3.Scale(box.extents,
                        new Vector3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1) * 2f - Vector3.one));
                    if (!any)
                        bounds = new Bounds(point, Vector3.zero);
                    else
                        bounds.Encapsulate(point);
                    any = true;
                }
            }
            // No model at all: a one-unit gray box still plays.
            return any ? bounds : new Bounds(Vector3.up * .5f, Vector3.one);
        }

        Bounds LocalBounds(Collider[] shapes)
        {
            var bounds = new Bounds(transform.InverseTransformPoint(shapes[0].bounds.center), Vector3.zero);
            foreach (var shape in shapes)
            {
                var box = shape.bounds;
                bounds.Encapsulate(transform.InverseTransformPoint(box.min));
                bounds.Encapsulate(transform.InverseTransformPoint(box.max));
            }
            return bounds;
        }
    }
}
