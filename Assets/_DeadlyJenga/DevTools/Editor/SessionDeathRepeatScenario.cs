using System;
using System.IO;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.DevTools.Editor
{
    // Explicitly invoked by the existing continuous session driver, never on editor load.
    public sealed class SessionDeathRepeatScenario : IDisposable
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly string output;
        readonly bool repeatDuringWipe;
        readonly StreamWriter log;
        readonly IDisposable subscription;
        readonly float deadline;
        int stage, firstOverlay, secondOverlay, eliminatedEvents;
        float fullSince;
        bool disposed, repeatHeld;
        public bool Complete { get; private set; }
        public string Failure { get; private set; }

        public SessionDeathRepeatScenario(string directory, bool repeat, float timeoutSeconds = 90)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Requires an actual playing session.");
            output = Path.GetFullPath(directory);
            Directory.CreateDirectory(output);
            if (File.Exists(Path.Combine(output, "death-lifecycle.jsonl")))
                throw new InvalidOperationException("Use a fresh evidence directory; preserve prior attempts.");
            repeatDuringWipe = repeat;
            deadline = Time.realtimeSinceStartup + Mathf.Clamp(timeoutSeconds, 5, 180);
            log = new StreamWriter(Path.Combine(output, "death-lifecycle.jsonl"), false, new System.Text.UTF8Encoding(false));
            try
            {
                Type observer = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    observer = observer ?? assembly.GetType("DeadlyJenga.XRSimulation.Editor.ManagedFrameObserver", false);
                var register = observer?.GetMethod("Register", BindingFlags.Public | BindingFlags.Static);
                if (register == null) throw new InvalidOperationException("Existing QA post-LateUpdate observer is required; no substitute loop is installed.");
                subscription = (IDisposable)register.Invoke(null, new object[] { (Action)Observe });
                GameEvents.Eliminated += Eliminated;
                Write("armed", null, 0, 0);
            }
            catch { log.Dispose(); throw; }
        }

        // Normal public gameplay ability; no private alive state or synthetic event mutation.
        public void EliminateLocal()
        {
            var session = Services.Session;
            var match = MatchRuntime.Current;
            var player = session?.Player;
            var damage = player?.gameObject.GetComponent<IDamageable>();
            if (session == null || match?.Frame?.phase != RoundPhase.Playing || session.PlayingHand ||
                session.MenuOpen || damage == null || damage.Health <= 0)
                throw new InvalidOperationException("Local living survivor in an actual Playing frame with menu closed is required.");
            Write("eliminate-command", null, 0, 0);
            damage.Eliminate(null);
        }

        void Eliminated(GameObject who, GameObject source)
        {
            if (who != Services.Session?.Player?.gameObject) return;
            eliminatedEvents++;
            Write("local-eliminated-event", null, 0, 0);
        }

        static T Read<T>(object owner, string name)
            => (T)owner.GetType().GetField(name, Fields).GetValue(owner);

        void Observe()
        {
            if (disposed || Complete || Failure != null) return;
            try
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Death lifecycle did not reach its expected genuine respawn/view transition.");
                var overlays = UnityEngine.Object.FindObjectsByType<CandyDeathOverlay>(FindObjectsSortMode.None);
                CandyDeathOverlay overlay = null;
                int visible = 0;
                foreach (var candidate in overlays)
                    if (Read<Canvas>(candidate, "canvas")?.enabled == true) { visible++; overlay = candidate; }
                if (visible > 1) throw new InvalidOperationException("More than one enabled death overlay.");
                float age = overlay ? Time.unscaledTime - Read<float>(overlay, "began") : 0;
                float at = overlay ? Read<float>(overlay, "wipeAt") : -1;
                float wipe = at < 0 ? 0 : Mathf.Clamp01((Time.unscaledTime - at) / .65f);
                if (stage == 0 && overlay && age >= .28f && wipe == 0)
                {
                    firstOverlay = overlay.GetInstanceID(); fullSince = Time.unscaledTime;
                    Capture("first-full", overlay, age, wipe); stage = 1;
                }
                else if (stage == 1 && overlay && wipe > 0 && wipe < 1)
                {
                    Capture("first-wipe", overlay, age, wipe);
                    if (repeatDuringWipe)
                    {
                        // This intentionally fails if the driver changed screens instead of genuinely respawning.
                        EliminateLocal(); stage = 2;
                    }
                    else stage = 4;
                }
                else if (stage == 2 && overlay && age >= .28f && wipe == 0)
                {
                    secondOverlay = overlay.GetInstanceID();
                    if (secondOverlay == firstOverlay) throw new InvalidOperationException("A death during the wipe reused the old splat.");
                    if (eliminatedEvents < 2) throw new InvalidOperationException("Missing two actual local elimination events.");
                    fullSince = Time.unscaledTime; Capture("repeat-full", overlay, age, wipe); stage = 3;
                }
                else if (stage == 3 && overlay)
                {
                    if (overlay.GetInstanceID() != secondOverlay) throw new InvalidOperationException("Delayed state replaced the second death overlay again.");
                    if (wipe > 0) { Capture("repeat-wipe", overlay, age, wipe); stage = 4; }
                    else if (!repeatHeld && Time.unscaledTime - fullSince > 1)
                    { repeatHeld = true; Write("repeat-held", overlay, age, wipe); }
                }
                else if (stage == 4 && overlays.Length == 0)
                {
                    Capture("cleared", null, 0, 1); Complete = true;
                }
            }
            catch (Exception error) { Failure = error.ToString(); Write("failed", null, 0, 0); }
        }

        void Capture(string name, CandyDeathOverlay overlay, float age, float wipe)
        {
            Write(name, overlay, age, wipe);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
        }

        [Serializable] sealed class Sample
        {
            public string phase, failure, assembly, scene;
            public int frame, overlay, events, stage;
            public float realtime, simulation, age, wipe;
        }
        void Write(string phase, CandyDeathOverlay overlay, float age, float wipe)
        {
            log.WriteLine(JsonUtility.ToJson(new Sample { phase = phase, failure = Failure,
                assembly = typeof(SessionDeathRepeatScenario).Module.ModuleVersionId.ToString(),
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                frame = Time.frameCount, overlay = overlay ? overlay.GetInstanceID() : 0,
                events = eliminatedEvents, stage = stage, realtime = Time.realtimeSinceStartup,
                simulation = Time.time, age = age, wipe = wipe }));
            log.Flush();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            GameEvents.Eliminated -= Eliminated;
            subscription.Dispose();
            log.Dispose();
        }
    }
}
