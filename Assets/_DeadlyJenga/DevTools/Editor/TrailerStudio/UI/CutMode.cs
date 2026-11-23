using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The Cut tab: put rendered shots in order, trim them, and hand the cut to an editing app.
    // The real finishing (titles, grade, sound mix) happens in Resolve or Premiere.
    public sealed class CutMode : IStudioMode
    {
        public string Title => "Cut";
        public int Order => 50;
        public bool Available => true;
        public bool TryGetRecord(StudioContext context, out string label, out Action start) { label = null; start = null; return false; }

        const string SnapKey = "DeadlyJenga.TrailerStudio.SnapCutOutToBeat";
        static bool SnapToBeat
        {
            get => EditorPrefs.GetBool(SnapKey, true);
            set => EditorPrefs.SetBool(SnapKey, value);
        }
        static readonly Dictionary<string, string> renderFolders = new Dictionary<string, string>();
        static AudioClip auditionClip;
        StudioCameraTrack previewCamera;
        float previewTapeTime, previewCameraTime;
        int previewTapeFrame = -1;
        string previewSet;

        internal StudioCameraTrack ViewedCamera(StudioSet set, int frame, out float tapeTime, out float cameraTime)
        {
            tapeTime = cameraTime = set == null ? 0 : frame / (float)set.fps;
            if (set?.id != previewSet || frame != previewTapeFrame) return null;
            tapeTime = previewTapeTime;
            cameraTime = previewCameraTime;
            return previewCamera;
        }

        static void AddAlternatives(VisualElement panel, StudioContext context, StudioSet set)
        {
            panel.Add(StudioUi.Heading("CUT VERSIONS"));
            var versions = set.cutAlternatives ?? new List<StudioCutAlternative>();
            var name = new TextField("Version name") { value = "Cut " + (versions.Count + 1) };
            panel.Add(StudioUi.Row(name, StudioUi.Small("Save version", () =>
            {
                string label = name.value.Trim();
                if (label.Length == 0) { context.Say("Enter a version name."); return; }
                if (versions.Any(v => string.Equals(v.name, label, StringComparison.OrdinalIgnoreCase)))
                { context.Say("That version name is already used."); return; }
                if (set.cuts.Count == 0) { context.Say("Add a shot before saving a version."); return; }
                context.Edit("Save cut version", () => (set.cutAlternatives ??= versions).Add(
                    new StudioCutAlternative { name = label, cuts = StudioExport.CopyCuts(set.cuts) }));
            }, "Save a copy of the current shot order, trims, transitions and sound edits")));
            foreach (var version in versions)
                panel.Add(StudioUi.Row(new Label(version.name),
                    StudioUi.Small("Load", () => context.Edit("Load cut version", () => set.cuts = StudioExport.CopyCuts(version.cuts)),
                        "Replace the working cut with a copy. Undo brings back the working cut.")));
        }

        static List<(float tape, float camera)> PreviewTimes(StudioSet set, StudioCameraTrack camera) =>
            StudioCameras.Timeline(camera, set, set.render.fps, Mathf.Clamp(set.render.subFrames, 1, 32));

        void Preview(StudioContext context, StudioSet set, StudioCameraTrack camera, float shotSeconds, float cutSeconds, Label clock)
        {
            var timeline = PreviewTimes(set, camera);
            if (timeline.Count == 0) { clock.text = "This shot has no frames."; return; }
            int fps = set.render.fps, sub = Mathf.Clamp(set.render.subFrames, 1, 32);
            int shotFrame = Mathf.Clamp(Mathf.RoundToInt(shotSeconds * fps), 0, Mathf.Max(0, timeline.Count / sub - 1));
            var (tape, cameraTime) = timeline[shotFrame * sub];
            ShowPreview(context, set, camera, tape, cameraTime, shotFrame, cutSeconds, clock);
        }

        void ShowPreview(StudioContext context, StudioSet set, StudioCameraTrack camera, float tape, float cameraTime,
            int shotFrame, float cutSeconds, Label clock)
        {
            clock.text = $"Cut {Mathf.Max(0, Mathf.RoundToInt(cutSeconds * set.render.fps))}f · Shot {shotFrame}f · Tape {tape:0.000}s  |  {camera.name}";
            if (!context.StageOpen) return;
            previewSet = set.id;
            previewCamera = camera;
            previewTapeTime = tape;
            previewCameraTime = cameraTime;
            previewTapeFrame = Mathf.Clamp(Mathf.FloorToInt(tape * set.fps), 0, set.Frames - 1);
            context.window.Scrub(previewTapeFrame);
            StudioRun.ShowExact(tape * set.fps);
            previewTapeFrame = context.Frame;
        }

        void Compare(StudioContext context, StudioSet set, StudioCameraTrack current, StudioCut cut, float start, Label clock)
        {
            var timeline = PreviewTimes(set, current);
            if (timeline.Count == 0) return;
            int sub = Mathf.Clamp(set.render.subFrames, 1, 32), fps = set.render.fps;
            int index = Mathf.Clamp(Mathf.RoundToInt(cut.from * fps) * sub, 0, timeline.Count - 1);
            float tape = timeline[index].tape;
            var menu = new GenericMenu();
            foreach (var candidate in set.cameras.Where(c => c.start <= tape && c.End(set) > tape))
            {
                var angle = candidate;
                menu.AddItem(new GUIContent(angle.name.Replace("/", "∕")), angle == current, () =>
                {
                    var samples = PreviewTimes(set, angle);
                    int sample = samples.FindIndex(t => t.tape >= tape - .00001f);
                    if (sample < 0) sample = samples.Count - 1;
                    if (sample >= 0)
                        ShowPreview(context, set, angle, tape, tape + samples[sample].camera - samples[sample].tape,
                            sample / sub, start, clock);
                });
            }
            menu.ShowAsContext();
        }

        static void AddAudio(VisualElement panel, StudioContext context, StudioSet set)
        {
            panel.Add(StudioUi.Heading("CUT AUDIO"));
            void Bus(string name, float gain, bool mute, Action<float> changeGain, Action<bool> changeMute)
            {
                panel.Add(StudioFields.Number(context, name + " gain (dB)", gain, value => changeGain(Mathf.Clamp(value, -60, 12))));
                panel.Add(StudioUi.Row(StudioFields.Switch(context, "Mute " + name.ToLowerInvariant(), mute, changeMute),
                    StudioUi.Small("Audition " + name.ToLowerInvariant(), () => Audition(context, set, name), "Listen to this bus by itself")));
            }
            Bus("World", set.worldGainDb, set.worldMuted, v => set.worldGainDb = v, v => set.worldMuted = v);
            Bus("Performers", set.performerGainDb, set.performerMuted, v => set.performerGainDb = v, v => set.performerMuted = v);
            Bus("Music", set.musicGainDb, set.musicMuted, v => set.musicGainDb = v, v => set.musicMuted = v);
            var duck = StudioFields.Number(context, "Music duck (dB)", set.musicDuckDb, v => set.musicDuckDb = Mathf.Clamp(v, 0, 24));
            duck.tooltip = "Lower music by this amount when a recorded performer stem becomes active. Zero disables ducking.";
            panel.Add(duck);
            panel.Add(StudioFields.Number(context, "Performer trigger (dBFS)", set.musicDuckThresholdDb,
                v => set.musicDuckThresholdDb = Mathf.Clamp(v, -60, 0)));
            panel.Add(StudioUi.Row(StudioUi.Small("Audition mix", () => Audition(context, set, null), "Listen to the planned cut with current levels and sound edits"),
                StudioUi.Small("Stop audition", StopAudition, "Stop the audio preview"),
                StudioUi.Small("Measure mix", () => Audition(context, set, null, true), "Measure sample peak and, with FFmpeg, integrated loudness and true peak")));
        }

        static System.Reflection.MethodInfo AudioPreviewMethod(string name, Type[] args) =>
            typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil")?.GetMethod(name,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null, args, null);

        static void StopAudition()
        {
            if (!auditionClip) return;
            AudioPreviewMethod("StopAllPreviewClips", Type.EmptyTypes)?.Invoke(null, null);
            UnityEngine.Object.DestroyImmediate(auditionClip);
            auditionClip = null;
        }

        static void Audition(StudioContext context, StudioSet set, string bus, bool measure = false)
        {
            try
            {
                var play = AudioPreviewMethod("PlayPreviewClip", new[] { typeof(AudioClip), typeof(int), typeof(bool) });
                if (!measure && play == null) { context.Say("This editor does not expose audio preview."); return; }
                if (!measure) StopAudition();
                int fps = set.render.fps, at = 0;
                int sub = Mathf.Clamp(set.render.subFrames, 1, 32);
                var pieces = new List<StudioExport.Piece>();
                foreach (var cut in set.cuts)
                {
                    var camera = set.cameras.Find(c => c.id == cut.camera);
                    if (camera == null) continue;
                    var timeline = PreviewTimes(set, camera);
                    int frames = timeline.Count / sub;
                    if (frames == 0) continue;
                    int first = Mathf.Clamp(Mathf.RoundToInt(cut.from * fps), 0, frames - 1);
                    int count = Mathf.Clamp(Mathf.RoundToInt((cut.to - cut.from) * fps), 1, frames - first);
                    var shot = new StudioRender.ShotRecord { fps = fps, frames = frames, tapeStart = camera.start };
                    if (StudioExport.RequiresTimeMap(camera))
                    {
                        shot.frameTapeTimes = new float[frames + 1];
                        for (int n = 0; n < frames; n++) shot.frameTapeTimes[n] = timeline[n * sub].tape;
                        int next = frames * sub;
                        shot.frameTapeTimes[frames] = next < timeline.Count ? timeline[next].tape : camera.End(set);
                    }
                    pieces.Add(new StudioExport.Piece { cut = cut, shot = shot, inFrame = first, frames = count, recordStart = at });
                    at += count;
                }
                var problems = new List<string>();
                var mix = StudioCutSound.BuildMix(set, StudioRender.AudioFile(set.id), StudioCutSound.PerformedSounds(set), pieces, fps, bus, problems);
                if (mix == null) { context.Say(problems.FirstOrDefault() ?? "No recorded audio for this audition."); return; }
                if (measure)
                {
                    context.Say(StudioCutSound.Measure(mix) + (problems.Count > 0 ? " " + string.Join(" · ", problems.Distinct()) : ""));
                    return;
                }
                auditionClip = AudioClip.Create("Trailer Studio cut audition", (int)mix.Frames, mix.channels, mix.rate, false);
                var samples = Array.ConvertAll(mix.samples, sample => sample / 32768f);
                auditionClip.SetData(samples, 0);
                play.Invoke(null, new object[] { auditionClip, 0, false });
                context.Say(problems.FirstOrDefault() ?? $"Auditioning {bus?.ToLowerInvariant() ?? "the cut mix"}.");
            }
            catch (Exception error) { context.Say("Audio audition failed: " + error.GetBaseException().Message); }
        }

        public void Build(VisualElement panel, StudioContext context)
        {
            panel.Add(StudioUi.Heading("CUT"));
            var set = context.Set;
            if (set == null) { panel.Add(StudioUi.Hint("Choose or make a Set first.")); return; }
            if (set.cameras.Count == 0) { panel.Add(StudioUi.Hint("Add cameras in the Shoot tab first. The cut is made of pieces of their shots.")); return; }
            int fps = set.render.fps;
            var spans = new List<(StudioCameraTrack camera, float start, float end)>();
            var summary = StudioUi.Card();
            if (set.cuts.Count > 0) panel.Add(summary);
            AddAlternatives(panel, context, set);

            panel.Add(StudioUi.Big("+  Add a shot to the cut", () =>
            {
                var menu = new GenericMenu();
                foreach (var camera in set.cameras)
                {
                    var captured = camera;
                    menu.AddItem(new GUIContent(camera.name), false, () => context.Edit("Add to cut", () =>
                        set.cuts.Add(new StudioCut { camera = captured.id, from = 0, to = StudioCameras.OutputLength(captured, set, fps) })));
                }
                menu.ShowAsContext();
            }));
            if (set.cuts.Count == 0)
            {
                panel.Add(StudioUi.Small("Use every camera once, in order", () => context.Edit("Build cut", () =>
                {
                    foreach (var camera in set.cameras.OrderBy(c => c.start)) set.cuts.Add(new StudioCut { camera = camera.id, from = 0, to = StudioCameras.OutputLength(camera, set, fps) });
                })));
                panel.Add(StudioUi.Hint("The cut is empty."));
            }

            float at = 0;
            for (int i = 0; i < set.cuts.Count; i++)
            {
                var cut = set.cuts[i]; int index = i;
                var camera = set.cameras.FirstOrDefault(c => c.id == cut.camera);
                var card = StudioUi.Card();
                if (camera == null)
                {
                    card.Add(StudioUi.Warning("This piece's camera was deleted."));
                    card.Add(StudioUi.Small("Remove", () => context.Edit("Remove from cut", () => set.cuts.RemoveAt(index))));
                    panel.Add(card);
                    continue;
                }
                float length = StudioCameras.OutputLength(camera, set, fps);
                var name = StudioDurationBar.Title($"{i + 1}. {camera.name}");
                name.AddToClassList("ts-item-name");
                var up = StudioUi.Small("Up", () => context.Edit("Reorder cut", () => { set.cuts.RemoveAt(index); set.cuts.Insert(index - 1, cut); }));
                up.SetEnabled(i > 0);
                var down = StudioUi.Small("Down", () => context.Edit("Reorder cut", () => { set.cuts.RemoveAt(index); set.cuts.Insert(index + 1, cut); }));
                down.SetEnabled(i < set.cuts.Count - 1);
                card.Add(StudioUi.Row(StudioUi.Dot(StudioColors.For(camera.color)), name, up, down, StudioUi.Small("x", () => context.Edit("Remove from cut", () => set.cuts.RemoveAt(index)))));
                // Trim handles: in and out, inside the rendered shot. With snapping on, the piece's end lands on a beat of the cut.
                float start = at;
                var clock = new Label();
                float Tick(float seconds) => Mathf.Round(seconds * fps) / fps;
                float step = 1f / fps;
                card.Add(StudioFields.Slide(context, "In (s)", cut.from, 0, Mathf.Max(step, length - step), v =>
                {
                    cut.from = Mathf.Clamp(Tick(v), 0, Mathf.Max(0, Math.Min(length, cut.to) - step));
                    Preview(context, set, camera, cut.from, start, clock);
                }));
                card.Add(StudioFields.Slide(context, "Out (s)", Mathf.Min(cut.to, length), step, Mathf.Max(step, length), v =>
                {
                    float wanted = Mathf.Max(v, cut.from + step);
                    if (SnapToBeat && set.bpm > 0) wanted = StudioExport.SnapToBeat(set, start + wanted - cut.from) - start + cut.from;
                    cut.to = Mathf.Clamp(Tick(wanted), cut.from + step, Mathf.Max(cut.from + step, length));
                    Preview(context, set, camera, cut.to - step, start + cut.to - cut.from - step, clock);
                }));
                if (i > 0) card.Add(StudioFields.Slide(context, "Dissolve in (s)", cut.dissolve, 0, 2, v => cut.dissolve = v));
                card.Add(StudioUi.Row(
                    StudioUi.Small("Preview in", () => Preview(context, set, camera, cut.from, start, clock), "View the first included frame"),
                    StudioUi.Small("Preview out", () => Preview(context, set, camera, cut.to - step, start + cut.to - cut.from - step, clock), "View the last included frame"),
                    StudioUi.Small("Compare angle", () => Compare(context, set, camera, cut, start, clock), "Preview another camera at the same tape moment")));
                card.Add(clock);
                card.Add(StudioFields.Number(context, "Sound lead (s)", cut.audioLead, value => cut.audioLead = Mathf.Clamp(value, 0, 5)));
                card.Add(StudioFields.Number(context, "Sound tail (s)", cut.audioTail, value => cut.audioTail = Mathf.Clamp(value, 0, 5)));
                float piece = Mathf.Max(0, Mathf.Min(cut.to, length) - cut.from);
                spans.Add((camera, at, at + piece));
                card.Add(StudioUi.Hint($"Plays from {at:0.00} s to {at + piece:0.00} s of the cut."));
                at += piece;
                panel.Add(card);
            }
            if (spans.Count > 0)
            {
                var strip = new StudioDurationBar("Planned cut order. Segment width shows trimmed duration.");
                foreach (var span in spans)
                    strip.Span(span.start, span.end, at, StudioColors.For(span.camera.color),
                        description: $"{span.camera.name} · {span.start:0.00}–{span.end:0.00} s", divider: true);
                summary.Add(StudioDurationBar.Title($"Planned cut · {at:0.00} s"));
                summary.Add(strip);
            }
            int missingCameras = set.cuts.Count - spans.Count;
            if (missingCameras > 0) summary.Add(StudioUi.Warning($"{missingCameras} cut piece(s) reference deleted cameras."));

            // Music bed and beats
            panel.Add(StudioUi.Heading("MUSIC"));
            var removeMusic = StudioUi.Small("Remove", () => context.Edit("Remove music bed", () => set.music = ""));
            removeMusic.SetEnabled(!string.IsNullOrEmpty(set.music));
            panel.Add(StudioUi.Row(new Label(string.IsNullOrEmpty(set.music) ? "No music bed" : Path.GetFileName(set.music)) { style = { flexGrow = 1 } },
                StudioUi.Small("Choose…", () =>
                {
                    string file = EditorUtility.OpenFilePanel("Music bed", "", "wav,mp3,ogg,aiff");
                    if (file.Length > 0) context.Edit("Music bed", () => set.music = file);
                }),
                removeMusic));
            panel.Add(StudioFields.Number(context, "Beats per minute", set.bpm, v => set.bpm = Mathf.Clamp(v, 0, 300)));
            panel.Add(StudioFields.Number(context, "First beat at (s)", set.beatOffset, v => set.beatOffset = Mathf.Max(0, v)));
            var snapping = new Toggle("Snap out points to beats")
            {
                value = SnapToBeat,
                tooltip = set.bpm > 0 ? "Moving an out point snaps the piece's end to the nearest beat." : "Set a tempo above zero to enable beat snapping."
            };
            snapping.SetEnabled(set.bpm > 0);
            snapping.RegisterValueChangedCallback(e => SnapToBeat = e.newValue);
            panel.Add(snapping);
            AddAudio(panel, context, set);

            // Export
            panel.Add(StudioUi.Heading("EXPORT"));
            if (StudioRender.Running) { panel.Add(StudioUi.Hint("Wait for the render to finish before exporting.")); return; }
            string renders = StudioRender.RendersFolder(set.id);
            var folders = Directory.Exists(renders) ? Directory.GetDirectories(renders).OrderByDescending(d => d).ToList() : new List<string>();
            if (folders.Count == 0) { panel.Add(StudioUi.Hint("Render the cameras in the Shoot tab first. The export points at those files.")); return; }
            int chosen = renderFolders.TryGetValue(set.id, out string saved) ? Mathf.Max(0, folders.IndexOf(saved)) : 0;
            string folder = folders[chosen];
            var batches = new DropdownField("Render batch", folders.Select(d => Path.GetFileName(d)).ToList(), chosen);
            batches.RegisterValueChangedCallback(_ =>
            {
                if (batches.index < 0) return;
                renderFolders[set.id] = folders[batches.index];
                context.Rebuild();
            });
            panel.Add(batches);
            int resolved = 0;
            bool missingMedia = false;
            if (set.cuts.Count > 0)
            {
                try
                {
                    var pieces = StudioExport.Resolve(set, folder, out var warnings);
                    resolved = pieces.Count;
                    panel.Add(StudioUi.Hint($"{resolved} of {set.cuts.Count} cut pieces have render records in this batch."));
                    foreach (var warning in warnings.Distinct()) panel.Add(StudioUi.Warning(warning));
                    missingMedia = pieces.Any(piece => !File.Exists(piece.media) || piece.shot.frames <= 0 || piece.shot.fps <= 0);
                    if (missingMedia)
                        panel.Add(StudioUi.Warning("This batch has a missing media file or an incomplete shot. Choose another batch or render the affected cameras again."));
                }
                catch (Exception error) { panel.Add(StudioUi.Warning("Could not read this render batch. " + error.Message)); }
            }
            string audio = StudioRender.AudioFile(set.id);
            if (!File.Exists(audio)) panel.Add(StudioUi.Hint("No game audio for this cut. Use Record game audio from tape in Shoot to include it."));
            else
            {
                // Each world take keeps its own sound. Say so when the active one has none (an older single file is used instead).
                string activeTake = StudioRender.ActiveWorldTakeId(set.id);
                if (activeTake.Length > 0 && StudioRender.AudioTakeId(set.id) != activeTake)
                    panel.Add(StudioUi.Hint("Careful: there is no sound for the active world take. The export uses sound recorded with another take, so impacts may not match the picture. Record the whole set again (or branch it) to give this take its own sound."));
            }
            int performedSounds = StudioCutSound.PerformedSounds(set).Count;
            if (performedSounds > 0) panel.Add(StudioUi.Hint($"{performedSounds} performed take(s) add what their performer heard, each on its own sound lane. The export also writes {StudioCutSound.MixName}, everything mixed into one file."));
            string captured = folder;
            bool partial = resolved > 0 && resolved < set.cuts.Count;
            var export = StudioUi.Big(partial ? "Export available pieces" : "Export FCPXML and EDL", () =>
            {
                try
                {
                    string xml = StudioExport.WriteFcpxml(set, captured, audio, out var problems);
                    string edl = StudioExport.WriteEdl(set, captured, out var edlProblems);
                    string warning = problems.Concat(edlProblems).FirstOrDefault();
                    context.Say(warning != null ? "Exported with warnings. " + warning : "Exported " + Path.GetFileName(xml) + " and " + Path.GetFileName(edl) + ".");
                    EditorUtility.RevealInFinder(xml);
                }
                catch (Exception error) { context.Say(error.Message); }
            }, partial ? "Writes an FCPXML and EDL using only the pieces rendered in this batch." : "Writes an FCPXML file and an EDL next to the rendered shots.");
            export.AddToClassList("ts-big--primary");
            export.SetEnabled(resolved > 0 && !missingMedia);
            panel.Add(export);
            var portable = StudioUi.Small("Package cut and media", () =>
            {
                try
                {
                    string path = StudioExport.WritePortable(set, captured, audio, out var problems);
                    EditorUtility.RevealInFinder(path);
                    context.Say(problems.Count == 0 ? "Packaged the cut and its media." : "Packaged the cut. " + string.Join(" · ", problems));
                }
                catch (Exception error) { context.Say(error.GetBaseException().Message); }
            }, "Copy used movie and audio files into a handoff with FCPXML, EDL, OpenTimelineIO and shot provenance");
            portable.SetEnabled(resolved > 0 && !partial && !missingMedia);
            panel.Add(portable);
        }
    }
}
