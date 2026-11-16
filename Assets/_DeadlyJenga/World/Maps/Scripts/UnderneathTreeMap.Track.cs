using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class UnderneathTreeMap
    {
        const float RailBedOffset = .64f;
        const float RailBedThickness = .36f;

        void BuildApprovedTrack()
        {
            const int samples = 128, perSection = 8;
            const float height = 6.5f;
            var course = new Vector3[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i * Mathf.PI * 2f / samples;
                float u = 43f * Mathf.Cos(t) + 1.3f * Mathf.Sin(3f * t);
                float v = 29f * Mathf.Sin(t) + 1.2f * Mathf.Sin(2f * t);
                course[i] = CoursePoint(u, v, height);
            }
            var colors = new[] { new Color(.82f, .035f, .07f), new Color(.035f, .38f, .19f),
                new Color(.97f, .84f, .63f), new Color(.04f, .17f, .64f) };
            var wraps = new Material[4];
            for (int i = 0; i < wraps.Length; i++)
                wraps[i] = ColoredPaper("Rail " + i + " snowflake gift wrap", colors[i], .4f);
            var ribbon = ColoredPaper("Gold ribbon", new Color(.93f, .62f, .18f), .65f);
            var metal = ColoredPaper("Track Rail polished brass", new Color(.75f, .57f, .3f), .8f);
            var wood = ColoredPaper("Track Tie Dark Wood", new Color(.22f, .10f, .035f), .4f);
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var root = Child(built, "Train Loop", Vector3.zero, Quaternion.identity);
            var ballast = new UnderneathTreeGiftPanel[samples];
            for (int first = 0; first < samples; first += perSection)
            {
                int mid = first + perSection / 2;
                var anchor = course[mid];
                var direction = Quaternion.LookRotation(course[(mid + 1) % samples] - course[mid - 1]);
                float top = height - RailBedOffset - RailBedThickness * .5f;
                float bottom = GiftFloor(anchor);
                var bearing = BoxGift("Rail Gift " + (first / perSection + 1),
                    new Vector3(anchor.x, (bottom + top) * .5f, anchor.z),
                    new Vector3(8f, top - bottom, Vector3.Distance(course[first], course[(first + perSection) % samples]) + 1.7f),
                    direction, wraps[(first / perSection) % 4], ribbon);
                var section = Child(root, "Gift-supported rail " + (first / perSection + 1), anchor, Quaternion.identity);
                var rails = new List<CombineInstance>();
                var ties = new List<CombineInstance>();
                for (int i = first; i < first + perSection; i++)
                {
                    var a = course[i];
                    var b = course[(i + 1) % samples];
                    var rotation = Quaternion.LookRotation(b - a);
                    var center = (a + b) * .5f - anchor;
                    var deck = Child(section, "Rail deck", center - Vector3.up * RailBedOffset, rotation);
                    AddBox(deck, new Vector3(4.25f, RailBedThickness, Vector3.Distance(a, b) + .06f), true);
                    foreach (float sign in new[] { -1f, 1f })
                        rails.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(
                            center + rotation * Vector3.right * (sign * 1.32f), rotation,
                            new Vector3(.19f, .28f, Vector3.Distance(a, b) + .08f)) });
                    ties.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(
                        center - Vector3.up * .25f, rotation, new Vector3(3.7f, .20f, .43f)) });
                    ballast[i] = null;
                }
                var visual = Child(section, "Rail Visual", Vector3.zero, Quaternion.identity);
                JoinRailDetail(visual, "Track Rail", rails, metal);
                JoinRailDetail(visual, "Track Tie", ties, wood);
                RegisterPanel(section.gameObject, visual, new UnderneathTreeGiftShell(), GiftPieceRole.Rail, 48f);
                var panel = section.GetComponent<UnderneathTreeGiftPanel>();
                giftBearings.Add(new GiftBearing(panel, bearing, null));
                for (int i = first; i < first + perSection; i++) ballast[i] = panel;
            }
            if (locomotive)
            {
                var train = Instantiate(locomotive, root);
                train.name = "Tinplate Christmas Train";
                train.AddComponent<UnderneathTreeTrain>().SetUp(course, ballast);
            }
        }

        void BuildTrack(List<Vector3> course, Dictionary<string, MeshFilter> originals)
        {
            if (course.Count < 3)
                throw new InvalidOperationException("The Christmas rail loop is missing its paired rails.");

            const int segmentLength = 7;
            var decking = Child(built, "Train Loop", Vector3.zero, Quaternion.identity);
            var railWrap = ColoredPaper("Track Gift kraft gift wrap", new Color(.49f, .27f, .135f), .38f);
            var ribbon = ColoredPaper("Amber ribbon", new Color(.86f, .62f, .28f), .62f);
            var ballast = new UnderneathTreeGiftPanel[course.Count];
            for (int start = 0; start < course.Count; start += segmentLength)
            {
                int count = Mathf.Min(segmentLength, course.Count - start);
                var segment = Child(decking, "Gift-supported rail " + (start / segmentLength + 1),
                    course[start + count / 2], Quaternion.identity);
                segment.gameObject.layer = Layers.Block;

                var rails = new List<CombineInstance>();
                var ties = new List<CombineInstance>();
                Material railMaterial = null, tieMaterial = null;
                for (int i = start; i < start + count; i++)
                {
                    var a = course[i] - Vector3.up * RailBedOffset;
                    var b = course[(i + 1) % course.Count] - Vector3.up * RailBedOffset;
                    var step = b - a;
                    var plank = Child(segment, "Rail deck", (a + b) * .5f - segment.localPosition,
                        Quaternion.LookRotation(step));
                    AddBox(plank, new Vector3(4.25f, RailBedThickness, step.magnitude), true);
                    playArea.Encapsulate(transform.TransformPoint(a));

                    AddRailDetail(originals, "Track Rail L ", i, segment, rails, ref railMaterial);
                    AddRailDetail(originals, "Track Rail R ", i, segment, rails, ref railMaterial);
                    if (i % 2 == 0)
                        AddRailDetail(originals, "Track Tie ", i / 2, segment, ties, ref tieMaterial);
                }

                var visual = Child(segment, "Rail Visual", Vector3.zero, Quaternion.identity);
                JoinRailDetail(visual, "Track Rail", rails, railMaterial);
                JoinRailDetail(visual, "Track Tie", ties, tieMaterial);
                var bearing = FindRailBearing(course, start, count, railWrap, ribbon);
                RegisterPanel(segment.gameObject, visual, new UnderneathTreeGiftShell(), GiftPieceRole.Rail, count * 6f);
                var physical = segment.GetComponent<UnderneathTreeGiftPanel>();
                giftBearings.Add(new GiftBearing(physical, bearing, null));
                for (int i = start; i < start + count; i++)
                    ballast[i] = physical;
            }

            if (locomotive)
            {
                var train = Instantiate(locomotive, decking);
                train.name = "Tinplate Christmas Train";
                train.AddComponent<UnderneathTreeTrain>().SetUp(course.ToArray(), ballast);
            }
        }

        static void AddRailDetail(Dictionary<string, MeshFilter> originals, string prefix, int index,
            Transform segment, List<CombineInstance> pieces, ref Material material)
        {
            if (!originals.TryGetValue(prefix + index.ToString("000"), out var source))
                return;
            material = source.GetComponent<MeshRenderer>().sharedMaterial;
            pieces.Add(new CombineInstance {
                mesh = source.sharedMesh,
                transform = segment.worldToLocalMatrix * source.transform.localToWorldMatrix
            });
            source.gameObject.SetActive(false);
        }

        void JoinRailDetail(Transform visual, string label, List<CombineInstance> pieces, Material material)
        {
            if (pieces.Count == 0 || !material)
                return;
            var node = Child(visual, label, Vector3.zero, Quaternion.identity);
            var mesh = new Mesh { name = label + " section" };
            mesh.CombineMeshes(pieces.ToArray(), true, true);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            node.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            generatedMeshes.Add(mesh);
        }

        UnderneathTreeGiftPanel FindRailBearing(List<Vector3> course, int start, int count,
            Material wrap, Material ribbon)
        {
            const int blockMask = 1 << Layers.Block;
            float scale = transform.lossyScale.x;
            var anchor = course[start + count / 2];
            UnderneathTreeGiftPanel lower = null;
            float bestGap = float.PositiveInfinity;
            float bearingFloor = GiftFloor(anchor);
            for (int i = start; i < start + count; i++)
            {
                var sample = course[i] - Vector3.up * (RailBedOffset + RailBedThickness * .5f);
                const float tolerance = .15f;
                if (!Physics.Raycast(transform.TransformPoint(sample + Vector3.up * tolerance), -transform.up,
                        out var hit, (8f + tolerance) * scale, blockMask, QueryTriggerInteraction.Ignore))
                    continue;
                var lid = hit.collider.GetComponentInParent<UnderneathTreeGiftPanel>();
                if (!lid || lid.Role != GiftPieceRole.Lid || hit.normal.y < .80f)
                    continue;
                float gap = hit.distance / scale - tolerance;
                if (gap <= tolerance)
                    return lid;
                if (gap >= bestGap)
                    continue;
                bestGap = gap;
                lower = lid;
                anchor = course[i];
                bearingFloor = transform.InverseTransformPoint(hit.point).y;
            }

            float top = anchor.y - RailBedOffset - RailBedThickness * .5f;
            if (!lower)
            {
                // Use any deeper parcel below the center as a bearing instead of burying a tall gift in it.
                if (Physics.Raycast(transform.TransformPoint(new Vector3(anchor.x, top + .06f, anchor.z)),
                        -transform.up, out var hit, (top - bearingFloor + .06f) * scale,
                        blockMask, QueryTriggerInteraction.Ignore) &&
                    hit.collider.TryGetComponent<UnderneathTreeGiftPanel>(out var lid) &&
                    lid.Role == GiftPieceRole.Lid)
                {
                    lower = lid;
                    bearingFloor = transform.InverseTransformPoint(hit.point).y;
                }
            }

            float height = Mathf.Max(.72f, top - bearingFloor - .008f);
            var middle = new Vector3(anchor.x, top - height * .5f, anchor.z);
            var size = new Vector3(lower ? 2.12f : 3.85f, height, lower ? 2.45f : 4.5f);
            var bearing = BoxGift("Rail Gift " + (start / 7 + 1), middle, size,
                Quaternion.identity, wrap, ribbon, false);
            if (lower)
                giftBearings.Add(new GiftBearing(GiftBase(bearing), lower, null));
            Physics.SyncTransforms();
            return bearing;
        }
    }
}
