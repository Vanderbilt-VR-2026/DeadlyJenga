using System.Collections;
using System.IO;
using System.Text;
using FlyingHand;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class CastleTraversalValidation
    {
        static CastleTraversalValidation()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("CastleTraversalTest", false))
                { SessionState.SetBool("CastleTraversalTest", false); EditorApplication.delayCall += Run; }
            };
        }
        [MenuItem("Tools/Castle Prototype/Validate Player and Vault %#F8")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                SessionState.SetBool("CastleTraversalTest", true); EditorApplication.EnterPlaymode(); return;
            }
            var session = Object.FindFirstObjectByType<CastlePlaySession>();
            session.StartCoroutine(Validate(session));
        }
        public static IEnumerator Validate(CastlePlaySession session)
        {
            yield return null;
            var report = new StringBuilder();
            void Check(string label, bool pass, string detail = "") => report.AppendLine((pass ? "PASS " : "FAIL ") + label + " " + detail);
            var fixture = new GameObject("Temporary traversal validation");
            var motor = Object.Instantiate(session.survivorPrefab, fixture.transform);
            motor.viewCamera.gameObject.SetActive(false); motor.enabled = false; motor.RefreshMode();
            var basis = new Vector3(100, 10, 100);
            GameObject Box(string name, Vector3 pos, Vector3 scale)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
                go.transform.SetParent(fixture.transform); go.transform.position = basis + pos; go.transform.localScale = scale; return go;
            }
            Box("Floor", new Vector3(0, -.5f, 0), new Vector3(20, 1, 20));
            var crate = Box("Full size crate ledge", new Vector3(0, 1, 2), new Vector3(6, 2, 2));
            try
            {
                Check("Default role survivor", !session.PlayingHand);
                Check("Spawn at top", session.Player.transform.position.y > 59,
                    $"actual={session.Player.transform.position}, target={session.LastSpawnFeet}");
                Check("Single active camera", Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1);
                session.Player.RefreshMode();
                Check("Auto desktop without running display", !session.Player.IsVR);
                session.Player.controlMode = ControlMode.VR; session.Player.RefreshMode();
                Check("VR enables head tracking", session.Player.viewCamera.GetComponent<TrackedPoseDriver>().enabled);
                session.Player.controlMode = ControlMode.Auto; session.Player.RefreshMode();
                Check("Desktop restores untracked eye", !session.Player.viewCamera.GetComponent<TrackedPoseDriver>().enabled);
                motor.Teleport(basis + Vector3.up * .04f, Quaternion.identity); Physics.SyncTransforms();
                for (int i = 0; i < 20; i++) motor.Simulate(Vector2.zero, false, .02f);
                Check("Ground detection", motor.Grounded);
                float baseHeight = motor.transform.position.y, peak = baseHeight;
                motor.Simulate(Vector2.zero, true, .01f);
                for (int i = 0; i < 110; i++) { motor.Simulate(Vector2.zero, false, .01f); peak = Mathf.Max(peak, motor.transform.position.y); }
                Check("Jump remains just below 2m crate", peak - baseHeight > 1.77f && peak - baseHeight < 1.9f, $"height={peak - baseHeight:F3}m");
                motor.Teleport(basis+new Vector3(0,.04f,-1.1f),Quaternion.identity);Physics.SyncTransforms();
                for(int i=0;i<15;i++)motor.Simulate(Vector2.zero,false,.02f);
                motor.Simulate(Vector2.up,true,.01f);
                for(int i=0;i<110;i++)motor.Simulate(Vector2.up,false,.01f);
                Check("Jump and forward movement cannot bypass crate without vault",motor.transform.position.z<basis.z+1&&motor.VaultCount==0);
                motor.Teleport(basis + new Vector3(-5, 3, -3), Quaternion.identity); Physics.SyncTransforms();
                motor.Simulate(Vector2.zero, false, .02f);
                float before = motor.VerticalSpeed; motor.Simulate(Vector2.zero, true, .02f);
                Check("No extra jump in empty air", motor.VerticalSpeed < before && !motor.Vaulting);
                Check("No ledge found in empty air", !motor.TryFindLedge(out _, out _));
                motor.Teleport(basis + new Vector3(0, 1.05f, .45f), Quaternion.identity); Physics.SyncTransforms();
                Check("Nearby crate detected", motor.TryFindLedge(out _, out _));
                Check("Vault begins", motor.TryStartVault());
                for (int i = 0; i < Mathf.CeilToInt(motor.vaultDuration/.02f)+5; i++) { motor.Simulate(Vector2.zero, false, .02f); Physics.SyncTransforms(); }
                Check("Vault lands on crate", motor.transform.position.y > basis.y + 1.98f && motor.transform.position.z > basis.z + 1.1f,
                    (motor.transform.position - basis).ToString());
                motor.Teleport(basis + new Vector3(0, 1.05f, .45f), Quaternion.identity);
                var ceiling = Box("Blocked headroom", new Vector3(0, 2 + motor.Capsule.height * .65f, 2), new Vector3(6, .25f, 2)); Physics.SyncTransforms();
                Check("Blocked landing rejected", !motor.TryFindLedge(out _, out _));
                ceiling.SetActive(false);
                crate.transform.localScale = new Vector3(6, 4, 2); crate.transform.position = basis + new Vector3(0, 2, 2); Physics.SyncTransforms();
                Check("Too high ledge rejected", !motor.TryFindLedge(out _, out _));
                crate.transform.localScale = new Vector3(6, 2, 2); crate.transform.position = basis + new Vector3(0, 1, 2); Physics.SyncTransforms();
                motor.Teleport(basis + new Vector3(0, 1.05f, .45f), Quaternion.identity); Physics.SyncTransforms();
                motor.TryStartVault(); crate.SetActive(false); Object.DestroyImmediate(crate);
                motor.Simulate(Vector2.zero, false, .02f);
                Check("Destroyed ledge cancels vault", !motor.Vaulting);
                session.SetRole(true);
                Check("Hand possession", session.Hand.gameObject.activeSelf && !session.Player.gameObject.activeSelf);
                Check("Hand camera follows prefab target", session.Hand.follow.target.IsChildOf(session.Hand.motor.transform));
                Check("Hand single camera", Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1);
                session.SetRole(false);
                Check("Return to survivor", session.Player.gameObject.activeSelf && !session.Hand.gameObject.activeSelf);
                session.ResetCastle();
                Check("Reset retains 568 blocks", session.generator.Bodies.Count == 568);
            }
            finally
            {
                Object.Destroy(fixture);
                session.Player.controlMode = ControlMode.Auto; session.SetRole(false); session.SetMenu(true);
                report.AppendLine("Headset hardware/comfort and network play are not covered by these editor checks.");
                File.WriteAllText("Assets/CastlePrototype/TraversalValidation.txt", report.ToString());
                Debug.Log(report.ToString());
            }
            yield return null;
        }
    }
}


