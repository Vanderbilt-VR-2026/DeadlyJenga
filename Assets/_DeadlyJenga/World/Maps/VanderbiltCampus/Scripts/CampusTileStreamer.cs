using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Local tile streaming for the campus (Play Mode only; does nothing in the editor).
    // A tile far from every character (more than streamDistance) shows only its far level (HLOD, or the LoD1
    // meshes when there is no HLOD, which keeps the backdrop ring visible) and switches its colliders off.
    // Tiles touching the play area always keep their colliders, so towers and bots never lose their ground,
    // and so does every tile near any character (players, remote players, bots).
    // Streaming is local only: it never changes the map's layout token or anything networked.
    [DisallowMultipleComponent]
    public sealed class CampusTileStreamer : MonoBehaviour
    {
        [Min(50)] public float streamDistance = 300;
        [Tooltip("Tiles this close to the play area edge always stay fully loaded.")]
        [Min(0)] public float playAreaMargin = 30;
        [Min(.1f)] public float checkSeconds = .5f;
        public bool streamingEnabled = true;

        sealed class Tile
        {
            public Transform root;
            public LODGroup lods;
            public GameObject colliders;
            public Rect area;       // map-local x/z rectangle
            public bool near = true;
        }

        readonly List<Tile> tiles = new List<Tile>();
        readonly List<Vector3> watchers = new List<Vector3>();
        VanderbiltCampusMap map;
        float nextCheck;

        public int NearTiles { get; private set; }
        public int FarTiles { get; private set; }

        void Start()
        {
            map = GetComponent<VanderbiltCampusMap>();
            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith("T_")) continue;
                var tile = new Tile { root = child, lods = child.GetComponent<LODGroup>() };
                var colliders = child.Find("Colliders"); if (colliders) tile.colliders = colliders.gameObject;
                var bounds = new Bounds(); bool any = false;
                foreach (var r in child.GetComponentsInChildren<Renderer>(true))
                {
                    var b = new Bounds(transform.InverseTransformPoint(r.bounds.center), r.bounds.size);
                    if (!any) { bounds = b; any = true; } else bounds.Encapsulate(b);
                }
                if (!any) continue;
                tile.area = new Rect(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z);
                tiles.Add(tile);
            }
        }

        void OnDisable()
        {
            foreach (var tile in tiles) Apply(tile, true);   // leave everything loaded when streaming stops
        }

        void Update()
        {
            if (!Application.isPlaying || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + checkSeconds;
            Watchers();
            int near = 0;
            foreach (var tile in tiles)
            {
                bool keep = !streamingEnabled || TouchesPlayArea(tile) || NearWatcher(tile);
                if (keep != tile.near) Apply(tile, keep);
                if (keep) near++;
            }
            NearTiles = near; FarTiles = tiles.Count - near;
        }

        // Everyone whose ground matters: the camera and every walking character.
        void Watchers()
        {
            watchers.Clear();
            var camera = Services.Session?.ActiveCamera;
            if (camera) watchers.Add(transform.InverseTransformPoint(camera.transform.position));
            foreach (var mover in Tracked.Movers)
                if (mover is Component c && c) watchers.Add(transform.InverseTransformPoint(c.transform.position));
        }

        bool NearWatcher(Tile tile)
        {
            foreach (var p in watchers) if (Distance(tile.area, p) <= streamDistance) return true;
            return false;
        }

        bool TouchesPlayArea(Tile tile) =>
            map && Distance(tile.area, map.playAreaCenter) <= map.playAreaRadius + playAreaMargin;

        // Ground distance from a point to a tile rectangle (0 inside it).
        static float Distance(Rect area, Vector3 p)
        {
            float dx = Mathf.Max(area.xMin - p.x, 0, p.x - area.xMax), dz = Mathf.Max(area.yMin - p.z, 0, p.z - area.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static void Apply(Tile tile, bool near)
        {
            tile.near = near;
            if (tile.colliders) tile.colliders.SetActive(near);
            if (tile.lods)
            {
                int far = tile.lods.lodCount - 1;
                tile.lods.ForceLOD(near ? -1 : far);   // -1 = normal LOD selection
            }
        }
    }
}
