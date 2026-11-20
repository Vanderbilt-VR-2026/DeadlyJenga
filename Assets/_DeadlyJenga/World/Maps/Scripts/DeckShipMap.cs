using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Deck Ship (work in progress, special game mode map).
    // A solid toy ship rocks on the sea. The ship never breaks; everything on its deck is loose physics
    // blocks ("cargo"), and the cargo can be swapped for another layout at any time (DeckShipCargo).
    // Ship events (big rolls, pitching, an iceberg hit) move the hull, so the cargo slides, topples and
    // goes overboard. Players ride the deck; anyone who falls into the sea drowns.
    // The hull is Blocks[0]: a kinematic body, so multiplayer copies follow its pose like any other block.
    public sealed partial class DeckShipMap : MonoBehaviour, IWorldQuery, ISyncedWorld, IRebuildable
    {
        [Header("Cargo")]
        public GameObject blockPrefab;
        [Tooltip("Block colors: red, green, purple, yellow.")]
        public Material[] blockMaterials;
        public DeckShipCargoKind cargo = DeckShipCargoKind.TowerAndForts;
        [Tooltip("The reset button picks a random cargo layout.")]
        public bool randomCargoOnRebuild;
        public int seed = 1;
        public float dropHeight = 2.5f;

        [Header("Ship")]
        [Tooltip("Hull colors are copies of this material with new colors.")]
        public Material hullSource;
        [Tooltip("Low deck friction lets cargo slide when the ship leans.")]
        public float deckFriction = .2f;
        public float waterHeight, killHeight = -8;
        [Min(1)] public int solverIterations = 16, solverVelocityIterations = 6;
        public bool generateOnStart = true;

        readonly List<Rigidbody> bodies = new List<Rigidbody>();
        readonly Dictionary<Rigidbody, float> submerged = new Dictionary<Rigidbody, float>();
        readonly Collider[] clearance = new Collider[16];
        Transform cargoRoot;
        bool replica;
        float nextCleanup;
        int spawnTurn;

        public IReadOnlyList<Rigidbody> Blocks => bodies;
        public int CargoBlocks => Mathf.Max(0, bodies.Count - 1);
        public int Overboard { get; private set; }
        public Transform Root => transform;
        public int Revision { get; private set; }
        public Vector3 Center => transform.position;
        public float TopHeight => transform.position.y + 16;
        public float Radius => 40;
        public float StepHeight => DeckShipCargo.Height;
        public Bounds PlayableBounds => new Bounds(transform.position + Vector3.up * 12, new Vector3(110, 32, 110));

        void OnEnable() { Services.World = this; }
        void OnDisable() { if (ReferenceEquals(Services.World, this)) Services.World = null; }
        void Start() { if (generateOnStart && bodies.Count == 0) Rebuild(); }

        public float WaterHeight(Vector3 position) => waterHeight;
        public bool IsSheltered(Vector3 position) =>
            Physics.Raycast(position + Vector3.up * .1f, Vector3.up, 60, (1 << Layers.Block) | 1, QueryTriggerInteraction.Ignore);

        public void Rebuild() => Rebuild(randomCargoOnRebuild && Application.isPlaying);

        void Rebuild(bool randomize)
        {
            if (!blockPrefab || !blockPrefab.GetComponent<CastleBlock>() || !blockPrefab.GetComponent<Rigidbody>())
                throw new InvalidOperationException("Deck Ship needs the ordinary physical block prefab.");
            if (randomize) { cargo = (DeckShipCargoKind)UnityEngine.Random.Range(0, DeckShipCargo.Count); seed = UnityEngine.Random.Range(1, 100000); }
            if (!hullBody) BuildHull();
            ResetMotion();
            ClearCargo();
            SpawnCargo(0);
            Revision++;
        }

        // Clears the deck and puts a different cargo layout on it. drop = it falls in from above.
        public void SwapCargo(DeckShipCargoKind kind, bool drop)
        {
            cargo = kind; seed++;
            ClearCargo();
            SpawnCargo(drop ? dropHeight : 0);
            Revision++;
        }

        public void NextCargo(bool drop) => SwapCargo((DeckShipCargoKind)(((int)cargo + 1) % DeckShipCargo.Count), drop);

        void ClearCargo()
        {
            if (cargoRoot)
            {
                cargoRoot.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(cargoRoot.gameObject); else DestroyImmediate(cargoRoot.gameObject);
            }
            if (bodies.Count > 1) bodies.RemoveRange(1, bodies.Count - 1);
            submerged.Clear();
            Overboard = 0;
        }

        void SpawnCargo(float lift)
        {
            // Cargo is not parented to the moving hull: dynamic bodies must not ride a moving transform.
            cargoRoot = new GameObject("Cargo: " + DeckShipCargo.Titles[(int)cargo]).transform;
            cargoRoot.SetParent(transform, false);
            var shipPosition = hullBody.position; var shipRotation = hullBody.rotation;
            var deck = Vector3.up * DeckHeight;
            var friction = blockPrefab.GetComponent<BoxCollider>().sharedMaterial;
            foreach (var piece in DeckShipCargo.Build(cargo, seed))
            {
                var go = Instantiate(blockPrefab, cargoRoot);
                go.name = (bodies.Count) + " · " + piece.role;
                var position = shipPosition + shipRotation * (deck + piece.position + Vector3.up * lift);
                var rotation = shipRotation * Quaternion.Euler(0, piece.yaw, 0);
                go.transform.SetPositionAndRotation(position, rotation);
                go.transform.localScale = piece.size;
                var block = go.GetComponent<CastleBlock>();
                if (blockMaterials != null && blockMaterials.Length > 0) block.sectionMaterial = blockMaterials[Mathf.Abs(piece.color) % blockMaterials.Length];
                block.RefreshAppearance();
                var body = go.GetComponent<Rigidbody>();
                body.position = position; body.rotation = rotation;
                body.mass = Mathf.Max(1, 40 * piece.size.x * piece.size.y * piece.size.z / 24);
                body.solverIterations = solverIterations; body.solverVelocityIterations = solverVelocityIterations;
                body.useGravity = true; body.isKinematic = replica; body.detectCollisions = true;
                body.interpolation = replica ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
                if (replica) body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                if (friction) go.GetComponent<BoxCollider>().sharedMaterial = friction;
                block.SyncSurface();
                bodies.Add(body);
            }
            Physics.SyncTransforms();
        }

        // Spawns rotate between open spots on the raised decks and the aft main deck.
        static readonly Vector3[] SpawnSpots =
        {
            new Vector3(0, 4, -25.5f), new Vector3(-3.5f, 4, -26), new Vector3(3.5f, 4, -26), new Vector3(-7.5f, 4, -27.5f), new Vector3(7.5f, 4, -27.5f),
            new Vector3(0, 4, 26), new Vector3(-5, 4, 27), new Vector3(5, 4, 27),
            new Vector3(-8.5f, 0, -19), new Vector3(8.5f, 0, -19), new Vector3(-8.5f, 0, 20), new Vector3(8.5f, 0, 20),
        };

        public Vector3 SafeSpawnPoint()
        {
            if (!hullBody) return transform.position + Vector3.up * (DeckHeight + 1);
            Physics.SyncTransforms();
            for (int i = 0; i < SpawnSpots.Length; i++)
            {
                var local = SpawnSpots[(spawnTurn + i) % SpawnSpots.Length] + Vector3.up * (DeckHeight + .05f);
                var p = hullBody.position + hullBody.rotation * local;
                if (Physics.OverlapCapsuleNonAlloc(p + Vector3.up * .2f, p + Vector3.up * .74f, .18f, clearance, (1 << Layers.Block) | (1 << Layers.Surface) | 1, QueryTriggerInteraction.Ignore) > 0) continue;
                spawnTurn = (spawnTurn + i + 1) % SpawnSpots.Length;
                return p;
            }
            return hullBody.position + hullBody.rotation * (SpawnSpots[0] + Vector3.up * (DeckHeight + .05f));
        }

        void FixedUpdate()
        {
            if (!hullBody) return;
            if (!replica) Step(Time.fixedDeltaTime);
            if (replica || Time.time < nextCleanup) return;
            nextCleanup = Time.time + .25f;
            CleanUpCargo(Time.time);
        }

        // Blocks that sink stay in the water for two seconds, then disappear; anything far below is removed.
        public void CleanUpCargo(float now)
        {
            for (int i = 1; i < bodies.Count; i++)
            {
                var body = bodies[i];
                if (!body || !body.gameObject.activeSelf) continue;
                if (CastleBlock.InGraveyard(body.position)) continue;   // broken blocks are parked far below on purpose
                float top = body.GetComponent<BoxCollider>().bounds.max.y;
                if (body.position.y < killHeight) { body.gameObject.SetActive(false); Overboard++; continue; }
                if (top > waterHeight - .15f) { submerged.Remove(body); continue; }
                if (!submerged.TryGetValue(body, out var began)) { submerged[body] = now; continue; }
                if (now - began >= 2) { body.gameObject.SetActive(false); submerged.Remove(body); Overboard++; }
            }
        }

        [Serializable] sealed class LayoutState { public int version = 1; public int cargo; public int seed; }
        const string Prefix = "deck-ship-v1:";
        public string SaveLayout() => Prefix + JsonUtility.ToJson(new LayoutState { cargo = (int)cargo, seed = seed });

        public bool LoadLayout(string layout, int expectedBlocks, Pose origin, Vector3 scale)
        {
            if (layout == null || !layout.StartsWith(Prefix, StringComparison.Ordinal) || !Finite(origin.position) || !Finite(scale) || scale.x <= 0) return false;
            LayoutState state;
            try { state = JsonUtility.FromJson<LayoutState>(layout.Substring(Prefix.Length)); } catch (ArgumentException) { return false; }
            if (state == null || state.version != 1 || !Enum.IsDefined(typeof(DeckShipCargoKind), state.cargo)) return false;
            var kind = (DeckShipCargoKind)state.cargo;
            if (DeckShipCargo.Build(kind, state.seed).Count + 1 != expectedBlocks) return false;
            cargo = kind; seed = state.seed;
            transform.SetPositionAndRotation(origin.position, origin.rotation); transform.localScale = scale;
            Rebuild(false);
            return true;
        }

        static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        public void SetReplica(bool value)
        {
            replica = value;
            for (int i = 1; i < bodies.Count; i++)
            {
                var body = bodies[i];
                if (!body) continue;
                if (value) body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                body.isKinematic = value;
                body.interpolation = value ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
            }
            // Copies get the hull pose from the host; the host alone runs ship events.
            if (hullBody) hullBody.interpolation = value ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        }

        void OnDestroy() { DestroyHullAssets(); }
    }
}
