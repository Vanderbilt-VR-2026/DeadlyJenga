using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class UnderneathTreeMap
    {
        void ResizeApprovedRug(MeshFilter filter)
        {
            var mesh = Instantiate(filter.sharedMesh);
            mesh.name = "Approved room rug footprint";
            var points = mesh.vertices;
            for (int i = 0; i < points.Length; i++)
            {
                var point = transform.InverseTransformPoint(filter.transform.TransformPoint(points[i]));
                point.x = -75f + (point.x + 35f) * 1.5f;
                point.z = -95f + (point.z + 35f) * 1.5f;
                points[i] = filter.transform.InverseTransformPoint(transform.TransformPoint(point));
            }
            mesh.vertices = points;
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
            generatedMeshes.Add(mesh);
        }

        void ScaleApprovedCourse(Transform imported)
        {
            var course = Child(built, "Approved Gift Course", new Vector3(-62f, 0f, -80f), Quaternion.identity);
            var frames = new List<Transform>();
            foreach (Transform node in built)
                if (node != course && node != imported && !node.name.EndsWith("(Clone)") && node.name != "Fir trunk collision")
                    frames.Add(node);
            foreach (var frame in frames) frame.SetParent(course, true);
            course.localPosition = new Vector3(-90f, -.486f, -110f);
            course.localScale = Vector3.one * 1.9f;
            Physics.SyncTransforms();
            foreach (var body in bodies)
                foreach (var shape in body.GetComponentsInChildren<Collider>())
                    playArea.Encapsulate(shape.bounds);
        }

        void BuildJunction()
        {
            var orientation = Quaternion.LookRotation(new Vector3(-.270f, 0f, .963f));
            var cream = ColoredPaper("Cream gift wrap", new Color(.94f, .76f, .49f), .32f);
            var emerald = ColoredPaper("Evergreen gift wrap", new Color(.045f, .32f, .15f), .36f);
            var cranberry = ColoredPaper("Cranberry ribbon", new Color(.65f, .10f, .15f), .55f);
            var gold = ColoredPaper("Satin gold ribbon", new Color(.88f, .55f, .16f), .55f);
            BoxGift("Cream Landing", new Vector3(4.896f, 2.330f, -20.026f),
                new Vector3(3.4f, 3.48f, 3.5f), orientation, cream, cranberry);
            BoxGift("Green Climb", new Vector3(7.828f, 3.530f, -18.935f),
                new Vector3(2.8f, 5.88f, 3.05f), orientation, emerald, gold);
        }

        Material ColoredPaper(string label, Color color, float gloss)
        {
            var material = new Material(paper) { name = label, color = color };
            material.SetFloat("_Smoothness", gloss);
            wrapping.Add(material);
            return material;
        }

        void BuildPaperPanels(MeshFilter source, Material wrap, Material ribbon)
        {
            var mesh = source.sharedMesh.bounds;
            var original = source.transform;
            Vector3 center = transform.InverseTransformPoint(original.TransformPoint(mesh.center));
            var rotation = Quaternion.Inverse(transform.rotation) * original.rotation;
            BoxGift(source.name.Replace(" box", ""), center, ModelSize(source), rotation, wrap, ribbon);
        }

        Vector3 ModelSize(MeshFilter source)
        {
            var size = Vector3.Scale(source.sharedMesh.bounds.size, source.transform.lossyScale) / transform.lossyScale.x;
            return new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        }

        static float GiftFloor(Vector3 center)
        {
            Vector2 fromTree = new Vector2(center.x + 62f, center.z + 75f);
            return fromTree.sqrMagnitude < 77f * 77f ? .54f : .02f;
        }

        UnderneathTreeGiftPanel BoxGift(string label, Vector3 center, Vector3 size, Quaternion orientation,
            Material wrap, Material ribbon, bool spawn = true)
        {
            float bottom = center.y - size.y * .5f;
            float trim = Mathf.Clamp(GiftFloor(center) - bottom, 0f, size.y * .25f);
            center.y += trim * .5f;
            size.y -= trim;
            var frame = Child(built, label, center, orientation);
            var shell = new UnderneathTreeGiftShell();
            const float joint = .016f;
            float thick = Mathf.Clamp(Mathf.Min(size.x, size.y, size.z) * .085f, .2f, .57f);
            float insideY = size.y - 2f * (thick + joint);
            float insideX = size.x - 2f * (thick + joint);

            AddPanel(frame, shell, "Bottom", GiftPieceRole.Base, Vector3.down * ((size.y - thick) * .5f),
                new Vector3(size.x, thick, size.z), wrap, null, Vector3.zero);
            var top = AddPanel(frame, shell, "Lid", GiftPieceRole.Lid, Vector3.up * ((size.y - thick) * .5f),
                new Vector3(size.x, thick, size.z), wrap, ribbon, Vector3.up);
            if (label == "Present 206" || label == "Entrance / Cream Catch")
                spawnTops.Insert(0, top);
            else if (spawn)
                spawnTops.Add(top);

            foreach (float sign in new[] { -1f, 1f })
            {
                AddPanel(frame, shell, sign < 0 ? "Left" : "Right", GiftPieceRole.Wall,
                    Vector3.right * (sign * (size.x - thick) * .5f),
                    new Vector3(thick, insideY, size.z), wrap, ribbon, Vector3.right * sign);
                AddPanel(frame, shell, sign < 0 ? "Front" : "Back", GiftPieceRole.Wall,
                    Vector3.forward * (sign * (size.z - thick) * .5f),
                    new Vector3(insideX, insideY, thick), wrap, ribbon, Vector3.forward * sign);
            }
            playArea.Encapsulate(top.bounds);
            return top.GetComponent<UnderneathTreeGiftPanel>();
        }

        Collider AddPanel(Transform gift, UnderneathTreeGiftShell shell, string label, GiftPieceRole role,
            Vector3 offset, Vector3 size, Material wrap, Material ribbon, Vector3 facing)
        {
            var piece = Child(gift, label, offset, Quaternion.identity);
            var collider = AddBox(piece, size, true);
            var visual = VisualBox(piece, "Visual", Vector3.zero, size, wrap);
            if (ribbon)
            {
                float width = Mathf.Min(size.x, size.z) * .135f;
                if (facing.y != 0f)
                {
                    VisualBox(piece, "Ribbon across", new Vector3(0f, size.y * .5f + .007f, -.23f * size.z),
                        new Vector3(size.x, .014f, width), ribbon);
                    VisualBox(piece, "Ribbon lengthwise", new Vector3(.26f * size.x, size.y * .5f + .014f, 0f),
                        new Vector3(width, .014f, size.z), ribbon);
                }
                else if (facing.x != 0f)
                    VisualBox(piece, "Ribbon", facing * (size.x * .5f + .007f) - Vector3.forward * (size.z * .23f),
                        new Vector3(.014f, size.y, size.z * .18f), ribbon);
                else
                    VisualBox(piece, "Ribbon", facing * (size.z * .5f + .007f) + Vector3.right * (size.x * .33f),
                        new Vector3(size.x * .18f, size.y, .014f), ribbon);
            }
            RegisterPanel(piece.gameObject, visual.transform, shell, role, size.x * size.y * size.z);
            return collider;
        }

        void RegisterPanel(GameObject go, Transform visual, UnderneathTreeGiftShell shell,
            GiftPieceRole role, float volume)
        {
            var body = AddBody(go);
            body.mass = Mathf.Clamp(volume * 1.9f, 4f, 65f);
            body.linearDamping = .12f;
            body.angularDamping = .18f;
            var panel = go.AddComponent<UnderneathTreeGiftPanel>();
            panel.SetUp(body, visual, shell, role);
            panel.SetReplica(replica);
            panels.Add(panel);
            bodies.Add(body);
        }

        static Transform Child(Transform parent, string label, Vector3 position, Quaternion orientation)
        {
            var child = new GameObject(label).transform;
            child.SetParent(parent, false);
            child.SetLocalPositionAndRotation(position, orientation);
            return child;
        }

        static BoxCollider AddBox(Transform root, Vector3 size, bool movable)
        {
            root.gameObject.layer = movable ? DeadlyJenga.Core.Layers.Block : DeadlyJenga.Core.Layers.Surface;
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.size = size;
            return collider;
        }

        static GameObject VisualBox(Transform parent, string label, Vector3 offset, Vector3 dimensions, Material material)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var incidental = visual.GetComponent<BoxCollider>();
            incidental.enabled = false;
            Release(incidental);
            visual.name = label;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = offset;
            visual.transform.localScale = dimensions;
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
            return visual;
        }

        void BuildApproach(List<Vector3> course)
        {
            var wrap = ColoredPaper("Mulberry walkway", new Color(.28f, .045f, .17f), .33f);
            var ribbon = ColoredPaper("Warm ivory edge", new Color(.82f, .66f, .40f), .40f);
            var supports = Child(built, "Present Walk to the Train Loop", Vector3.zero, Quaternion.identity);
            var crest = new Vector3(3.8f, 12.08f, -47.2f);
            BuildWalkway(supports, "Ribbon approach", new Vector3(3.8f, 5.01f, -32.7f),
                crest, 2.6f, wrap, ribbon);
            BuildWalkway(supports, "Ribbon landing", crest, new Vector3(3.8f, 12.08f, -54.2f),
                2.6f, wrap, ribbon);

            var west = new Vector3(-2.7f, 12.03f, -56.0f);
            var target = course.OrderBy(p => (new Vector2(p.x - west.x, p.z - west.z)).sqrMagnitude).First();
            BuildWalkway(supports, "Ribbon bridge to the loop", west,
                target - Vector3.up * .46f, 2.8f, wrap, ribbon);
        }

        void BuildWalkway(Transform parent, string label, Vector3 start, Vector3 end,
                          float width, Material wrap, Material ribbon)
        {
            Vector3 delta = end - start;
            Quaternion direction = Quaternion.LookRotation(delta);
            Vector3 center = (start + end) * .5f - direction * Vector3.up * .19f;
            var walkway = Child(parent, label, center, direction);
            var dimensions = new Vector3(width, .38f, delta.magnitude + .22f);
            AddBox(walkway, dimensions, true);
            var visual = Child(walkway, "Visual", Vector3.zero, Quaternion.identity);
            VisualBox(visual, "Deck", Vector3.zero, dimensions, wrap);
            foreach (float sign in new[] { -1f, 1f })
                VisualBox(visual, "Edge stitching",
                    new Vector3(sign * (width * .5f - .08f), .194f, 0f),
                    new Vector3(.07f, .012f, dimensions.z), ribbon);
            RegisterPanel(walkway.gameObject, visual, new UnderneathTreeGiftShell(),
                GiftPieceRole.Walkway, dimensions.x * dimensions.y * dimensions.z);
            playArea.Encapsulate(transform.TransformPoint(end));
        }
    }
}
