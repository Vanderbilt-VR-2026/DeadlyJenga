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

        // Spawns, team areas and the hill come from the resolved gameplay (gameplay.json or the default),
        // so every point is inside the play area. Positions are map-root local.
        static void AuthorMatchLayout(VanderbiltCampusMap map, CampusPlay play, BuildStats stats)
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
            layout.freeSpawns = play.spawns.Select(s => s.pos).Distinct().ToArray();
            layout.teams = play.teams.Select(t => new MapMatchLayout.TeamArea { name = t.name, flagHome = t.flag, spawns = t.spawns.ToArray() }).ToArray();
            layout.hill = play.hasHill ? play.hill : layout.freeSpawns.Length > 0 ? layout.freeSpawns[0] : map.playAreaCenter;
            EditorUtility.SetDirty(layout);

            stats.notes.Add($"Match layout: {layout.freeSpawns.Length} free spawns, {layout.teams.Length} teams " +
                $"({string.Join(", ", layout.teams.Select(t => t.name + " " + t.spawns.Length))}), hill {layout.hill:F1}, signature {layout.layoutSignature}.");
            if (layout.freeSpawns.Length < 4) stats.Problem("Fewer than 4 usable free spawns inside the play area.");
            foreach (var team in layout.teams) if (team.spawns.Length < 3) stats.Problem("Team area " + team.name + " has fewer than 3 usable spawns.");
        }

        // Null when the point is fine; otherwise why not. Matches MapMatchLayout.TrySpawnPosition
        // (ray from 0.25 m above, 2 m down, Surface + Default layers, ground normal y >= 0.9) plus a standing-room capsule.
        static string SpawnProblem(Vector3 p)
        {
            int mask = (1 << Layers.Surface) | 1;
            if (!Physics.Raycast(p + Vector3.up * .25f, Vector3.down, out var hit, 2f, mask, QueryTriggerInteraction.Ignore))
            {
                if (Physics.Raycast(p + Vector3.up * 300, Vector3.down, out var far, 600, mask, QueryTriggerInteraction.Ignore))
                    return $"no ground within 2 m; nearest surface below 300 m up is '{far.collider.name}' at y={far.point.y:F2} ({far.point.y - p.y:+0.00;-0.00} m from the point)";
                return "no collider anywhere above or below this point (outside the tiles, or a ring tile without COL_Terrain)";
            }
            if (hit.normal.y < .9f) return $"ground too steep under it ('{hit.collider.name}', normal y {hit.normal.y:F2}; the runtime needs >= 0.90)";
            var blockers = Physics.OverlapCapsule(p + Vector3.up * .5f, p + Vector3.up * 1.65f, .32f, mask, QueryTriggerInteraction.Ignore);
            if (blockers.Length > 0)
                return "blocked by " + string.Join(", ", blockers.Select(b => "'" + (b.transform.parent ? b.transform.parent.name + "/" : "") + b.name + "'").Distinct().Take(4));
            return null;
        }
    }
}
