using System;
using System.IO;
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
            public PlayerArtifact[] artifacts;
        }

        public static void RunBatch()
        {
            var output = Path.GetFullPath("Builds/IntegrationSweep");
            var player = Path.Combine(output, "DeadlyJenga.exe");
            var receipt = new Receipt { unity = Application.unityVersion, player = player };
            int exitCode = 1;
            try
            {
                receipt.result = CrossProcessBuild.BuildSmokePlayer(player, false);
                if (!receipt.result.StartsWith("Succeeded ", StringComparison.Ordinal) || !File.Exists(player))
                    throw new InvalidOperationException(receipt.result);
                string[] selected =
                {
                    "DeadlyJenga.exe",
                    "DeadlyJenga_Data/Managed/DeadlyJenga.DevTools.dll",
                    "DeadlyJenga_Data/Managed/DeadlyJenga.GameSession.dll",
                    "DeadlyJenga_Data/Managed/DeadlyJenga.Core.dll",
                    "DeadlyJenga_Data/Managed/DeadlyJenga.Characters.dll",
                    "DeadlyJenga_Data/Managed/Fusion.Runtime.dll",
                    "DeadlyJenga_Data/globalgamemanagers"
                };
                receipt.artifacts = Array.ConvertAll(selected, path =>
                {
                    using var stream = File.OpenRead(Path.Combine(output, path));
                    using var hash = SHA256.Create();
                    return new PlayerArtifact
                    {
                        path = path,
                        sha256 = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant()
                    };
                });
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
