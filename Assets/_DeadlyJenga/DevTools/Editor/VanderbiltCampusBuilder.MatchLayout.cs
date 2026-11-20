using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    // Writes GameSession/Modes/Resources/MapMatchLayouts/Campus Match Layout.asset
    // (pattern: ChristmasTownTools.AuthorMatchFoundation). Positions are map-root local.
    public static partial class VanderbiltCampusBuilder
    {
        const string MatchLayoutName = "Campus Match Layout";

        static void AuthorMatchLayout(VanderbiltCampusMap map, VanderbiltCampusData data, BuildStats stats)
        {
            Physics.SyncTransforms();
            string modes = ProjectPath.FolderOf("MapMatchLayout.cs").TrimEnd('/');
            string path = Folder(Folder(modes, "Resources"), "MapMatchLayouts") + "/" + MatchLayoutName + ".asset";
            var layout = AssetDatabase.LoadAssetAtPath<MapMatchLayout>(path);
            if (!layout) { layout = ScriptableObject.CreateInstance<MapMatchLayout>(); AssetDatabase.CreateAsset(layout, path); }

            // The signature must equal the map's SaveLayout(), or match modes ignore this layout.
            layout.layoutSignature = map.SaveLayout();
            layout.groundSpawns = true;   // authored points snap onto the static ground at runtime
            layout.playOpeningOnly = false;

            var free = new List<Vector3>();
            foreach (var spawn in map.spawns) if (spawn) free.Add(map.transform.InverseTransformPoint(spawn.position));
            layout.freeSpawns = free.Distinct().ToArray();

            layout.teams = data.teams.Select(t => new MapMatchLayout.TeamArea
            {
                name = t.name, flagHome = t.flag, spawns = t.spawns.ToArray()
            }).ToArray();
            layout.hill = data.hasHill ? data.hill : free.Count > 0 ? free[0] : map.localArea.center;
            EditorUtility.SetDirty(layout);

            // Report authored points that would not find ground or are inside something.
            int blocked = 0;
            foreach (var p in layout.freeSpawns.Concat(layout.teams.SelectMany(t => t.spawns)))
                if (!OpenSpawn(map.transform.TransformPoint(p))) blocked++;
            stats.notes.Add($"Match layout: {layout.freeSpawns.Length} free spawns, {layout.teams.Length} teams, signature {layout.layoutSignature}.");
            if (blocked > 0) stats.Problem(blocked + " match spawn points have no ground within 2 m or are blocked.");
        }

        // Same test the runtime uses (MapMatchLayout.TrySpawnPosition) plus a standing-room check.
        static bool OpenSpawn(Vector3 p)
        {
            int mask = (1 << Layers.Surface) | 1;
            return Physics.Raycast(p + Vector3.up * .25f, Vector3.down, 2f, mask, QueryTriggerInteraction.Ignore) &&
                !Physics.CheckCapsule(p + Vector3.up * .5f, p + Vector3.up * 1.65f, .32f, mask, QueryTriggerInteraction.Ignore);
        }
    }
}
