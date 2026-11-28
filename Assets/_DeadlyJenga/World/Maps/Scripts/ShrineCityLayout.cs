using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Shrine City: the redesigned Shrine Town built from its Blender-tested physics bodies.
    // Physics preview only: every body is a group of boxes with breakable timber joints; art comes later.
    // Data: Resources/ShrineCityBodies.json, written by the Blender scripts in the Shrine Town art pass
    // (export_unity_bodies.py). Coordinates are map-local; y is height above the foundation top.
    public static class ShrineCityLayout
    {
        public const string Preset = "shrine-city";
        public static readonly Vector2 Size = new Vector2(210, 240);
        public const int HeightCourses = 27;

        [Serializable] sealed class Box { public float[] c, x, y, z, s; }
        [Serializable] sealed class Body { public string building, id; public float mass; public bool @fixed; public Box[] boxes; }
        [Serializable] sealed class Joint { public int a, b; public float f; }
        [Serializable] sealed class Ground { public string name; public float[] v; public int[] t; }
        [Serializable] sealed class Data { public int version; public Body[] bodies; public Joint[] joints; public Ground[] ground; }

        static Data data;
        static Data Load()
        {
            if (data != null) return data;
            var asset = Resources.Load<TextAsset>("ShrineCityBodies");
            if (!asset) throw new InvalidOperationException("Resources/ShrineCityBodies.json is missing.");
            data = JsonUtility.FromJson<Data>(asset.text);
            if (data?.bodies == null || data.bodies.Length == 0) throw new InvalidOperationException("ShrineCityBodies.json has no bodies.");
            return data;
        }

        static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);
        static Quaternion Rotation(Box b) => Quaternion.LookRotation(V(b.z), V(b.y));
        // The largest box is the body's root collider: water contact, spawning and footprints read the root.
        static int Root(Body body)
        {
            int best = 0; float volume = -1;
            for (int i = 0; i < body.boxes.Length; i++)
            {
                var s = body.boxes[i].s; float v = s[0] * s[1] * s[2];
                if (v > volume) { volume = v; best = i; }
            }
            return best;
        }

        public static int BodyCount => Load().bodies.Length;
        // Multiplies every authored joint break force. The Blender joints (1) hold ~300x carried weight, too strong for
        // any in-game disaster; Unity sweeps against the match earthquake (8 m/s2, 2 Hz) chose 0.2: the town stands and
        // the quake breaks it progressively.
        public const float DefaultJointScale = .2f;
        public static float JointScale = DefaultJointScale;
        public static string Building(int index) => Load().bodies[index].building;
        public static string BodyId(int index) => Load().bodies[index].id;

        public static List<CastlePlacement> Build()
        {
            var d = Load();
            var pieces = new List<CastlePlacement>(d.bodies.Length);
            foreach (var body in d.bodies)
            {
                var b = body.boxes[Root(body)];
                var section = body.id.Contains("roof") ? CastleSection.Corner : CastleSection.Keep;
                pieces.Add(new CastlePlacement(V(b.c), Rotation(b), section, Mathf.FloorToInt(b.c[1] / 2), V(b.s)));
            }
            return pieces;
        }

        // Runs after the block is spawned: the other boxes join the body as child colliders, the survivor proxy
        // gets the same shapes, and the authored mass replaces the block-volume mass. Fixed props do not move.
        public static void Configure(int index, Rigidbody body, CastleBlock block)
        {
            var d = Load(); var data = d.bodies[index];
            int root = Root(data);
            var rootBox = data.boxes[root];
            Vector3 origin = V(rootBox.c); Quaternion turn = Rotation(rootBox); Quaternion back = Quaternion.Inverse(turn);
            var rootCollider = body.GetComponent<BoxCollider>();
            Transform frame = null, walk = null, look = null;
            for (int i = 0; i < data.boxes.Length; i++)
            {
                if (i == root) continue;
                var box = data.boxes[i];
                Vector3 at = back * (V(box.c) - origin); Quaternion spin = back * Rotation(box);
                if (!frame)
                {
                    // Cancel the root box's scale so child boxes keep their own sizes without shear.
                    frame = new GameObject("Body boxes").transform;
                    frame.SetParent(body.transform, false);
                    frame.gameObject.layer = body.gameObject.layer;
                    var size = body.transform.localScale;
                    frame.localScale = new Vector3(1 / size.x, 1 / size.y, 1 / size.z);
                }
                var shape = new GameObject("Box").transform;
                shape.gameObject.layer = frame.gameObject.layer;
                shape.SetParent(frame, false);
                shape.SetLocalPositionAndRotation(at, spin);
                var collider = shape.gameObject.AddComponent<BoxCollider>();
                collider.size = V(box.s);
                if (rootCollider) { collider.excludeLayers = rootCollider.excludeLayers; collider.sharedMaterial = rootCollider.sharedMaterial; }
                if (block && block.visualRoot)
                {
                    // Gray-box look for the extra boxes, in the block's own material (art replaces this later).
                    if (!look)
                    {
                        look = new GameObject("Body box visuals").transform;
                        look.SetParent(block.visualRoot, false);
                        var size = block.visualRoot.lossyScale;
                        look.localScale = new Vector3(1 / size.x, 1 / size.y, 1 / size.z);
                        look.localRotation = Quaternion.identity;
                    }
                    var piece = new GameObject("Box visual").transform;
                    piece.SetParent(look, false);
                    piece.SetLocalPositionAndRotation(at, spin);
                    piece.localScale = V(box.s);
                    piece.gameObject.AddComponent<MeshFilter>().sharedMesh = CubeMesh();
                    piece.gameObject.AddComponent<MeshRenderer>().sharedMaterial = block.sectionMaterial;
                }
                if (block)
                {
                    if (!walk) walk = block.CreateTraversalFrame();
                    if (walk) ShrineTownLayout.ColliderBox(walk, "Walkable box", at, V(box.s), spin);
                }
            }
            if (block && walk) block.RegisterTraversalShapes(walk);
            body.mass = Mathf.Max(1, data.mass);
            if (data.@fixed) body.isKinematic = true;
        }

        static Mesh cube;
        static Mesh CubeMesh()
        {
            if (cube) return cube;
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = temp.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) UnityEngine.Object.Destroy(temp); else UnityEngine.Object.DestroyImmediate(temp);
            return cube;
        }

        // Timber joinery: each authored joint ties two bodies and snaps above its break force.
        public static void Join(List<Rigidbody> bodies)
        {
            foreach (var j in Load().joints)
            {
                if (j.a < 0 || j.b < 0 || j.a >= bodies.Count || j.b >= bodies.Count) continue;
                var a = bodies[j.a]; var b = bodies[j.b];
                if (!a || !b || a.isKinematic || b.isKinematic) continue;
                var joint = a.gameObject.AddComponent<FixedJoint>();
                joint.connectedBody = b;
                // A load hanging off a long body twists the joint more than it pulls; scale the torque limit by the lever.
                float lever = 1 + Vector3.Distance(a.worldCenterOfMass, b.worldCenterOfMass);
                joint.breakForce = j.f * JointScale;
                joint.breakTorque = j.f * JointScale * lever;
                joint.enablePreprocessing = false;
            }
        }

        // Mud islands, islets and pavers: static ground with the exact authored shape.
        public static void BuildGround(Transform parent, Material material)
        {
            var root = new GameObject("Shrine City ground").transform;
            root.gameObject.layer = parent.gameObject.layer;
            root.SetParent(parent, false);
            foreach (var g in Load().ground)
            {
                if (g.v == null || g.t == null || g.v.Length < 9 || g.t.Length < 3) continue;
                var points = new Vector3[g.v.Length / 3];
                for (int i = 0; i < points.Length; i++) points[i] = new Vector3(g.v[3 * i], g.v[3 * i + 1], g.v[3 * i + 2]);
                var mesh = new Mesh { name = g.name };
                mesh.indexFormat = points.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.SetVertices(points);
                mesh.SetTriangles(g.t, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var go = new GameObject(g.name);
                go.layer = root.gameObject.layer;
                go.transform.SetParent(root, false);
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                if (material) renderer.sharedMaterial = material;
            }
        }
    }
}
