using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.ArchitectureMap.Editor
{
    // Editor-only adapter. No scene is opened, saved, instantiated, or marked dirty.
    [InitializeOnLoad]
    public static class ArchitectureMapEditor
    {
        [Serializable] public sealed class Entry { public string path; public string hash; public string guid; public string[] dependencies; }
        [Serializable] public sealed class Snapshot { public string exportedAt; public Entry[] assets; }
        static readonly Queue<string> Pending = new Queue<string>();
        static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();
        static double nextWrite;
        static bool active;
        static bool dashboard;
        static double nextLeaseCheck;
        static string Root { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); } }
        static string Output { get { return Path.Combine(Root, ".architecture/local/unity.json"); } }
        static ArchitectureMapEditor() { EditorApplication.update += Tick; }

        [MenuItem("Tools/Architecture Map/Open")]
        public static void Open()
        {
            var launcher = Path.Combine(Root, "Tools/ArchitectureMap/Start-ArchitectureMap.ps1");
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                EditorUtility.DisplayDialog("Architecture Map", "Run dotnet run --project Tools/ArchitectureMap -- --root . --open from the repository. The dashboard works on Windows, macOS and Linux.", "OK");
                return;
            }
            Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + launcher + "\"") { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
        }
        [MenuItem("Tools/Architecture Map/Export Unity Dependencies")]
        public static void ExportNow() { QueueAll(); active = true; }
        static void QueueAll()
        {
            Pending.Clear(); Entries.Clear();
            foreach (var path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && !AssetDatabase.IsValidFolder(p))) Pending.Enqueue(path);
        }
        public static void Changed(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
        {
            if (!active) return;
            foreach (var path in imported.Concat(moved)) Pending.Enqueue(path);
            foreach (var path in deleted.Concat(oldPaths)) Entries.Remove(path);
            nextWrite = 0;
        }
        static void Tick()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.timeSinceStartup >= nextLeaseCheck)
            {
                var lease = Path.Combine(Root, ".architecture/local/lease.json");
                dashboard = File.Exists(lease) && DateTime.UtcNow - File.GetLastWriteTimeUtc(lease) < TimeSpan.FromSeconds(90);
                nextLeaseCheck = EditorApplication.timeSinceStartup + 2;
            }
            if (dashboard && !active) { active = true; QueueAll(); }
            if (!active) return;
            var timer = Stopwatch.StartNew();
            while (Pending.Count > 0 && timer.ElapsedMilliseconds < 8) Analyze(Pending.Dequeue());
            if (Pending.Count == 0 && EditorApplication.timeSinceStartup > nextWrite)
            {
                Save(); nextWrite = EditorApplication.timeSinceStartup + 60;
                if (!dashboard) active = false;
            }
        }
        static void Analyze(string path)
        {
            if (!File.Exists(Path.Combine(Root, path))) { Entries.Remove(path); return; }
            try
            {
                using (var sha = SHA256.Create()) using (var stream = File.OpenRead(Path.Combine(Root, path)))
                    Entries[path] = new Entry { path = path, hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), guid = AssetDatabase.AssetPathToGUID(path), dependencies = AssetDatabase.GetDependencies(path, false).Where(p => p != path).Select(AssetDatabase.AssetPathToGUID).Where(g => !string.IsNullOrEmpty(g)).ToArray() };
            }
            catch (IOException) { /* Scanner retains explicit fallback evidence while a file is being written. */ }
        }
        static void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            var value = new Snapshot { exportedAt = DateTime.UtcNow.ToString("O"), assets = Entries.Values.OrderBy(e => e.path).ToArray() };
            File.WriteAllText(Output + ".tmp", JsonUtility.ToJson(value));
            if (File.Exists(Output)) File.Replace(Output + ".tmp", Output, null); else File.Move(Output + ".tmp", Output);
        }
        // For an isolated Unity batch validation project. Does not invoke gameplay.
        public static void ValidateBatch()
        {
            QueueAll(); while (Pending.Count > 0) Analyze(Pending.Dequeue()); Save();
            if (Entries.Count == 0) throw new InvalidOperationException("Unity discovered no assets.");
            UnityEngine.Debug.Log("ARCHITECTURE_MAP_VALIDATION_OK " + Entries.Count);
        }
    }
    public sealed class ArchitectureMapPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
        { ArchitectureMapEditor.Changed(imported, deleted, moved, oldPaths); }
    }
}
