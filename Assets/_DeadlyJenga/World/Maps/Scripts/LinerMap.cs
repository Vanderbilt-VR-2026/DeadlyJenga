using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Sinking Liner (work in progress): a Titanic-sized ship that floats on simple buoyancy physics.
    // The hull is built from sections welded into one Rigidbody, so it moves as one ship. It can break at
    // each split line: the piece holding that line becomes two separate physics bodies, each still floating,
    // tilting and sinking on its own. Sections take on water (flood), lose buoyancy, and drag their piece down.
    // The automatic story follows the Titanic: the bow floods and the stern lifts high, the hull breaks in the
    // middle, the stern half falls back, tips up on its flooded end, stands in the air and slowly goes under.
    // Ship space: origin at the waterline in the middle, x = starboard, y = up, z = toward the bow.
    public sealed class LinerMap : MonoBehaviour, IWorldQuery, ISyncedWorld, IRebuildable
    {
        [Header("Hull (about the Titanic's size)")]
        public float length = 269, beam = 28, hullHeight = 20, draft = 10;
        [Tooltip("Where the hull can split, as fractions of its length from the stern. Sections lie between them.")]
        public float[] splitLines = { .25f, .5f, .75f };
        public float sectionMass = 2000;
        public Material colorSource;

        [Header("Water")]
        public float waterHeight;
        [Tooltip("How strongly the water slows the hull (1 = about critically damped bobbing).")]
        public float waterDamping = .6f;
        [Tooltip("Buoyancy counts the hull from the keel up to this height above the waterline (deck plus deckhouse).")]
        public float buoyancyTop = 16;
        [Tooltip("Extra weight of the water inside a flooded section, as a share of the section's own weight.")]
        public float floodWeight = .3f;
        [Tooltip("How strongly the water slows pieces that are deep under (makes sinking slow).")]
        public float underwaterDrag = 4, underwaterSpin = 1;

        [Header("Sinking story (automatic)")]
        public bool autoSinking = true;
        public float calmSeconds = 25;
        [Tooltip("The bow section fills over this long; the section behind it fills partly (secondFlood).")]
        public float bowFloodSeconds = 60;
        [Range(0, 1)] public float secondFlood;
        [Tooltip("Twists the flooding ship about its middle (no lift, no weight) so the stern rises high while the middle stays at the waterline. " +
                 "Without it the ship sinks deep before it tips far, and the bow half is already out of sight when it breaks.")]
        public bool tipAssist = true;
        [Tooltip("When the ship's pitch passes this (stern high in the air), it breaks at breakLine.")]
        public float breakPitch = 22;
        [Tooltip("Breaks anyway this long after the bow starts flooding.")]
        public float breakAfterSeconds = 120;
        [Tooltip("Which split line breaks (0 = nearest the stern).")]
        public int breakLine = 1;
        [Tooltip("At a break, the open end behind the line fills this much over brokenEndSeconds: the stern half tips up on it.")]
        [Range(0, 1)] public float brokenEndFlood = .85f;
        public float brokenEndSeconds = 20;
        [Tooltip("At a break, the section ahead of the line fills over this long: the bow half goes down nose first.")]
        public float bowSinkSeconds = 90;
        [Tooltip("The stern stands up for this long after the break, then fills to sternFlood over sternSinkSeconds and slowly goes under.")]
        public float sternHoldSeconds = 50;
        [Range(0, 1)] public float sternFlood = .9f;
        public float sternSinkSeconds = 150;
        public bool generateOnStart = true;

        readonly List<Rigidbody> bodies = new List<Rigidbody>();
        readonly List<LinerSegment> segments = new List<LinerSegment>();
        readonly List<Transform> spawns = new List<Transform>();
        readonly Collider[] clearance = new Collider[16];
        readonly List<Material> materials = new List<Material>();
        Material black, red, white, wood, buff, dark;
        Transform shipRoot;
        readonly List<float> underwater = new List<float>();
        float clock, breachedAt, brokeAt, tipError;
        bool breached, broken, sternFlooding, replica;
        int spawnTurn;

        public IReadOnlyList<Rigidbody> Blocks => bodies;
        public IReadOnlyList<LinerSegment> Segments => segments;
        public int PieceCount => bodies.Count(b => b);
        public float Clock => clock;
        public bool Breached => breached;
        public bool Broken => broken;
        public float BrokeAt => brokeAt;
        public Transform Root => transform;
        public int Revision { get; private set; }
        public Vector3 Center => transform.position;
        public float TopHeight => transform.position.y + hullHeight - draft + 30;
        public float Radius => length * .5f + 10;
        public float StepHeight => 1;
        public Bounds PlayableBounds => new Bounds(transform.position + Vector3.up * 10, new Vector3(length + 120, 80, length + 120));
        float Deck => hullHeight - draft;

        void OnEnable() { Services.World = this; }
        void OnDisable() { if (ReferenceEquals(Services.World, this)) Services.World = null; }
        void Start() { if (generateOnStart && bodies.Count == 0) Rebuild(); }
        void OnDestroy() { foreach (var m in materials) if (m) { if (Application.isPlaying) Destroy(m); else DestroyImmediate(m); } }

        public float WaterHeight(Vector3 position) => waterHeight;
        public bool IsSheltered(Vector3 position) => Physics.Raycast(position + Vector3.up * .1f, Vector3.up, 60, 1, QueryTriggerInteraction.Ignore);

        // ---------- Build ----------

        public void Rebuild()
        {
            if (shipRoot) { shipRoot.gameObject.SetActive(false); if (Application.isPlaying) Destroy(shipRoot.gameObject); else DestroyImmediate(shipRoot.gameObject); }
            bodies.Clear(); segments.Clear(); spawns.Clear();
            clock = 0; tipError = 0; breached = broken = sternFlooding = false;
            if (materials.Count == 0) MakeMaterials();

            shipRoot = new GameObject("Liner").transform;
            shipRoot.SetParent(transform, false);
            var first = NewBody("Liner (whole ship)", shipRoot);
            var lines = splitLines.Where(f => f > 0 && f < 1).OrderBy(f => f).ToList();
            float stern = -length * .5f;
            var edges = new List<float> { stern };
            edges.AddRange(lines.Select(f => stern + f * length));
            edges.Add(length * .5f);
            for (int i = 0; i + 1 < edges.Count; i++) segments.Add(BuildSegment(i, edges[i], edges[i + 1], edges.Count - 1, first.transform));
            Funnels();
            ConfigureBody(first);
            Physics.SyncTransforms();
            Revision++;
        }

        Rigidbody NewBody(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var body = go.AddComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.linearDamping = .02f; body.angularDamping = .3f;
            body.isKinematic = replica;
            bodies.Add(body);
            return body;
        }

        // Mass, center of mass (low, for a stable float) from the sections the body now carries.
        void ConfigureBody(Rigidbody body)
        {
            var parts = body.GetComponentsInChildren<LinerSegment>();
            body.mass = Mathf.Max(1, parts.Sum(p => p.mass));
            var center = Vector3.zero;
            // Low weight (60% of the draft below the waterline) keeps the float points' righting push ahead of gravity's tipping.
            foreach (var p in parts) center += p.transform.TransformPoint(new Vector3(0, -draft * .6f, (p.zFrom + p.zTo) * .5f));
            body.centerOfMass = body.transform.InverseTransformPoint(center / Mathf.Max(1, parts.Length));
            body.ResetInertiaTensor();
        }

        LinerSegment BuildSegment(int index, float z0, float z1, int count, Transform body)
        {
            var go = new GameObject($"Section {index + 1} ({(index == 0 ? "stern" : index == count - 1 ? "bow" : "middle")})");
            go.transform.SetParent(body, false);
            var seg = go.AddComponent<LinerSegment>();
            seg.index = index; seg.zFrom = z0; seg.zTo = z1; seg.mass = sectionMass;
            var col = new GameObject("Collision").transform; col.SetParent(go.transform, false);
            var vis = new GameObject("Visual").transform; vis.SetParent(go.transform, false);
            float half = beam * .5f, keel = -draft, deck = Deck;
            // A 10 cm gap at every split line so broken pieces never start inside each other.
            float a = z0 + (index > 0 ? .05f : 0), b = z1 - (index < count - 1 ? .05f : 0);
            bool bow = index == count - 1, stern = index == 0;

            // Hull: full-width box, stepped narrower toward the bow tip and slightly at the stern.
            float mainA = stern ? a + 6 : a, mainB = bow ? b - 24 : b;
            HullBox(col, vis, "Hull", half, mainA, mainB, keel, deck);
            if (stern) HullBox(col, vis, "Stern", half * .82f, a, a + 6, keel + 4, deck);
            if (bow)
            {
                HullBox(col, vis, "Bow 1", half * .8f, b - 24, b - 14, keel + 1, deck);
                HullBox(col, vis, "Bow 2", half * .55f, b - 14, b - 6, keel + 2, deck);
                HullBox(col, vis, "Bow 3", half * .28f, b - 6, b, keel + 3, deck);
            }
            // Deckhouse (white superstructure) on every section, with walkways around it.
            float houseA = a + (stern ? 18 : 6), houseB = b - (bow ? 40 : 6);
            if (houseB - houseA > 8)
            {
                Box(col, vis, "Deckhouse", new Vector3(0, deck + 3, (houseA + houseB) * .5f), new Vector3(beam - 9, 6, houseB - houseA), white);
                Paint(vis, "Deckhouse roof", new Vector3(0, deck + 6.05f, (houseA + houseB) * .5f), new Vector3(beam - 8.6f, .14f, houseB - houseA + .4f), wood);
                Paint(vis, "Windows", new Vector3(0, deck + 3.6f, (houseA + houseB) * .5f), new Vector3(beam - 8.94f, 1, houseB - houseA - 2), dark);
            }
            // Rails along the deck edges (and across the ends of the ship).
            float railA = stern ? a + 6 : a, railB = bow ? b - 24 : b;
            Rail(col, vis, new Vector3(-half + .2f, deck, railA), new Vector3(-half + .2f, deck, railB));
            Rail(col, vis, new Vector3(half - .2f, deck, railA), new Vector3(half - .2f, deck, railB));
            if (stern) { Rail(col, vis, new Vector3(-half * .82f + .2f, deck, a + .2f), new Vector3(half * .82f - .2f, deck, a + .2f)); }
            if (bow) { Rail(col, vis, new Vector3(-half * .8f + .2f, deck, b - 24), new Vector3(-half * .28f + .2f, deck, b - .2f)); Rail(col, vis, new Vector3(half * .8f - .2f, deck, b - 24), new Vector3(half * .28f - .2f, deck, b - .2f)); }
            // A red seam marks each split line.
            if (!stern) Paint(vis, "Split line", new Vector3(0, (keel + deck) * .5f, a + .08f), new Vector3(beam + .1f, hullHeight + .1f, .16f), red);
            // Masts fore and aft.
            if (bow) Paint(vis, "Fore mast", new Vector3(0, deck + 15, b - 34), new Vector3(.8f, 15, .8f), buff, PrimitiveType.Cylinder);
            if (stern) Paint(vis, "Aft mast", new Vector3(0, deck + 13, a + 12), new Vector3(.8f, 13, .8f), buff, PrimitiveType.Cylinder);

            // Float points: 3 across x 4 along the keel of the full-width part.
            var points = new List<Vector3>();
            for (int i = 0; i < 4; i++)
                for (int j = -1; j <= 1; j++)
                    points.Add(new Vector3(j * half * .8f, keel, Mathf.Lerp(a + 3, b - 3, i / 3f)));
            seg.floatPoints = points.ToArray();

            // Spawn spots on the deck, either side of the deckhouse.
            for (int side = -1; side <= 1; side += 2)
            {
                var s = new GameObject("Spawn").transform; s.SetParent(go.transform, false);
                s.localPosition = new Vector3(side * (half - 2.5f), deck + .05f, (mainA + mainB) * .5f);
                spawns.Add(s);
            }
            return seg;
        }

        void Funnels()
        {
            // Four funnels over the middle of the ship, each on whichever section it stands over.
            foreach (float f in new[] { -.22f, -.08f, .06f, .2f })
            {
                float z = f * length;
                var seg = segments.FirstOrDefault(s => z > s.zFrom + 5 && z < s.zTo - 5) ?? segments.OrderBy(s => Mathf.Abs((s.zFrom + s.zTo) * .5f - z)).First();
                var vis = seg.transform.Find("Visual"); var col = seg.transform.Find("Collision");
                var p = new Vector3(0, Deck + 6 + 8, z);
                Paint(vis, "Funnel", p, new Vector3(6, 8, 6), buff, PrimitiveType.Cylinder);
                Paint(vis, "Funnel top", p + Vector3.up * 7.2f, new Vector3(6.1f, .9f, 6.1f), dark, PrimitiveType.Cylinder);
                Collide(col, "Funnel", p, new Vector3(5.4f, 16, 5.4f));
            }
        }

        void HullBox(Transform col, Transform vis, string name, float halfWidth, float z0, float z1, float y0, float y1)
        {
            var c = new Vector3(0, (y0 + y1) * .5f, (z0 + z1) * .5f);
            var size = new Vector3(halfWidth * 2, y1 - y0, z1 - z0);
            Collide(col, name, c, size);
            Paint(vis, name, c, size, black);
            Paint(vis, name + " antifouling", new Vector3(0, (y0 - .5f + -1.2f) * .5f, c.z), new Vector3(size.x + .06f, -1.2f - y0 + .5f, size.z + .02f), red);
            Paint(vis, name + " deck", new Vector3(0, y1 - .04f, c.z), new Vector3(size.x - .3f, .1f, size.z - .1f), wood);
            Paint(vis, name + " white band", new Vector3(0, y1 - .6f, c.z), new Vector3(size.x + .06f, .5f, size.z + .02f), white);
        }

        void Box(Transform col, Transform vis, string name, Vector3 center, Vector3 size, Material m) { Collide(col, name, center, size); Paint(vis, name, center, size, m); }

        void Rail(Transform col, Transform vis, Vector3 from, Vector3 to)
        {
            var along = to - from; if (along.magnitude < .1f) return;
            var rot = Quaternion.LookRotation(along.normalized, Vector3.up);
            var c = (from + to) * .5f + Vector3.up * .55f;
            var size = new Vector3(.3f, 1.1f, along.magnitude);
            var box = Collide(col, "Rail", c, size); box.transform.localRotation = rot;
            var v = Paint(vis, "Rail", c, size, white); v.transform.localRotation = rot;
        }

        BoxCollider Collide(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = center;
            var box = go.AddComponent<BoxCollider>(); box.size = size;
            return box;
        }

        GameObject Paint(Transform parent, string name, Vector3 center, Vector3 size, Material m, PrimitiveType shape = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(shape); go.name = name;
            var c = go.GetComponent<Collider>(); if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            go.transform.SetParent(parent, false); go.transform.localPosition = center; go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        void MakeMaterials()
        {
            Material Make(string n, Color c)
            {
                var m = colorSource ? new Material(colorSource) : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                m.name = n; m.mainTexture = null; m.color = c; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                materials.Add(m); return m;
            }
            black = Make("Liner hull", new Color(.08f, .09f, .13f)); red = Make("Liner antifouling", new Color(.72f, .14f, .12f));
            white = Make("Liner white", new Color(.96f, .95f, .92f)); wood = Make("Liner deck", new Color(.84f, .7f, .5f));
            buff = Make("Liner funnel", new Color(.95f, .7f, .25f)); dark = Make("Liner windows", new Color(.12f, .14f, .2f));
        }

        // ---------- Physics: buoyancy, flooding, splitting ----------

        void FixedUpdate()
        {
            if (replica || bodies.Count == 0) return;
            Step(Time.fixedDeltaTime);
        }

        // Applies one physics step of buoyancy and runs the sinking story. Tests call it before Physics.Simulate.
        // Each float point stands for a column of hull from the keel up to buoyancyTop; it pushes up by how much
        // of that column is under water. That still works when a piece stands on end.
        public void Step(float dt)
        {
            clock += dt;
            float g = -Physics.gravity.y;
            underwater.Clear();
            foreach (var _ in bodies) underwater.Add(0);
            foreach (var seg in segments)
            {
                if (!seg) continue;
                seg.StepFlood(dt);
                var body = seg.GetComponentInParent<Rigidbody>();
                if (!body || body.isKinematic) continue;
                int n = seg.floatPoints.Length, which = bodies.IndexOf(body);
                float column = buoyancyTop + draft;
                // At the design draft the dry points carry exactly the section's weight.
                float k = seg.mass * g * column / (n * draft);
                float c = seg.mass / n * waterDamping * 2 * Mathf.Sqrt(g / draft);
                float water = seg.flood * floodWeight * seg.mass * g / n;
                foreach (var local in seg.floatPoints)
                {
                    var keel = seg.transform.TransformPoint(local);
                    var top = seg.transform.TransformPoint(new Vector3(local.x, buoyancyTop, local.z));
                    if (water > 0) body.AddForceAtPosition(Vector3.down * water, keel, ForceMode.Force);
                    float low = Mathf.Min(keel.y, top.y), high = Mathf.Max(keel.y, top.y), span = Mathf.Max(high - low, 1);
                    float depth = Mathf.Clamp01((waterHeight - ((low + high) * .5f - span * .5f)) / span);
                    if (depth <= 0) continue;
                    // Push at the middle of the wet part of the column.
                    var p = Vector3.Lerp(keel, top, keel.y < top.y ? depth * .5f : 1 - depth * .5f);
                    var force = Vector3.up * (k * depth * (1 - seg.flood)) - body.GetPointVelocity(p) * (c * depth);
                    body.AddForceAtPosition(force, p, ForceMode.Force);
                    if (which >= 0) underwater[which] += Mathf.Clamp01((depth - .6f) / .4f) / n;
                }
            }
            // Pieces that are deep under move slowly, so they sink gradually instead of dropping like a stone.
            for (int i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                if (!body || body.isKinematic || underwater[i] <= 0) continue;
                float share = underwater[i] / Mathf.Max(1, body.GetComponentsInChildren<LinerSegment>().Length);
                body.AddForce(-body.linearVelocity * (underwaterDrag * share), ForceMode.Acceleration);
                body.AddTorque(-body.angularVelocity * (underwaterSpin * share), ForceMode.Acceleration);
            }
            if (autoSinking) Direct(dt);
            // Pieces that have sunk far below stop simulating.
            foreach (var body in bodies)
                if (body && !body.isKinematic && body.worldCenterOfMass.y < waterHeight - 120) { body.isKinematic = true; body.gameObject.SetActive(false); }
        }

        // The story: calm, the bow floods and the stern lifts out of the water, the ship breaks, the stern half
        // falls back, then tips up on its flooded end and stands in the air before it slowly goes under.
        void Direct(float dt)
        {
            if (!breached && clock >= calmSeconds) BreachBow(bowFloodSeconds);
            if (!breached) return;
            if (!broken && breakLine >= 0 && breakLine < segments.Count - 1)
            {
                var body = segments[breakLine].GetComponentInParent<Rigidbody>();
                if (body && tipAssist) TipTowardBreak(body, dt);
                if (body && (Pitch(body) >= breakPitch || clock - breachedAt >= breakAfterSeconds)) Break(breakLine);
            }
            if (broken && !sternFlooding && clock - brokeAt >= sternHoldSeconds)
            {
                sternFlooding = true;
                segments[0].Breach(sternFlood, sternSinkSeconds);
            }
        }

        // Steers the bow-down pitch toward a little past breakPitch over bowFloodSeconds with a pure twist.
        void TipTowardBreak(Rigidbody body, float dt)
        {
            float progress = Mathf.Clamp01((clock - breachedAt) / Mathf.Max(1, bowFloodSeconds));
            float target = (breakPitch + 2) * progress * Mathf.Deg2Rad;
            float pitch = Mathf.Asin(Mathf.Clamp(-body.transform.forward.y, -1, 1));   // bow down = positive
            float error = target - pitch;
            tipError += error * dt;
            float spin = Vector3.Dot(body.angularVelocity, body.transform.right);
            body.AddTorque(body.transform.right * (2 * error + .6f * tipError - 2 * spin), ForceMode.Acceleration);
        }

        public static float Pitch(Rigidbody body) => Mathf.Abs(Mathf.DeltaAngle(0, body.rotation.eulerAngles.x));

        // Splits the ship at split line `line` (0 = nearest the stern). The piece holding that line becomes two
        // physics bodies; the bow-side part keeps the velocity it had at its own center. Returns false if that
        // line has already broken. Both broken ends start taking on water.
        public bool Break(int line)
        {
            if (line < 0 || line >= segments.Count - 1) return false;
            var stern = segments[line]; var bow = segments[line + 1];
            var body = stern.GetComponentInParent<Rigidbody>();
            if (!body || bow.GetComponentInParent<Rigidbody>() != body) return false;
            if (!broken) brokeAt = clock;
            broken = true;
            // Everything bow-ward of the line that rides this body moves to a new body.
            var moving = segments.Where(s => s.index > line && s.GetComponentInParent<Rigidbody>() == body).ToList();
            var piece = NewBody("Liner piece", shipRoot);
            piece.transform.SetPositionAndRotation(body.transform.position, body.transform.rotation);
            var velocity = body.linearVelocity; var spin = body.angularVelocity;
            foreach (var s in moving) s.transform.SetParent(piece.transform, true);
            piece.position = body.position; piece.rotation = body.rotation;
            ConfigureBody(body); ConfigureBody(piece);
            piece.linearVelocity = velocity + Vector3.Cross(spin, piece.worldCenterOfMass - body.worldCenterOfMass);
            piece.angularVelocity = spin;
            Physics.SyncTransforms();
            stern.Breach(brokenEndFlood, brokenEndSeconds); bow.Breach(1, bowSinkSeconds);
            NamePieces();
            return true;
        }

        // Names each piece after the sections it carries, e.g. "Liner piece (sections 1-2)".
        void NamePieces()
        {
            foreach (var body in bodies)
            {
                if (!body) continue;
                var parts = body.GetComponentsInChildren<LinerSegment>(true).Select(s => s.index + 1).OrderBy(i => i).ToList();
                if (parts.Count == 0) continue;
                body.name = parts.Count == 1 ? $"Liner piece (section {parts[0]})" : $"Liner piece (sections {parts[0]}-{parts[parts.Count - 1]})";
            }
        }

        // Starts the sinking: the bow section fills over `seconds`, the section behind it partly.
        public void BreachBow(float seconds)
        {
            if (!breached) breachedAt = clock;
            breached = true;
            segments[segments.Count - 1].Breach(1, seconds);
            if (segments.Count > 2) segments[segments.Count - 2].Breach(secondFlood, seconds * 1.3f);
        }

        // ---------- Spawns and network ----------

        public Vector3 SafeSpawnPoint()
        {
            Physics.SyncTransforms();
            for (int i = 0; i < spawns.Count; i++)
            {
                var s = spawns[(spawnTurn + i) % spawns.Count];
                if (!s || !s.gameObject.activeInHierarchy || s.position.y < waterHeight + 3) continue;
                var p = s.position + Vector3.up * .05f;
                if (Physics.OverlapCapsuleNonAlloc(p + Vector3.up * .2f, p + Vector3.up * .74f, .18f, clearance, ~(1 << Layers.Survivor), QueryTriggerInteraction.Ignore) > 0) continue;
                spawnTurn = (spawnTurn + i + 1) % spawns.Count;
                return p;
            }
            return transform.position + Vector3.up * (Deck + 1);
        }

        const string Prefix = "liner-v1:";
        public string SaveLayout() => Prefix + segments.Count;
        public bool LoadLayout(string layout, int expectedBlocks, Pose origin, Vector3 scale)
        {
            if (layout == null || !layout.StartsWith(Prefix, StringComparison.Ordinal) || expectedBlocks != 1) return false;
            transform.SetPositionAndRotation(origin.position, origin.rotation); transform.localScale = scale;
            Rebuild(); return true;
        }
        // Network copies only follow the host; splitting changes the body count, which multiplayer does not handle yet.
        public void SetReplica(bool value) { replica = value; foreach (var b in bodies) if (b) b.isKinematic = value; }
    }
}
