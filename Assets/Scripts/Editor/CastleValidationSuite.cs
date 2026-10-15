using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class CastleValidationSuite
    {
        const string Key = "CastleRunValidationSuite";
        static CastleValidationSuite()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
                {
                    SessionState.SetBool(Key,false);
                    EditorApplication.delayCall += () =>
                    {
                        var session = Object.FindFirstObjectByType<CastlePlaySession>();
                        session.StartCoroutine(All(session));
                    };
                }
            };
        }
        [MenuItem("Tools/Castle Prototype/Run Full Validation Suite")]
        public static void Run()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/Game.unity")
                throw new System.InvalidOperationException("Open the Game scene before running the suite.");
            SessionState.SetBool(Key,true); EditorApplication.EnterPlaymode();
        }
        static IEnumerator All(CastlePlaySession session)
        {
            yield return new WaitForSeconds(2);
            File.WriteAllText("Assets/CastlePrototype/SpawnValidation.txt",
                $"{(session.Player.Grounded && Vector3.Distance(session.Player.transform.position, session.LastSpawnFeet) < .2f ? "PASS" : "FAIL")} Initial spawn stays on top after 2 seconds: actual={session.Player.transform.position}, target={session.LastSpawnFeet}\n");
            yield return CastleReferenceValidation.Validate(session);
            yield return CastleTraversalValidation.Validate(session);
            CastlePrototypeBuilder.ValidatePhysics();
            yield return CastleInputValidation.Validate(session);
            CastleEvidenceCapture.Capture();
            File.WriteAllText("Temp/CastleEvidence/suite-complete.txt", "Unity validation suite completed. Inspect all reports for PASS/FAIL.");
            EditorApplication.isPlaying = false;
        }
    }
}
