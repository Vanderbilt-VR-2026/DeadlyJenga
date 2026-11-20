using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    // Gameplay on the campus: play area, Jenga towers, spawns, team areas and the hill objective.
    // Source: Tools/VanderbiltCampus/config/gameplay.json (campus x/y meters); without it a default is generated
    // (towers on the flattest open lawn cells inside the play area). Ground heights come from COL_Terrain.
    public static partial class VanderbiltCampusBuilder
    {
        // Default play area: Alumni Lawn (the campus origin), 150 m radius.
        static readonly Vector2 DefaultPlayCenter = Vector2.zero;
        const float DefaultPlayRadius = 150;

        // Resolved gameplay positions in map-local Unity coordinates (ground height included).
        sealed class CampusPlay
        {
            public readonly List<(string name, Vector3 pos)> spawns = new List<(string, Vector3)>();
            public readonly List<(string name, Vector3 flag, List<Vector3> spawns)> teams = new List<(string, Vector3, List<Vector3>)>();
            public Vector3 hill;
            public bool hasHill;
        }

        static CampusPlay ApplyGameplay(VanderbiltCampusMap map, VanderbiltCampusData data, BuildStats stats)
        {
            Physics.SyncTransforms();
            var g = VanderbiltCampusGameplay.Load();
            foreach (var p in g.problems) stats.Problem(p);
            stats.notes.Add(g.found ? "Gameplay from " + g.path : "gameplay.json not found: generated default gameplay (flattest open lawn cells).");
            var play = new CampusPlay();

            // Play area.
            var center2 = g.hasPlayArea ? g.playCenter : DefaultPlayCenter;
            map.playAreaRadius = g.found ? g.playRadius : DefaultPlayRadius;
            if (!GroundAt(map.transform, center2, out float centerY)) { stats.Problem($"No COL_Terrain under the play-area center {center2}."); centerY = map.lowestGround; }
            map.playAreaCenter = new Vector3(center2.x, centerY, center2.y);
            map.playAreaTop = PlayAreaTop(map);

            AssignBlockAssets(map, stats);
            PlaceTowers(map, g.found && g.towers.Count > 0 ? g.towers : DefaultTowers(map), stats);

            // Spawns (free for all).
            var spawnSource = g.found && g.spawns.Count > 0 ? g.spawns : DefaultSpawns(map, data);
            foreach (var s in spawnSource)
                if (TryPoint(map, s.xy, "spawn " + s.name, stats, out var p)) play.spawns.Add((s.name, p));
            // Team areas (CTF flags and team spawns).
            var teamSource = g.found && g.teams.Count > 0 ? g.teams : DefaultTeams(map);
            foreach (var t in teamSource)
            {
                if (!TryPoint(map, t.flag, "flag of " + t.name, stats, out var flag)) continue;
                var spawns = new List<Vector3>();
                foreach (var xy in t.spawns) if (TryPoint(map, xy, "team " + t.name + " spawn", stats, out var p)) spawns.Add(p);
                play.teams.Add((t.name, flag, spawns));
            }
            if (play.teams.Count is > 0 and < 2) stats.Problem("Capture the Flag needs at least two team areas inside the play area.");
            // Hill objective (King of the Hill).
            var hill2 = g.hasHill ? g.hill : center2;
            if (TryPoint(map, hill2, "hill", stats, out var hill)) { play.hill = hill; play.hasHill = true; }

            // The map's own spawns (SafeSpawnPoint) are the free-for-all spawns.
            if (play.spawns.Count > 0)
            {
                var group = new GameObject("Spawns (gameplay)").transform; group.SetParent(map.transform, false);
                map.spawns = play.spawns.Select(s => { var t = new GameObject(s.name).transform; t.SetParent(group, false); t.localPosition = s.pos; return t; }).ToArray();
            }
            stats.notes.Add($"Play area center {map.playAreaCenter:F1} radius {map.playAreaRadius} m top {map.playAreaTop:F1}; towers {map.towers.Length} ({map.ExpectedBlockCount()} blocks); " +
                $"spawns {play.spawns.Count}; team areas {play.teams.Count}; hill {(play.hasHill ? play.hill.ToString("F1") : "none")}");
            return play;
        }

        // Highest COL_Terrain surface at a campus x/y (roofs, trees and towers are ignored).
        internal static bool GroundAt(Transform root, Vector2 xy, out float y)
        {
            y = 0; bool found = false;
            var from = root.TransformPoint(new Vector3(xy.x, 2000, xy.y));
            foreach (var hit in Physics.RaycastAll(from, Vector3.down, 4000, 1 << Layers.Surface, QueryTriggerInteraction.Ignore))
            {
                if (CleanName(hit.collider.name) != "COL_Terrain") continue;
                float local = root.InverseTransformPoint(hit.point).y;
                if (!found || local > y) { y = local; found = true; }
            }
            return found;
        }

        // A ground point inside the play area, clear of colliders and tower plinths; reported when unusable.
        static bool TryPoint(VanderbiltCampusMap map, Vector2 xy, string what, BuildStats stats, out Vector3 point)
        {
            point = default;
            if ((xy - new Vector2(map.playAreaCenter.x, map.playAreaCenter.z)).magnitude > map.playAreaRadius)
            { stats.notes.Add($"GAMEPLAY {what} at {xy} is outside the play area; skipped."); return false; }
            if (!GroundAt(map.transform, xy, out float y)) { stats.notes.Add($"GAMEPLAY {what} at {xy}: no COL_Terrain; skipped."); return false; }
            var p = new Vector3(xy.x, y, xy.y);
            foreach (var t in map.towers)
                if (new Vector2(p.x - t.position.x, p.z - t.position.z).magnitude < map.HalfWidth(t) + 1.5f)
                { stats.notes.Add($"GAMEPLAY {what} at {xy} is on tower {t.id}'s plinth; skipped."); return false; }
            string reason = SpawnProblem(map.transform.TransformPoint(p + Vector3.up * .08f));
            if (reason != null) { stats.notes.Add($"GAMEPLAY {what} at {xy}: {reason}; skipped."); return false; }
            point = p + Vector3.up * .08f;
            return true;
        }

        // Highest collider top inside the play area (buildings), so PlayableBounds holds every roof.
        static float PlayAreaTop(VanderbiltCampusMap map)
        {
            float top = map.playAreaCenter.y + 10;
            foreach (var c in map.GetComponentsInChildren<Collider>())
            {
                var b = c.bounds; var local = map.transform.InverseTransformPoint(b.center);
                if (new Vector2(local.x - map.playAreaCenter.x, local.z - map.playAreaCenter.z).magnitude > map.playAreaRadius + 20) continue;
                if (CleanName(c.name) == "COL_Terrain") continue;
                top = Mathf.Max(top, map.transform.InverseTransformPoint(b.max).y);
            }
            return top;
        }

        // The castle block prefab and section materials come from an existing castle map prefab (reuse, not new art).
        static void AssignBlockAssets(VanderbiltCampusMap map, BuildStats stats)
        {
            var folder = Path.GetDirectoryName(ProjectPath.Find(PrefabName)).Replace('\\', '/');
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var castle = prefab ? prefab.GetComponent<CastleGenerator>() : null;
                if (!castle || !castle.blockPrefab || castle.environmentOnly) continue;
                map.blockPrefab = castle.blockPrefab; map.blockSize = castle.settings.blockSize; map.blockMass = castle.mass;
                map.cornerMaterial = castle.cornerMaterial; map.wallMaterial = castle.wallMaterial; map.keepMaterial = castle.keepMaterial;
                stats.notes.Add("Tower blocks and materials from " + prefab.name);
                break;
            }
            if (!map.blockPrefab) stats.Problem("No castle map prefab with a block prefab found in " + folder + "; towers cannot be built.");
            map.plinthMaterial = AssetDatabase.LoadAssetAtPath<Material>(CampusFolder() + "/Materials/mat_concrete.mat");
        }

        // Places each tower on flat open ground inside the play area, nudging up to 12 m when blocked.
        static void PlaceTowers(VanderbiltCampusMap map, List<VanderbiltCampusGameplay.Tower> source, BuildStats stats)
        {
            var placed = new List<CampusTower>();
            foreach (var t in source)
            {
                var tower = new CampusTower { id = t.id, kind = t.kind, size = t.size, yaw = t.yaw };
                float half = map.HalfWidth(tower);
                bool done = false; string why = "";
                foreach (var offset in Nudges())
                {
                    var xy = t.xy + offset;
                    if ((xy - new Vector2(map.playAreaCenter.x, map.playAreaCenter.z)).magnitude + half > map.playAreaRadius) { why = "outside the play area"; break; }
                    if (!FootprintGround(map.transform, xy, half, out float baseY, out float spread)) { why = "no COL_Terrain under the footprint"; continue; }
                    if (spread > 3) { why = $"ground too uneven ({spread:F1} m)"; continue; }
                    if (placed.Any(o => new Vector2(o.position.x - xy.x, o.position.z - xy.y).magnitude < half + map.HalfWidth(o) + 4)) { why = "too close to another tower"; continue; }
                    tower.position = new Vector3(xy.x, baseY, xy.y);
                    string blocker = TowerBlocker(map, tower);
                    if (blocker != null) { why = "blocked by " + blocker; continue; }
                    if (offset != Vector2.zero) stats.notes.Add($"GAMEPLAY tower {t.id} moved {offset.magnitude:F0} m to open ground.");
                    placed.Add(tower); done = true; break;
                }
                if (!done) stats.notes.Add($"GAMEPLAY tower {t.id} at {t.xy} not placed: {why}.");
            }
            map.towers = placed.ToArray();
            if (placed.Count == 0) stats.Problem("No tower could be placed inside the play area.");
        }

        static IEnumerable<Vector2> Nudges()
        {
            yield return Vector2.zero;
            for (int ring = 1; ring <= 3; ring++)
                for (int i = 0; i < 8; i++)
                    yield return new Vector2(Mathf.Cos(i * Mathf.PI / 4), Mathf.Sin(i * Mathf.PI / 4)) * ring * 4;
        }

        // Ground under the footprint: the highest point becomes the plinth top; spread = highest - lowest.
        static bool FootprintGround(Transform root, Vector2 xy, float half, out float top, out float spread)
        {
            top = float.NegativeInfinity; float low = float.PositiveInfinity;
            for (int i = -1; i < 8; i++)
            {
                var p = i < 0 ? xy : xy + new Vector2(Mathf.Cos(i * Mathf.PI / 4), Mathf.Sin(i * Mathf.PI / 4)) * half;
                if (!GroundAt(root, p, out float y)) { spread = 0; return false; }
                top = Mathf.Max(top, y); low = Mathf.Min(low, y);
            }
            spread = top - low; return true;
        }

        // Name of a building, tree or other collider inside the tower's volume, or null when clear.
        static string TowerBlocker(VanderbiltCampusMap map, CampusTower tower)
        {
            float half = map.HalfWidth(tower), height = CastleLayout.HeightCourses(map.SettingsFor(tower)) * map.blockSize.y;
            var center = map.transform.TransformPoint(tower.position + Vector3.up * (height * .5f + .3f));
            var hits = Physics.OverlapBox(center, new Vector3(half, height * .5f, half), map.transform.rotation * Quaternion.Euler(0, tower.yaw, 0),
                (1 << Layers.Surface) | 1, QueryTriggerInteraction.Ignore);
            var blocker = hits.FirstOrDefault(h => CleanName(h.name) != "COL_Terrain");
            return blocker ? (blocker.transform.parent ? blocker.transform.parent.name + "/" : "") + blocker.name : null;
        }

        // Default towers: the flattest open 8 m cells inside the play area, at least 30 m apart: one castle, two towers.
        static List<VanderbiltCampusGameplay.Tower> DefaultTowers(VanderbiltCampusMap map)
        {
            var cells = new List<(Vector2 xy, float spread)>();
            var c = new Vector2(map.playAreaCenter.x, map.playAreaCenter.z);
            for (float x = -map.playAreaRadius; x <= map.playAreaRadius; x += 8)
                for (float z = -map.playAreaRadius; z <= map.playAreaRadius; z += 8)
                {
                    var xy = c + new Vector2(x, z);
                    if ((xy - c).magnitude > map.playAreaRadius - 20) continue;
                    if (FootprintGround(map.transform, xy, 6, out _, out float spread) && spread < 1) cells.Add((xy, spread));
                }
            var picks = new List<VanderbiltCampusGameplay.Tower>();
            foreach (var cell in cells.OrderBy(k => k.spread).ThenBy(k => (k.xy - c).magnitude))
            {
                if (picks.Any(p => (p.xy - cell.xy).magnitude < 30)) continue;
                var tower = picks.Count == 0 ? new VanderbiltCampusGameplay.Tower { id = "default_castle", kind = "castle", size = 12, xy = cell.xy }
                    : new VanderbiltCampusGameplay.Tower { id = "default_tower_" + picks.Count, kind = "tower", size = 18, xy = cell.xy };
                var probe = new CampusTower { kind = tower.kind, size = tower.size, position = new Vector3(cell.xy.x, 0, cell.xy.y) };
                if (!FootprintGround(map.transform, cell.xy, map.HalfWidth(probe), out float y, out _)) continue;
                probe.position.y = y;
                if (TowerBlocker(map, probe) != null) continue;
                picks.Add(tower);
                if (picks.Count == 3) break;
            }
            return picks;
        }

        // Default spawns: the pipeline's spawns inside the play area plus a ring at 60% of the radius.
        static List<VanderbiltCampusGameplay.Point> DefaultSpawns(VanderbiltCampusMap map, VanderbiltCampusData data)
        {
            var list = data.spawns.Select(s => new VanderbiltCampusGameplay.Point { name = s.name, xy = new Vector2(s.pos.x, s.pos.z) }).ToList();
            var c = new Vector2(map.playAreaCenter.x, map.playAreaCenter.z);
            for (int i = 0; i < 12; i++)
                list.Add(new VanderbiltCampusGameplay.Point { name = "Ring spawn " + i, xy = c + new Vector2(Mathf.Cos(i * Mathf.PI / 6), Mathf.Sin(i * Mathf.PI / 6)) * map.playAreaRadius * .6f });
            return list;
        }

        // Default team areas: west and east of the play-area center, six spawns around each flag.
        static List<VanderbiltCampusGameplay.Team> DefaultTeams(VanderbiltCampusMap map)
        {
            var c = new Vector2(map.playAreaCenter.x, map.playAreaCenter.z);
            var teams = new List<VanderbiltCampusGameplay.Team>();
            foreach (var (name, dir) in new[] { ("West", Vector2.left), ("East", Vector2.right) })
            {
                var team = new VanderbiltCampusGameplay.Team { name = name, flag = c + dir * map.playAreaRadius * .4f };
                for (int i = 0; i < 6; i++) team.spawns.Add(team.flag + new Vector2(Mathf.Cos(i * Mathf.PI / 3), Mathf.Sin(i * Mathf.PI / 3)) * 5);
                teams.Add(team);
            }
            return teams;
        }
    }
}
