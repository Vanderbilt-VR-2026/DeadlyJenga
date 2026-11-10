using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using DeadlyJenga.NinjaArt;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    // Explicit authoring only: builds the Moonlit Shinobi Temple map prefab, its lacquer materials and its match layout.
    public static class NinjaTempleTools
    {
        public const string MapId = "mode-ninja-temple";
        const string Materials = "Assets/_DeadlyJenga/Presentation/Visuals/NinjaTemple/Materials";
        const string Prefab = "Assets/_DeadlyJenga/World/Maps/Resources/ModeMaps/Mode_ninja-temple.prefab";
        const string Layout = "Assets/_DeadlyJenga/GameSession/Modes/Resources/MapMatchLayouts/Ninja Temple Match Layout.asset";
        public static readonly Vector3 Origin = new Vector3(0, 1.5f, 0);

        [MenuItem("Tools/Deadly Jenga/Maps/Build Ninja Temple Map")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play Mode before building the Ninja Temple.");
            if (!AssetDatabase.IsValidFolder(Materials)) AssetDatabase.CreateFolder("Assets/_DeadlyJenga/Presentation/Visuals/NinjaTemple", "Materials");
            var red = Lacquer("Ninja Vermilion Lacquer", new Color(.56f, .075f, .045f), .78f, .55f, .4f);
            var black = Lacquer("Ninja Black Lacquer", new Color(.055f, .05f, .06f), .88f, .6f, .45f);
            var deck = Lacquer("Ninja Cedar Deck", new Color(.24f, .14f, .085f), .35f, .3f, .3f);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("CastleBlock.prefab"));
            var root = new GameObject("Mode map - ninja temple");
            try
            {
                root.transform.position = Origin;
                var generator = root.AddComponent<CastleGenerator>();
                generator.settings = new CastleSettings { layoutPreset = NinjaTempleLayout.Preset }; generator.blockPrefab = source; generator.generateOnStart = false;
                generator.wallMaterial = red; generator.cornerMaterial = black; generator.keepMaterial = deck;
                var foundation = new GameObject("Fixed Foundation"); foundation.transform.SetParent(root.transform, false); foundation.transform.localPosition = Vector3.down * .5f;
                var size = CastleLayout.FootprintSize(generator.settings) + Vector2.one * 4; foundation.transform.localScale = new Vector3(size.x, 1, size.y); foundation.AddComponent<BoxCollider>();
                generator.foundation = foundation.transform; generator.waterHeight = .08f; generator.oceanKillHeight = -6; generator.mass = 40;
                root.AddComponent<NinjaTempleArt>().waterLevel = .08f;
                PrefabUtility.SaveAsPrefabAsset(root, Prefab);
                var layout = AssetDatabase.LoadAssetAtPath<MapMatchLayout>(Layout);
                if (!layout) { layout = ScriptableObject.CreateInstance<MapMatchLayout>(); AssetDatabase.CreateAsset(layout, Layout); }
                layout.layoutSignature = generator.SaveLayout();
                layout.freeSpawns = new[] {
                    new Vector3(14,.06f,13), new Vector3(-14,.06f,-13), new Vector3(-13,.06f,14), new Vector3(13,.06f,-14),
                    new Vector3(31,.06f,12), new Vector3(-31,.06f,-12), new Vector3(-12,.06f,31), new Vector3(12,.06f,-31),
                    new Vector3(31,.06f,-12), new Vector3(-31,.06f,12), new Vector3(12,.06f,31), new Vector3(-12,.06f,-31) };
                layout.teams = new[] {
                    new MapMatchLayout.TeamArea { name = "South", flagHome = new Vector3(0, 16.1f, -24), spawns = layout.freeSpawns.Where(p => p.z < 0).ToArray() },
                    new MapMatchLayout.TeamArea { name = "North", flagHome = new Vector3(0, 16.1f, 24), spawns = layout.freeSpawns.Where(p => p.z > 0).ToArray() } };
                layout.hill = new Vector3(0, 11.05f, 4.5f);
                EditorUtility.SetDirty(layout); AssetDatabase.SaveAssets();
                Debug.Log("Built " + Prefab + " with " + CastleLayout.Build(generator.settings).Count + " blocks.");
            }
            finally { Object.DestroyImmediate(root); }
        }
        static Material Lacquer(string name, Color color, float gloss, float spec, float rim)
        {
            string path = Materials + "/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Resources.Load<Shader>("NinjaScenery/NinjaLacquer")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Resources.Load<Shader>("NinjaScenery/NinjaLacquer"); m.enableInstancing = true;
            m.SetColor("_BaseColor", color); m.SetFloat("_Gloss", gloss); m.SetFloat("_Spec", spec); m.SetFloat("_Rim", rim); m.SetFloat("_Warm", 1);
            EditorUtility.SetDirty(m); return m;
        }

        // Size-aware layout check: no penetration within a course and every raised block rests on bearing below.
        [MenuItem("Tools/Deadly Jenga/Validation/Check Ninja Temple Layout")]
        public static string ValidateLayout()
        {
            var settings = new CastleSettings { layoutPreset = NinjaTempleLayout.Preset }; var layout = CastleLayout.Build(settings);
            var report = new System.Text.StringBuilder(); int fails = 0;
            void Check(bool ok, string message) { report.Append(ok ? "PASS " : "FAIL ").AppendLine(message); if (!ok) fails++; }
            Check(layout.Count > 150 && layout.Count <= 400, "block budget within 150..400: " + layout.Count);
            Check(CastleLayout.Build(settings).Count == layout.Count, "deterministic");
            var boxes = layout.Select(p => Box(p, settings)).ToArray(); bool overlap = false, floating = false;
            for (int i = 0; i < boxes.Length; i++)
            {
                bool supported = layout[i].Position.y - layout[i].BlockSize(settings).y * .5f < .01f;
                for (int k = 0; k < boxes.Length; k++)
                {
                    if (i == k) continue; var a = boxes[i]; var b = boxes[k];
                    float ox = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x), oz = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z), oy = Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y);
                    if (ox > .01f && oz > .01f && oy > .01f) overlap = true;
                    if (ox > .01f && oz > .01f && Mathf.Abs(b.max.y - a.min.y) < .01f && b.min.x - .01f <= a.center.x + a.extents.x && b.max.x + .01f >= a.center.x - a.extents.x) supported = true;
                }
                if (!supported) floating = true;
            }
            Check(!overlap, "no penetrating blocks");
            Check(!floating, "every raised block rests on a block below");
            var json = JsonUtility.ToJson(settings); Check(CastleLayout.Build(JsonUtility.FromJson<CastleSettings>(json)).Count == layout.Count, "survives serialized layout round-trip");
            var text = report.ToString(); File.WriteAllText(ProjectPath.Report("NinjaTempleLayout.txt"), text); Debug.Log(text);
            if (fails > 0) throw new System.InvalidOperationException("Ninja temple layout failed: " + text);
            return text;
        }
        static Bounds Box(CastlePlacement p, CastleSettings s)
        {
            var size = p.BlockSize(s); var right = p.Rotation * Vector3.right; var forward = p.Rotation * Vector3.forward;
            return new Bounds(p.Position, new Vector3(Mathf.Abs(right.x) * size.x + Mathf.Abs(forward.x) * size.z, size.y, Mathf.Abs(right.z) * size.x + Mathf.Abs(forward.z) * size.z));
        }
    }
}
