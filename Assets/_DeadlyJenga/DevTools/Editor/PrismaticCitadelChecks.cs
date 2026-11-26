using System;
using System.IO;
using System.Linq;
using DeadlyJenga.CandyArt;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class PrismaticCitadelChecks
    {
        const string Key = "PrismaticCitadelChecks";
        const string MapId = "mode-prismatic-citadel";
        const string Preset = "prismatic-citadel";
        static string ReportPath => ProjectPath.Report("PrismaticCitadel.txt");
        static CastlePlaySession session;
        static CastleGenerator map;
        static CastleSurvivor walker;
        static Driver driver;
        static Vector3[] homes;
        static Rigidbody[] bearings;
        static Rigidbody span;
        static Vector3[] bearingStarts;
        static Vector3 spanStart;
        static int phase, waypoint, failed, errors;
        static float phaseStart;
        static double deadline;
        static bool attemptedApply;
        static readonly float[] Approach = { -51.5f, -47f, -43.9f, -40.8f, -37.7f, -34.6f, -31.5f };
        static readonly float[] UpperRoute = { -48f, -45f, -42f, -39f };
        static readonly float[] UpperRails = { 999f, 999f, 999f };

        sealed class Driver : ICastleSurvivorDriver
        {
            public Vector3 Destination;
            public float JumpAtZ = float.PositiveInfinity;

            public CastleBotCommand ReadCommand(CastleSurvivor motor, float dt)
            {
                var delta = Vector3.ProjectOnPlane(Destination - motor.transform.position, Vector3.up);
                bool jump = motor.StandingBody && motor.transform.position.z >= JumpAtZ;
                if (jump) JumpAtZ = float.PositiveInfinity;
                return new CastleBotCommand { move = delta.normalized * Mathf.Clamp01(delta.magnitude * 3f), look = delta, jump = jump };
            }
        }

        static PrismaticCitadelChecks()
        {
            EditorApplication.playModeStateChanged += PlayStateChanged;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Finish the current Play Mode session first.");
            for (int index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("A scene has unsaved changes.");

            SessionState.SetString(Key + ".Scenes", EncodeScenes(EditorSceneManager.GetSceneManagerSetup()));
            SessionState.SetFloat(Key + ".MaxDelta", Time.maximumDeltaTime);
            SessionState.SetBool(Key + ".FromLobby", MatchSetup.FromLobby);
            SessionState.SetBool(Key + ".LocalSession", MatchSetup.StartLocalSession);
            SessionState.SetString(Key + ".Map", MatchSetup.MapId ?? "");
            SessionState.SetString(Key + ".Mode", MatchSetup.ModeId ?? "");
            SessionState.SetInt(Key + ".Bots", MatchSetup.BotTarget);
            SessionState.SetInt(Key + ".Hands", MatchSetup.HandCount);
            SessionState.SetBool(Key + ".Active", true);
            SessionState.SetBool(Key + ".Finishing", false);
            SessionState.SetInt(Key + ".Exit", 1);
            phase = waypoint = failed = errors = 0;
            attemptedApply = false;
            File.WriteAllText(ReportPath, "Prismatic Citadel | Game.unity | solo Demo | zero bots\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            ConfigureSelection();
            EditorApplication.EnterPlaymode();
        }

        [Serializable]
        sealed class SceneState { public string[] paths; public bool[] loaded; public bool[] active; }

        static string EncodeScenes(SceneSetup[] scenes) => JsonUtility.ToJson(new SceneState
        {
            paths = scenes.Select(scene => scene.path).ToArray(),
            loaded = scenes.Select(scene => scene.isLoaded).ToArray(),
            active = scenes.Select(scene => scene.isActive).ToArray()
        });

        static void ConfigureSelection()
        {
            MatchSetup.FromLobby = true;
            MatchSetup.StartLocalSession = false;
            MatchSetup.MapId = MapId;
            MatchSetup.ModeId = "demo";
            MatchSetup.BotTarget = 1;
            MatchSetup.HandCount = 0;
        }

        static void PlayStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key + ".Active", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                ConfigureSelection();
                Application.logMessageReceived += OnLog;
                deadline = EditorApplication.timeSinceStartup + 90;
                Report($"Unity {Application.unityVersion}, scene {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, assembly {typeof(PrismaticCitadelChecks).Module.ModuleVersionId}");
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                Application.logMessageReceived -= OnLog;
                SessionState.SetBool(Key + ".Active", false);
                var saved = JsonUtility.FromJson<SceneState>(SessionState.GetString(Key + ".Scenes", ""));
                if (saved != null && saved.paths.Length > 0 && saved.paths.All(path => !string.IsNullOrEmpty(path)))
                    EditorSceneManager.RestoreSceneManagerSetup(saved.paths.Select((path, index) => new SceneSetup
                    {
                        path = path,
                        isActive = saved.active[index],
                        isLoaded = saved.loaded[index]
                    }).ToArray());
                Time.maximumDeltaTime = SessionState.GetFloat(Key + ".MaxDelta", Time.maximumDeltaTime);
                MatchSetup.FromLobby = SessionState.GetBool(Key + ".FromLobby", false);
                MatchSetup.StartLocalSession = SessionState.GetBool(Key + ".LocalSession", false);
                MatchSetup.MapId = SessionState.GetString(Key + ".Map", "");
                MatchSetup.ModeId = SessionState.GetString(Key + ".Mode", "");
                MatchSetup.BotTarget = SessionState.GetInt(Key + ".Bots", 1);
                MatchSetup.HandCount = SessionState.GetInt(Key + ".Hands", 0);
                int code = SessionState.GetInt(Key + ".Exit", 1);
                Report($"COMPLETE exit={code}; Edit Mode restored");
                if (Application.isBatchMode)
                {
                    const string warmExit = "Logs/Warm/exit.txt";
                    if (!string.IsNullOrEmpty(SessionState.GetString("DJWarm.id", ""))) File.WriteAllText(warmExit, code.ToString());
                    else EditorApplication.Exit(code);
                }
            }
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Assert && type != LogType.Exception && type != LogType.Error) return;
            errors++;
            Report($"ERROR {message}\n{stack}");
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || !SessionState.GetBool(Key + ".Active", false)
                || SessionState.GetBool(Key + ".Finishing", false) || deadline == 0) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new InvalidOperationException($"Phase {phase} timed out at simulation elapsed {Time.time - phaseStart:F2}s.");
                switch (phase)
                {
                    case 0: InitializeMap(); break;
                    case 1 when Time.time - phaseStart >= 3f: CheckSettlingAndCapture(); break;
                    case 2: WalkRoute(); break;
                    case 3 when Time.time - phaseStart >= 3f: CheckCollapseAndBeginDetour(); break;
                    case 4: WalkUpperRoute(); break;
                    case 5 when Time.time - phaseStart >= 2f: CheckReset(); break;
                }
            }
            catch (Exception exception)
            {
                Check(false, exception.ToString());
                Finish();
            }
        }

        static void InitializeMap()
        {
            session = Services.Session as CastlePlaySession;
            walker = session ? session.Survivor() : null;
            if (!session || !walker || !session.ActiveCamera) return;
            CheckCatalog();
            map = Services.World as CastleGenerator;
            if (!map || map.settings.layoutPreset != Preset)
            {
                if (attemptedApply)
                    throw new InvalidOperationException($"Catalog loaded unexpected layout {map?.settings.layoutPreset}.");
                attemptedApply = true;
                MatchSetup.ApplyMap();
                Report("Applied the catalog selection after the editor entered Play Mode.");
                return;
            }
            if (map.Bodies.Count == 0) return;

            session.SurvivorBots?.Clear();
            session.HandBots?.Clear();
            homes = map.Bodies.Select(body => body.position).ToArray();
            Check(session.HasWorldAuthority && !session.Network, "Solo world authority");
            Check(map.SaveLayout().Contains(Preset), $"Serialized layout contains {Preset}");
            Check(homes.Length == CastleLayout.Build(map.settings).Count, $"{homes.Length} live bodies match the authored layout; bounds {map.PlayableBounds.size}");
            PrismaticCitadelReplicaChecks.Run(map, MatchSetup.SelectedMap.mapPrefab, Check);
            Next();
        }

        static void CheckCatalog()
        {
            if (MatchSetup.SelectedMap?.title != "Prismatic Citadel" || !MatchSetup.SelectedMap.mapPrefab)
                throw new InvalidOperationException($"Production catalog does not resolve {MapId}.");
        }

        static void CheckSettlingAndCapture()
        {
            float drift = Drift();
            Check(drift < .35f, $"Maximum unforced drift after 3 simulation seconds: {drift:F3} m");
            Shot("01-overview", new Vector3(0, 54, -74), new Vector3(0, 26.2f, 0), 58);
            Shot("02-entrance", new Vector3(0, 1.98f, -26), new Vector3(0, 4, -13), 65);
            Shot("03-gallery", new Vector3(-2, 7.68f, -21), new Vector3(3, 9.85f, -11), 65);
            Shot("04-bearing-before", new Vector3(-12, 9, -38), new Vector3(-2, 4, -21), 57);

            Check(!MatchSetup.SelectedMap.walkableIsland && !walker.HasWalkableIsland, "Original lethal sand rules remained active.");
            var start = Point(0, 1.65f, Approach[0]);
            walker.Place(start, Quaternion.identity);
            driver = new Driver { Destination = start };
            walker.externallyDriven = true;
            walker.InputDriver = driver;
            walker.InputSuspended = false;
            Next();
        }

        static void WalkRoute()
        {
            var goal = Point(0, 0, Approach[waypoint]);
            driver.Destination = goal;
            var flatOffset = Vector3.ProjectOnPlane(walker.transform.position - goal, Vector3.up);
            if (!walker.Alive || Time.time - phaseStart > 6f)
            {
                Check(false, $"Ground route waypoint {waypoint} blocked at {walker.transform.position}; alive={walker.Alive}, suspended={walker.InputSuspended}");
                StartCollapse();
                return;
            }
            if (flatOffset.magnitude > .45f) return;
            var standing = walker.StandingBody;
            if (walker.Sinking || !standing || !map.Bodies.Contains(standing) || standing.position.y > Point(0, 2.0f, 0).y)
                return;
            Report($"Ground route waypoint {waypoint} reached on {standing.name} at {walker.transform.position}");
            waypoint++;
            phaseStart = Time.time;
            if (waypoint != Approach.Length) return;
            Check(true, "Production survivor motor stood on breakable pavers through the front portal and all three ground galleries.");
            StartCollapse();
        }

        static void StartCollapse()
        {
            driver.Destination = walker.transform.position;
            var coordinates = new[] { Point(-8.0f, 9.025f, -43.5f), Point(8.0f, 9.025f, -43.5f) };
            bearings = coordinates.Select(FindBlock).ToArray();
            span = FindBlock(Point(0, 18.75f, -43.5f));
            bearingStarts = bearings.Select(body => body.position).ToArray();
            spanStart = span.position;
            Report($"Observing span {span.name} at {spanStart} after pushing its two left bearings.");
            for (int i = 0; i < bearings.Length; i++)
            {
                var bearing = bearings[i];
                if (bearing.GetComponent<IPushable>() is not { } pushable)
                    throw new InvalidOperationException("The selected bearing is not a production pushable block.");
                Report($"Pushing {bearing.name} at {bearingStarts[i]} through IPushable.");
                float strength = (bearing.GetComponent<IDamageable>()?.MaxHealth ?? 11f) + 1f;
                pushable.Push(Vector3.forward * (strength * 18f), walker.gameObject);
            }
            Next();
        }

        static Rigidbody FindBlock(Vector3 position)
        {
            var body = map.Bodies.OrderBy(candidate => (candidate.position - position).sqrMagnitude).First();
            if (Vector3.Distance(body.position, position) > .35f)
                throw new InvalidOperationException($"No settled target block near {position}; nearest was {body.name} at {body.position}.");
            return body;
        }

        static void CheckCollapseAndBeginDetour()
        {
            Shot("05-bearing-after", new Vector3(-12, 9, -38), new Vector3(-2, 4, -21), 57);
            for (int i = 0; i < bearings.Length; i++)
            {
                var bearing = bearings[i];
                float displacement = bearing ? Vector3.Distance(bearingStarts[i], bearing.position) : float.PositiveInfinity;
                Check(displacement > .75f, $"Bearing {i + 1} displaced {displacement:F2} m after the push.");
            }
            float spanMove = span ? spanStart.y - span.position.y : float.PositiveInfinity;
            Check(spanMove > .50f, $"Independent gallery span dropped {spanMove:F2} m after bearing failure.");
            var start = Point(-15.0f, 19.65f, -48.0f);
            walker.Place(start, Quaternion.identity);
            driver.Destination = start;
            waypoint = 0;
            Next();
        }

        static void WalkUpperRoute()
        {
            var goal = Point(-15.0f, 19.5f, UpperRoute[waypoint]);
            driver.Destination = goal;
            var position = walker.transform.position;
            var local = map.transform.InverseTransformPoint(position);
            if (!walker.Alive || local.y < 18.0f || Time.time - phaseStart > 7f)
            {
                Check(false, $"Post-collapse upper route waypoint {waypoint} blocked at {position}; alive={walker.Alive}, suspended={walker.InputSuspended}");
                Rebuild();
                return;
            }
            var offset = Vector3.ProjectOnPlane(position - goal, Vector3.up);
            if (offset.magnitude > .45f || !walker.StandingBody) return;
            Report($"Post-collapse upper route waypoint {waypoint} reached on {walker.StandingBody.name} at {position}");
            waypoint++;
            phaseStart = Time.time;
            if (waypoint < UpperRoute.Length)
            {
                driver.JumpAtZ = Point(0, 0, UpperRails[waypoint - 1] - 1f).z;
                return;
            }

            Check(true, "Production survivor motor traversed the surviving upper gallery from an elevated start after the front span failed.");
            Rebuild();
        }

        static void Rebuild()
        {
            driver.Destination = walker.transform.position;
            driver.JumpAtZ = float.PositiveInfinity;
            ((IRebuildable)map).Rebuild();
            Next();
        }

        static void CheckReset()
        {
            float drift = Drift();
            Check(drift < .35f, $"Production map rebuild restored {map.Bodies.Count} bodies; maximum reconstructed drift {drift:F3} m.");
            Finish();
        }

        static float Drift()
        {
            if (map.Bodies.Count != homes.Length) return float.PositiveInfinity;
            float maximum = 0;
            for (int i = 0; i < homes.Length; i++)
            {
                var body = map.Bodies[i];
                if (!body) return float.PositiveInfinity;
                maximum = Mathf.Max(maximum, Vector3.Distance(homes[i], body.position));
            }
            return maximum;
        }

        static void Shot(string name, Vector3 eye, Vector3 target, float fov)
        {
            const int width = 1448, height = 996;
            var rig = new GameObject("Prismatic Citadel review camera");
            var camera = rig.AddComponent<Camera>();
            var texture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.Default, RenderTextureReadWrite.Default, 4);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var batchers = Object.FindObjectsByType<CandyCrateBatchRenderer>(FindObjectsSortMode.None)
                .Where(batcher => batcher.enabled).ToArray();
            try
            {
                camera.CopyFrom(session.ActiveCamera);
                camera.enabled = false;
                camera.stereoTargetEye = StereoTargetEyeMask.None;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.nearClipPlane = .07f;
                camera.farClipPlane = Mathf.Max(1200f, camera.farClipPlane);
                camera.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));
                camera.fieldOfView = fov;
                camera.transform.position = map.transform.TransformPoint(eye);
                camera.transform.LookAt(map.transform.TransformPoint(target));
                camera.targetTexture = texture;
                foreach (var batcher in batchers) batcher.enabled = false;
                camera.Render();
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(ProjectPath.Report($"prismatic-citadel-{name}.png"), pixels.EncodeToPNG());
                Report($"Captured {name} from {eye} toward {target}; vertical FOV {fov}");
            }
            finally
            {
                foreach (var batcher in batchers) batcher.enabled = true;
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(texture);
                Object.Destroy(pixels);
                Object.Destroy(rig);
            }
        }

        static Vector3 Point(float x, float y, float z) => map.transform.TransformPoint(new Vector3(x, y, z));

        static void Next()
        {
            phase++;
            phaseStart = Time.time;
            deadline = EditorApplication.timeSinceStartup + 90;
        }

        static void Finish()
        {
            SessionState.SetBool(Key + ".Finishing", true);
            SessionState.SetInt(Key + ".Exit", failed == 0 && errors == 0 ? 0 : 1);
            Report($"Assertions failed: {failed}, runtime errors: {errors}");
            EditorApplication.ExitPlaymode();
        }

        static void Check(bool condition, string message)
        {
            if (!condition) failed++;
            Report((condition ? "PASS " : "FAIL ") + message);
        }

        static void Report(string message) => File.AppendAllText(ReportPath, message + "\n");
    }
}
