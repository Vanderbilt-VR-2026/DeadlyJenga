using System;
using System.Threading.Tasks;
using Fusion;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.Avatars.Editor
{
    /// <summary>Offline Play-mode check; does not create a Photon room or save runtime changes.</summary>
    [InitializeOnLoad]
    public static class LobbyAvatarSmokeTest
    {
        const string Pending = "DeadlyJenga.LobbyAvatarSmokeTest";
        static LobbyAvatarSmokeTest() => EditorApplication.playModeStateChanged += OnPlayMode;

        [MenuItem("Tools/Deadly Jenga/Run Lobby Avatar Smoke Test")]
        public static void Run()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != "Assets/Scenes/Lobby.unity")
                throw new InvalidOperationException("Open Lobby outside Play mode first.");
            LobbyAvatarSetup.Validate();
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            EditorApplication.delayCall += Check;
        }

        static async void Check()
        {
            NetworkRunner runner = null;
            try
            {
                var config = UnityEngine.Object.FindFirstObjectByType<LobbyAvatarScene>();
                var local = config.GetComponentInChildren<SimpleAvatarView>();
                Require(local != null && local.localView, "Immediate local avatar");
                Require(local.head.GetComponent<Renderer>().forceRenderingOff, "Local head hidden");
                Require(!local.leftHand.GetComponent<Renderer>().forceRenderingOff, "Local mitten visible");
                var controller = config.leftHandTarget.GetComponentInChildren<MeshRenderer>(true);
                Require(controller != null && controller.forceRenderingOff, "Original controller hidden");
                var input = LocalHandInput.FindOrCreate();
                Require(input != null && input.TryRead(out _), "Local input available");
                input.TryRead(out var hardwarePose);
                Require(Vector3.Distance(hardwarePose.HeadPosition, config.headTarget.position) < .0001f, "Hardware head input");

                var go = new GameObject("Offline avatar validation runner");
                runner = go.AddComponent<NetworkRunner>();
                runner.ProvideInput = true;
                var pose = new HandPoseInput {
                    RootPosition = new Vector3(2, 0, 3), RootRotation = Quaternion.identity,
                    HeadPosition = new Vector3(2, 1.7f, 3), HeadRotation = Quaternion.Euler(10, 30, 0),
                    LeftPosition = new Vector3(1.6f, 1.2f, 3.2f), LeftRotation = Quaternion.Euler(0, 20, 0),
                    RightPosition = new Vector3(2.4f, 1.3f, 3.2f), RightRotation = Quaternion.identity
                };
                var events = go.AddComponent<NetworkEvents>();
                events.OnInput = new NetworkEvents.InputEvent();
                events.OnInput.AddListener((r, value) => value.Set(pose));
                var result = await runner.StartGame(new StartGameArgs { GameMode = GameMode.Single });
                Require(result.Ok, "Offline Fusion start");
                var owner = runner.Spawn(config.networkPrefab, inputAuthority: runner.LocalPlayer);
                Require(owner != null, "Registered network prefab spawn");
                Require(owner.GetComponentInChildren<SimpleAvatarView>() == null, "No duplicate owner avatar");
                var remote = runner.Spawn(config.networkPrefab, inputAuthority: PlayerRef.None);
                var remoteView = remote.GetComponentInChildren<SimpleAvatarView>();
                Require(remoteView != null && !remoteView.localView, "Remote presentation");
                Require(!remoteView.head.GetComponent<Renderer>().forceRenderingOff, "Remote head visible");
                var until = EditorApplication.timeSinceStartup + .5;
                while (EditorApplication.timeSinceStartup < until) await Task.Yield();
                var tracked = owner.GetComponent<LobbyNetworkAvatar>();
                Require(Vector3.Distance(tracked.headPose.position, pose.HeadPosition) < .001f, "Fusion head input application");
                Require(Quaternion.Angle(tracked.headPose.rotation, pose.HeadRotation) < .1f, "Fusion head rotation");
                Require(Vector3.Distance(tracked.leftHandPose.position, pose.LeftPosition) < .001f, "Fusion left hand input");
                Require(Vector3.Distance(tracked.rightHandPose.position, pose.RightPosition) < .001f, "Fusion right hand input");
                runner.Despawn(remote);
                runner.Despawn(owner);
                config.enabled = false;
                Require(!controller.forceRenderingOff, "Controller visibility restored on disable");
                Debug.Log("Lobby avatar OFFLINE PLAY TEST PASSED: local visibility, hardware input, Fusion prefab registration/spawning, head/hand input simulation, remote presentation, despawning, and cleanup.");
            }
            catch (Exception error) { Debug.LogException(error); }
            finally
            {
                if (runner != null) await runner.Shutdown();
                EditorApplication.isPlaying = false;
            }
        }

        static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Avatar play test failed: " + message);
        }
    }
}
