using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using P = DeadlyJenga.CastlePrototype.CastleLanProtocol;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class CastleCleanupValidation
    {
        const string Request = "Temp/CastleCleanup/check.request";
        static CastleCleanupValidation()
        {
            EditorApplication.update += () =>
            {
                if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(Request);
                try { Run(); }
                catch (Exception e) { Directory.CreateDirectory("Temp/CastleCleanup"); File.WriteAllText("Temp/CastleCleanup/results.txt", "FAIL " + e); Debug.LogException(e); }
            };
        }
        [MenuItem("Tools/Castle Prototype/Validate Cleanup Networking")]
        public static void Run()
        {
            var report = new StringBuilder();
            void Check(bool value, string name) { if (!value) throw new Exception(name); report.AppendLine("PASS " + name); }
            CastleLanValidation.Run();
            Check(true, "Existing snapshot, malformed-packet and remote-hand physics validation");
            int count = CastleLayout.Build(new CastleSettings()).Count;
            var snapshot = new P.Snapshot
            {
                revision = 4, settings = JsonUtility.ToJson(new CastleSettings()),
                origin = new P.Pose(Vector3.zero, Quaternion.identity), originScale = Vector3.one,
                blocks = Enumerable.Range(0, count).Select(i => new P.Pose(new Vector3(i % 20, i / 20, i % 7), Quaternion.Euler(i % 90, i, 0))).ToArray(),
                active = Enumerable.Repeat(true, count).ToArray(), actors = Array.Empty<P.Actor>()
            };
            byte[] raw = P.EncodeWorld(snapshot), packed = CastleFusionWire.Compress(raw);
            byte[] restored = CastleFusionWire.Decompress(packed);
            Check(raw.SequenceEqual(restored), "Compressed castle round trip preserves every byte");
            Check(P.DecodeWorld(restored).blocks.Length == count, "Complete castle survives the Fusion wire format");
            bool bounded = false;
            using (var output = new MemoryStream())
            {
                using (var zip = new System.IO.Compression.DeflateStream(output, System.IO.Compression.CompressionLevel.Fastest, true))
                { var large = new byte[CastleFusionWire.MaxBytes + 1]; zip.Write(large, 0, large.Length); }
                try { CastleFusionWire.Decompress(output.ToArray()); } catch (InvalidDataException) { bounded = true; }
            }
            Check(bounded, "Oversized expanded transfers are rejected");
            var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var go = new GameObject("Cleanup validation survivor");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, preview);
            go.SetActive(false);
            try
            {
                var survivor = go.AddComponent<CastleSurvivor>();
                survivor.climbGripSeconds = 7; survivor.climbDrainCurve = StaminaCurve.PowerOut; survivor.climbRecoveryDegree = 4;
                string rules = MatchSetup.CaptureRules(survivor);
                survivor.climbGripSeconds = 1; survivor.climbDrainCurve = StaminaCurve.Linear; survivor.climbRecoveryDegree = 1;
                MatchSetup.ApplyRules(survivor, rules);
                Check(survivor.climbGripSeconds == 7 && survivor.climbDrainCurve == StaminaCurve.PowerOut && survivor.climbRecoveryDegree == 4, "Host stamina rules restore on clients");
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview); }
            report.AppendLine($"Sample layout: {count} blocks, {raw.Length} raw bytes, {packed.Length} compressed bytes.");
            Directory.CreateDirectory("Temp/CastleCleanup"); File.WriteAllText("Temp/CastleCleanup/results.txt", report.ToString());
            Debug.Log(report.ToString());
        }
    }
}
