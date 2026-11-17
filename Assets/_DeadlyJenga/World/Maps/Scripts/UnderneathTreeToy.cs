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
