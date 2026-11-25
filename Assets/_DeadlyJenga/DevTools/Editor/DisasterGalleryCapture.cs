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
                    View(session, name + "-ground", world.Center + new Vector3(.6f, 0, -1).normalized * r * 1.35f + Vector3.up * 1.7f, world.Center + Vector3.up * world.TopHeight * .15f, 70);
                    yield return new WaitForSecondsRealtime(1.5f);
                    View(session, name + "-ground-later", world.Center + new Vector3(-.8f, 0, -1).normalized * r * 1.35f + Vector3.up * 1.7f, world.Center + Vector3.up * world.TopHeight * .15f, 70);
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
            var camera = session.ActiveCamera;
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
            var camera = session.ActiveCamera;
            camera.transform.position = position; camera.transform.LookAt(close ? (found ? focus : center) : target);
            camera.fieldOfView = 60; camera.farClipPlane = 2500;
            Render(session, file, position, focus);
        }

        static void Render(CastlePlaySession session, string file, Vector3 position, Vector3 focus)
        {
            var camera = session.ActiveCamera;
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
            }
            finally { camera.targetTexture = null; RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt); Object.Destroy(texture); }
        }
    }
}
