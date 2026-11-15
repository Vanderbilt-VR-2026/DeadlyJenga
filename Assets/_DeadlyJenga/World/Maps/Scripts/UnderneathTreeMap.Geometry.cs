using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class UnderneathTreeMap
    {
        void BuildJunction()
        {
            var orientation = Quaternion.LookRotation(new Vector3(-.270f, 0f, .963f));
            var cream = ColoredPaper("Cream gift wrap", new Color(.94f, .76f, .49f), .32f);
            var emerald = ColoredPaper("Evergreen gift wrap", new Color(.045f, .32f, .15f), .36f);
            var cranberry = ColoredPaper("Cranberry ribbon", new Color(.65f, .10f, .15f), .55f);
            var gold = ColoredPaper("Satin gold ribbon", new Color(.88f, .55f, .16f), .55f);
            FixedGift("Cream Landing", new Vector3(4.896f, 2.330f, -20.026f),
                new Vector3(3.4f, 3.48f, 3.5f), orientation, cream, cranberry);
            FixedGift("Green Climb", new Vector3(7.828f, 3.530f, -18.935f),
                new Vector3(2.8f, 5.88f, 3.05f), orientation, emerald, gold);
        }

        Material ColoredPaper(string label, Color color, float gloss)
        {
            var material = new Material(paper) { name = label, color = color };
            material.SetFloat("_Smoothness", gloss);
            wrapping.Add(material);
            return material;
        }

        void FixedGift(string label, Vector3 center, Vector3 size, Quaternion orientation, Material wrap, Material ribbon)
        {
            var root = Child(built, label, center, orientation);
            AddBox(root, size, false);
            var art = VisualBox(root, "Visual", Vector3.zero, size, wrap);
            AddRibbon(root, size, ribbon);
            AddBody(root.gameObject);
            root.gameObject.AddComponent<PowerupLabSurface>();
            playArea.Encapsulate(art.GetComponent<MeshRenderer>().bounds);
        }

        void BuildPaperPanels(MeshFilter source, Material wrap, Material ribbon)
        {
            var mesh = source.sharedMesh.bounds;
            var original = source.transform;
            var frame = Child(built, source.name.Replace(" box", ""),
                transform.InverseTransformPoint(original.TransformPoint(mesh.center)),
                Quaternion.Inverse(transform.rotation) * original.rotation);
            Vector3 size = Vector3.Scale(mesh.size, original.lossyScale) / transform.lossyScale.x;
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));

            // The authored boxes extend slightly below the felt. Trim the bottom without moving the top.
            float bottom = frame.localPosition.y - size.y * .5f;
            float trim = Mathf.Clamp(.56f - bottom, 0f, size.y * .25f);
            frame.localPosition += Vector3.up * (trim * .5f);
            size.y -= trim;
            const float joint = .018f;
            float thick = Mathf.Clamp(Mathf.Min(size.x, size.y, size.z) * .085f, .2f, .55f);
            float insideY = size.y - 2f * (thick + joint);
            float insideX = size.x - 2f * (thick + joint);

            AddPanel(frame, "Bottom", Vector3.down * ((size.y - thick) * .5f),
                new Vector3(size.x, thick, size.z), wrap, null, Vector3.zero);
            AddPanel(frame, "Lid", Vector3.up * ((size.y - thick) * .5f),
                new Vector3(size.x, thick, size.z), wrap, ribbon, Vector3.up);
            foreach (float sign in new[] { -1f, 1f })
            {
                AddPanel(frame, sign < 0 ? "Left" : "Right", Vector3.right * (sign * (size.x - thick) * .5f),
                    new Vector3(thick, insideY, size.z), wrap, ribbon, Vector3.right * sign);
                AddPanel(frame, sign < 0 ? "Front" : "Back", Vector3.forward * (sign * (size.z - thick) * .5f),
                    new Vector3(insideX, insideY, thick), wrap, ribbon, Vector3.forward * sign);
            }
        }

        void AddPanel(Transform gift, string label, Vector3 offset, Vector3 size, Material wrap, Material ribbon, Vector3 facing)
        {
            var piece = Child(gift, label, offset, Quaternion.identity);
            AddBox(piece, size, true);
            var visual = VisualBox(piece, "Visual", Vector3.zero, size, wrap);
            if (ribbon)
            {
                float width = Mathf.Min(size.x, size.z) * .18f;
                if (facing.y != 0f)
                {
                    VisualBox(piece, "Ribbon across", Vector3.up * (size.y * .5f + .007f),
                        new Vector3(size.x, .014f, width), ribbon);
                    VisualBox(piece, "Ribbon lengthwise", Vector3.up * (size.y * .5f + .014f),
                        new Vector3(width, .014f, size.z), ribbon);
                }
                else if (facing.x != 0f)
                    VisualBox(piece, "Ribbon", facing * (size.x * .5f + .007f),
                        new Vector3(.014f, size.y, size.z * .18f), ribbon);
                else
                    VisualBox(piece, "Ribbon", facing * (size.z * .5f + .007f),
                        new Vector3(size.x * .18f, size.y, .014f), ribbon);
            }
            RegisterPanel(piece.gameObject, visual.transform, size.x * size.y * size.z);
        }

        void BuildRoundLid(MeshFilter filter)
        {
            var go = filter.gameObject;
            go.layer = DeadlyJenga.Core.Layers.Block;
            var shape = go.AddComponent<MeshCollider>();
            shape.sharedMesh = filter.sharedMesh;
            shape.convex = true;
            RegisterPanel(go, go.transform, 10f);
        }

        void RegisterPanel(GameObject go, Transform visual, float volume)
        {
            var body = AddBody(go);
            body.mass = Mathf.Clamp(volume * 1.9f, 4f, 65f);
            body.linearDamping = .12f;
            body.angularDamping = .18f;
            var panel = go.AddComponent<UnderneathTreeGiftPanel>();
            panel.SetUp(body, visual);
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

        static void AddBox(Transform root, Vector3 size, bool movable)
        {
            root.gameObject.layer = movable ? DeadlyJenga.Core.Layers.Block : DeadlyJenga.Core.Layers.Surface;
            root.gameObject.AddComponent<BoxCollider>().size = size;
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

        static void AddRibbon(Transform parent, Vector3 size, Material ribbon)
        {
            float y = size.y * .5f + .004f;
            VisualBox(parent, "Top ribbon", new Vector3(-size.x * .29f, y, 0),
                new Vector3(.27f, .012f, size.z), ribbon);
            VisualBox(parent, "Side ribbon", new Vector3(-size.x * .29f, 0, -size.z * .5f - .004f),
                new Vector3(.27f, size.y, .012f), ribbon);
        }

        void BuildTrack(List<Vector3> course)
        {
            if (course.Count < 3)
                throw new InvalidOperationException("The Christmas rail loop is missing its paired rails.");
            var combined = new List<CombineInstance>();
            var floor = ColoredPaper("Dark wood track bed", new Color(.16f, .077f, .036f), .27f);
            var decking = Child(built, "Train Loop", Vector3.zero, Quaternion.identity);
            Mesh cubeMesh = null;
            for (int i = 0; i < course.Count; i++)
            {
                var start = course[i] - Vector3.up * .64f;
                var end = course[(i + 1) % course.Count] - Vector3.up * .64f;
                var delta = end - start;
                var center = (start + end) * .5f;
                var rotation = Quaternion.LookRotation(delta);
                var size = new Vector3(4.25f, .36f, delta.magnitude + .23f);
                var plank = Child(decking, "Rail bed " + i, center, rotation);
                AddBox(plank, size, false);
                if (!cubeMesh)
                {
                    var sample = VisualBox(plank, "Bed Cube", Vector3.zero, Vector3.one, floor);
                    cubeMesh = sample.GetComponent<MeshFilter>().sharedMesh;
                    Release(sample);
                }
                combined.Add(new CombineInstance { mesh = cubeMesh, transform = Matrix4x4.TRS(center, rotation, size) });
                playArea.Encapsulate(transform.TransformPoint(start));
            }
            var mesh = new Mesh { name = "Joined rail bed" };
            mesh.CombineMeshes(combined.ToArray(), true, true);
            generatedMeshes.Add(mesh);
            AddBody(decking.gameObject);
            decking.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            decking.gameObject.AddComponent<MeshRenderer>().sharedMaterial = floor;

            if (locomotive)
            {
                var train = Instantiate(locomotive, decking);
                train.name = "Tinplate Christmas Train";
                train.AddComponent<UnderneathTreeTrain>().SetUp(course.ToArray());
            }
        }

        void BuildApproach(List<Vector3> course)
        {
            var wrap = ColoredPaper("Mulberry walkway", new Color(.28f, .045f, .17f), .33f);
            var ribbon = ColoredPaper("Warm ivory edge", new Color(.82f, .66f, .40f), .40f);
            var supports = Child(built, "Present Walk to the Train Loop", Vector3.zero, Quaternion.identity);
            var landing = new Vector3(3.8f, 12.03f, -54.2f);
            BuildWalkway(supports, "Ribbon approach", new Vector3(3.8f, 5.01f, -32.7f),
                landing, 2.6f, wrap, ribbon);

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
            AddBox(walkway, dimensions, false);
            AddBody(walkway.gameObject);
            VisualBox(walkway, "Deck", Vector3.zero, dimensions, wrap);
            foreach (float sign in new[] { -1f, 1f })
                VisualBox(walkway, "Edge stitching",
                    new Vector3(sign * (width * .5f - .08f), .194f, 0f),
                    new Vector3(.07f, .012f, dimensions.z), ribbon);
            playArea.Encapsulate(transform.TransformPoint(end));
        }
    }
}
