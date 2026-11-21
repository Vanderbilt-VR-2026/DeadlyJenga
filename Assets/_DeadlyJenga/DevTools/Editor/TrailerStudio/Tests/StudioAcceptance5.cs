using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Phase 5 acceptance tests: cameras, time remap, renders and the cut export.
    public static partial class StudioAcceptance
    {
        static IEnumerable<int> UntilRendered()
        {
            float started = Time.realtimeSinceStartup;
            while (StudioRender.Running)
            {
                if (Time.realtimeSinceStartup - started > 420) { StudioRender.Cancel(); throw new TimeoutException("A render did not finish in seven minutes: " + StudioRender.Progress); }
                yield return 0;
            }
        }

        static StudioCameraKey CameraKey(float time, Vector3 position, Vector3 lookAt, float focal = 28) => new StudioCameraKey
            { time = time, position = position, euler = Quaternion.LookRotation(lookAt - position).eulerAngles, focal = focal, aperture = 16 };

        static IEnumerator<int> Phase5Tests()
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
            StudioRun.RecordWholeSet();
            foreach (int wait in UntilHeld()) yield return wait;
            string ada = set.marks[0].id, ben = set.marks[1].id;
            Result("Setup: a finished tape to film", tape.FindType(WorldTrack.Id)?.ActiveTake?.frames.Count == frames, $"{frames} frames, two actors, a collapse at 1.0 s.");

            // ---- Test 1: five cameras over one tape; rendering twice gives the same frames ----
            Vector3 centre = new Vector3(0, 1, 0);
            set.cameras.Clear();
            set.cameras.Add(new StudioCameraTrack { name = "Wide", color = 0, keys = { CameraKey(0, new Vector3(14, 7, -14), centre), CameraKey(4, new Vector3(10, 5, -16), centre) } });
            set.cameras.Add(new StudioCameraTrack { name = "Follow Ada", color = 1, behavior = "follow", subject = ada, noise = "subtle", keys = { CameraKey(0, new Vector3(5, 2.5f, -6), centre, 40) } });
            set.cameras.Add(new StudioCameraTrack { name = "Ride Ben", color = 2, behavior = "ride", subject = ben, keys = { new StudioCameraKey { time = 0, position = new Vector3(2.5f, 1.6f, -3), focal = 32, aperture = 16 } } });
            set.cameras.Add(new StudioCameraTrack { name = "Orbit", color = 3, behavior = "orbit", subject = ada, orbitSpeed = 40, keys = { new StudioCameraKey { time = 0, position = new Vector3(6, 3, 0), focal = 30, aperture = 16 } } });
            set.cameras.Add(new StudioCameraTrack { name = "Two shot", color = 4, behavior = "twoShot", subject = ada, second = ben, noise = "walking",
                keys = { CameraKey(0, new Vector3(-7, 2.2f, -7), centre, 50), CameraKey(2, new Vector3(-4, 3, -9), centre, 50), CameraKey(4, new Vector3(0, 4, -10), centre, 50) } });
            set.render = new StudioRenderSettings { width = 320, height = 180, fps = 15, subFrames = 1, format = "png", cinema = false, freezeAmbient = true };
            StudioLibrary.Save(set);
            string first = Path.Combine(Folder, "render-1"), second = Path.Combine(Folder, "render-2");
            StudioRender.Start(set.cameras, set.render, first);
            foreach (int wait in UntilRendered()) yield return wait;
            var firstResults = StudioRender.Results.ToList();
            StudioRender.Start(set.cameras, set.render, second);
            foreach (int wait in UntilRendered()) yield return wait;
            int expected = Mathf.RoundToInt(set.length * set.render.fps), compared = 0, different = 0, blank = 0;
            foreach (var done in firstResults)
                foreach (string file in Directory.GetFiles(done.folder, "*.png"))
                {
                    byte[] a = File.ReadAllBytes(file), b = File.ReadAllBytes(file.Replace(first, second));
                    compared++;
                    if (!a.SequenceEqual(b)) different++;
                    if (a.Length < 900) blank++;   // a flat, empty picture compresses to almost nothing
                }
            // The five cameras must really see different things.
            var middles = firstResults.Select(d => new FileInfo(Path.Combine(d.folder, $"{expected / 2:D05}.png")).Length).Distinct().Count();
            Result("1. A five-camera shoot over one tape renders every camera; rendering again gives identical frames",
                firstResults.Count == 5 && firstResults.All(d => d.frames == expected) && compared == 5 * expected && different == 0 && blank == 0 && middles == 5,
                $"{firstResults.Count} cameras ({string.Join(", ", firstResults.Select(d => d.camera + " " + d.frames + " frames"))}); expected {expected} frames each at {set.render.fps} fps. " +
                $"{compared} pictures compared byte for byte between the two renders: {different} differ. {middles} of 5 cameras show a different middle frame. " +
                $"Ocean and sky were held still for this check (with them moving, two renders differ in the water and clouds only). Pictures: render-1/, render-2/.");

            // ---- Test 1b: a first-person camera: smooth, level, the actor's own body for that lens only ----
            {
                var fp = new StudioCameraTrack { name = "First person Ada", color = 7, behavior = "firstPerson", subject = ada, keys = { new StudioCameraKey { time = 0, focal = 28, aperture = 4 } } };
                var raw = new StudioCameraTrack { name = "Raw head Ada", color = 7, behavior = "firstPerson", subject = ada, keys = { new StudioCameraKey { time = 0, focal = 28, aperture = 4 } },
                    fpSmoothMove = 0, fpSmoothTurn = 0, fpLevel = false, fpFieldOfView = 0, fpOwnBody = false };
                float rawError = 0, rawTurn = 0, offHead = 0, roll = 0;
                for (float t = .2f; t < set.length - .2f; t += .1f)
                {
                    var head = StudioActorPoses.At(tape, ada, t, fps);
                    var r = StudioCameras.Evaluate(raw, set, tape, stage.anchor, t, t);
                    var s = StudioCameras.Evaluate(fp, set, tape, stage.anchor, t, t);
                    rawError = Mathf.Max(rawError, Vector3.Distance(r.position, head.head)); rawTurn = Mathf.Max(rawTurn, Quaternion.Angle(r.rotation, head.headRotation));
                    offHead = Mathf.Max(offHead, Vector3.Distance(s.position, head.head));
                    roll = Mathf.Max(roll, Mathf.Abs((s.rotation * Vector3.right).y));
                }
                var lens = StudioCameras.Evaluate(fp, set, tape, stage.anchor, 1, 1);
                var probe = new GameObject("First-person probe").AddComponent<Camera>(); probe.enabled = false;
                var other = new GameObject("Other probe").AddComponent<Camera>(); other.enabled = false;
                StudioRun.ShowExact(fps); yield return 0;
                StudioCameras.Apply(probe, lens);
                StudioCameras.Apply(other, StudioCameras.Evaluate(set.cameras[0], set, tape, stage.anchor, 1, 1));
                var rig = StudioFirstPerson.LentTo(probe);
                var benRig = stage.playback.TryGetValue(ben, out var benShown) && benShown ? benShown.GetComponentInChildren<DeadlyJenga.CandyPlayers.CandyPlayerRig>(true) : null;
                bool lentRight = rig && rig.ownerCamera == probe && rig.transform.IsChildOf(stage.playback[ada].transform) && StudioFirstPerson.LentTo(other) == null && (!benRig || !benRig.ownerCamera);
                float near = probe.nearClipPlane;
                StudioCameras.Apply(probe, StudioCameras.Evaluate(set.cameras[0], set, tape, stage.anchor, 1, 1));
                bool released = rig && !rig.ownerCamera && StudioFirstPerson.LentTo(probe) == null && probe.nearClipPlane > near;
                UnityEngine.Object.DestroyImmediate(probe.gameObject); UnityEngine.Object.DestroyImmediate(other.gameObject);
                fp.start = 1; fp.end = 2;
                StudioRender.Start(new[] { fp }, set.render, Path.Combine(Folder, "render-first-person"));
                foreach (int wait in UntilRendered()) yield return wait;
                var fpDone = StudioRender.Results.FirstOrDefault();
                int fpBlank = fpDone == null ? -1 : Directory.GetFiles(fpDone.folder, "*.png").Count(f => new FileInfo(f).Length < 900);
                bool afterRender = !rig || !rig.ownerCamera;   // the render's camera is gone, so nothing still looks out of Ada
                Result("1b. A first-person camera is smooth and level and shows the actor's own body to its own lens only",
                    rawError < .001f && rawTurn < .5f && offHead < .125f && roll < .01f && Mathf.Abs(lens.fieldOfView - 62) < .1f && lens.aperture >= 16 && lens.ownBody == ada &&
                    lentRight && released && near < .05f && fpDone != null && fpDone.frames == Mathf.RoundToInt(set.render.fps) && fpBlank == 0 && afterRender,
                    $"All settings off gives the old raw head lock ({rawError * 100:0.00} cm, {rawTurn:0.00} deg off the head). With the defaults the eye stays within {offHead * 100:0.0} cm of the head, " +
                    $"the largest roll is {Mathf.Asin(Mathf.Clamp01(roll)) * Mathf.Rad2Deg:0.00} deg, the lens is {lens.fieldOfView:0.0} deg tall at f/{lens.aperture:0}. " +
                    $"Ada's played-back rig lent to the first-person lens: {lentRight} (near clip {near:0.00} m; Ben's rig and a second camera untouched); given back when that lens shows another shot: {released}. " +
                    $"A 1 s first-person render: {fpDone?.frames ?? 0} frames, {fpBlank} blank; rig given back after the render: {afterRender}. Pictures: render-first-person/.");
            }

            // ---- Test 2: a speed ramp to a quarter speed and back, with in-between frames ----
            var ramp = new StudioCameraTrack { name = "Ramp", color = 5, keys = { CameraKey(0, new Vector3(9, 5, -11), centre), CameraKey(4, new Vector3(6, 4, -12), centre) },
                speeds = { new StudioSpeedMark { time = 0, speed = 1 }, new StudioSpeedMark { time = .9f, speed = .25f }, new StudioSpeedMark { time = 1.6f, speed = .25f }, new StudioSpeedMark { time = 2.3f, speed = 1 } } };
            set.cameras.Add(ramp);
            var times = StudioCameras.Timeline(ramp, set, 30);
            float slowest = float.MaxValue, fastest = 0, biggestChange = 0, previousStep = -1;
            bool forward = true;
            for (int i = 1; i < times.Count; i++)
            {
                float step = (times[i].tape - times[i - 1].tape) * 30;   // tape seconds per second of film = playback speed
                if (step <= 0) forward = false;
                slowest = Mathf.Min(slowest, step); fastest = Mathf.Max(fastest, step);
                if (previousStep >= 0) biggestChange = Mathf.Max(biggestChange, Mathf.Abs(step - previousStep));
                previousStep = step;
            }
            // In-between frames: a moving block shown at frame f + 0.5 sits halfway between frames f and f + 1.
            int probeFrame = Mathf.RoundToInt(1.4f * fps), moving = -1; float gap = 0;
            StudioRun.Scrub(probeFrame); yield return 0;
            var at = stage.blocks.Select(b => b ? b.transform.position : Vector3.zero).ToArray();
            StudioRun.Scrub(probeFrame + 1); yield return 0;
            var next = stage.blocks.Select(b => b ? b.transform.position : Vector3.zero).ToArray();
            for (int i = 0; i < at.Length; i++) if (Vector3.Distance(at[i], next[i]) > gap) { gap = Vector3.Distance(at[i], next[i]); moving = i; }
            float halfError = -1;
            if (moving >= 0)
            {
                StudioRun.ShowExact(probeFrame + .5f);
                halfError = Vector3.Distance(stage.blocks[moving].transform.position, (at[moving] + next[moving]) * .5f) * 1000;
            }
            set.render = new StudioRenderSettings { width = 320, height = 180, fps = 30, subFrames = 4, format = "png", cinema = false, freezeAmbient = true };
            string rampFolder = Path.Combine(Folder, "render-ramp");
            StudioRender.Start(new[] { ramp }, set.render, rampFolder);
            foreach (int wait in UntilRendered()) yield return wait;
            var rampDone = StudioRender.Results.FirstOrDefault();
            Result("2. A speed ramp to 0.25x and back is smooth (no stepping)",
                forward && slowest > .2f && slowest < .3f && fastest > .95f && biggestChange < .12f && moving >= 0 && halfError >= 0 && halfError < 2 && rampDone != null && Mathf.Abs(rampDone.frames - times.Count) <= 1,
                $"The 4 s tape plays for {times.Count / 30f:0.00} s of film ({times.Count} frames). Playback speed goes from {fastest:0.00}x down to {slowest:0.00}x and back, always forward; " +
                $"the largest change of speed from one frame to the next is {biggestChange:0.000}x (a step would be 0.75x). " +
                $"In-between frames are real: block #{moving} moves {gap * 100:0.0} cm between tape frames {probeFrame} and {probeFrame + 1}, and at {probeFrame}.5 it is {halfError:0.00} mm from the halfway point. " +
                $"Rendered with 4 in-between frames per picture for motion blur: {rampDone?.frames ?? 0} pictures. Pictures: render-ramp/.");

            // ---- Motion blur must not change how bright the picture is ----
            // The same still frame (before the collapse) rendered plain and with 2 in-between frames.
            float Brightness(string file)
            {
                var picture = new Texture2D(2, 2);
                try
                {
                    if (!File.Exists(file) || !picture.LoadImage(File.ReadAllBytes(file))) return -1;
                    float sum = 0; var pixels = picture.GetPixels32();
                    foreach (var c in pixels) sum += (c.r + c.g + c.b) / 3f;
                    return sum / Mathf.Max(1, pixels.Length);
                }
                finally { UnityEngine.Object.DestroyImmediate(picture); }
            }
            var wide = set.cameras.First(c => c.name == "Wide");
            var renderWas = set.render;
            set.render = new StudioRenderSettings { width = 320, height = 180, fps = 15, subFrames = 2, format = "png", cinema = false, freezeAmbient = true };
            string blurFolder = Path.Combine(Folder, "render-blur");
            StudioRender.Start(new[] { wide }, set.render, blurFolder);
            foreach (int wait in UntilRendered()) yield return wait;
            float plain = Brightness(Path.Combine(firstResults.First(r => r.cameraId == wide.id).folder, "00000.png"));
            float blurred = Brightness(Path.Combine(StudioRender.Results.FirstOrDefault(r => r.cameraId == wide.id)?.folder ?? "", "00000.png"));
            Result("A blurred render is as bright as a plain one", plain > 0 && blurred > 0 && Mathf.Abs(plain - blurred) < plain * .03f,
                $"Average brightness of the first Wide frame: {plain:0.0} of 255 with no in-between frames, {blurred:0.0} with 2.");
            set.render = renderWas;

            // ---- Test 3: a handheld camera take becomes an editable track ----
            var handheld = new StudioCameraTrack { name = "Handheld", color = 6 };
            set.cameras.Add(handheld);
            Vector3 PathAt(float t) => stage.anchor + new Vector3(8 - 3 * t, 3 + Mathf.Sin(t * 2) * .5f, -9 + t);
            StudioHandheld.PoseSource = t => (PathAt(t), Quaternion.LookRotation(stage.anchor + Vector3.up - PathAt(t)), 28 + t * 4);
            bool finished = false;
            StudioHandheld.Start(handheld, 0, frames - 1, _ => finished = true);
            foreach (int wait in UntilHeld()) yield return wait;
            yield return 0;
            StudioHandheld.PoseSource = null;
            float worst = 0;
            for (float t = .3f; t < set.length - .3f; t += .37f)
                worst = Mathf.Max(worst, Vector3.Distance(StudioCameras.Evaluate(handheld, set, tape, stage.anchor, t, t).position, PathAt(t)));
            int keysBefore = handheld.keys.Count;
            // "Editable": change one key and the path changes there, like any other camera.
            var edited = handheld.keys[handheld.keys.Count / 2];
            Vector3 beforeEdit = StudioCameras.Evaluate(handheld, set, tape, stage.anchor, edited.time, edited.time).position;
            edited.position += new Vector3(0, 2, 0);
            Vector3 afterEdit = StudioCameras.Evaluate(handheld, set, tape, stage.anchor, edited.time, edited.time).position;
            Result("3. A handheld camera take records and plays back as an editable track (no headset in this run)",
                finished && keysBefore >= 8 && worst < .08f && Mathf.Abs(Vector3.Distance(beforeEdit, afterEdit) - 2) < .05f,
                $"A camera pose was fed in every tape frame while the tape played in real time, the way a tracked controller or the desktop fly camera feeds it. " +
                $"The take became {keysBefore} keys on \"{handheld.name}\" covering {handheld.start:0.00} to {handheld.end:0.00} s; played back, it stays within {worst * 100:0.0} cm of the path that was performed. " +
                $"Raising one key by 2 m moved the camera there by {Vector3.Distance(beforeEdit, afterEdit):0.00} m. " +
                "NOT covered here: reading a real VR controller and the in-headset viewfinder, and flying with mouse and keyboard; both need a person.");

            // ---- Test 4: the cut exports as FCPXML and EDL ----
            // The tape's sound was recorded together with the live world (the whole-set recording in the setup),
            // because block impacts only sound while real physics runs.
            string audioFile = StudioRender.AudioFile(set.id);
            var soundEngine = DeadlyJenga.CandyAudio.CandyAudioEngine.Instance;
            string soundNote = "This sound was recorded during the live whole-set recording, not during a playback. " +
                (!soundEngine ? "The game's sound engine was not in the scene." : $"Application focused: {Application.isFocused}.");
            yield return 0;
            set.cuts.Clear();
            set.cuts.Add(new StudioCut { camera = set.cameras[0].id, from = 0, to = 1.5f });
            set.cuts.Add(new StudioCut { camera = set.cameras[1].id, from = 1, to = 2.5f, dissolve = .4f });
            set.cuts.Add(new StudioCut { camera = set.cameras[4].id, from = 2, to = 4 });
            set.title = "Acceptance cut";
            string xmlFile = "", edlFile = "", exportNote = "";
            List<string> problems = new List<string>();
            int clips = 0, assets = 0, missing = 0, soundClips = 0, transitions = 0; string duration = "";
            try
            {
                xmlFile = StudioExport.WriteFcpxml(set, first, audioFile, out problems);
                edlFile = StudioExport.WriteEdl(set, first, out _);
                var document = new XmlDocument { XmlResolver = null };
                var reader = XmlReader.Create(xmlFile, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
                document.Load(reader); reader.Close();
                foreach (XmlNode asset in document.SelectNodes("//asset/media-rep"))
                {
                    assets++;
                    if (!File.Exists(new Uri(asset.Attributes["src"].Value).LocalPath)) missing++;
                }
                clips = document.SelectNodes("//spine/asset-clip").Count;
                soundClips = document.SelectNodes("//spine/asset-clip/asset-clip[@ref='s1']").Count;
                transitions = document.SelectNodes("//spine/transition").Count;
                duration = document.SelectSingleNode("//sequence").Attributes["duration"].Value;
            }
            catch (Exception error) { exportNote = error.Message; }
            long audioBytes = File.Exists(audioFile) ? new FileInfo(audioFile).Length : 0;
            int stems = Directory.Exists(Path.Combine(Path.GetDirectoryName(audioFile ?? "") ?? "", "stems")) ? Directory.GetFiles(Path.Combine(Path.GetDirectoryName(audioFile), "stems"), "*.wav").Length : 0;
            // A sound file full of zeros is silence. Find the loudest 16-bit sample after the 44-byte WAV header.
            int loudest = 0;
            if (audioBytes > 44)
            {
                byte[] wav = File.ReadAllBytes(audioFile);
                for (int i = 44; i + 1 < wav.Length; i += 2) loudest = Mathf.Max(loudest, Mathf.Abs((short)(wav[i] | (wav[i + 1] << 8))));
            }
            // The collapse must be heard too, not only the music: the World stem holds block impacts.
            int worldLoudest = 0;
            string worldStem = audioBytes > 44 ? Path.Combine(Path.GetDirectoryName(audioFile), "stems", "World.wav") : "";
            if (File.Exists(worldStem))
            {
                byte[] wav = File.ReadAllBytes(worldStem);
                for (int i = 44; i + 1 < wav.Length; i += 2) worldLoudest = Mathf.Max(worldLoudest, Mathf.Abs((short)(wav[i] | (wav[i + 1] << 8))));
            }
            // A dissolve is two EDL lines with one event number, so count event numbers, not lines.
            int edlEvents = 0;
            var xmlFaults = File.Exists(xmlFile) ? CheckFcpxml(xmlFile) : new List<string> { "no FCPXML file" };
            var edlFaults = File.Exists(edlFile) ? CheckEdl(edlFile, firstResults.Count > 0 ? firstResults[0].fps : 15, out edlEvents) : new List<string> { "no EDL file" };
            int edlLines = File.Exists(edlFile) ? File.ReadAllLines(edlFile).Count(l => l.Length > 3 && char.IsDigit(l[0]) && char.IsDigit(l[2])) : 0;
            Result("4. The cut exports as FCPXML and EDL with shots and sound in place, and both pass the format checks (opening them in Resolve was NOT done)",
                exportNote == "" && clips == 3 && missing == 0 && assets >= 4 && soundClips == 3 && transitions == 1 && edlEvents == 3 && edlLines == 4 && xmlFaults.Count == 0 && edlFaults.Count == 0
                    && audioBytes > 10000 && loudest > 0 && worldLoudest > 0,
                (exportNote != "" ? "Export failed: " + exportNote + ". " : "") +
                $"FCPXML is well-formed XML with {clips} shots on the main storyline, {transitions} cross dissolve, game sound under all {soundClips} shots, sequence length {duration}; " +
                $"all {assets} media files it points at exist ({missing} missing). FCPXML structure and timing check: {(xmlFaults.Count == 0 ? "no faults" : string.Join("; ", xmlFaults))}. " +
                $"EDL has {edlEvents} events on {edlLines} lines (the dissolve is a cut line and a D line); CMX 3600 check: {(edlFaults.Count == 0 ? "no faults" : string.Join("; ", edlFaults))}. " +
                $"The tape's sound was recorded: {audioBytes / 1024} KB with {stems} stems; loudest sample {loudest} of 32767{(loudest == 0 ? " (SILENT: nothing was heard on the stage)" : "")}; block impacts (World stem) loudest sample {worldLoudest}{(worldLoudest == 0 ? " (SILENT)" : "")}. " +
                $"Notes from the exporter: {(problems.Count == 0 ? "none" : string.Join("; ", problems))}. " +
                soundNote + " " +
                "NOT run: importing the file into DaVinci Resolve or Premiere. That needs one of those apps and a person; the files are in render-1/.");

            StudioLibrary.Save(set);
            Result("Runtime errors during the Studio tests", exceptions == 0, exceptions == 0 && errorLines.Count == 0 ? "None." : $"{exceptions} exceptions. First messages: {string.Join(" // ", errorLines)}");
            StudioRun.SaveTape();
        }
    }
}
