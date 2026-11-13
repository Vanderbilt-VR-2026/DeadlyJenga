using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine.Animations.Rigging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Explicit preview scene for the Blender asset. Production scenes are never saved.
    [InitializeOnLoad]
    public static class TycoonCreatorPilot
    {
        const string Pending = "DeadlyJenga.TycoonCreatorPilot";
        static readonly Vector3 Start = new Vector3(0, 4, 0);
        static readonly Vector3 End = new Vector3(0, 0, 12);
        static IEnumerator run;
        static string folder;
        static RecorderController recorder;
        static RecorderControllerSettings recorderSettings;
        static MovieRecorderSettings movieSettings;

        [Serializable]
        sealed class SceneSetupSnapshot { public SceneState[] scenes; }

        [Serializable]
        sealed class SceneState
        {
            public string path;
            public bool isLoaded, isActive;
        }

        static TycoonCreatorPilot() => EditorApplication.playModeStateChanged += PlayState;

        [MenuItem("Tools/Deadly Jenga/Creator Toolkit/Record Tycoon Flume Pilot")]
        static void StartMoviePreview() => StartPreview(true);

        [MenuItem("Tools/Deadly Jenga/Creator Toolkit/Run Tycoon Flume Pilot")]
        public static void StartPreview() => StartPreview(false);

        public static void StartRecordedPreview() => StartPreview(true);

        static void StartPreview(bool recordMovie)
        {
            var sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                sceneSetup.Any(scene => scene.isLoaded && (string.IsNullOrEmpty(scene.path) ||
                    UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene.path).isDirty)) && !Application.isBatchMode)
                throw new InvalidOperationException("Save edited scenes and leave Play Mode first.");

            string output = Argument("-creatorOutput");
            if (string.IsNullOrEmpty(output))
                output = Path.Combine("Logs", "Validation", "TycoonPilot-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            folder = Path.GetFullPath(output);
            if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
                throw new IOException("Choose an empty output folder.");
            Directory.CreateDirectory(folder);
            SessionState.SetString(Pending + "scenes", JsonUtility.ToJson(new SceneSetupSnapshot
            {
                scenes = sceneSetup.Select(scene => new SceneState
                    { path = scene.path, isLoaded = scene.isLoaded, isActive = scene.isActive }).ToArray()
            }));
            SessionState.SetString(Pending, folder);
            SessionState.SetInt(Pending + "result", 1);
            SessionState.SetBool(Pending + "record", recordMovie);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index < args.Length - 1 ? args[index + 1] : null;
        }

        static void PlayState(PlayModeStateChange state)
        {
            folder = SessionState.GetString(Pending, "");
            if (folder.Length == 0) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                run = Pilot();
                EditorApplication.update += Advance;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Advance;
                SessionState.EraseString(Pending);
                StopMovie();
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Pending + "result", 1));
                else
                {
                    var previous = JsonUtility.FromJson<SceneSetupSnapshot>(SessionState.GetString(Pending + "scenes", ""));
                    if (previous?.scenes?.Length > 0 && previous.scenes.All(scene => !string.IsNullOrEmpty(scene.path)))
                        EditorSceneManager.RestoreSceneManagerSetup(previous.scenes.Select(scene => new SceneSetup
                            { path = scene.path, isLoaded = scene.isLoaded, isActive = scene.isActive }).ToArray());
                }
                SessionState.EraseString(Pending + "scenes");
            }
        }

        static void Advance()
        {
            try
            {
                if (run.MoveNext()) return;
                SessionState.SetInt(Pending + "result", 0);
            }
            catch (Exception failure)
            {
                File.WriteAllText(Path.Combine(folder, "failure.txt"), failure.ToString());
                Debug.LogException(failure);
            }
            EditorApplication.update -= Advance;
            (run as IDisposable)?.Dispose();
            StopMovie();
            EditorApplication.ExitPlaymode();
        }

        static IEnumerator Pilot()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("TycoonPilot.fbx"));
            if (!model) throw new InvalidOperationException("TycoonPilot.fbx did not import as a model.");
            var art = Object.Instantiate(model);
            var meshes = art.GetComponentsInChildren<MeshFilter>();
            if (meshes.Length != 5) throw new InvalidDataException("Expected five authored mesh parts.");

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit is unavailable.");
            foreach (var mesh in meshes)
            {
                Color color = mesh.name.Contains("terrace", StringComparison.OrdinalIgnoreCase) ? new Color(.95f, .045f, .16f) :
                    mesh.name.Contains("liner", StringComparison.OrdinalIgnoreCase) ? new Color(.025f, .82f, .96f) :
                    new Color(.41f, .91f, .035f);
                mesh.GetComponent<Renderer>().sharedMaterial = Candy(shader, color);
            }

            var terraceMesh = meshes.Single(mesh => mesh.name.Contains("terrace", StringComparison.OrdinalIgnoreCase));
            Vector3 terraceCenter = terraceMesh.GetComponent<Renderer>().bounds.center;
            if (terraceCenter.z < 0) art.transform.Rotate(0, 180, 0);
            terraceCenter = terraceMesh.GetComponent<Renderer>().bounds.center;
            if (Mathf.Abs(terraceCenter.z - 15) > .3f)
                throw new InvalidDataException("Imported terrace does not line up with the physics fixture.");

            Vector3 From(float t) => Vector3.Lerp(Start, End, t) + Vector3.right * (1.35f * Mathf.Sin(t * Mathf.PI * 2));
            for (int i = 0; i < 8; i++)
            {
                var a = From(i / 8f);
                var b = From((i + 1) / 8f);
                Vector3 center = (a + b) * .5f;
                var shape = new GameObject("Flume box " + i);
                shape.transform.SetPositionAndRotation(center - Vector3.up * .2f, Quaternion.LookRotation(b - a));
                shape.AddComponent<BoxCollider>().size = new Vector3(3.4f, .35f, Vector3.Distance(a, b) + .03f);
            }
            var terrace = new GameObject("Terrace box");
            terrace.transform.position = End + new Vector3(0, -.18f, 3);
            terrace.AddComponent<BoxCollider>().size = new Vector3(7.4f, .36f, 6);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 2.1f;
            sun.transform.rotation = Quaternion.Euler(48, -42, 0);
            RenderSettings.ambientLight = new Color(.58f, .64f, .74f);

            var camera = new GameObject("Capture").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.11f, .22f, .39f);
            camera.transform.position = new Vector3(12, 11, -4);
            camera.transform.LookAt(new Vector3(0, 1, 8));
            var brain = camera.gameObject.AddComponent<CinemachineBrain>();
            var framing = new GameObject("Cinemachine wide").AddComponent<CinemachineCamera>();
            framing.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            capsule.name = "Traversal probe";
            capsule.transform.position = Start + new Vector3(0, .45f, .2f);
            capsule.transform.localScale = Vector3.one * .46f;
            capsule.GetComponent<Renderer>().sharedMaterial = Candy(shader, new Color(1f, .74f, .06f));
            var body = capsule.AddComponent<Rigidbody>();
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = new Vector3(.55f, 0, 4.5f);
            bool rigPassed = CheckContactRig();
            bool recording = SessionState.GetBool(Pending + "record", false);
            if (recording) StartMovie();
            var frames = new List<float>(120);
            var path = new List<Vector3>(120);
            bool landed = false, fell = false;
            int sample = 0;
            Vector3 landing = default;
            var previousSimulation = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                for (int step = 0; step < 300 && !landed; step++)
                {
                    if (step % 3 == 0)
                    {
                        if (sample == 0 || sample == 30) Capture(camera, sample);
                        yield return null;
                        frames.Add(Time.unscaledDeltaTime * 1000);
                        path.Add(body.position);
                        sample++;
                    }
                    if (body.position.z > 0 && body.position.z < 12 && body.position.y > -.2f)
                    {
                        var center = From(body.position.z / 12f);
                        var velocity = body.linearVelocity;
                        velocity.x = Mathf.Clamp((center.x - body.position.x) * 5f, -4, 4);
                        body.linearVelocity = velocity;
                    }
                    Physics.Simulate(.02f);
                    landed = body.position.z > 12 && body.position.z < 17.5f &&
                        Mathf.Abs(body.position.x) < 3.7f && body.position.y > -.3f;
                    if (landed)
                    {
                        landing = body.position;
                        body.isKinematic = true;
                    }
                }

                // Isolate the release from adjacent fixture colliders.
                var support = new GameObject("Detached support");
                support.transform.position = new Vector3(5, 3, 10);
                support.AddComponent<BoxCollider>().size = new Vector3(1, 2, 1);
                var released = support.AddComponent<Rigidbody>();
                released.useGravity = true;
                Physics.SyncTransforms();
                float originalHeight = released.position.y;
                released.linearVelocity = Vector3.right * 3f;
                for (int step = 0; step < 100 && released.position.y > originalHeight - 1; step++)
                {
                    if (step % 3 == 0) yield return null;
                    Physics.Simulate(.02f);
                }
                fell = released.position.y <= originalHeight - 1;
            }
            finally
            {
                Physics.simulationMode = previousSimulation;
            }
            Capture(camera, 99);
            if (recording) StopMovie();
            var movie = new FileInfo(Path.Combine(folder, "tycoon-pilot.mp4"));
            bool movieSaved = !recording || movie.Exists && movie.Length > 0;
            File.WriteAllText(Path.Combine(folder, "probe.json"), JsonUtility.ToJson(new ProbeTrace
                { positions = path.ToArray(), frameMilliseconds = frames.ToArray() }, true));
            var sorted = frames.OrderBy(ms => ms).ToArray();
            var dimensions = art.GetComponentsInChildren<Renderer>().Select(renderer => renderer.bounds).ToArray();
            var bounds = dimensions.Aggregate((left, right) => { left.Encapsulate(right); return left; });
            File.WriteAllText(Path.Combine(folder, "report.txt"),
                $"Unity {Application.unityVersion}\nFive Blender meshes; {meshes.Sum(mesh => (int)mesh.sharedMesh.GetIndexCount(0) / 3)} triangles\n" +
                $"Imported bounds: {bounds.size.x:F2} x {bounds.size.y:F2} x {bounds.size.z:F2} m\n" +
                $"Animation Rigging reached contact target: {rigPassed}\nRecorder output present: {movieSaved} (enabled: {recording})\n" +
                $"Cinemachine live: {brain.ActiveVirtualCamera != null}\n" +
                $"Flume probe reached terrace: {landed}; position {(landed ? landing : body.position)}\nReleased support fell: {fell}\n" +
                $"Frames: {sorted.Length}; p95: {sorted[Mathf.Clamp(Mathf.CeilToInt(sorted.Length * .95f) - 1, 0, sorted.Length - 1)]:F2} ms (editor)\n");
            if (!landed || !fell || !rigPassed || !movieSaved || brain.ActiveVirtualCamera == null)
                throw new InvalidOperationException("Pilot acceptance failed. Inspect report.txt and captures.");
        }

        static bool CheckContactRig()
        {
            var arm = new GameObject("Contact rig fixture");
            var animator = arm.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var builder = arm.AddComponent<RigBuilder>();
            Transform Child(string name, Transform parent, Vector3 position)
            {
                var joint = new GameObject(name).transform;
                joint.SetParent(parent, false);
                joint.localPosition = position;
                return joint;
            }
            var shoulder = Child("Shoulder", arm.transform, Vector3.zero);
            var elbow = Child("Elbow", shoulder, new Vector3(.8f, .2f, 0));
            var hand = Child("Hand", elbow, new Vector3(.8f, -.2f, 0));
            var rig = Child("Contact", arm.transform, Vector3.zero).gameObject.AddComponent<Rig>();
            var constraint = rig.gameObject.AddComponent<TwoBoneIKConstraint>();
            var target = Child("Rail grip target", rig.transform, new Vector3(1, .45f, .3f));
            constraint.data.root = shoulder;
            constraint.data.mid = elbow;
            constraint.data.tip = hand;
            constraint.data.target = target;
            constraint.data.hint = Child("Elbow guide", rig.transform, new Vector3(.2f, .9f, -.3f));
            constraint.weight = 1;
            constraint.data.targetPositionWeight = 1;
            constraint.data.targetRotationWeight = 0;
            constraint.data.hintWeight = 1;
            rig.weight = 1;
            builder.layers.Add(new RigLayer(rig));
            bool built = builder.Build();
            if (built) builder.Evaluate(0);
            bool reached = built && Vector3.Distance(hand.position, target.position) < .08f;
            Object.Destroy(arm);
            return reached;
        }

        static void StartMovie()
        {
            recorderSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movieSettings.Enabled = true;
            movieSettings.CaptureAudio = false;
            movieSettings.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High
            };
            movieSettings.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1280, OutputHeight = 720 };
            movieSettings.OutputFile = Path.Combine(folder, "tycoon-pilot");
            recorderSettings.FrameRate = 24;
            recorderSettings.SetRecordModeToManual();
            recorderSettings.AddRecorderSettings(movieSettings);
            recorder = new RecorderController(recorderSettings);
            recorder.PrepareRecording();
            if (!recorder.StartRecording()) throw new InvalidOperationException("Unity Recorder could not start.");
        }

        static void StopMovie()
        {
            if (recorder == null) return;
            recorder.StopRecording();
            recorder = null;
            Object.DestroyImmediate(movieSettings);
            Object.DestroyImmediate(recorderSettings);
        }

        [Serializable]
        sealed class ProbeTrace { public Vector3[] positions; public float[] frameMilliseconds; }

        static Material Candy(Shader shader, Color color)
        {
            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .72f);
            return material;
        }

        static void Capture(Camera camera, int frame)
        {
            var target = RenderTexture.GetTemporary(960, 540, 24);
            var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                File.WriteAllBytes(Path.Combine(folder, $"frame-{frame:00}.png"), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(pixels);
            }
        }
    }
}
