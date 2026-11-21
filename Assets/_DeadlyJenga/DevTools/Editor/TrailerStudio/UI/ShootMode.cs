using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The Shoot tab: cameras over the finished tape. Look around in the viewport, press "Key here",
    // move the playhead, look somewhere else, press it again: that is a camera move.
    public sealed class ShootMode : IStudioMode
    {
        public string Title => "Shoot";
        public int Order => 40;
        public bool Available => true;

        // The camera the viewport is looking through (null = the director's free view).
        public static string LookThrough = "";
        public static bool SafeAreas = true;

        public bool TryGetRecord(StudioContext context, out string label, out Action start)
        {
            var camera = context.Selection as StudioCameraTrack;
            label = camera == null ? "Pick a camera to film by hand" : "Film \"" + camera.name + "\" by hand";
            start = () =>
            {
                if (camera == null) { context.Say("Add or select a camera first."); return; }
                LookThrough = "";
                context.window.BeginEdit("Handheld take");
                StudioHandheld.Start(camera, context.window.InFrame, context.window.OutFrame, _ => { context.window.EndEdit(false); context.Say($"Handheld take recorded on \"{camera.name}\": {camera.keys.Count} keys you can now edit."); });
                context.Say("Filming. In the Game view: hold the right mouse button to look, W A S D to move, Q/E down/up, wheel to zoom.");
            };
            return context.Set != null;
        }

        static string NewName(StudioSet set)
        {
            for (int n = set.cameras.Count; n < 26; n++)
            {
                string name = "Cam " + (char)('A' + n);
                if (set.cameras.All(c => c.name != name)) return name;
            }
            return "Cam " + (set.cameras.Count + 1);
        }

        public static StudioCameraTrack AddCamera(StudioContext context)
        {
            var set = context.Set;
            StudioCameraTrack made = null;
            context.Edit("Add camera", () =>
            {
                made = new StudioCameraTrack { name = NewName(set), color = set.cameras.Count % 8, start = 0, end = -1 };
                if (context.Stage != null) made.keys.Add(StudioCameras.KeyFromView(context.Stage, made, context.Frame / (float)set.fps));
                else made.keys.Add(new StudioCameraKey { position = new Vector3(6, 3, -8), euler = new Vector3(15, -35, 0) });
                set.cameras.Add(made);
            });
            context.Select(made);
            return made;
        }

        public void Build(VisualElement panel, StudioContext context)
        {
            panel.Add(StudioUi.Heading("SHOOT"));
            var set = context.Set;
            if (set == null) { panel.Add(StudioUi.Hint("Choose or make a Set first.")); return; }
            panel.Add(StudioUi.Hint(set.cameras.Count == 0
                ? "Cameras come last: the tape is finished, and you can film it as many times as you like. Look around in the viewport, then add a camera from that view."
                : "Select a camera to edit it. Its keys show on the timeline and as a path in the viewport."));
            var add = StudioUi.Big("+  Camera from this view", () => AddCamera(context), "A new camera with its first key where the viewport is looking now.");
            panel.Add(add);
            if (set.shots.Count > 0)
                panel.Add(StudioUi.Small($"Turn {set.shots.Count} Theatre shot(s) into cameras", () =>
                {
                    context.Edit("Convert Theatre shots", () => { foreach (var shot in set.shots) set.cameras.Add(FromTheatre(shot, set)); set.shots.Clear(); });
                }, "Imported Gameplay Theatre shots become editable cameras."));

            foreach (var camera in set.cameras)
            {
                var captured = camera;
                var name = new Label(camera.name); name.AddToClassList("ts-item-name");
                bool through = LookThrough == camera.id;
                var look = StudioUi.Small(through ? "Looking" : "Look", () => { LookThrough = through ? "" : captured.id; if (!through) context.Select(captured); else { StudioCamera.Place(); context.Rebuild(); } },
                    "Show the viewport through this camera.");
                look.EnableInClassList("ts-small--on", through);
                var row = StudioUi.Row(StudioUi.Dot(StudioColors.For(camera.color)), name, new Label(camera.keys.Count == 1 ? "1 key  " : $"{camera.keys.Count} keys  ") { style = { color = new Color(.6f, .6f, .66f), fontSize = 11 } }, look);
                row.AddToClassList("ts-item");
                row.EnableInClassList("ts-item--selected", ReferenceEquals(camera, context.Selection));
                row.RegisterCallback<ClickEvent>(_ => context.Select(captured));
                panel.Add(row);
            }

            // Render
            panel.Add(StudioUi.Heading("RENDER"));
            var render = set.render;
            int aspect = ClosestAspect(render);
            var lines = new List<string> { "720", "1080", "1440", "2160 (4K)" };
            int[] lineValues = { 720, 1080, 1440, 2160 };
            // Which size step is this? The one whose width and height are closest to the Set's.
            int line = 0; float nearest = float.MaxValue;
            for (int i = 0; i < lineValues.Length; i++)
            {
                var candidate = StudioAspects.Size(aspect, lineValues[i]);
                float off = Mathf.Abs(candidate.x - render.width) + Mathf.Abs(candidate.y - render.height);
                if (off < nearest) { nearest = off; line = i; }
            }
            void Size(int newAspect, int newLine)
            {
                var size = StudioAspects.Size(newAspect, lineValues[Mathf.Clamp(newLine, 0, 3)]);
                render.width = size.x; render.height = size.y;
            }
            panel.Add(StudioFields.Choice(context, "Shape", StudioAspects.Names, aspect, i => Size(i, line)));
            panel.Add(StudioFields.Choice(context, "Size", lines, line, i => Size(aspect, i)));
            panel.Add(StudioUi.Hint($"{render.width} × {render.height} at {render.fps} fps"));
            var formats = new List<string> { "png", "exr", "mp4", "prores" };
            var formatNames = new List<string> { "PNG images (and an MP4 if FFmpeg is here)", "EXR images (for grading)", "MP4 draft", "ProRes (for grading)" };
            panel.Add(StudioFields.Choice(context, "Output", formatNames, Mathf.Max(0, formats.IndexOf(render.format)), i => render.format = formats[Mathf.Max(0, i)]));
            var blurNames = new List<string> { "Off", "Light (4 in-between frames)", "Smooth (8)", "Cinema (16)" };
            int[] blurValues = { 1, 4, 8, 16 };
            panel.Add(StudioFields.Choice(context, "Motion blur", blurNames, Mathf.Max(0, Array.IndexOf(blurValues, render.subFrames)), i => render.subFrames = blurValues[Mathf.Max(0, i)]));
            var rates = new List<string> { "24", "25", "30", "50", "60" };
            panel.Add(StudioUi.Advanced(
                StudioFields.Choice(context, "Frame rate", rates, Mathf.Max(0, rates.IndexOf(render.fps.ToString())), i => render.fps = int.Parse(rates[Mathf.Max(0, i)])),
                StudioFields.Switch(context, "Cinema quality while rendering", render.cinema, v => render.cinema = v),
                StudioFields.Slide(context, "Shutter", render.shutter, .1f, 1, v => render.shutter = v),
                StudioFields.Switch(context, "Hold ocean and sky still", render.freezeAmbient, v => render.freezeAmbient = v),
                StudioUi.Hint("With ocean and sky held still, two renders of the same shot are the same picture bit for bit. Otherwise they move with the game clock.")));

            if (StudioRender.Running)
            {
                panel.Add(StudioUi.Hint(StudioRender.Progress));
                panel.Add(StudioUi.Small("Stop the render", StudioRender.Cancel));
            }
            else
            {
                bool held = StudioRun.State == StudioState.Held;
                var selected = context.Selection as StudioCameraTrack;
                void Go(IEnumerable<StudioCameraTrack> cameras)
                {
                    try { LookThrough = ""; StudioRender.Start(cameras, render); context.Rebuild(); }
                    catch (Exception error) { context.Say(error.Message); }
                }
                var one = StudioUi.Big(selected == null ? "Render the selected camera" : "Render \"" + selected.name + "\"", () => Go(new[] { selected }));
                one.SetEnabled(held && selected != null);
                var all = StudioUi.Big($"Render all {set.cameras.Count} cameras", () => Go(set.cameras));
                all.AddToClassList("ts-big--primary");
                all.SetEnabled(held && set.cameras.Count > 0);
                panel.Add(one); panel.Add(all);
                var sound = StudioUi.Small("Record the tape's sound", () =>
                {
                    try { StudioRender.RecordAudio(file => context.Say("Sound recorded: " + file)); }
                    catch (Exception error) { context.Say(error.Message); }
                }, "Plays the tape once and saves the game mix and its stems. The cut export uses it.");
                sound.SetEnabled(held);
                panel.Add(sound);
                if (!string.IsNullOrEmpty(StudioRender.Progress)) panel.Add(StudioUi.Hint(StudioRender.Progress));
                foreach (var done in StudioRender.Results)
                    panel.Add(StudioUi.Hint($"• {done.camera}: {done.frames} frames in {done.seconds:0} s" + (done.movie.Length > 0 ? ", movie made" : "") + (done.error.Length > 0 ? ". " + done.error : "")));
                if (Directory.Exists(StudioRender.LastFolder)) panel.Add(StudioUi.Small("Open the render folder", () => EditorUtility.RevealInFinder(StudioRender.LastFolder)));
            }
        }

        static int ClosestAspect(StudioRenderSettings render)
        {
            float ratio = render.width / (float)Mathf.Max(1, render.height);
            int best = 0;
            for (int i = 1; i < StudioAspects.Ratios.Length; i++)
                if (Mathf.Abs(StudioAspects.Ratios[i] - ratio) < Mathf.Abs(StudioAspects.Ratios[best] - ratio)) best = i;
            return best;
        }

        // A Theatre shot (rail from A to B looking at a point) as a two-key camera.
        static StudioCameraTrack FromTheatre(StudioShot shot, StudioSet set)
        {
            var camera = new StudioCameraTrack { name = shot.name, color = set.cameras.Count % 8, start = shot.start, end = shot.start + shot.duration,
                avoidBlocks = shot.avoidWalls, focusOnSubject = shot.autoFocus, subject = shot.subject, second = shot.second };
            float focal = StudioCameras.FocalFor(shot.fov, camera.sensorHeight);
            Vector3 Aim(Vector3 from) => Quaternion.LookRotation((shot.look - from).sqrMagnitude > .001f ? shot.look - from : Vector3.forward).eulerAngles + new Vector3(0, 0, shot.roll);
            if (shot.firstPerson) camera.behavior = "firstPerson";
            else if (!string.IsNullOrEmpty(shot.second)) camera.behavior = "twoShot";
            else if (!string.IsNullOrEmpty(shot.subject)) camera.behavior = "ride";
            camera.keys.Add(new StudioCameraKey { time = shot.start, position = shot.from, euler = Aim(shot.from), focal = focal, aperture = shot.autoFocus ? 4 : 16 });
            camera.keys.Add(new StudioCameraKey { time = shot.start + shot.duration, position = shot.to, euler = Aim(shot.to), focal = focal, aperture = shot.autoFocus ? 4 : 16 });
            return camera;
        }
    }

    public sealed class CameraInspector : IStudioInspector
    {
        public bool Handles(object selection, StudioContext context) => selection is StudioCameraTrack;

        public void Build(VisualElement panel, object selection, StudioContext context)
        {
            var camera = (StudioCameraTrack)selection;
            var set = context.Set;
            // The playhead at the moment of a click (scrubbing does not rebuild the inspector).
            float Now() => context.Frame / (float)set.fps;
            panel.Add(StudioFields.Text(context, "Name", camera.name, v => camera.name = string.IsNullOrWhiteSpace(v) ? camera.name : v));
            panel.Add(StudioFields.Swatches(context, camera.color, v => camera.color = v));

            // Keys
            var here = StudioUi.Big("Key here  (" + StudioUi.Time(context.Frame, set.fps) + ")", () =>
                context.Edit("Camera key", () => StudioCameras.AddKey(camera, StudioCameras.KeyFromView(context.Stage, camera, Now()))),
                "Save the viewport's current view as this camera's key at the playhead.");
            here.AddToClassList("ts-big--primary");
            here.SetEnabled(context.StageOpen && ShootMode.LookThrough != camera.id);
            panel.Add(here);
            if (ShootMode.LookThrough == camera.id) panel.Add(StudioUi.Hint("You are looking through this camera. Press \"Looking\" in the list to look around freely again."));
            string[] eases = { "Straight", "Smooth", "Slow start", "Slow end", "Hold, then cut" };
            foreach (var key in camera.keys.ToList())
            {
                var captured = key;
                var go = StudioUi.Small(StudioUi.Time(Mathf.RoundToInt(key.time * set.fps), set.fps), () => context.window.Scrub(Mathf.RoundToInt(captured.time * set.fps)), "Go to this key");
                var ease = StudioUi.Small(eases[Mathf.Clamp(key.ease, 0, 4)], () => context.Edit("Key easing", () => captured.ease = (captured.ease + 1) % 5), "How the camera arrives at this key");
                ease.style.minWidth = 84;
                var lens = new FloatField { value = key.focal, isDelayed = true, tooltip = "Focal length (mm)" };
                lens.style.width = 46;
                lens.RegisterValueChangedCallback(e => context.Edit("Focal length", () => captured.focal = Mathf.Clamp(e.newValue, 8, 400)));
                var remove = StudioUi.Small("x", () => context.Edit("Delete key", () => camera.keys.Remove(captured)), "Delete this key");
                panel.Add(StudioUi.Row(go, ease, lens, new Label(" mm"), remove));
            }
            if (camera.keys.Count == 0) panel.Add(StudioUi.Hint("No keys yet. Look around in the viewport and press \"Key here\"."));

            // What it films
            panel.Add(StudioUi.Heading("MOVE"));
            var actors = new List<string> { "" }.Concat(set.marks.Select(m => m.id)).ToList();
            var names = new List<string> { "Nobody" }.Concat(set.marks.Select(m => m.actor)).ToList();
            panel.Add(StudioFields.Choice(context, "Behavior", StudioCameras.BehaviorLabels, Mathf.Max(0, StudioCameras.Behaviors.IndexOf(camera.behavior)), i => camera.behavior = StudioCameras.Behaviors[Mathf.Max(0, i)]));
            if (camera.behavior != "")
            {
                panel.Add(StudioFields.Choice(context, "Actor", names, Mathf.Max(0, actors.IndexOf(camera.subject)), i => camera.subject = actors[Mathf.Max(0, i)]));
                if (camera.behavior == "twoShot") panel.Add(StudioFields.Choice(context, "Second actor", names, Mathf.Max(0, actors.IndexOf(camera.second)), i => camera.second = actors[Mathf.Max(0, i)]));
                if (camera.behavior == "orbit") panel.Add(StudioFields.Slide(context, "Orbit (deg/s)", camera.orbitSpeed, -90, 90, v => camera.orbitSpeed = v));
                if (camera.behavior != "firstPerson" && camera.behavior != "dollyZoom") panel.Add(StudioFields.Slide(context, "Lag (s)", camera.lag, 0, 1.5f, v => camera.lag = v));
                if (camera.behavior == "ride" || camera.behavior == "orbit") panel.Add(StudioUi.Hint("With this behavior the keys are positions around the actor, not places on the map."));
                if (camera.behavior == "firstPerson")
                {
                    panel.Add(StudioFields.Slide(context, "Smooth position (s)", camera.fpSmoothMove, 0, 1, v => camera.fpSmoothMove = v));
                    panel.Add(StudioFields.Slide(context, "Smooth turning (s)", camera.fpSmoothTurn, 0, 1.5f, v => camera.fpSmoothTurn = v));
                    panel.Add(StudioFields.Switch(context, "Keep the horizon level", camera.fpLevel, v => camera.fpLevel = v));
                    panel.Add(StudioFields.Slide(context, "Field of view (deg)", camera.fpFieldOfView, 0, 100, v => camera.fpFieldOfView = v));
                    panel.Add(StudioFields.Switch(context, "Show own hands and weapon", camera.fpOwnBody, v => camera.fpOwnBody = v));
                    panel.Add(StudioUi.Hint("The actor's eyes, as its player saw the game: own arms, hands and weapon in view, no head or hair in the lens. Other cameras still see the whole body. Field of view 0 uses the keys' focal length; all settings at 0 / off is the raw head view."));
                }
            }
            panel.Add(StudioFields.Choice(context, "Handheld", StudioCameras.NoiseNames, Mathf.Max(0, StudioCameras.NoiseNames.IndexOf(camera.noise)), i => camera.noise = i <= 0 ? "" : StudioCameras.NoiseNames[i]));
            if (camera.noise != "") panel.Add(StudioFields.Slide(context, "Shake amount", camera.noiseStrength, 0, 3, v => camera.noiseStrength = v));
            panel.Add(StudioFields.Switch(context, "Even pace along the path", camera.constantSpeed, v => camera.constantSpeed = v));
            panel.Add(StudioFields.Switch(context, "Stay in front of blocks", camera.avoidBlocks, v => camera.avoidBlocks = v));

            // Lens
            panel.Add(StudioUi.Heading("LENS"));
            if (camera.keys.Count > 0)
            {
                var first = camera.keys[0];
                panel.Add(StudioFields.Slide(context, "Aperture (f)", first.aperture, 1, 16, v => { foreach (var key in camera.keys) key.aperture = v; }));
                panel.Add(StudioFields.Slide(context, "Focus (m)", first.focus, .3f, 60, v => { foreach (var key in camera.keys) key.focus = v; }));
            }
            panel.Add(StudioFields.Switch(context, "Keep the actor in focus", camera.focusOnSubject, v => camera.focusOnSubject = v));
            panel.Add(StudioUi.Hint("f/16 keeps everything sharp. Lower numbers blur the background."));

            // Time
            panel.Add(StudioUi.Heading("TIME"));
            panel.Add(StudioFields.Slide(context, "Shot starts (s)", camera.start, 0, set.length, v => camera.start = Mathf.Min(v, camera.End(set) - .1f)));
            panel.Add(StudioFields.Slide(context, "Shot ends (s)", camera.End(set), 0, set.length, v => camera.end = Mathf.Max(v, camera.start + .1f)));
            panel.Add(StudioUi.Small("Speed change at the playhead", () => context.Edit("Speed mark", () =>
            {
                float now = Now();
                // A ramp needs somewhere to come from: the first mark also pins the shot's start at real speed.
                if (camera.speeds.Count == 0 && now > camera.start + .05f) camera.speeds.Add(new StudioSpeedMark { time = camera.start, speed = 1 });
                camera.speeds.RemoveAll(m => Mathf.Abs(m.time - now) < .001f);
                camera.speeds.Add(new StudioSpeedMark { time = now, speed = .25f });
                camera.speeds.Sort((a, b) => a.time.CompareTo(b.time));
            }), "Adds a marker. The tape eases from the marker before it to this speed (slow motion into an impact, real speed after)."));
            foreach (var mark in camera.speeds.ToList())
            {
                var captured = mark;
                var speed = new Slider(StudioUi.Time(Mathf.RoundToInt(mark.time * set.fps), set.fps), .05f, 2) { value = mark.speed, showInputField = true };
                speed.style.flexGrow = 1;
                // Both events come after one drag; only a real change becomes an undo step.
                void Keep() { if (!Mathf.Approximately(captured.speed, speed.value)) context.Edit("Speed", () => captured.speed = speed.value); }
                speed.RegisterCallback<FocusOutEvent>(_ => Keep());
                speed.RegisterCallback<PointerCaptureOutEvent>(_ => Keep(), TrickleDown.TrickleDown);
                panel.Add(StudioUi.Row(speed, StudioUi.Small("x", () => context.Edit("Delete speed mark", () => camera.speeds.Remove(captured)))));
            }
            panel.Add(StudioUi.Small(camera.freezeAt >= 0 ? "Remove the freeze frame" : "Freeze frame at the playhead", () =>
                context.Edit("Freeze frame", () => { if (camera.freezeAt >= 0) camera.freezeAt = -1; else { camera.freezeAt = Now(); camera.freezeFor = 1; } }),
                "The tape holds still while the camera keeps moving."));
            if (camera.freezeAt >= 0) panel.Add(StudioFields.Slide(context, "Freeze for (s)", camera.freezeFor, .1f, 6, v => camera.freezeFor = v));
            panel.Add(StudioUi.Hint($"This shot plays for {StudioCameras.OutputLength(camera, set, set.render.fps):0.00} s."));

            // Look
            var look = StudioUi.Advanced(
                StudioFields.Slide(context, "Bloom", camera.bloom, 0, 1, v => camera.bloom = v),
                StudioFields.Slide(context, "Vignette", camera.vignette, 0, 1, v => camera.vignette = v),
                StudioFields.Slide(context, "Grain", camera.grain, 0, 1, v => camera.grain = v),
                StudioFields.Slide(context, "Colour fringes", camera.chroma, 0, 1, v => camera.chroma = v),
                StudioFields.Text(context, "LUT texture", camera.lut, v => camera.lut = v ?? ""),
                StudioFields.Slide(context, "Sensor height (mm)", camera.sensorHeight, 8, 36, v => camera.sensorHeight = v),
                StudioFields.Switch(context, "Curved path", camera.spline, v => camera.spline = v));
            look.text = "Look and lens details";
            panel.Add(look);

            panel.Add(StudioUi.Row(
                StudioUi.Small("Duplicate", () =>
                {
                    StudioCameraTrack copy = null;
                    context.Edit("Duplicate camera", () => { copy = JsonUtility.FromJson<StudioCameraTrack>(JsonUtility.ToJson(camera)); copy.id = Guid.NewGuid().ToString("N").Substring(0, 8); copy.name = camera.name + " copy"; set.cameras.Add(copy); });
                    context.Select(copy);
                }),
                StudioUi.Small("Delete camera", () => { context.Edit("Delete camera", () => set.cameras.Remove(camera)); context.Select(null); })));
        }
    }

    // Camera paths, keys and frustums in the viewport; safe-area guides when looking through a camera.
    public sealed class CameraOverlay : IStudioOverlay
    {
        public void Draw(StudioOverlayPainter painter, StudioContext context)
        {
            var stage = context.Stage; var set = context.Set;
            if (stage == null || set == null) return;
            bool shoot = context.window.Mode is ShootMode || context.window.Mode is CutMode;
            float now = context.Frame / (float)set.fps;
            foreach (var camera in set.cameras)
            {
                bool selected = ReferenceEquals(camera, context.Selection);
                if (!shoot && !selected || ShootMode.LookThrough == camera.id) continue;
                var color = StudioColors.For(camera.color);
                color.a = selected ? 1 : .55f;
                // The path, sampled along the shot.
                var path = new List<Vector3>();
                float end = camera.End(set);
                for (float t = camera.start; t <= end + .001f; t += Mathf.Max(.05f, (end - camera.start) / 40)) path.Add(StudioCameras.Evaluate(camera, set, stage.tape, stage.anchor, t, t).position);
                painter.Path(path, color, selected ? 2 : 1.5f);
                if (selected && camera.behavior != "ride" && camera.behavior != "orbit")
                    foreach (var key in camera.keys) painter.Dot(stage.ToWorld(key.position), color, 5);
                // Where it is now, with a small frustum showing where it looks and how wide.
                if (now < camera.start - .001f || now > end + .001f) continue;
                var shot = StudioCameras.Evaluate(camera, set, stage.tape, stage.anchor, now, now);
                float reach = selected ? 3 : 1.6f;
                float half = Mathf.Tan(shot.fieldOfView * Mathf.Deg2Rad / 2) * reach, wide = half * set.render.width / Mathf.Max(1, set.render.height);
                Vector3 centre = shot.position + shot.rotation * Vector3.forward * reach;
                Vector3 right = shot.rotation * Vector3.right * wide, up = shot.rotation * Vector3.up * half;
                var corners = new[] { centre - right - up, centre + right - up, centre + right + up, centre - right + up };
                for (int i = 0; i < 4; i++) { painter.Line(shot.position, corners[i], color, 1.5f); painter.Line(corners[i], corners[(i + 1) % 4], color, 1.5f); }
                painter.Dot(shot.position, color, 6);
            }
            if (ShootMode.LookThrough.Length > 0 && ShootMode.SafeAreas)
            {
                // Title-safe (80%) and action-safe (90%) frames of the picture.
                var rect = painter.Picture;
                foreach (float part in new[] { .9f, .8f })
                    painter.ScreenBox(new Rect(rect.x + rect.width * (1 - part) / 2, rect.y + rect.height * (1 - part) / 2, rect.width * part, rect.height * part), new Color(1, 1, 1, .35f));
            }
        }
    }
}
