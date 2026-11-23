using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Where Sets and tapes live on disk.
    //   Sets:  small JSON files next to TrailerStudioSets.md (found by name, so the folder can move).
    //   Tapes: big binary files in <project>/Recordings/TrailerStudio/<set id>/ (never imported, never committed).
    public static class StudioLibrary
    {
        const string Marker = "TrailerStudioSets.md", Extension = ".set.json";

        // Tests keep their Sets outside Assets so a test run never adds project files.
        const string OverrideKey = "DeadlyJenga.TrailerStudio.SetsFolder";
        public static string SetsFolderOverride
        {
            get => SessionState.GetString(OverrideKey, "");
            set => SessionState.SetString(OverrideKey, string.IsNullOrEmpty(value) ? "" : value.Replace('\\', '/').TrimEnd('/') + "/");
        }
        static bool InProject => SetsFolderOverride.Length == 0;

        public static string SetsFolder => InProject ? ProjectPath.FolderOf(Marker) : SetsFolderOverride;
        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        public static string TapeFolder(string setId) => Path.Combine(ProjectRoot, "Recordings", "TrailerStudio", setId);
        public static string TapePath(string setId, string tape = "main") => Path.Combine(TapeFolder(setId), tape + ".djtape");
        public static string SetPath(string setId) => SetsFolder + setId + Extension;

        public static List<string> SetIds()
        {
            string folder = SetsFolder;
            return Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*" + Extension).Select(f => Path.GetFileName(f).Replace(Extension, "")).OrderBy(n => n).ToList()
                : new List<string>();
        }

        public static StudioSet Load(string setId)
        {
            string file = SetPath(setId);
            if (!File.Exists(file)) return null;
            StudioSet set;
            // A damaged file (cut short, or with merge conflict marks) must not stop the window from opening.
            try { set = JsonUtility.FromJson<StudioSet>(File.ReadAllText(file)); }
            catch (Exception error)
            {
                Debug.LogWarning($"Trailer Studio could not read the Set file {file}: {error.Message}");
                return null;
            }
            if (set != null)
            {
                set.id = setId;
                set.moments ??= new List<StudioMoment>();
                set.theatreBookmarks ??= new List<TheatreBookmark>();
            }
            return set;
        }

        public static void Save(StudioSet set)
        {
            if (string.IsNullOrEmpty(set.id)) set.id = NewId(set.title);
            Directory.CreateDirectory(SetsFolder);
            File.WriteAllText(SetPath(set.id), JsonUtility.ToJson(set, true));
            // Tell Unity about the file, but never in the middle of a Play Mode run. Only this file:
            // a full refresh after every edit (each slider release) scans the whole project.
            if (InProject && !EditorApplication.isPlayingOrWillChangePlaymode) AssetDatabase.ImportAsset(SetPath(set.id));
        }

        public static StudioSet Create(string title)
        {
            var set = new StudioSet { title = title, id = NewId(title) };
            Save(set);
            return set;
        }

        // A copy with its own id. Tapes are not copied: a new Set starts with an empty tape.
        public static StudioSet Duplicate(StudioSet source)
        {
            var copy = source.Clone();
            copy.title = source.title + " copy";
            copy.id = NewId(copy.title);
            copy.moments.Clear();
            copy.theatreBookmarks.Clear();
            Save(copy);
            return copy;
        }

        public static void Rename(StudioSet set, string title)
        {
            set.title = title;
            Save(set);
        }

        // Deleting a Set moves its file to the trash folder; tapes stay where they are.
        public static void Trash(StudioSet set)
        {
            string file = SetPath(set.id);
            if (!File.Exists(file)) return;
            string trash = Path.Combine(TapeFolder(set.id), "trash");
            Directory.CreateDirectory(trash);
            File.Copy(file, Path.Combine(trash, set.id + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + Extension), true);
            if (InProject) AssetDatabase.DeleteAsset(file);
            else File.Delete(file);
        }

        static string NewId(string title)
        {
            var clean = new string((title ?? "set").ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
            while (clean.Contains("--")) clean = clean.Replace("--", "-");
            if (clean.Length == 0) clean = "set";
            if (clean.Length > 40) clean = clean.Substring(0, 40);
            var taken = new HashSet<string>(SetIds());
            string id = clean;
            for (int n = 2; taken.Contains(id); n++) id = clean + "-" + n;
            return id;
        }
    }
}
