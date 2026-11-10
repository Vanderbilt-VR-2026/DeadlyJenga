using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    // Desktop shortcut: Game scene, Moonlit Shinobi Temple, Shadow Lantern with bots.
    [InitializeOnLoad]
    public static class NinjaTemplePlay
    {
        const string Key = "NinjaTemplePlay";
        static NinjaTemplePlay() { EditorApplication.update += Tick; }
        [MenuItem("Tools/Deadly Jenga/Maps/Play Ninja Temple (Shadow Lantern + bots)")]
        public static void Play()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { SessionState.SetBool(Key, true); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetBool(Key, true); EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity")); EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            var session = Services.Session as CastlePlaySession; if (!session || session.Player == null || !session.Modes) return;
            SessionState.SetBool(Key, false);
            MatchSetup.MapId = NinjaTempleTools.MapId; MatchSetup.ModeId = "demo"; MatchSetup.ApplyMap();
            var rules = MatchSetup.DefaultModeRules(GameModeKind.Ninja, MatchSetup.SelectedMap); rules.countdownSeconds = 2;
            session.Modes.Configure(rules);
            typeof(MatchModeRuntime).GetMethod("StartRoundAuthoritative", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session.Modes, null);
            Debug.Log("Shadow Lantern round started on the Moonlit Shinobi Temple.");
        }
    }
}
