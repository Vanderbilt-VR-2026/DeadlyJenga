using System;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class TycoonReviewCapture
    {
        const string Key = "TycoonReviewCapture";


        static double captureAt;
        static double deadline;
        static string Output => Path.GetFullPath("Logs/TycoonReview");

        static TycoonReviewCapture()
        {
            EditorApplication.playModeStateChanged += PlayStateChanged;
        }

        public static void Run()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output,"report.txt"), "Tycoon visual review in Game.unity\n");
            SessionState.SetInt(Key+"Errors",0);
            SessionState.SetInt(Key+"Exit",1);
            MatchSetup.FromLobby = true;
            MatchSetup.StartLocalSession = false;
            MatchSetup.ModeId = "demo";
            MatchSetup.MapId = "mode-tycoon";
            MatchSetup.BotTarget = 1;
            MatchSetup.HandCount = 0;
            if (MatchSetup.SelectedMap?.mapPrefab == null)
                throw new InvalidOperationException("Tycoon did not resolve through the lobby catalog.");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            SessionState.SetBool(Key,true);
            Application.logMessageReceived += OnLog;
            EditorApplication.LockReloadAssemblies();
            EditorApplication.EnterPlaymode();
        }

        static void OnLog(string message,string trace,LogType type)
        {
            if(type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            SessionState.SetInt(Key+"Errors",SessionState.GetInt(Key+"Errors",0)+1);
            Report($"ERROR {message}\n{trace}");
        }

        static void PlayStateChanged(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false)) return;
            if(state == PlayModeStateChange.EnteredPlayMode)
            {
                Application.logMessageReceived -= OnLog;
                Application.logMessageReceived += OnLog;
                MatchSetup.FromLobby = true;
                MatchSetup.StartLocalSession = false;
                MatchSetup.ModeId = "demo";
                MatchSetup.MapId = "mode-tycoon";
                MatchSetup.BotTarget = 1;
                MatchSetup.HandCount = 0;
                // Review the authored resting layout before a physics pass.
                Time.timeScale = 0;
                captureAt = EditorApplication.timeSinceStartup + 1.5;
                deadline = captureAt + 30;
                EditorApplication.update += CaptureWhenReady;
            }
            else if(state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key,false);
                Time.timeScale = 1;
                EditorApplication.UnlockReloadAssemblies();
                Application.logMessageReceived -= OnLog;
                EditorApplication.update -= CaptureWhenReady;
                int exitCode = SessionState.GetInt(Key+"Exit",1);
                int loggedErrors = SessionState.GetInt(Key+"Errors",0);
                Report($"COMPLETE exit={exitCode}; errors={loggedErrors}");
                if(Application.isBatchMode) EditorApplication.Exit(loggedErrors == 0 ? exitCode : 1);
            }
        }

        static void CaptureWhenReady()
        {
            if(EditorApplication.timeSinceStartup < captureAt) return;
            var world = Services.World as CastleGenerator;
            if (Services.Session != null && world && world.settings.layoutPreset != TycoonLayout.Preset)
            {
                MatchSetup.ApplyMap();
                Report("Applied the selected catalog map after the solo session started.");
                captureAt = EditorApplication.timeSinceStartup + 1;
                return;
            }
            var camera = Services.Session?.ActiveCamera ?? Camera.main;
            if((!world || !camera || world.Bodies.Count == 0) && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= CaptureWhenReady;
            try
            {
                if(!world || !camera || world.settings.layoutPreset != TycoonLayout.Preset || world.Bodies.Count == 0)
                    throw new InvalidOperationException($"{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} did not start Tycoon. Map={MatchSetup.MapId}; world={world?.settings.layoutPreset}; bodies={world?.Bodies.Count}; camera={camera?.name}; fromLobby={MatchSetup.FromLobby}");
                Report($"Map={MatchSetup.MapId}; builder={world.name}; bodies={world.Bodies.Count}; bounds={world.PlayableBounds.size}; Unity={Application.unityVersion}");
                var settings = world.settings;
                var pieces = CastleLayout.Build(settings);
                if(pieces.Count != world.Bodies.Count || !world.SaveLayout().Contains(TycoonLayout.Preset))
                    throw new InvalidOperationException("Tycoon authored layout and runtime blocks differ.");
                if(!pieces.Any(p=>p.Section==CastleSection.Gold) || !pieces.Any(p=>p.Section==CastleSection.Aqua))
                    throw new InvalidOperationException("Tycoon chute palettes missing.");
                var validation = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="DeadlyJenga.World.Editor")
                    ?.GetType("DeadlyJenga.World.Hazards.Editor.HazardValidation")?.GetMethod("ValidateCatalog");
                if(validation == null) throw new InvalidOperationException("Disaster catalog validation is unavailable.");
                validation.Invoke(null,null);
                Capture(world,camera);
                SessionState.SetInt(Key+"Exit",0);
            }
            catch(Exception exception)
            {
                Report("FAIL "+exception);
            }
            finally
            {
                EditorApplication.ExitPlaymode();
            }
        }

        static void Capture(CastleGenerator world, Camera source)
        {
            var rig = new GameObject("Tycoon review camera");
            var camera = rig.AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.enabled = false;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = .07f;
            camera.farClipPlane = Mathf.Max(source.farClipPlane,1600f);
            camera.fieldOfView = 49;
            camera.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));
            var target = RenderTexture.GetTemporary(1920,1080,24,RenderTextureFormat.Default,RenderTextureReadWrite.Default,4);
            var pixels = new Texture2D(1920,1080,TextureFormat.RGB24,false);
            var previous = RenderTexture.active;
            var origin = world.transform.position;
            try
            {
                camera.targetTexture = target;
                void Shot(string name,Vector3 from,Vector3 at)
                {
                    camera.transform.position = origin+from;
                    camera.transform.LookAt(origin+at);
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);
                    pixels.Apply();
                    File.WriteAllBytes(Path.Combine(Output,name+".png"),pixels.EncodeToPNG());
                    Report("Captured "+name);
                }
                Shot("01-Tycoon-overview",new Vector3(5,102,-158),new Vector3(0,36,-7));
                Shot("02-Tycoon-rear",new Vector3(-122,104,138),new Vector3(0,35,0));
                Shot("03-Core-junction",new Vector3(1,19.7f,-16.5f),new Vector3(7,22,-3));
                Shot("04-West-flume",new Vector3(-27.5f,37.7f,14),new Vector3(-42,22,-7));
                Shot("05-East-terrace",new Vector3(39,19.7f,-39.5f),new Vector3(29,21,-25));
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(pixels);
                Object.Destroy(rig);
            }
        }

        static void Report(string line) => File.AppendAllText(Path.Combine(Output,"report.txt"),line+"\n");
    }
}
