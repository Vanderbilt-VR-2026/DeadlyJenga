using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Works out how much weight every block is holding up, straight from the physics contacts.
    // Overloaded blocks crack, then snap. Hard hits break blocks too. Only the host simulates;
    // copies on other machines just notice when a block lands in the graveyard.
    public sealed class BlockStressEngine : MonoBehaviour
    {
        public static BlockStressEngine Instance { get; private set; }

        [Header("Weight")]
        public float safetyMargin = 2f;      // holds this many times the load it settled under
        public float minimumCapacity = 4f;   // and never less than this many of its own weights
        public float crackAt = .8f;
        public float breakAfter = .6f;       // seconds over 100% before it snaps
        [Header("Impacts")]
        public float impactBreakSpeed = 12f; // velocity change from one hit that shatters a block
        public float settleTime = 3f;        // learn resting loads, no breaking, right after a build
        public bool breakingEnabled = true;

        public int BrokenCount { get; private set; }

        IReadOnlyList<Rigidbody> source;
        Rigidbody first;
        int sourceCount;
        CastleBlock[] blocks = new CastleBlock[0];
        Rigidbody[] bodies = new Rigidbody[0];
        readonly Dictionary<Component, int> indexOf = new Dictionary<Component, int>();
        float[] support = new float[0], baseline = new float[0], overload = new float[0], hit = new float[0];
        bool[] cracked = new bool[0];
        Vector3[] lastPosition = new Vector3[0];
        float builtAt, quietUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance) return;
            var go = new GameObject("Block Physics");
            DontDestroyOnLoad(go);
            go.AddComponent<BlockStressEngine>();
            go.AddComponent<BlockTimeline>();
        }

        void Awake() => Instance = this;
        void OnEnable() { Physics.ContactEvent += OnContacts; GameEvents.RewindEnded += AfterRewind; }
        void OnDisable() { Physics.ContactEvent -= OnContacts; GameEvents.RewindEnded -= AfterRewind; }
        void AfterRewind() => quietUntil = Time.time + 1;

        void Refresh()
        {
            var list = Services.World?.Blocks;
            int n = list?.Count ?? 0;
            if (list == source && n == sourceCount && (n == 0 || list[0] == first)) return;
            source = list; sourceCount = n; first = n > 0 ? list[0] : null;
            blocks = new CastleBlock[n]; bodies = new Rigidbody[n];
            support = new float[n]; baseline = new float[n]; overload = new float[n]; hit = new float[n];
            cracked = new bool[n]; lastPosition = new Vector3[n];
            indexOf.Clear(); BrokenCount = 0;
            for (int i = 0; i < n; i++)
            {
                var body = list[i];
                if (!body) continue;
                bodies[i] = body;
                blocks[i] = body.GetComponent<CastleBlock>();
                indexOf[body] = i;
                foreach (var col in body.GetComponents<Collider>()) col.providesContacts = true;
                lastPosition[i] = body.position;
            }
            builtAt = Time.time;
        }

        // Runs after every physics step with all the contacts that step produced.
        void OnContacts(PhysicsScene scene, NativeArray<ContactPairHeader>.ReadOnly headers)
        {
            if (bodies.Length == 0) return;
            float dt = Time.fixedDeltaTime;
            for (int h = 0; h < headers.Length; h++)
            {
                var header = headers[h];
                int a = Index(header.body), b = Index(header.otherBody);
                if (a < 0 && b < 0) continue;
                for (int p = 0; p < header.pairCount; p++)
                {
                    var pair = header.GetContactPair(p);
                    if (pair.isCollisionExit || pair.contactCount == 0) continue;
                    var impulse = pair.impulseSum;
                    float contactY = pair.GetContactPoint(0).position.y;
                    if (a >= 0) Add(a, impulse, contactY, dt, pair.isCollisionEnter);
                    if (b >= 0) Add(b, impulse, contactY, dt, pair.isCollisionEnter);
                }
            }
        }

        int Index(Component body) => body && indexOf.TryGetValue(body, out int i) ? i : -1;

        void Add(int i, Vector3 impulse, float contactY, float dt, bool enter)
        {
            var body = bodies[i];
            if (!body) return;
            // A contact below the middle of the block is something holding it up.
            if (contactY < body.worldCenterOfMass.y) support[i] += Mathf.Abs(impulse.y) / dt;
            if (enter) hit[i] = Mathf.Max(hit[i], impulse.magnitude / Mathf.Max(.01f, body.mass));
        }

        void FixedUpdate()
        {
            Refresh();
            if (blocks.Length == 0) return;
            float dt = Time.fixedDeltaTime, gravity = Physics.gravity.magnitude;
            float age = Time.time - builtAt;
            bool calibrating = age < settleTime;
            bool quiet = calibrating || Time.time < quietUntil;
            float blend = 1 - Mathf.Exp(-dt * 6);
            int broken = 0;

            for (int i = 0; i < blocks.Length; i++)
            {
                var block = blocks[i]; var body = bodies[i];
                if (!block || !body) continue;
                var position = body.position;
                bool down = CastleBlock.InGraveyard(position);

                if (body.isKinematic)
                {
                    // A copy of the host's world (or a rewind): follow what the poses say.
                    if (down && !block.Broken) block.MarkBroken(lastPosition[i], Vector3.zero);
                    else if (!down && block.Broken) block.Restore();
                    if (!down) lastPosition[i] = position;
                    if (block.Broken) broken++;
                    support[i] = 0; hit[i] = 0;
                    continue;
                }

                if (block.Broken)
                {
                    if (down) { broken++; support[i] = 0; hit[i] = 0; continue; }
                    block.Restore();
                }
                lastPosition[i] = position;

                float weight = Mathf.Max(.001f, body.mass * gravity);
                if (!body.IsSleeping())
                {
                    float load = support[i] / weight; // measured in "own weights"
                    if (calibrating) { if (age > .3f) baseline[i] = baseline[i] <= 0 ? load : Mathf.Lerp(baseline[i], load, .05f); }
                    else if (baseline[i] <= 0) baseline[i] = Mathf.Max(1, load);
                    float capacity = Mathf.Max(minimumCapacity, baseline[i] * safetyMargin);
                    block.SetStress(Mathf.Lerp(block.Stress, load / capacity, blend));
                }

                if (!quiet && breakingEnabled)
                {
                    float s = block.Stress;
                    if (s >= crackAt && !cracked[i]) { cracked[i] = true; GameEvents.RaiseBlockCracked(block.gameObject, s); }
                    else if (s < crackAt * .8f) cracked[i] = false;
                    overload[i] = s >= 1 ? overload[i] + dt : Mathf.Max(0, overload[i] - dt);
                    if (overload[i] >= breakAfter || hit[i] >= impactBreakSpeed)
                    {
                        overload[i] = 0; cracked[i] = false;
                        block.Break(gameObject);
                        broken++;
                    }
                }
                support[i] = 0; hit[i] = 0;
            }
            BrokenCount = broken;
        }
    }
}
