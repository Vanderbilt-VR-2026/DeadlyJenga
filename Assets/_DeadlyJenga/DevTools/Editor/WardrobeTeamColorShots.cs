using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CharacterNetwork;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class WardrobeTeamColorShots
    {
        const string Fixture = "DeadlyJenga.WardrobeTeamColorShots";
        const string BridgeFolder = "DeadlyJenga.WardrobePreviewBridge";
        const BindingFlags Private = BindingFlags.Static | BindingFlags.NonPublic;
        static readonly Type Bridge = typeof(WardrobePreviewBridge);

        static WardrobeTeamColorShots()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetString(Fixture, "") != "")
                    EditorApplication.update += CaptureWhenReady;
                if (state == PlayModeStateChange.ExitingPlayMode)
                    EditorApplication.update -= CaptureWhenReady;
            };
        }

        public static void Run()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Use the reserved batch editor.");
            RuntimeHelpers.RunClassConstructor(Bridge.TypeHandle);
            string output = Path.GetFullPath(ProjectPath.Report(
                "WardrobeTeamColors-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(output);
            SessionState.SetString(Fixture, output);
            SessionState.SetString(BridgeFolder, output);
            SessionState.SetFloat(Fixture + ".Deadline", (float)EditorApplication.timeSinceStartup + 60f);
            Debug.Log("WARDROBE_TEAM_COLOR_OUTPUT " + output);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }

        static void CaptureWhenReady()
        {
            string output = SessionState.GetString(Fixture, "");
            if (string.IsNullOrEmpty(output)) return;
            try
            {
                bool ready = (bool)Bridge.GetField("castleReady", Private).GetValue(null);
                if (!ready && !(bool)Call("TryPrepareCastle"))
                {
                    if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Fixture + ".Deadline", 0))
                        throw new TimeoutException("Castle preview did not become ready.");
                    return;
                }
                EditorApplication.update -= CaptureWhenReady;
                Capture(output);
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(output, "unity-error.txt"), error.ToString());
                Debug.LogException(error);
            }

            SessionState.EraseString(Fixture);
            SessionState.EraseFloat(Fixture + ".Deadline");
            EditorApplication.ExitPlaymode();
        }

        static object Call(string method, params object[] arguments) =>
            Bridge.GetMethod(method, Private).Invoke(null, arguments);

        static void Capture(string output)
        {
            var stage = (GameObject)Bridge.GetField("root", Private).GetValue(null);
            var main = (CandyPlayerRig)Bridge.GetField("rig", Private).GetValue(null);
            if (!stage || !main) throw new InvalidOperationException("Production preview rig is missing.");
            main.gameObject.SetActive(false);
            var wardrobeActor = new GameObject("Staged team identity");
            wardrobeActor.transform.SetParent(stage.transform, false);
            var identity = wardrobeActor.AddComponent<SurvivorNetworkView>();
            identity.enabled = false;
            var avatar = Object.Instantiate(Resources.Load<CandyPlayerRig>("CandyPlayerVisual"), wardrobeActor.transform);
            if (avatar.TryGetComponent<CandyAvatarDetail>(out var detail)) detail.enabled = false;
            avatar.SetColor(0);
            var image = new Texture2D(720, 720, TextureFormat.RGB24, false);
            var original = MatchRuntime.Current;
            var fixtureRuntime = new TeamRules(original, wardrobeActor);
            var block = new MaterialPropertyBlock();
            var rows = new List<string>();
            try
            {
                foreach (var skin in new[] { AvatarSkin.Rabbit, AvatarSkin.Clownfish, AvatarSkin.CandyCorn, AvatarSkin.Dragon })
                {
                    MatchRuntime.Current = fixtureRuntime;
                    identity.SetIdentity("castle-palette", "Castle palette", -1, Color.white);
                    avatar.SetAppearance(new AvatarAppearance { skin = skin });
                    Call("Pose", avatar);
                    var frame = (Bounds)Call("Measure", avatar);
                    for (int team = -1; team < 2; team++)
                    {
                        Color teamColor = team < 0 ? Color.white : ModeNames.Colors[team];
                        string label = team < 0 ? "original" : ModeNames.ColorNames[team].ToLowerInvariant();
                        identity.SetIdentity("castle-palette", "Castle palette", team, teamColor);
                        Call("Pose", avatar);
                        avatar.bodyRenderer.GetPropertyBlock(block);
                        Color bodyColor = block.GetColor("_BaseColor");
                        if (team >= 0 && bodyColor != teamColor)
                            throw new InvalidOperationException($"The production wardrobe failed to apply {label} to {skin}: {bodyColor}.");
                        string filename = $"{skin}-{label}.png";
                        Call("PlaceCamera", frame, true, 153f, 8f, 1f);
                        Call("RenderView", image, true, 0);
                        image.Apply();
                        File.WriteAllBytes(Path.Combine(output, filename), image.EncodeToPNG());
                        rows.Add($"{skin}\t{label}\t{ColorUtility.ToHtmlStringRGB(bodyColor)}\t{filename}");
                    }
                    MatchRuntime.Current = original;
                }
                File.WriteAllLines(Path.Combine(output, "team-colors.tsv"), rows);
                Debug.Log("WARDROBE_TEAM_COLOR_COMPLETE 12 PNGs; four production skin palettes");
            }
            finally
            {
                MatchRuntime.Current = original;
                Object.Destroy(image);
                Object.Destroy(wardrobeActor);
            }
        }

        sealed class TeamRules : IMatchRuntime
        {
            readonly IMatchRuntime original;
            readonly GameObject actor;
            readonly ModeRules teamRules = new ModeRules { mode = GameModeKind.CaptureFlag };

            public TeamRules(IMatchRuntime original, GameObject actor)
            {
                this.original = original;
                this.actor = actor;
            }

            public ModeRules Rules => original?.Rules;
            public MatchFrame Frame => original?.Frame;
            public string LocalActorId => original?.LocalActorId;
            public bool Authority => original?.Authority ?? false;
            public IModeActor FindActor(string id) => original?.FindActor(id);
            public Transform FindParticipantRoot(string id) => original?.FindParticipantRoot(id);
            public ModeRules RulesFor(GameObject obj) => obj == actor ? teamRules : original?.RulesFor(obj);
            public bool CanClimb(GameObject obj, IClimbable climbable) => original?.CanClimb(obj, climbable) ?? false;
            public bool RequestPunch(GameObject source, Vector3 origin, Vector3 direction, float speed) =>
                original?.RequestPunch(source, origin, direction, speed) ?? false;
            public void Configure(ModeRules rules) => original?.Configure(rules);
            public void StartRound() => original?.StartRound();
            public void ReturnToLobby() => original?.ReturnToLobby();
            public void NextSpectator(int step) => original?.NextSpectator(step);
            public void ActivateHandPower(float seconds) => original?.ActivateHandPower(seconds);
        }
    }
}
