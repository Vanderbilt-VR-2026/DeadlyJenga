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

    }
}
