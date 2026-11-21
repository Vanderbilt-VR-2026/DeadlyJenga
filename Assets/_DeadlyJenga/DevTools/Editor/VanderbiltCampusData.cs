using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    // Reads Tools/VanderbiltCampus/config/unity_map.json, which the Blender pipeline writes.
    // Every key is optional; a missing key just leaves the default. All positions are map-root local,
    // Unity axes (X east, Y up, Z north, meters). Schema: see World/Maps/VanderbiltCampus/VanderbiltCampus.md.
    public sealed class VanderbiltCampusData
    {
        public sealed class Named { public string name; public Vector3 pos; }
        public sealed class Team { public string name; public Vector3 flag; public List<Vector3> spawns = new List<Vector3>(); }
        public sealed class View { public string name; public Vector3 pos, look; }
        public sealed class Climb { public string building; public Vector3 pos, forward = Vector3.forward; }

        public bool found;
        public string path;
        public int layoutVersion = 1;
        public bool hasArea; public Bounds localArea;
        public bool hasLowestGround; public float lowestGround;
        public bool hasHill; public Vector3 hill;
        public readonly List<Named> spawns = new List<Named>();
        public readonly List<Team> teams = new List<Team>();
        public readonly List<View> viewpoints = new List<View>();
        public readonly List<Vector3> route = new List<Vector3>();
        public readonly List<Climb> climb = new List<Climb>();
        public readonly List<Vector3> sheltered = new List<Vector3>();   // optional: points under a roof overhang
        public readonly List<string> problems = new List<string>();

        public static string FilePath =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Tools", "VanderbiltCampus", "config", "unity_map.json");

        public static VanderbiltCampusData Load()
        {
            var data = new VanderbiltCampusData { path = FilePath };
            if (!File.Exists(data.path)) { data.problems.Add("unity_map.json not found at " + data.path); return data; }
            Dictionary<string, object> root;
            try { root = MiniJson.Parse(File.ReadAllText(data.path)) as Dictionary<string, object>; }
            catch (Exception e) { data.problems.Add("unity_map.json could not be read: " + e.Message); return data; }
            if (root == null) { data.problems.Add("unity_map.json is not a JSON object"); return data; }
            data.found = true;

            if (root.TryGetValue("layout_version", out var version) && version is double v) data.layoutVersion = Mathf.Max(1, (int)v);
            if (root.TryGetValue("local_area", out var areaValue) && areaValue is Dictionary<string, object> area &&
                TryVector(area, "center", out var center) && TryVector(area, "size", out var size))
            { data.hasArea = true; data.localArea = new Bounds(center, size); }
            if (root.TryGetValue("lowest_ground", out var lowest) && lowest is double low) { data.hasLowestGround = true; data.lowestGround = (float)low; }
            if (TryVector(root, "hill", out var hill)) { data.hasHill = true; data.hill = hill; }

            foreach (var item in Objects(root, "spawns"))
                if (TryVector(item, "pos", out var p)) data.spawns.Add(new Named { name = Text(item, "name", "Spawn " + data.spawns.Count), pos = p });
            foreach (var item in Objects(root, "teams"))
            {
                var team = new Team { name = Text(item, "name", "Team " + data.teams.Count) };
                TryVector(item, "flag", out team.flag);
                if (item.TryGetValue("spawns", out var list) && list is List<object> points)
                    foreach (var point in points) if (ToVector(point, out var s)) team.spawns.Add(s);
                data.teams.Add(team);
            }
            foreach (var item in Objects(root, "viewpoints"))
                if (TryVector(item, "pos", out var p) && TryVector(item, "look", out var look))
                    data.viewpoints.Add(new View { name = Text(item, "name", "View " + data.viewpoints.Count), pos = p, look = look });
            if (root.TryGetValue("route", out var routeValue) && routeValue is List<object> route)
                foreach (var point in route) if (ToVector(point, out var p)) data.route.Add(p);
            foreach (var item in Objects(root, "climb"))
                if (TryVector(item, "pos", out var p))
                {
                    var anchor = new Climb { building = Text(item, "building", ""), pos = p };
                    if (TryVector(item, "forward", out var f) && f.sqrMagnitude > .0001f) anchor.forward = f.normalized;
                    data.climb.Add(anchor);
                }
            if (root.TryGetValue("sheltered", out var shelterValue) && shelterValue is List<object> shelters)
                foreach (var point in shelters) if (ToVector(point, out var p)) data.sheltered.Add(p);
            return data;
        }

        static IEnumerable<Dictionary<string, object>> Objects(Dictionary<string, object> root, string key)
        {
            if (!root.TryGetValue(key, out var value) || !(value is List<object> list)) yield break;
            foreach (var item in list) if (item is Dictionary<string, object> d) yield return d;
        }
        static string Text(Dictionary<string, object> d, string key, string fallback) =>
            d.TryGetValue(key, out var value) && value is string s && s.Length > 0 ? s : fallback;
        static bool TryVector(Dictionary<string, object> d, string key, out Vector3 v)
        { v = Vector3.zero; return d.TryGetValue(key, out var value) && ToVector(value, out v); }
        static bool ToVector(object value, out Vector3 v)
        {
            v = Vector3.zero;
            if (!(value is List<object> list) || list.Count < 3 || !(list[0] is double x) || !(list[1] is double y) || !(list[2] is double z)) return false;
            v = new Vector3((float)x, (float)y, (float)z);
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        // How every campus batch run ends. A warm (already open) editor writes the exit code to a file
        // for the coordinator and stays open; a normal batch run quits Unity with the code.
        internal static void Exit(int code)
        {
            if (SessionState.GetString("DJWarm.id", "") != "")
            {
                Directory.CreateDirectory("Logs/Warm");
                File.WriteAllText("Logs/Warm/exit.txt", code.ToString());
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                return;
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }

    // A very small JSON reader (Unity's JsonUtility cannot read arrays of arrays).
    // Objects become Dictionary<string,object>, arrays List<object>, numbers double.
    static class MiniJson
    {
        public static object Parse(string text)
        {
            int i = 0; var value = Value(text, ref i); Skip(text, ref i);
            if (i < text.Length) throw new FormatException("Unexpected text at " + i);
            return value;
        }
        static void Skip(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) throw new FormatException("Unexpected end of JSON");
            char c = s[i];
            if (c == '{') return Obj(s, ref i);
            if (c == '[') return Arr(s, ref i);
            if (c == '"') return Str(s, ref i);
            if (Word(s, ref i, "true")) return true;
            if (Word(s, ref i, "false")) return false;
            if (Word(s, ref i, "null")) return null;
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == start) throw new FormatException("Bad JSON value at " + i);
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
        static bool Word(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length; return true;
        }
        static Dictionary<string, object> Obj(string s, ref int i)
        {
            var d = new Dictionary<string, object>(); i++;
            Skip(s, ref i); if (i < s.Length && s[i] == '}') { i++; return d; }
            while (true)
            {
                Skip(s, ref i); string key = Str(s, ref i);
                Skip(s, ref i); if (i >= s.Length || s[i] != ':') throw new FormatException("Expected : at " + i); i++;
                d[key] = Value(s, ref i);
                Skip(s, ref i); if (i >= s.Length) throw new FormatException("Unclosed object");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return d; }
                throw new FormatException("Expected , or } at " + i);
            }
        }
        static List<object> Arr(string s, ref int i)
        {
            var list = new List<object>(); i++;
            Skip(s, ref i); if (i < s.Length && s[i] == ']') { i++; return list; }
            while (true)
            {
                list.Add(Value(s, ref i));
                Skip(s, ref i); if (i >= s.Length) throw new FormatException("Unclosed array");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return list; }
                throw new FormatException("Expected , or ] at " + i);
            }
        }
        static string Str(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') throw new FormatException("Expected string at " + i);
            var b = new StringBuilder(); i++;
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++; char e = s[i];
                    if (e == 'u' && i + 4 < s.Length) { b.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                    else b.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e == 'b' ? '\b' : e == 'f' ? '\f' : e);
                }
                else b.Append(s[i]);
                i++;
            }
            if (i >= s.Length) throw new FormatException("Unclosed string");
            i++; return b.ToString();
        }
    }
}
