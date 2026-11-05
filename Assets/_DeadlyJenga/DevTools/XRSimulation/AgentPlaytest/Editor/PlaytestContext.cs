using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AgentPlaytest
{
    public interface IPlaytestScenario { IEnumerator Run(PlaytestContext context); }

    [Serializable] public sealed class PlaytestPlan
    {
        public int schemaVersion = 1;
        public string id, project, output, scene = "", scenario = "AgentPlaytest.SmokeScenario", arguments = "{}";
        public int targetPid, width = 960, height = 540;
        public float timeoutSeconds = 60;
        public bool mute = true;
    }
    [Serializable] public sealed class Assertion
    { public string message; public bool passed; public int frame; }
    [Serializable] public sealed class Capture
    {
        public string file, series, capturedUtc, sha256, mode, context;
        public int frame, width, height;
        public float simulationTime, variance;
        public bool screenSpaceOverlayIncluded;
        public CaptureView view;
    }
    [Serializable] public sealed class CaptureView
    {
        public string camera, scene;
        public Vector3 position;
        public Quaternion rotation;
        public float fieldOfView, nearClip, farClip;
        public int cullingMask;
    }
    [Serializable] public sealed class CaptureReady
    {
        public int schemaVersion = 1, pid;
        public string status = "capture-ready", scenarioStatus = "pending", id, project, scenario;
        public Capture capture;
    }
    [Serializable] public sealed class PlaytestReport
    {
        public string version = "1.0.0", id, project, unityVersion, scenario, status, failure, startedUtc, finishedUtc;
        // Diagnostic boundaries: scenario includes its own finally; excludes runner teardown.
        public string scenarioFinishedUtc, editModeEnteredUtc, restoreFinishedUtc;
        public double scenarioElapsedSeconds = -1, finishToEditSeconds = -1, sceneRestoreSeconds = -1;
        public int pid, firstFrame, lastFrame;
        public double elapsedSeconds;
        public List<Assertion> assertions = new List<Assertion>();
        public List<Capture> captures = new List<Capture>();
        public List<string> errors = new List<string>();
    }

    public sealed class PlaytestContext
    {
        public PlaytestPlan Plan { get; }
        public PlaytestReport Report { get; }
        public PlaytestContext(PlaytestPlan plan, PlaytestReport report) { Plan = plan; Report = report; }
        public void Check(bool condition, string message)
        {
            Report.assertions.Add(new Assertion { passed = condition, message = message, frame = Time.frameCount });
            if (!condition) throw new InvalidOperationException(message);
        }
        public IEnumerator WaitFrames(int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            int first = Time.frameCount;
            while (Time.frameCount - first < count) yield return null;
        }
        public IEnumerator WaitSeconds(float seconds)
        {
            if (seconds < 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end) yield return null;
        }
        public IEnumerator WaitUntil(Func<bool> condition)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            while (!condition()) yield return null;
        }
        public Capture Screenshot(Camera camera, string name, string series = "")
            => Screenshot(camera, name, series, "");
        public Capture Screenshot(Camera camera, string name, string series, string contextJson)
        {
            if (!camera) throw new ArgumentNullException(nameof(camera));
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[] {'/', '\\', ':'}) >= 0 || name == "." || name == "..")
                throw new ArgumentException("Use a plain screenshot name, not a path.");
            string file = Path.Combine(Plan.output, name + ".png");
            if (File.Exists(file)) throw new IOException("Capture already exists: " + file);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(Plan.width, Plan.height, 24);
            Texture2D image = null;
            try
            {
                camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target;
                image = new Texture2D(Plan.width, Plan.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Plan.width, Plan.height), 0, 0); image.Apply();
                var pixels = image.GetPixels32(); double sum = 0, square = 0;
                foreach (var p in pixels) { double l = (p.r + p.g + p.b) / 765.0; sum += l; square += l * l; }
                double mean = sum / pixels.Length;
                var capture = new Capture { file = name + ".png", series = series, frame = Time.frameCount,
                    simulationTime = Time.unscaledTime, variance = (float)Math.Max(0, square / pixels.Length - mean * mean),
                    view = new CaptureView { camera = camera.name, scene = camera.gameObject.scene.path,
                        position = camera.transform.position, rotation = camera.transform.rotation,
                        fieldOfView = camera.fieldOfView, nearClip = camera.nearClipPlane,
                        farClip = camera.farClipPlane, cullingMask = camera.cullingMask } };
                File.WriteAllBytes(file, image.EncodeToPNG());
                PublishCaptureReady(capture, Plan.width, Plan.height, "camera-render", false, contextJson);
                return capture;
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target); if (image) Object.Destroy(image);
            }
        }
        // Also usable after an owner's actual full Game View capture; no additional render.
        public void PublishCaptureReady(Capture capture, int width, int height, string mode,
            bool screenSpaceOverlayIncluded = false, string contextJson = "")
        {
            if (capture == null || string.IsNullOrWhiteSpace(capture.file) ||
                capture.file.IndexOfAny(new[] {'/', '\\', ':'}) >= 0 || !capture.file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Use a PNG leaf name inside this run's output.");
            if (width < 1 || height < 1 || string.IsNullOrWhiteSpace(mode)) throw new ArgumentException("Capture dimensions/mode required.");
            string file = Path.Combine(Plan.output, capture.file);
            string ready = file + ".ready.json";
            if (!File.Exists(file) || File.Exists(ready)) throw new IOException("Missing image or capture receipt already published.");
            var existing = Report.captures.Find(item => item.file == capture.file);
            if (existing != null && !ReferenceEquals(existing, capture)) throw new IOException("Capture name is already recorded.");
            capture.width = width; capture.height = height; capture.mode = mode;
            capture.screenSpaceOverlayIncluded = screenSpaceOverlayIncluded; capture.context = contextJson;
            capture.capturedUtc = DateTime.UtcNow.ToString("O");
            using (var hash = SHA256.Create())
                capture.sha256 = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(file))).Replace("-", "").ToLowerInvariant();
            if (existing == null) Report.captures.Add(capture);
            var receipt = new CaptureReady { id = Plan.id, project = Plan.project, pid = Report.pid,
                scenario = Plan.scenario, capture = capture };
            string temporary = ready + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(receipt, true));
                File.Move(temporary, ready);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        // Samples live simulation; timestamps allow variable-frame-rate encoding afterward.
        public IEnumerator Record(Camera camera, string series, float seconds, int fps)
        {
            if (seconds <= 0 || seconds > 3600 || fps < 1 || fps > 60) throw new ArgumentOutOfRangeException();
            float start = Time.unscaledTime, next = start; int index = 0;
            while (Time.unscaledTime - start < seconds)
            {
                if (Time.unscaledTime >= next)
                {
                    Screenshot(camera, series + "-" + index.ToString("D06"), series); index++;
                    next = Time.unscaledTime + 1f / fps;
                }
                yield return null;
            }
        }
    }
}
