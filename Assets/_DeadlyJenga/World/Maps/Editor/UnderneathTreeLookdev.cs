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
            ("overhead", new Vector3(-74f, 170f, -100f), new Vector3(-74f, 0f, -99f), 80f),
            ("turned-around", new Vector3(-60f, 70f, 60f), new Vector3(-80f, 15f, -150f), 62f),
            ("from-window", new Vector3(70f, 60f, -120f), new Vector3(-90f, 15f, -105f), 62f),
            ("from-sofa", new Vector3(-235f, 55f, -110f), new Vector3(-74f, 15f, -110f), 62f),
            ("player-courtyard", new Vector3(-81.6f, 3f, -153.7f), new Vector3(-112f, 19.5f, -110f), 75f),
            ("player-front", new Vector3(-93f, 3f, -194.5f), new Vector3(-74f, 15.7f, -129f), 70f),
            ("player-bridge", new Vector3(-89.2f, 25.6f, -115.7f), new Vector3(-150f, 36f, -115.7f), 75f),
            ("player-tunnel", new Vector3(-131f, 15.4f, -149.9f), new Vector3(-151.9f, 15.7f, -119.5f), 70f),
            ("player-back", new Vector3(-89.2f, 3f, -15f), new Vector3(-74f, 15.7f, -91f), 72f),
            ("player-left", new Vector3(-188f, 3f, -110f), new Vector3(-153.8f, 27f, -110f), 72f),
            ("player-right", new Vector3(40f, 3f, -117.6f), new Vector3(-24.6f, 23.3f, -92.9f), 72f)
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

        // Family room views, for the unattended capture job below.
        public static readonly (string name, Vector3 eye, Vector3 target, float fov)[] FamilyRoomViews =
        {
            ("room-tree-corner", new Vector3(-250f, 90f, -300f), new Vector3(-40f, 120f, -30f), 70f),
            ("room-sitting", new Vector3(-40f, 120f, -350f), new Vector3(-300f, 30f, -350f), 72f),
            ("room-stockings", new Vector3(-210f, 85f, -350f), new Vector3(-310f, 82f, -350f), 45f),
            ("room-kitchen", new Vector3(-200f, 80f, -110f), new Vector3(-540f, 60f, -110f), 55f),
            ("room-front-entry", new Vector3(-200f, 90f, -540f), new Vector3(-540f, 70f, -545f), 60f),
            ("room-piano-clock", new Vector3(-210f, 80f, -200f), new Vector3(-320f, 75f, 20f), 60f),
            ("room-photos", new Vector3(-200f, 140f, -340f), new Vector3(58f, 140f, -340f), 60f),
            ("room-pet-bed", new Vector3(-200f, 40f, -440f), new Vector3(-262f, 5f, -500f), 50f),
            ("room-south-window", new Vector3(-145f, 80f, -330f), new Vector3(-145f, 95f, -600f), 72f),
            ("room-overview", new Vector3(30f, 225f, -585f), new Vector3(-250f, 40f, -100f), 85f)
        };

        const string BatchKey = "UnderneathTreeLookdev.Batch";
        const string BatchStarted = "UnderneathTreeLookdev.BatchStarted";
        const string BatchPlaying = "UnderneathTreeLookdev.BatchPlaying";
        public const string BatchFolder = "Logs/TreeLookdev/batch";

        // Unattended capture for a coordinator job (-executeMethod): opens the map in Play Mode,
        // waits for it to build, saves every family room view to Logs/TreeLookdev/batch, then quits.
        // Exit code 0 = pictures written; 2 = timed out.
        public static void BatchCaptureFamilyRoom()
        {
            SessionState.SetBool(BatchKey, true);
            SessionState.SetFloat(BatchStarted, (float)EditorApplication.timeSinceStartup);
            SessionState.SetFloat(BatchPlaying, 0f);
            EditorApplication.update += BatchStep;
            Open();
        }

        // Entering Play Mode reloads scripts, so the wait picks up again afterwards.
        [InitializeOnLoadMethod]
        static void ResumeBatch()
        {
            if (SessionState.GetBool(BatchKey, false))
                EditorApplication.update += BatchStep;
        }

        static void BatchStep()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - SessionState.GetFloat(BatchStarted, (float)now) > 900f)
            {
                FinishBatch(2);
                return;
            }
            if (!EditorApplication.isPlaying || !(Services.Session?.Player?.ViewCamera))
                return;
            float playing = SessionState.GetFloat(BatchPlaying, 0f);
            if (playing <= 0f)
            {
                SessionState.SetFloat(BatchPlaying, (float)now);
                return;
            }
            // Give the map, its art and the fire a few seconds to build and settle.
            if (now - playing < 15f)
                return;
            Directory.CreateDirectory(BatchFolder);
            foreach (var view in FamilyRoomViews)
                Shoot(Path.Combine(BatchFolder, view.name + ".png"), view.eye, view.target, view.fov);
            File.WriteAllText(Path.Combine(BatchFolder, "done.txt"), System.DateTime.Now.ToString("s"));
            FinishBatch(0);
        }

        static void FinishBatch(int code)
        {
            EditorApplication.update -= BatchStep;
            SessionState.EraseBool(BatchKey);
            EditorApplication.Exit(code);
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
                // A camera outside the room starts drawing past the south wall. The family room's
                // south wall is at z -600 (it was -262 when the room was small).
                camera.nearClipPlane = eye.z < -602f ? (-590f - eye.z) * 1.06f : .05f;
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
