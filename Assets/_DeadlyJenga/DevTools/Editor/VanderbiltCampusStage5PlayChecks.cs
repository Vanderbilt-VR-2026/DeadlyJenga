using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Stage 5 Play Mode checks in the real Game.unity on the campus (Stage 5 "Done when" 2):
    //   bots:  towers built and standing, one realtime sun, streaming active, then 8 survivor bots for 60 s:
    //          PASS when no exceptions and at least 6 of them walked more than 1 m.
    //   modes: King of the Hill and Capture the Flag each started for 20 s through session.Modes.Configure +
    //          StartRound (as ChristmasTownChecks does): PASS when the round is playing, the authored hill / flag
    //          homes from the campus match layout are used, spawn queries land inside the play area, no exceptions.
    //   all:   both, in one Play Mode session (used by VanderbiltCampusStage5Checks.RunAllBatch).
    // Each run always exits: clean-up in try/finally and an 8-minute watchdog. Report: Logs/Validation/VanderbiltCampus-stage5-play.txt
    [InitializeOnLoad]
    public static class VanderbiltCampusStage5PlayChecks
    {
        const string Key = "VanderbiltCampusStage5Play";
        const double WatchdogSeconds = 8 * 60;
        const int BotCount = 8;
        [Serializable] sealed class SavedScenes { public SceneSetup[] values; }
        static int exceptions, failures;
        static string step = "starting";
        static double enteredAt;
        static bool finished;
        static string Suite => SessionState.GetString(Key + "Suite", "all");
        internal static string Report => ProjectPath.Report("VanderbiltCampus-stage5-play.txt");

        static VanderbiltCampusStage5PlayChecks()
        {
            EditorApplication.update += Watchdog;
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    exceptions = failures = 0; finished = false; step = "waiting for the session";
                    enteredAt = EditorApplication.timeSinceStartup;
                    Application.logMessageReceived += Log;
                    ChooseMatch();
                    EditorApplication.delayCall += () => Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Guard());
                }
                if (state == PlayModeStateChange.EnteredEditMode) BackInEditMode();
            };
        }

        [MenuItem("Tools/Deadly Jenga/Validation/Vanderbilt Campus Bot Smoke Test")]
        public static void BotSmokeBatch() => Start("bots", false);

        [MenuItem("Tools/Deadly Jenga/Validation/Vanderbilt Campus Modes (hill + ctf)")]
        public static void ModesBatch() => Start("modes", false);

        // Called by RunAllBatch after the edit-mode checks; editFailed carries their result into the exit code.
        internal static void Start(string suite, bool editFailed)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            if (scenes.Any(s => s.isLoaded && UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))
                throw new InvalidOperationException("Save edited scenes before the campus play check.");
            SessionState.SetString(Key + "Suite", suite); SessionState.SetBool(Key + "EditFailed", editFailed);
            SessionState.SetString(Key + "Scenes", JsonUtility.ToJson(new SavedScenes { values = scenes }));
            SessionState.SetString(Key + "Choices", JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key + "Lobby", MatchSetup.FromLobby); SessionState.SetBool(Key + "Local", MatchSetup.StartLocalSession);
            MatchSetup.FromLobby = false; MatchSetup.StartLocalSession = false; ChooseMatch();
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Failed", true);
            File.WriteAllText(Report, $"Campus Stage 5 play check ({suite}): actual Game.unity. Desktop editor only; no headset or two-player claim.\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity")); EditorApplication.EnterPlaymode();
        }

        static void ChooseMatch()
        {
            MatchSetup.MapId = VanderbiltCampusBuilder.MapId; MatchSetup.ModeId = "demo";
            MatchSetup.BotTarget = 0; MatchSetup.HandCount = 0;
        }

        static void BackInEditMode()
        {
            try
            {
                Application.logMessageReceived -= Log; SessionState.SetBool(Key, false);
                SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key + "Choices", "")));
                MatchSetup.FromLobby = SessionState.GetBool(Key + "Lobby", false); MatchSetup.StartLocalSession = SessionState.GetBool(Key + "Local", false);
                var saved = JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key + "Scenes", "{}"))?.values;
                if (saved != null && saved.Length > 0 && saved.Any(s => s.isActive && s.isLoaded) && saved.All(s => !string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(saved);
            }
            catch (Exception e) { Debug.LogWarning("[VanderbiltCampus] stage 5 clean-up: " + e.Message); }
            finally
            {
                bool failed = SessionState.GetBool(Key + "Failed", true) || SessionState.GetBool(Key + "EditFailed", false);
                VanderbiltCampusData.Exit(failed ? 1 : 0);
            }
        }

        static void Watchdog()
        {
            if (finished || !SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup - enteredAt < WatchdogSeconds) return;
            finished = true;
            File.AppendAllText(Report, "FAIL watchdog timeout after 8 minutes during: " + step + "\nRESULT FAIL\n");
            SessionState.SetBool(Key + "Failed", true);
            EditorApplication.ExitPlaymode();
        }

        // Exceptions fail the run. PhysX convex warnings are not counted here (the main play check summarizes them).
        static void Log(string message, string trace, LogType type)
        {
            if (type != LogType.Exception) return;
            exceptions++; File.AppendAllText(Report, "EXCEPTION " + message + "\n");
        }

        static bool Expect(bool pass, string message)
        {
            File.AppendAllText(Report, (pass ? "PASS " : "FAIL ") + message + "\n");
            if (!pass) failures++;
            return pass;
        }

        static IEnumerator Guard()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Check()); bool crashed = false;
            while (stack.Count > 0 && !finished)
            {
                bool next = false; object current = null; Exception failure = null;
                try { next = stack.Peek().MoveNext(); if (next) current = stack.Peek().Current; } catch (Exception e) { failure = e; }
                if (failure != null) { File.AppendAllText(Report, "FAIL during " + step + ": " + failure.Message + "\n"); crashed = true; break; }
                if (!next) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return current;
            }
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            if (finished) yield break;
            finished = true;
            bool failed = crashed || failures > 0 || exceptions > 0;
            File.AppendAllText(Report, $"Exceptions={exceptions} failed checks={failures}\n" + (failed ? "RESULT FAIL" : "RESULT PASS") + "\n");
            SessionState.SetBool(Key + "Failed", failed);
            EditorApplication.ExitPlaymode();
        }

        static IEnumerator Check()
        {
            step = "session start";
            var session = Object.FindFirstObjectByType<CastlePlaySession>(); float deadline = Time.realtimeSinceStartup + 35;
            while ((session.Player == null || session.SurvivorBots == null) && Time.realtimeSinceStartup < deadline) yield return null;
            if (!Expect(session.Player != null, "Solo survivor initialized")) yield break;
            session.SurvivorBots.AutoFill = false; session.SurvivorBots.Clear(); session.HandBots?.Clear();
            if (session.Social != null) session.Social.VoiceEnabled = false;
            step = "loading the campus map";
            if (!(Services.World is VanderbiltCampusMap)) { ChooseMatch(); MatchSetup.ApplyMap(); yield return null; }
            var map = Services.World as VanderbiltCampusMap;
            if (!Expect(map, "Campus map loaded in Game")) yield break;
            session.Player.Place(map.SafeSpawnPoint(), Quaternion.identity);

            step = "towers";
            Expect(map.Blocks.Count > 0 && map.Blocks.Count == map.ExpectedBlockCount(), $"Towers built: {map.Blocks.Count} blocks (expected {map.ExpectedBlockCount()}) in {map.towers.Length} structures");
            var start = map.Blocks.Where(b => b).ToDictionary(b => b, b => b.position);
            yield return new WaitForSeconds(5);
            int fell = start.Count(kv => kv.Key && kv.Key.position.y < kv.Value.y - 1);
            Expect(start.Count == 0 || fell <= start.Count / 10, $"Towers stand on their own for 5 s ({fell} of {start.Count} blocks dropped more than 1 m)");

            step = "lighting and streaming";
            var suns = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled && l.type == LightType.Directional && l.lightmapBakeType != LightmapBakeType.Baked);
            Expect(suns <= 1, $"At most one realtime sun ({suns} enabled realtime directional lights)");
            var streamer = map.GetComponent<CampusTileStreamer>();
            yield return new WaitForSeconds(1.5f);
            Expect(streamer && streamer.NearTiles > 0 && streamer.FarTiles > 0, $"Tile streaming active: {(streamer ? streamer.NearTiles : 0)} tiles near, {(streamer ? streamer.FarTiles : 0)} far (HLOD only, no colliders)");
            var feet = session.Player.transform.position;
            Expect(Physics.Raycast(feet + Vector3.up, Vector3.down, 5, 1 << Layers.Surface, QueryTriggerInteraction.Ignore), "Ground collider under the player is on");
            Expect((map as ISyncedWorld).SaveLayout() == map.SaveLayout() && map.SaveLayout().StartsWith("vanderbilt-campus:"), "Streaming leaves the layout token unchanged: " + map.SaveLayout());

            if (Suite == "bots" || Suite == "all") yield return Bots(session, map);
            if (Suite == "modes" || Suite == "all")
            {
                yield return Mode(session, map, GameModeKind.KingOfHill);
                yield return Mode(session, map, GameModeKind.CaptureFlag);
            }
            step = "finishing";
            File.AppendAllText(Report, "COMPLETE desktop Game scene checks. Quest headset and two-player multiplayer remain unverified.\n");
        }

        // Spawns the bots, then measures ordinary walking for 60 s (placement and respawn jumps over 3 m ignored).
        static IEnumerator Bots(CastlePlaySession session, VanderbiltCampusMap map)
        {
            step = "bot smoke test";
            int before = exceptions;
            for (int i = 0; i < BotCount; i++) session.SurvivorBots.SpawnOne();
            yield return new WaitForSeconds(2);
            var bots = session.Bots() ? session.Bots().Bots.Where(b => b && b.gameObject.activeInHierarchy).ToList() : new List<CastleBots.CastleBotBrain>();
            Expect(bots.Count >= BotCount - 2, $"{bots.Count} of {BotCount} survivor bots spawned");
            var last = bots.ToDictionary(b => b, b => b.transform.position); var walked = bots.ToDictionary(b => b, b => 0f);
            float end = Time.time + 60;
            while (Time.time < end)
            {
                yield return new WaitForSeconds(1);
                foreach (var b in bots)
                {
                    if (!b) continue;
                    float d = Vector3.Distance(last[b], b.transform.position);
                    if (d < 3) walked[b] += d;
                    last[b] = b.transform.position;
                }
            }
            int moved = walked.Count(kv => kv.Value > 1);
            Expect(moved >= Mathf.Max(1, bots.Count * 3 / 4), $"Bots moved: {moved} of {bots.Count} walked more than 1 m in 60 s");
            Expect(exceptions == before, "No exceptions during the 60 s bot run");
            int inArea = bots.Count(b => b && map.InPlayArea(b.transform.position, 20));
            File.AppendAllText(Report, $"NOTE {inArea} of {bots.Count} bots inside the play area (+20 m) at the end.\n");
            session.SurvivorBots.Clear();
        }

        // Starts one mode for 20 s and checks it uses the campus match layout.
        static IEnumerator Mode(CastlePlaySession session, VanderbiltCampusMap map, GameModeKind kind)
        {
            step = "mode " + kind;
            int before = exceptions;
            var layout = Resources.LoadAll<MapMatchLayout>("MapMatchLayouts").FirstOrDefault(l => l.Matches(map));
            Expect(layout, $"{kind}: campus match layout found for token {map.SaveLayout()}");
            var rules = ModeSimulation.Defaults(kind); rules.countdownSeconds = 0; rules.automaticDisasters = false; rules.island = IslandRule.Walkable;
            session.Modes.Configure(rules); session.Modes.StartRound();
            yield return new WaitForSeconds(2);
            var frame = session.Modes.Frame;
            Expect(frame.phase == RoundPhase.Playing, $"{kind}: round is playing (phase {frame.phase})");
            if (layout && kind == GameModeKind.KingOfHill)
                Expect(Vector3.Distance(frame.hill, map.transform.TransformPoint(layout.hill)) < 3, $"{kind}: hill at the authored point (frame {frame.hill:F1}, layout {map.transform.TransformPoint(layout.hill):F1})");
            if (layout && kind == GameModeKind.CaptureFlag)
            {
                bool homes = frame.flags.Length == 2 && Enumerable.Range(0, 2).All(t => Vector3.Distance(frame.flags[t].home, map.transform.TransformPoint(layout.teams[t].flagHome)) < .5f);
                Expect(homes, $"{kind}: two flags at the authored team-area homes ({frame.flags.Length} flags)");
            }
            int inside = 0, total = 0;
            for (int i = 0; i < 16; i++)
            {
                int team = rules.spawn == SpawnRule.TeamPoles ? i % rules.teamCount : -1;
                var p = session.Modes.SpawnPoint(team, i, "survivor"); total++;
                if (map.InPlayArea(p, 2)) inside++;
            }
            Expect(inside == total, $"{kind}: {inside} of {total} spawn queries inside the play area");
            yield return new WaitForSeconds(18);
            Expect(session.Modes.Frame.phase is RoundPhase.Playing or RoundPhase.Victory or RoundPhase.Results, $"{kind}: still running after 20 s (phase {session.Modes.Frame.phase})");
            Expect(exceptions == before, $"{kind}: no exceptions in 20 s");
        }
    }
}
