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
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    Application.runInBackground = true;
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
                }
            };
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

        sealed class SimplePickupSource : IEquipmentPickupSource
        {
            public EquipmentKind Kind { get; set; }
            public Vector3 Position { get; set; }
            public float Duration => 60f;
        }
    }
}
