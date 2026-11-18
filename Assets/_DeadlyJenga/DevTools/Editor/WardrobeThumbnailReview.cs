using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Side-by-side review of wardrobe pictures: what the wardrobe shows now vs a freshly rendered candidate.
    // Accept replaces the wardrobe picture with the candidate; Discard deletes the candidate.
    // Tools > Deadly Jenga > Wardrobe Thumbnails > Review Candidates.
    public sealed class WardrobeThumbnailReview : EditorWindow
    {
        sealed class Row { public string slot, file, label, candidate, current; public Texture2D newTex, oldTex; public bool changed; }

        readonly List<Row> rows = new();
        Vector2 scroll;
        string filter = "";
        int slotFilter, colorFilter = -1;
        bool changedOnly = true;
        float size = 128;
        string renderItem = "";

        [MenuItem("Tools/Deadly Jenga/Wardrobe Thumbnails/Review Candidates")]
        public static void Open()
        {
            var window = GetWindow<WardrobeThumbnailReview>("Wardrobe pictures");
            window.minSize = new Vector2(520, 360);
            window.Reload();
            window.Show();
        }

        void OnDisable() => Clear();

        void Clear()
        {
            foreach (var r in rows) { if (r.newTex) DestroyImmediate(r.newTex); if (r.oldTex) DestroyImmediate(r.oldTex); }
            rows.Clear();
        }

        void Reload()
        {
            Clear();
            string root = WardrobeThumbnailRig.CandidateFolder;
            if (!Directory.Exists(root)) return;
            foreach (string candidate in Directory.GetFiles(root, "*.png", SearchOption.AllDirectories).OrderBy(f => f))
            {
                string relative = candidate.Substring(root.Length + 1);
                string current = Path.Combine(WardrobeThumbnailRig.LiveFolder, relative);
                var row = new Row
                {
                    slot = Path.GetDirectoryName(relative), file = relative, candidate = candidate, current = current,
                    label = Path.GetFileNameWithoutExtension(relative).Replace("_c", " · colour ")
                };
                byte[] newBytes = File.ReadAllBytes(candidate);
                byte[] oldBytes = File.Exists(current) ? File.ReadAllBytes(current) : null;
                row.changed = oldBytes == null || !newBytes.SequenceEqual(oldBytes);
                row.newTex = Load(newBytes);
                row.oldTex = oldBytes != null ? Load(oldBytes) : null;
                rows.Add(row);
            }
        }

        static Texture2D Load(byte[] bytes) { var t = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave }; t.LoadImage(bytes); return t; }

        IEnumerable<Row> Visible()
        {
            string slot = slotFilter == 0 ? null : WardrobeLibrarySlots[slotFilter - 1];
            foreach (var r in rows)
            {
                if (changedOnly && !r.changed) continue;
                if (slot != null && r.slot != slot) continue;
                if (colorFilter >= 0 && !r.file.EndsWith("_c" + colorFilter + ".png")) continue;
                if (filter.Length > 0 && r.label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                yield return r;
            }
        }

        static string[] WardrobeLibrarySlots => Presentation.WardrobeThumbnailLibrary.Slots;

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60))) Reload();
                slotFilter = EditorGUILayout.Popup(slotFilter, new[] { "All slots" }.Concat(WardrobeLibrarySlots).ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(100));
                colorFilter = EditorGUILayout.IntPopup(colorFilter, new[] { "All colours", "0", "1", "2", "3", "4", "5", "6", "7" }, new[] { -1, 0, 1, 2, 3, 4, 5, 6, 7 }, EditorStyles.toolbarPopup, GUILayout.Width(90));
                changedOnly = GUILayout.Toggle(changedOnly, "Changed only", EditorStyles.toolbarButton, GUILayout.Width(95));
                filter = GUILayout.TextField(filter, EditorStyles.toolbarSearchField, GUILayout.MinWidth(80));
                size = GUILayout.HorizontalSlider(size, 64, 256, GUILayout.Width(90));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Render candidates:", GUILayout.Width(118));
                renderItem = EditorGUILayout.TextField(renderItem, GUILayout.MinWidth(120));
                if (GUILayout.Button(renderItem.Length > 0 ? "Render this item" : "Render all", GUILayout.Width(120)))
                    WardrobeThumbnailRig.RenderCandidates(renderItem.Trim());
            }
            EditorGUILayout.HelpBox("Item names look like Headgear/CowboyHat. Candidates render into Logs/WardrobeCandidates and replace nothing until you accept them.", MessageType.None);

            var visible = Visible().ToList();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"{visible.Count} shown · {rows.Count(r => r.changed)} differ from the wardrobe · {rows.Count} candidates", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                GUI.enabled = visible.Count > 0;
                if (GUILayout.Button("Accept all shown", GUILayout.Width(120)) &&
                    EditorUtility.DisplayDialog("Replace wardrobe pictures", $"Replace {visible.Count} wardrobe picture(s) with the new renders?", "Replace", "Cancel"))
                    Accept(visible);
                if (GUILayout.Button("Discard all shown", GUILayout.Width(120))) Discard(visible);
                GUI.enabled = true;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            Row accept = null, discard = null;
            foreach (var r in visible)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    Picture(r.oldTex, "Current");
                    Picture(r.newTex, "New");
                    using (new EditorGUILayout.VerticalScope())
                    {
                        GUILayout.Label(r.slot + " / " + r.label, EditorStyles.boldLabel);
                        GUILayout.Label(r.oldTex ? (r.changed ? "Different from the wardrobe" : "Same as the wardrobe") : "Not in the wardrobe yet", EditorStyles.miniLabel);
                        if (GUILayout.Button("Use new picture", GUILayout.Width(130))) accept = r;
                        if (GUILayout.Button("Discard candidate", GUILayout.Width(130))) discard = r;
                        if (GUILayout.Button("Show files", GUILayout.Width(130))) EditorUtility.RevealInFinder(r.candidate);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
            if (accept != null) Accept(new List<Row> { accept });
            if (discard != null) Discard(new List<Row> { discard });
        }

        void Picture(Texture2D texture, string caption)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(size)))
            {
                var rect = GUILayoutUtility.GetRect(size, size);
                EditorGUI.DrawRect(rect, new Color(.15f, .11f, .22f));
                if (texture) GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
                else GUI.Label(rect, "none", EditorStyles.centeredGreyMiniLabel);
                GUILayout.Label(caption, EditorStyles.centeredGreyMiniLabel);
            }
        }

        void Accept(List<Row> chosen)
        {
            foreach (var r in chosen)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(r.current));
                File.Copy(r.candidate, r.current, true);
                File.Delete(r.candidate);
            }
            WardrobeThumbnailRig.ImportAccepted();
            Debug.Log($"Wardrobe pictures: replaced {chosen.Count}.");
            Reload();
        }

        void Discard(List<Row> chosen)
        {
            foreach (var r in chosen) if (File.Exists(r.candidate)) File.Delete(r.candidate);
            Reload();
        }
    }
}
