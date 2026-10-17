using System;
using UnityEditor;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.World.Hazards.Editor
{
    public static class HazardContractChecks
    {
        [MenuItem("Tools/Deadly Jenga/Test Disaster Map Contract")]
        public static void Run()
        {
            HazardValidation.ValidateCatalog();
            var previous = Services.World;
            var mapObject = new GameObject("Temporary hazard test map");
            var castleObject = new GameObject("Temporary castle bounds test");
            GameObject instance = null;
            int begins = 0, ends = 0;
            Vector3 eventPosition = Vector3.zero;
            Action<HazardType, Vector3> onBegin = (_, position) => { begins++; eventPosition = position; };
            Action<HazardType, Vector3> onEnd = (_, __) => ends++;
            GameEvents.HazardStarted += onBegin;
            GameEvents.HazardEnded += onEnd;
            try
            {
                var world = mapObject.AddComponent<FakeWorldQuery>();
                Services.World = world;
                mapObject.transform.position = new Vector3(100, 20, -70);
                foreach (var prefab in Resources.Load<HazardCatalog>("DisasterCatalog").prefabs)
                {
                    instance = UnityEngine.Object.Instantiate(prefab);
                    var hazard = instance.GetComponent<MapHazard>();
                    float intensity = hazard.Intensity, duration = hazard.Duration;
                    Require(!hazard.TryValidate(null, out _), "Missing map was accepted.");
                    foreach (float size in new[] { 12f, 200f })
                    {
                        world.playableBounds = new Bounds(new Vector3(3, 8, 5), new Vector3(size, 16, size * .5f));
                        Require(hazard.TryValidate(world, out _), "Valid translated bounds were rejected.");
                        hazard.Begin();
                        if (hazard is IWaveState wave)
                        {
                            Require(hazard.Active && wave.Wave.active && wave.Wave.width >= size * .5f, "Wave did not initialize from the map area.");
                            hazard.End();
                            Require(!wave.Wave.active && !Tracked.Waves.Contains(wave), "Wave did not clean up.");
                        }
                        else { Require(hazard.Active && instance.transform.position == world.PlayableBounds.center &&
                            eventPosition == world.PlayableBounds.center, "Disaster ignored the map area or failed to start."); hazard.End(); }
                        Require(hazard.Intensity == intensity && hazard.Duration == duration, "Map size changed authored settings.");
                    }
                    world.playableBounds = new Bounds(Vector3.zero, new Vector3(0, 10, 10));
                    Require(!hazard.TryValidate(world, out _), "Zero-size bounds were accepted.");
                    world.playableBounds = new Bounds(new Vector3(float.NaN, 0, 0), Vector3.one);
                    Require(!hazard.TryValidate(world, out _), "Nonfinite bounds were accepted.");
                    instance.transform.localScale = Vector3.one * 2;
                    Require(!hazard.TryValidatePrefab(out _), "Scaled disaster root was accepted.");
                    UnityEngine.Object.DestroyImmediate(instance);
                    instance = null;
                }
                Require(begins == ends && begins == Resources.Load<HazardCatalog>("DisasterCatalog").prefabs.Length * 2,
                    "Placeholder lifecycle events were unbalanced.");
                var castle = castleObject.AddComponent<CastleGenerator>();
                castle.generateOnStart = false;
                castleObject.transform.SetPositionAndRotation(new Vector3(-80, 15, 60), Quaternion.Euler(0, 35, 0));
                castleObject.transform.localScale = new Vector3(2, 1.5f, .75f);
                var bounds = castle.PlayableBounds;
                Require(MapHazard.ValidBounds(bounds), "Transformed castle area is invalid.");
                var origin = castleObject.transform.TransformPoint(Vector3.zero);
                Require(bounds.min.x <= origin.x && bounds.max.x >= origin.x && bounds.min.z <= origin.z &&
                    bounds.max.z >= origin.z, "Castle bounds ignored its transform.");
                var box = castleObject.AddComponent<BoxCollider>();
                box.isTrigger = true; box.center = new Vector3(4, 6, 2); box.size = new Vector3(30, 12, 20);
                castle.playableArea = box;
                Physics.SyncTransforms();
                Require(castle.PlayableBounds == box.bounds, "Authored area override was ignored.");
                box.isTrigger = false;
                Require(!MapHazard.ValidBounds(castle.PlayableBounds), "Non-trigger override was accepted.");
                var sample = new WaveFrame { center = new Vector3(40, 0, -30), direction = Vector3.forward, width = 80, depth = 20, height = 15, waterHeight = .08f };
                Require(Mathf.Abs(TsunamiHazard.SurfaceHeight(sample, sample.center + Vector3.forward * 4.4f) - 15.08f) < .001f, "Crest height does not match the profile.");
                Require(float.IsNegativeInfinity(TsunamiHazard.SurfaceHeight(sample, sample.center + Vector3.right * 50)), "Outside-width target was submerged.");
                Require(float.IsNegativeInfinity(TsunamiHazard.SurfaceHeight(sample, sample.center + Vector3.forward * 11)), "Outside-depth target was submerged.");
                Debug.Log("Disaster map contract checks passed: map bounds, lifecycle, translated wave profile and collider override.");
            }
            finally
            {
                GameEvents.HazardStarted -= onBegin; GameEvents.HazardEnded -= onEnd;
                if (instance) UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(castleObject);
                UnityEngine.Object.DestroyImmediate(mapObject);
                Services.World = previous;
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
