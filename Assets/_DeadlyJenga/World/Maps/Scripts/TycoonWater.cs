using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Tycoon's water follows its supply.
    // Every wet piece knows what feeds it: the summit pump feeds the slides, the river pump
    // feeds the lazy river, and the bucket and wheel feed themselves. A piece is intact while it
    // rests where it was built. Water reaches a piece only through intact pieces from an intact
    // source. Cut-off pieces drain or dry; a fed piece whose neighbour is gone spills from that end.
    // Everything here is decided from piece poses, so host and joined machines agree.
    public sealed class TycoonWater : MonoBehaviour
    {
        public enum Kind { Pump, Slide, Tube, Basin, River, Fixture }

        sealed class Node
        {
            public int body;
            public Kind kind;
            public bool source;
            public Transform transform;
            public Vector3 restPosition;
            public Quaternion restRotation;
            public TycoonDeckProfile profile;
            public Vector3 size;
            public readonly List<int> downstream = new List<int>();
            public int previous = -1, next = -1;
            public bool intact = true, fed = true;
            public float level = 1;
            public MeshRenderer deck;
            public Material[] wet, dry;
            public bool showingWet = true;
            public Transform water;
            public Vector3 waterRest, waterDown;
        }

        const float MoveTolerance = .15f, TurnTolerance = 5f, SolveInterval = .2f;
        const int MaxSpills = 16;
        readonly List<Node> nodes = new List<Node>();
        readonly Dictionary<int, int> nodeOfBody = new Dictionary<int, int>();
        readonly List<TycoonWaterStream> streams = new List<TycoonWaterStream>();
        readonly List<(int node, bool atEnd)> spills = new List<(int, bool)>();
        Material waterMaterial, foamMaterial;
        float nextSolve;

        public int NodeCount => nodes.Count;
        public int ActiveSpills => spills.Count;
        public IReadOnlyList<int> RiverBodies { get; private set; } = new List<int>();
        public IReadOnlyList<IReadOnlyList<int>> Lanes { get; private set; } = new List<IReadOnlyList<int>>();
        public int RiverPumpBody { get; private set; } = -1;

        public bool IsTracked(int body) => nodeOfBody.ContainsKey(body);
        public bool IsFed(int body) => nodeOfBody.TryGetValue(body, out var n) && nodes[n].fed;
        public bool IsIntact(int body) => nodeOfBody.TryGetValue(body, out var n) && nodes[n].intact;
        public bool IsWet(int body) => nodeOfBody.TryGetValue(body, out var n) && nodes[n].showingWet;
        public float Level(int body) => nodeOfBody.TryGetValue(body, out var n) ? nodes[n].level : 0;

        public static TycoonWater Build(Transform root, IReadOnlyList<Rigidbody> bodies, IReadOnlyList<CastlePlacement> layout)
        {
            var water = root.gameObject.AddComponent<TycoonWater>();
            water.Initialize(bodies, layout);
            return water;
        }

        void Initialize(IReadOnlyList<Rigidbody> bodies, IReadOnlyList<CastlePlacement> layout)
        {
            waterMaterial = Resources.Load<Material>("Tycoon Aqua");
            foamMaterial = Resources.Load<Material>("Tycoon Cream");
            int Add(int body, Kind kind, bool source)
            {
                if (nodeOfBody.TryGetValue(body, out var existing)) return existing;
                var t = bodies[body].transform;
                var node = new Node
                {
                    body = body, kind = kind, source = source, transform = t,
                    restPosition = transform.InverseTransformPoint(t.position),
                    restRotation = Quaternion.Inverse(transform.rotation) * t.rotation,
                    profile = layout[body].DeckProfile, size = t.localScale,
                };
                var visual = t.GetComponentInChildren<TycoonDeckVisual>();
                if (visual) node.deck = visual.GetComponent<MeshRenderer>();
                if (node.deck && kind is Kind.Slide or Kind.Basin && node.deck.sharedMaterials.Length > 1)
                {
                    node.wet = node.deck.sharedMaterials;
                    node.dry = (Material[])node.wet.Clone();
                    node.dry[1] = node.wet[0];
                }
                if (visual && kind == Kind.River)
                {
                    node.water = visual.transform.Find(TycoonDeckVisual.RiverWaterName);
                    if (node.water)
                    {
                        node.waterRest = node.water.localPosition;
                        node.waterDown = visual.transform.InverseTransformVector(Vector3.down);
                    }
                }
                nodes.Add(node);
                nodeOfBody[body] = nodes.Count - 1;
                return nodes.Count - 1;
            }
            void Link(int from, int to)
            {
                nodes[from].downstream.Add(to);
                if (nodes[from].next < 0) nodes[from].next = to;
                if (nodes[to].previous < 0) nodes[to].previous = from;
            }

            // Summit pump: the launch deck feeds the four gates; each gate feeds its slide.
            int launch = -1;
            var gates = new List<int>();
            var river = new List<int>();
            for (int i = 0; i < layout.Count; i++)
            {
                var finish = layout[i].DeckProfile.Finish;
                if (finish == TycoonDeckFinish.Launch) launch = Add(i, Kind.Pump, true);
                else if (finish == TycoonDeckFinish.Gate) gates.Add(Add(i, Kind.Tube, false));
                else if (finish is TycoonDeckFinish.Basin or TycoonDeckFinish.Splash) Add(i, Kind.Basin, false);
                else if (finish is TycoonDeckFinish.River or TycoonDeckFinish.RiverPump)
                {
                    river.Add(i);
                    Add(i, Kind.River, finish == TycoonDeckFinish.RiverPump);
                    if (finish == TycoonDeckFinish.RiverPump) RiverPumpBody = i;
                }
                else if (finish is TycoonDeckFinish.Bucket or TycoonDeckFinish.Wheel) Add(i, Kind.Fixture, true);
            }
            if (launch >= 0) foreach (var g in gates) Link(launch, g);

            // Slide runs are consecutive panels of one colour. The first four belong to the summit gates;
            // later runs (the raft slide) have a header tank on their first panel.
            var lanes = new List<IReadOnlyList<int>>();
            for (int i = 0; i < layout.Count; i++)
            {
                var finish = layout[i].DeckProfile.Finish;
                if (finish is not (TycoonDeckFinish.Flume or TycoonDeckFinish.Tube)) continue;
                var run = new List<int>();
                int j = i;
                while (j < layout.Count && layout[j].DeckProfile.Finish == finish && layout[j].Section == layout[i].Section) run.Add(j++);
                // The dry gold chute carries no water and keeps its gate slot.
                int laneIndex = lanes.Count + CountDryRunsBefore(layout, i);
                var kind = finish == TycoonDeckFinish.Flume ? Kind.Slide : Kind.Tube;
                int previous = laneIndex < gates.Count ? gates[laneIndex] : -1;
                for (int k = 0; k < run.Count; k++)
                {
                    int node = Add(run[k], kind, previous < 0 && k == 0);
                    if (previous >= 0) Link(previous, node);
                    previous = node;
                }
                lanes.Add(run);
                i = j - 1;
            }
            Lanes = lanes;

            // The lazy river is a loop: water can reach a panel from either side.
            for (int k = 0; k < river.Count; k++)
            {
                int a = nodeOfBody[river[k]], b = nodeOfBody[river[(k + 1) % river.Count]];
                nodes[a].downstream.Add(b); nodes[b].downstream.Add(a);
                nodes[a].next = b; nodes[b].previous = a;
            }
            RiverBodies = river;

            foreach (var bucket in GetComponentsInChildren<TycoonBucketMotion>(true))
                if (nodeOfBody.TryGetValue(BodyIndex(bodies, bucket.transform), out var n)) { var node = nodes[n]; bucket.SetSupply(() => node.fed); }
            foreach (var wheel in GetComponentsInChildren<TycoonWheelMotion>(true))
                if (nodeOfBody.TryGetValue(BodyIndex(bodies, wheel.transform), out var n)) { var node = nodes[n]; wheel.SetSupply(() => node.fed); }
            Solve();
        }

        static int CountDryRunsBefore(IReadOnlyList<CastlePlacement> layout, int index)
        {
            int dry = 0;
            for (int i = 0; i < index; i++)
                if (layout[i].DeckProfile.Finish == TycoonDeckFinish.DryChute && (i == 0 || layout[i - 1].DeckProfile.Finish != TycoonDeckFinish.DryChute)) dry++;
            return dry;
        }

        static int BodyIndex(IReadOnlyList<Rigidbody> bodies, Transform child)
        {
            var body = child.GetComponentInParent<Rigidbody>();
            for (int i = 0; i < bodies.Count; i++) if (bodies[i] == body) return i;
            return -1;
        }

        bool Intact(Node n)
        {
            if (!n.transform || !n.transform.gameObject.activeInHierarchy) return false;
            var position = transform.InverseTransformPoint(n.transform.position);
            var rotation = Quaternion.Inverse(transform.rotation) * n.transform.rotation;
            return (position - n.restPosition).sqrMagnitude < MoveTolerance * MoveTolerance &&
                   Quaternion.Angle(rotation, n.restRotation) < TurnTolerance;
        }

        static float Tilt(Node n) => n.transform ? Vector3.Angle(n.transform.up, Vector3.up) : 180;

        readonly Queue<int> frontier = new Queue<int>();

        // Recompute which pieces are intact and which are still connected to a working source.
        public void Solve()
        {
            foreach (var n in nodes) { n.intact = Intact(n); n.fed = false; }
            frontier.Clear();
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].source && nodes[i].intact) { nodes[i].fed = true; frontier.Enqueue(i); }
            while (frontier.Count > 0)
            {
                var n = nodes[frontier.Dequeue()];
                foreach (int d in n.downstream)
                {
                    var next = nodes[d];
                    if (next.fed || !next.intact) continue;
                    next.fed = true;
                    frontier.Enqueue(d);
                }
            }
            spills.Clear();
            for (int i = 0; i < nodes.Count && spills.Count < MaxSpills; i++)
            {
                var n = nodes[i];
                if (!n.fed) continue;
                if (n.kind is Kind.Slide or Kind.Tube && n.next >= 0 && !nodes[n.next].intact) spills.Add((i, true));
                if (n.kind == Kind.River && n.level > .3f)
                {
                    if (n.next >= 0 && !nodes[n.next].intact) spills.Add((i, true));
                    if (n.previous >= 0 && !nodes[n.previous].intact && spills.Count < MaxSpills) spills.Add((i, false));
                }
            }
            nextSolve = Time.time + SolveInterval;
        }

        void Update()
        {
            if (Time.time >= nextSolve) Solve();
            float dt = Time.deltaTime;
            foreach (var n in nodes)
            {
                switch (n.kind)
                {
                    case Kind.Slide:
                        n.level = Mathf.MoveTowards(n.level, n.fed && Tilt(n) < 35 ? 1 : 0, dt / 1.5f);
                        ShowWet(n, n.level > .5f);
                        break;
                    case Kind.Basin:
                        // A pool keeps its water while it stays upright.
                        n.level = Mathf.MoveTowards(n.level, Tilt(n) < 20 ? 1 : 0, dt * 4);
                        ShowWet(n, n.level > .5f);
                        break;
                    case Kind.River:
                        float target = n.fed && Tilt(n) < 15 ? 1 : 0;
                        if (Tilt(n) >= 15) n.level = 0;
                        n.level = Mathf.MoveTowards(n.level, target, dt * (target > n.level ? .5f : .1f));
                        n.showingWet = n.level > .04f;
                        if (n.water)
                        {
                            if (n.water.gameObject.activeSelf != n.showingWet) n.water.gameObject.SetActive(n.showingWet);
                            n.water.localPosition = n.waterRest + n.waterDown * ((1 - n.level) * TycoonDeckVisual.RiverWaterDepth);
                        }
                        break;
                }
            }
            for (int k = 0; k < streams.Count || k < spills.Count; k++)
            {
                if (k >= spills.Count) { streams[k].Hide(); continue; }
                if (k >= streams.Count) streams.Add(TycoonWaterStream.Create(transform, waterMaterial, foamMaterial));
                var (index, atEnd) = spills[k];
                var n = nodes[index];
                var p = n.profile;
                var mid = atEnd ? (p.EndLeft + p.EndRight) * .5f : (p.StartLeft + p.StartRight) * .5f;
                var other = atEnd ? (p.StartLeft + p.StartRight) * .5f : (p.EndLeft + p.EndRight) * .5f;
                float surface = n.kind == Kind.River ? TycoonDeckVisual.RiverWaterDepth * n.level : .05f;
                var lip = n.transform.TransformPoint(mid) + Vector3.up * surface;
                var outward = n.transform.TransformDirection(mid - other);
                float width = n.kind == Kind.River ? (n.size.x - 2 * TycoonDeckVisual.RiverWallThickness) * .8f
                    : n.kind == Kind.Tube ? n.size.x * .45f : n.size.x * .5f;
                streams[k].Show(lip, outward, width);
            }
        }

        void ShowWet(Node n, bool wet)
        {
            if (n.showingWet == wet) return;
            n.showingWet = wet;
            if (n.deck && n.wet != null) n.deck.sharedMaterials = wet ? n.wet : n.dry;
        }
    }
}
