using DeadlyJenga.DevTools.Editor;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype.Editor
{
    public static class CastleReferenceValidation
    {
        [MenuItem("Tools/Castle Prototype/Validate Confirmed Structure (Play Mode) %#F10")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new System.InvalidOperationException("Enter Play Mode first.");
            var session = Object.FindFirstObjectByType<CastlePlaySession>();
            session.StartCoroutine(Validate(session));
        }
        public static IEnumerator Validate(CastlePlaySession session)
        {
            var report = new StringBuilder();
            void Check(string label, bool pass, string detail = "") => report.AppendLine((pass ? "PASS " : "FAIL ") + label + " " + detail);
            var settings = Object.FindAnyObjectByType<CastleGenerator>().settings;
            var layout = CastleLayout.Build(settings);
            var keep = layout.Where(p => p.Section == CastleSection.Keep).ToArray();
            Check("568 identical crates", layout.Count == 568 && Object.FindAnyObjectByType<CastleGenerator>().Bodies.All(b => b.transform.localScale == new Vector3(6, 2, 2)));
            Check("Four crates on every keep layer", keep.GroupBy(p => p.Course).All(g => g.Count() == 4));
            Check("Keep alternates 0/45 degrees without accumulated twist", keep.All(p =>
                Mathf.Abs(Mathf.DeltaAngle(p.Rotation.eulerAngles.y % 90, (p.Course % 2) * 45)) < .01f));
            Check("Keep repeats positions after two layers", keep.Where(p => p.Course >= 2).All(p => keep.Any(q =>
                q.Course == p.Course - 2 && Vector3.Distance(p.Position - Vector3.up * 4, q.Position) < .001f)));
            Check("Crate height exceeds player", settings.blockSize.y == 2 && session.Survivor().Capsule.height < 2);

            var fixture = new GameObject("Reference interaction validation");
            var motor = Object.Instantiate(session.survivorPrefab, fixture.transform).GetComponent<CastleSurvivor>();
            motor.enabled = false; motor.viewCamera.gameObject.SetActive(false); motor.RefreshMode();
            var crate = Object.Instantiate(Object.FindAnyObjectByType<CastleGenerator>().blockPrefab, fixture.transform);
            crate.transform.position = new Vector3(180, 11, 180); crate.transform.localScale = settings.blockSize;
            var block = crate.GetComponent<CastleBlock>(); var rb = crate.GetComponent<Rigidbody>();
            rb.position = new Vector3(180, 11, 180); rb.rotation = Quaternion.identity;
            block.SyncSurface(); Physics.SyncTransforms();
            rb.useGravity = false; rb.mass = 1; // Deliberately light and unsupported: even a small player impulse would be obvious.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(fixture.transform);
            floor.transform.position = new Vector3(180, 9.5f, 180); floor.transform.localScale = new Vector3(30, 1, 30);
            var appearance = block.appearance; var savedPrefab = appearance.visualPrefab; var savedMaterial = appearance.materialOverride;
            GameObject replacement = null; Material replacementMaterial = null;
            float oldCapture = Time.captureDeltaTime; Time.captureDeltaTime = .02f;
            try
            {
                yield return new WaitForFixedUpdate(); block.SyncSurface(); Physics.SyncTransforms();
                Vector3 start = rb.position;
                Check("Test crate physics pose initialized", Vector3.Distance(start, new Vector3(180,11,180)) < .03f, start.ToString());
                motor.Teleport(new Vector3(180, 10.04f, 178.4f), Quaternion.identity);
                for (int i = 0; i < 75; i++)
                { motor.Simulate(Vector2.up, false, .02f); yield return new WaitForFixedUpdate(); }
                Check("Player blocked by crate face", motor.transform.position.z < 179, motor.transform.position.ToString());
                Check("Walking cannot move 1kg crate", Vector3.Distance(rb.position, start) < .001f && rb.linearVelocity.magnitude < .001f,
                    $"drift={Vector3.Distance(rb.position, start):F6}m");
                motor.Teleport(new Vector3(180, 13, 180), Quaternion.identity);
                for (int i = 0; i < 60; i++)
                { motor.Simulate(Vector2.zero, false, .02f); yield return new WaitForFixedUpdate(); }
                Check("Landing stands on crate", motor.Grounded && Mathf.Abs(motor.transform.position.y - 12) < .12f);
                Check("Landing cannot move crate", Vector3.Distance(rb.position, start) < .001f);
                motor.Teleport(new Vector3(180, 11.05f, 178.45f), Quaternion.identity); Physics.SyncTransforms();
                bool started = motor.TryStartVault();
                for (int i = 0; i < Mathf.CeilToInt(motor.vaultDuration/.02f)+5; i++)
                { motor.Simulate(Vector2.zero, false, .02f); yield return new WaitForFixedUpdate(); }
                Check("Vault onto dynamic crate surface", started && motor.transform.position.y > 11.98f && motor.transform.position.z > 179.1f);
                Check("Vault cannot move crate", Vector3.Distance(rb.position, start) < .001f);
                motor.gameObject.SetActive(false);
                rb.AddForce(Vector3.right * 2, ForceMode.Impulse); yield return new WaitForFixedUpdate();
                Check("Real crate remains dynamic and responds to impact", !rb.isKinematic && rb.linearVelocity.x > 1);

                int bodyID = rb.GetInstanceID(), colliderID = crate.GetComponent<BoxCollider>().GetInstanceID();
                Vector3 velocity = rb.linearVelocity;
                replacement = GameObject.CreatePrimitive(PrimitiveType.Sphere); replacement.name = "Temporary visual swap";
                replacement.transform.position = new Vector3(220, 0, 220);
                replacementMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")); replacementMaterial.color = Color.yellow;
                appearance.visualPrefab = replacement; appearance.materialOverride = replacementMaterial; block.RefreshAppearance();
                Check("Live artwork swaps without changing physics", rb.GetInstanceID() == bodyID
                    && crate.GetComponent<BoxCollider>().GetInstanceID() == colliderID && rb.linearVelocity == velocity
                    && crate.transform.localScale == settings.blockSize && rb.mass == 1);
                Check("Artwork collider cannot affect physics", block.visualRoot.GetComponentsInChildren<Collider>().All(c => !c.enabled));
                Check("Live material override reaches artwork", block.visualRoot.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterial == replacementMaterial));
            }
            finally
            {
                appearance.visualPrefab = savedPrefab; appearance.materialOverride = savedMaterial;
                Time.captureDeltaTime = oldCapture;
                Object.Destroy(fixture); if (replacement) Object.Destroy(replacement); if (replacementMaterial) Object.Destroy(replacementMaterial);
                session.SetRole(false); session.SetMenu(true);
                File.WriteAllText(ProjectPath.Report("ReferenceValidation.txt"), report.ToString()); Debug.Log(report.ToString());
            }
        }
    }
}

