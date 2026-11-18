using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Films the finished tape: one image sequence per camera, at any speed, in a queue that runs while
    // the window stays usable. Nothing in the project settings is changed; quality is raised only
    // during the render and put back afterwards.
    public static class StudioRender
    {
        public sealed class Finished
        {
            public string camera = "", cameraId = "", folder = "", movie = "", error = "";
            public int frames, fps, width, height;
            public float seconds;
        }

        public static bool Running { get; private set; }
        public static string Progress { get; private set; } = "";
        public static readonly List<Finished> Results = new List<Finished>();
        public static string LastFolder { get; private set; } = "";
        public static event Action Changed;

        static IEnumerator<int> routine;
        static int lastTick = -1;
        static bool hooked, cancel;

        public static string RendersFolder(string setId) => Path.Combine(StudioLibrary.TapeFolder(setId), "renders");
        public static string AudioFile(string setId) => Path.Combine(StudioLibrary.TapeFolder(setId), "audio", "tape.wav");

        public static void Start(IEnumerable<StudioCameraTrack> cameras, StudioRenderSettings settings, string folder = null)
        {
            if (Running) throw new InvalidOperationException("A render is already running.");
            if (StudioRun.State != StudioState.Held) throw new InvalidOperationException("Open the stage, and wait for any run to finish.");
            var list = cameras.Where(c => c != null).ToList();
            if (list.Count == 0) throw new InvalidOperationException("Add a camera first.");
            var stage = StudioRun.Stage;
            if (stage.performer.Length > 0) StudioRun.LeaveBody();   // the performer's own rig must not be in the shot
            LastFolder = folder ?? Path.Combine(RendersFolder(stage.set.id), DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Results.Clear();
            cancel = false; Running = true;
            routine = Routine(stage, list, settings, LastFolder);
            if (!hooked) { EditorApplication.update += Tick; hooked = true; }
        }

        public static void Cancel() { if (Running) cancel = true; }

        static void Tick()
        {
            if (routine == null || !EditorApplication.isPlaying || Time.frameCount == lastTick) { if (routine != null && !EditorApplication.isPlaying) Stop("Play Mode ended."); return; }
            lastTick = Time.frameCount;
            try { if (!routine.MoveNext()) Stop(""); }
            catch (Exception error) { Debug.LogException(error); Stop(error.Message); }
        }

        static void Stop(string error)
        {
            routine = null; Running = false;
            Restore();
            Progress = error.Length > 0 ? "Render stopped: " + error : $"Rendered {Results.Count} shot(s) to {LastFolder}";
            Debug.Log("[TrailerStudio] " + Progress);
            try { Changed?.Invoke(); } catch (Exception problem) { Debug.LogException(problem); }
        }

        // ---- Quality and look, only while rendering ----

        // The settings a cinema render raises, as they were before. Kept in SessionState as well, so a
        // script reload or a stopped Play Mode in the middle of a render can still put them back.
        [Serializable] sealed class OldQuality { public int level, msaa, antiAliasing, maxLod; public float shadows, lod, urpShadows; public bool urp; }
        const string RaisedKey = "TrailerStudio.RenderRaised";
        static bool raised;
        static OldQuality old;
        static float oldScale;
        static Camera camera;
        static GameObject volumeHost;
        static VolumeProfile profile;
        static Texture2D lutTexture;

        static void Raise(StudioRenderSettings settings)
        {
            oldScale = Time.timeScale;
            if (!settings.cinema || raised) return;
            raised = true;
            old = new OldQuality { level = QualitySettings.GetQualityLevel() };
            // The highest quality level the project defines (its PC tier), without changing which one is saved.
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
            old.shadows = QualitySettings.shadowDistance; old.lod = QualitySettings.lodBias; old.antiAliasing = QualitySettings.antiAliasing;
            old.maxLod = QualitySettings.maximumLODLevel;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp) { old.urp = true; old.msaa = urp.msaaSampleCount; old.urpShadows = urp.shadowDistance; }
            SessionState.SetString(RaisedKey, JsonUtility.ToJson(old));
            QualitySettings.lodBias = Mathf.Max(old.lod, 4);
            QualitySettings.maximumLODLevel = 0;
            if (urp)
            {
                urp.msaaSampleCount = 8;
                urp.shadowDistance = Mathf.Max(old.urpShadows, 180);
            }
        }

        static void LowerQuality(OldQuality was)
        {
            if (was.urp && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) { urp.msaaSampleCount = was.msaa; urp.shadowDistance = was.urpShadows; }
            QualitySettings.lodBias = was.lod; QualitySettings.shadowDistance = was.shadows; QualitySettings.antiAliasing = was.antiAliasing;
            QualitySettings.maximumLODLevel = was.maxLod;
            QualitySettings.SetQualityLevel(was.level, true);
            SessionState.EraseString(RaisedKey);
        }

        // After a script reload the "raised" flag above is gone. If a render was cut short, undo it now.
        [InitializeOnLoadMethod]
        static void LowerQualityAfterReload()
        {
            string saved = SessionState.GetString(RaisedKey, "");
            if (saved.Length == 0 || raised) return;
            try { LowerQuality(JsonUtility.FromJson<OldQuality>(saved)); }
            catch (Exception error) { Debug.LogException(error); SessionState.EraseString(RaisedKey); }
        }

        static void Restore()
        {
            Time.captureDeltaTime = 0;
            if (StudioRun.State == StudioState.Held) { Time.timeScale = 0; Time.captureFramerate = StudioRun.Stage != null ? StudioRun.Stage.Fps : 0; }
            if (raised) { LowerQuality(old); raised = false; }
            if (camera) Object.Destroy(camera.gameObject);
            if (volumeHost) Object.Destroy(volumeHost);
            if (profile) Object.Destroy(profile);
            camera = null; volumeHost = null; profile = null;
        }

        static void BuildCamera(StudioRenderSettings settings)
        {
            var rig = new GameObject("Trailer Studio render camera");
            camera = rig.AddComponent<Camera>();
            camera.enabled = false;
            camera.nearClipPlane = .08f; camera.farClipPlane = 1600;
            camera.clearFlags = CameraClearFlags.Skybox; camera.allowHDR = true;
            var data = camera.GetUniversalAdditionalCameraData();
            data.allowXRRendering = false; data.renderPostProcessing = true; data.requiresDepthTexture = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            volumeHost = new GameObject("Trailer Studio shot look");
            var volume = volumeHost.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 2000;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;
        }

        // The per-shot look: depth of field from the lens, plus bloom, vignette, grain, colour fringes and a LUT.
        public static void ApplyLook(VolumeProfile target, StudioCameraTrack shot, StudioCameras.Shot lens)
        {
            T Get<T>() where T : VolumeComponent => target.TryGet<T>(out var found) ? found : target.Add<T>(true);
            var focus = Get<DepthOfField>();
            focus.active = lens.aperture < 15.5f;
            focus.mode.Override(DepthOfFieldMode.Bokeh);
            focus.focusDistance.Override(lens.focus); focus.aperture.Override(Mathf.Clamp(lens.aperture, 1, 32)); focus.focalLength.Override(Mathf.Clamp(lens.focal, 1, 300));
            var bloom = Get<Bloom>(); bloom.active = shot.bloom > .001f; bloom.intensity.Override(shot.bloom * 2); bloom.threshold.Override(.9f);
            var vignette = Get<Vignette>(); vignette.active = shot.vignette > .001f; vignette.intensity.Override(Mathf.Clamp01(shot.vignette) * .55f);
            var grain = Get<FilmGrain>(); grain.active = shot.grain > .001f; grain.intensity.Override(Mathf.Clamp01(shot.grain));
            var fringe = Get<ChromaticAberration>(); fringe.active = shot.chroma > .001f; fringe.intensity.Override(Mathf.Clamp01(shot.chroma));
            var lookup = Get<ColorLookup>();
            lookup.active = false;
            if (!string.IsNullOrEmpty(shot.lut))
            {
                if (!lutTexture || lutTexture.name != shot.lut)
                {
                    string guid = AssetDatabase.FindAssets(shot.lut + " t:Texture2D").FirstOrDefault();
                    lutTexture = guid == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                }
                if (lutTexture) { lookup.active = true; lookup.texture.Override(lutTexture); lookup.contribution.Override(1); }
            }
        }

        // ---- The render itself ----

        const int WarmUpFrames = 12;

        static IEnumerator<int> Routine(StudioStage stage, List<StudioCameraTrack> shots, StudioRenderSettings settings, string root)
        {
            var set = stage.set;
            int sub = Mathf.Clamp(settings.subFrames, 1, 32);
            int open = Mathf.Max(1, Mathf.RoundToInt(sub * Mathf.Clamp(settings.shutter, .05f, 1)));   // sub-frames the shutter is open for
            int width = Mathf.Clamp(settings.width, 64, 4096) & ~1, height = Mathf.Clamp(settings.height, 64, 4096) & ~1;
            bool exr = settings.format == "exr";
            Raise(settings);
            BuildCamera(settings);
            var target = new RenderTexture(width, height, 24, exr ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32) { name = "Trailer Studio render" };
            var reader = new Texture2D(width, height, exr || sub > 1 ? TextureFormat.RGBAFloat : TextureFormat.RGB24, false, exr);
            var writer = sub > 1 && !exr ? new Texture2D(width, height, TextureFormat.RGBA32, false) : null;
            float[] sum = sub > 1 ? new float[width * height * 4] : null;
            int idleRate = Application.targetFrameRate;
            Application.targetFrameRate = -1;   // the idle stage is capped; a render goes as fast as it can
            try
            {
                foreach (var shot in shots)
                {
                    if (cancel) break;
                    var watch = Stopwatch.StartNew();
                    string folder = Path.Combine(root, ShotFolder(shot));
                    Directory.CreateDirectory(folder);
                    var times = StudioCameras.Timeline(shot, set, settings.fps, sub);
                    int frames = times.Count / sub;
                    var done = new Finished { camera = shot.name, cameraId = shot.id, folder = folder, fps = settings.fps, width = width, height = height };
                    if (times.Count > 0)
                    {
                        // Throw-away pictures of the first frame. Whatever was on screen before the render
                        // (a different tape time, moving cloth and hair, effects) needs a few frames to settle,
                        // and the shot's first saved frames must not depend on it.
                        if (settings.freezeAmbient) Time.timeScale = 0;
                        var first = StudioCameras.Evaluate(shot, set, stage.tape, stage.anchor, times[0].tape, times[0].camera);
                        camera.targetTexture = target;
                        for (int warm = 0; warm < WarmUpFrames && !cancel; warm++)
                        {
                            StudioRun.ShowExact(times[0].tape * set.fps);
                            StudioCameras.Apply(camera, first);
                            ApplyLook(profile, shot, first);
                            camera.Render();
                            yield return 0;
                        }
                    }
                    for (int n = 0; n < frames && !cancel; n++)
                    {
                        if (sum != null) Array.Clear(sum, 0, sum.Length);
                        for (int s = 0; s < open; s++)
                        {
                            var (tape, cameraTime) = times[n * sub + s];
                            // The game clock moves by the same amount as the tape, so ocean, sky and effects slow down with it.
                            float step = n * sub + s + 1 < times.Count ? Mathf.Max(0, times[n * sub + s + 1].tape - tape) : 1f / (settings.fps * sub);
                            if (settings.freezeAmbient) Time.timeScale = 0;
                            else { Time.timeScale = 1; Time.captureDeltaTime = Mathf.Max(.00002f, step); }
                            StudioRun.ShowExact(tape * set.fps);
                            var lens = StudioCameras.Evaluate(shot, set, stage.tape, stage.anchor, tape, cameraTime);
                            StudioCameras.Apply(camera, lens);
                            ApplyLook(profile, shot, lens);
                            camera.targetTexture = target;
                            camera.Render();
                            var old = RenderTexture.active;
                            RenderTexture.active = target;
                            reader.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                            RenderTexture.active = old;
                            if (sum != null)
                            {
                                var pixels = reader.GetRawTextureData<float>();
                                for (int i = 0; i < sum.Length; i++) sum[i] += pixels[i];
                            }
                            // One engine frame per sub-frame, so time really passes between them.
                            if (!settings.freezeAmbient || s == open - 1) yield return 0;
                        }
                        string file = Path.Combine(folder, $"{n:D05}." + (exr ? "exr" : "png"));
                        if (sum != null)
                        {
                            // Average the sub-frames: that is the motion blur and the smooth edges.
                            float scale = 1f / open;
                            if (exr)
                            {
                                var pixels = reader.GetRawTextureData<float>();
                                for (int i = 0; i < sum.Length; i++) pixels[i] = sum[i] * scale;
                                reader.Apply(false);
                                File.WriteAllBytes(file, reader.EncodeToEXR(Texture2D.EXRFlags.CompressZIP));
                            }
                            else
                            {
                                var bytes = writer.GetRawTextureData<byte>();
                                // The summed values are linear light; a PNG holds display (gamma) values. Alpha stays as it is.
                                bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
                                for (int i = 0; i < sum.Length; i++)
                                {
                                    float value = sum[i] * scale;
                                    if (linear && (i & 3) != 3) value = Mathf.LinearToGammaSpace(value);
                                    bytes[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255), 0, 255);
                                }
                                writer.Apply(false);
                                File.WriteAllBytes(file, writer.EncodeToPNG());
                            }
                        }
                        else
                        {
                            reader.Apply(false);
                            File.WriteAllBytes(file, exr ? reader.EncodeToEXR(Texture2D.EXRFlags.CompressZIP) : reader.EncodeToPNG());
                        }
                        done.frames = n + 1;
                        Progress = $"Rendering {shot.name}: frame {n + 1} of {frames}";
                        if (n % 5 == 0) try { Changed?.Invoke(); } catch { }
                    }
                    done.seconds = (float)watch.Elapsed.TotalSeconds;
                    File.WriteAllText(Path.Combine(folder, "shot.json"), JsonUtility.ToJson(new ShotRecord
                    { name = shot.name, id = shot.id, frames = done.frames, fps = settings.fps, width = width, height = height, tapeStart = shot.start, tapeEnd = shot.End(set), subFrames = sub, format = settings.format }, true));

                    // A movie file for review or grading, when FFmpeg is on this machine. The image sequence is always kept.
                    if (!cancel && done.frames > 0 && (settings.format == "mp4" || settings.format == "prores" || settings.format == "png"))
                    {
                        string encoder = FindEncoder();
                        if (encoder.Length > 0)
                        {
                            bool prores = settings.format == "prores";
                            string movie = Path.Combine(folder, Safe(shot.name) + (prores ? ".mov" : ".mp4"));
                            string codec = prores ? "-c:v prores_ks -profile:v 3 -pix_fmt yuv422p10le" : "-c:v libx264 -preset fast -crf 16 -pix_fmt yuv420p -movflags +faststart";
                            var process = Encode(encoder, folder, $"-hide_banner -loglevel error -nostdin -y -framerate {settings.fps.ToString(CultureInfo.InvariantCulture)} -i %05d.png {codec} \"{movie}\"");
                            var timer = Stopwatch.StartNew();
                            while (process != null && !process.HasExited && timer.Elapsed.TotalMinutes < 10) { Progress = "Encoding " + shot.name; yield return 0; }
                            if (process != null && process.HasExited && process.ExitCode == 0 && File.Exists(movie)) done.movie = movie;
                            else done.error = "FFmpeg could not make the movie; the image sequence is complete.";
                            if (process != null && !process.HasExited) { try { process.Kill(); } catch (Exception) { } }
                            process?.Dispose();
                        }
                        else if (settings.format != "png") done.error = "FFmpeg was not found, so only the image sequence was written.";
                    }
                    Results.Add(done);
                }
            }
            finally
            {
                camera.targetTexture = null;
                Object.Destroy(target); Object.Destroy(reader); if (writer) Object.Destroy(writer);
                Restore();
                Application.targetFrameRate = idleRate;
                if (StudioRun.State == StudioState.Held) StudioRun.Scrub(StudioRun.Frame);
            }
        }

        [Serializable] public sealed class ShotRecord
        {
            public string name, id, format;
            public int frames, fps, width, height, subFrames;
            public float tapeStart, tapeEnd;
        }

        // Two cameras may share a name (or differ only in punctuation), so the folder carries the camera's id too.
        public static string ShotFolder(StudioCameraTrack shot) => Safe(shot.name) + "-" + shot.id;

        public static string Safe(string name)
        {
            var clean = new string((name ?? "shot").Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-').ToArray()).Trim('-');
            return clean.Length == 0 ? "shot" : clean;
        }

        public static string FindEncoder()
        {
            try { return SpectatorRecordingPlan.FindEncoder("") ?? ""; }
            catch { return ""; }
        }

        static Process Encode(string encoder, string folder, string arguments)
        {
            try
            {
                var process = new Process { StartInfo = new ProcessStartInfo(encoder) { WorkingDirectory = folder, Arguments = arguments, UseShellExecute = false, CreateNoWindow = true } };
                return process.Start() ? process : null;
            }
            catch (Exception error) { Debug.LogWarning("Trailer Studio: FFmpeg did not start. " + error.Message); return null; }
        }

        // ---- Sound for the tape ----

        // New sound is recorded into its own folder first, so a stopped or failed recording never
        // destroys the sound that was already there.
        static string AudioFolder(string setId) => Path.GetDirectoryName(AudioFile(setId));
        static string NewAudioFolder(string setId) => Path.Combine(AudioFolder(setId), "recording");

        internal static TheatreAudioCapture OpenAudioFile(string setId)
        {
            string folder = NewAudioFolder(setId);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            return new TheatreAudioCapture(Path.Combine(folder, "tape.wav"));
        }

        // The new sound is complete: put it in place of the old one, and note which world take it was made from.
        internal static void KeepAudioFile(string setId, string worldTakeId)
        {
            string folder = NewAudioFolder(setId), file = AudioFile(setId), stems = Path.Combine(AudioFolder(setId), "stems");
            if (!File.Exists(Path.Combine(folder, "tape.wav"))) return;
            if (File.Exists(file)) File.Delete(file);
            if (Directory.Exists(stems)) Directory.Delete(stems, true);
            File.Move(Path.Combine(folder, "tape.wav"), file);
            if (Directory.Exists(Path.Combine(folder, "stems"))) Directory.Move(Path.Combine(folder, "stems"), stems);
            File.WriteAllText(Path.Combine(AudioFolder(setId), "tape.take.txt"), worldTakeId ?? "");
            Directory.Delete(folder, true);
        }

        internal static void DropAudioFile(string setId)
        {
            string folder = NewAudioFolder(setId);
            try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch (Exception error) { Debug.LogException(error); }
        }

        // The world take the saved sound was recorded with ("" if unknown or there is no sound).
        public static string AudioTakeId(string setId)
        {
            string note = Path.Combine(AudioFolder(setId), "tape.take.txt");
            return File.Exists(note) ? File.ReadAllText(note).Trim() : "";
        }

        // A whole-set recording already records its sound, with real block impacts. This is the fallback
        // for a tape that has none: it can only hear music, disasters and recorded sound events, because
        // played-back blocks do not really collide.
        // Plays the whole tape once at the fixed clock and records the game mix and its stems.
        // Sounds come from the tape's sound events and the disasters' own audio, heard from head height at the stage spot.
        public static void RecordAudio(Action<string> finished = null)
        {
            if (StudioRun.State != StudioState.Held) throw new InvalidOperationException("Open the stage, and wait for any run to finish.");
            var stage = StudioRun.Stage;
            string file = AudioFile(stage.set.id);
            var capture = OpenAudioFile(stage.set.id);
            StudioCamera.EarsAt = stage.anchor + Vector3.up * 1.6f;
            int frames = 0;
            void Each(int frame) { capture.CaptureFrame(); frames++; }
            void Done()
            {
                if (StudioRun.State == StudioState.Running) return;     // still playing
                StudioRun.FrameShown -= Each; StudioRun.Changed -= Done;
                StudioCamera.EarsAt = null;
                // Finished properly only if the same stage is holding still again; a closed stage throws the sound away.
                bool whole = StudioRun.State == StudioState.Held && StudioRun.Stage == stage && frames > 0;
                try { if (whole) capture.Complete(frames, stage.Fps); } finally { capture.Dispose(); }
                if (!whole) { DropAudioFile(stage.set.id); return; }
                KeepAudioFile(stage.set.id, stage.tape.FindType(WorldTrack.Id)?.ActiveTake?.id);
                finished?.Invoke(file);
            }
            StudioRun.FrameShown += Each;
            StudioRun.Play(0, -1, false);
            StudioRun.Changed += Done;
        }
    }
}
