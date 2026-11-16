using System.IO;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype.Editor
{
    // Art tool: opens Underneath the Tree in a solo demo and saves pictures from fixed cameras,
    // so each change can be compared with the approved picture straight away.
    public static class UnderneathTreeLookdev
    {
        const string Pending = "UnderneathTreeLookdev.Pending";
        const string MapId = "workshop-under-the-tree";

        // The approved picture looks north across the rug from outside the south wall.
        public static readonly (string name, Vector3 eye, Vector3 target, float fov)[] Views =
        {
            ("hero", new Vector3(-77.2f, 105.8f, -367.3f), new Vector3(-77.2f, .5f, -43.1f), 26.2f),
            ("overhead", new Vector3(-74f, 330f, -100f), new Vector3(-74f, 0f, -99f), 40f),
            ("turned-around", new Vector3(-60f, 70f, 60f), new Vector3(-80f, 15f, -150f), 62f),
            ("from-window", new Vector3(70f, 60f, -120f), new Vector3(-90f, 15f, -105f), 62f),
            ("from-sofa", new Vector3(-235f, 55f, -110f), new Vector3(-74f, 15f, -110f), 62f),
            ("player-courtyard", new Vector3(-70f, 2.6f, -136f), new Vector3(-76f, 16f, -112f), 75f),
            ("player-front", new Vector3(-58f, 2.6f, -205f), new Vector3(-80f, 14f, -150f), 70f),
            ("player-bridge", new Vector3(-74f, 24.6f, -116f), new Vector3(-120f, 28f, -112f), 75f),
            ("player-tunnel", new Vector3(-128f, 3f, -160f), new Vector3(-146f, 12f, -128f), 70f),
            ("player-back", new Vector3(-80f, 2.6f, -20f), new Vector3(-74f, 14f, -80f), 72f),
            ("player-left", new Vector3(-192f, 2.6f, -112f), new Vector3(-150f, 16f, -112f), 72f),
            ("player-right", new Vector3(38f, 2.6f, -118f), new Vector3(-10f, 14f, -106f), 72f)
        };

        [MenuItem("Tools/Deadly Jenga/Underneath the Tree/Open Lookdev")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            SessionState.SetBool(Pending, true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SelectMap()
        {
            if (!SessionState.GetBool(Pending, false))
                return;
            SessionState.SetBool(Pending, false);
            MatchChoices.MapId = MapId;
            MatchChoices.ModeId = "demo";
            MatchChoices.BotTarget = 0;
            MatchChoices.HandCount = 0;
        }

        // Saves every fixed view into the folder. Returns how many pictures were written.
        public static int ShootAll(string folder, int width = 1600, int height = 900)
        {
            foreach (var view in Views)
                Shoot(Path.Combine(folder, view.name + ".png"), view.eye, view.target, view.fov, width, height);
            return Views.Length;
        }

        public static string Shoot(string path, Vector3 eye, Vector3 target, float fov, int width = 1600, int height = 900)
        {
            var camera = Services.Session?.Player?.ViewCamera;
            if (!camera)
                return "No player camera yet.";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var priorTexture = camera.targetTexture;
            var priorActive = RenderTexture.active;
            var priorPosition = camera.transform.position;
            var priorRotation = camera.transform.rotation;
            float priorFov = camera.fieldOfView, priorAspect = camera.aspect, priorNear = camera.nearClipPlane, priorFar = camera.farClipPlane;
            bool priorAsync = ShaderUtil.allowAsyncCompilation;
            var render = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.Default, RenderTextureReadWrite.Default, 4);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
                camera.targetTexture = render;
                camera.aspect = (float)width / height;
                camera.fieldOfView = fov;
                // A camera outside the room starts drawing past the south wall and its fireplace.
                camera.nearClipPlane = eye.z < -262f ? (-225f - eye.z) * 1.06f : .05f;
                camera.farClipPlane = 1500f;
                ShaderUtil.allowAsyncCompilation = false;
                camera.Render();
                camera.Render();
                RenderTexture.active = render;
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = priorAsync;
                camera.targetTexture = priorTexture;
                RenderTexture.active = priorActive;
                camera.transform.SetPositionAndRotation(priorPosition, priorRotation);
                camera.fieldOfView = priorFov;
                camera.aspect = priorAspect;
                camera.nearClipPlane = priorNear;
                camera.farClipPlane = priorFar;
                RenderTexture.ReleaseTemporary(render);
                Object.DestroyImmediate(image);
            }
            return path;
        }
    }
}
