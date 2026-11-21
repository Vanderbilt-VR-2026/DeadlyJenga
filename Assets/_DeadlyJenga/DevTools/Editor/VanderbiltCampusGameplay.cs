using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    // Reads Tools/VanderbiltCampus/config/gameplay.json: where the play area, towers, spawns, team areas and
    // objectives go. Positions are CAMPUS meters on the ground plane: x east, y north (Unity x and z).
    // Heights are not stored; the builder samples them from the built COL_Terrain colliders.
    // Format (every key optional): see World/Maps/VanderbiltCampus/VanderbiltCampus.md.
    public sealed class VanderbiltCampusGameplay
    {
        public sealed class Tower { public string id, kind = "tower"; public Vector2 xy; public int size = 18; public float yaw; }
        public sealed class Point { public string name; public Vector2 xy; }
        public sealed class Team { public string name; public Vector2 flag; public readonly List<Vector2> spawns = new List<Vector2>(); }

        public bool found;
        public string path;
        public Vector2 playCenter;
        public float playRadius = 150;
        public bool hasPlayArea, hasHill;
        public Vector2 hill;
        public readonly List<Tower> towers = new List<Tower>();
        public readonly List<Point> spawns = new List<Point>();
        public readonly List<Team> teams = new List<Team>();
        public readonly List<string> problems = new List<string>();

        public static string FilePath =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Tools", "VanderbiltCampus", "config", "gameplay.json");

        public static VanderbiltCampusGameplay Load()
        {
            var g = new VanderbiltCampusGameplay { path = FilePath };
            if (!File.Exists(g.path)) return g;
            Dictionary<string, object> root;
            try { root = MiniJson.Parse(File.ReadAllText(g.path)) as Dictionary<string, object>; }
            catch (Exception e) { g.problems.Add("gameplay.json could not be read: " + e.Message); return g; }
            if (root == null) { g.problems.Add("gameplay.json is not a JSON object"); return g; }
            g.found = true;

            if (root.TryGetValue("play_area", out var areaValue) && areaValue is Dictionary<string, object> area)
            {
                if (XY(area, "center", out var c)) { g.playCenter = c; g.hasPlayArea = true; }
                if (area.TryGetValue("radius", out var r) && r is double radius && radius >= 10) g.playRadius = (float)radius;
            }
            foreach (var item in Objects(root, "towers"))
            {
                if (!Num(item, "x", out var x) || !Num(item, "y", out var y)) { g.problems.Add("A tower in gameplay.json has no x/y."); continue; }
                var t = new Tower { id = Text(item, "id", "tower" + g.towers.Count), kind = Text(item, "kind", "tower"), xy = new Vector2(x, y) };
                if (Num(item, "size", out var size)) t.size = Mathf.RoundToInt(size);
                if (Num(item, "yaw", out var yaw)) t.yaw = yaw;
                if (t.kind != "tower" && t.kind != "castle") { g.problems.Add($"Tower {t.id}: unknown kind '{t.kind}' (tower or castle)."); continue; }
                g.towers.Add(t);
            }
            foreach (var item in Objects(root, "spawns"))
                if (Num(item, "x", out var x) && Num(item, "y", out var y))
                    g.spawns.Add(new Point { name = Text(item, "name", "Spawn " + g.spawns.Count), xy = new Vector2(x, y) });
            foreach (var item in Objects(root, "team_areas"))
            {
                var team = new Team { name = Text(item, "name", "Team " + g.teams.Count) };
                if (!XY(item, "flag", out team.flag)) { g.problems.Add("Team area " + team.name + " has no flag [x, y]."); continue; }
                if (item.TryGetValue("spawns", out var list) && list is List<object> points)
                    foreach (var p in points) if (ToXY(p, out var s)) team.spawns.Add(s);
                g.teams.Add(team);
            }
            foreach (var item in Objects(root, "objectives"))
                if (Text(item, "kind", "") == "hill" && Num(item, "x", out var x) && Num(item, "y", out var y)) { g.hill = new Vector2(x, y); g.hasHill = true; }
            return g;
        }

        static IEnumerable<Dictionary<string, object>> Objects(Dictionary<string, object> root, string key)
        {
            if (!root.TryGetValue(key, out var value) || !(value is List<object> list)) yield break;
            foreach (var item in list) if (item is Dictionary<string, object> d) yield return d;
        }
        static string Text(Dictionary<string, object> d, string key, string fallback) =>
            d.TryGetValue(key, out var v) && v is string s && s.Length > 0 ? s : fallback;
        static bool Num(Dictionary<string, object> d, string key, out float value)
        { value = 0; if (!d.TryGetValue(key, out var v) || !(v is double n)) return false; value = (float)n; return true; }
        static bool XY(Dictionary<string, object> d, string key, out Vector2 v) { v = default; return d.TryGetValue(key, out var value) && ToXY(value, out v); }
        static bool ToXY(object value, out Vector2 v)
        {
            v = default;
            if (!(value is List<object> list) || list.Count < 2 || !(list[0] is double x) || !(list[1] is double y)) return false;
            v = new Vector2((float)x, (float)y); return true;
        }
    }
}
