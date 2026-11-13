using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.CandyAudio.Editor
{
    [Serializable]
    public sealed class CandyPreviewPacket
    {
        public int schemaVersion = 1, cueId, frameCount, columns, rows, frameWidth = 512, frameHeight = 288;
        public float fps = 12, eventTimeSeconds;
        public string title, atlasFile, atlasSha256, gifFile, gifSha256, sourceSha256;
        public string sourceCommit, scene, map, mode, captureKind, source, action, target, listenerRole;
        public string triggerEvidence, semanticReview, reviewer, viewerUrl;
    }

    public sealed class CandySoundPreview : ScriptableObject
    {
        public Texture2D atlas;
        public CandyPreviewPacket packet;
        static Dictionary<int, string> cache;
        public static void Invalidate() => cache = null;
        static void Index()
        {
            if (cache == null)
            {
                cache = new Dictionary<int, string>();
                foreach (string guid in AssetDatabase.FindAssets("t:CandySoundPreview"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid), name = Path.GetFileNameWithoutExtension(path);
                    if (name.StartsWith("Cue-") && int.TryParse(name.Substring(4), out int id)) cache[id] = path;
                }
            }
        }
        public static bool Has(int cueId) { Index(); return cache.ContainsKey(cueId); }
        public static CandySoundPreview Find(int cueId) { Index(); return cache.TryGetValue(cueId, out var path) ? AssetDatabase.LoadAssetAtPath<CandySoundPreview>(path) : null; }
        public static Rect FrameUv(CandyPreviewPacket packet, int frame)
        {
            frame = Mathf.Clamp(frame, 0, packet.frameCount - 1);
            return new Rect((frame % packet.columns) / (float)packet.columns,
                1f - (frame / packet.columns + 1f) / packet.rows, 1f / packet.columns, 1f / packet.rows);
        }
        public static List<string> Validate(CandyPreviewPacket p, IEnumerable<int> ids)
        {
            var errors = new List<string>();
            if (p == null) { errors.Add("Missing preview manifest."); return errors; }
            if (p.schemaVersion != 1) errors.Add("Unsupported preview schema.");
            if (!ids.Contains(p.cueId)) errors.Add("Unknown cue ID: " + p.cueId);
            if (float.IsNaN(p.fps) || float.IsInfinity(p.fps) || p.fps < 8 || p.fps > 24 || p.frameCount < p.fps || p.frameCount > p.fps * 5 + .01f) errors.Add("Preview must be 1–5 seconds at 8–24 fps.");
            if (p.columns < 1 || p.rows < 1 || p.columns > 16 || p.rows > 16 || p.columns * p.rows < p.frameCount) errors.Add("Invalid atlas grid.");
            if (p.frameWidth < 128 || p.frameHeight < 72 || (long)p.frameWidth * p.columns > 8192 || (long)p.frameHeight * p.rows > 8192) errors.Add("Invalid atlas dimensions.");
            if (float.IsNaN(p.eventTimeSeconds) || float.IsInfinity(p.eventTimeSeconds) || p.eventTimeSeconds < 0 || p.fps <= 0 || p.eventTimeSeconds >= p.frameCount / p.fps) errors.Add("The event marker must lie inside the clip.");
            if (p.captureKind != "gameplay" && p.captureKind != "staged-gameplay") errors.Add("Use actual gameplay or explicitly staged gameplay, never a concept render.");
            if (p.semanticReview != "verified" || string.IsNullOrWhiteSpace(p.reviewer)) errors.Add("A named agent must review the actual action before import.");
            foreach (string value in new[] { p.sourceCommit, p.scene, p.map, p.mode, p.source, p.action, p.target, p.listenerRole, p.triggerEvidence, p.sourceSha256 })
                if (string.IsNullOrWhiteSpace(value)) { errors.Add("Capture context and source/event evidence are required."); break; }
            foreach (string value in new[] { p.sourceSha256, p.atlasSha256, p.gifSha256 })
                if (value == null || value.Length != 64 || value.Any(c => !Uri.IsHexDigit(c))) { errors.Add("Valid SHA-256 checksums are required."); break; }
            if (!string.IsNullOrEmpty(p.viewerUrl) && (!Uri.TryCreate(p.viewerUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https")) errors.Add("Viewer links must use HTTPS.");
            return errors;
        }
        static string PackageFile(string folder, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidDataException("Package file must be relative.");
            string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Package file escapes its folder.");
            return path;
        }
        static void CheckHash(string path, string expected)
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 64 * 1024 * 1024) throw new InvalidDataException("Missing or oversized preview file: " + Path.GetFileName(path));
            using var hash = SHA256.Create(); using var stream = File.OpenRead(path);
            string actual = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Preview checksum mismatch: " + Path.GetFileName(path));
        }
        public static CandySoundPreview Import(string manifestPath, CandyAudioBank bank, string testFolder = null)
        {
            var packet = JsonUtility.FromJson<CandyPreviewPacket>(File.ReadAllText(manifestPath));
            var errors = Validate(packet, CandySoundCatalog.Build(bank).Select(e => (int)e.id));
            if (errors.Count > 0) throw new InvalidDataException(string.Join(" ", errors));
            string folder = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
            string atlas = PackageFile(folder, packet.atlasFile), gif = PackageFile(folder, packet.gifFile);
            if (!atlas.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || !gif.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Expected PNG atlas and GIF.");
            CheckHash(atlas, packet.atlasSha256); CheckHash(gif, packet.gifSha256);
            var probe = new Texture2D(2, 2);
            try
            {
                if (!probe.LoadImage(File.ReadAllBytes(atlas)) || probe.width != packet.frameWidth * packet.columns || probe.height != packet.frameHeight * packet.rows)
                    throw new InvalidDataException("Atlas pixels do not match the manifest.");
            }
            finally { DestroyImmediate(probe); }
            string parent = ProjectPath.FolderOf("CandySoundPreview.cs").TrimEnd('/');
            if (!parent.Contains("/Editor")) throw new InvalidOperationException("Preview storage must be Editor-only.");
            string destination = testFolder ?? parent + "/TriggerPreviews";
            if (testFolder != null && (!testFolder.StartsWith(parent + "/StudioPreviewCheck-", StringComparison.Ordinal) || !AssetDatabase.IsValidFolder(testFolder))) throw new InvalidOperationException("Invalid preview test folder.");
            if (!AssetDatabase.IsValidFolder(destination)) AssetDatabase.CreateFolder(parent, "TriggerPreviews");
            string texturePath = destination + "/Cue-" + packet.cueId + "-" + packet.atlasSha256 + ".png";
            if (!File.Exists(texturePath)) File.Copy(atlas, texturePath);
            else CheckHash(texturePath, packet.atlasSha256);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Default; importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false; importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear; importer.maxTextureSize = 8192;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            string assetPath = destination + "/Cue-" + packet.cueId + ".asset";
            var result = AssetDatabase.LoadAssetAtPath<CandySoundPreview>(assetPath);
            if (!result) { result = CreateInstance<CandySoundPreview>(); AssetDatabase.CreateAsset(result, assetPath); Undo.RegisterCreatedObjectUndo(result, "Import trigger preview"); }
            else Undo.RecordObject(result, "Replace trigger preview");
            result.packet = packet; result.atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            EditorUtility.SetDirty(result); AssetDatabase.SaveAssetIfDirty(result); Invalidate();
            return result;
        }
    }

    public sealed class CandyPreviewPlayer : VisualElement
    {
        readonly Image image;
        readonly CandySoundPreview clip;
        readonly IVisualElementScheduledItem clock;
        readonly Label time;
        readonly Button toggle;
        readonly Slider scrub;
        double started;
        int frame;
        bool playing, focused;
        public CandyPreviewPlayer(CandySoundPreview value)
        {
            clip = value; AddToClassList("trigger-player");
            image = new Image { image = value.atlas, scaleMode = ScaleMode.ScaleToFit }; image.AddToClassList("trigger-image"); Add(image);
            var bar = new VisualElement(); bar.AddToClassList("preview-controls");
            toggle = new Button(() => SetPlaying(!playing)) { text = "▶ Watch" }; bar.Add(toggle);
            scrub = new Slider(0, value.packet.frameCount - 1); scrub.AddToClassList("preview-scrub");
            scrub.RegisterValueChangedCallback(e => { SetPlaying(false); SetFrame(Mathf.RoundToInt(e.newValue)); }); bar.Add(scrub);
            time = new Label(); time.AddToClassList("preview-time"); bar.Add(time); Add(bar);
            clock = schedule.Execute(Tick).Every(Mathf.RoundToInt(1000f / value.packet.fps)); clock.Pause();
            RegisterCallback<DetachFromPanelEvent>(_ => clock.Pause());
            RegisterCallback<AttachToPanelEvent>(_ => { if (playing && focused) clock.Resume(); });
            SetFrame(Mathf.RoundToInt(value.packet.eventTimeSeconds * value.packet.fps));
        }
        void SetFrame(int value)
        {
            frame = Mathf.Clamp(value, 0, clip.packet.frameCount - 1);
            image.uv = CandySoundPreview.FrameUv(clip.packet, frame);
            scrub.SetValueWithoutNotify(frame);
            time.text = (frame / clip.packet.fps).ToString("0.0") + " / " + (clip.packet.frameCount / clip.packet.fps).ToString("0.0") + "s";
        }
        public void SetFocused(bool value) { focused = value; if (value && playing) { started = EditorApplication.timeSinceStartup - frame / clip.packet.fps; clock.Resume(); } else clock.Pause(); }
        public void SetPlaying(bool value)
        {
            playing = value; toggle.text = value ? "Ⅱ Pause" : "▶ Watch";
            started = EditorApplication.timeSinceStartup - frame / clip.packet.fps;
            if (value && focused) clock.Resume(); else clock.Pause();
        }
        void Tick() { if (playing && focused) SetFrame((int)((EditorApplication.timeSinceStartup - started) * clip.packet.fps) % clip.packet.frameCount); }
        public void ShowEvent() { SetPlaying(false); SetFrame(Mathf.RoundToInt(clip.packet.eventTimeSeconds * clip.packet.fps)); }
    }
}
