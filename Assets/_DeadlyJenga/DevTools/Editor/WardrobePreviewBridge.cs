using System;
using System.Collections.Generic;
using System.IO;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class WardrobePreviewBridge
    {
        const string SessionKey = "DeadlyJenga.WardrobePreviewBridge";
        const int PreviewLayer = 31;
        const int Size = 720;
        static readonly HashSet<AvatarSkin> Skins = new HashSet<AvatarSkin>
        {
            AvatarSkin.Default, AvatarSkin.Rabbit, AvatarSkin.Clownfish, AvatarSkin.CandyCorn
        };

        [Serializable]
        sealed class OrbitRequest
        {
            public string id;
            public string skin;
            public float yaw;
            public float pitch;
            public float zoom;
        }

        static string folder, lastId;
        static GameObject root;
        static CandyPlayerRig rig;
        static Camera camera;
        static RenderTexture target;
        static Texture2D pixels;
        static AvatarSkin? activeSkin;
        static readonly Vector3 Stage = new Vector3(2000, 1000, 2000);
        static readonly List<Renderer> Renderers = new List<Renderer>();
        static WardrobeFraming.GeometrySampler sampler;
        static Bounds bounds;

        static WardrobePreviewBridge()
        {
            EditorApplication.playModeStateChanged += OnState;
            EditorApplication.quitting += Release;
        }

        public static void Run()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Open the wardrobe viewer in its reserved batch editor.");
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-wardrobeBridgeFolder");
            if (index < 0 || index + 1 >= args.Length || !Directory.Exists(args[index + 1]))
                throw new InvalidOperationException("A local wardrobe bridge folder is required.");
            SessionState.SetString(SessionKey, Path.GetFullPath(args[index + 1]));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void OnState(PlayModeStateChange change)
        {
            string requested = SessionState.GetString(SessionKey, "");
            if (string.IsNullOrEmpty(requested)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                folder = requested;
                Application.runInBackground = true;
                EditorApplication.update += Tick;
                try { CreateStage(); }
                catch (Exception error)
                {
                    File.WriteAllText(Path.Combine(folder, "unity-error.txt"), error.ToString());
                    Debug.LogException(error);
                    EditorApplication.ExitPlaymode();
                }
            }
            else if (change == PlayModeStateChange.ExitingPlayMode) Release();
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseString(SessionKey);
                EditorApplication.Exit(File.Exists(Path.Combine(requested, "unity-error.txt")) ? 2 : 0);
            }
        }

        static void CreateStage()
        {
            var prefab = Resources.Load<CandyPlayerRig>("CandyPlayerVisual");
            if (!prefab) throw new InvalidOperationException("Production CandyPlayerVisual is missing.");
            root = new GameObject("Wardrobe preview");
            rig = Object.Instantiate(prefab, root.transform);
            rig.SetColor(0);

            var cameraRoot = new GameObject("Wardrobe preview camera", typeof(Camera));
            cameraRoot.transform.SetParent(root.transform);
            camera = cameraRoot.GetComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << PreviewLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.042f, .075f, .10f);
            camera.fieldOfView = 35;
            camera.nearClipPlane = .04f;
            camera.farClipPlane = 25;
            camera.GetUniversalAdditionalCameraData().allowXRRendering = false;

            target = new RenderTexture(Size, Size, 24);
            camera.targetTexture = target;
            pixels = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            sampler = new WardrobeFraming.GeometrySampler();
            AddLight(new Vector3(-2.2f, 2.8f, 2.4f), new Color(1f, .89f, .78f), 21);
            AddLight(new Vector3(2.4f, 1.6f, 1.9f), new Color(.66f, .84f, 1f), 11);
            AddLight(new Vector3(.4f, 2.4f, -1.9f), Color.white, 15);
            File.WriteAllText(Path.Combine(folder, "unity-ready.txt"), Application.unityVersion);
        }

        static void AddLight(Vector3 offset, Color color, float intensity)
        {
            var go = new GameObject("Wardrobe studio light", typeof(Light));
            go.transform.SetParent(root.transform);
            go.transform.position = Stage + offset;
            var light = go.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = 9;
            light.cullingMask = 1 << PreviewLayer;
        }

        static void Tick()
        {
            if (string.IsNullOrEmpty(folder)) return;
            if (File.Exists(Path.Combine(folder, "stop")))
            {
                EditorApplication.Exit(0);
                return;
            }
            if (!rig || !camera) return;
            string path = Path.Combine(folder, "orbit.json");
            if (!File.Exists(path)) return;

            OrbitRequest request;
            try { request = JsonUtility.FromJson<OrbitRequest>(File.ReadAllText(path)); }
            catch (IOException) { return; }
            catch (ArgumentException) { return; }
            if (request == null || request.id == lastId ||
                !Guid.TryParseExact(request.id, "N", out _) ||
                !Enum.TryParse(request.skin, out AvatarSkin skin) || !Skins.Contains(skin) ||
                !Finite(request.yaw) || !Finite(request.pitch) || !Finite(request.zoom)) return;
            lastId = request.id;

            try
            {
                Render(skin, request.yaw, request.pitch, request.zoom);
                string complete = Path.Combine(folder, "frame-" + request.id + ".png");
                string temporary = complete + ".tmp";
                File.WriteAllBytes(temporary, pixels.EncodeToPNG());
                File.Move(temporary, complete);
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(folder, "unity-error.txt"), error.ToString());
                Debug.LogException(error);
            }
        }

        static void Render(AvatarSkin skin, float yaw, float pitch, float zoom)
        {
            if (skin != activeSkin)
            {
                rig.SetAppearance(new AvatarAppearance { skin = skin });
                rig.ApplyFrame(new CandyAvatarFrame
                {
                    feet = Stage, head = Stage + Vector3.up * 1.6f,
                    headRotation = Quaternion.identity, scale = 1,
                    left = Hand(true), right = Hand(false)
                });
                Renderers.Clear();
                rig.GetComponentsInChildren(false, Renderers);
                foreach (var renderer in Renderers) renderer.gameObject.layer = PreviewLayer;
                sampler.Refresh(Renderers);
                var envelope = new Bounds(Stage + Vector3.up * .95f, new Vector3(.85f, 1.95f, .62f));
                if (sampler.TryBounds(Renderers, out bounds, out _)) bounds.Encapsulate(envelope);
                else bounds = envelope;
                activeSkin = skin;
            }

            Quaternion orbit = Quaternion.Euler(Mathf.Clamp(pitch, -55, 70), yaw, 0);
            Vector3 pivot = bounds.center;
            camera.transform.rotation = orbit;
            float fit = WardrobeFraming.RequiredDistance(bounds, pivot, orbit, camera.fieldOfView, 1,
                camera.nearClipPlane, out float near);
            camera.transform.position = pivot - orbit * Vector3.forward * Mathf.Max(near, fit * Mathf.Clamp(zoom, .55f, 2f));
            var previous = RenderTexture.active;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                pixels.Apply();
            }
            finally { RenderTexture.active = previous; }
        }

        static CandyHandPose Hand(bool left)
        {
            float side = left ? -1 : 1;
            return new CandyHandPose
            {
                position = Stage + new Vector3(side * .31f, .76f, .09f),
                rotation = Quaternion.Euler(0, side * 12, side * 85),
                tracked = true, skeletonTracked = true, skeleton = CandyHandSkeleton.Rest(left, .04f)
            };
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static void Release()
        {
            EditorApplication.update -= Tick;
            sampler?.Dispose();
            if (camera) camera.targetTexture = null;
            if (target) { target.Release(); Object.Destroy(target); }
            if (pixels) Object.Destroy(pixels);
            if (root) Object.Destroy(root);
            activeSkin = null;
            lastId = null;
            folder = null;
        }
    }
}
