using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.CandyAudio.Editor
{
    public static class CandySoundPreviewChecks
    {
        static readonly string Report = Path.Combine("Logs", "SoundStudio", "preview-checks.txt");
        static void Check(bool value, string name)
        {
            if (!value) throw new InvalidOperationException(name);
            File.AppendAllText(Report, "PASS " + name + "\n");
        }
        static string Hash(byte[] bytes)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        [MenuItem("Tools/Candy Audio/Check Sound Studio Previews")]
        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, "Sound Studio preview/editor checks · " + DateTime.UtcNow.ToString("O") + "\nSynthetic fixture, NOT gameplay evidence.\n");
            string parent = ProjectPath.FolderOf("CandySoundPreviewChecks.cs").TrimEnd('/');
            string folder = parent + "/StudioPreviewCheck-" + Guid.NewGuid().ToString("N");
            string package = Path.Combine("Logs", "SoundStudio", "preview-fixture");
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder)); Directory.CreateDirectory(package);
            Texture2D texture = null;
            try
            {
                var bank = Resources.Load<CandyAudioBank>("CandyAudio/Default");
                var entries = CandySoundCatalog.Build(bank); var ids = entries.Select(e => (int)e.id).ToArray();
                Check(entries.Count >= 316 && ids.Distinct().Count() == ids.Length, "All existing sound routes remain available with unique IDs");
                var personal = CandySoundCaptureBriefs.For(entries.Single(e => e.id == CandySound.PersonalShot));
                Check(personal.listenerRole.StartsWith("Victim") && personal.audibleMoment.Contains("NOT the weapon firing"), "Projectile damage brief identifies victim impact, not firing");
                var kraken = entries.Where(e => e.id >= CandySound.KrakenRise && e.id <= CandySound.KrakenRetreat).Select(CandySoundCaptureBriefs.For).ToArray();
                Check(kraken.Length == 10 && kraken.Select(b => b.action).Distinct().Count() == 10, "All ten Kraken phases have distinct contextual actions");
                Check(kraken.Single(b => b.cueId == (int)CandySound.KrakenSlam).audibleMoment.Contains("report the mismatch"), "Slam brief requires real contact and timing-mismatch reporting");
                Check(File.Exists(ProjectPath.Find("SOUND_PREVIEW_CAPTURE_GUIDE.md")), "Capture guide is discoverable");
                CandySoundCaptureBriefs.Export(Path.Combine("Logs", "SoundStudio", "capture-briefs"), bank);
                Check(File.Exists(Path.Combine("Logs", "SoundStudio", "capture-briefs", "cue-" + (int)CandySound.KrakenSlam + ".md")), "Per-cue handoff export writes current source/action evidence");

                texture = new Texture2D(1024, 144); var png = texture.EncodeToPNG();
                var gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
                File.WriteAllBytes(Path.Combine(package, "atlas.png"), png); File.WriteAllBytes(Path.Combine(package, "preview.gif"), gif);
                var p = new CandyPreviewPacket { cueId = ids[0], frameCount = 16, columns = 8, rows = 2, frameWidth = 128, frameHeight = 72,
                    fps = 8, eventTimeSeconds = 1, atlasFile = "atlas.png", gifFile = "preview.gif", atlasSha256 = Hash(png), gifSha256 = Hash(gif), sourceSha256 = Hash(png),
                    sourceCommit = "synthetic-test-only", scene = "fixture", map = "fixture", mode = "fixture", captureKind = "staged-gameplay",
                    source = "fixture", action = "fixture", target = "fixture", listenerRole = "fixture", triggerEvidence = "Synthetic import fixture, never accepted as gameplay", semanticReview = "verified", reviewer = "automated structural check" };
                Check(CandySoundPreview.Validate(p, ids).Count == 0, "Structurally valid preview metadata accepted");
                p.semanticReview = "pending"; Check(CandySoundPreview.Validate(p, ids).Count > 0, "Unreviewed metadata rejected"); p.semanticReview = "verified";
                p.captureKind = "concept"; Check(CandySoundPreview.Validate(p, ids).Count > 0, "Concept images rejected as gameplay previews"); p.captureKind = "staged-gameplay";
                p.fps = float.NaN; Check(CandySoundPreview.Validate(p, ids).Count > 0, "Non-finite frame rates rejected"); p.fps = 8;
                p.eventTimeSeconds = 2; Check(CandySoundPreview.Validate(p, ids).Count > 0, "Out-of-clip event marker rejected"); p.eventTimeSeconds = 1;
                p.frameCount = 80; Check(CandySoundPreview.Validate(p, ids).Count > 0, "Clips longer than five seconds rejected"); p.frameCount = 16;
                var first = CandySoundPreview.FrameUv(p, 0); var last = CandySoundPreview.FrameUv(p, 15);
                Check(first.y == .5f && last.y == 0 && last.x == .875f, "Atlas frame coordinates preserve top-row-first ordering");
                string manifest = Path.Combine(package, "preview.json"); File.WriteAllText(manifest, JsonUtility.ToJson(p, true));
                var imported = CandySoundPreview.Import(manifest, bank, folder);
                Check(imported && imported.atlas.width == 1024 && imported.atlas.height == 144, "Explicit import creates correctly sized editor-only atlas and registry");
                string asset = AssetDatabase.GetAssetPath(imported), guid = AssetDatabase.AssetPathToGUID(asset);
                Check(asset.Contains("/Editor/"), "Preview data is outside runtime resources");
                var replacement = CandySoundPreview.Import(manifest, bank, folder);
                Check(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(replacement)) == guid, "Reimport preserves cue registry GUID");
                var player = new CandyPreviewPlayer(imported); player.ShowEvent();
                Check(player.Q<Image>().uv == CandySoundPreview.FrameUv(p, 8), "Show event selects exact marked frame");
                p.atlasFile = "../atlas.png"; File.WriteAllText(manifest, JsonUtility.ToJson(p));
                bool rejected = false; try { CandySoundPreview.Import(manifest, bank, folder); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "Path traversal rejected");
                p.atlasFile = "atlas.png"; p.atlasSha256 = new string('0', 64); File.WriteAllText(manifest, JsonUtility.ToJson(p));
                rejected = false; try { CandySoundPreview.Import(manifest, bank, folder); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "Tampered atlas checksum rejected");

                CandySoundStudioWindow.Open(); var window = EditorWindow.GetWindow<CandySoundStudioWindow>();
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(CandySoundStudioWindow).GetField("search", flags).SetValue(window, "Kraken Slam");
                window.Refresh();
                Check(window.rootVisualElement.Query<Label>().ToList().Any(l => l.text == "Kraken Slam"), "Global search finds a different section's sound");
                var buttons = window.rootVisualElement.Query<Button>().ToList().Select(b => b.text).ToArray();
                Check(buttons.Contains("Capture brief") && buttons.Contains("Replace recording…") && buttons.Contains("Save changes") && buttons.Contains("+ New sound"), "Main audition, replacement, creation and capture controls remain accessible");
                typeof(CandySoundStudioWindow).GetField("search", flags).SetValue(window, "");
                typeof(CandySoundStudioWindow).GetField("section", flags).SetValue(window, "Disasters / Kraken");
                typeof(CandySoundStudioWindow).GetField("selectedId", flags).SetValue(window, (int)CandySound.KrakenSlam);
                window.CreateGUI();
                File.AppendAllText(Report, "COMPLETE\n"); Debug.Log("Sound Studio preview checks passed: " + Report);
            }
            catch (Exception error) { File.AppendAllText(Report, "FAIL " + error + "\n"); Debug.LogException(error); }
            finally
            {
                if (texture) UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.DeleteAsset(folder); CandySoundPreview.Invalidate();
                string cache = parent + "/__pycache__";
                if (Directory.Exists(cache) && Directory.GetFiles(cache).All(p => Path.GetFileName(p).StartsWith("build_trigger_preview.", StringComparison.Ordinal))) AssetDatabase.DeleteAsset(cache);
            }
        }
    }
}
