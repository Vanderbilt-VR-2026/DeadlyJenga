using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyArt.Editor
{
    public static class TsunamiPreview
    {
        sealed class PreviewState : IWaveState
        {
            public WaveFrame Wave => new WaveFrame { active = true, center = Vector3.up * .08f, direction = Vector3.forward,
                width = 90, depth = 28, height = 18, waterHeight = .08f, remaining = 10 };
        }
        [MenuItem("Tools/Deadly Jenga/Capture Tsunami Preview")]
        public static void Capture()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            { Debug.LogWarning("Tsunami preview skipped: this Unity process has no graphics device. Use a graphics-enabled editor for visual review."); return; }
            var scene = EditorSceneManager.NewPreviewScene();
            var state = new PreviewState(); var oldWorld = Services.World;
            var previousSky = RenderSettings.skybox;
            RenderTexture target = null; Texture2D pixels = null;
            GameObject root = null;
            try
            {
                Services.World = null; Tracked.Waves.Add(state);
                root = new GameObject("Tsunami preview"); SceneManager.MoveGameObjectToScene(root, scene);
                var environment = root.AddComponent<CandyEnvironment>();
                CandyArtProfile profile = null;
                foreach (var guid in AssetDatabase.FindAssets("t:CandyArtProfile"))
                { profile = AssetDatabase.LoadAssetAtPath<CandyArtProfile>(AssetDatabase.GUIDToAssetPath(guid)); if (profile) break; }
                if (!profile) throw new Exception("Candy ocean profile missing.");
                environment.profile = profile;
                var water = new GameObject("Ocean"); water.transform.SetParent(root.transform, false);
                environment.waterRenderer = water.AddComponent<MeshRenderer>();
                water.AddComponent<CandyOceanSurface>().profile = profile;
                var sand = GameObject.CreatePrimitive(PrimitiveType.Cube); sand.transform.SetParent(root.transform, false);
                environment.sandRenderer = sand.GetComponent<Renderer>(); sand.SetActive(false);
                environment.Refresh();
                var visual = root.AddComponent<CandyTsunamiVisuals>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                typeof(CandyTsunamiVisuals).GetMethod("Awake", flags).Invoke(visual, null);
                typeof(CandyTsunamiVisuals).GetMethod("LateUpdate", flags).Invoke(visual, null);
                var waveObject = GameObject.Find("Visual - Tsunami");
                if (!waveObject) throw new Exception("Wave renderer was not created.");
                SceneManager.MoveGameObjectToScene(waveObject, scene);
                var lamp = new GameObject("Sun"); lamp.transform.SetParent(root.transform, false);
                var light = lamp.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.3f;
                lamp.transform.rotation = Quaternion.Euler(40, -35, 0);
                var cameraObject = new GameObject("Preview camera"); cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene;
                camera.transform.position = new Vector3(58, 25, 65); camera.transform.LookAt(new Vector3(0, 8, 0));
                camera.fieldOfView = 58; camera.farClipPlane = 2000; camera.clearFlags = CameraClearFlags.Skybox;
                RenderSettings.skybox = profile.sky;
                target = new RenderTexture(1280, 720, 24); camera.targetTexture = target;
                bool asynchronous = ShaderUtil.allowAsyncCompilation;
                try { ShaderUtil.allowAsyncCompilation = false; camera.Render(); }
                finally { ShaderUtil.allowAsyncCompilation = asynchronous; }
                var prior = RenderTexture.active;
                try { RenderTexture.active = target; pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply(); }
                finally { RenderTexture.active = prior; }
                Directory.CreateDirectory("Logs/Validation"); File.WriteAllBytes("Logs/Validation/Tsunami.png", pixels.EncodeToPNG());
                foreach (var renderer in waveObject.GetComponentsInChildren<Renderer>())
                    if (renderer.sharedMaterial && ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader)) throw new Exception("Tsunami shader has compilation errors.");
                Debug.Log("Tsunami preview captured to Logs/Validation/Tsunami.png.");
            }
            finally
            {
                Tracked.Waves.Remove(state); Services.World = oldWorld; RenderSettings.skybox = previousSky;
                if (root) UnityEngine.Object.DestroyImmediate(root);
                if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
                if (target) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
