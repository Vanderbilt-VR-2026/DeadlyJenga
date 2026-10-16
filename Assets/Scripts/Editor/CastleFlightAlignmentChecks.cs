using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlyingHand;
using DeadlyJenga.CandyPlayers;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class CastleFlightAlignmentChecks
    {
        const string Request = "Temp/CastleFlightAlignment/check.request";
        const string Report = "Temp/CastleFlightAlignment/results.txt";
        static CastleFlightAlignmentChecks()
        {
            EditorApplication.update += () =>
            {
                if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(Request);
                try { Run(); }
                catch (Exception error) { File.WriteAllText(Report, "FAIL " + error); Debug.LogException(error); }
            };
        }
        sealed class ForwardInput : IFlyingHandInputSource
        {
            public FlyingHandCommand ReadFlightInput(FlyingHandController motor, float dt)
                => new FlyingHandCommand { moveWorld = Vector3.forward };
        }
        [MenuItem("Tools/Castle Prototype/Validate Flight and Body Alignment")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run these checks outside Play mode.");
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                if (scene.GetPhysicsScene() == Physics.defaultPhysicsScene)
                    throw new Exception("Regression checks require isolated preview physics.");
                // Read the saved runner configuration without opening or changing Lobby.
                string lobby = File.ReadAllText("Assets/Scenes/Lobby.unity");
                if (!lobby.Contains("Update3DPhysicsScene: 1") || lobby.Contains("Update3DPhysicsScene: 0"))
                    throw new Exception("Lobby runner must simulate 3D physics.");
                var vehicle = new GameObject("Flight regression motor");
                SceneManager.MoveGameObjectToScene(vehicle, scene);
                vehicle.AddComponent<BoxCollider>();
                var motor = vehicle.AddComponent<FlyingHandController>();
                motor.captureDesktopCursor = false;
                motor.externallyDriven = true;
                motor.InputDriver = new ForwardInput();
                motor.ConfigureBody();
                motor.SendMessage("Update");
                for (int i = 0; i < 60; i++)
                {
                    motor.SendMessage("FixedUpdate");
                    scene.GetPhysicsScene().Simulate(Time.fixedDeltaTime);
                }
                if (motor.Body.position.z < 1f)
                    throw new Exception("Flight input did not move the rigidbody forward.");

                var root = new GameObject("Avatar regression");
                root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.SetPositionAndRotation(new Vector3(4, 2, 6), Quaternion.Euler(0, 90, 0));
                var camera = new GameObject("Head").AddComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.transform.localPosition = new Vector3(.4f, 1.7f, .3f);
                camera.transform.localRotation = Quaternion.Euler(20, 40, 10);
                var avatar = root.AddComponent<CandyPlayerAvatar>();
                avatar.viewCamera = camera;
                var frame = avatar.ReadFrame();
                var expected = new Vector3(camera.transform.position.x, root.transform.position.y, camera.transform.position.z);
                if (Vector3.Distance(frame.feet, expected) > .0001f || Quaternion.Angle(frame.headRotation, camera.transform.rotation) > .001f)
                    throw new Exception("Body must stay under the head without changing tracked head rotation.");
                File.WriteAllText(Report, "PASS Lobby 3D simulation enabled\nPASS Flight motor moves under simulated physics\nPASS Body follows room-scale head position and preserves head rotation\n");
                Debug.Log(File.ReadAllText(Report));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
