using System;
using System.IO;
using System.Linq;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class ScreenshotThroughputFixture
    {
        const string Key = "ScreenshotThroughputFixture";
        static string output;
        static double deadline, nextCapture;
        static int stage;
        static Camera lobbyCamera;



        static ScreenshotThroughputFixture()
        {
            EditorApplication.playModeStateChanged += StateChanged;
        }

        public static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-djCaptureOut");
            string folder = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.GetFullPath("Logs/Validation/TimedScreenshots");
            Directory.CreateDirectory(folder);
            SessionState.SetString(Key + "Output", folder);
            SessionState.SetInt(Key, 1);
            EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));
            EditorApplication.EnterPlaymode();
        }

        static void StateChanged(PlayModeStateChange change)
        {
            int phase = SessionState.GetInt(Key, 0);
            if (phase == 0) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                output = SessionState.GetString(Key + "Output", "");
                deadline = EditorApplication.timeSinceStartup + 125;
                stage = 0;
                EditorApplication.update += Update;
            }
            if (change != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.update -= Update;
            if (phase == 2)
            {
                SessionState.SetInt(Key, 0);
                BotCombatChecks.RunScreenshot();
                return;
            }
            SessionState.SetInt(Key, 0);
            EditorApplication.Exit(phase == 3 ? 0 : 2);
        }

        static void Update()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new TimeoutException("Timed out waiting for gameplay state at stage " + stage);
                if (EditorApplication.timeSinceStartup < nextCapture) return;
                Lobby();

            }
            catch (Exception error)
            {
                File.AppendAllText(Path.Combine(output, "failure.txt"), error + Environment.NewLine);
                Debug.LogException(error);
                Stop(4);
            }
        }

        static void Lobby()
        {
            var lobby = Object.FindFirstObjectByType<IslandLobbyView>();
            var terminal = Object.FindFirstObjectByType<DeadlyJenga.CandyArt.Marina.MarinaScreen>();
            lobbyCamera = Services.Session?.ActiveCamera;
            if (!lobby || !terminal || !lobbyCamera || !(Services.Session is IslandLobby)) return;
            if (stage == 0)
            {
                if (!terminal.Visible) return;
                if (!terminal.Invoke("marina-maps")) throw new InvalidOperationException("Production Map control refused the action");
                stage = 1;
                nextCapture = EditorApplication.timeSinceStartup + .7;
                return;
            }
            var panel = lobby.transform.Find("MAP + MODE");
            if (!panel || !panel.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Production Map action did not open the live setup panel");
            Vector3 target = panel.position - Vector3.up * .1f;
            Capture("lobby-maps", lobbyCamera, panel.position - panel.forward * 3.25f, target, 52,
                "Production MarinaScreen.Invoke(marina-maps) opened active MAP + MODE panel. Scripted control and framing.");
            Stop(2);
        }

        static void Stop(int phase)
        {
            EditorApplication.update -= Update;
            SessionState.SetInt(Key, phase);
            EditorApplication.ExitPlaymode();
        }

        internal static void Capture(string name, Camera original, Vector3 eye, Vector3 target, float fov, string assertion)
        {
            output = SessionState.GetString(Key + "Output", "");
            const int width = 1920, height = 1080;
            var gameObject = new GameObject("Timed capture camera");
            var camera = gameObject.AddComponent<Camera>();
            camera.CopyFrom(original);
            camera.enabled = false;
            camera.rect = new Rect(0, 0, 1, 1);
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.fieldOfView = fov;
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            var render = RenderTexture.GetTemporary(width, height, 24);
            var previous = RenderTexture.active;
            try
            {
                Canvas.ForceUpdateCanvases();
                camera.targetTexture = render;
                camera.Render();
                RenderTexture.active = render;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                string png = Path.Combine(output, name + ".png");
                File.WriteAllBytes(png + ".tmp", image.EncodeToPNG());
                File.Move(png + ".tmp", png);
                File.WriteAllLines(png + ".ready.txt", new[] {
                    DateTime.UtcNow.ToString("o"), SceneManager.GetActiveScene().path, assertion,
                    "Production source 0c771335de525e6ecf69e7b78087645cd7406ced plus isolated Editor-only fixture",
                    "1920x1080; dedicated camera; eye=" + eye + "; target=" + target + "; FOV=" + fov,
                    "frame=" + Time.frameCount + "; application focused=" + Application.isFocused
                });
                Debug.Log("PASS rendered " + png);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(render);
                Object.Destroy(image);
                Object.Destroy(gameObject);
            }
        }
    }
}
