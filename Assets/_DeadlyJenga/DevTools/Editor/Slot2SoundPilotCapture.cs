using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyArt;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.World.Hazards;
using DeadlyJenga.Modes;
using DeadlyJenga.CandyAudio;
using DeadlyJenga.CandyAudio.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class Slot2SoundPilotCapture
    {
        const string Key = "DeadlyJenga.Slot2SoundPilotCapture";
        static string RawRoot => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "SoundStudio", "captures", "raw");
        static string Report => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "SoundStudio", "pilot-capture-log.txt");

        static Slot2SoundPilotCapture()
        {
            if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying)
            {
                Application.runInBackground = true;
                EditorApplication.update -= StartRoutineWhenReady;
                EditorApplication.update += StartRoutineWhenReady;
            }
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    Application.runInBackground = true;
                    EditorApplication.update -= StartRoutineWhenReady;
                    EditorApplication.update += StartRoutineWhenReady;
                }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    int phase = SessionState.GetInt(Key + ".Phase", 0);
                    if (phase == 1)
                    {
                        // Game.unity phase finished, proceed to Home.unity phase
                        SessionState.SetInt(Key + ".Phase", 2);
                        EditorApplication.delayCall += () =>
                        {
                            EditorSceneManager.OpenScene(ProjectPath.Find("Home.unity"));
                            EditorApplication.EnterPlaymode();
                        };
                    }
                    else if (phase == 2)
                    {
                        // All phases complete
                        SessionState.SetBool(Key, false);
                        SessionState.SetInt(Key + ".Phase", 0);
                        File.AppendAllText(Report, "ALL PHASES COMPLETE at " + DateTime.UtcNow.ToString("O") + "\n");
                        Debug.Log("Slot 2 Sound Pilot Captures Complete! See: " + Report);
                    }
                    else if (phase == 10)
                    {
                        SessionState.SetBool(Key, false);
                        SessionState.SetInt(Key + ".Phase", 0);
                        File.AppendAllText(Report, "BATCH 1 DISASTERS COMPLETE at " + DateTime.UtcNow.ToString("O") + "\n");
                        Debug.Log("Slot 2 Batch 1 Disasters Captures Complete! See: " + Report);
                    }
                }
            };
        }

        [MenuItem("Tools/Candy Audio/Batch Captures/Run Batch 1 Disasters (42 Cues)")]
        public static void RunBatch1Disasters()
        {
            Directory.CreateDirectory(RawRoot);
            File.AppendAllText(Report, "Starting Batch 1 Disasters Captures (42 cues) · " + DateTime.UtcNow.ToString("O") + "\n");
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".Phase", 10);
            MatchSetup.FromLobby = false;
            MatchSetup.StartLocalSession = false;
            MatchSetup.MapId = "castle";
            MatchSetup.ModeId = "demo";
            MatchSetup.BotTarget = 0;
            MatchSetup.HandCount = 0;
            if (EditorApplication.isPlaying)
            {
                Application.runInBackground = true;
                EditorApplication.update -= StartRoutineWhenReady;
                EditorApplication.update += StartRoutineWhenReady;
            }
            else
            {
                EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
                EditorApplication.EnterPlaymode();
            }
        }

        [MenuItem("Tools/Candy Audio/Pilot Captures/Run 10 Pilot Captures")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(RawRoot);
            File.WriteAllText(Report, "Starting Pilot Captures (10 cues) · " + DateTime.UtcNow.ToString("O") + "\n");
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".Phase", 1);
            MatchSetup.FromLobby = false;
            MatchSetup.StartLocalSession = false;
            MatchSetup.MapId = "castle";
            MatchSetup.ModeId = "demo";
            MatchSetup.BotTarget = 0;
            MatchSetup.HandCount = 0;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Tools/Candy Audio/Pilot Captures/Run Cue 25 Marina")]
        public static void RunCue25()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(RawRoot);
            File.AppendAllText(Report, "Starting Cue 25 Marina · " + DateTime.UtcNow.ToString("O") + "\n");
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".Phase", 2);
            EditorSceneManager.OpenScene(ProjectPath.Find("Home.unity"));
            EditorApplication.EnterPlaymode();
        }

        static void StartRoutineWhenReady()
        {
            int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 1)
            {
                var session = Object.FindFirstObjectByType<CastlePlaySession>();
                if (!session || session.Player == null || Services.World == null || !session.ActiveCamera) return;
                EditorApplication.update -= StartRoutineWhenReady;
                session.StartCoroutine(Guard(RunGameSceneCaptures(session)));
            }
            else if (phase == 2)
            {
                var home = Object.FindFirstObjectByType<HomeSession>();
                if (!home) return;
                EditorApplication.update -= StartRoutineWhenReady;
                home.StartCoroutine(Guard(RunHomeSceneCaptures(home)));
            }
            else if (phase == 10)
            {
                var session = Object.FindFirstObjectByType<CastlePlaySession>();
                if (!session || session.Player == null || Services.World == null || !session.ActiveCamera) return;
                EditorApplication.update -= StartRoutineWhenReady;
                session.StartCoroutine(Guard(RunBatch1DisastersCaptures(session)));
            }
        }

        static IEnumerator Guard(IEnumerator sequence)
        {
            while (true)
            {
                bool next;
                try { next = sequence.MoveNext(); }
                catch (Exception e)
                {
                    File.AppendAllText(Report, "ERROR: " + e + "\n");
                    Debug.LogException(e);
                    break;
                }
                if (!next) break;
                yield return sequence.Current;
            }
            EditorApplication.ExitPlaymode();
        }

        static void Log(string msg)
        {
            File.AppendAllText(Report, msg + "\n");
            Debug.Log("[PilotCapture] " + msg);
        }

        static void CaptureFrame(Camera camera, string filePath, int width = 960, int height = 540)
        {
            var rt = RenderTexture.GetTemporary(width, height, 24);
            var prev = RenderTexture.active;
            var prevTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(filePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            finally
            {
                camera.targetTexture = prevTarget;
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        static IEnumerator RunGameSceneCaptures(CastlePlaySession session)
        {
            Log("Game.unity session loaded. Setting up match rules...");
            AudioListener.volume = 0;
            session.SurvivorBots.AutoFill = false;
            session.SurvivorBots.Clear();
            session.HandBots?.Clear();
            if (session.Social != null) session.Social.VoiceEnabled = false;

            var rules = ModeSimulation.Defaults(GameModeKind.Demo);
            rules.countdownSeconds = 0;
            rules.healthEnabled = true;
            rules.deathEnabled = false;
            rules.fallDamage = false;
            rules.island = IslandRule.Walkable;
            session.Modes.Configure(rules);
            session.Modes.StartRound();
            session.SetRole(true);
            session.SetMenu(false);
            session.Player.InputSuspended = true;
            yield return new WaitForSecondsRealtime(1f);

            var cam = new GameObject("Pilot Capture Camera").AddComponent<Camera>();
            cam.CopyFrom(session.ActiveCamera);
            cam.enabled = false;
            cam.rect = new Rect(0, 0, 1, 1);
            cam.fieldOfView = 55;

            var world = Services.World;
            var player = session.Survivor();
            var inventory = session.Modes;
            var controls = player.GetComponent<CandyEquipment>();
            var avatar = player.GetComponent<CandyPlayerAvatar>();

            // ==========================================
            // CUE 21: Surf (Map ambience / Ocean)
            // ==========================================
            Log("Starting Cue 21: Surf...");
            string dir21 = Path.Combine(RawRoot, "21");
            Directory.CreateDirectory(dir21);
            float waterY = world.WaterHeight(world.Center);
            Vector3 beachPos = world.Center + Vector3.forward * (world.Radius + 14f) + Vector3.up * (waterY + 1.2f);
            cam.transform.position = beachPos;
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(0.3f, -0.08f, 1f));
            for (int f = 0; f < 36; f++)
            {
                CaptureFrame(cam, Path.Combine(dir21, $"frame_{f:D4}.png"));
                yield return new WaitForSecondsRealtime(1f / 12f);
            }
            Log("Completed Cue 21 (36 frames).");

            // ==========================================
            // CUE 88: Kraken Rise (Disasters / Kraken)
            // ==========================================
            Log("Starting Cue 88: Kraken Rise...");
            string dir88 = Path.Combine(RawRoot, "88");
            Directory.CreateDirectory(dir88);
            DemoDisasters.StopAll(session);
            yield return new WaitForSecondsRealtime(0.5f);

            var krakenPrefab = Array.Find(DemoDisasters.Prefabs, p => p && p.name == "Kraken");
            if (!krakenPrefab) throw new InvalidOperationException("Kraken prefab not found in DisasterCatalog");
            var kParams = DemoDisasters.Parameters(krakenPrefab);
            for (int i = 0; i < kParams.Count; i++)
            {
                if (kParams[i].id == "warning") DemoDisasters.SetValue(session, krakenPrefab, i, 0);
                if (kParams[i].id == "duration") DemoDisasters.SetValue(session, krakenPrefab, i, 60);
            }
            DemoDisasters.Request(session, krakenPrefab);
            yield return null;

            KrakenHazard kraken = null;
            float waitDeadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < waitDeadline && !(kraken = Object.FindFirstObjectByType<KrakenHazard>()))
                yield return null;
            if (!kraken) throw new InvalidOperationException("Kraken hazard failed to start.");

            // Find head position from KrakenHazard fields
            var headField = typeof(KrakenHazard).GetField("head", BindingFlags.Instance | BindingFlags.NonPublic);
            var headDirField = typeof(KrakenHazard).GetField("headDir", BindingFlags.Instance | BindingFlags.NonPublic);
            Vector3 kHead = headField != null ? (Vector3)headField.GetValue(kraken) : world.Center + Vector3.forward * 40f + Vector3.up * waterY;
            Vector3 kDir = headDirField != null ? (Vector3)headDirField.GetValue(kraken) : Vector3.forward;

            cam.transform.position = kHead - kDir * 28f + Vector3.up * 8f;
            cam.transform.LookAt(kHead + Vector3.up * 3f);

            // Record emergence frames as HeadRise goes from 0 to > 0.05
            for (int f = 0; f < 30; f++)
            {
                CaptureFrame(cam, Path.Combine(dir88, $"frame_{f:D4}.png"));
                yield return new WaitForSecondsRealtime(1f / 12f);
            }
            Log("Completed Cue 88 (30 frames).");

            // ==========================================
            // CUE 95: Kraken Slam (Disasters / Kraken)
            // ==========================================
            Log("Starting Cue 95: Kraken Slam...");
            string dir95 = Path.Combine(RawRoot, "95");
            Directory.CreateDirectory(dir95);

            // Look towards the tower blocks where tentacles strike
            var targetBlock = world.Blocks.FirstOrDefault(b => b != null && b.position.y > waterY + 4f && b.position.y < waterY + 12f);
            Vector3 slamTarget = targetBlock ? targetBlock.position : world.Center + Vector3.up * (waterY + 6f);
            cam.transform.position = slamTarget + new Vector3(10f, 6f, 10f);
            cam.transform.LookAt(slamTarget);

            // Record 30 frames capturing tentacle sweep and block impact
            for (int f = 0; f < 30; f++)
            {
                CaptureFrame(cam, Path.Combine(dir95, $"frame_{f:D4}.png"));
                yield return new WaitForSecondsRealtime(1f / 12f);
            }
            Log("Completed Cue 95 (30 frames).");
            DemoDisasters.StopAll(session);
            yield return new WaitForSecondsRealtime(0.5f);

            // Platform for player actions
            var topBlock = world.Blocks.Where(b => b && b.GetComponent<Collider>())
                .Select(b => b.GetComponent<Collider>().bounds)
                .OrderByDescending(b => b.max.y).First();
            Vector3 standPos = new Vector3(topBlock.center.x, topBlock.max.y + 0.05f, topBlock.center.z);
            player.Place(standPos, Quaternion.identity);
            yield return new WaitForSecondsRealtime(0.3f);

            // ==========================================
            // CUE 785092: Stock Pistol Shot · primary
            // ==========================================
            Log("Starting Cue 785092: Stock Pistol Shot...");
            string dir785 = Path.Combine(RawRoot, "785092");
            Directory.CreateDirectory(dir785);

            int pistolPickup = inventory.RegisterPickup(new SimplePickupSource { Kind = EquipmentKind.StockPistol, Position = standPos });
            inventory.RequestPickup(pistolPickup, true);
            controls.SetEquipped(true);
            yield return null;
            avatar.RefreshNow();
            yield return null;

            cam.transform.position = standPos + Vector3.up * 1.55f - player.transform.forward * 0.4f + player.transform.right * 0.35f;
            cam.transform.rotation = player.transform.rotation * Quaternion.Euler(2f, -8f, 0);

            for (int f = 0; f < 24; f++)
            {
                if (f == 6)
                {
                    // Frame 6 (0.5s): Firing discharge
                    inventory.RequestEquipmentAttack(new Pose(cam.transform.position + cam.transform.forward * 0.5f, cam.transform.rotation));
                }
                CaptureFrame(cam, Path.Combine(dir785, $"frame_{f:D4}.png"));
                yield return new WaitForSecondsRealtime(1f / 12f);
            }
            Log("Completed Cue 785092 (24 frames).");

            // ==========================================
            // CUE 536436: Stock Pistol Reload
            // ==========================================
            Log("Starting Cue 536436: Stock Pistol Reload...");
            string dir536 = Path.Combine(RawRoot, "536436");
            Directory.CreateDirectory(dir536);

            var eqState = inventory.EquipmentFor(inventory.LocalActorId);
            if (eqState != null) eqState.rounds = 0; // Empty magazine

            for (int f = 0; f < 30; f++)
            {
                if (f == 10)
                {
                    // Frame 10 (~0.8s): Trigger reload action
                    inventory.RequestEquipment(true, false, false);
                }
                CaptureFrame(cam, Path.Combine(dir536, $"frame_{f:D4}.png"));
                yield return new WaitForSecondsRealtime(1f / 12f);
            }
            Log("Completed Cue 536436 (30 frames).");

            // ==========================================
            // CUE 175138: Dagger Impact
            // ==========================================
            Log("Starting Cue 175138: Dagger Impact...");
            string dir175 = Path.Combine(RawRoot, "175138");
            Directory.CreateDirectory(dir175);

            int daggerPickup = inventory.RegisterPickup(new SimplePickupSource { Kind = EquipmentKind.Dagger, Position = standPos });
            inventory.RequestPickup(daggerPickup, true);
            controls.SetEquipped(true);
            yield return null;
            avatar.RefreshNow();
            yield return null;

            cam.transform.position = standPos + Vector3.up * 1.35f + player.transform.right * 0.6f + player.transform.forward * 0.2f;
            cam.transform.LookAt(standPos + Vector3.up * 1.25f + player.transform.forward * 0.8f);

            for (int f = 0; f < 24; f++)
            {
                if (f == 8)
                {
                    // Frame 8 (~0.67s): Dagger strikes forward
                    inventory.RequestEquipmentAttack(new Pose(cam.transform.position + cam.transform.forward * 0.4f, cam.transform.rotation));
                }
                CaptureFrame(cam, Path.Combine(dir175, $"frame_{f:D4}.png"));
                yield return new WaitForSecondsRealtime(1f / 12f);
            }
            Log("Completed Cue 175138 (24 frames).");

            // ==========================================
            // CUE 113: Personal Shot (Combat / Shared feedback)
            // ==========================================
            Log("Starting Cue 113: Personal Shot...");
            string dir113 = Path.Combine(RawRoot, "113");
            Directory.CreateDirectory(dir113);

            // Victim first person POV
            cam.transform.position = standPos + Vector3.up * 1.55f;
            cam.transform.rotation = player.transform.rotation;

            for (int f = 0; f < 24; f++)
            {
                if (f == 8)
                {
                    // Frame 8 (~0.67s): Projectile damage hits victim POV
                    player.GetComponent<IDamageable>()?.Damage(15f, null);
                    GameEvents.RaiseDamaged(player.gameObject, 15f);
                }
                CaptureFrame(cam, Path.Combine(dir113, $"frame_{f:D4}.png"));
                yield return new WaitForSecondsRealtime(1f / 12f);
            }
            Log("Completed Cue 113 (24 frames).");

            // ==========================================
            // CUE 13: Land Hard (Player / Movement)
            // ==========================================
            Log("Starting Cue 13: Land Hard...");
            string dir13 = Path.Combine(RawRoot, "13");
            Directory.CreateDirectory(dir13);

            // Place player 4m up, position camera at landing surface watching feet
            Vector3 landSpot = standPos;
            Vector3 dropSpot = standPos + Vector3.up * 4.5f;
            cam.transform.position = landSpot + player.transform.forward * 1.6f + player.transform.right * 0.8f + Vector3.up * 0.35f;
            cam.transform.LookAt(landSpot + Vector3.up * 0.3f);

            player.Place(dropSpot, Quaternion.identity);
            player.InputSuspended = false;

            for (int f = 0; f < 24; f++)
            {
                CaptureFrame(cam, Path.Combine(dir13, $"frame_{f:D4}.png"));
                yield return new WaitForSecondsRealtime(1f / 12f);
            }
            player.InputSuspended = true;
            Log("Completed Cue 13 (24 frames).");

            // ==========================================
            // CUE 16: Climb Plant (Player / Climbing and stamina)
            // ==========================================
            Log("Starting Cue 16: Climb Plant...");
            string dir16 = Path.Combine(RawRoot, "16");
            Directory.CreateDirectory(dir16);

            // Position player next to a block wall/ledge
            cam.transform.position = standPos + player.transform.right * 1.2f - player.transform.forward * 0.4f + Vector3.up * 1.2f;
            cam.transform.LookAt(standPos + Vector3.up * 1.4f + player.transform.forward * 0.3f);

            for (int f = 0; f < 24; f++)
            {
                CaptureFrame(cam, Path.Combine(dir16, $"frame_{f:D4}.png"));
                yield return new WaitForSecondsRealtime(1f / 12f);
            }
            Log("Completed Cue 16 (24 frames).");

            Object.DestroyImmediate(cam.gameObject);
            Log("Game.unity captures complete.");
        }

        static IEnumerator RunHomeSceneCaptures(HomeSession home)
        {
            Log("Home.unity loaded. Setting up Marina lobby terminal...");
            AudioListener.volume = 0;
            float deadline = Time.realtimeSinceStartup + 30f;
            home.Demo();

            while ((!IslandLobby.Current || !Object.FindFirstObjectByType<IslandLobbyView>()) && Time.realtimeSinceStartup < deadline)
                yield return null;

            var lobby = IslandLobby.Current;
            var view = Object.FindFirstObjectByType<IslandLobbyView>();
            if (!lobby || !view) throw new InvalidOperationException("Island lobby view unavailable.");

            lobby.Player.Place(new Vector3(0, 0.1f, -8f), Quaternion.identity);
            lobby.Player.InputSuspended = true;
            yield return new WaitForSecondsRealtime(0.5f);

            // ==========================================
            // CUE 25: Ui Click (UI / Menus and results)
            // ==========================================
            Log("Starting Cue 25: Ui Click on Marina terminal...");
            string dir25 = Path.Combine(RawRoot, "25");
            Directory.CreateDirectory(dir25);

            var screen = view.transform.Find("MAP + MODE");
            var marina = view.transform.Find("Marina · live cinema terminal");
            var point = screen ? screen.position : (marina ? marina.position : view.transform.position);

            var cam = new GameObject("Terminal Camera").AddComponent<Camera>();
            cam.CopyFrom(Services.Session.ActiveCamera);
            cam.enabled = false;
            cam.fieldOfView = 50;
            cam.transform.position = point - Vector3.forward * 2.2f + Vector3.up * 0.1f;
            cam.transform.LookAt(point - Vector3.up * 0.1f);

            for (int f = 0; f < 24; f++)
            {
                if (f == 8)
                {
                    // Trigger terminal interaction / button click
                    CandyAudioControls.PlayUiClick(point);
                    typeof(IslandLobbyView).GetMethod("OpenMarinaDetail", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(view, new object[] { "modes" });
                }
                CaptureFrame(cam, Path.Combine(dir25, $"frame_{f:D4}.png"));
                yield return new WaitForSecondsRealtime(1f / 12f);
            }
            Object.DestroyImmediate(cam.gameObject);
            Log("Completed Cue 25 (24 frames).");
        }

        static IEnumerator CaptureCue(Camera camera, int cueId, string name, int frameCount = 24, Action<int> onFrame = null)
        {
            Log($"Starting Cue {cueId}: {name}...");
            string dir = Path.Combine(RawRoot, cueId.ToString());
            Directory.CreateDirectory(dir);
            for (int f = 0; f < frameCount; f++)
            {
                onFrame?.Invoke(f);
                CaptureFrame(camera, Path.Combine(dir, $"frame_{f:D4}.png"));
                yield return new WaitForSecondsRealtime(1f / 12f);
            }
            Log($"Completed Cue {cueId}: {name} ({frameCount} frames).");
        }

        static GameObject GetPrefab(string name)
        {
            var p = Array.Find(DemoDisasters.Prefabs, x => x && x.name == name);
            if (!p) throw new InvalidOperationException("Disaster prefab not found: " + name);
            return p;
        }

        static void StartDisaster(CastlePlaySession session, string name, float duration = 60f)
        {
            DemoDisasters.StopAll(session);
            var prefab = GetPrefab(name);
            var pars = DemoDisasters.Parameters(prefab);
            for (int i = 0; i < pars.Count; i++)
            {
                if (pars[i].id == "warning") DemoDisasters.SetValue(session, prefab, i, 0);
                if (pars[i].id == "duration") DemoDisasters.SetValue(session, prefab, i, duration);
            }
            DemoDisasters.Request(session, prefab);
        }

        static IEnumerator RunBatch1DisastersCaptures(CastlePlaySession session)
        {
            Log("Game.unity session loaded for Batch 1 Disasters. Setting up match rules...");
            AudioListener.volume = 0;
            session.SurvivorBots.AutoFill = false;
            session.SurvivorBots.Clear();
            session.HandBots?.Clear();
            if (session.Social != null) session.Social.VoiceEnabled = false;

            var rules = ModeSimulation.Defaults(GameModeKind.Demo);
            rules.countdownSeconds = 0;
            rules.healthEnabled = true;
            rules.deathEnabled = false;
            rules.fallDamage = false;
            rules.island = IslandRule.Walkable;
            session.Modes.Configure(rules);
            session.Modes.StartRound();
            session.SetRole(true);
            session.SetMenu(false);
            session.Player.InputSuspended = true;
            yield return new WaitForSecondsRealtime(1f);

            var cam = new GameObject("Disaster Capture Camera").AddComponent<Camera>();
            cam.CopyFrom(session.ActiveCamera);
            cam.enabled = false;
            cam.rect = new Rect(0, 0, 1, 1);
            cam.fieldOfView = 55;

            var world = Services.World;
            var player = session.Survivor();
            float waterY = world.WaterHeight(world.Center);

            var topBlock = world.Blocks.Where(b => b && b.GetComponent<Collider>())
                .Select(b => b.GetComponent<Collider>().bounds)
                .OrderByDescending(b => b.max.y).First();
            Vector3 standPos = new Vector3(topBlock.center.x, topBlock.max.y + 0.05f, topBlock.center.z);
            player.Place(standPos, Quaternion.identity);

            // ========================================================
            // SECTION A: KRAKEN REMAINING (8 CUES: 89, 90, 91, 92, 93, 94, 96, 97)
            // ========================================================
            Log("--- Commencing Kraken captures ---");
            StartDisaster(session, "Kraken", 90f);
            yield return new WaitForSecondsRealtime(1.5f);

            KrakenHazard kraken = null;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline && !(kraken = Object.FindFirstObjectByType<KrakenHazard>()))
                yield return null;

            var headField = typeof(KrakenHazard).GetField("head", BindingFlags.Instance | BindingFlags.NonPublic);
            var headDirField = typeof(KrakenHazard).GetField("headDir", BindingFlags.Instance | BindingFlags.NonPublic);
            Vector3 kHead = headField != null ? (Vector3)headField.GetValue(kraken) : world.Center + Vector3.forward * 40f + Vector3.up * waterY;
            Vector3 kDir = headDirField != null ? (Vector3)headDirField.GetValue(kraken) : Vector3.forward;

            // 89: Kraken Idle
            cam.transform.position = kHead - kDir * 26f + Vector3.up * 7f;
            cam.transform.LookAt(kHead + Vector3.up * 4f);
            yield return CaptureCue(cam, 89, "Kraken Idle");

            // 90: Kraken Surface
            cam.transform.position = kHead - kDir * 18f + Vector3.up * 3f;
            cam.transform.LookAt(kHead + kDir * 5f + Vector3.up * 1f);
            yield return CaptureCue(cam, 90, "Kraken Surface");

            // 91: Kraken Reach
            cam.transform.position = world.Center + Vector3.up * (waterY + 10f) + kDir * 15f + Vector3.right * 15f;
            cam.transform.LookAt(world.Center + Vector3.up * (waterY + 8f));
            yield return CaptureCue(cam, 91, "Kraken Reach");

            // 92: Kraken Grip
            var targetBlock = world.Blocks.FirstOrDefault(b => b != null && b.position.y > waterY + 4f && b.position.y < waterY + 12f);
            Vector3 gripPos = targetBlock ? targetBlock.position : world.Center + Vector3.up * (waterY + 6f);
            cam.transform.position = gripPos + new Vector3(8f, 4f, 8f);
            cam.transform.LookAt(gripPos);
            yield return CaptureCue(cam, 92, "Kraken Grip");

            // 93: Kraken Drag
            cam.transform.position = gripPos + new Vector3(10f, 5f, 5f);
            cam.transform.LookAt(gripPos);
            yield return CaptureCue(cam, 93, "Kraken Drag");

            // 94: Kraken Windup
            cam.transform.position = gripPos + new Vector3(12f, 8f, 10f);
            cam.transform.LookAt(gripPos + Vector3.up * 4f);
            yield return CaptureCue(cam, 94, "Kraken Windup");

            // 96: Kraken Submerge
            cam.transform.position = kHead - kDir * 15f + Vector3.up * 2f;
            cam.transform.LookAt(kHead + Vector3.up * 0.5f);
            yield return CaptureCue(cam, 96, "Kraken Submerge");

            // 97: Kraken Retreat
            DemoDisasters.StopAll(session);
            cam.transform.position = kHead - kDir * 28f + Vector3.up * 8f;
            cam.transform.LookAt(kHead + Vector3.up * 2f);
            yield return CaptureCue(cam, 97, "Kraken Retreat");
            yield return new WaitForSecondsRealtime(1f);

            // ========================================================
            // SECTION B: TORNADO (4 CUES: 45, 101, 132, 133)
            // ========================================================
            Log("--- Commencing Tornado captures ---");
            StartDisaster(session, "Tornado", 60f);
            yield return new WaitForSecondsRealtime(1.5f);

            var tornado = Object.FindFirstObjectByType<TornadoHazard>();
            Vector3 tPos = tornado ? tornado.transform.position : world.Center + Vector3.forward * 12f;

            // 45: CycloneWind
            cam.transform.position = world.Center + Vector3.back * 25f + Vector3.up * 10f;
            cam.transform.LookAt(world.Center + Vector3.up * 12f);
            yield return CaptureCue(cam, 45, "CycloneWind");

            // 101: CycloneDebris
            cam.transform.position = tPos + Vector3.right * 10f + Vector3.up * 3f;
            cam.transform.LookAt(tPos + Vector3.up * 4f);
            yield return CaptureCue(cam, 101, "CycloneDebris");

            // 132: TornadoPass
            cam.transform.position = standPos + Vector3.right * 6f + Vector3.up * 1.5f;
            cam.transform.LookAt(standPos + Vector3.forward * 8f);
            yield return CaptureCue(cam, 132, "TornadoPass");

            // 133: TornadoPressure
            cam.transform.position = tPos + Vector3.forward * 2f + Vector3.up * 4f;
            cam.transform.LookAt(tPos + Vector3.up * 5f);
            yield return CaptureCue(cam, 133, "TornadoPressure");

            DemoDisasters.StopAll(session);
            yield return new WaitForSecondsRealtime(1f);

            // ========================================================
            // SECTION C: TSUNAMI (6 CUES: 40, 41, 42, 98, 99, 100)
            // ========================================================
            Log("--- Commencing Tsunami captures ---");
            StartDisaster(session, "Tsunami", 60f);
            yield return new WaitForSecondsRealtime(1.5f);

            // 100: WaveRoar
            cam.transform.position = standPos + Vector3.up * 3f;
            cam.transform.LookAt(world.Center + Vector3.forward * 50f + Vector3.up * 10f);
            yield return CaptureCue(cam, 100, "WaveRoar");

            // 40: WaveImpact
            Vector3 towerBase = world.Center + Vector3.up * (waterY + 2f);
            cam.transform.position = towerBase + Vector3.right * 15f + Vector3.up * 5f;
            cam.transform.LookAt(towerBase);
            yield return CaptureCue(cam, 40, "WaveImpact");

            // 41: WaveSpray
            cam.transform.position = towerBase + Vector3.forward * 12f + Vector3.up * 6f;
            cam.transform.LookAt(towerBase + Vector3.up * 2f);
            yield return CaptureCue(cam, 41, "WaveSpray");

            // 98: WaveSubmerged
            cam.transform.position = world.Center + Vector3.up * (waterY - 1f) + Vector3.forward * 5f;
            cam.transform.LookAt(world.Center + Vector3.up * (waterY - 0.5f));
            yield return CaptureCue(cam, 98, "WaveSubmerged");

            // 99: WaveSurfaceBreath
            cam.transform.position = world.Center + Vector3.up * (waterY + 0.3f) + Vector3.forward * 5f;
            cam.transform.LookAt(world.Center + Vector3.up * (waterY + 1.5f));
            yield return CaptureCue(cam, 99, "WaveSurfaceBreath");

            // 42: PuddleDrain
            cam.transform.position = towerBase + Vector3.forward * 8f + Vector3.up * 3f;
            cam.transform.LookAt(towerBase - Vector3.up * 0.5f);
            yield return CaptureCue(cam, 42, "PuddleDrain");

            DemoDisasters.StopAll(session);
            yield return new WaitForSecondsRealtime(1f);

            // ========================================================
            // SECTION D: METEORS & PLANET (5 CUES: 128, 49, 129, 130, 131)
            // ========================================================
            Log("--- Commencing Meteors captures ---");
            StartDisaster(session, "MeteorShower", 60f);
            yield return new WaitForSecondsRealtime(1.5f);

            // 128: MeteorFlyby
            cam.transform.position = standPos + Vector3.up * 2f;
            cam.transform.LookAt(standPos + Vector3.up * 25f + Vector3.forward * 10f);
            yield return CaptureCue(cam, 128, "MeteorFlyby");

            // 49: MeteorImpact
            cam.transform.position = world.Center + Vector3.back * 16f + Vector3.up * (waterY + 6f);
            cam.transform.LookAt(world.Center + Vector3.up * (waterY + 2f));
            yield return CaptureCue(cam, 49, "MeteorImpact");

            // 129: MeteorWaterImpact
            Vector3 oceanSpot = world.Center + Vector3.forward * (world.Radius + 15f) + Vector3.up * waterY;
            cam.transform.position = oceanSpot + Vector3.back * 20f + Vector3.up * 8f;
            cam.transform.LookAt(oceanSpot);
            yield return CaptureCue(cam, 129, "MeteorWaterImpact");

            DemoDisasters.StopAll(session);
            yield return new WaitForSecondsRealtime(0.5f);

            Log("--- Commencing Planet captures ---");
            StartDisaster(session, "PlanetKillerMeteor", 60f);
            yield return new WaitForSecondsRealtime(1.5f);

            // 130: PlanetApproach
            cam.transform.position = standPos + Vector3.up * 2f;
            cam.transform.LookAt(standPos + Vector3.up * 40f + Vector3.forward * 20f);
            yield return CaptureCue(cam, 130, "PlanetApproach");

            // 131: PlanetImpact
            cam.transform.position = world.Center + Vector3.back * 30f + Vector3.up * 15f;
            cam.transform.LookAt(world.Center + Vector3.up * 5f);
            yield return CaptureCue(cam, 131, "PlanetImpact");

            DemoDisasters.StopAll(session);
            yield return new WaitForSecondsRealtime(1f);

            // ========================================================
            // SECTION E: ACID RAIN (3 CUES: 43, 125, 44)
            // ========================================================
            Log("--- Commencing Acid Rain captures ---");
            StartDisaster(session, "AcidRain", 60f);
            yield return new WaitForSecondsRealtime(1.5f);

            // 43: AcidSizzle
            cam.transform.position = standPos + Vector3.back * 4f + Vector3.up * 3f;
            cam.transform.LookAt(standPos);
            yield return CaptureCue(cam, 43, "AcidSizzle");

            // 125: AcidRoofRain
            cam.transform.position = world.Center + Vector3.up * (waterY + 3f);
            cam.transform.LookAt(world.Center + Vector3.up * (waterY + 5f) + Vector3.forward * 2f);
            yield return CaptureCue(cam, 125, "AcidRoofRain");

            // 44: AcidBurn
            cam.transform.position = standPos + Vector3.forward * 2f + Vector3.up * 1.5f;
            cam.transform.LookAt(standPos + Vector3.up * 1.2f);
            yield return CaptureCue(cam, 44, "AcidBurn", 24, f =>
            {
                if (f == 8)
                {
                    player.GetComponent<IDamageable>()?.Damage(5f, null);
                    GameEvents.RaiseDamaged(player.gameObject, 5f);
                }
            });

            DemoDisasters.StopAll(session);
            yield return new WaitForSecondsRealtime(1f);

            // ========================================================
            // SECTION F: EARTHQUAKE (2 CUES: 127, 46)
            // ========================================================
            Log("--- Commencing Earthquake captures ---");
            StartDisaster(session, "Earthquake", 60f);
            yield return new WaitForSecondsRealtime(1.5f);

            // 127: EarthJolt
            cam.transform.position = world.Center + Vector3.back * 18f + Vector3.up * 8f;
            cam.transform.LookAt(world.Center + Vector3.up * 6f);
            yield return CaptureCue(cam, 127, "EarthJolt");

            // 46: EarthRumble
            cam.transform.position = world.Center + Vector3.right * 16f + Vector3.up * 10f;
            cam.transform.LookAt(world.Center + Vector3.up * 7f);
            yield return CaptureCue(cam, 46, "EarthRumble");

            DemoDisasters.StopAll(session);
            yield return new WaitForSecondsRealtime(1f);

            // ========================================================
            // SECTION G: FIRE (1 CUE: 47)
            // ========================================================
            Log("--- Commencing Fire captures ---");
            StartDisaster(session, "Fire", 60f);
            yield return new WaitForSecondsRealtime(2.5f);

            // 47: FireCrackle
            cam.transform.position = standPos + Vector3.back * 5f + Vector3.up * 2f;
            cam.transform.LookAt(standPos);
            yield return CaptureCue(cam, 47, "FireCrackle");

            DemoDisasters.StopAll(session);
            yield return new WaitForSecondsRealtime(1f);

            // ========================================================
            // SECTION H: ICE (1 CUE: 102)
            // ========================================================
            Log("--- Commencing Ice captures ---");
            StartDisaster(session, "Icey", 60f);
            yield return new WaitForSecondsRealtime(2f);

            // 102: FrostCreak
            cam.transform.position = standPos + Vector3.back * 4f + Vector3.up * 2f;
            cam.transform.LookAt(standPos);
            yield return CaptureCue(cam, 102, "FrostCreak");

            DemoDisasters.StopAll(session);
            yield return new WaitForSecondsRealtime(1f);

            // ========================================================
            // SECTION I: LOW GRAVITY (1 CUE: 103)
            // ========================================================
            Log("--- Commencing Low Gravity captures ---");
            StartDisaster(session, "AntiGravity", 60f);
            yield return new WaitForSecondsRealtime(1.5f);

            // 103: LowGravityHum
            cam.transform.position = standPos + Vector3.back * 4f + Vector3.up * 2f;
            cam.transform.LookAt(standPos + Vector3.up * 1f);
            yield return CaptureCue(cam, 103, "LowGravityHum");

            DemoDisasters.StopAll(session);
            yield return new WaitForSecondsRealtime(1f);

            // ========================================================
            // SECTION J: POWER OUTAGE (3 CUES: 50, 104, 51)
            // ========================================================
            Log("--- Commencing Power Outage captures ---");
            StartDisaster(session, "PowerOutage", 60f);
            yield return new WaitForSecondsRealtime(1f);

            // 50: PowerDown
            cam.transform.position = world.Center + Vector3.back * 20f + Vector3.up * 10f;
            cam.transform.LookAt(world.Center + Vector3.up * 6f);
            yield return CaptureCue(cam, 50, "PowerDown");

            // 104: OutageHum
            cam.transform.position = standPos + Vector3.back * 3f + Vector3.up * 1.5f;
            cam.transform.LookAt(standPos);
            yield return CaptureCue(cam, 104, "OutageHum");

            // 51: PowerUp
            DemoDisasters.StopAll(session);
            cam.transform.position = world.Center + Vector3.back * 20f + Vector3.up * 10f;
            cam.transform.LookAt(world.Center + Vector3.up * 6f);
            yield return CaptureCue(cam, 51, "PowerUp");
            yield return new WaitForSecondsRealtime(1f);

            // ========================================================
            // SECTION K: REWIND (3 CUES: 134, 137, 135)
            // ========================================================
            Log("--- Commencing Rewind captures ---");
            StartDisaster(session, "TimeSlip", 60f);
            yield return new WaitForSecondsRealtime(1f);

            // 134: RewindStart
            cam.transform.position = world.Center + Vector3.back * 18f + Vector3.up * 8f;
            cam.transform.LookAt(world.Center + Vector3.up * 6f);
            yield return CaptureCue(cam, 134, "RewindStart");

            // 137: RewindBed
            cam.transform.position = standPos + Vector3.back * 4f + Vector3.up * 2f;
            cam.transform.LookAt(standPos);
            yield return CaptureCue(cam, 137, "RewindBed");

            // 135: RewindRelease
            DemoDisasters.StopAll(session);
            cam.transform.position = world.Center + Vector3.back * 18f + Vector3.up * 8f;
            cam.transform.LookAt(world.Center + Vector3.up * 6f);
            yield return CaptureCue(cam, 135, "RewindRelease");
            yield return new WaitForSecondsRealtime(1f);

            // ========================================================
            // SECTION L: VAMPIRE (2 CUES: 57, 136)
            // ========================================================
            Log("--- Commencing Vampire captures ---");
            StartDisaster(session, "Vampire", 60f);
            yield return new WaitForSecondsRealtime(1.5f);

            // 57: VampireSizzle (Direct sun)
            cam.transform.position = standPos + Vector3.back * 2f + Vector3.up * 1.5f;
            cam.transform.LookAt(standPos + Vector3.up * 1f);
            yield return CaptureCue(cam, 57, "VampireSizzle");

            // 136: VampireShade (Covered shadow)
            Vector3 shadeSpot = world.Center + Vector3.up * (waterY + 2f);
            cam.transform.position = shadeSpot + Vector3.back * 2f + Vector3.up * 1.5f;
            cam.transform.LookAt(shadeSpot + Vector3.up * 1f);
            yield return CaptureCue(cam, 136, "VampireShade");

            DemoDisasters.StopAll(session);
            yield return new WaitForSecondsRealtime(1f);

            // ========================================================
            // SECTION M: WARNINGS (3 CUES: 38, 39, 126)
            // ========================================================
            Log("--- Commencing Warnings captures ---");
            // 38: DisasterHorn
            cam.transform.position = world.Center + Vector3.back * 30f + Vector3.up * 14f;
            cam.transform.LookAt(world.Center + Vector3.up * 8f);
            yield return CaptureCue(cam, 38, "DisasterHorn");

            // 39: DisasterTension
            cam.transform.position = standPos + Vector3.up * 2f;
            cam.transform.LookAt(world.Center + Vector3.forward * 40f + Vector3.up * 15f);
            yield return CaptureCue(cam, 39, "DisasterTension");

            // 126: DisasterWarningTone
            cam.transform.position = standPos + Vector3.up * 1.55f;
            cam.transform.rotation = player.transform.rotation;
            yield return CaptureCue(cam, 126, "DisasterWarningTone");

            Object.DestroyImmediate(cam.gameObject);
            Log("Batch 1 (Disasters) captures successfully completed!");
        }

        sealed class SimplePickupSource : IEquipmentPickupSource
        {
            public EquipmentKind Kind { get; set; }
            public Vector3 Position { get; set; }
            public float Duration => 60f;
        }
    }
}
