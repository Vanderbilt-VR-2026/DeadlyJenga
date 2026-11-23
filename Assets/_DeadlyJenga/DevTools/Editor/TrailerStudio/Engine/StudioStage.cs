using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The running scene the Studio works on: the session, the map and who stands where.
    // It only exists in Play Mode. Track types read what they need from here.
    public sealed class StudioStage
    {
        public CastlePlaySession session;
        public IWorldQuery world;
        public StudioSet set;
        public StudioTape tape;
        public Vector3 anchor;                    // world position of the stage; marks are relative to it
        public Rigidbody[] blocks = new Rigidbody[0];
        public bool physicalPlayback;             // true while something live shares the scene with playback
        public bool recording;
        public string performer = "";             // mark id the local player is playing right now (empty = nobody)
        internal AvatarAppearance? localWardrobe;

        // Live characters by mark id (bots, or the local player while performing).
        public readonly Dictionary<string, GameObject> live = new Dictionary<string, GameObject>();

        // Played-back actors by mark id (the objects cameras and overlays can follow).
        public readonly Dictionary<string, GameObject> playback = new Dictionary<string, GameObject>();
        internal GameObject starredGhost;

        public int Fps => set.fps;
        public int Frames => set.Frames;
        public ISyncedWorld Synced => world as ISyncedWorld;
        public Vector3 ToWorld(Vector3 stagePoint) => anchor + stagePoint;
        public Vector3 ToStage(Vector3 worldPoint) => worldPoint - anchor;

        public GameObject Live(string markId) => live.TryGetValue(markId, out var found) && found ? found : null;

        public void RefreshBlocks() => blocks = world.Blocks.ToArray();

        // Same rule as Gameplay Theatre: the stage sits on the highest block near the middle of the map.
        public static Vector3 FindAnchor(IWorldQuery world, StudioSet set)
        {
            if (set.useMapAnchor) return world.Center + set.mapAnchor + set.stageOffset;
            var candidates = world.Blocks.Where(b => b && b.gameObject.activeInHierarchy)
                .OrderByDescending(b => b.position.y).Take(32)
                .OrderBy(b => Vector3.ProjectOnPlane(b.position - world.Center, Vector3.up).sqrMagnitude);
            foreach (var block in candidates)
            {
                var colliders = block.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger);
                float top = colliders.Select(c => c.bounds.max.y).DefaultIfEmpty(block.position.y).Max();
                var point = new Vector3(block.position.x, top, block.position.z);
                if (SurfaceBelow(point, out var hit) && Mathf.Abs(hit.y - top) <= .75f &&
                    hit.y > world.WaterHeight(hit) + .05f)
                    return point + Vector3.up * .025f + set.stageOffset;
            }
            return world.SafeSpawnPoint() + set.stageOffset;
        }

        // The surface under a point, for snapping marks. False when there is nothing below (open water).
        public static bool SurfaceBelow(Vector3 point, out Vector3 surface)
        {
            int mask = (1 << Layers.Block) | (1 << Layers.Surface) | 1;
            if (Physics.Raycast(point + Vector3.up * 2.5f, Vector3.down, out var hit, 400, mask, QueryTriggerInteraction.Ignore))
            {
                surface = hit.point;
                return true;
            }
            surface = point;
            return false;
        }

        // For the crowd tool: the ground height under a stage-space spot, or null over a drop.
        public float? GroundAt(Vector3 stagePoint)
        {
            Vector3 point = ToWorld(stagePoint);
            int mask = (1 << Layers.Block) | (1 << Layers.Surface) | 1;
            if (!Physics.Raycast(point + Vector3.up * 1.2f, Vector3.down, out var hit, 3f, mask, QueryTriggerInteraction.Ignore)) return null;
            return ToStage(hit.point).y;
        }

        // Plain-language warning for a mark's spot, or empty when it is fine.
        public string MarkWarning(StudioMark mark)
        {
            if (mark.role == StudioRole.Hand) return "";
            Vector3 feet = ToWorld(mark.position);
            int mask = (1 << Layers.Block) | (1 << Layers.Surface);
            if (Physics.CheckCapsule(feet + Vector3.up * .45f, feet + Vector3.up * 1.35f, .2f, mask, QueryTriggerInteraction.Ignore))
                return "Inside a block";
            if (!SurfaceBelow(feet, out var surface)) return "Nothing to stand on";
            if (surface.y <= world.WaterHeight(feet) + .05f) return "Over water";
            if (feet.y - surface.y > .6f) return "Floating above the surface";
            return "";
        }
    }
}
