using System;
using UnityEditor;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;

namespace DeadlyJenga.World.Hazards.Editor
{
    public sealed class TsunamiProbe : MonoBehaviour, IPushable, IHazardMotion, IWaterResistance
    {
        public Vector3 velocity;
        public int calls;
        public float resistance = 1;
        public Vector3 HazardVelocity => velocity;
        public float WaterResistance => resistance;
        public void Push(Vector3 force, GameObject source) { velocity += force; calls++; }
    }

    public static class TsunamiPhysicsChecks
    {
        [MenuItem("Tools/Deadly Jenga/Test Tsunami Forces")]
        public static void Run()
        {
            var oldWorld = Services.World; var oldSession = Services.Session;
            Services.Session = null;
            var root = new GameObject("Temporary tsunami force check");
            GameObject instance = null;
            try
            {
                root.transform.position = new Vector3(10000, 0, -10000);
                var world = root.AddComponent<FakeWorldQuery>();
                Services.World = world;
                world.playableBounds = new Bounds(new Vector3(0, 3, 0), new Vector3(20, 6, 20));
                var normal = CreateProbe(root.transform, 1);
                var resistant = CreateProbe(root.transform, 2);
                var catalog = Resources.Load<HazardCatalog>("DisasterCatalog");
                var prefab = Array.Find(catalog.prefabs, item => item.GetComponent<TsunamiHazard>());
                instance = UnityEngine.Object.Instantiate(prefab);
                var hazard = instance.GetComponent<TsunamiHazard>();
                hazard.randomDirection = false; hazard.approachHeading = 0; hazard.height = 5; hazard.depth = 6;
                hazard.offshoreDistance = 4; hazard.warningTime = .1f; hazard.riseTime = .5f;
                hazard.approachSpeed = 8; hazard.recessionDistance = 2; hazard.fallTime = .5f;
                Physics.SyncTransforms();
                var step = typeof(TsunamiHazard).GetMethod("FixedUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                hazard.Begin();
                bool checkedResistance = false;
                for (int tick = 0; tick < 1000 && hazard.Active; tick++)
                {
                    int before = normal.calls;
                    step.Invoke(hazard, null);
                    Require(normal.calls - before <= 1, "Multiple colliders applied duplicate forces.");
                    Require(normal.velocity.magnitude <= hazard.maximumCarriedSpeed + .001f, "Carried-speed cap exceeded.");
                    if (!checkedResistance && normal.calls > 0 && resistant.calls > 0)
                    {
                        Require(normal.velocity.magnitude > resistant.velocity.magnitude, "Resistance did not reduce push.");
                        checkedResistance = true;
                    }
                }
                Require(!hazard.Active && !Tracked.Waves.Contains(hazard), "Timed wave did not end and unregister.");
                Require(normal.calls > 0 && checkedResistance, "The wave never affected the probes.");
                normal.calls = 0; hazard.affectPlayers = false;
                hazard.Begin();
                for (int tick = 0; tick < 1000 && hazard.Active; tick++) step.Invoke(hazard, null);
                Require(normal.calls == 0, "Affected-target filter ignored Players = off.");
                hazard.Begin(); hazard.End();
                Require(!hazard.Active && !Tracked.Waves.Contains(hazard), "Stop/retrigger leaked an active wave.");
                Debug.Log("Tsunami force checks passed: lifecycle, duplicate colliders, resistance, speed cap, target filter and stop/retrigger.");
            }
            finally
            {
                if (instance) UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(root);
                Services.World = oldWorld; Services.Session = oldSession;
            }
        }
        static TsunamiProbe CreateProbe(Transform root, float resistance)
        {
            var obj = new GameObject("Water target"); obj.transform.SetParent(root, false); obj.transform.localPosition = Vector3.up;
            obj.layer = Layers.Survivor; obj.AddComponent<BoxCollider>();
            var child = new GameObject("Second collider"); child.layer = Layers.Survivor;
            child.transform.SetParent(obj.transform, false); child.AddComponent<BoxCollider>();
            var probe = obj.AddComponent<TsunamiProbe>(); probe.resistance = resistance; return probe;
        }
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
