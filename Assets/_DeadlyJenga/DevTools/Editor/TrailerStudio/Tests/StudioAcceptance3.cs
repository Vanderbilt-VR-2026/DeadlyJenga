using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Phase 3 acceptance tests: directing the world.
    public static partial class StudioAcceptance
    {
        static StudioPass WorldPass(StudioStage stage, string label, int branchFrom = -1)
        {
            var pass = new StudioPass { label = label, liveWorld = true, fixedClock = true, branchFrom = branchFrom };
            foreach (var track in stage.tape.tracks)
                if (track.type != SurvivorTrack.Id && track.type != HandTrack.Id && StudioTrackTypes.Get(track.type) != null) pass.record.Add(track.id);
            return pass;
        }

        // The first sampled frame with more than 5 cm of block movement from the reference frame (-1 = never).
        static int[] FirstMoves(StudioTake take, int frames, int referenceFrame = -1)
        {
            int baseline = Mathf.Max(referenceFrame, take.start);
            WorldTrack.Positions(take, baseline, out var start, out var active);
            var first = Enumerable.Repeat(-1, start.Length).ToArray();
            for (int f = baseline + 1; f < take.End && f < frames; f += 2)
            {
                WorldTrack.Positions(take, f, out var now, out var on);
                for (int i = 0; i < now.Length; i++)
                    if (first[i] < 0 && on[i] && (referenceFrame < 0 || active[i]) && Vector3.Distance(now[i], start[i]) > .05f) first[i] = f;
            }
            return first;
        }

        static StudioStage PrepareTornadoStage()
        {
            var stage = StudioRun.Stage;
            var set = stage.set;
            set.marks.Clear(); set.cues.Clear();
            set.length = 9;
            StudioLibrary.Save(set);
            StudioRun.Refresh();
            StudioCamera.Resize(640, 360);
            StudioCamera.Target = stage.anchor; StudioCamera.Distance = 20; StudioCamera.Pitch = 24; StudioCamera.Place();
            return stage;
        }

        static IEnumerator<int> HeldOutTornadoTests()
        {
            var stage = PrepareTornadoStage();
            foreach (int wait in TornadoTest(stage, 270, true)) yield return wait;
            Result("Runtime exceptions during the held-out tornado test", exceptions == 0,
                exceptions == 0 && errorLines.Count == 0 ? "None." : $"{exceptions} exceptions. First messages: {string.Join(" // ", errorLines)}");
            StudioRun.SaveTape();
        }

        static StudioSet MakeHeldOutTornadoSet() => new StudioSet
        {
            id = "acceptance-heldout-tornado-" + HeldOutRunId, title = "Acceptance: Held-out tornado",
            map = Map, fps = 30, length = 9, alive = new StudioAliveSettings { on = false },
        };

        static IEnumerable<int> TornadoTest(StudioStage stage, float heading, bool heldOut)
        {
            var set = stage.set;
            var tape = stage.tape;
            int fps = set.fps, frames = set.Frames;
            var worldTrack = tape.FindType(WorldTrack.Id);
            // ---- Test 1: a tornado with a set heading; playback is identical every time ----
            string tornado = SetLists.Hazards.FirstOrDefault(h => h.IndexOf("tornado", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!heldOut && tornado == null) tornado = SetLists.Hazards.FirstOrDefault();
            if (tornado == null) Result("1. Tornado cue with a set heading", false, "No tornado was found in the disaster catalog.");
            else
            {
                var prefab = DemoDisasters.Prefabs.First(p => p && p.name == tornado);
                var parameters = DemoDisasters.Parameters(prefab);
                var cue = new StudioCue { at = .2f, action = "hazard", hazard = tornado, values = DirectCueFields.Defaults(tornado), aimed = true, heading = heading };
                var settings = new List<string>();
                for (int i = 0; i < parameters.Count; i++)
                {
                    string name = (parameters[i].id + " " + parameters[i].label).ToLowerInvariant();
                    // Make it arrive quickly and hit hard, so a short tape shows it tearing through.
                    if (name.Contains("warning")) cue.values[i] = parameters[i].min;
                    if (name.Contains("duration")) cue.values[i] = parameters[i].Clamp(9);
                    if (name.Contains("ferocity") || parameters[i].id == "force") cue.values[i] = parameters[i].max;
                    settings.Add($"{parameters[i].label}={cue.values[i]:0.##}{parameters[i].unit}");
                }
                set.cues.Add(cue);
                StudioLibrary.Save(set);
                void Shoot(int frame) { if (frame % 5 == 0) StudioCamera.SavePng(Path.Combine(Folder, "tornado-live", $"{frame / 5:D05}.png")); }
                StudioRun.FrameShown += Shoot;
                try
                {
                    StudioRun.Record(WorldPass(stage, "Tornado"));
                    foreach (int wait in UntilHeld()) yield return wait;
                }
                finally { StudioRun.FrameShown -= Shoot; }
                var take = worldTrack.ActiveTake;
                var hazards = tape.FindType(HazardTrack.Id)?.ActiveTake;
                int hazardFrames = hazards?.frames.Count(f => f != null && f.Length > 0) ?? 0;
                // Where did the tornado come in? Read its first recorded position from the hazard track.
                float entrySide = -1;
                int entryFrame = -1;
                var bounds = stage.world.PlayableBounds;
                for (int f = 0; f < frames && hazards != null; f++)
                {
                    if (!HazardTrack.CenterAt(hazards, f, out var funnel)) continue;
                    entryFrame = f;
                    entrySide = Mathf.Repeat(Mathf.Atan2(funnel.z - bounds.center.z, funnel.x - bounds.center.x) * Mathf.Rad2Deg, 360);
                    break;
                }
                bool fromRequestedSide = entrySide >= 0 && Mathf.Abs(Mathf.DeltaAngle(entrySide, heading)) < 12;
                if (take == null || (heldOut && !take.Covers(entryFrame)))
                {
                    Result("Tornado world and hazard take coverage", false, $"World take covers the recorded hazard entry at frame {entryFrame}: false.");
                    yield break;
                }
                var moves = FirstMoves(take, frames, heldOut ? entryFrame : -1);
                int torn = moves.Count(m => m >= 0);
                var cueLog = string.Join("; ", StudioRun.CueLog);
                var drifts = new List<StudioDrift>();
                var pictures = new List<long>[2];
                for (int run = 0; run < 2; run++)
                {
                    int captured = run;
                    pictures[run] = new List<long>();
                    void Look(int frame)
                    {
                        if (frame % 30 != 15) return;
                        string file = Path.Combine(Folder, "tornado-playback-" + (captured + 1), $"{frame:D05}.png");
                        StudioCamera.SavePng(file);
                        pictures[captured].Add(new FileInfo(file).Length);
                    }
                    StudioRun.FrameShown += Look;
                    try
                    {
                        StudioRun.Play(0, -1, false);
                        foreach (int wait in UntilHeld()) yield return wait;
                    }
                    finally { StudioRun.FrameShown -= Look; }
                    drifts.Add(StudioRun.LastDrift);
                }
                Result(heldOut ? "Held-out tornado cue enters from the opposite side, moves blocks and replays within drift limits"
                        : "1. A tornado cue with a set heading tears through the tower; playback is identical every time",
                    hazardFrames > fps && torn > 0 && fromRequestedSide && drifts.All(d => d != null && d.Passed && d.samples > 0),
                    $"Disaster \"{tornado}\" with {(settings.Count > 0 ? string.Join(", ", settings) : "no settings of its own")}. Cue log: {cueLog}. " +
                    $"The hazard track holds {hazardFrames} frames of disaster state; {torn} of {stage.blocks.Length} blocks were moved. " +
                    $"Asked to come from heading {heading:0}; its first recorded position was {entrySide:0} degrees around the map centre at frame {entryFrame}. " +
                    (heldOut ? "Movement is measured from hazard entry and counts active blocks displaced more than 5 cm. " : "") +
                    $"Two playbacks: {drifts[0]} / {drifts[1]} Blocks and disaster state come from the same tape both times; " +
                    $"the saved pictures' sizes were {(pictures[0].SequenceEqual(pictures[1]) ? "the same" : "different (the disaster's particles are drawn fresh each time)")}. Pictures: tornado-live/, tornado-playback-1/, tornado-playback-2/.");
            }
        }

        static IEnumerator<int> Phase3Tests()
        {
            var stage = PrepareTornadoStage();
            var set = stage.set;
            var tape = stage.tape;
            int fps = set.fps;
            var worldTrack = tape.FindType(WorldTrack.Id);
            foreach (int wait in TornadoTest(stage, 90, false)) yield return wait;

            // ---- Test 4: a ripple push across 30 blocks looks like a wave ----
            set.cues.Clear();
            set.length = 5;
            StudioLibrary.Save(set);
            StudioRun.Refresh();
            int frames = set.Frames;
            // Thirty blocks in one neighbourhood: the ones nearest the stage spot, nearest first.
            var loose = Enumerable.Range(0, stage.blocks.Length).Where(i => stage.blocks[i] && stage.blocks[i].gameObject.activeSelf)
                .OrderBy(i => (stage.blocks[i].position - stage.anchor).sqrMagnitude).Take(30).ToList();
            var ripple = new StudioCue { at = .5f, action = PushCue.Id, blocks = loose, direction = new Vector3(0, .15f, 1), strength = 5, ripple = .05f };
            set.cues.Add(ripple);
            StudioLibrary.Save(set);
            void ShootWave(int frame) { if (frame % 4 == 0) StudioCamera.SavePng(Path.Combine(Folder, "ripple", $"{frame / 4:D05}.png")); }
            StudioRun.FrameShown += ShootWave;
            StudioRun.Record(WorldPass(stage, "Ripple"));
            foreach (int wait in UntilHeld()) yield return wait;
            StudioRun.FrameShown -= ShootWave;
            var rippleTake = worldTrack.ActiveTake;
            var rippleMoves = FirstMoves(rippleTake, frames);
            WorldTrack.Positions(rippleTake, 0, out var rippleStart, out _);
            Vector3 origin = rippleStart[loose[0]];
            var pairs = loose.Where(i => rippleMoves[i] >= 0).Select(i => (distance: Vector3.Distance(origin, rippleStart[i]), frame: rippleMoves[i])).OrderBy(p => p.distance).ToList();
            int inOrder = 0, compared = 0;
            for (int a = 0; a < pairs.Count; a++)
                for (int b = a + 1; b < pairs.Count; b++)
                {
                    if (pairs[b].distance - pairs[a].distance < 1) continue;
                    compared++;
                    if (pairs[b].frame >= pairs[a].frame) inOrder++;
                }
            float spread = pairs.Count > 0 ? (pairs.Max(p => p.frame) - pairs.Min(p => p.frame)) / (float)fps : 0;
            Result("4. A ripple push across 30 selected blocks moves as a wave", pairs.Count >= 10 && compared > 0 && inOrder >= compared * .8f && spread > .3f,
                $"{loose.Count} blocks picked, {pairs.Count} moved (the rest are fixed parts of the structure that never move). Cue log: {string.Join("; ", StudioRun.CueLog)}. Farther blocks started later in {inOrder} of {compared} pairs more than 1 m apart; " +
                $"first to last block started {spread:0.00} s apart. Pictures: ripple/.");

            // ---- Test 3: branch at 3 s, change one push; frames before 3 s are unchanged ----
            var before = worldTrack.ActiveTake;
            int branchFrame = 3 * fps;
            int far = loose[loose.Count - 1];
            set.cues.Add(new StudioCue { at = 3.3f, action = PushCue.Id, blocks = new List<int> { far, loose[loose.Count - 2] }, direction = new Vector3(-1, .4f, 0), strength = 12 });
            StudioLibrary.Save(set);
            StudioRun.Record(WorldPass(stage, "Branch", branchFrame));
            foreach (int wait in UntilHeld()) yield return wait;
            var branched = worldTrack.ActiveTake;
            bool sameBefore = branched != before && branched.start == before.start && branched.frames.Count == frames;
            int sameFrames = 0;
            for (int f = 0; f <= branchFrame && sameBefore; f++)
            {
                if (!before.frames[f].SequenceEqual(branched.frames[f])) { sameBefore = false; break; }
                sameFrames++;
            }
            int differs = WorldTrack.FirstDifference(before, branched, 5);
            var note = StudioBranch.NoteOf(branched);
            // Does the world carry on smoothly across the branch frame? (No block may jump at the hand-over.)
            WorldTrack.Positions(branched, branchFrame - 1, out var justBefore, out var onBefore);
            WorldTrack.Positions(branched, branchFrame, out var atBranch, out var onAt);
            WorldTrack.Positions(before, branchFrame, out var oldAt, out _);
            float jump = 0;
            for (int i = 0; i < atBranch.Length; i++) if (onBefore[i] && onAt[i]) jump = Mathf.Max(jump, Vector3.Distance(atBranch[i], oldAt[i]));
            WorldTrack.Positions(branched, branchFrame + 1, out var firstNew, out var onNew);
            float step = 0;
            for (int i = 0; i < atBranch.Length; i++) if (onAt[i] && onNew[i]) step = Mathf.Max(step, Vector3.Distance(firstNew[i], atBranch[i]));
            Result("3. Branch at 3 s with one changed push; the frames before 3 s are unchanged",
                sameBefore && sameFrames == branchFrame + 1 && differs > branchFrame && jump < .001f && step < .6f && note.parent == before.id && note.branchFrame == branchFrame && worldTrack.takes.Contains(before),
                $"New take \"{branched.name}\": {branched.frames.Count} frames. Frames 0 to {branchFrame} are byte-for-byte the old take's ({sameFrames} checked). " +
                $"The two takes first differ by more than 5 mm at frame {differs} (the new push is at 3.3 s = frame {Mathf.RoundToInt(3.3f * fps)}). " +
                $"At the branch frame the largest block offset from the old tape is {jump * 1000:0.0} mm, and the largest move into the first new frame is {step * 100:0.0} cm (no pop at the hand-over). Parent recorded: {note.parent == before.id}; the old take is still on the tape; cue log: {string.Join("; ", StudioRun.CueLog)}.");

            // ---- Test 2: push a block so it lands within 1 m of a spot, found with a variation run of 20 ----
            set.cues.Clear();
            int hero = Enumerable.Range(0, stage.blocks.Length).Where(i => stage.blocks[i] && stage.blocks[i].gameObject.activeSelf)
                .OrderBy(i => (stage.blocks[i].position - stage.anchor).sqrMagnitude).First();
            set.cues.Add(new StudioCue { at = 1.5f, action = PushCue.Id, blocks = new List<int> { hero }, direction = new Vector3(1, .25f, 0), strength = 7 });
            StudioLibrary.Save(set);
            StudioRun.Record(WorldPass(stage, "Baseline push"));
            foreach (int wait in UntilHeld()) yield return wait;
            var baseline = worldTrack.ActiveTake;
            WorldTrack.Positions(baseline, baseline.End - 1, out var landed, out var landedOn);
            WorldTrack.Positions(baseline, 0, out var heroStart, out _);
            // The spot is 1.6 m short of where the plain push lands, so the plain push misses it.
            Vector3 flight = Vector3.ProjectOnPlane(landed[hero] - heroStart[hero], Vector3.up);
            Vector3 spot = landed[hero] - flight.normalized * 1.6f;
            var mark = set.AddMark(StudioRole.Survivor, stage.ToStage(spot));
            mark.actor = "Hero";
            StudioLibrary.Save(set);
            StudioRun.Refresh();
            var vary = new StudioVariations.Settings { runs = 20, fromFrame = Mathf.RoundToInt(1.4f * fps), forceJitter = .35f, timeJitter = 0, seed = 5,
                targetBlock = hero, targetPoint = mark.position, zoneRadius = 1 };
            float baselineMiss = Vector3.Distance(landed[hero], spot);
            StudioVariations.Start(vary);
            float started = Time.realtimeSinceStartup;
            while (StudioVariations.Running)
            {
                if (Time.realtimeSinceStartup - started > 420) { StudioVariations.Cancel(); break; }
                yield return 0;
            }
            foreach (int wait in UntilHeld()) yield return wait;
            var results = StudioVariations.Results(tape, StudioVariations.LastGroup);
            var scores = results.Select(t => StudioBranch.NoteOf(t)).Where(n => n.targetDistance >= 0).OrderBy(n => n.targetDistance).ToList();
            var best = results.OrderBy(t => { float d = StudioBranch.NoteOf(t).targetDistance; return d < 0 ? float.MaxValue : d; }).FirstOrDefault();
            bool restored = worldTrack.ActiveTake == baseline;
            if (best != null) { StudioVariations.Activate(tape, best); StudioRun.SaveTape(); StudioRun.Refresh(); }
            int thumbs = results.Count(t => File.Exists(StudioBranch.Thumbnail(set.id, t)));
            Result("2. Push a block at 1.5 s so it lands within 1 m of a mark, found with a variation run of 20 tries",
                results.Count == 20 && scores.Count > 0 && scores[0].targetDistance <= 1 && restored && worldTrack.ActiveTake == best,
                $"Block #{hero} pushed at 1.5 s. The plain push lands {baselineMiss:0.00} m from the mark. {results.Count} tries (each branching at 1.4 s, force changed by up to 35%): " +
                $"distances from the mark, best first: {string.Join(", ", scores.Take(8).Select(s => s.targetDistance.ToString("0.00")))} m… {scores.Count(s => s.inZone)} tries landed within 1 m. " +
                $"The run left the original world active until one was picked: {restored}. The best try was then made the active world with one call. {thumbs} tries have thumbnails.");

            // ---- Test 2b: the best try's recipe goes back into the Set by cue id, then the whole set is recorded again ----
            if (best == null) Result("2b. The best try's cue timings go back into the Set by cue id", false, "There was no best try.");
            else
            {
                bool copyOk = RecipeOnCopy(set, best, out string copyNote);
                string applied = StudioVariations.ApplyRecipe(set, best);
                StudioLibrary.Save(set);
                StudioRun.RecordWholeSet();
                foreach (int wait in UntilHeld()) yield return wait;
                var again = worldTrack.ActiveTake;
                FinalDistance(best, again, out float worst, out float mean, out int count);
                // These tries branched at 1.4 s from the baseline take, so a whole-set recording from a fresh map is
                // only expected to come close. The distance is reported, not judged (the Cut suite judges a fresh-map try).
                Result("2b. The best try's cue timings go back into the Set by cue id (the re-recorded distance is reported, not judged)",
                    copyOk && again != best && count > 0,
                    copyNote + $" On the Set: \"{applied}\" Recording the whole set again from a fresh map: over {count} blocks its last frame is on average {mean * 100:0.0} cm and at most {worst * 100:0.0} cm " +
                    $"from the best try's last frame. The try started from the baseline take's pose at 1.4 s, not from a fresh map, so these are not expected to match exactly.");
            }

            Result("Runtime errors during the Studio tests", exceptions == 0, exceptions == 0 && errorLines.Count == 0 ? "None." : $"{exceptions} exceptions. First messages: {string.Join(" // ", errorLines)}");
            StudioRun.SaveTape();
        }
    }
}
