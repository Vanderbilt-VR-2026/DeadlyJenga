using System;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    [Serializable]
    public sealed class SpectatorRecordingPlan
    {
        public string mapId = "castle", modeId = "demo";
        public LobbySetup match = new LobbySetup();
        public int iterations = 1, seconds = 30, fps = 15, viewSeconds = 15;
        public bool keepFrames, continueAfterFailure = true, notify = true;
        public string ffmpeg = "", output = "";

        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        public static string DefaultOutput => Path.Combine(ProjectRoot, "Recordings");
        public static string EncoderPreference => "DeadlyJenga.Recording.FFmpeg." + Application.dataPath;

        public static IslandLobbyCatalog Catalog()
        {
            var source = IslandRoomConnection.Instance ? IslandRoomConnection.Instance.Catalog : Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");
            if (!source) throw new InvalidOperationException("The island lobby catalog is unavailable.");
            var copy = UnityEngine.Object.Instantiate(source);
            copy.hideFlags = HideFlags.HideAndDontSave;
            copy.EnsureSupportedModes();
            copy.EnsureModeMaps();
            return copy;
        }

        public static SpectatorRecordingPlan Current()
        {
            var plan = new SpectatorRecordingPlan { mapId = MatchChoices.MapId, modeId = MatchChoices.ModeId };
            var lobby = Services.Session as ILobbyDetails;
            var session = Services.Session as CastlePlaySession;
            if (lobby != null)
            {
                plan.mapId = lobby.SelectedMapId;
                plan.modeId = lobby.SelectedModeId;
                plan.match = Clone(lobby.Setup);
            }
            else
            {
                plan.match = new LobbySetup
                {
                    population = session ? session.SurvivorBots.Count + 1 : MatchChoices.BotTarget,
                    hands = session ? session.HandBots.Count : MatchChoices.HandCount,
                    survivorPreset = session ? session.SurvivorBots.SelectedPreset : MatchChoices.SurvivorPreset,
                    handPreset = session ? session.HandBots.SelectedPreset : MatchChoices.HandPreset,
                    sameHumanTeam = MatchChoices.SameHumanTeam,
                    movement = Clone(session && session.Player is ISurvivorSettings settings ? settings.CurrentRules : MatchChoices.Rules),
                    rules = session ? Clone(session.Modes.Rules) : ModeSimulation.Defaults(MatchModeRuntime.ParseMode(plan.modeId)),
                    disasters = session ? DemoDisasters.Prefabs.Select(p => new LobbyHazardSetup { id = p.name, values = DemoDisasters.Values(session, p).ToArray() }).ToArray() : Array.Empty<LobbyHazardSetup>()
                };
            }
            plan.output = DefaultOutput;
            plan.ffmpeg = FindEncoder(EditorPrefs.GetString(EncoderPreference, ""));
            return plan;
        }

        public static T Clone<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));

        public static bool MapAvailable(IslandLobbyCatalog.Map map) => map != null && (map.mapPrefab || (!string.IsNullOrEmpty(map.scenePath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(map.scenePath)));

        public void Validate()
        {
            if (iterations < 1 || iterations > 100) throw new ArgumentException("Iterations must be between 1 and 100.");
            if (seconds < 1 || seconds > 3600) throw new ArgumentException("Duration must be between 1 and 3600 seconds.");
            if (viewSeconds < 0 || viewSeconds > 3600) throw new ArgumentException("View rotation must be between 0 and 3600 seconds; zero keeps fixed views.");
            if (fps < 1 || fps > 60) throw new ArgumentException("Frame rate must be between 1 and 60.");
            if (match == null || match.rules == null || match.movement == null) throw new ArgumentException("Match settings are missing.");
            if (match.population < 2 || match.population > 100) throw new ArgumentException("Player slots must be between 2 and 100, including the observer.");
            if (match.hands < 0 || match.hands > 50) throw new ArgumentException("AI hands must be between 0 and 50.");
            if (match.rules.mode != MatchModeRuntime.ParseMode(modeId) || !ModeFrameValidation.Rules(match.rules))
                throw new ArgumentException("The mode settings do not match the selected game mode or contain invalid values.");
            var catalog = Catalog();
            try
            {
                if (!catalog.maps.Any(m => m != null && m.id == mapId && MapAvailable(m))) throw new ArgumentException("Select a map with an available scene or prefab.");
                if (!catalog.modes.Any(m => m != null && m.id == modeId && m.available)) throw new ArgumentException("Select an available game mode.");
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
            output = Path.GetFullPath(string.IsNullOrWhiteSpace(output) ? DefaultOutput : output);
            var assets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar);
            if (output.Equals(assets, StringComparison.OrdinalIgnoreCase) || output.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Save recordings outside Assets to avoid importing video frames.");
            if (!string.IsNullOrWhiteSpace(ffmpeg) && !File.Exists(ffmpeg)) throw new FileNotFoundException("The selected FFmpeg executable no longer exists. Choose its current location.");
            ffmpeg = FindEncoder(ffmpeg);
            if (string.IsNullOrEmpty(ffmpeg)) throw new FileNotFoundException("FFmpeg was not found. Choose its executable in the recording window.");
        }

        public static string FindEncoder(string selected)
        {
            if (!string.IsNullOrWhiteSpace(selected) && File.Exists(selected)) return Path.GetFullPath(selected);
            string executable = Application.platform == RuntimePlatform.WindowsEditor ? "ffmpeg.exe" : "ffmpeg";
            foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                try
                {
                    string candidate = Path.Combine(folder.Trim('"'), executable);
                    if (File.Exists(candidate)) return Path.GetFullPath(candidate);
                }
                catch (ArgumentException) { }
            }
            return "";
        }

        public static string TimestampDirectory(string parent)
        {
            Directory.CreateDirectory(parent);
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string path = Path.Combine(parent, stamp);
            for (int suffix = 2; Directory.Exists(path) || File.Exists(path); suffix++) path = Path.Combine(parent, stamp + "-" + suffix.ToString("D2"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
