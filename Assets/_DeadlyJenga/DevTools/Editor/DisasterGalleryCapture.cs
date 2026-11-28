using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyArt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Pictures of every catalog disaster in the real Game scene (Candy Castle, demo mode, no bots).
    // For each disaster: launch with no warning, then a wide shot early, and a wide and a close shot later.
    // Run in batch: -executeMethod DeadlyJenga.DevTools.Editor.DisasterGalleryCapture.RunBatch
    // Pictures go to Logs/Captures/<time>-Disaster-Gallery with a contact sheet. Changes nothing in the project.
    [InitializeOnLoad]
    public static class DisasterGalleryCapture
    {
        const string Key = "DisasterGalleryCapture";
        static IEnumerator run;
        static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
        static CaptureOutput output;
        static bool failed;
        static string Report => ProjectPath.Report("DisasterGallery.txt");

        static DisasterGalleryCapture() { EditorApplication.update += Tick; EditorApplication.playModeStateChanged += State; }

        [MenuItem("Tools/Deadly Jenga/Captures/Disaster Gallery (Play Mode)")]
        public static void RunBatch() => Start("");

        // Only the tsunami, with extra framed shots (side profile, low front, three-quarter). Fits a short job.
        [MenuItem("Tools/Deadly Jenga/Captures/Tsunami Shape (Play Mode)")]
        public static void RunTsunamiBatch() => Start("Tsunami");

        // Only the tornado, with a low hero view from the island, a base close-up and a wide view.
        [MenuItem("Tools/Deadly Jenga/Captures/Tornado Look (Play Mode)")]
        public static void RunTornadoBatch() => Start("Tornado");

        // The tsunami once per look-only skin (sand, snow, lava) on the castle map.
        [MenuItem("Tools/Deadly Jenga/Captures/Tsunami Skins (Play Mode)")]
        public static void RunTsunamiSkinsBatch() => Start("TsunamiSkins");

        // Acid rain once per look-only skin (lava, dry ice) on the castle map.
        [MenuItem("Tools/Deadly Jenga/Captures/Rain Skins (Play Mode)")]
        public static void RunRainSkinsBatch() => Start("RainSkins");

        // Meteor shower and Planet Killer with the icy skin.
        [MenuItem("Tools/Deadly Jenga/Captures/Icy Meteors (Play Mode)")]
        public static void RunIceMeteorsBatch() => Start("IceMeteors");

        // Only Icey, with close views of frosted blocks and icicles.
        [MenuItem("Tools/Deadly Jenga/Captures/Icey Frost (Play Mode)")]
        public static void RunIceyBatch() => Start("Icey");

        // Only Fire, with close and wide views of the burning tower (Blender flipbook flames).
        [MenuItem("Tools/Deadly Jenga/Captures/Fire Look (Play Mode)")]
        public static void RunFireBatch() => Start("Fire");

        // Meteor shower and Planet Killer hitting the castle: shards, flash and dust plumes.
        [MenuItem("Tools/Deadly Jenga/Captures/Meteor Impacts (Play Mode)")]
        public static void RunMeteorImpactsBatch() => Start("MeteorImpacts");

        // Every approved disaster through its live phases: buildup (warning), peak, aftermath (after it is stopped)
        // and replay (triggered again), from an overview and a player-eye camera, with the pixel audit on each frame.
        [MenuItem("Tools/Deadly Jenga/Captures/Disaster Phases (Play Mode)")]
        public static void RunPhasesBatch() => Start("Phases");

        static void Start(string only)
        {
            SessionState.SetString(Key + "Only", only);
            SessionState.SetBool(Key, true);
            File.WriteAllText(Report, "Disaster gallery: Game.unity, castle map, demo mode. " + DateTime.Now.ToString("O") + "\n");
            MatchSetup.MapId = "castle"; MatchSetup.ModeId = "demo"; MatchSetup.BotTarget = 1; MatchSetup.HandCount = 0;
            MatchSetup.FromLobby = true; MatchSetup.StartLocalSession = false;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }

        static void State(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                failed = false; AudioListener.volume = 0; Application.runInBackground = true;
                run = Capture();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                if (Application.isBatchMode) EditorApplication.Exit(failed ? 2 : 0);
            }
        }

        static void Tick()
        {
            if (run == null) return;
            try
            {
                if (steps.Count == 0) steps.Push(run);
                while (steps.Count > 0)
                {
                    var step = steps.Peek();
                    if (!step.MoveNext()) { steps.Pop(); continue; }
                    if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
                    return;
                }
                File.AppendAllText(Report, "COMPLETE\n");
            }
            catch (Exception error) { failed = true; File.AppendAllText(Report, "FAIL " + error + "\n"); }
            steps.Clear(); run = null;
            output?.Finish(false);
            EditorApplication.ExitPlaymode();
            // Batch jobs quit straight away; waiting for the play-mode exit callback left Unity hanging after captures.
            if (Application.isBatchMode)
            {
                bool failedRun = failed;
                EditorApplication.delayCall += () => EditorApplication.Exit(failedRun ? 2 : 0);
            }
        }

        static IEnumerator Capture()
        {
            float deadline = Time.realtimeSinceStartup + 65;
            while ((Services.Session?.Demo == null || Services.World == null) && Time.realtimeSinceStartup < deadline) yield return null;
            var session = Object.FindFirstObjectByType<CastlePlaySession>();
            if (!session || Services.World == null || !session.ActiveCamera) throw new InvalidOperationException("Game session or camera is unavailable.");
            if (session.Network) throw new InvalidOperationException("Refusing to run in a network session.");
            session.SurvivorBots.AutoFill = false; session.SurvivorBots.Clear(); session.HandBots?.Clear();
            if (session.Social != null) session.Social.VoiceEnabled = false;
            session.SetRole(true); session.SetMenu(false); session.Player.InputSuspended = true; session.Hand.InputSuspended = true;
            string only = SessionState.GetString(Key + "Only", "");
            output = new CaptureOutput(only == "" ? "Disaster Gallery" : only + " Shape");
            if (only == "TsunamiSkins") { yield return SkinShots(session); DemoDisasters.StopAll(session); yield break; }
            if (only == "RainSkins") { yield return RainSkinShots(session); DemoDisasters.StopAll(session); yield break; }
            if (only == "Phases") { yield return PhaseShots(session); DemoDisasters.StopAll(session); yield break; }
            if (only == "MeteorImpacts")
            {
                foreach (var meteorName in new[] { "MeteorShower", "PlanetKillerMeteor" })
                {
                    var meteorPrefab = DemoDisasters.Prefabs.FirstOrDefault(p => p.name == meteorName);
                    if (!meteorPrefab) continue;
                    DemoDisasters.StopAll(session); session.ResetMap();
                    yield return new WaitForSecondsRealtime(1.5f);
                    var meteorParameters = DemoDisasters.Parameters(meteorPrefab);
                    for (int i = 0; i < meteorParameters.Count; i++) if (meteorParameters[i].id == "warning") DemoDisasters.SetValue(session, meteorPrefab, i, 0);
                    File.AppendAllText(Report, "Impacts-" + meteorName + ": " + DemoDisasters.Request(session, meteorPrefab) + "\n");
                    var world = Services.World; float r = Mathf.Max(world.Radius, 8);
                    bool planet = meteorName == "PlanetKillerMeteor";
                    yield return new WaitForSecondsRealtime(planet ? 2f : 3f);
                    for (int moment = 0; moment < (planet ? 8 : 6); moment++)
                    {
                        float distance = planet ? r * 7 : r * 2.6f;
                        View(session, "Impacts-" + meteorName + "-" + moment, world.Center + new Vector3(.65f, .45f, -1).normalized * distance, world.Center + Vector3.up * world.TopHeight * .35f, planet ? 60 : 62);
                        yield return new WaitForSecondsRealtime(planet ? 1f : .7f);
                    }
                }
                DemoDisasters.StopAll(session); yield break;
            }
            if (only == "IceMeteors")
            {
                CandyDisasterVisuals.MeteorSkinOverride = CandyDisasterVisuals.MeteorSkin.Ice;
                try
                {
                    foreach (var meteorName in new[] { "MeteorShower", "PlanetKillerMeteor" })
                    {
                        var meteorPrefab = DemoDisasters.Prefabs.FirstOrDefault(p => p.name == meteorName);
                        if (!meteorPrefab) continue;
                        DemoDisasters.StopAll(session); session.ResetMap();
                        yield return new WaitForSecondsRealtime(1.5f);
                        var meteorParameters = DemoDisasters.Parameters(meteorPrefab);
                        for (int i = 0; i < meteorParameters.Count; i++) if (meteorParameters[i].id == "warning") DemoDisasters.SetValue(session, meteorPrefab, i, 0);
                        File.AppendAllText(Report, "Ice-" + meteorName + ": " + DemoDisasters.Request(session, meteorPrefab) + "\n");
                        for (int moment = 0; moment < 4; moment++)
                        {
                            yield return new WaitForSecondsRealtime(moment == 0 ? 2.5f : 1.2f);
                            MeteorShot(session, "Ice-" + meteorName + "-m" + moment);
                        }
                    }
                }
                finally { CandyDisasterVisuals.MeteorSkinOverride = null; }
                DemoDisasters.StopAll(session); yield break;
            }
            int index = 0;
            foreach (var prefab in DemoDisasters.Prefabs)
            {
                index++;
                if (only != "" && prefab.name != only) continue;
                string name = index.ToString("00") + "-" + prefab.name.Replace(" ", "");
                DemoDisasters.StopAll(session); session.ResetMap();
                yield return new WaitForSecondsRealtime(1.5f);
                var parameters = DemoDisasters.Parameters(prefab);
                for (int i = 0; i < parameters.Count; i++) if (parameters[i].id == "warning") DemoDisasters.SetValue(session, prefab, i, 0);
                string result = DemoDisasters.Request(session, prefab);
                File.AppendAllText(Report, name + ": " + result + "\n");
                yield return new WaitForSecondsRealtime(3f);
                Shot(session, name + "-a-wide-3s", false);
                yield return new WaitForSecondsRealtime(5f);
                if (prefab.name == "Tsunami")
                {
                    float until = Time.realtimeSinceStartup + 20;
                    while (Time.realtimeSinceStartup < until && !Tracked.Waves.Any(w => w.Wave.active && w.Wave.height > 18)) yield return null;
                }
                Shot(session, name + "-b-wide-8s", false);
                Shot(session, name + "-c-close-8s", true);
                if (prefab.name == "Tsunami") yield return TsunamiShots(session, name);
                if (prefab.name == "Tornado") yield return TornadoShots(session, name);
                if (prefab.name == "Fire" && only == "Fire")
                {
                    var world = Services.World; float r = Mathf.Max(world.Radius, 8);
                    yield return new WaitForSecondsRealtime(4f);
                    View(session, name + "-flames-close", world.Center + new Vector3(.6f, 0, -1).normalized * r * 1.2f + Vector3.up * world.TopHeight * .6f, world.Center + Vector3.up * world.TopHeight * .55f, 55);
                    View(session, name + "-flames-wide", world.Center + new Vector3(-.7f, .3f, -1).normalized * r * 2.6f + Vector3.up * world.TopHeight * .5f, world.Center + Vector3.up * world.TopHeight * .5f, 55);
                }
                if (prefab.name == "Icey" && only == "Icey")
                {
                    var world = Services.World; float r = Mathf.Max(world.Radius, 8);
                    View(session, name + "-frost-close", world.Center + new Vector3(.6f, 0, -1).normalized * r * 1.25f + Vector3.up * world.TopHeight * .55f, world.Center + Vector3.up * world.TopHeight * .5f, 55);
                    View(session, name + "-icicles-low", world.Center + new Vector3(-.7f, 0, -1).normalized * r * 1.15f + Vector3.up * 1.6f, world.Center + Vector3.up * world.TopHeight * .45f, 75);
                    View(session, name + "-top", world.Center + new Vector3(.3f, 1.2f, -1).normalized * r * 1.6f + Vector3.up * world.TopHeight, world.Center + Vector3.up * world.TopHeight * .8f, 55);
                }
            }
            DemoDisasters.StopAll(session);
        }

        static IEnumerator TsunamiShots(CastlePlaySession session, string name)
        {
            var source = Tracked.Waves.FirstOrDefault(w => w.Wave.active);
            if (source == null) { File.AppendAllText(Report, "  no active wave for tsunami shots\n"); yield break; }
            for (int round = 0; round < 2; round++)
            {
                var w = source.Wave;
                Vector3 across = Vector3.Cross(Vector3.up, w.direction).normalized, up = Vector3.up, mid = w.center + up * w.height * .5f;
                float span = Mathf.Max(w.height * 2.2f, w.depth * 1.6f);
                View(session, name + "-t" + round + "-side-profile", mid + across * span, mid, 50);
                View(session, name + "-t" + round + "-front-low", w.center + w.direction * (w.depth * .5f + w.height * 1.4f) + up * 2 + across * w.height * .4f, w.center + up * w.height * .65f, 70);
                View(session, name + "-t" + round + "-three-quarter", w.center + (w.direction * .9f + across * .7f).normalized * span * 1.4f + up * w.height * .6f, mid, 55);
                yield return new WaitForSecondsRealtime(3f);
            }
            yield return ImpactShots(session, name, source);
        }

        static IEnumerator SkinShots(CastlePlaySession session)
        {
            var prefab = DemoDisasters.Prefabs.FirstOrDefault(p => p.name == "Tsunami");
            if (!prefab) throw new InvalidOperationException("Tsunami prefab missing from the demo catalog.");
            try
            {
                foreach (var skin in new[] { CandyTsunamiVisuals.WaveSkin.Sand, CandyTsunamiVisuals.WaveSkin.Snow, CandyTsunamiVisuals.WaveSkin.Lava })
                {
                    CandyTsunamiVisuals.SkinOverride = skin;
                    string name = "Tsunami-" + skin;
                    DemoDisasters.StopAll(session); session.ResetMap();
                    yield return new WaitForSecondsRealtime(1.5f);
                    var parameters = DemoDisasters.Parameters(prefab);
                    for (int i = 0; i < parameters.Count; i++) if (parameters[i].id == "warning") DemoDisasters.SetValue(session, prefab, i, 0);
                    File.AppendAllText(Report, name + ": " + DemoDisasters.Request(session, prefab) + "\n");
                    float until = Time.realtimeSinceStartup + 25;
                    while (Time.realtimeSinceStartup < until && !Tracked.Waves.Any(w => w.Wave.active && w.Wave.height > 18)) yield return null;
                    var source = Tracked.Waves.FirstOrDefault(w => w.Wave.active);
                    if (source == null) { File.AppendAllText(Report, "  no active wave\n"); continue; }
                    var wv = source.Wave;
                    Vector3 across = Vector3.Cross(Vector3.up, wv.direction).normalized, mid = wv.center + Vector3.up * wv.height * .5f;
                    float span = Mathf.Max(wv.height * 2.2f, wv.depth * 1.6f);
                    View(session, name + "-side", mid + across * span, mid, 50);
                    View(session, name + "-three-quarter", wv.center + (wv.direction * .9f + across * .7f).normalized * span * 1.4f + Vector3.up * wv.height * .6f, mid, 55);
                    var world = Services.World;
                    until = Time.realtimeSinceStartup + 30;
                    while (Time.realtimeSinceStartup < until && source.Wave.active &&
                           Vector3.Dot(world.Center - source.Wave.center, source.Wave.direction.normalized) > source.Wave.depth * .1f) yield return null;
                    if (!source.Wave.active) continue;
                    wv = source.Wave;
                    Vector3 dir = Vector3.ProjectOnPlane(wv.direction, Vector3.up).normalized, side = Vector3.Cross(Vector3.up, dir);
                    Vector3 castle = world.Center + Vector3.up * world.TopHeight * .45f;
                    float r = Mathf.Max(world.Radius, 8);
                    View(session, name + "-hit", castle + side * (r * 2.6f + 18) - dir * r * .6f + Vector3.up * world.TopHeight * .35f, castle, 60);
                }
            }
            finally { CandyTsunamiVisuals.SkinOverride = null; }
        }

        static IEnumerator PhaseShots(CastlePlaySession session)
        {
            auditFailures = 0; int frames = 0;
            var world = Services.World; float r = Mathf.Max(world.Radius, 8);
            foreach (var kind in new[] { "Tsunami", "Tornado", "AcidRain", "Icey", "Earthquake", "Fire", "MeteorShower", "PlanetKillerMeteor" })
            {
                var prefab = DemoDisasters.Prefabs.FirstOrDefault(p => p.name == kind);
                if (!prefab) { File.AppendAllText(Report, "  FAIL missing prefab " + kind + "\n"); auditFailures++; continue; }
                DemoDisasters.StopAll(session); session.ResetMap();
                yield return new WaitForSecondsRealtime(1.5f);
                // Buildup: a short warning so the approach / build-up state is visible.
                File.AppendAllText(Report, kind + ": started=" + Launch(session, prefab, 3) + "\n");
                yield return new WaitForSecondsRealtime(1.6f);
                PhasePair(session, kind, "1-buildup", r); frames += 2;
                // Peak: well into the active phase (the tsunami waits for a tall wave).
                float until = Time.realtimeSinceStartup + (kind == "Tsunami" ? 20 : kind == "PlanetKillerMeteor" ? 15 : 0);
                hazardEnded = false; GameEvents.HazardEnded += OnHazardEnded;
                yield return new WaitForSecondsRealtime(kind == "Tsunami" ? 3 : kind == "Earthquake" ? 3 : kind == "MeteorShower" ? 4.5f : kind == "PlanetKillerMeteor" ? 1.5f : 5.5f);
                while (Time.realtimeSinceStartup < until && !Tracked.Waves.Any(w => w.Wave.active && w.Wave.height > 18) && !hazardEnded) yield return null;
                GameEvents.HazardEnded -= OnHazardEnded;
                // The Planet Killer ends on impact: shoot the blast just after it lands.
                if (kind == "PlanetKillerMeteor") { File.AppendAllText(Report, "  impact seen=" + hazardEnded + "\n"); yield return new WaitForSecondsRealtime(.7f); }
                PhasePair(session, kind, "2-peak", r); frames += 2;
                // Aftermath: stopped; residue (charred blocks, puddles, smoke) and cleanup.
                DemoDisasters.StopAll(session);
                yield return new WaitForSecondsRealtime(2f);
                PhasePair(session, kind, "3-aftermath", r); frames += 2;
                // Replay: the same disaster again with no warning, to prove it starts clean.
                File.AppendAllText(Report, kind + " replay: started=" + Launch(session, prefab, 0) + "\n");
                yield return new WaitForSecondsRealtime(kind == "Tsunami" ? 8 : 3.5f);
                Shot(session, kind + "-4-replay-overview", false); frames++;
            }
            string summary = $"Disaster phase gallery: {frames} frames, {auditFailures} failed the pixel audit";
            File.AppendAllText(Report, summary + "\n"); Debug.Log(summary);
            if (auditFailures > 0) failed = true;
        }

        // Starts a disaster the way game modes do (ExecuteForMode is not gated by the shared demo-controls policy),
        // with an explicit warning time.
        static bool hazardEnded;
        static void OnHazardEnded(HazardType type, Vector3 position) => hazardEnded = true;

        static bool Launch(CastlePlaySession session, GameObject prefab, float warning)
        {
            var values = DemoDisasters.Values(session, prefab).ToArray(); var parameters = DemoDisasters.Parameters(prefab);
            for (int i = 0; i < parameters.Count && i < values.Length; i++) if (parameters[i].id == "warning") values[i] = warning;
            bool ok = DemoDisasters.ExecuteForMode(session, prefab.name, values);
            if (!ok) { File.AppendAllText(Report, "  FAIL " + prefab.name + " did not start\n"); auditFailures++; }
            return ok;
        }

        static Camera captureCamera;
        static Camera CaptureCamera(CastlePlaySession session)
        {
            if (session && session.ActiveCamera) return session.ActiveCamera;
            if (Camera.main) return Camera.main;
            if (!captureCamera) captureCamera = new GameObject("Capture camera (gallery)").AddComponent<Camera>();
            return captureCamera;
        }

        static void SetParameter(CastlePlaySession session, GameObject prefab, string id, float value)
        {
            var parameters = DemoDisasters.Parameters(prefab);
            for (int i = 0; i < parameters.Count; i++) if (parameters[i].id == id) DemoDisasters.SetValue(session, prefab, i, value);
        }

        // Overview (the existing wide framing) plus a player standing at the castle's foot looking at the danger.
        static void PhasePair(CastlePlaySession session, string kind, string phase, float r)
        {
            Shot(session, kind + "-" + phase + "-overview", false);
            var world = Services.World; var focus = Focus(out bool found);
            // Stand on open ground (or the water line) outside the castle, not on top of blocks.
            Vector3 eye = world.Center + new Vector3(.6f, 0, -1).normalized * r * 1.7f;
            if (Physics.Raycast(eye + Vector3.up * 50, Vector3.down, out var hit, 120, 1 << Layers.Surface)) eye = hit.point;
            else { float water = world.WaterHeight(eye); eye.y = float.IsFinite(water) ? water : world.Center.y; }
            eye += Vector3.up * 1.7f;
            View(session, kind + "-" + phase + "-player-eye", eye, found ? Vector3.Lerp(world.Center + Vector3.up * world.TopHeight * .4f, focus, .6f) : world.Center + Vector3.up * world.TopHeight * .45f, 75);
        }

        static IEnumerator RainSkinShots(CastlePlaySession session)
        {
            var prefab = DemoDisasters.Prefabs.FirstOrDefault(p => p.name == "AcidRain");
            if (!prefab) throw new InvalidOperationException("AcidRain prefab missing from the demo catalog.");
            try
            {
                foreach (var skin in new[] { CandyDisasterVisuals.RainSkin.Acid, CandyDisasterVisuals.RainSkin.Lava, CandyDisasterVisuals.RainSkin.DryIce })
                {
                    CandyDisasterVisuals.RainSkinOverride = skin;
                    string name = "Rain-" + skin;
                    DemoDisasters.StopAll(session); session.ResetMap();
                    yield return new WaitForSecondsRealtime(1.5f);
                    var parameters = DemoDisasters.Parameters(prefab);
                    for (int i = 0; i < parameters.Count; i++) if (parameters[i].id == "warning") DemoDisasters.SetValue(session, prefab, i, 0);
                    File.AppendAllText(Report, name + ": " + DemoDisasters.Request(session, prefab) + "\n");
                    yield return new WaitForSecondsRealtime(7f);
                    var world = Services.World;
                    Vector3 castle = world.Center + Vector3.up * world.TopHeight * .4f;
                    float r = Mathf.Max(world.Radius, 8);
                    View(session, name + "-wide", castle + new Vector3(.7f, .35f, -1).normalized * r * 3.2f, castle, 60);
                    // Outside the castle at mid height (streaks against the walls), and above the top looking down
                    // across the block tops where most landings splash.
                    View(session, name + "-side", world.Center + new Vector3(.6f, 0, -1).normalized * r * 2.3f + Vector3.up * world.TopHeight * .35f, world.Center + Vector3.up * world.TopHeight * .45f, 60);
                    View(session, name + "-tops", world.Center + new Vector3(.5f, 0, -1).normalized * r * 1.1f + Vector3.up * (world.TopHeight + 4), world.Center + Vector3.up * world.TopHeight * .75f, 70);
                    yield return new WaitForSecondsRealtime(1.2f);
                    View(session, name + "-tops-later", world.Center + new Vector3(-.6f, 0, -1).normalized * r * 1.1f + Vector3.up * (world.TopHeight + 4), world.Center + Vector3.up * world.TopHeight * .75f, 70);
                }
            }
            finally { CandyDisasterVisuals.RainSkinOverride = null; }
        }

        // Frame the biggest meteor in flight from beside and slightly behind it, so the trail reads.
        static void MeteorShot(CastlePlaySession session, string file)
        {
            DisasterPoint best = default; bool found = false;
            foreach (var state in DisasterStates.Active)
            {
                var frame = state?.Capture();
                if (frame?.points == null) continue;
                foreach (var p in frame.points) if (!found || p.size > best.size) { best = p; found = true; }
            }
            if (!found) { File.AppendAllText(Report, "  no meteor in flight for " + file + "\n"); return; }
            Vector3 dir = best.velocity.sqrMagnitude > .01f ? best.velocity.normalized : Vector3.down;
            Vector3 side = Vector3.Cross(Vector3.up, dir); if (side.sqrMagnitude < .01f) side = Vector3.right; side.Normalize();
            float back = Mathf.Max(6, best.size * 5);
            View(session, file, best.position - dir * back * .6f + side * back + Vector3.up * best.size * 1.5f, best.position + dir * best.size, 50);
        }

        static IEnumerator TornadoShots(CastlePlaySession session, string name)
        {
            for (int round = 0; round < 2; round++)
            {
                var frame = DisasterStates.Active.Select(s => s?.Capture()).FirstOrDefault(f => f != null && f.kind == "Tornado");
                if (frame == null || frame.values == null || frame.values.Length < 2) { File.AppendAllText(Report, "  no active tornado frame\n"); yield break; }
                float height = frame.values[0], size = frame.values[1];
                Vector3 foot = frame.center, toCastle = Vector3.ProjectOnPlane(Services.World.Center - foot, Vector3.up);
                Vector3 side = toCastle.sqrMagnitude > 1 ? toCastle.normalized : Vector3.forward, across = Vector3.Cross(Vector3.up, side);
                // Cameras stand on the far side from the castle so the castle sits behind the tornado, not in front.
                View(session, name + "-r" + round + "-hero-low", foot - side * size * 4.5f + across * size * 1.5f + Vector3.up * 2.5f, foot + Vector3.up * height * .42f, 70);
                View(session, name + "-r" + round + "-base", foot - side * size * 3.4f - across * size * 1.6f + Vector3.up * size * 1.1f, foot + Vector3.up * size * .6f, 62);
                View(session, name + "-r" + round + "-wide", foot + (side + across * .6f).normalized * height * 2.4f + Vector3.up * height * .35f, foot + Vector3.up * height * .5f, 50);
                yield return new WaitForSecondsRealtime(2.5f);
            }
        }

        // The wave meeting the castle: splash-back on the upstream face, the gap refilling behind it,
        // blocks carried along. Waits for the front to reach the castle, then shoots three moments.
        static IEnumerator ImpactShots(CastlePlaySession session, string name, IWaveState source)
        {
            var world = Services.World;
            float until = Time.realtimeSinceStartup + 30;
            while (Time.realtimeSinceStartup < until && source.Wave.active)
            {
                var w = source.Wave;
                if (Vector3.Dot(world.Center - w.center, w.direction.normalized) < w.depth * .25f) break;
                yield return null;
            }
            if (!source.Wave.active) { File.AppendAllText(Report, "  wave ended before reaching the castle\n"); yield break; }
            for (int moment = 0; moment < 3; moment++)
            {
                File.AppendAllText(Report, "  impact moment " + moment + " active=" + source.Wave.active + " t=" + Time.realtimeSinceStartup.ToString("0.0") + "\n");
                if (!source.Wave.active) break;
                var w = source.Wave;
                Vector3 dir = Vector3.ProjectOnPlane(w.direction, Vector3.up).normalized, across = Vector3.Cross(Vector3.up, dir);
                Vector3 castle = world.Center + Vector3.up * world.TopHeight * .45f;
                float r = Mathf.Max(world.Radius, 8);
                View(session, name + "-hit" + moment + "-side", castle + across * (r * 2.6f + 18) - dir * r * .6f + Vector3.up * world.TopHeight * .35f, castle, 60);
                View(session, name + "-hit" + moment + "-lee", castle + dir * (r * 2.8f + 12) + across * r * .8f + Vector3.up * world.TopHeight * 1.1f, castle - dir * r, 62);
                View(session, name + "-hit" + moment + "-close", castle + across * r * 1.5f - dir * r * 1.1f + Vector3.up * world.TopHeight * .2f, castle - dir * r * .3f, 70);
                yield return new WaitForSecondsRealtime(.8f);
            }
        }

        static void View(CastlePlaySession session, string file, Vector3 position, Vector3 target, float fov)
        {
            var camera = CaptureCamera(session);
            camera.transform.position = position; camera.transform.LookAt(target); camera.fieldOfView = fov; camera.farClipPlane = 2500;
            Render(session, file, position, target);
        }

        // The disaster's own center (tornado, kraken, wave) when it publishes one, else the map center.
        static Vector3 Focus(out bool found)
        {
            found = false;
            var wave = Tracked.Waves.FirstOrDefault(w => w.Wave.active);
            if (wave != null) { found = true; return wave.Wave.center + Vector3.up * wave.Wave.height * .4f; }
            foreach (var state in DisasterStates.Active)
            {
                var frame = state?.Capture();
                if (frame != null && frame.center != Vector3.zero) { found = true; return frame.center; }
            }
            return Services.World.Center;
        }

        static void Shot(CastlePlaySession session, string file, bool close)
        {
            var world = Services.World;
            var focus = Focus(out bool found);
            var center = world.Center + Vector3.up * world.TopHeight * .45f;
            var target = found ? Vector3.Lerp(center, focus, .5f) : center;
            float span = Mathf.Max(world.Radius * 2, Vector3.Distance(center, focus) * 2, 30);
            Vector3 position;
            if (close) position = world.Center + new Vector3(.75f, 0, -1).normalized * world.Radius * 1.25f + Vector3.up * world.TopHeight * .7f;
            else position = target + new Vector3(.7f, .5f, -1).normalized * span * 1.5f;
            var camera = CaptureCamera(session);
            camera.transform.position = position; camera.transform.LookAt(close ? (found ? focus : center) : target);
            camera.fieldOfView = 60; camera.farClipPlane = 2500;
            Render(session, file, position, focus);
        }

        static void Render(CastlePlaySession session, string file, Vector3 position, Vector3 focus)
        {
            var camera = CaptureCamera(session);
            foreach (var art in Object.FindObjectsByType<CandyDisasterVisuals>(FindObjectsSortMode.None))
                typeof(CandyDisasterVisuals).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(art, null);
            foreach (var art in Object.FindObjectsByType<CandyTsunamiVisuals>(FindObjectsSortMode.None))
                typeof(CandyTsunamiVisuals).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(art, null);
            var rt = RenderTexture.GetTemporary(1280, 720, 24); var old = RenderTexture.active;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                string path = output.PathFor(file + ".png");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                output.Add(path, file, "image", 1280, 720);
                File.AppendAllText(Report, "  shot " + file + " camera=" + position + " focus=" + focus + "\n");
                Audit(texture, file);
            }
            finally { camera.targetTexture = null; RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt); Object.Destroy(texture); }
        }

        // Pixel audit for every frame: missing-shader magenta and blown-out white are logged as PASS/FAIL lines.
        // Snow and frost are legitimately white, so only a frame that is mostly pure white counts as a whiteout.
        static void Audit(Texture2D texture, string file)
        {
            var pixels = texture.GetPixels32(); int magenta = 0, white = 0, black = 0;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                var p = pixels[i];
                if (p.r > 220 && p.b > 220 && p.g < 60) magenta++;
                if (p.r > 250 && p.g > 250 && p.b > 250) white++;
                if (p.r < 4 && p.g < 4 && p.b < 4) black++;
            }
            float n = pixels.Length / 4f, m = magenta / n, w = white / n, b = black / n;
            bool ok = m < .01f && w < .35f && b < .6f;
            string line = (ok ? "PASS" : "FAIL") + $" frame {file}: magenta={m:P1} white={w:P1} black={b:P1}";
            File.AppendAllText(Report, "  " + line + "\n");
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
            if (!ok) auditFailures++;
        }
        static int auditFailures;
    }
}
