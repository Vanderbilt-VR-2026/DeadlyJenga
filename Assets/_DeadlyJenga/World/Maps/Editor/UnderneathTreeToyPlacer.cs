using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype.Editor
{
    // Place toys on Underneath the Tree without entering Play Mode.
    // 1. Place Toys opens a scratch scene with the gifts built (gray box).
    // 2. Move, turn or scale the toys; drag new toy prefabs in from the Project window.
    // 3. Save Toy Placements writes them to the UnderneathTreeToys layout. Toys over a gift
    //    are tied to that gift, so they follow it if the gift layout changes later.
    public static class UnderneathTreeToyPlacer
    {
        const string LayoutFile = "UnderneathTreeToys.asset";
        const string MapPrefab = "Mode_workshop-under-the-tree.prefab";

        [MenuItem("Tools/Deadly Jenga/Underneath the Tree/Place Toys")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find(MapPrefab));
            var copy = (GameObject)Object.Instantiate(prefab);
            copy.name = "Underneath the Tree (toy placing)";
            var map = copy.GetComponentInChildren<UnderneathTreeMap>(true);
            if (!map)
            {
                Debug.LogError("The map prefab has no UnderneathTreeMap.");
                return;
            }
            map.Rebuild();
            var view = SceneView.lastActiveSceneView;
            if (view)
                view.LookAt(map.Center + Vector3.up * 20f, Quaternion.Euler(35f, 0f, 0f), 140f);
            Debug.Log("Move the toys, drag in new toy prefabs, then use Save Toy Placements. " +
                "This scene is scratch: do not save it.");
        }

        [MenuItem("Tools/Deadly Jenga/Underneath the Tree/Save Toy Placements")]
        public static void Save()
        {
            var map = Object.FindFirstObjectByType<UnderneathTreeMap>();
            if (!map)
            {
                Debug.LogError("Open Place Toys first.");
                return;
            }
            Physics.SyncTransforms();
            var layout = AssetDatabase.LoadAssetAtPath<UnderneathTreeToyLayout>(ProjectPath.Find(LayoutFile));
            var course = map.transform.Find("Underneath the Tree Layout/Approved Gift Course");
            var placements = new List<UnderneathTreeToyLayout.Placement>();
            foreach (var toy in Object.FindObjectsByType<UnderneathTreeToy>(FindObjectsSortMode.InstanceID))
            {
                var prefab = toy.source ? toy.source : PrefabUtility.GetCorrespondingObjectFromSource(toy);
                if (!prefab)
                {
                    Debug.LogWarning(toy.name + " is not from a toy prefab, so it was skipped.", toy);
                    continue;
                }
                var place = new UnderneathTreeToyLayout.Placement
                {
                    toy = prefab,
                    size = toy.transform.lossyScale.x / Mathf.Max(.0001f, prefab.transform.localScale.x)
                };
                var gift = GiftUnder(toy);
                var frame = gift ? gift : course ? course : map.transform;
                var local = frame.InverseTransformPoint(toy.transform.position);
                place.onGift = gift ? gift.name : "";
                place.spot = new Vector2(local.x, local.z);
                place.turn = Mathf.Round((Quaternion.Inverse(frame.rotation) * toy.transform.rotation).eulerAngles.y);
                placements.Add(place);
            }
            Undo.RecordObject(layout, "Save toy placements");
            layout.toys = placements.ToArray();
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved " + placements.Count + " toy placements.", layout);
        }

        // The gift directly under the toy's middle, if there is one.
        static Transform GiftUnder(UnderneathTreeToy toy)
        {
            var bounds = new Bounds(toy.transform.position, Vector3.zero);
            foreach (var shape in toy.GetComponentsInChildren<Collider>())
                bounds.Encapsulate(shape.bounds);
            foreach (var shape in toy.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(shape.bounds);
            var start = new Vector3(bounds.center.x, bounds.min.y + .3f, bounds.center.z);
            foreach (var hit in Physics.RaycastAll(start, Vector3.down, 8f, 1 << Layers.Block, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(toy.transform))
                    continue;
                var panel = hit.collider.GetComponentInParent<UnderneathTreeGiftPanel>();
                if (panel && panel.name == "Lid" && panel.transform.parent)
                    return panel.transform.parent;
            }
            return null;
        }
    }
}
