using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CastleBots;
using FlyingHand;
using DeadlyJenga.CandyArt;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.World.Hazards;
using DeadlyJenga.Modes;
using DeadlyJenga.CandyAudio;
using DeadlyJenga.CandyAudio.Editor;
using DeadlyJenga.Wildlife;
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
                    else if (phase == 20)
                    {
                        SessionState.SetBool(Key, false);
                        SessionState.SetInt(Key + ".Phase", 0);
                        File.AppendAllText(Report, "BATCH 2 PLAYER COMPLETE at " + DateTime.UtcNow.ToString("O") + "\n");
                        Debug.Log("Slot 2 Batch 2 Player Captures Complete! See: " + Report);
                    }
                    else if (phase == 30)
                    {
                        SessionState.SetInt(Key + ".Phase", 31);
                        EditorApplication.delayCall += () =>
                        {
                            MatchSetup.MapId = "mode-ninja-temple";
                            MatchSetup.ModeId = "ninja";
                            MatchSetup.BotTarget = 1;
                            MatchSetup.HandCount = 0;
                            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
                            EditorApplication.EnterPlaymode();
                        };
                    }
                    else if (phase == 31)
                    {
                        SessionState.SetInt(Key + ".Phase", 32);
                        EditorApplication.delayCall += () =>
                        {
                            MatchSetup.MapId = "castle";
                            MatchSetup.ModeId = "demo";
                            EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));
                            EditorApplication.EnterPlaymode();
                        };
                    }
                    else if (phase == 32)
                    {
                        SessionState.SetBool(Key, false);
                        SessionState.SetInt(Key + ".Phase", 0);
                        File.AppendAllText(Report, "BATCH 3 WORLD COMPLETE at " + DateTime.UtcNow.ToString("O") + "\n");
                        Debug.Log("Slot 2 Batch 3 World Captures Complete! See: " + Report);
                    }
                    else if (phase == 50)
                    {
                        SessionState.SetBool(Key, false);
                        SessionState.SetInt(Key + ".Phase", 0);
                        File.AppendAllText(Report, "BATCH 5 WILDLIFE COMPLETE at " + DateTime.UtcNow.ToString("O") + "\n");
                        Debug.Log("Slot 2 Batch 5 Wildlife Captures Complete! See: " + Report);
                    }
                }
            };
        }

        [MenuItem("Tools/Candy Audio/Batch Captures/Run Batch 2 Player (42 Cues)")]
        public static void RunBatch2Player()
        {
            Directory.CreateDirectory(RawRoot);
            File.AppendAllText(Report, "Starting Batch 2 Player Captures (42 cues) · " + DateTime.UtcNow.ToString("O") + "\n");
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".Phase", 20);
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

        [MenuItem("Tools/Candy Audio/Batch Captures/Import Batch 2 Player Previews")]
        public static void ImportBatch2()
        {
            ImportBatch("batch2_player_meta.json");
        }

        [MenuItem("Tools/Candy Audio/Batch Captures/Run Batch 3 World (35 Cues)")]
        public static void RunBatch3World()
        {
            Directory.CreateDirectory(RawRoot);
            File.AppendAllText(Report, "Starting Batch 3 World Captures (35 cues) · " + DateTime.UtcNow.ToString("O") + "\n");
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".Phase", 30);
            MatchSetup.FromLobby = false;
            MatchSetup.StartLocalSession = false;
            MatchSetup.MapId = "castle";
            MatchSetup.ModeId = "demo";
            MatchSetup.BotTarget = 0;
            MatchSetup.HandCount = 1;
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

        [MenuItem("Tools/Candy Audio/Batch Captures/Import Batch 3 World Previews")]
        public static void ImportBatch3()
        {
            ImportBatch("batch3_world_meta.json");
        }

        [MenuItem("Tools/Candy Audio/Batch Captures/Run Batch 3 Lobby Only (3 Cues)")]
        public static void RunBatch3LobbyOnly()
        {
            Directory.CreateDirectory(RawRoot);
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".Phase", 32);
            MatchSetup.FromLobby = false;
            MatchSetup.StartLocalSession = false;
            MatchSetup.MapId = "castle";
            MatchSetup.ModeId = "demo";
            if (EditorApplication.isPlaying && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Lobby")
            {
                Application.runInBackground = true;
                EditorApplication.update -= StartRoutineWhenReady;
                EditorApplication.update += StartRoutineWhenReady;
            }
            else
            {
                EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));
                EditorApplication.EnterPlaymode();
            }
        }

        [MenuItem("Tools/Candy Audio/Batch Captures/Run Batch 5 Wildlife (21 Cues)")]
        public static void RunBatch5Wildlife()
        {
            Directory.CreateDirectory(RawRoot);
            File.AppendAllText(Report, "Starting Batch 5 Wildlife Captures (21 cues) · " + DateTime.UtcNow.ToString("O") + "\n");
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".Phase", 50);
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

        [MenuItem("Tools/Candy Audio/Batch Captures/Import Batch 5 Wildlife Previews")]
        public static void ImportBatch5()
        {
            ImportBatch("batch5_wildlife_meta.json");
        }

        public static void ImportBatch(string metaFileName)
        {
            var bank = Resources.Load<CandyAudioBank>("CandyAudio/Default");
            string capturesRoot = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "SoundStudio", "captures");
            string metaPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "SoundStudio", metaFileName);
            if (!File.Exists(metaPath)) throw new FileNotFoundException("Missing meta file: " + metaPath);
            string metaText = File.ReadAllText(metaPath);

            int count = 0;
            foreach (var dir in Directory.GetDirectories(capturesRoot))
            {
                string folderName = Path.GetFileName(dir);
                if (!int.TryParse(folderName, out int cueId)) continue;
                if (!metaText.Contains("\"" + cueId + "\":")) continue;

                string pkg = Path.Combine(dir, "preview.json");
                if (File.Exists(pkg))
                {
                    CandySoundPreview.Import(pkg, bank);
                    count++;
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Imported {count} previews for {metaFileName}");
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
            else if (phase == 20)
            {
                var session = Object.FindFirstObjectByType<CastlePlaySession>();
                if (!session || session.Player == null || Services.World == null || !session.ActiveCamera) return;
                EditorApplication.update -= StartRoutineWhenReady;
                session.StartCoroutine(Guard(RunBatch2PlayerCaptures(session)));
            }
            else if (phase == 30)
            {
                var session = Object.FindFirstObjectByType<CastlePlaySession>();
                if (!session || session.Player == null || Services.World == null || !session.ActiveCamera) return;
                EditorApplication.update -= StartRoutineWhenReady;
                session.StartCoroutine(Guard(RunBatch3CastleCaptures(session)));
            }
            else if (phase == 31)
            {
                var session = Object.FindFirstObjectByType<CastlePlaySession>();
                if (!session || session.Player == null || Services.World == null || !session.ActiveCamera) return;
                EditorApplication.update -= StartRoutineWhenReady;
                session.StartCoroutine(Guard(RunBatch3NinjaCaptures(session)));
            }
            else if (phase == 32)
            {
                var lobby = Object.FindFirstObjectByType<IslandLobby>();
                if (!lobby || lobby.Player == null || !lobby.ActiveCamera) return;
                EditorApplication.update -= StartRoutineWhenReady;
                lobby.StartCoroutine(Guard(RunBatch3LobbyCaptures(lobby)));
            }
            else if (phase == 50)
            {
                var session = Object.FindFirstObjectByType<CastlePlaySession>();
                if (!session || session.Player == null || Services.World == null || !session.ActiveCamera) return;
                EditorApplication.update -= StartRoutineWhenReady;
                session.StartCoroutine(Guard(RunBatch5WildlifeCaptures(session)));
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

        static IEnumerator RunBatch2PlayerCaptures(CastlePlaySession session)
        {
            Log("Game.unity session loaded for Batch 2 Player. Setting up match rules...");
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
            if (session.Hand != null) session.Hand.InputSuspended = true;

            var world = Services.World;
            float waterY = world != null ? world.WaterHeight(world.Center) : 0f;

            // Dedicated camera
            var camObj = new GameObject("Batch2_Capture_Camera");
            var cam = camObj.AddComponent<Camera>();
            cam.CopyFrom(session.ActiveCamera);
            cam.enabled = false;

            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;

            // Find top castle platform block
            var blocksWithCollider = world.Blocks.Where(b => b && b.GetComponent<Collider>()).ToList();
            var castlePlatform = blocksWithCollider
                .Select(b => b.GetComponent<Collider>().bounds)
                .OrderByDescending(b => b.max.y).First();
            Vector3 platformTop = new Vector3(castlePlatform.center.x, castlePlatform.max.y + 0.05f, castlePlatform.center.z);

            // Spawn bot survivor
            GameObject bot = session.SurvivorBots.SpawnOne();
            if (!bot)
            {
                Log("Using session.Player as primary survivor actor");
                bot = session.Player.gameObject;
            }
            var botMotor = bot.GetComponent<CastleSurvivor>();
            var botBrain = bot.GetComponent<CastleBotBrain>();
            var botEmotes = bot.GetComponent<CastleBotEmotes>();
            var botAvatar = bot.GetComponent<CandyPlayerAvatar>();
            if (botBrain) botBrain.enabled = false;
            if (botMotor) botMotor.InputSuspended = true;

            if (botMotor) botMotor.Teleport(platformTop, Quaternion.LookRotation(Vector3.back));
            yield return new WaitForSecondsRealtime(1f);

            // ========================================================
            // SECTION A: EMOTES (14 CUES)
            // ========================================================
            Log("--- Commencing Emote captures (14 cues) ---");
            cam.transform.position = platformTop + Vector3.back * 2.2f + Vector3.up * 1.35f;
            cam.transform.LookAt(platformTop + Vector3.up * 1.15f);

            // 27: Friendly Wave
            yield return CaptureCue(cam, 27, "Friendly Wave", 24, f => { if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.FriendlyWave, true); });
            // 28: Totally Meant That
            yield return CaptureCue(cam, 28, "Totally Meant That", 24, f => { if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.TotallyMeantThat, true); });
            // 29: Phew
            yield return CaptureCue(cam, 29, "Phew", 24, f => { if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.Phew, true); });
            // 30: What Was That
            yield return CaptureCue(cam, 30, "What Was That", 24, f => { if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.WhatWasThat, true); });
            // 31: Golf Clap
            yield return CaptureCue(cam, 31, "Golf Clap", 24, f => { if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.GolfClap, true); });
            // 32: Just Chilling
            yield return CaptureCue(cam, 32, "Just Chilling", 24, f => { if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.JustChilling, true); });
            // 33: Nope Nope Nope
            yield return CaptureCue(cam, 33, "Nope Nope Nope", 24, f => { if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.NopeNopeNope, true); });
            // 34: Tiny Victory Dance
            yield return CaptureCue(cam, 34, "Tiny Victory Dance", 24, f => { if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.TinyVictoryDance, true); });

            // 99982: Irish Dance
            yield return CaptureCue(cam, 99982, "Irish Dance", 24, f => {
                if (f == 6) { if (botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.RobotDance, true); GameEvents.RaiseEmoted(bot, "Irish Dance"); }
            });
            // 211503: Thumbs Down
            yield return CaptureCue(cam, 211503, "Thumbs Down", 24, f => {
                if (f == 6) { if (botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.ThumbsDown, true); GameEvents.RaiseEmoted(bot, "Thumbs Down"); }
            });
            // 418932: Finger Wag
            yield return CaptureCue(cam, 418932, "Finger Wag", 24, f => {
                if (f == 6) { if (botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.WatchingYou, true); GameEvents.RaiseEmoted(bot, "Finger Wag"); }
            });
            // 663524: Breakdance
            yield return CaptureCue(cam, 663524, "Breakdance", 24, f => {
                if (f == 6) { if (botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.AirGuitar, true); GameEvents.RaiseEmoted(bot, "Breakdance"); }
            });
            // 759625: Slow Clap
            yield return CaptureCue(cam, 759625, "Slow Clap", 24, f => {
                if (f == 6) { if (botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.GolfClap, true); GameEvents.RaiseEmoted(bot, "Slow Clap"); }
            });
            // 858650: Point And Laugh
            yield return CaptureCue(cam, 858650, "Point And Laugh", 24, f => {
                if (f == 6) { if (botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.Laughing, true); GameEvents.RaiseEmoted(bot, "Point And Laugh"); }
            });

            // ========================================================
            // SECTION B: MOVEMENT (6 CUES)
            // ========================================================
            Log("--- Commencing Movement captures (6 cues) ---");

            // 9: Footstep Candy
            if (botMotor) botMotor.Teleport(platformTop, Quaternion.LookRotation(Vector3.back));
            cam.transform.position = platformTop + Vector3.back * 1.5f + Vector3.right * 0.4f + Vector3.up * 0.45f;
            cam.transform.LookAt(platformTop + Vector3.up * 0.2f);
            yield return CaptureCue(cam, 9, "Footstep Candy", 24, f => {
                if (f >= 6 && botMotor) botMotor.Teleport(platformTop + Vector3.back * (f - 6) * 0.06f, Quaternion.LookRotation(Vector3.back));
            });

            // 10: Footstep Sand
            Vector3 sandSpot = world.Center + Vector3.forward * 32f + Vector3.up * (waterY + 0.15f);
            if (botMotor) botMotor.Teleport(sandSpot, Quaternion.LookRotation(Vector3.back));
            cam.transform.position = sandSpot + Vector3.back * 1.5f + Vector3.right * 0.4f + Vector3.up * 0.45f;
            cam.transform.LookAt(sandSpot + Vector3.up * 0.2f);
            yield return CaptureCue(cam, 10, "Footstep Sand", 24, f => {
                if (f >= 6 && botMotor) botMotor.Teleport(sandSpot + Vector3.back * (f - 6) * 0.06f, Quaternion.LookRotation(Vector3.back));
            });

            // 11: Jump
            if (botMotor) botMotor.Teleport(platformTop, Quaternion.LookRotation(Vector3.back));
            cam.transform.position = platformTop + Vector3.right * 2.8f + Vector3.up * 1.1f;
            cam.transform.LookAt(platformTop + Vector3.up * 1.2f);
            yield return CaptureCue(cam, 11, "Jump", 24, f => {
                if (f >= 6 && botMotor) {
                    float t = (f - 6) / 18f;
                    float yOffset = Mathf.Sin(t * Mathf.PI) * 1.6f;
                    botMotor.Teleport(platformTop + Vector3.up * yOffset, Quaternion.LookRotation(Vector3.back));
                }
            });

            // 12: Land Soft
            cam.transform.position = platformTop + Vector3.right * 2.4f + Vector3.up * 0.6f;
            cam.transform.LookAt(platformTop + Vector3.up * 0.35f);
            yield return CaptureCue(cam, 12, "Land Soft", 24, f => {
                if (botMotor) {
                    float y = f < 6 ? Mathf.Lerp(1.2f, 0f, f / 6f) : 0f;
                    botMotor.Teleport(platformTop + Vector3.up * y, Quaternion.LookRotation(Vector3.back));
                }
            });

            // 18: Fall Wind
            Vector3 towerEdge = world.Center + Vector3.right * 14f + Vector3.up * 25f;
            cam.transform.position = towerEdge + Vector3.forward * 3.5f + Vector3.up * 1f;
            cam.transform.LookAt(towerEdge);
            yield return CaptureCue(cam, 18, "Fall Wind", 24, f => {
                float y = Mathf.Lerp(25f, 4f, f / 24f);
                Vector3 fallPos = new Vector3(towerEdge.x, y, towerEdge.z);
                if (botMotor) botMotor.Teleport(fallPos, Quaternion.LookRotation(Vector3.down));
                cam.transform.position = fallPos + Vector3.forward * 3.5f + Vector3.up * 1f;
                cam.transform.LookAt(fallPos);
            });

            // 6: Player Splash
            Vector3 waterSpot = world.Center + Vector3.forward * 22f + Vector3.up * waterY;
            cam.transform.position = waterSpot + Vector3.back * 3.5f + Vector3.up * 0.8f;
            cam.transform.LookAt(waterSpot + Vector3.up * 0.2f);
            yield return CaptureCue(cam, 6, "Player Splash", 24, f => {
                float y = f < 6 ? Mathf.Lerp(waterY + 3f, waterY, f / 6f) : waterY - (f - 6) * 0.08f;
                if (botMotor) botMotor.Teleport(new Vector3(waterSpot.x, y, waterSpot.z), Quaternion.LookRotation(Vector3.back));
            });

            // ========================================================
            // SECTION C: CLIMBING & STAMINA (10 CUES)
            // ========================================================
            Log("--- Commencing Climbing & Stamina captures (10 cues) ---");
            if (botMotor) botMotor.Teleport(platformTop, Quaternion.LookRotation(Vector3.back));
            cam.transform.position = platformTop + Vector3.back * 2.2f + Vector3.up * 1.35f;
            cam.transform.LookAt(platformTop + Vector3.up * 1.15f);

            // 15: Climb Grunt
            cam.transform.position = platformTop + Vector3.right * 1.8f + Vector3.back * 0.5f + Vector3.up * 0.5f;
            cam.transform.LookAt(platformTop);
            yield return CaptureCue(cam, 15, "Climb Grunt", 24, f => {
                if (botMotor) {
                    float y = f < 6 ? -0.8f : Mathf.Lerp(-0.8f, 0f, (f - 6) / 18f);
                    botMotor.Teleport(platformTop + Vector3.up * y, Quaternion.LookRotation(Vector3.forward));
                }
            });

            // 14: Vault Grunt
            cam.transform.position = platformTop + Vector3.right * 1.8f + Vector3.up * 0.6f;
            cam.transform.LookAt(platformTop + Vector3.up * 0.2f);
            yield return CaptureCue(cam, 14, "Vault Grunt", 24, f => {
                if (botMotor) {
                    float y = f < 6 ? -0.4f : Mathf.Lerp(-0.4f, 0.1f, (f - 6) / 18f);
                    float z = f < 6 ? -0.3f : Mathf.Lerp(-0.3f, 0.3f, (f - 6) / 18f);
                    botMotor.Teleport(platformTop + new Vector3(0, y, z), Quaternion.LookRotation(Vector3.forward));
                }
            });

            // 17: Climb Release
            yield return CaptureCue(cam, 17, "Climb Release", 24, f => {
                if (botMotor) {
                    float y = f < 6 ? 0f : -(f - 6) * 0.12f;
                    botMotor.Teleport(platformTop + Vector3.up * y, Quaternion.LookRotation(Vector3.forward));
                }
            });

            // 26: Grip Warning
            if (botMotor) botMotor.Teleport(platformTop, Quaternion.LookRotation(Vector3.back));
            cam.transform.position = platformTop + Vector3.back * 2.0f + Vector3.up * 1.3f;
            cam.transform.LookAt(platformTop + Vector3.up * 1.1f);
            yield return CaptureCue(cam, 26, "Grip Warning", 24, f => {
                if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.Shaking, true);
            });

            // 35: Breath Exertion
            yield return CaptureCue(cam, 35, "Breath Exertion", 24, f => {
                if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.Exhausted, true);
            });

            // 36: Breath Recovery
            yield return CaptureCue(cam, 36, "Breath Recovery", 24, f => {
                if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.DeepBreath, true);
            });

            // 37: Breath Exhausted
            yield return CaptureCue(cam, 37, "Breath Exhausted", 24, f => {
                if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.Exhausted, true);
            });

            // 121: Stamina Low
            yield return CaptureCue(cam, 121, "Stamina Low", 24, f => {
                if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.Stretch, true);
            });

            // 122: Stamina Empty
            yield return CaptureCue(cam, 122, "Stamina Empty", 24, f => {
                if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.Exhausted, true);
            });

            // 123: Stamina Usable
            yield return CaptureCue(cam, 123, "Stamina Usable", 24, f => {
                if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.ReadyToFight, true);
            });

            // ========================================================
            // SECTION D: HEALTH & DEATH (6 CUES)
            // ========================================================
            Log("--- Commencing Health & Death captures (6 cues) ---");

            // 82: Player Pain
            yield return CaptureCue(cam, 82, "Player Pain", 24, f => {
                if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.Oops, true);
            });

            // 83: Player Death
            yield return CaptureCue(cam, 83, "Player Death", 24, f => {
                if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.FacePalm, true);
            });

            // 87: Player Heal
            yield return CaptureCue(cam, 87, "Player Heal", 24, f => {
                if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.SafeAtLast, true);
            });

            // 117: Nearby Death
            var botB = session.SurvivorBots.SpawnOne();
            if (botB)
            {
                var botBMotor = botB.GetComponent<CastleSurvivor>();
                var botBBrain = botB.GetComponent<CastleBotBrain>();
                var botBEmotes = botB.GetComponent<CastleBotEmotes>();
                if (botBBrain) botBBrain.enabled = false;
                if (botBMotor) {
                    botBMotor.InputSuspended = true;
                    botBMotor.Teleport(platformTop + Vector3.right * 1.5f, Quaternion.LookRotation(Vector3.back));
                }
                cam.transform.position = platformTop + Vector3.back * 3.2f + Vector3.up * 1.35f;
                cam.transform.LookAt(platformTop + Vector3.right * 0.75f + Vector3.up * 1.15f);
                yield return CaptureCue(cam, 117, "Nearby Death", 24, f => {
                    if (f == 6) {
                        if (botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.BodyFound, true);
                        if (botBEmotes) botBEmotes.TryPlay(CastleBotEmotes.Emote.FacePalm, true);
                    }
                });
                Object.DestroyImmediate(botB);
            }
            else
            {
                yield return CaptureCue(cam, 117, "Nearby Death", 24, f => {
                    if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.BodyFound, true);
                });
            }

            // 120: Drowning Death
            cam.transform.position = waterSpot + Vector3.back * 2.4f + Vector3.down * 0.6f;
            cam.transform.LookAt(waterSpot + Vector3.down * 0.6f);
            if (botMotor) botMotor.Teleport(waterSpot + Vector3.down * 0.6f, Quaternion.LookRotation(Vector3.back));
            yield return CaptureCue(cam, 120, "Drowning Death", 24, f => {
                if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.PleaseNo, true);
            });

            // 124: Health Regenerating
            if (botMotor) botMotor.Teleport(platformTop, Quaternion.LookRotation(Vector3.back));
            cam.transform.position = platformTop + Vector3.back * 2.2f + Vector3.up * 1.35f;
            cam.transform.LookAt(platformTop + Vector3.up * 1.15f);
            yield return CaptureCue(cam, 124, "Health Regenerating", 24, f => {
                if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.SafeAtLast, true);
            });

            // ========================================================
            // SECTION E: TRAVEL & SPAWNING (6 CUES)
            // ========================================================
            Log("--- Commencing Travel & Spawning captures (6 cues) ---");

            // 23: Spawn
            cam.transform.position = platformTop + Vector3.back * 2.8f + Vector3.up * 1.4f;
            cam.transform.LookAt(platformTop + Vector3.up * 1.0f);
            yield return CaptureCue(cam, 23, "Spawn", 24, f => {
                if (botMotor) {
                    float y = f < 6 ? -5f : 0f;
                    botMotor.Teleport(platformTop + Vector3.up * y, Quaternion.LookRotation(Vector3.back));
                    if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.Salute, true);
                }
            });

            // 24: Respawn
            yield return CaptureCue(cam, 24, "Respawn", 24, f => {
                if (botMotor) {
                    float y = f < 6 ? -5f : 0f;
                    botMotor.Teleport(platformTop + Vector3.up * y, Quaternion.LookRotation(Vector3.back));
                    if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.Bow, true);
                }
            });

            // 58: Lobby Launch
            yield return CaptureCue(cam, 58, "Lobby Launch", 24, f => {
                if (botMotor) {
                    float y = f < 6 ? 0f : (f - 6) * 0.8f;
                    botMotor.Teleport(platformTop + Vector3.up * y, Quaternion.LookRotation(Vector3.up));
                }
            });

            // 59: Spawn Slam
            cam.transform.position = platformTop + Vector3.back * 2.2f + Vector3.up * 0.6f;
            cam.transform.LookAt(platformTop + Vector3.up * 0.3f);
            yield return CaptureCue(cam, 59, "Spawn Slam", 24, f => {
                if (botMotor) {
                    float y = f < 6 ? Mathf.Lerp(4f, 0f, f / 6f) : 0f;
                    botMotor.Teleport(platformTop + Vector3.up * y, Quaternion.LookRotation(Vector3.back));
                    if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.JumpForJoy, true);
                }
            });

            // 108: Beam Flyout
            cam.transform.position = platformTop + Vector3.back * 3.0f + Vector3.up * 1.5f;
            cam.transform.LookAt(platformTop + Vector3.up * 1.2f);
            yield return CaptureCue(cam, 108, "Beam Flyout", 24, f => {
                if (botMotor) {
                    float y = f < 6 ? 0f : (f - 6) * 0.9f;
                    botMotor.Teleport(platformTop + Vector3.up * y, Quaternion.LookRotation(Vector3.back));
                }
            });

            // 109: Beam Arrival
            yield return CaptureCue(cam, 109, "Beam Arrival", 24, f => {
                if (botMotor) {
                    float y = f < 6 ? 8f : 0f;
                    botMotor.Teleport(platformTop + Vector3.up * y, Quaternion.LookRotation(Vector3.back));
                    if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.Salute, true);
                }
            });

            Object.DestroyImmediate(cam.gameObject);
            session.SurvivorBots.Clear();
            Log("Batch 2 (Player) captures successfully completed!");
        }

        static IEnumerator RunBatch3CastleCaptures(CastlePlaySession session)
        {
            Log("Setting up Castle scene for Batch 3...");
            AudioListener.volume = 0;
            session.SurvivorBots.AutoFill = false;
            session.SurvivorBots.Clear();
            session.HandBots?.Clear();
            if (session.Social != null) session.Social.VoiceEnabled = false;

            var rules = ModeSimulation.Defaults(GameModeKind.Demo);
            rules.countdownSeconds = 0;
            rules.healthEnabled = false;
            rules.deathEnabled = false;
            rules.fallDamage = false;
            rules.island = IslandRule.Walkable;
            session.Modes.Configure(rules);
            session.Modes.StartRound();
            session.SetRole(true);
            session.SetMenu(false);

            session.Player.InputSuspended = true;
            if (session.Hand != null) session.Hand.InputSuspended = true;

            var world = Services.World;
            float waterY = world != null ? world.WaterHeight(world.Center) : 0f;

            var camObj = new GameObject("Batch3_Capture_Camera");
            var cam = camObj.AddComponent<Camera>();
            cam.CopyFrom(session.ActiveCamera);
            cam.enabled = false;

            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;

            var blocksWithCollider = world.Blocks.Where(b => b && b.GetComponent<Collider>()).ToList();
            var topBlock = blocksWithCollider.Select(b => b.GetComponent<Collider>().bounds).OrderByDescending(b => b.max.y).First();
            Vector3 platformTop = new Vector3(topBlock.center.x, topBlock.max.y + 0.05f, topBlock.center.z);

            var midBlock = blocksWithCollider.FirstOrDefault(b => b.position.y > waterY + 4f && b.position.y < waterY + 10f);
            Vector3 blockPos = midBlock ? midBlock.position : platformTop;

            // SECTION 1: BLOCKS (6 Cues: 0, 1, 2, 3, 4, 5)
            Log("--- Commencing Blocks captures (6 cues) ---");

            // Cue 0: Block Tap
            cam.transform.position = blockPos + Vector3.back * 1.8f + Vector3.up * 0.4f;
            cam.transform.LookAt(blockPos);
            yield return CaptureCue(cam, 0, "Block Tap", 24, f => {
                if (midBlock && f == 6) midBlock.transform.position += Vector3.up * 0.04f;
            });

            // Cue 1: Block Hit
            cam.transform.position = blockPos + Vector3.back * 2.2f + Vector3.up * 0.8f;
            cam.transform.LookAt(blockPos);
            yield return CaptureCue(cam, 1, "Block Hit", 24, f => {
                if (midBlock) {
                    float dy = f < 6 ? (6 - f) * 0.2f : 0f;
                    midBlock.transform.position = blockPos + Vector3.up * dy;
                }
            });

            // Cue 2: Block Smash
            yield return CaptureCue(cam, 2, "Block Smash", 24, f => {
                if (midBlock && f >= 6) midBlock.transform.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.2f, 0.2f, 1.2f), (f - 6) / 10f);
            });
            if (midBlock) midBlock.transform.localScale = Vector3.one;

            // Cue 3: Block Scrape
            cam.transform.position = blockPos + Vector3.right * 1.6f + Vector3.up * 0.5f;
            cam.transform.LookAt(blockPos);
            yield return CaptureCue(cam, 3, "Block Scrape", 24, f => {
                if (midBlock) {
                    float dx = f < 6 ? 0f : (f - 6) * 0.05f;
                    midBlock.transform.position = blockPos + Vector3.forward * dx;
                }
            });

            // Cue 4: Structure Lean
            cam.transform.position = world.Center + new Vector3(0, waterY + 12f, -18f);
            cam.transform.LookAt(world.Center + Vector3.up * (waterY + 10f));
            yield return CaptureCue(cam, 4, "Structure Lean", 24, f => {
                float angle = Mathf.Sin(f / 24f * Mathf.PI * 2) * 4f;
                cam.transform.RotateAround(world.Center, Vector3.forward, angle * 0.05f);
            });

            // Cue 5: Block Splash
            Vector3 splashPos = world.Center + Vector3.forward * 8f;
            splashPos.y = waterY;
            cam.transform.position = splashPos + Vector3.back * 4.5f + Vector3.up * 2f;
            cam.transform.LookAt(splashPos + Vector3.up * 0.5f);
            yield return CaptureCue(cam, 5, "Block Splash", 24, f => {
                if (midBlock) {
                    float y = f < 6 ? waterY + (6 - f) * 0.8f : waterY - (f - 6) * 0.2f;
                    midBlock.transform.position = splashPos + Vector3.up * (y - waterY);
                }
            });

            // SECTION 2: SINKING ISLAND (3 Cues: 56, 118, 119)
            Log("--- Commencing Sinking Island captures (3 cues) ---");

            // Cue 56: Island Sink
            cam.transform.position = world.Center + new Vector3(14f, waterY + 4f, 14f);
            cam.transform.LookAt(world.Center + Vector3.up * waterY);
            yield return CaptureCue(cam, 56, "Island Sink", 24, f => {
                cam.transform.position += Vector3.down * 0.02f;
            });

            // Cue 118: Island Sinking
            cam.transform.position = world.Center + new Vector3(-8f, waterY + 1.5f, -10f);
            cam.transform.LookAt(world.Center + Vector3.up * (waterY + 0.5f));
            yield return CaptureCue(cam, 118, "Island Sinking", 24, f => {
                cam.transform.position += Vector3.down * 0.03f;
            });

            // Cue 119: Island Death
            cam.transform.position = world.Center + new Vector3(0, waterY + 0.8f, -6f);
            cam.transform.LookAt(world.Center + Vector3.up * (waterY - 0.5f));
            yield return CaptureCue(cam, 119, "Island Death", 24, f => {
                cam.transform.position += Vector3.down * 0.05f;
            });

            // SECTION 3: FLYING HAND (6 Cues: 7, 8, 20, 52, 53, 54)
            Log("--- Commencing Flying Hand captures (6 cues) ---");
            var hand = session.Hand != null ? session.Hand.gameObject : null;
            if (!hand)
            {
                var hObj = Object.FindFirstObjectByType<FlyingHandController>();
                if (hObj) hand = hObj.gameObject;
            }
            Vector3 handTarget = platformTop + Vector3.up * 0.3f;

            // Cue 7: Rubber Slide
            cam.transform.position = handTarget + Vector3.back * 1.6f + Vector3.up * 0.5f;
            cam.transform.LookAt(handTarget);
            yield return CaptureCue(cam, 7, "Rubber Slide", 24, f => {
                if (hand) {
                    float dx = (f - 12) * 0.04f;
                    hand.transform.position = handTarget + Vector3.right * dx;
                }
            });

            // Cue 8: Rubber Touch
            yield return CaptureCue(cam, 8, "Rubber Touch", 24, f => {
                if (hand) {
                    float dy = f < 6 ? (6 - f) * 0.06f : 0f;
                    hand.transform.position = handTarget + Vector3.up * dy;
                }
            });

            // Cue 20: Hand Flight
            cam.transform.position = world.Center + new Vector3(0, waterY + 15f, -14f);
            cam.transform.LookAt(world.Center + Vector3.up * (waterY + 14f));
            yield return CaptureCue(cam, 20, "Hand Flight", 24, f => {
                if (hand) {
                    float t = f / 24f;
                    hand.transform.position = Vector3.Lerp(world.Center + new Vector3(-8f, waterY + 14f, 0), world.Center + new Vector3(8f, waterY + 16f, 0), t);
                    hand.transform.rotation = Quaternion.Euler(0, 90f, Mathf.Sin(t * Mathf.PI * 4f) * 15f);
                }
            });

            // Cue 52: Finger Flex
            cam.transform.position = handTarget + Vector3.back * 1.2f + Vector3.up * 0.3f;
            cam.transform.LookAt(handTarget);
            if (hand) hand.transform.position = handTarget;
            yield return CaptureCue(cam, 52, "Finger Flex", 24, f => {
                if (hand) {
                    float scale = 1f + Mathf.Sin(f / 24f * Mathf.PI * 2f) * 0.1f;
                    hand.transform.localScale = Vector3.one * scale;
                }
            });

            // Cue 53: Hand Swirl
            yield return CaptureCue(cam, 53, "Hand Swirl", 24, f => {
                if (hand) {
                    hand.transform.Rotate(Vector3.up, 15f);
                    hand.transform.position = handTarget + new Vector3(Mathf.Cos(f * 0.3f) * 0.2f, Mathf.Sin(f * 0.3f) * 0.2f, 0);
                }
            });

            // Cue 54: Push Impact
            yield return CaptureCue(cam, 54, "Push Impact", 24, f => {
                if (hand) {
                    float z = f < 6 ? -0.4f + f * 0.08f : (f < 12 ? 0.08f - (f - 6) * 0.04f : 0f);
                    hand.transform.position = handTarget + Vector3.forward * z;
                }
            });
            if (hand) hand.transform.localScale = Vector3.one;

            // SECTION 4: MAP AMBIENCE, MUSIC & UI (6 Cues: 19, 22, 55, 105, 106, 107)
            Log("--- Commencing Ambience, Music & UI captures (6 cues) ---");

            // Cue 19: Beach Wind
            cam.transform.position = world.Center + new Vector3(-16f, waterY + 8f, -22f);
            cam.transform.LookAt(world.Center + Vector3.up * (waterY + 6f));
            yield return CaptureCue(cam, 19, "Beach Wind", 24, f => {
                cam.transform.position += Vector3.right * 0.04f;
            });

            // Cue 22: Music
            cam.transform.position = world.Center + new Vector3(18f, waterY + 16f, -18f);
            cam.transform.LookAt(world.Center + Vector3.up * (waterY + 12f));
            yield return CaptureCue(cam, 22, "Music", 24, f => {
                cam.transform.RotateAround(world.Center, Vector3.up, 0.4f);
            });

            var bot = session.SurvivorBots.SpawnOne();
            if (!bot) bot = session.Player.gameObject;
            var botMotor = bot.GetComponent<CastleSurvivor>();
            var botEmotes = bot.GetComponent<CastleBotEmotes>();
            if (botMotor) botMotor.Teleport(platformTop, Quaternion.LookRotation(Vector3.back));

            // Cue 55: Claim Win
            cam.transform.position = platformTop + Vector3.back * 2.2f + Vector3.up * 1.35f;
            cam.transform.LookAt(platformTop + Vector3.up * 1.15f);
            yield return CaptureCue(cam, 55, "Claim Win", 24, f => {
                if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.JumpForJoy, true);
            });

            // Cue 105: Scoreboard Open
            cam.transform.position = platformTop + Vector3.back * 2.5f + Vector3.up * 1.4f;
            cam.transform.LookAt(platformTop + Vector3.up * 1.1f);
            yield return CaptureCue(cam, 105, "Scoreboard Open", 24, f => {
                if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.FriendlyWave, true);
            });

            // Cue 106: Point Scored
            yield return CaptureCue(cam, 106, "Point Scored", 24, f => {
                if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.ThumbsUp, true);
            });

            // Cue 107: Round Results
            yield return CaptureCue(cam, 107, "Round Results", 24, f => {
                if (f == 6 && botEmotes) botEmotes.TryPlay(CastleBotEmotes.Emote.Salute, true);
            });

            Object.DestroyImmediate(cam.gameObject);
            session.SurvivorBots.Clear();
            Log("Castle scene captures complete.");
        }

        static IEnumerator RunBatch3NinjaCaptures(CastlePlaySession session)
        {
            Log("Setting up Ninja Temple scene for Batch 3...");
            AudioListener.volume = 0;
            session.SurvivorBots.AutoFill = false;
            session.SurvivorBots.Clear();
            session.HandBots?.Clear();
            if (session.Social != null) session.Social.VoiceEnabled = false;

            var world = Services.World;
            float waterY = world != null ? world.WaterHeight(world.Center) : 0.08f;

            var camObj = new GameObject("Batch3_Ninja_Camera");
            var cam = camObj.AddComponent<Camera>();
            cam.CopyFrom(session.ActiveCamera);
            cam.enabled = false;

            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;

            Vector3 center = world != null ? world.Center : Vector3.zero;
            Vector3 courtyard = center + Vector3.up * (waterY + 1f);

            var ninjaBot = session.SurvivorBots.SpawnOne();
            if (!ninjaBot) ninjaBot = session.Player.gameObject;
            var ninjaMotor = ninjaBot.GetComponent<CastleSurvivor>();
            var ninjaEmotes = ninjaBot.GetComponent<CastleBotEmotes>();
            if (ninjaMotor) ninjaMotor.Teleport(courtyard, Quaternion.LookRotation(Vector3.back));

            Log("--- Commencing Ninja Temple captures (11 cues) ---");

            // Cue 27900: Taiko
            cam.transform.position = courtyard + Vector3.back * 6f + Vector3.up * 3f;
            cam.transform.LookAt(courtyard + Vector3.up * 2f);
            yield return CaptureCue(cam, 27900, "Taiko", 24, f => {
                cam.transform.position += Vector3.forward * 0.03f;
            });

            // Cue 114211: Water
            cam.transform.position = center + new Vector3(8f, waterY + 1.2f, 8f);
            cam.transform.LookAt(center + new Vector3(0, waterY, 0));
            yield return CaptureCue(cam, 114211, "Water", 24, f => {
                cam.transform.position += Vector3.left * 0.02f;
            });

            // Cue 219870: Chime
            cam.transform.position = courtyard + new Vector3(-2f, 4f, -3f);
            cam.transform.LookAt(courtyard + Vector3.up * 4.5f);
            yield return CaptureCue(cam, 219870, "Chime", 24, f => {
                cam.transform.Rotate(0, 0, Mathf.Sin(f * 0.4f) * 0.2f);
            });

            // Cue 292904: Rain
            cam.transform.position = courtyard + Vector3.back * 4f + Vector3.up * 1.5f;
            cam.transform.LookAt(courtyard + Vector3.up * 3.5f);
            yield return CaptureCue(cam, 292904, "Rain", 24, f => {
                cam.transform.position += Vector3.up * 0.02f;
            });

            // Cue 346823: Poof
            cam.transform.position = courtyard + Vector3.back * 3f + Vector3.up * 1.4f;
            cam.transform.LookAt(courtyard + Vector3.up * 1.1f);
            yield return CaptureCue(cam, 346823, "Poof", 24, f => {
                if (ninjaMotor) {
                    float y = f < 6 ? 0f : -10f;
                    ninjaMotor.Teleport(courtyard + Vector3.up * y, Quaternion.LookRotation(Vector3.back));
                }
            });
            if (ninjaMotor) ninjaMotor.Teleport(courtyard, Quaternion.LookRotation(Vector3.back));

            // Cue 403756: Clack
            cam.transform.position = center + new Vector3(4f, waterY + 0.8f, -4f);
            cam.transform.LookAt(center + new Vector3(4f, waterY + 0.3f, -2f));
            yield return CaptureCue(cam, 403756, "Clack", 24, f => {
                cam.transform.position += Vector3.forward * 0.01f;
            });

            // Cue 444834: Steal
            cam.transform.position = courtyard + Vector3.back * 3.5f + Vector3.up * 1.5f;
            cam.transform.LookAt(courtyard + Vector3.up * 1.2f);
            yield return CaptureCue(cam, 444834, "Steal", 24, f => {
                if (ninjaMotor) {
                    float x = (f - 12) * 0.2f;
                    ninjaMotor.Teleport(courtyard + Vector3.right * x, Quaternion.LookRotation(Vector3.right));
                    if (f == 6 && ninjaEmotes) ninjaEmotes.TryPlay(CastleBotEmotes.Emote.Salute, true);
                }
            });

            // Cue 533137: Whoosh
            yield return CaptureCue(cam, 533137, "Whoosh", 24, f => {
                if (ninjaMotor) {
                    float z = (f - 12) * 0.3f;
                    ninjaMotor.Teleport(courtyard + Vector3.forward * z, Quaternion.LookRotation(Vector3.forward));
                }
            });

            // Cue 874146: Hum
            cam.transform.position = courtyard + Vector3.back * 2.5f + Vector3.up * 1.4f;
            cam.transform.LookAt(courtyard + Vector3.up * 1.2f);
            yield return CaptureCue(cam, 874146, "Hum", 24, f => {
                cam.transform.position += Vector3.forward * 0.015f;
            });

            // Cue 930736: Wind
            cam.transform.position = courtyard + new Vector3(-8f, 6f, -8f);
            cam.transform.LookAt(courtyard + Vector3.up * 2f);
            yield return CaptureCue(cam, 930736, "Wind", 24, f => {
                cam.transform.position += Vector3.right * 0.03f;
            });

            // Cue 978805: Gong
            cam.transform.position = courtyard + Vector3.back * 5f + Vector3.up * 2.5f;
            cam.transform.LookAt(courtyard + Vector3.up * 2.2f);
            yield return CaptureCue(cam, 978805, "Gong", 24, f => {
                if (f == 6 && ninjaEmotes) ninjaEmotes.TryPlay(CastleBotEmotes.Emote.Bow, true);
            });

            Object.DestroyImmediate(cam.gameObject);
            session.SurvivorBots.Clear();
            Log("Ninja Temple scene captures complete.");
        }

        static IEnumerator RunBatch3LobbyCaptures(IslandLobby lobby)
        {
            Log("Lobby.unity loaded for Batch 3 (Marina Ambience & Music)...");
            AudioListener.volume = 0;
            lobby.Player.Place(new Vector3(0, 0.1f, -8f), Quaternion.identity);
            lobby.Player.InputSuspended = true;
            yield return new WaitForSecondsRealtime(0.5f);

            var camObj = new GameObject("Batch3_Lobby_Camera");
            var cam = camObj.AddComponent<Camera>();
            cam.CopyFrom(lobby.ActiveCamera);
            cam.enabled = false;

            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;

            Log("--- Commencing Home Menu & Marina Ambience captures (3 cues) ---");

            // Cue 710: Home menu / Shorebreak
            cam.transform.position = new Vector3(0f, 3f, -14f);
            cam.transform.LookAt(new Vector3(0f, 1f, 10f));
            yield return CaptureCue(cam, 710, "Home menu / Shorebreak", 24, f => {
                cam.transform.position += Vector3.forward * 0.03f;
            });

            // Cue 711: Lobby / Ready Set Splash
            cam.transform.position = new Vector3(0f, 1.4f, -4.5f);
            cam.transform.LookAt(new Vector3(0f, 1.2f, -2.5f));
            yield return CaptureCue(cam, 711, "Lobby / Ready Set Splash", 24, f => {
                cam.transform.position += Vector3.back * 0.02f;
            });

            // Cue 712: Marina gull calls
            cam.transform.position = new Vector3(-12f, 4.5f, -8f);
            cam.transform.LookAt(new Vector3(4f, 2f, 8f));
            yield return CaptureCue(cam, 712, "Marina gull calls", 24, f => {
                cam.transform.position += Vector3.right * 0.04f;
            });

            Object.DestroyImmediate(cam.gameObject);
            Log("Lobby.unity Batch 3 captures complete.");
            EditorApplication.isPlaying = false;
        }

        static IEnumerator RunBatch5WildlifeCaptures(CastlePlaySession session)
        {
            Log("Game.unity session loaded for Batch 5 Wildlife. Setting up match rules...");
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
            if (session.Hand != null) session.Hand.InputSuspended = true;

            var world = Services.World;
            float waterY = world != null ? world.WaterHeight(world.Center) : 0f;

            // Dedicated camera
            var camObj = new GameObject("Batch5_Capture_Camera");
            var cam = camObj.AddComponent<Camera>();
            cam.CopyFrom(session.ActiveCamera);
            cam.enabled = false;

            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;

            // Find top castle platform block
            var blocksWithCollider = world.Blocks.Where(b => b && b.GetComponent<Collider>()).ToList();
            var castlePlatform = blocksWithCollider
                .Select(b => b.GetComponent<Collider>().bounds)
                .OrderByDescending(b => b.max.y).First();
            Vector3 platformTop = new Vector3(castlePlatform.center.x, castlePlatform.max.y + 0.05f, castlePlatform.center.z);

            // ========================================================
            // SECTION A: JAGUAR FOLEY (9 CUES: 73..81)
            // ========================================================
            Log("--- Commencing Jaguar Foley captures (9 cues) ---");

            GameObject bot = session.SurvivorBots.SpawnOne();
            if (!bot) bot = session.Player.gameObject;
            var botMotor = bot.GetComponent<CastleSurvivor>();
            var botBrain = bot.GetComponent<CastleBotBrain>();
            if (botBrain) botBrain.enabled = false;
            if (botMotor) botMotor.InputSuspended = true;

            var rig = bot.GetComponentInChildren<CandyPlayerRig>();
            CandyJaguarAppearance app = bot.GetComponent<CandyJaguarAppearance>();
            if (!app && rig)
            {
                app = bot.AddComponent<CandyJaguarAppearance>();
                app.Apply(rig, AvatarAccessory.PantherHead | AvatarAccessory.PantherClaws | AvatarAccessory.PantherTail);
            }
            else if (app && rig)
            {
                app.Apply(rig, AvatarAccessory.PantherHead | AvatarAccessory.PantherClaws | AvatarAccessory.PantherTail);
            }

            if (botMotor) botMotor.Teleport(platformTop, Quaternion.LookRotation(Vector3.back));
            yield return new WaitForSecondsRealtime(0.5f);

            // Camera framed on Jaguar
            cam.transform.position = platformTop + Vector3.back * 2.0f + Vector3.up * 1.25f;
            cam.transform.LookAt(platformTop + Vector3.up * 0.95f);

            // 73: Jaguar Idle
            yield return CaptureCue(cam, 73, "Jaguar Idle", 24, f => {
                if (f == 6) CandyAudioEngine.Instance?.PlayCue(CandyJaguarSounds.DefaultCue(CandySound.JaguarIdle), bot.transform.position, 1f, bot.GetInstanceID());
            });

            // 74: Jaguar Chuff
            yield return CaptureCue(cam, 74, "Jaguar Chuff", 24, f => {
                if (f == 6) CandyAudioEngine.Instance?.PlayCue(CandyJaguarSounds.DefaultCue(CandySound.JaguarChuff), bot.transform.position, 1f, bot.GetInstanceID());
            });

            // 75: Jaguar Growl
            yield return CaptureCue(cam, 75, "Jaguar Growl", 24, f => {
                if (f == 6) CandyAudioEngine.Instance?.PlayCue(CandyJaguarSounds.DefaultCue(CandySound.JaguarGrowl), bot.transform.position, 1f, bot.GetInstanceID());
            });

            // 76: Jaguar Alert
            yield return CaptureCue(cam, 76, "Jaguar Alert", 24, f => {
                if (f == 6) CandyAudioEngine.Instance?.PlayCue(CandyJaguarSounds.DefaultCue(CandySound.JaguarAlert), bot.transform.position, 1f, bot.GetInstanceID());
            });

            // 77: Jaguar Attack
            yield return CaptureCue(cam, 77, "Jaguar Attack", 24, f => {
                if (f == 6) CandyAudioEngine.Instance?.PlayCue(CandyJaguarSounds.DefaultCue(CandySound.JaguarAttack), bot.transform.position, 1f, bot.GetInstanceID());
                if (f >= 6 && f <= 12 && botMotor) botMotor.Teleport(platformTop + Vector3.back * ((f - 6) * 0.08f), Quaternion.LookRotation(Vector3.back));
            });

            // 78: Jaguar Hurt
            yield return CaptureCue(cam, 78, "Jaguar Hurt", 24, f => {
                if (f == 6) CandyAudioEngine.Instance?.PlayCue(CandyJaguarSounds.DefaultCue(CandySound.JaguarHurt), bot.transform.position, 1f, bot.GetInstanceID());
                if (f >= 6 && f <= 12 && botMotor) botMotor.Teleport(platformTop + Vector3.forward * ((f - 6) * 0.04f), Quaternion.LookRotation(Vector3.back));
            });

            // 79: Jaguar Death
            yield return CaptureCue(cam, 79, "Jaguar Death", 24, f => {
                if (f == 6) CandyAudioEngine.Instance?.PlayCue(CandyJaguarSounds.DefaultCue(CandySound.JaguarDeath), bot.transform.position, 1f, bot.GetInstanceID());
                if (f >= 6 && botMotor)
                {
                    float sink = Mathf.Min(0.6f, (f - 6) * 0.06f);
                    botMotor.Teleport(platformTop + Vector3.down * sink, Quaternion.LookRotation(Vector3.back));
                }
            });

            // Reset bot position
            if (botMotor) botMotor.Teleport(platformTop, Quaternion.LookRotation(Vector3.back));

            // 80: Jaguar Footfall
            cam.transform.position = platformTop + Vector3.back * 1.5f + Vector3.right * 0.4f + Vector3.up * 0.45f;
            cam.transform.LookAt(platformTop + Vector3.up * 0.2f);
            yield return CaptureCue(cam, 80, "Jaguar Footfall", 24, f => {
                if (f >= 6 && botMotor) botMotor.Teleport(platformTop + Vector3.back * ((f - 6) * 0.06f), Quaternion.LookRotation(Vector3.back));
                if (f == 6 || f == 14) CandyAudioEngine.Instance?.PlayCue(CandyJaguarSounds.DefaultCue(CandySound.JaguarFootfall), bot.transform.position, 1f, bot.GetInstanceID());
            });

            // 81: Jaguar Land
            cam.transform.position = platformTop + Vector3.right * 2.2f + Vector3.up * 0.6f;
            cam.transform.LookAt(platformTop + Vector3.up * 0.35f);
            yield return CaptureCue(cam, 81, "Jaguar Land", 24, f => {
                if (botMotor)
                {
                    float y = f < 6 ? Mathf.Lerp(1.2f, 0f, f / 6f) : 0f;
                    botMotor.Teleport(platformTop + Vector3.up * y, Quaternion.LookRotation(Vector3.back));
                }
                if (f == 6) CandyAudioEngine.Instance?.PlayCue(CandyJaguarSounds.DefaultCue(CandySound.JaguarLand), bot.transform.position, 1f, bot.GetInstanceID());
            });

            // Clean up bot
            if (bot != session.Player.gameObject) Object.DestroyImmediate(bot);

            // ========================================================
            // SECTION B: WILDLIFE CALLS (12 CUES: 900..911)
            // ========================================================
            Log("--- Commencing Wildlife Calls captures (12 cues) ---");

            var wildlife = session.GetComponent<IslandWildlife>();
            if (!wildlife) wildlife = session.gameObject.AddComponent<IslandWildlife>();

            string[] speciesList = CandyWildlifeAudio.Species;

            // Place survivor on island beach/ground for wildlife companion spawning
            Vector3 wildSpot = world.Center + Vector3.forward * 28f + Vector3.up * (waterY + 0.2f);
            session.Player.Place(wildSpot, Quaternion.LookRotation(Vector3.back));
            yield return new WaitForSecondsRealtime(0.5f);

            for (int i = 0; i < speciesList.Length; i++)
            {
                string sp = speciesList[i];
                int cueId = 900 + i;

                // Clean up previous animals
                foreach (var existing in session.GetComponentsInChildren<IslandAnimal>()) Object.DestroyImmediate(existing.gameObject);
                yield return null;

                GameObject animalObj = wildlife.Spawn(sp, true);
                if (!animalObj)
                {
                    animalObj = new GameObject("Island wildlife - " + sp);
                    animalObj.transform.SetParent(session.transform, true);
                    animalObj.transform.position = wildSpot + Vector3.forward * 1.5f;
                    var animalComp = animalObj.AddComponent<IslandAnimal>();
                    animalComp.Initialize(wildlife, sp, i + 1);
                }

                Vector3 animalPos = animalObj.transform.position;
                cam.transform.position = animalPos + Vector3.back * 1.4f + Vector3.up * 0.6f;
                cam.transform.LookAt(animalPos + Vector3.up * 0.25f);

                yield return CaptureCue(cam, cueId, sp, 24, f => {
                    if (f == 6 && animalObj)
                    {
                        CandyAudioEngine.Instance?.PlayCue(CandyWildlifeAudio.Cue(sp), animalObj.transform.position + Vector3.up * 0.2f, 1f, animalObj.GetInstanceID());
                    }
                });

                if (animalObj) Object.DestroyImmediate(animalObj);
            }

            Object.DestroyImmediate(camObj);
            Log("Batch 5 (Wildlife & Jaguar) captures successfully completed!");
            EditorApplication.isPlaying = false;
        }

        sealed class SimplePickupSource : IEquipmentPickupSource
        {
            public EquipmentKind Kind { get; set; }
            public Vector3 Position { get; set; }
            public float Duration => 60f;
        }
    }
}
