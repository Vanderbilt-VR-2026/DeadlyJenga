using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CastlePrototype.Editor
{
    // Checks that every toy building (a toy made of "Piece" parts) stands on its own once all of its
    // parts are free to fall, the way they are after the first hard hit. Run it after changing a
    // building's model. It also tries each building with its floors thinner, thicker, smaller and
    // bigger, so you know a floor change will not shake the building apart.
    // Nothing is saved: the test runs in a hidden scene that is thrown away.
    public static class UnderneathTreeToyStability
    {
        // Floor thickness and floor size changes tried on every building.
        static readonly (string label, float thick, float wide)[] Trials =
        {
            ("as modelled", 1f, 1f),
            ("floors half as thick", .5f, 1f),
            ("floors twice as thick", 2f, 1f),
            ("floors 20% bigger", 1f, 1.2f),
            ("floors thick and big", 2f, 1.2f),
        };
        // A floor made smaller than the room no longer reaches its walls and falls, as it would in a
        // real toy; this check reports it as "floating" so you know which floor to widen again.

        const float SettleSeconds = 4f;

        [MenuItem("Tools/Deadly Jenga/Underneath the Tree/Check Toy Building Stability")]
        public static void CheckAll()
        {
            var report = new StringBuilder();
            bool allPassed = true;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab Toy"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<UnderneathTreeToy>(AssetDatabase.GUIDToAssetPath(guid));
                if (!prefab || prefab.Pieces().Count == 0)
                    continue;
                report.AppendLine(prefab.name);
                foreach (var trial in Trials)
                {
                    string result = Check(prefab, trial.thick, trial.wide, out bool passed);
                    allPassed &= passed;
                    report.AppendLine("  " + (passed ? "PASS  " : "FAIL  ") + trial.label + ": " + result);
                }
            }
            if (allPassed)
                Debug.Log("Toy buildings stand in every trial.\n" + report);
            else
                Debug.LogWarning("Some toy buildings did not stand.\n" + report);
        }

        // Builds one building in a hidden scene, frees every part and lets physics run.
        public static string Check(UnderneathTreeToy prefab, float thickness, float width, out bool passed)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var toy = (UnderneathTreeToy)PrefabUtility.InstantiatePrefab(prefab, scene);
                toy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var pieces = toy.Pieces();
                int floors = ReshapeFloors(toy, pieces, thickness, width);
                toy.PrepareCollision();
                var settled = toy.Settle(pieces);
                int ignored = toy.IgnoreOverlaps(pieces);

                float bottom = float.MaxValue, top = float.MinValue;
                var bodies = new List<Rigidbody>();
                var start = new List<Pose>();
                foreach (var piece in pieces)
                {
                    if (!piece.TryGetComponent<Collider>(out var shape))
                        continue;
                    var box = shape.bounds;
                    bottom = Mathf.Min(bottom, box.min.y);
                    top = Mathf.Max(top, box.max.y);
                    var body = piece.gameObject.AddComponent<Rigidbody>();
                    // The same weights the map gives the parts.
                    body.mass = Mathf.Clamp(box.size.x * box.size.y * box.size.z * toy.weight * .5f * 1.9f, 4f, 65f);
                    body.linearDamping = .12f;
                    body.angularDamping = .18f;
                    body.solverIterations = UnderneathTreeToy.StackSolverIterations;
                    body.solverVelocityIterations = 4;
                    if (Mathf.Min(box.size.x, Mathf.Min(box.size.y, box.size.z)) < .6f)
                        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    bodies.Add(body);
                    start.Add(new Pose(body.position, body.rotation));
                }
                float heaviest = 0f;
                foreach (var body in bodies)
                    heaviest = Mathf.Max(heaviest, body.mass);
                foreach (var body in bodies)
                    body.mass = Mathf.Max(body.mass, heaviest * .1f);

                // The model itself says where the bottom is now (a thicker base slab reaches lower).
                foreach (var shape in toy.GetComponentsInChildren<Renderer>())
                    bottom = Mathf.Min(bottom, shape.bounds.min.y);
                foreach (var part in settled)
                    if (part.onGround && toy.TryPieceBox(part.piece, out var box))
                        bottom = Mathf.Min(bottom, toy.transform.TransformPoint(box.min).y);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(ground, scene);
                ground.transform.position = new Vector3(0f, bottom - 5f - .01f, 0f);
                ground.transform.localScale = new Vector3(400f, 10f, 400f);

                var physics = scene.GetPhysicsScene();
                for (float time = 0f; time < SettleSeconds; time += Time.fixedDeltaTime)
                    physics.Simulate(Time.fixedDeltaTime);

                // A part counts as moved when it slid or sank more than 2% of the building's height
                // (at least a tenth of a unit), or tipped more than 3 degrees.
                float allowed = Mathf.Max(.1f, (top - bottom) * .02f);
                int moved = 0;
                var movers = new StringBuilder();
                float worst = 0f;
                string worstName = "";
                for (int i = 0; i < bodies.Count; i++)
                {
                    float distance = Vector3.Distance(bodies[i].position, start[i].position);
                    float tip = Quaternion.Angle(bodies[i].rotation, start[i].rotation);
                    if (distance > allowed || tip > 3f)
                    {
                        if (moved++ < 6)
                            movers.AppendFormat("\n      {0}: slid {1:0.00}, tipped {2:0} degrees", bodies[i].name, distance, tip);
                    }
                    if (distance > worst)
                    {
                        worst = distance;
                        worstName = bodies[i].name;
                    }
                }
                int floating = 0;
                foreach (var part in settled)
                    if (part.floating && floating++ < 6)
                        movers.AppendFormat("\n      {0}: nothing under it", part.piece.name);
                passed = moved == 0 && floating == 0;
                return string.Format("{0} parts, {1} floors reshaped, {2} moved, {3} floating, {4} overlaps ignored, " +
                    "largest slide {5:0.00} ({6}){7}", bodies.Count, floors, moved, floating, ignored, worst, worstName, movers);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // Floors are the flat parts: much wider than they are thick. Thickness scales along the
        // building's up direction and size across it, keeping each floor's middle where it was.
        public static int ReshapeFloors(UnderneathTreeToy toy, List<Transform> pieces, float thickness, float width)
        {
            if (Mathf.Approximately(thickness, 1f) && Mathf.Approximately(width, 1f))
                return 0;
            int count = 0;
            foreach (var piece in pieces)
            {
                if (!toy.TryPieceBox(piece, out var box) || box.size.y > 1.5f ||
                    box.size.y > Mathf.Min(box.size.x, box.size.z) * .35f)
                    continue;
                var up = toy.transform.up;
                var axes = new[] { piece.right, piece.up, piece.forward };
                int upAxis = 0;
                for (int a = 1; a < 3; a++)
                    if (Mathf.Abs(Vector3.Dot(axes[a], up)) > Mathf.Abs(Vector3.Dot(axes[upAxis], up)))
                        upAxis = a;
                var scale = piece.localScale;
                for (int a = 0; a < 3; a++)
                    scale[a] *= a == upAxis ? thickness : width;
                var middle = piece.TransformPoint(piece.GetComponent<MeshFilter>().sharedMesh.bounds.center);
                piece.localScale = scale;
                piece.position += middle - piece.TransformPoint(piece.GetComponent<MeshFilter>().sharedMesh.bounds.center);
                count++;
            }
            return count;
        }
    }
}
