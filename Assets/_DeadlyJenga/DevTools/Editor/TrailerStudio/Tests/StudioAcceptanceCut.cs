using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // "Cut" suite: track warnings that do not hide each other, cue ids, variation recipes that make a try
    // again, a cue exactly on the branch frame, and the export files checked against their formats.
    //   -executeMethod DeadlyJenga.DevTools.Editor.TrailerStudio.StudioAcceptance.RunCut
    public static partial class StudioAcceptance
    {
        static IEnumerator<int> CutTests()
        {
            var stage = StudioRun.Stage;
            var set = stage.set;
            var tape = stage.tape;
            int fps = set.fps;
            set.marks.Clear(); set.cues.Clear();
            set.length = 3;
            StudioLibrary.Save(set);
            StudioRun.Refresh();
            var worldTrack = tape.FindType(WorldTrack.Id);

            BadgeTests();
            CueIdTests();
            OldRecipeTests();
            ExportFormatTests();
            yield return 0;

            // Two loose blocks near the stage spot, pushed in different directions.
            var loose = Enumerable.Range(0, stage.blocks.Length).Where(i => stage.blocks[i] && stage.blocks[i].gameObject.activeSelf && !stage.blocks[i].isKinematic)
                .OrderBy(i => (stage.blocks[i].position - stage.anchor).sqrMagnitude).Take(2).ToList();
            if (loose.Count < 2) { Result("Setup: two loose blocks near the stage spot", false, "The map has fewer than two loose blocks near the stage spot."); yield break; }
            int hero = loose[0], other = loose[1];

            // ---- A cue exactly on the branch frame fires in the branch, and the frames up to it stay the old take's ----
            StudioRun.Record(WorldPass(stage, "Calm baseline"));
            foreach (int wait in UntilHeld()) yield return wait;
            var calm = worldTrack.ActiveTake;
            int branchFrame = Mathf.RoundToInt(1.5f * fps);
            var onFrame = new StudioCue { at = branchFrame / (float)fps, action = PushCue.Id, blocks = new List<int> { hero }, direction = new Vector3(1, .3f, 0), strength = 6 };
            set.cues.Add(onFrame);
            StudioLibrary.Save(set);
            StudioRun.Record(WorldPass(stage, "Branch on the cue's frame", branchFrame));
            foreach (int wait in UntilHeld()) yield return wait;
            var branched = worldTrack.ActiveTake;
            var log = StudioRun.CueLog.ToList();
            int same = 0;
            for (int f = 0; f <= branchFrame && f < calm.frames.Count && f < branched.frames.Count; f++)
            {
                if (!calm.frames[f].SequenceEqual(branched.frames[f])) break;
                same++;
            }
            WorldTrack.Positions(calm, calm.End - 1, out var calmEnd, out _);
            WorldTrack.Positions(branched, branched.End - 1, out var pushedEnd, out var pushedOn);
            float heroMoved = pushedOn[hero] ? Vector3.Distance(calmEnd[hero], pushedEnd[hero]) : 99;
            int differs = WorldTrack.FirstDifference(calm, branched, 5);
            Result("Cut 1. A push placed exactly on the branch frame fires in the branch; frames up to the branch frame are the old take's",
                branched != calm && log.Any(l => l.Contains("push")) && same == branchFrame + 1 && heroMoved > .3f && differs > branchFrame && differs <= branchFrame + 4,
                $"Cue at {onFrame.at:0.000} s = frame {branchFrame}, branch at frame {branchFrame}. Cue log: {string.Join("; ", log)}. Frames 0 to {branchFrame}: {same} of {branchFrame + 1} byte-for-byte the old take's. " +
                $"The pushed block ended {heroMoved:0.00} m from where it rests in the calm take; the takes first differ by more than 5 mm at frame {differs}.");

            // ---- The same cue with a variation run from that frame: it is varied and fires ----
            float originalAt = onFrame.at, originalStrength = onFrame.strength;
            StudioVariations.Start(new StudioVariations.Settings { runs = 1, fromFrame = branchFrame, forceJitter = .3f, timeJitter = .1f, seed = 11 });
            foreach (int wait in UntilVariationsDone()) yield return wait;
            var tried = StudioVariations.Results(tape, StudioVariations.LastGroup).FirstOrDefault();
            var recipe = tried == null ? null : StudioVariations.RecipeOf(tried);
            var saved = recipe?.cues.FirstOrDefault(c => c.id == onFrame.id);
            var variedLog = StudioRun.CueLog.ToList();
            Result("Cut 2. A variation run from the branch frame varies the cue sitting exactly on that frame, and it fires",
                saved != null && saved.at >= branchFrame / (float)fps - .0001f && Mathf.Abs(saved.strength - originalStrength) > .001f && variedLog.Any(l => l.Contains("push"))
                    && onFrame.at == originalAt && onFrame.strength == originalStrength && onFrame.jitter == 0,
                saved == null ? "No recipe with this cue was kept." :
                $"Recipe: the cue ran at {saved.at:0.000} s (not before the branch at {branchFrame / (float)fps:0.000} s) with strength {saved.strength:0.00} instead of {originalStrength:0.00}. Cue log of the try: {string.Join("; ", variedLog)}. " +
                $"After the run the Set's cue is back to {onFrame.at:0.000} s, strength {onFrame.strength:0.00}, jitter {onFrame.jitter}.");

            // ---- A variation from a fresh map, put back into the Set and recorded again as the whole set ----
            set.cues.Clear();
            set.cues.Add(new StudioCue { at = 1, action = PushCue.Id, blocks = new List<int> { hero }, direction = new Vector3(1, .3f, 0), strength = 6 });
            set.cues.Add(new StudioCue { at = 1.6f, action = PushCue.Id, blocks = new List<int> { other }, direction = new Vector3(0, .3f, 1), strength = 5 });
            StudioLibrary.Save(set);
            StudioVariations.Start(new StudioVariations.Settings { runs = 2, fromFrame = 0, forceJitter = .3f, timeJitter = .15f, seed = 3 });
            foreach (int wait in UntilVariationsDone()) yield return wait;
            var tries = StudioVariations.Results(tape, StudioVariations.LastGroup);
            var pick = tries.LastOrDefault();
            if (pick == null) { Result("Cut 3. A variation's recipe is applied by cue id", false, "The variation run kept no takes."); yield break; }
            bool copyOk = RecipeOnCopy(set, pick, out string copyNote);
            int seedBefore = set.seed;
            string message = StudioVariations.ApplyRecipe(set, pick);
            Result("Cut 3. A variation's recipe is applied by cue id: reordered cues get their own timings, applying twice changes nothing, missing cues are counted",
                copyOk, copyNote + $" On the Set itself: \"{message}\"");
            StudioLibrary.Save(set);
            StudioRun.RecordWholeSet();
            foreach (int wait in UntilHeld()) yield return wait;
            var again = worldTrack.ActiveTake;
            FinalDistance(pick, again, out float worst, out float mean, out int count);
            Result("Cut 4. Recording the whole set after applying a fresh-map variation's recipe makes that try again",
                again != pick && count > 0 && worst < .05f,
                $"\"{pick.name}\" (seed {StudioVariations.RecipeOf(pick)?.seed}; the Set's seed was {seedBefore}, now {set.seed}). Over {count} blocks, the re-recorded world's last frame is on average {mean * 1000:0.0} mm " +
                $"and at most {worst * 1000:0.0} mm from the try's last frame. Only a try from a fresh map can match this way: one that branched from an older take started from that take's pose.");

            Result("Runtime errors during the Studio tests", exceptions == 0, exceptions == 0 && errorLines.Count == 0 ? "None." : $"{exceptions} exceptions. First messages: {string.Join(" // ", errorLines)}");
            StudioRun.SaveTape();
        }

        static IEnumerable<int> UntilVariationsDone()
        {
            float started = Time.realtimeSinceStartup;
            while (StudioVariations.Running)
            {
                if (Time.realtimeSinceStartup - started > 240) { StudioVariations.Cancel(); break; }
                yield return 0;
            }
            foreach (int wait in UntilHeld()) yield return wait;
        }

        // How far apart two world takes' last frames are, over the blocks present in both.
        static void FinalDistance(StudioTake a, StudioTake b, out float worst, out float mean, out int count)
        {
            worst = 0; mean = 0; count = 0;
            if (a == null || b == null) return;
            if (!WorldTrack.Positions(a, a.End - 1, out var pa, out var oa) || !WorldTrack.Positions(b, b.End - 1, out var pb, out var ob)) return;
            for (int i = 0; i < pa.Length && i < pb.Length; i++)
            {
                if (!oa[i] || !ob[i]) continue;
                float d = Vector3.Distance(pa[i], pb[i]);
                worst = Mathf.Max(worst, d); mean += d; count++;
            }
            if (count > 0) mean /= count;
        }

        // Apply a take's recipe to a copy of the Set whose cues are in another order (a decoy cue in front,
        // and the first two real cues swapped); check every cue got its own values, twice, and that a removed
        // cue is reported as missing. The Set itself is not changed.
        static bool RecipeOnCopy(StudioSet set, StudioTake take, out string detail)
        {
            var recipe = StudioVariations.RecipeOf(take);
            if (recipe == null || recipe.cues.Count == 0) { detail = "The take has no recipe by cue id."; return false; }
            var copy = set.Clone();
            var decoy = new StudioCue { at = .25f, action = PushCue.Id, strength = 1.25f };
            if (copy.cues.Count >= 2) { var first = copy.cues[0]; copy.cues[0] = copy.cues[1]; copy.cues[1] = first; }
            copy.cues.Insert(0, decoy);
            string said = StudioVariations.ApplyRecipe(copy, take);
            bool right = recipe.cues.All(r => copy.cues.Any(c => c.id == r.id && c.at == r.at && c.strength == r.strength && c.jitter == 0))
                && decoy.at == .25f && decoy.strength == 1.25f;
            string Values(StudioSet s) => string.Join(",", s.cues.Select(c => $"{c.id}:{c.at.ToString("R", CultureInfo.InvariantCulture)}:{c.strength.ToString("R", CultureInfo.InvariantCulture)}:{c.jitter}")) + ":" + s.seed;
            string once = Values(copy);
            string saidAgain = StudioVariations.ApplyRecipe(copy, take);
            bool twice = Values(copy) == once;
            var less = set.Clone();
            less.cues.RemoveAll(c => c.id == recipe.cues[0].id);
            string saidMissing = StudioVariations.ApplyRecipe(less, take);
            bool missing = saidMissing.Contains("1 cue is no longer in the Set");
            detail = $"Recipe holds {recipe.cues.Count} cues by id: {string.Join(", ", recipe.cues.Select(r => $"{r.id} at {r.at:0.000} s strength {r.strength:0.000}"))}. " +
                $"On a copy with the cues swapped and a decoy in front: each cue got its own values: {right} (\"{said}\"); applying again changed nothing: {twice} (\"{saidAgain}\"); " +
                $"with one cue removed: \"{saidMissing}\".";
            return right && twice && missing;
        }

        // ---- Tests that need no stage ----

        static void BadgeTests()
        {
            var track = new StudioTrack { id = "ada", type = SurvivorTrack.Id, name = "Ada" };
            StudioBadges.Set(track, StudioBadges.CouldNotPlay, "a broken frame");
            StudioBadges.Set(track, StudioBadges.Retimed, "after 2.0 s this take no longer lines up with the world.");
            StudioBadges.Set(track, StudioBadges.Stale, "the world was re-recorded and first differs at 1.00 s (frame 30).");
            bool three = StudioBadges.Parts(track).Count == 3 && track.badge.StartsWith("Stale:");
            StudioBadges.Set(track, StudioBadges.Stale, "first differs at 1.50 s.");   // replaced, not added
            bool replaced = StudioBadges.Parts(track).Count(p => p.StartsWith(StudioBadges.Stale)) == 1 && track.badge.Contains("1.50 s");
            StudioBadges.Clear(track, StudioBadges.Retimed);
            bool cleared = !StudioBadges.Has(track, StudioBadges.Retimed) && StudioBadges.Has(track, StudioBadges.Stale) && StudioBadges.Has(track, StudioBadges.CouldNotPlay);
            // Saved and loaded through a real tape file: the binary layout is unchanged (one string per track).
            var tape = new StudioTape();
            tape.tracks.Add(track);
            var old = new StudioTrack { id = "ben", type = SurvivorTrack.Id, name = "Ben", badge = "Stale: an old tape's single warning" };
            tape.tracks.Add(old);
            string file = Path.Combine(Folder, "badges.djtape");
            tape.Save(file);
            var loaded = StudioTape.Load(file);
            var back = loaded.Find("ada"); var oldBack = loaded.Find("ben");
            bool kept = back != null && back.badge == track.badge && StudioBadges.Has(back, StudioBadges.Stale) && StudioBadges.Has(back, StudioBadges.CouldNotPlay);
            bool oldReads = oldBack != null && StudioBadges.Parts(oldBack).Count == 1 && StudioBadges.Has(oldBack, StudioBadges.Stale);
            StudioBadges.ClearAfterRecording(back);
            bool recorded = string.IsNullOrEmpty(back.badge);
            Result("Cut 0a. Track warnings do not hide each other and survive saving",
                three && replaced && cleared && kept && oldReads && recorded,
                $"Stale + retimed + could-not-play on one track: {StudioBadges.Parts(track).Count + 1} lines before clearing one, shown stale first: {three}; setting stale again replaced it: {replaced}; " +
                $"clearing retimed left the other two: {cleared}. Saved and loaded through StudioTape.Save/Load: {kept} (\"{back?.badge?.Replace("\n", " / ")}\"). A single warning as older tapes have it reads as one: {oldReads}. A new recording clears all three: {recorded}.");
        }

        static void CueIdTests()
        {
            // A Set saved before cues had ids, and one with a copied cue.
            string json = "{\"title\":\"old\",\"cues\":[{\"at\":1,\"action\":\"push\"},{\"id\":\"\",\"at\":2},{\"id\":\"same\",\"at\":3},{\"id\":\"same\",\"at\":4}]}";
            var old = JsonUtility.FromJson<StudioSet>(json);
            string fromJson = string.Join(",", old.cues.Select(c => c.id == null ? "null" : c.id == "" ? "empty" : "set"));
            int added = old.EnsureCueIds();
            var ids = old.cues.Select(c => c.id).ToList();
            bool unique = ids.All(i => !string.IsNullOrEmpty(i)) && ids.Distinct().Count() == ids.Count && ids[2] == "same";
            int again = old.EnsureCueIds();
            Result("Cut 0b. Cues without an id, or sharing one, get their own id",
                unique && again == 0 && added >= 2,
                $"Ids as loaded by JsonUtility: {fromJson}. EnsureCueIds gave {added} new ids ({string.Join(", ", ids)}); the first cue named \"same\" kept its id; a second call changed {again}.");
        }

        static void OldRecipeTests()
        {
            var set = new StudioSet();
            set.cues.Add(new StudioCue { at = 1, strength = 4 });
            set.cues.Add(new StudioCue { at = 2, strength = 5 });
            var oldRecipe = new StudioVariations.Recipe(); oldRecipe.cues.Clear();
            oldRecipe.at.AddRange(new[] { 1.1f, 2.2f }); oldRecipe.jitter.AddRange(new[] { .5f, 0f });
            var take = new StudioTake { meta = JsonUtility.ToJson(new StudioBranch.WorldNote { recipe = JsonUtility.ToJson(oldRecipe) }) };
            string fits = StudioVariations.ApplyRecipe(set, take);
            bool applied = set.cues[0].at == 1.1f && Mathf.Abs(set.cues[0].strength - 6) < .0001f && set.cues[1].at == 2.2f && set.cues[1].strength == 5;
            set.cues.Add(new StudioCue { at = 3 });
            float before = set.cues[0].strength;
            string refused = StudioVariations.ApplyRecipe(set, take);
            bool untouched = set.cues[0].strength == before && set.cues[2].at == 3;
            Result("Cut 0c. A recipe saved by older builds (by cue order) still applies when the count matches, and only then",
                applied && untouched && refused.Contains("no longer fit"), $"Matching count: \"{fits}\". One cue more: \"{refused}\" and nothing changed: {untouched}.");
        }

        // ---- The export, checked against the formats without an editor ----

        // A render folder made by hand (shot records, empty movie files, real WAV headers), so every
        // dissolve case can be written and checked in a second.
        static void ExportFormatTests()
        {
            string root = Path.Combine(Folder, "export");
            if (Directory.Exists(root)) Directory.Delete(root, true);
            var set = new StudioSet { title = "Format check", fps = 30, length = 6 };
            string[] names = { "Wide", "Close", "Top" };
            foreach (string name in names)
            {
                var camera = new StudioCameraTrack { name = name };
                set.cameras.Add(camera);
                string folder = Path.Combine(root, StudioRender.ShotFolder(camera));
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "shot.json"), JsonUtility.ToJson(new StudioRender.ShotRecord { name = name, id = camera.id, format = "mp4", frames = 90, fps = 30, width = 1920, height = 1080 }));
                File.WriteAllBytes(Path.Combine(folder, name + ".mp4"), new byte[0]);
            }
            string sound = Path.Combine(root, "tape.wav"), music = Path.Combine(root, "music.wav");
            WriteSilence(sound, 48000, 2, 48000 * 6 + 7);   // 6 s and 7 samples: a length that is not whole frames
            WriteSilence(music, 44100, 2, 44100 * 2);        // shorter than the cut
            set.music = music;
            // Wide 0-2 s; Close from 1 s with a 0.5 s dissolve (enough frames both sides); Top from 0 s with a dissolve (no frames before its in point).
            set.cuts.Add(new StudioCut { camera = set.cameras[0].id, from = 0, to = 2 });
            set.cuts.Add(new StudioCut { camera = set.cameras[1].id, from = 1, to = 2, dissolve = .5f });
            set.cuts.Add(new StudioCut { camera = set.cameras[2].id, from = 0, to = 1.5f, dissolve = .4f });
            List<string> problems = null, edlProblems = null;
            string xmlFile = "", edlFile = "", error = "";
            try
            {
                xmlFile = StudioExport.WriteFcpxml(set, root, sound, out problems);
                edlFile = StudioExport.WriteEdl(set, root, out edlProblems);
            }
            catch (Exception e) { error = e.Message; }
            var xmlFaults = error == "" ? CheckFcpxml(xmlFile) : new List<string> { error };
            var edlFaults = error == "" ? CheckEdl(edlFile, out _) : new List<string> { error };
            string[] edl = error == "" ? File.ReadAllLines(edlFile) : new string[0];
            // The expected dissolve pair, worked out by hand: Wide ends 7 frames early (0:00:01:23), the zero-length
            // cut line sits there, and Close comes in 7 frames before its in point (0:00:00:23) with D 015.
            string cutLine = "002  AX       V     C        00:00:01:23 00:00:01:23 00:00:01:23 00:00:01:23";
            string dissolveLine = "002  AX       V     D    015 00:00:00:23 00:00:02:00 00:00:01:23 00:00:03:00";
            bool pair = edl.Contains(cutLine) && edl.Contains(dissolveLine);
            bool shortened = problems != null && problems.Any(p => p.Contains("\"Top\"") && p.Contains("straight cut"));
            string xml = error == "" ? File.ReadAllText(xmlFile) : "";
            bool audio = xml.Contains("duration=\"288007/48000s\"") && xml.Contains("audioRate=\"48000\"") && xml.Contains("duration=\"2s\" audioRate=\"44100\"") && xml.Contains("name=\"FFVideoFormat1080p30\"");
            bool musicClamped = xml.Contains("name=\"Music\" offset=\"0s\" start=\"0s\" duration=\"2s\"");
            Result("Cut 5. FCPXML and EDL written from a hand-made render folder pass the format checks (two-line dissolve, audio lengths, a dissolve with no spare frames becomes a cut)",
                error == "" && xmlFaults.Count == 0 && edlFaults.Count == 0 && pair && shortened && audio && musicClamped,
                (error != "" ? "Export failed: " + error + ". " : "") +
                $"FCPXML structure check against the FCPXML DTD rules: {(xmlFaults.Count == 0 ? "no faults" : string.Join("; ", xmlFaults))}. EDL check: {(edlFaults.Count == 0 ? "no faults" : string.Join("; ", edlFaults))}. " +
                $"Dissolve pair as expected: {pair}. Audio assets carry their WAV length and rate, video format named FFVideoFormat1080p30: {audio}; music clip clamped to its 2 s file: {musicClamped}. " +
                $"Exporter notes: {string.Join(" / ", (problems ?? new List<string>()).Concat(edlProblems ?? new List<string>()).Distinct())}. Files: export/.");
        }

        static void WriteSilence(string file, int rate, int channels, int samples)
        {
            using (var w = new BinaryWriter(File.Create(file)))
            {
                int data = samples * channels * 2;
                w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + data); w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)channels); w.Write(rate);
                w.Write(rate * channels * 2); w.Write((short)(channels * 2)); w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data")); w.Write(data); w.Write(new byte[data]);
            }
        }

        // FCPXML: the element and attribute rules of Apple's FCPXML DTD for every element the Studio writes
        // (allowed children and their order, required and allowed attributes, ids and references, time values),
        // and the timing rules an editor applies on import (clips back to back, transitions inside media handles).
        static readonly Dictionary<string, string[]> FcpAttributes = new Dictionary<string, string[]>
        {
            ["fcpxml"] = new[] { "version" },
            ["resources"] = new string[0], ["library"] = new[] { "location", "colorProcessing" },
            ["event"] = new[] { "name", "uid" }, ["project"] = new[] { "name", "uid", "id", "modDate" },
            ["format"] = new[] { "id", "name", "frameDuration", "fieldOrder", "width", "height", "paspH", "paspV", "colorSpace", "projection", "stereoscopic" },
            ["asset"] = new[] { "id", "name", "uid", "start", "duration", "hasVideo", "format", "hasAudio", "videoSources", "audioSources", "audioChannels", "audioRate",
                "customLUTOverride", "colorSpaceOverride", "projectionOverride", "stereoscopicOverride", "auxVideoFlags" },
            ["media-rep"] = new[] { "kind", "sig", "src", "suggestedFilename" },
            ["sequence"] = new[] { "format", "duration", "tcStart", "tcFormat", "audioLayout", "audioRate", "renderFormat", "keywords" },
            ["spine"] = new[] { "lane", "offset", "name", "format" },
            ["asset-clip"] = new[] { "ref", "lane", "offset", "name", "start", "duration", "enabled", "srcEnable", "audioStart", "audioDuration", "format", "tcStart", "tcFormat", "modDate", "audioRole", "videoRole" },
            ["transition"] = new[] { "name", "offset", "duration" },
        };
        static readonly Dictionary<string, string[]> FcpRequired = new Dictionary<string, string[]>
        {
            ["fcpxml"] = new[] { "version" }, ["format"] = new[] { "id" }, ["asset"] = new[] { "id" }, ["media-rep"] = new[] { "src" },
            ["sequence"] = new[] { "format" }, ["asset-clip"] = new[] { "ref" }, ["transition"] = new[] { "duration" },
        };
        // Children allowed in each element, in the order the DTD puts them (only elements the Studio writes).
        static readonly Dictionary<string, string[]> FcpChildren = new Dictionary<string, string[]>
        {
            ["fcpxml"] = new[] { "resources", "library" }, ["resources"] = new[] { "asset|effect|format|media|locator" },
            ["library"] = new[] { "event|smart-collection" }, ["event"] = new[] { "project|asset-clip|clip" }, ["project"] = new[] { "sequence" },
            ["asset"] = new[] { "media-rep", "metadata" }, ["media-rep"] = new string[0], ["format"] = new string[0],
            ["sequence"] = new[] { "note", "spine", "metadata" }, ["spine"] = new[] { "asset-clip|clip|gap|transition|audio|video" },
            ["asset-clip"] = new[] { "note", "asset-clip|audio|video|clip|spine", "marker", "filter-video", "filter-audio", "metadata" },
            ["transition"] = new[] { "filter-video", "filter-audio", "marker", "metadata" },
        };

        static readonly Regex FcpTime = new Regex(@"^(0|\d+|\d+/\d+)s$");

        static long Frames(string time, int fps)
        {
            var m = Regex.Match(time ?? "", @"^(\d+)(?:/(\d+))?s$");
            if (!m.Success) return long.MinValue;
            long top = long.Parse(m.Groups[1].Value); long bottom = m.Groups[2].Success ? long.Parse(m.Groups[2].Value) : 1;
            return top * fps % bottom == 0 ? top * fps / bottom : long.MinValue;   // not a whole frame
        }

        public static List<string> CheckFcpxml(string file)
        {
            var faults = new List<string>();
            var document = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore }))
                document.Load(reader);
            if (!File.ReadAllText(file).Contains("<!DOCTYPE fcpxml>")) faults.Add("no <!DOCTYPE fcpxml>");
            var root = document.DocumentElement;
            if (root?.Name != "fcpxml" || root.GetAttribute("version") != "1.9") faults.Add("root must be <fcpxml version=\"1.9\">");
            var ids = new Dictionary<string, XmlElement>();
            foreach (XmlElement e in document.SelectNodes("//*"))
            {
                if (!FcpAttributes.TryGetValue(e.Name, out var allowed)) { faults.Add($"<{e.Name}> is not an element the check knows"); continue; }
                foreach (XmlAttribute a in e.Attributes)
                {
                    if (!allowed.Contains(a.Name)) faults.Add($"<{e.Name}> has attribute {a.Name}, which the DTD does not allow");
                    if ((a.Name == "offset" || a.Name == "start" || a.Name == "duration" || a.Name == "frameDuration" || a.Name == "tcStart") && !FcpTime.IsMatch(a.Value))
                        faults.Add($"<{e.Name} {a.Name}=\"{a.Value}\"> is not an FCPXML time");
                }
                if (FcpRequired.TryGetValue(e.Name, out var required))
                    foreach (string r in required) if (!e.HasAttribute(r)) faults.Add($"<{e.Name}> is missing required attribute {r}");
                if (e.HasAttribute("id") && (e.Name == "format" || e.Name == "asset"))
                {
                    if (ids.ContainsKey(e.GetAttribute("id"))) faults.Add("duplicate id " + e.GetAttribute("id"));
                    ids[e.GetAttribute("id")] = e;
                }
                if (e.Name == "media-rep" && e.HasAttribute("kind") && e.GetAttribute("kind") != "original-media" && e.GetAttribute("kind") != "proxy-media") faults.Add("media-rep kind " + e.GetAttribute("kind"));
                if (e.Name == "sequence" && e.HasAttribute("audioRate") && !new[] { "32k", "44.1k", "48k", "88.2k", "96k", "176.4k", "192k" }.Contains(e.GetAttribute("audioRate"))) faults.Add("sequence audioRate " + e.GetAttribute("audioRate"));
                if ((e.Name == "sequence" || e.Name == "asset-clip") && e.HasAttribute("tcFormat") && e.GetAttribute("tcFormat") != "DF" && e.GetAttribute("tcFormat") != "NDF") faults.Add("tcFormat " + e.GetAttribute("tcFormat"));
                if (e.Name == "asset-clip" && e.ParentNode.Name == "asset-clip" && (e.GetAttribute("lane") == "" || e.GetAttribute("lane") == "0")) faults.Add("an anchored asset-clip needs a lane other than 0");
                // Children: allowed, and in the DTD's order.
                if (FcpChildren.TryGetValue(e.Name, out var order))
                {
                    int at = 0;
                    foreach (XmlNode child in e.ChildNodes)
                    {
                        if (!(child is XmlElement c)) continue;
                        int slot = Array.FindIndex(order, o => o.Split('|').Contains(c.Name));
                        if (slot < 0) faults.Add($"<{c.Name}> may not be inside <{e.Name}>");
                        else if (slot < at) faults.Add($"<{c.Name}> comes too late inside <{e.Name}>");
                        else at = slot;
                    }
                }
                if (e.Name == "asset" && e.SelectNodes("media-rep").Count == 0) faults.Add("an asset needs at least one media-rep");
                if (e.Name == "project" && e.SelectNodes("sequence").Count != 1) faults.Add("a project holds exactly one sequence");
            }
            foreach (XmlElement e in document.SelectNodes("//*[@ref or @format]"))
                foreach (string name in new[] { "ref", "format" })
                    if (e.HasAttribute(name) && !ids.ContainsKey(e.GetAttribute(name))) faults.Add($"<{e.Name} {name}=\"{e.GetAttribute(name)}\"> points at nothing");
            if (faults.Count > 0) return faults;

            // Timing, as an editor reads it.
            var format = (XmlElement)document.SelectSingleNode("//resources/format");
            int fps = (int)Math.Round(1.0 / ParseSeconds(format.GetAttribute("frameDuration")));
            var sequence = (XmlElement)document.SelectSingleNode("//sequence");
            long expectedOffset = 0;
            XmlElement previous = null, pendingTransition = null;
            foreach (XmlElement item in sequence.SelectSingleNode("spine").ChildNodes.OfType<XmlElement>())
            {
                if (item.Name == "transition")
                {
                    if (previous == null) faults.Add("a transition before the first clip");
                    pendingTransition = item; continue;
                }
                long offset = Frames(item.GetAttribute("offset"), fps), start = Frames(item.GetAttribute("start"), fps), duration = Frames(item.GetAttribute("duration"), fps);
                if (offset != expectedOffset) faults.Add($"clip \"{item.GetAttribute("name")}\" starts at frame {offset}, expected {expectedOffset} (clips on the storyline are back to back)");
                var asset = ids[item.GetAttribute("ref")];
                long assetLength = Frames(asset.GetAttribute("duration"), fps);
                if (start < 0 || duration <= 0 || assetLength >= 0 && start + duration > assetLength) faults.Add($"clip \"{item.GetAttribute("name")}\" uses frames {start} to {start + duration} of a {assetLength}-frame asset");
                if (pendingTransition != null)
                {
                    long tOffset = Frames(pendingTransition.GetAttribute("offset"), fps), tLength = Frames(pendingTransition.GetAttribute("duration"), fps);
                    long head = offset - tOffset, tail = tLength - head;
                    long prevStart = Frames(previous.GetAttribute("start"), fps), prevLength = Frames(previous.GetAttribute("duration"), fps);
                    long prevAsset = Frames(ids[previous.GetAttribute("ref")].GetAttribute("duration"), fps);
                    if (head < 0 || tail < 0) faults.Add("a transition that does not straddle the edit point");
                    if (start - head < 0) faults.Add($"the dissolve into \"{item.GetAttribute("name")}\" needs {head} frames before its in point; there are {start}");
                    if (prevStart + prevLength + tail > prevAsset) faults.Add($"the dissolve out of \"{previous.GetAttribute("name")}\" needs {tail} frames after its out point");
                    pendingTransition = null;
                }
                // Connected sound: inside its own asset.
                foreach (XmlElement child in item.SelectNodes("asset-clip"))
                {
                    var audioAsset = ids[child.GetAttribute("ref")];
                    double end = ParseSeconds(child.GetAttribute("start")) + ParseSeconds(child.GetAttribute("duration"));
                    if (audioAsset.HasAttribute("duration") && end > ParseSeconds(audioAsset.GetAttribute("duration")) + 1e-6)
                        faults.Add($"\"{child.GetAttribute("name")}\" under \"{item.GetAttribute("name")}\" runs past the end of its file");
                    if (!audioAsset.HasAttribute("duration")) faults.Add($"audio asset \"{audioAsset.GetAttribute("name")}\" has no duration");
                    if (Frames(child.GetAttribute("offset"), fps) < start) faults.Add($"\"{child.GetAttribute("name")}\" is anchored before its parent's start");
                }
                expectedOffset = offset + duration;
                previous = item;
            }
            if (Frames(sequence.GetAttribute("duration"), fps) != expectedOffset) faults.Add($"the sequence is {sequence.GetAttribute("duration")} but its clips add up to {expectedOffset} frames");
            return faults;
        }

        static double ParseSeconds(string time)
        {
            var m = Regex.Match(time ?? "", @"^(\d+)(?:/(\d+))?s$");
            return m.Success ? double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) / (m.Groups[2].Success ? double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : 1) : double.NaN;
        }

        // CMX 3600: TITLE and FCM lines, event lines in the standard field order, record times back to back,
        // source and record lengths equal, and a dissolve written as a zero-length cut line followed by the
        // D line with the same event number. 'events' = how many event numbers the list has.
        static readonly Regex EdlLine = new Regex(@"^(\d{3})  (\S{1,8}) +(V|A|A2|AA|AA/V|B) +(C|D|W\d{3}) +(\d{3})? *(\d\d:\d\d:\d\d:\d\d) (\d\d:\d\d:\d\d:\d\d) (\d\d:\d\d:\d\d:\d\d) (\d\d:\d\d:\d\d:\d\d)$");

        public static List<string> CheckEdl(string file, out int events)
        {
            var faults = new List<string>();
            var lines = File.ReadAllLines(file);
            events = 0;
            if (lines.Length < 2 || !lines[0].StartsWith("TITLE: ")) faults.Add("the first line must be TITLE:");
            if (!lines.Any(l => l == "FCM: NON-DROP FRAME" || l == "FCM: DROP FRAME")) faults.Add("no FCM line");
            int fps = 30;
            var numbers = new HashSet<string>();
            long recordAt = -1;
            string[] last = null;
            foreach (string line in lines)
            {
                if (line.Length == 0 || !char.IsDigit(line[0])) { if (line.Length > 0 && !line.StartsWith("*") && !line.StartsWith("TITLE:") && !line.StartsWith("FCM:")) faults.Add("unknown line: " + line); continue; }
                var m = EdlLine.Match(line);
                if (!m.Success) { faults.Add("not a CMX 3600 event line: " + line); continue; }
                long Tc(int group) { var p = m.Groups[group].Value.Split(':').Select(int.Parse).ToArray(); return ((p[0] * 60L + p[1]) * 60 + p[2]) * fps + p[3]; }
                long sIn = Tc(6), sOut = Tc(7), rIn = Tc(8), rOut = Tc(9);
                if (sOut - sIn != rOut - rIn) faults.Add("source and record lengths differ: " + line);
                if (recordAt >= 0 && rIn != recordAt) faults.Add($"record time jumps from {recordAt} to {rIn}: " + line);
                string kind = m.Groups[4].Value;
                if (kind == "D")
                {
                    if (!m.Groups[5].Success) faults.Add("a dissolve without its length: " + line);
                    if (last == null || last[0] != m.Groups[1].Value || last[1] != "C" || last[2] != "0") faults.Add("a D line must follow a zero-length C line with the same event number: " + line);
                }
                else if (m.Groups[5].Success) faults.Add("a cut with a length field: " + line);
                numbers.Add(m.Groups[1].Value);
                recordAt = rOut;
                last = new[] { m.Groups[1].Value, kind, (rOut - rIn).ToString() };
            }
            events = numbers.Count;
            return faults;
        }
    }
}
