using System;
using System.Collections.Generic;
using System.IO;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
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
            public string environment;
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
        static bool castleReady;
        static Vector3 castleStage;
        static Quaternion castleFacing;
        static Light[] castleLights;
        static SkinWeights? originalSkinWeights;
        static readonly List<Light> StudioLights = new List<Light>();
        static int castleMask;
        static bool castlePostProcessing;
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
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
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
                AudioListener.volume = 0;
                MatchSetup.FromLobby = false;
                MatchSetup.StartLocalSession = false;
                MatchSetup.MapId = "castle";
                MatchSetup.ModeId = "demo";
                MatchSetup.BotTarget = 0;
                MatchSetup.HandCount = 0;
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
            originalSkinWeights = QualitySettings.skinWeights;
            QualitySettings.skinWeights = SkinWeights.FourBones;
            root = new GameObject("Wardrobe preview");
            rig = Object.Instantiate(prefab, root.transform);
            rig.SetColor(0);
            if (rig.TryGetComponent<CandyAvatarDetail>(out var detail)) detail.enabled = false;

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
            go.transform.localPosition = offset;
            var light = go.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = 9;
            light.cullingMask = 1 << PreviewLayer;
            StudioLights.Add(light);
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
                (request.environment != "studio" && request.environment != "castle") ||
                !Finite(request.yaw) || !Finite(request.pitch) || !Finite(request.zoom)) return;
            try
            {
                bool castle = request.environment == "castle";
                if (!castleReady && !TryPrepareCastle()) return;
                lastId = request.id;
                Render(skin, castle, request.yaw, request.pitch, request.zoom);
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

        static bool TryPrepareCastle()
        {
            if (!(Services.Session is CastlePlaySession session) || !NetworkActors.Alive(session.Player) ||
                !session.ActiveCamera || Services.World == null || Services.World.Blocks.Count == 0) return false;

            var world = Services.World;
            Physics.SyncTransforms();
            float distance = Mathf.Max(7f, world.Radius + 4f);
            var direction = Vector3.forward;
            for (int side = 0; side < 4; side++)
            {
                direction = Quaternion.Euler(0, side * 90, 0) * Vector3.forward;
                var point = world.Center + direction * distance;
                point.y = world.TopHeight + 12f;
                if (!Physics.Raycast(point, Vector3.down, out var ground, point.y - world.WaterHeight(point) + 1f,
                        1 << Layers.Surface, QueryTriggerInteraction.Ignore)) continue;
                castleStage = ground.point + Vector3.up * .035f;
                castleFacing = Quaternion.LookRotation(direction);
                castleReady = true;
                break;
            }
            if (!castleReady)
            {
                castleStage = world.SafeSpawnPoint();
                if (Physics.Raycast(castleStage + Vector3.up * .8f, Vector3.down, out var floor, 2.8f,
                        (1 << Layers.Block) | (1 << Layers.Surface), QueryTriggerInteraction.Ignore))
                    castleStage = floor.point + Vector3.up * .015f;
                castleFacing = Quaternion.identity;
                castleReady = true;
            }

            root.transform.SetPositionAndRotation(castleStage, castleFacing);
            var gameCamera = session.ActiveCamera;
            var cameraData = gameCamera.GetUniversalAdditionalCameraData();
            castleMask = (gameCamera.cullingMask | (1 << PreviewLayer)) & ~(1 << 5);
            castlePostProcessing = cameraData.renderPostProcessing;
            camera.GetUniversalAdditionalCameraData().volumeLayerMask = cameraData.volumeLayerMask;
            HideActor(session.Player);
            HideActor(session.Hand);
            if (session.TryGetComponent<CandyEquipmentView>(out var equipment)) Object.Destroy(equipment);
            if (session.SurvivorBots != null)
            {
                session.SurvivorBots.AutoFill = false;
                session.SurvivorBots.Running = false;
                session.SurvivorBots.Clear();
            }
            if (session.HandBots != null)
            {
                session.HandBots.Running = false;
                session.HandBots.Clear();
            }
            var lights = new List<Light>();
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.enabled && !StudioLights.Contains(light)) lights.Add(light);
            castleLights = lights.ToArray();
            File.WriteAllText(Path.Combine(folder, "castle-ready.txt"),
                $"Map {MatchSetup.MapId}; {world.GetType().Name}; {world.Blocks.Count} blocks; " +
                $"stage {castleStage:F3}; sky {RenderSettings.skybox?.name}; {castleLights.Length} scene lights");
            return true;
        }

        static void HideActor(IPlayableCharacter actor)
        {
            if (!NetworkActors.Alive(actor)) return;
            actor.InputSuspended = true;
            foreach (var renderer in actor.gameObject.GetComponentsInChildren<Renderer>(true))
                renderer.forceRenderingOff = true;
        }

        static void Render(AvatarSkin skin, bool castle, float yaw, float pitch, float zoom)
        {
            Vector3 stage = castleStage;
            Quaternion facing = castleFacing;
            if (skin != activeSkin)
            {
                rig.SetAppearance(new AvatarAppearance { skin = skin });
                rig.ApplyFrame(new CandyAvatarFrame
                {
                    feet = stage, head = stage + Vector3.up * 1.6f,
                    headRotation = facing, scale = 1,
                    left = Hand(stage, facing, true), right = Hand(stage, facing, false)
                });
                Renderers.Clear();
                rig.GetComponentsInChildren(false, Renderers);
                foreach (var renderer in Renderers) renderer.gameObject.layer = PreviewLayer;
                sampler.Refresh(Renderers);
                var envelope = new Bounds(stage + Vector3.up * .95f, new Vector3(.85f, 1.95f, .62f));
                if (sampler.TryBounds(Renderers, out bounds, out _)) bounds.Encapsulate(envelope);
                else bounds = envelope;
                activeSkin = skin;
            }

            Quaternion orbit = facing * Quaternion.Euler(Mathf.Clamp(pitch, castle ? -6 : -55, 70), yaw, 0);
            camera.cullingMask = castle ? castleMask : 1 << PreviewLayer;
            camera.clearFlags = castle ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            camera.farClipPlane = castle ? 1500 : 25;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = castle && castlePostProcessing;
            foreach (var light in StudioLights) light.enabled = !castle;
            Vector3 pivot = bounds.center;
            camera.transform.rotation = orbit;
            float fit = WardrobeFraming.RequiredDistance(bounds, pivot, orbit, camera.fieldOfView, 1,
                camera.nearClipPlane, out float near);
            Vector3 back = orbit * Vector3.back;
            float distance = Mathf.Max(near, fit * Mathf.Clamp(zoom, .55f, 2f));
            if (castle && Physics.SphereCast(pivot, .12f, back, out var barrier, distance,
                    (1 << Layers.Block) | (1 << Layers.Surface), QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(near, barrier.distance - .06f);
            camera.transform.position = pivot + back * distance;
            var previous = RenderTexture.active;
            bool fog = RenderSettings.fog;
            if (!castle)
            {
                RenderSettings.fog = false;
                if (castleLights != null)
                    foreach (var light in castleLights) if (light) light.enabled = false;
            }
            try
            {
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                pixels.Apply();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderSettings.fog = fog;
                if (!castle && castleLights != null)
                    foreach (var light in castleLights) if (light) light.enabled = true;
            }
        }

        static CandyHandPose Hand(Vector3 stage, Quaternion facing, bool left)
        {
            float side = left ? -1 : 1;
            return new CandyHandPose
            {
                position = stage + facing * new Vector3(side * .31f, .76f, .09f),
                rotation = facing * Quaternion.Euler(0, side * 12, side * 85),
                tracked = true, skeletonTracked = true, skeleton = CandyHandSkeleton.Rest(left, .04f)
            };
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static void Release()
        {
            EditorApplication.update -= Tick;
            sampler?.Dispose();
            if (originalSkinWeights.HasValue) QualitySettings.skinWeights = originalSkinWeights.Value;
            originalSkinWeights = null;
            if (camera) camera.targetTexture = null;
            if (target) { target.Release(); Object.Destroy(target); }
            if (pixels) Object.Destroy(pixels);
            if (root) Object.Destroy(root);
            activeSkin = null;
            castleReady = false;
            castleLights = null;
            StudioLights.Clear();
            lastId = null;
            folder = null;
        }
    }
}
