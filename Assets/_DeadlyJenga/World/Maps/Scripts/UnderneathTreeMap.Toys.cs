using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class UnderneathTreeMap
    {
        // Every machine loads the same layout, so hosts and joined players build the same toys.
        const string ToyLayoutName = "UnderneathTreeToys";

        // Toys behave like a gift piece: held still until hit hard enough, and they fall
        // when the gift they stand on breaks. Each one counts as a block, so it is synced too.
        void BuildToys()
        {
            var layout = Resources.Load<UnderneathTreeToyLayout>(ToyLayoutName);
            if (!layout)
                return;
            for (int i = 0; i < layout.toys.Length; i++)
            {
                var place = layout.toys[i];
                if (place.toy)
                    PlaceToy(place, i);
            }
        }

        void PlaceToy(UnderneathTreeToyLayout.Placement place, int index)
        {
            UnderneathTreeGiftPanel support = null;
            Vector3 standOn;
            Quaternion facing;
            if (!string.IsNullOrEmpty(place.onGift) && giftLids.TryGetValue(place.onGift, out var lid))
            {
                support = lid;
                var gift = lid.transform.parent;
                standOn = gift.TransformPoint(new Vector3(place.spot.x, 0f, place.spot.y));
                standOn.y = lid.GetComponent<Collider>().bounds.max.y;
                facing = gift.rotation * Quaternion.Euler(0f, place.turn, 0f);
            }
            else
            {
                // On the rug: the spot is measured from the centre of the gift course.
                var course = built.Find("Approved Gift Course");
                standOn = (course ? course : transform).TransformPoint(new Vector3(place.spot.x, 0f, place.spot.y));
                if (Physics.Raycast(standOn + Vector3.up * 200f, Vector3.down, out var ground, 400f,
                    1 << Layers.Surface, QueryTriggerInteraction.Ignore))
                    standOn.y = ground.point.y;
                facing = transform.rotation * Quaternion.Euler(0f, place.turn, 0f);
            }

            var toy = Instantiate(place.toy, built);
            // The map gives each toy its own body; one left on the prefab would get in the way.
            foreach (var extra in toy.GetComponentsInChildren<Rigidbody>())
                DestroyImmediate(extra);
            toy.name = place.toy.name + " " + (index + 1);
            toy.source = place.toy;
            toy.transform.rotation = facing;
            toy.transform.localScale = place.toy.transform.localScale * (place.size > 0f ? place.size : 1f);
            var size = toy.PrepareCollision();

            // Stand it on the surface with a hair of air, like the gifts.
            toy.transform.position = standOn;
            Physics.SyncTransforms();
            float bottom = float.MaxValue;
            foreach (var shape in toy.GetComponentsInChildren<Collider>())
                bottom = Mathf.Min(bottom, shape.bounds.min.y);
            toy.transform.position += Vector3.up * (standOn.y - bottom + .02f);

            var pieces = toy.Pieces();
            if (pieces.Count > 0)
                RegisterToySet(toy, pieces, support, bottom);
            else
            {
                var scaled = Vector3.Scale(size.size, toy.transform.lossyScale);
                var shell = new UnderneathTreeGiftShell();
                var visual = toy.transform.Find("Visual");
                RegisterPanel(toy.gameObject, visual ? visual : toy.transform, shell, GiftPieceRole.Base,
                    scaled.x * scaled.y * scaled.z * toy.weight);
                var panel = toy.GetComponent<UnderneathTreeGiftPanel>();
                if (support)
                    giftBearings.Add(new GiftBearing(panel, support, null));
            }
            foreach (var shape in toy.GetComponentsInChildren<Collider>())
                playArea.Encapsulate(shape.bounds);
            if (pieces.Count == 0)
                WarnIfClipping(toy, support);
        }

        // Every part of a set is its own body in one shell: a hard hit or losing a ground-floor part
        // brings the whole set down, and parts resting on others drop when those go.
        void RegisterToySet(UnderneathTreeToy toy, System.Collections.Generic.List<Transform> pieces,
            UnderneathTreeGiftPanel support, float oldBottom)
        {
            var shell = new UnderneathTreeGiftShell();
            float floor = float.MaxValue;
            foreach (var piece in pieces)
                if (piece.TryGetComponent<Collider>(out var shape))
                    floor = Mathf.Min(floor, shape.bounds.min.y);
            var registered = new System.Collections.Generic.List<UnderneathTreeGiftPanel>();
            foreach (var piece in pieces)
            {
                if (!piece.TryGetComponent<Collider>(out var shape))
                    continue;
                var box = shape.bounds;
                bool ground = box.min.y < floor + .3f;
                RegisterPanel(piece.gameObject, piece, shell, ground ? GiftPieceRole.Base : GiftPieceRole.Wall,
                    box.size.x * box.size.y * box.size.z * toy.weight * .5f);
                var panel = piece.GetComponent<UnderneathTreeGiftPanel>();
                registered.Add(panel);
                if (ground && support)
                    giftBearings.Add(new GiftBearing(panel, support, null));
            }
            // Parts sitting on other parts fall when what holds them up is gone.
            foreach (var upper in registered)
            {
                var top = upper.GetComponent<Collider>().bounds;
                if (top.min.y < floor + .3f)
                    continue;
                UnderneathTreeGiftPanel first = null, second = null;
                foreach (var lower in registered)
                {
                    if (lower == upper)
                        continue;
                    var under = lower.GetComponent<Collider>().bounds;
                    if (Mathf.Abs(under.max.y - top.min.y) > .35f || under.max.x < top.min.x || under.min.x > top.max.x ||
                        under.max.z < top.min.z || under.min.z > top.max.z)
                        continue;
                    if (!first) first = lower; else if (!second) second = lower;
                }
                if (first)
                    giftBearings.Add(new GiftBearing(upper, first, second));
            }
        }

        // A toy placed into a neighbouring gift would be shoved out at the first touch; say so.
        static void WarnIfClipping(UnderneathTreeToy toy, UnderneathTreeGiftPanel support)
        {
            Physics.SyncTransforms();
            foreach (var shape in toy.GetComponentsInChildren<Collider>())
            {
                var box = shape.bounds;
                box.Expand(-.1f);
                foreach (var other in Physics.OverlapBox(box.center, box.extents, Quaternion.identity,
                    1 << Layers.Block, QueryTriggerInteraction.Ignore))
                {
                    if (other.transform.IsChildOf(toy.transform))
                        continue;
                    Debug.LogWarning(toy.name + " is clipping into " + (other.attachedRigidbody ?
                        other.attachedRigidbody.transform.parent.name : other.name) + ". Move it in the toy layout.", toy);
                    return;
                }
            }
        }
    }
}
