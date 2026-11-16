using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class WardrobeApprovedTeamColorShots
    {
        const string Fixture = "DeadlyJenga.WardrobeApprovedTeamColorShots";
        const string BridgeFolder = "DeadlyJenga.WardrobePreviewBridge";
        const BindingFlags Private = BindingFlags.Static | BindingFlags.NonPublic;
        static readonly Type Bridge = typeof(WardrobePreviewBridge);

        static WardrobeApprovedTeamColorShots()
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
                "WardrobeApprovedTeamColors-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(output);
            SessionState.SetString(Fixture, output);
            SessionState.SetString(BridgeFolder, output);
            SessionState.SetFloat(Fixture + ".Deadline", (float)EditorApplication.timeSinceStartup + 60f);
            Debug.Log("WARDROBE_APPROVED_TEAM_COLOR_OUTPUT " + output);
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
            var image = new Texture2D(720, 720, TextureFormat.RGB24, false);
            var rows = new List<string>();
            try
            {
                foreach (var skin in new[] { AvatarSkin.Rabbit, AvatarSkin.Clownfish, AvatarSkin.CandyCorn, AvatarSkin.Dragon })
                {
                    Bounds frame = default;
                    for (int team = -1; team < 2; team++)
                    {
                        Color? teamColor = team < 0 ? null : ModeNames.Colors[team];
                        string label = team < 0 ? "original" : ModeNames.ColorNames[team].ToLowerInvariant();
                        var avatar = Object.Instantiate(Resources.Load<CandyPlayerRig>("CandyPlayerVisual"), stage.transform);
                        try
                        {
                            avatar.SetColor(0);
                            if (avatar.TryGetComponent<CandyAvatarDetail>(out var detail)) detail.enabled = false;
                            avatar.SetAppearance(new AvatarAppearance { skin = skin });
                            Call("Pose", avatar);
                            using var rabbit = skin == AvatarSkin.Rabbit ? new CaramelRabbitPreview() : null;
                            using var store = skin == AvatarSkin.Rabbit ? null : new StoreSkinPreview(teamColor);
                            if (rabbit != null) rabbit.Apply(avatar, true, teamColor);
                            else store.Apply(avatar, skin);
                            var measured = (Bounds)Call("Measure", avatar);
                            if (team < 0) frame = measured;
                            Color body = avatar.bodyRenderer.sharedMaterial.GetColor("_BaseColor");
                            if (teamColor.HasValue && body != teamColor.Value)
                                throw new InvalidOperationException($"Approved {skin} did not use the requested {label} body color.");
                            string filename = $"{skin}-{label}.png";
                            Call("PlaceCamera", frame, true, 153f, 8f, 1f);
                            Call("RenderView", image, true, 0);
                            image.Apply();
                            File.WriteAllBytes(Path.Combine(output, filename), image.EncodeToPNG());
                            rows.Add($"{skin}\t{label}\t{ColorUtility.ToHtmlStringRGB(body)}\t{filename}");
                        }
                        finally
                        {
                            avatar.gameObject.SetActive(false);
                            Object.Destroy(avatar.gameObject);
                        }
                    }
                }

                File.WriteAllLines(Path.Combine(output, "team-colors.tsv"), rows);
                Debug.Log("WARDROBE_APPROVED_TEAM_COLOR_COMPLETE 12 PNGs; four approved store recipes");
            }
            finally
            {
                Object.Destroy(image);
            }
        }
    }
}
