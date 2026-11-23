using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class BlockSpiresChecks
    {
        const string Key = "BlockSpiresChecks";
        static IEnumerator steps;
        static int failures;
        static string Report => ProjectPath.Report(Key + ".txt");
        [Serializable] sealed class Scenes { public SceneSetup[] values; }

        static BlockSpiresChecks()
        {
            EditorApplication.playModeStateChanged += OnState;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            if (scenes.Any(s => s.isLoaded && (string.IsNullOrEmpty(s.path) ||
                UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty)))
                throw new InvalidOperationException("Save the open scene before running the fixture.");
            if (!EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"))
                throw new InvalidOperationException("Disaster validator unavailable.");
            SessionState.SetString(Key + "Scenes", JsonUtility.ToJson(new Scenes { values = scenes }));
            File.WriteAllText(Report, "Block Spires, desktop Game.unity, staged gameplay inputs.\n");
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }

        static void OnState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                failures = 0;
                steps = Play();
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseBool(Key);
                EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Scenes>(SessionState.GetString(Key + "Scenes", "")).values);
                int exitCode = SessionState.GetBool(Key + "Passed", false) ? 0 : 2;
                if (SessionState.GetString("DJWarm.id", "") != "")
                {
                    Directory.CreateDirectory("Logs/Warm");
                    File.WriteAllText("Logs/Warm/exit.txt", exitCode.ToString());
                }
                else if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            }
        }

        static void Tick()
        {
            if (steps == null) return;
            try { if (steps.MoveNext()) return; }
            catch (Exception e) { Check(false, e.ToString()); }
            (steps as IDisposable)?.Dispose();
            steps = null;
            File.AppendAllText(Report, $"COMPLETE failures={failures}\n");
            SessionState.SetBool(Key + "Passed", failures == 0);
            EditorApplication.ExitPlaymode();
        }

        static void Check(bool pass, string note)
        {
            if (!pass) failures++;
            File.AppendAllText(Report, (pass ? "PASS " : "FAIL ") + note + "\n");
        }

        static IEnumerator Play()
        {
            MatchSetup.MapId = BlockSpiresLayout.Preset;
            MatchSetup.ModeId = "demo";
            MatchSetup.FromLobby = true;
            MatchSetup.StartLocalSession = false;
            MatchSetup.BotTarget = 3;
            MatchSetup.HandCount = 1;
            EditorSceneManager.LoadSceneInPlayMode(ProjectPath.Find("Game.unity"), new UnityEngine.SceneManagement.LoadSceneParameters());
            float deadline = Time.realtimeSinceStartup + 45;
            CastleGenerator map;
            while (!(Services.World is CastleGenerator current && current.settings.layoutPreset == BlockSpiresLayout.Preset &&
                   current.Bodies.Count > 0 && Services.Session?.Player is CastleSurvivor))
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Catalog map startup.");
                yield return null;
            }
            map = (CastleGenerator)Services.World;
            var session = Services.Session;
            var player = (CastleSurvivor)session.Player;
            var layout = BlockSpiresLayout.Build();
            var bodies = map.Bodies;
            Check(bodies.Count == layout.Count && bodies.All(b => !b.isKinematic && b.GetComponent<CastleBlock>().CanClimb),
                $"{bodies.Count} independent dynamic climbable blocks");
            float end = Time.fixedTime + 4;
            while (Time.fixedTime < end) yield return null;
            float drift = bodies.Select((b,i) => Vector3.Distance(b.position, map.transform.TransformPoint(layout[i].Position))).Max();
            Check(drift < .15f, $"Four physics seconds, worst drift {drift:F3} m");
            session.SetMenu(false);
            session.SurvivorBots.AutoFill = false;
            for (int i = 0; i < 2; i++) session.SurvivorBots.SpawnOne();
            if (session.HandBots.Count == 0) session.HandBots.SpawnOne();
            var wide = new GameObject("Block Spires review camera").AddComponent<Camera>();
            wide.enabled = false;
            wide.CopyFrom(session.ActiveCamera);
            wide.enabled = false;
            wide.transform.position = new Vector3(4.7f,28,-8.03f);
            wide.transform.LookAt(new Vector3(0,19,0));
            wide.fieldOfView = 93;
            Capture(wide, "wide");
            UnityEngine.Object.Destroy(wide.gameObject);

            player.Place(map.SafeSpawnPoint(), Quaternion.Euler(0,160,0));
            yield return null;
            player.viewCamera.transform.localRotation = Quaternion.Euler(32,0,0);
            Capture(player.viewCamera, "summit");
            // One placement at the foot of the lowest attached column. Every subsequent rise uses the motor.
            player.Teleport(new Vector3(.5f * BlockSpiresLayout.Pitch, .15f, 3.5f * BlockSpiresLayout.Pitch + .5f + player.Capsule.radius + .04f),
                Quaternion.LookRotation(Vector3.back));
            player.viewCamera.transform.localRotation = Quaternion.identity;
            bool enabled = player.enabled;
            player.enabled = false;
            bool grabbed = false, pictured = false;
            try
            {
                foreach (float height in new[] {5f,12f,19f,26f})
                {
                    for (int i = 0; i < 800 && player.FootPosition.y < height - .12f; i++)
                    {
                        player.Simulate(Vector2.up, false, .02f, true);
                        grabbed |= player.Climbing;
                        if (i % 12 == 0) yield return null;
                        if (!pictured && player.Climbing && player.FootPosition.y > 8)
                        {
                            player.viewCamera.transform.localRotation = Quaternion.Euler(20,58,0);
                            Capture(player.viewCamera, "climbing");
                            player.viewCamera.transform.localRotation = Quaternion.identity;
                            pictured = true;
                        }
                    }
                    Check(player.FootPosition.y >= height - .12f, $"Climb to {height} m, feet {player.FootPosition}");
                    if (player.FootPosition.y < height - .12f) break;
                    for (int i = 0; i < 420; i++)
                    {
                        player.Simulate(Vector2.zero, false, .02f);
                        if (i % 24 == 0) yield return null;
                    }
                }
                Check(grabbed && pictured && player.Grounded && player.FootPosition.y >= 25.8f, "Motor reached the stepped summit");
            }
            finally { player.enabled = enabled; }
            if (!pictured)
            {
                // An independently staged grip view is still useful when the continuous route fails.
                player.Teleport(new Vector3(2.25f,17,-.52f), Quaternion.LookRotation(Vector3.left));
                for (int i = 0; i < 20 && !player.Climbing; i++) player.Simulate(Vector2.up,false,.02f,true);
                Check(player.Climbing, "Staged upper-wall grip");
                player.viewCamera.transform.localRotation = Quaternion.Euler(22,62,0);
                Capture(player.viewCamera, "climbing-staged");
            }
            int support = layout.FindIndex(p => p.Course == 1 && p.Position.x > 8);
            var upper = bodies[support + 1];
            float initialHeight = upper.position.y;
            bodies[support].GetComponent<CastleBlock>().Push(Vector3.right * 11, player.gameObject);
            end = Time.fixedTime + 2;
            while (Time.fixedTime < end) yield return null;
            Check(upper.position.y < initialHeight - .35f, $"Satellite after support push: drop {initialHeight - upper.position.y:F3} m");
            int revision = map.Revision;
            session.ResetMap();
            yield return null;
            Check(map.Revision > revision && map.Bodies.Count == layout.Count &&
                Vector3.Distance(map.Bodies[support].position,layout[support].Position) < .1f, "Session reset restores removed support");
            bool eliminated = false;
            void OnEliminated(GameObject who, GameObject source) { if (who == player.gameObject) eliminated = true; }
            GameEvents.Eliminated += OnEliminated;
            try
            {
                player.Place(new Vector3(-2,.08f,-4), Quaternion.identity);
                end = Time.fixedTime + 6;
                while (Time.fixedTime < end && (!eliminated || player.FootPosition.y < 1)) yield return null;
                Check(eliminated && player.FootPosition.y > 1, $"Sand elimination and respawn, feet {player.FootPosition}");
                eliminated = false;
                player.Place(new Vector3(14,map.oceanKillHeight - 1,0), Quaternion.identity);
                end = Time.fixedTime + 4;
                while (Time.fixedTime < end && (!eliminated || player.FootPosition.y < 1)) yield return null;
                Check(eliminated && player.FootPosition.y > 1, $"Ocean elimination and live respawn, feet {player.FootPosition}");
            }
            finally { GameEvents.Eliminated -= OnEliminated; }
        }

        static void Capture(Camera camera, string name)
        {
            var old = camera.targetTexture;
            var active = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1600,900,24);
            var texture = new Texture2D(1600,900,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0,0,1600,900),0,0);
                texture.Apply();
                File.WriteAllBytes(ProjectPath.Report("BlockSpires-" + name + ".png"),texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = old;
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(texture);
            }
        }
    }
}
