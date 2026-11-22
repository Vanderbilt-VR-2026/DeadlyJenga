using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    internal sealed class ShrineTownLayout
    {
        public const string Preset = "shrine-town";
        readonly List<CastlePlacement> pieces = new List<CastlePlacement>(360);

        public static List<CastlePlacement> Build()
        {
            var town = new ShrineTownLayout();
            // Street runs north from the gate to the five-bay worship hall.
            town.Box(0, 0, 20, 41, 4, 22, CastleSection.Keep);
            for (int i = 0; i < 8; i++)
                town.Box(0, 0, 1.5f + i, 11, (i + 1) * .5f, 1, CastleSection.Keep);
            town.Hall(0, 20, 36, 17, 4, 8, 2, 6);
            town.Hall(-32, 27, 10, 10, 0, 5, 3, 2, true);
            town.Hall(34, 16, 10, 10, 0, 3.1f, 4, 2);
            town.Hall(-30, -2, 19, 13, 0, 5.25f, 2, 3);
            town.Hall(-10, -2, 9, 9, 0, 5.25f, 2, 2);
            // Bridge ends bear on both first-storey decks.
            town.Box(-18.5f, 6.85f, -2, 8, .5f, 1.6f, CastleSection.Keep);
            for (int side = -1; side <= 1; side += 2)
            {
                town.Hall(side * 10, -29, 9, 8, 0, 4.4f, 1, 2);
                town.Hall(side * 11, -15, 10, 8, 0, 3, 2, 2, yaw: side * 20);
                town.Hall(side * 30, -33, 12, 9, 0, 4.8f, 1, 3, yaw: side * 42);
                town.Hall(side * 32, -17, 9, 8, 0, 4.6f, 1, 2, yaw: side * 65);
            }
            town.Hall(29, 0, 14, 10, 0, 4, 2, 3);
            town.Hall(10, -2, 8, 8, 0, 3, 1, 2);
            town.Hall(-30, 12, 8, 6, 0, 3.6f, 1, 2);
            town.Hall(17, -40, 8, 6, 0, 3.8f, 1, 2);
            town.Hall(-19, -38, 7, 6, 0, 3.4f, 1, 2);
            town.Hall(42, 1, 7, 7, 0, 3.6f, 1, 2);
            return town.pieces;
        }

        static Mesh foundationMesh;

        public static void ShapeFoundation(Transform foundation)
        {
            if (foundation.TryGetComponent<MeshCollider>(out _)) return;
            if (!foundationMesh) foundationMesh = BuildFoundationMesh();
            foundation.gameObject.AddComponent<MeshCollider>().sharedMesh = foundationMesh;
            foundation.GetComponent<BoxCollider>().enabled = false;
        }

        static Mesh BuildFoundationMesh()
        {
            // Matches the eight quay faces. Share the fixed foundation across rebuilds.
            const float h = .5f, cut = .25f;
            Vector2[] rim = {new(-cut,-h),new(-h,-cut),new(-h,cut),new(-cut,h),
                new(cut,h),new(h,cut),new(h,-cut),new(cut,-h)};
            var vertices = new Vector3[16];
            var triangles = new List<int>(84);
            for (int i = 0; i < 8; i++)
            {
                vertices[i] = new Vector3(rim[i].x, h, rim[i].y);
                vertices[i + 8] = vertices[i] + Vector3.down;
                int j = (i + 1) % 8;
                triangles.AddRange(new[] {i, i + 8, j, j, i + 8, j + 8});
                if (i > 0 && i < 7) triangles.AddRange(new[] {0, i, j, 8, j + 8, i + 8});
            }
            return new Mesh { name = "Octagonal shrine quay", vertices = vertices, triangles = triangles.ToArray() };
        }

        public static void JoinTimberFrames(List<CastlePlacement> layout, List<Rigidbody> bodies)
        {
            // Each lintel follows the uprights in its row. Peg only the two outside posts.
            // The frame still bears freely on its floor and the roof remains a loose body.
            for (int i = 0; i < layout.Count; i++)
            {
                var beam = layout[i];
                if (beam.Section != CastleSection.Wall || Mathf.Abs(beam.Size.y - .6f) > .01f) continue;
                int last = i - 1, first = last;
                while (first > 0 && layout[first-1].Section == CastleSection.Wall && layout[first-1].Size.x == 1) first--;
                Join(bodies[i], bodies[first]);
                if (last != first) Join(bodies[i], bodies[last]);
            }
        }

        static void Join(Rigidbody beam, Rigidbody post)
        {
            var joint = beam.gameObject.AddComponent<FixedJoint>();
            joint.anchor = beam.transform.InverseTransformPoint(post.transform.TransformPoint(Vector3.up * .5f));
            joint.connectedBody = post;
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = Vector3.up * .5f;
            joint.breakForce = (beam.mass + post.mass) * 9.81f * 8;
            joint.breakTorque = joint.breakForce;
            joint.enablePreprocessing = false;
        }

        // Each pitched roof is one moving body. Both slopes travel with the deck beneath them.
        public static void AddRoofColliders(Rigidbody body, Vector3 size)
        {
            bool skirt = Mathf.Abs(size.y - 1) < .01f && size.x >= 30 && size.z > 4;
            if (!skirt && Mathf.Abs(size.y - 1.25f) > .01f && Mathf.Abs(size.y - 1.35f) > .01f && Mathf.Abs(size.y - 1.4f) > .01f) return;
            var frame = new GameObject("Roof collision").transform;
            frame.SetParent(body.transform, false);
            frame.localScale = new Vector3(1 / size.x, 1 / size.y, 1 / size.z);
            if (skirt)
            {
                for (int edge=0; edge<4; edge++)
                {
                    var turn = Quaternion.Euler(0,edge*90,0);
                    float across = edge%2==0 ? size.x : size.z;
                    float depth = (edge%2==0 ? size.z : size.x)/2;
                    float width = depth * .19f;
                    var eave = new GameObject("Gallery eave");
                    eave.layer = body.gameObject.layer;
                    eave.transform.SetParent(frame,false);
                    eave.transform.localPosition = turn * new Vector3(0,.55f+width*.36f,depth-width/2);
                    eave.transform.localRotation = turn * Quaternion.Euler(Mathf.Atan(.72f)*Mathf.Rad2Deg,0,0);
                    eave.AddComponent<BoxCollider>().size = new Vector3(across*.81f,.1f,width*Mathf.Sqrt(1+.72f*.72f));
                }
                return;
            }
            float run = size.z / 2, rise = run * .44f;
            for (int side = -1; side <= 1; side += 2)
            {
                var slope = new GameObject("Roof slope");
                slope.layer = body.gameObject.layer;
                slope.transform.SetParent(frame, false);
                slope.transform.localPosition = new Vector3(0, (size.y + rise) / 2, side * run / 2);
                slope.transform.localRotation = Quaternion.Euler(side * Mathf.Atan(.44f) * Mathf.Rad2Deg, 0, 0);
                slope.AddComponent<BoxCollider>().size = new Vector3(size.x, .22f, Mathf.Sqrt(run * run + rise * rise));
                if (Mathf.Abs(size.y - 1.25f) < .01f && size.x >= 15)
                {
                    float span = size.x * .38f, peak = rise + span * .32f;
                    var gable = new GameObject("Front gable slope");
                    gable.layer = body.gameObject.layer;
                    gable.transform.SetParent(frame, false);
                    gable.transform.localPosition = new Vector3(side * span / 2,(size.y + peak) / 2,-(run + .75f) / 2);
                    gable.transform.localRotation = Quaternion.Euler(0,0,-side * Mathf.Atan2(peak,span) * Mathf.Rad2Deg);
                    gable.AddComponent<BoxCollider>().size = new Vector3(Mathf.Sqrt(span * span + peak * peak),.15f,run + .75f);
                }
            }
        }

        void Box(float x, float bottom, float z, float width, float height, float depth, CastleSection section, float yaw = 0)
        {
            pieces.Add(new CastlePlacement(new Vector3(x, bottom + height / 2, z),
                Quaternion.Euler(0, yaw, 0), section, Mathf.FloorToInt(bottom / 2), new Vector3(width, height, depth)));
        }

        void Hall(float x, float z, float width, float depth, float y, float height, int levels, int columns, bool bell = false, float yaw = 0)
        {
            int first = pieces.Count;
            for (int level = 0; level < levels; level++)
            {
                if (x == 0 && level == 1) height *= .6f;
                float span = width - 2, reach = depth - 2;
                for (int side = -1; side <= 1; side += 2)
                {
                    float row = z + side * reach / 2;
                    for (int bay = 0; bay < columns; bay++)
                        Box(x + span * (bay / (columns - 1f) - .5f), y, row, 1, height, 1, CastleSection.Wall);
                    Box(x, y + height, row, width, .6f, 1, CastleSection.Wall);
                }
                if (!bell || level + 1 < levels)
                {
                    float bayWidth = span / (columns - 1);
                    bool main = x == 0;
                    float panel = main ? (level == 0 ? .46f : .47f) : bell || levels == 4 ? .44f : level == 0 ? .41f : .42f;
                    for (int bay = 0; bay < columns - 1; bay++)
                    {
                        float center = x - span / 2 + (bay + .5f) * bayWidth;
                        bool sanctuary = main && bay == (columns - 2) / 2 && level == 0;
                        Box(center, y, sanctuary ? z - reach * .24f : z + reach / 2, bayWidth - 1.05f, height,
                            sanctuary ? .48f : .42f, CastleSection.Keep);
                        if (sanctuary) continue;
                        // A recessed shop front leaves a doorway beside the end post.
                        Box(center - (columns == 2 ? .55f : 0), y, z - reach / 2 + (main && level == 0 ? 2.1f : .75f),
                            bayWidth - (columns == 2 ? 2.2f : 1.05f), height, panel, CastleSection.Keep);
                    }
                    for (int side = -1; side <= 1; side += 2)
                        Box(x + side * span / 2, y, z, reach - 1.1f, height, .42f, CastleSection.Keep, 90);
                }
                y += height + .6f;
                bool last = level + 1 == levels;
                float cap = last ? (bell ? 1.4f : levels == 4 ? 1.35f : 1.25f) : 1;
                Box(x, y, z, width + 1, cap, depth + 1, CastleSection.Corner);
                y += cap;
                width *= .80f;
                depth *= .80f;
            }
            if (yaw == 0) return;
            var turn = Quaternion.Euler(0,yaw,0);
            var pivot = new Vector3(x,0,z);
            for (int i = first; i < pieces.Count; i++)
            {
                var part = pieces[i];
                pieces[i] = new CastlePlacement(pivot + turn * (part.Position - pivot),turn * part.Rotation,
                    part.Section,part.Course,part.Size);
            }
        }
    }
}
