using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype.Editor
{
    public static class CrossProcessPeerBuild
    {
        [Serializable]
        sealed class PlayerArtifact
        {
            public string path;
            public string sha256;
        }

        [Serializable]
        sealed class Receipt
        {
            public string result;
            public string unity;
            public string player;
            public bool scriptsOnly;
            public string[] enabledScenes;
            public PlayerArtifact[] retainedArtifacts;
            public PlayerArtifact[] artifacts;
        }

        const string FixtureAssembly = "DeadlyJenga_Data/Managed/DeadlyJenga.DevTools.dll";
        static readonly string[] artifactPaths =
        {
            "DeadlyJenga.exe",
            FixtureAssembly,
            "DeadlyJenga_Data/Managed/DeadlyJenga.GameSession.dll",
            "DeadlyJenga_Data/Managed/DeadlyJenga.Core.dll",
            "DeadlyJenga_Data/Managed/DeadlyJenga.Characters.dll",
            "DeadlyJenga_Data/Managed/Fusion.Runtime.dll",
            "DeadlyJenga_Data/globalgamemanagers"
        };

        static PlayerArtifact[] HashArtifacts(string output) => Array.ConvertAll(artifactPaths, path =>
        {
            using var stream = File.OpenRead(Path.Combine(output, path));
            using var hash = SHA256.Create();
            return new PlayerArtifact
            {
                path = path,
                sha256 = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant()
            };
        });

        public static void RunBatch() => BuildAndExit(false);
        public static void RunScriptsOnlyBatch() => BuildAndExit(true);

        static void BuildAndExit(bool scriptsOnly)
        {
            var output = Path.GetFullPath("Builds/IntegrationSweep");
            var player = Path.Combine(output, "DeadlyJenga.exe");
            var receipt = new Receipt
            {
                unity = Application.unityVersion,
                player = player,
                scriptsOnly = scriptsOnly,
                enabledScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray()
            };
            int exitCode = 1;
            try
            {
                if (scriptsOnly) receipt.retainedArtifacts = HashArtifacts(output);
                receipt.result = CrossProcessBuild.BuildSmokePlayer(player, scriptsOnly);
                if (!receipt.result.StartsWith("Succeeded ", StringComparison.Ordinal) || !File.Exists(player))
                    throw new InvalidOperationException(receipt.result);
                receipt.artifacts = HashArtifacts(output);
                if (scriptsOnly)
                {
                    for (int i = 0; i < receipt.artifacts.Length; i++)
                    {
                        var artifact = receipt.artifacts[i];
                        bool changed = artifact.sha256 != receipt.retainedArtifacts[i].sha256;
                        if (artifact.path == FixtureAssembly && !changed)
                            throw new InvalidOperationException("Scripts-only build did not update the fixture assembly.");
                        if (artifact.path != FixtureAssembly && changed)
                            throw new InvalidOperationException("Scripts-only build changed retained player artifact " + artifact.path);
                    }
                }
                exitCode = 0;
            }
            catch (Exception error)
            {
                receipt.result = error.ToString();
                Debug.LogError(error);
            }
            finally
            {
                Directory.CreateDirectory("Logs/Validation");
                File.WriteAllText("Logs/Validation/CrossProcessPeerBuild.json", JsonUtility.ToJson(receipt, true));
                EditorApplication.Exit(exitCode);
            }
        }
    }
}
