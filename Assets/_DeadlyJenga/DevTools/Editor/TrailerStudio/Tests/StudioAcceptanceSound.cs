using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Sound acceptance tests: sound kept per world take, sound for branches, and the sound of a
    // performed take (what the performer heard), all the way to the exported cut.
    //   -executeMethod DeadlyJenga.DevTools.Editor.TrailerStudio.StudioAcceptance.RunSound
    public static partial class StudioAcceptance
    {
        public static void RunSound() => Begin("Sound");

        static int LoudestIn(StudioWave.Clip clip, long from = 0, long to = long.MaxValue) => clip == null ? 0 : StudioWave.Loudest(clip, from, to);
        static StudioWave.Clip ReadOrNull(string file) { try { return File.Exists(file) ? StudioWave.Read(file) : null; } catch { return null; } }

        static IEnumerator<int> SoundTests()
        {
            var stage = StudioRun.Stage;
            var set = stage.set;
            var tape = stage.tape;
            set.length = 4; set.cues.Clear();
            set.cues.Add(new StudioCue { at = 1, action = "collapse", strength = 6 });
            foreach (var mark in set.marks)
                if (StudioStage.SurfaceBelow(stage.ToWorld(mark.position), out var surface)) mark.position = stage.ToStage(surface);
            StudioLibrary.Save(set);
            StudioRun.Refresh();
            int fps = set.fps, frames = set.Frames;
            var worldTrack = tape.FindType(WorldTrack.Id);

            // ---- a) A whole-set recording with a collapse keeps its sound with its world take ----
            var watch = System.Diagnostics.Stopwatch.StartNew();
            StudioRun.RecordWholeSet();
            foreach (int wait in UntilHeld()) yield return wait;
            double wholeSeconds = watch.Elapsed.TotalSeconds;
            var takeA = worldTrack.ActiveTake;
            string fileA = takeA == null ? "" : StudioRender.TakeAudioFile(set.id, takeA.id);
            var mixA = ReadOrNull(fileA);
            var worldStemA = ReadOrNull(Path.Combine(Path.GetDirectoryName(fileA ?? "") ?? "", "stems", "World.wav"));
            int stemsA = Directory.Exists(Path.Combine(Path.GetDirectoryName(fileA ?? "") ?? "", "stems")) ? Directory.GetFiles(Path.Combine(Path.GetDirectoryName(fileA), "stems"), "*.wav").Length : 0;
            long expectedA = mixA == null ? -1 : StudioWave.SampleAt(frames, mixA.rate, fps);
            bool leftover = Directory.Exists(Path.Combine(StudioLibrary.TapeFolder(set.id), "audio", "recording"));
            Result("a. A whole-set recording keeps its sound with its world take",
                mixA != null && mixA.Frames == expectedA && LoudestIn(mixA) > 0 && LoudestIn(worldStemA) > 0 && StudioRun.LastRecordingHasSound && StudioRender.AudioFile(set.id) == fileA && !leftover,
                $"World take {takeA?.id}: sound at audio/takes/{takeA?.id}/tape.wav, {mixA?.Frames ?? 0} sample frames at {mixA?.rate ?? 0} Hz, {mixA?.channels ?? 0} channels " +
                $"(expected {expectedA} = {frames} frames / {fps} fps). Loudest mix sample {LoudestIn(mixA)}, World stem (block impacts) loudest {LoudestIn(worldStemA)}, {stemsA} stems. " +
                $"AudioFile points at it: {StudioRender.AudioFile(set.id) == fileA}. Temporary recording folder left behind: {leftover}. Recording took {wholeSeconds:0.0} s.");

            // ---- b) A branch at 2 s with an extra push after it ----
            int branchFrame = 2 * fps;
            var near = Enumerable.Range(0, stage.blocks.Length).Where(i => stage.blocks[i] && stage.blocks[i].gameObject.activeSelf)
                .OrderBy(i => (stage.blocks[i].position - stage.anchor).sqrMagnitude).Take(4).ToList();
            set.cues.Add(new StudioCue { at = 2.3f, action = PushCue.Id, blocks = near, direction = new Vector3(1, .4f, .2f), strength = 12 });
            StudioLibrary.Save(set);
            watch.Restart();
            StudioRun.Record(WorldPass(stage, "Sound branch", branchFrame));
            foreach (int wait in UntilHeld()) yield return wait;
            double branchSeconds = watch.Elapsed.TotalSeconds;
            var takeB = worldTrack.ActiveTake;
            string fileB = takeB == null ? "" : StudioRender.TakeAudioFile(set.id, takeB.id);
            var mixB = ReadOrNull(fileB);
            bool isBranch = takeB != null && takeB != takeA && StudioBranch.NoteOf(takeB).parent == takeA.id;
            long split = mixB == null ? 0 : StudioWave.SampleAt(branchFrame + 1, mixB.rate, fps);
            long sameBefore = 0, firstDifference = -1, differentAfter = 0;
            bool before = mixA != null && mixB != null && mixA.channels == mixB.channels && mixA.rate == mixB.rate;
            if (before)
            {
                int ch = mixB.channels;
                for (long i = 0; i < split * ch; i++)
                {
                    if (i >= mixA.samples.Length || mixA.samples[i] != mixB.samples[i]) { before = false; break; }
                    sameBefore++;
                }
                for (long i = split * ch; i < mixB.samples.Length && i < mixA.samples.Length; i++)
                    if (mixA.samples[i] != mixB.samples[i]) { differentAfter++; if (firstDifference < 0) firstDifference = i / ch; }
            }
            // The stems are joined the same way.
            var worldStemB = ReadOrNull(Path.Combine(Path.GetDirectoryName(fileB ?? "") ?? "", "stems", "World.wav"));
            bool stemSame = worldStemA != null && worldStemB != null && worldStemB.Frames == mixB?.Frames;
            for (long i = 0; stemSame && i < split * worldStemB.channels; i++) if (worldStemA.samples[i] != worldStemB.samples[i]) stemSame = false;
            long expectedB = mixB == null ? -1 : StudioWave.SampleAt(frames, mixB.rate, fps);
            Result("b. A branch's sound is the old take's sound up to the branch frame, then new",
                isBranch && mixB != null && mixB.Frames == expectedB && before && sameBefore == split * mixB.channels && differentAfter > 0 && stemSame && StudioRun.SoundNote == "",
                $"Branch at frame {branchFrame} (first new frame {branchFrame + 1}, sample {split}). New take {takeB?.id}: {mixB?.Frames ?? 0} sample frames (expected {expectedB}). " +
                $"Samples before the branch identical to the old take's: {before} ({sameBefore} compared). After it, {differentAfter} samples differ; the first difference is " +
                $"{(firstDifference < 0 ? "none" : $"{firstDifference / (double)(mixB?.rate ?? 1):0.000} s")} (the extra push is at 2.3 s). World stem joined the same way: {stemSame}. " +
                $"Sound note: \"{StudioRun.SoundNote}\". The branch with sound took {branchSeconds:0.0} s.");

            // ---- c) Switching the active world take switches the sound ----
            StudioVariations.Activate(tape, takeA); StudioRun.Refresh(); yield return 0;
            string whenA = StudioRender.AudioFile(set.id), idA = StudioRender.AudioTakeId(set.id);
            StudioVariations.Activate(tape, takeB); StudioRun.Refresh(); yield return 0;
            string whenB = StudioRender.AudioFile(set.id), idB = StudioRender.AudioTakeId(set.id);
            Result("c. Switching the active world take switches which sound is used",
                whenA == fileA && whenB == fileB && fileA != fileB && idA == takeA.id && idB == takeB.id,
                $"With take {takeA.id} active the sound is {Short(whenA)} (take id {idA}); with take {takeB.id} active it is {Short(whenB)} (take id {idB}).");

            // ---- d) A performed take keeps what the performer heard ----
            var ada = set.marks[0];
            var motor = stage.session.Survivor();
            var performer = new ScriptedPerformer();
            var oldDriver = motor.InputDriver;
            motor.externallyDriven = true;
            motor.InputDriver = performer;
            motor.RefreshMode();
            performer.script = t => new CastleBotCommand { move = t > .3f && t < 3f ? Vector3.right * .6f : Vector3.zero, look = Vector3.right, jump = t >= 1.5f && t < 1.57f };
            var pass = PerformPass(stage, ada, false);   // real time, as a person performs
            watch.Restart();
            StudioRun.Record(pass);
            foreach (int wait in UntilHeld()) yield return wait;
            double performSeconds = watch.Elapsed.TotalSeconds;
            var adaTake = tape.Find(ada.id)?.ActiveTake;
            string performedFile = adaTake == null ? "" : StudioRender.PerformedAudioFile(set.id, adaTake.id);
            var heard = ReadOrNull(performedFile);
            long callbacks = StudioRun.LastTapCallbacks;
            long expectedP = heard == null ? -1 : StudioWave.SampleAt(frames, heard.rate, fps);
            int heardLoudest = LoudestIn(heard);
            string performedDetail = $"Ears used: \"{StudioRun.LastTapListener}\". Unity called the tap {callbacks} times during the take (output {AudioSettings.outputSampleRate} Hz, batch mode {Application.isBatchMode}). " +
                $"Ada take {adaTake?.id}: sound {(heard == null ? "missing" : $"{heard.Frames} sample frames (expected {expectedP}), {heard.channels} channels, loudest sample {heardLoudest}")}. " +
                $"Recorded {StudioRun.LastPerformedSeconds:0.00} s of sound for {StudioRun.LastPerformedTapeSeconds:0.00} s of tape; the take took {performSeconds:0.0} s. Sound note: \"{StudioRun.SoundNote}\".";
            if (callbacks == 0)
                Result("d. A performed take keeps what the performer heard", false,
                    "NOT MEASURABLE in a batch editor: the audio tap on the listener got no callbacks, so no sound reached it. " + performedDetail);
            else
                Result("d. A performed take keeps what the performer heard",
                    heard != null && heard.Frames == expectedP && heardLoudest > 0 && Math.Abs(StudioRun.LastPerformedSeconds - StudioRun.LastPerformedTapeSeconds) < .5,
                    performedDetail + " No stems for performed takes (one mix of everything the performer heard; game music muted while recording).");

            // ---- e) The export puts the performed sound on its own lane and writes one mixed file ----
            set.cameras.Clear();
            set.cameras.Add(new StudioCameraTrack { name = "Wide", color = 0, keys = { CameraKey(0, new Vector3(14, 7, -14), new Vector3(0, 1, 0)), CameraKey(4, new Vector3(10, 5, -16), new Vector3(0, 1, 0)) } });
            set.render = new StudioRenderSettings { width = 160, height = 90, fps = 15, subFrames = 1, format = "png", cinema = false, freezeAmbient = true };
            set.cuts.Clear();
            set.cuts.Add(new StudioCut { camera = set.cameras[0].id, from = 0, to = 1.5f });
            set.cuts.Add(new StudioCut { camera = set.cameras[0].id, from = 2, to = 4 });
            set.title = "Sound cut";
            StudioLibrary.Save(set);
            string folder = Path.Combine(Folder, "render");
            StudioRender.Start(set.cameras, set.render, folder);
            foreach (int wait in UntilRendered()) yield return wait;
            int lanes = 0, gameLanes = 0; string exportNote = ""; List<string> problems = new List<string>();
            int heardCount = StudioCutSound.PerformedSounds(set).Count;
            try
            {
                string xmlFile = StudioExport.WriteFcpxml(set, folder, StudioRender.AudioFile(set.id), out problems);
                var document = new XmlDocument { XmlResolver = null };
                var reader = XmlReader.Create(xmlFile, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
                document.Load(reader); reader.Close();
                lanes = document.SelectNodes("//spine/asset-clip/asset-clip[starts-with(@ref,'p')]").Count;
                gameLanes = document.SelectNodes("//spine/asset-clip/asset-clip[@ref='s1']").Count;
            }
            catch (Exception error) { exportNote = error.Message; }
            var mix = ReadOrNull(Path.Combine(folder, StudioCutSound.MixName));
            int cutFrames = Mathf.RoundToInt(1.5f * 15) + Mathf.RoundToInt(2f * 15);
            long expectedMix = mix == null ? -1 : StudioWave.SampleAt(cutFrames, mix.rate, 15);
            var adaTrack = tape.Find(ada.id);
            var modeWas = adaTrack.mode;
            adaTrack.mode = TrackMode.Muted;
            int whenMuted = StudioCutSound.PerformedSounds(set).Count;
            adaTrack.mode = modeWas;
            bool performedThere = heard != null;
            Result("e. The export adds each performed take on its own lane and writes one mixed file (opening it in an editor was NOT done)",
                exportNote == "" && gameLanes == 2 && lanes == (performedThere ? 2 : 0) && heardCount == (performedThere ? 1 : 0) && whenMuted == 0 && mix != null && mix.Frames == expectedMix && LoudestIn(mix) > 0,
                (exportNote != "" ? "Export failed: " + exportNote + ". " : "") +
                $"Two pieces: game sound under {gameLanes}, performed sound under {lanes} ({heardCount} performed take heard; {whenMuted} once Ada's track is muted). " +
                $"{StudioCutSound.MixName}: {mix?.Frames ?? 0} sample frames (expected {expectedMix} for {cutFrames} frames at 15 fps), loudest sample {LoudestIn(mix)}. " +
                $"Exporter notes: {(problems.Count == 0 ? "none" : string.Join("; ", problems))}.");

            motor.InputDriver = oldDriver;
            StudioRun.LeaveBody();
            yield return 0;
            Result("Runtime errors during the Studio tests", exceptions == 0, exceptions == 0 && errorLines.Count == 0 ? "None." : $"{exceptions} exceptions. First messages: {string.Join(" // ", errorLines)}");
            StudioRun.SaveTape();
        }

        static string Short(string file) => string.IsNullOrEmpty(file) ? "(none)" : Path.Combine(Path.GetFileName(Path.GetDirectoryName(file)), Path.GetFileName(file));
    }
}
