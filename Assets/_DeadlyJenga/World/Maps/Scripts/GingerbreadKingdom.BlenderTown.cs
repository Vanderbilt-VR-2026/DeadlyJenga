using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CastlePrototype
{
    // Physics validation import of the Blender Gingerbread Kingdom (town, castle and Big Ben).
    // Off by default: the normal match still builds the hand-made kingdom. Tests switch it on.
    // Every body, collision shape and glue joint comes from the Blender physics files; only the art is thinned.
    public sealed partial class GingerbreadKingdom : ITraversalCollisionWorld
    {
        public static bool UseBlenderPhysicsTown;
        // Survivors collide with and climb the imported pieces themselves.
        public bool UsesOriginalColliders => builtBlender;
        // The castle and Big Ben part files are authored in town coordinates, so they need no offset.
        static readonly (string file, Vector3 offset)[] BlenderParts =
        {
            ("GingerbreadPhysicsTown", Vector3.zero),
            ("GingerbreadPhysicsCastle", Vector3.zero),
            ("GingerbreadPhysicsBigBen", Vector3.zero),
        };
        const float SettleSeconds = 2.5f;
        const float LoadMargin = 3f;
        // Lightly loaded glue still holds this many times the lighter piece's weight.
        const float GlueStrength = 15f;

        [Serializable] sealed class BoxSpec { public Vector3 p, size, forward, up; }
        [Serializable] sealed class BodySpec { public string name; public float mass; public Vector3 p; public BoxSpec[] boxes; public int[] hulls, visuals; }
        [Serializable] sealed class StaticSpec { public string name, group; public int collider, visual; }
        [Serializable] sealed class JointSpec { public int a, b, count; public Vector3 at; public float impulse; }
        [Serializable] sealed class LayoutSpec
        {
            public float blenderHz; public bool hasSpawn; public Vector3 spawn, bodyMin, bodyMax;
            public BodySpec[] bodies; public StaticSpec[] statics; public JointSpec[] joints;
        }
        sealed class Glue { public FixedJoint joint; public float floor, cap, load, twist; }

        bool builtBlender;
        Vector3 blenderSpawn;
        Bounds blenderBounds;
        float blenderSettledAt;
        readonly List<Glue> blenderGlue = new();
        public int BlenderGlueCount => blenderGlue.Count;
        public bool BuiltFromBlender => builtBlender;

        void BuildBlenderTown()
        {
            builtBlender = true; blenderGlue.Clear();
            blenderSettledAt = Time.time + SettleSeconds;
            var anchor = new GameObject("Glue anchor").AddComponent<Rigidbody>();
            anchor.transform.SetParent(town, false); anchor.isKinematic = true;
            var kill = Group("Below the snow", town, V(0, -8.5f, 0));
            kill.gameObject.AddComponent<BoxCollider>().size = V(260, .6f, 260);
            kill.gameObject.AddComponent<UnderneathTreeLethalFloor>();
            bool first = true;
            foreach (var (file, offset) in BlenderParts)
            {
                var layoutAsset = Resources.Load<TextAsset>(file);
                var meshAsset = Resources.Load<TextAsset>(file + "Meshes");
                if (!layoutAsset || !meshAsset) { Debug.LogWarning("Missing Blender physics file " + file); continue; }
                var layout = JsonUtility.FromJson<LayoutSpec>(layoutAsset.text);
                var part = Group(file, town, offset);
                // The castle and Big Ben were tested in Blender on a flat floor at street level.
                if (!first) Foundation(part, layout.bodyMin, layout.bodyMax);
                var shapes = ReadBlenderMeshes(meshAsset.bytes);
                var built = new List<GingerbreadPiece>();
                foreach (var spec in layout.bodies) built.Add(BlenderBody(part, spec, shapes));
                foreach (var spec in layout.statics) BlenderStatic(part, spec, shapes);
                foreach (var j in layout.joints)
                {
                    var a = built[j.a].Body;
                    var b = j.b >= 0 ? built[j.b].Body : anchor;
                    var at = part.TransformPoint(j.at);
                    var joint = a.gameObject.AddComponent<FixedJoint>();
                    joint.autoConfigureConnectedAnchor = false;
                    joint.connectedBody = b;
                    joint.anchor = a.transform.InverseTransformPoint(at);
                    joint.connectedAnchor = b.transform.InverseTransformPoint(at);
                    joint.enableCollision = false; joint.enablePreprocessing = false;
                    if (j.b >= 0)
                    {
                        // A 2 kg ornament glued to a 1 t floor jitters loose; the solver treats them as at most 10:1.
                        joint.massScale = Mathf.Min(1, 10 * a.mass / b.mass);
                        joint.connectedMassScale = Mathf.Min(1, 10 * b.mass / a.mass);
                    }
                    // Blender breaks glue on impulse per substep; PhysX breaks on force.
                    // Never stronger than Blender, and weak enough for gameplay to break.
                    float lighter = j.b >= 0 ? Mathf.Min(a.mass, b.mass) : a.mass;
                    float cap = j.impulse * layout.blenderHz, floor = Mathf.Min(cap, GlueStrength * 9.81f * lighter);
                    joint.breakForce = joint.breakTorque = float.PositiveInfinity;
                    blenderGlue.Add(new Glue { joint = joint, floor = floor, cap = cap });
                }
                // Spawn and bounds stay in map space; the properties convert them to world space.
                if (first && layout.hasSpawn) blenderSpawn = offset + layout.spawn;
                var lo = offset + layout.bodyMin; var hi = offset + layout.bodyMax;
                if (first) blenderBounds = new Bounds((lo + hi) * .5f, hi - lo); else { blenderBounds.Encapsulate(lo); blenderBounds.Encapsulate(hi); }
                first = false;
            }
            // Stand the spawn on the street surface itself.
            Physics.SyncTransforms();
            IgnoreStartingOverlaps();
            var above = transform.TransformPoint(blenderSpawn + Vector3.up * 20);
            if (Physics.Raycast(above, -transform.up, out var hit, 40, 1, QueryTriggerInteraction.Ignore))
                blenderSpawn = transform.InverseTransformPoint(hit.point);
        }

        void Foundation(Transform part, Vector3 lo, Vector3 hi)
        {
            var slab = Group("Flat foundation", part, V((lo.x + hi.x) * .5f, -.5f, (lo.z + hi.z) * .5f));
            slab.gameObject.AddComponent<BoxCollider>().size = V(hi.x - lo.x + 12, 1, hi.z - lo.z + 12);
        }

        readonly List<(Collider a, Collider b)> overlapping = new();
        public int StartingOverlapCount { get; private set; }
        int overlapCursor;
        // The Blender pieces sit slightly inside their neighbours. PhysX would push those apart at
        // rest and load the glue with random spikes, so they ignore each other until they separate.
        void IgnoreStartingOverlaps()
        {
            overlapping.Clear();
            var near = new Collider[64];
            foreach (var body in pieces)
                foreach (var shape in body.GetComponent<GingerbreadPiece>().shapes)
                {
                    var bounds = shape.bounds;
                    int n = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, near, Quaternion.identity, 1 << Layers.Block, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < n; i++)
                    {
                        var other = near[i]; var rb = other.attachedRigidbody;
                        if (!rb || rb == body || other.GetInstanceID() <= shape.GetInstanceID() || !rb.GetComponent<GingerbreadPiece>()) continue;
                        if (!Physics.ComputePenetration(shape, shape.transform.position, shape.transform.rotation,
                                other, other.transform.position, other.transform.rotation, out _, out float depth) || depth < .004f) continue;
                        Physics.IgnoreCollision(shape, other, true); overlapping.Add((shape, other));
                    }
                }
            StartingOverlapCount = overlapping.Count;
        }

        void RestoreSeparatedShapes()
        {
            for (int i = 0; i < 32 && overlapping.Count > 0; i++)
            {
                overlapCursor %= overlapping.Count;
                var (a, b) = overlapping[overlapCursor];
                if (a && b)
                {
                    var seat = a.bounds; seat.Expand(.08f);
                    if (seat.Intersects(b.bounds)) { overlapCursor++; continue; }
                    Physics.IgnoreCollision(a, b, false);
                }
                overlapping.RemoveAt(overlapCursor);
            }
        }

        void FinishBlenderSettle()
        {
            if (builtBlender && blenderSettledAt <= 0) RestoreSeparatedShapes();
            // Joints cannot break while the freshly built town settles. During the last second
            // each joint records the load it carries; it then breaks at three times that load
            // (at least a light floor), so the town holds at rest but gameplay can break it.
            if (!builtBlender || blenderSettledAt <= 0) return;
            if (Time.time >= blenderSettledAt - 1f)
                foreach (var g in blenderGlue)
                    if (g.joint)
                    {
                        g.load = Mathf.Max(g.load, g.joint.currentForce.magnitude);
                        g.twist = Mathf.Max(g.twist, g.joint.currentTorque.magnitude);
                    }
            if (Time.time < blenderSettledAt) return;
            blenderSettledAt = 0;
            foreach (var g in blenderGlue)
                if (g.joint)
                {
                    g.joint.breakForce = Mathf.Min(g.cap, Mathf.Max(g.floor, g.load * LoadMargin));
                    g.joint.breakTorque = Mathf.Min(g.cap, Mathf.Max(g.floor * .75f, g.twist * LoadMargin));
                }
        }

        GingerbreadPiece BlenderBody(Transform part, BodySpec spec, List<(Mesh mesh, int[] finishes)> shapes)
        {
            var go = new GameObject(spec.name) { layer = Layers.Block };
            go.transform.SetParent(part, false); go.transform.localPosition = spec.p;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(.5f, spec.mass); rb.isKinematic = replica;
            rb.solverIterations = 16; rb.solverVelocityIterations = 6; rb.maxDepenetrationVelocity = .8f;
            var piece = go.AddComponent<GingerbreadPiece>(); piece.rigid = rb;
            foreach (var box in spec.boxes)
            {
                var shape = new GameObject("Shape") { layer = Layers.Block };
                shape.transform.SetParent(go.transform, false);
                shape.transform.localPosition = box.p;
                shape.transform.localRotation = Quaternion.LookRotation(box.forward, box.up);
                var col = shape.AddComponent<BoxCollider>(); col.size = box.size;
                Shape(piece, col);
            }
            foreach (int h in spec.hulls)
            {
                var shape = new GameObject("Hull") { layer = Layers.Block };
                shape.transform.SetParent(go.transform, false);
                var col = shape.AddComponent<MeshCollider>(); col.convex = true; col.sharedMesh = shapes[h].mesh;
                Shape(piece, col);
            }
            piece.art = new GameObject("Visual").transform; piece.art.SetParent(go.transform, false);
            foreach (int v in spec.visuals) Art(piece.art, shapes[v]);
            pieces.Add(rb);
            return piece;
        }

        void Shape(GingerbreadPiece piece, Collider col)
        {
            col.sharedMaterial = friction;
            // Survivors climb and stand on the moving pieces themselves.
            col.includeLayers = 1 << Layers.Survivor; col.layerOverridePriority = 12;
            piece.shapes.Add(col); Climbables.Register(col, piece);
        }

        void BlenderStatic(Transform part, StaticSpec spec, List<(Mesh mesh, int[] finishes)> shapes)
        {
            // Ground holds everything; set dressing (drifts, trees, lanterns) only stops survivors.
            bool decor = spec.group == "static_decor";
            var go = new GameObject(spec.name) { layer = decor ? Layers.Surface : 0 };
            go.transform.SetParent(part, false);
            var col = go.AddComponent<MeshCollider>(); col.sharedMesh = shapes[spec.collider].mesh; col.sharedMaterial = friction;
            Art(go.transform, shapes[spec.visual]);
        }

        void Art(Transform owner, (Mesh mesh, int[] finishes) art)
        {
            var go = new GameObject("Art"); go.transform.SetParent(owner, false);
            go.AddComponent<MeshFilter>().sharedMesh = art.mesh;
            var mats = new Material[art.finishes.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = finishes[Mathf.Clamp(art.finishes[i], 0, finishes.Length - 1)];
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        }

        List<(Mesh, int[])> ReadBlenderMeshes(byte[] bytes)
        {
            using var reader = new BinaryReader(new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress));
            int count = reader.ReadInt32();
            var list = new List<(Mesh, int[])>(count);
            for (int m = 0; m < count; m++)
            {
                int vc = reader.ReadInt32();
                var verts = new Vector3[vc];
                for (int v = 0; v < vc; v++) verts[v] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                Vector3[] normals = null;
                if (reader.ReadByte() == 1)
                {
                    normals = new Vector3[vc];
                    for (int v = 0; v < vc; v++) normals[v] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                }
                int subs = reader.ReadInt32();
                var mesh = new Mesh { name = "Blender piece " + m };
                if (vc > 65535) mesh.indexFormat = IndexFormat.UInt32;
                mesh.vertices = verts; mesh.subMeshCount = subs;
                var fins = new int[subs];
                for (int s = 0; s < subs; s++)
                {
                    fins[s] = reader.ReadInt32();
                    var idx = new int[reader.ReadInt32()];
                    for (int i = 0; i < idx.Length; i++) idx[i] = reader.ReadInt32();
                    mesh.SetTriangles(idx, s, false);
                }
                if (normals != null) mesh.normals = normals;
                mesh.RecalculateBounds();
                meshes.Add(mesh); list.Add((mesh, fins));
            }
            return list;
        }
    }
}
