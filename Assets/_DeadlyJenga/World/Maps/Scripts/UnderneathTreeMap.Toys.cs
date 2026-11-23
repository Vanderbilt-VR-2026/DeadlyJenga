using System.Collections.Generic;
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

        readonly List<ToyHold> toyHolds = new List<ToyHold>();

        // A part of a set and the parts under it. It drops once what is left no longer holds its middle.
        sealed class ToyHold
        {
            public UnderneathTreeGiftPanel load;
            public Vector2 centre;
            public readonly List<UnderneathTreeGiftPanel> under = new List<UnderneathTreeGiftPanel>();
            public readonly List<Rect> areas = new List<Rect>();
            readonly List<Rect> left = new List<Rect>();

            public void Check()
            {
                if (!load || load.IsLoose)
                    return;
                left.Clear();
                for (int i = 0; i < under.Count; i++)
                    if (under[i] && under[i].gameObject.activeInHierarchy && !under[i].IsLoose)
                        left.Add(areas[i]);
                if (left.Count < areas.Count && !UnderneathTreeToy.StillHeld(centre, left))
                    load.Push(Vector3.down * 1.5f, null);
            }
        }

        // Every part of a set is its own body in one shell: a hard hit or losing a ground-floor part
        // brings the whole set down, and parts resting on others drop when those go.
        // The parts are stacked first (Settle), so floors can change size or thickness in the model
        // without the set bursting apart or floating.
        void RegisterToySet(UnderneathTreeToy toy, List<Transform> pieces,
            UnderneathTreeGiftPanel support, float oldBottom)
        {
            var settled = toy.Settle(pieces);
            toy.IgnoreOverlaps(pieces);
            var shell = new UnderneathTreeGiftShell();
            var panelOf = new Dictionary<Transform, UnderneathTreeGiftPanel>();
            var registered = new List<Rigidbody>();
            foreach (var part in settled)
            {
                if (!part.piece.TryGetComponent<Collider>(out var shape))
                    continue;
                var box = shape.bounds;
                RegisterPanel(part.piece.gameObject, part.piece, shell,
                    part.onGround ? GiftPieceRole.Base : GiftPieceRole.Wall,
                    box.size.x * box.size.y * box.size.z * toy.weight * .5f);
                var panel = part.piece.GetComponent<UnderneathTreeGiftPanel>();
                panelOf[part.piece] = panel;
                registered.Add(panel.Body);
                // Tall stacks need more solver passes than one gift, or the storeys sink into each other.
                panel.Body.solverIterations = UnderneathTreeToy.StackSolverIterations;
                panel.Body.solverVelocityIterations = 4;
                // Thin parts (slabs, landings) check ahead for contacts so they never sink through.
                if (Mathf.Min(box.size.x, Mathf.Min(box.size.y, box.size.z)) < .6f)
                    panel.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                if (part.onGround && support)
                    giftBearings.Add(new GiftBearing(panel, support, null));
                if (part.floating)
                    Debug.LogWarning(part.piece.name + " in " + toy.name + " has nothing under it to rest on.", part.piece);
            }
            if (!string.IsNullOrEmpty(toy.captureSection))
            {
                var compound = Child(toy.transform, "Intact building body", Vector3.zero, Quaternion.identity);
                RegisterPanel(compound.gameObject, compound, new UnderneathTreeGiftShell(), GiftPieceRole.Base, 65f);
                var panel = compound.GetComponent<UnderneathTreeGiftPanel>();
                float mass = 0f;
                foreach (var body in registered) mass += body.mass;
                panel.Body.mass = mass;
                panel.Body.solverIterations = UnderneathTreeToy.StackSolverIterations;
                if (support) giftBearings.Add(new GiftBearing(panel, support, null));
                compound.gameObject.SetActive(false);
                var building = toy.gameObject.AddComponent<UnderneathTreeBuilding>();
                building.Configure(this, panel, skirt, registered, toy.captureSection, toy.captureHull);
                if (startBuildingsTogether) building.SetTogether(true);
            }
            // Big mass gaps between touching parts make physics jittery: keep every part within 10x.
            float heaviest = 0f;
            foreach (var body in registered)
                heaviest = Mathf.Max(heaviest, body.mass);
            foreach (var body in registered)
                body.mass = Mathf.Max(body.mass, heaviest * .1f);

            foreach (var part in settled)
            {
                if (part.under.Count == 0 || !panelOf.TryGetValue(part.piece, out var load))
                    continue;
                var hold = new ToyHold { load = load, centre = part.centre };
                for (int i = 0; i < part.under.Count; i++)
                    if (panelOf.TryGetValue(part.under[i], out var below))
                    {
                        hold.under.Add(below);
                        hold.areas.Add(part.areas[i]);
                    }
                if (hold.under.Count > 0)
                    toyHolds.Add(hold);
            }
        }

        void CheckToyHolds()
        {
            foreach (var hold in toyHolds)
                hold.Check();
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
