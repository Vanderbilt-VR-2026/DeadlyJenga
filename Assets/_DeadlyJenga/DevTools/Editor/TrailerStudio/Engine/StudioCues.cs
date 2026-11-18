using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.World.Hazards;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // A kind of cue. To add one, implement this in a new file; the Studio finds it by itself.
    public interface IStudioCueType
    {
        string Action { get; }            // saved in the Set
        string Label { get; }             // shown in the Studio
        // Do it through the real gameplay path. Return a short note for the cue log; throw if gameplay refused.
        string Run(StudioStage stage, StudioCue cue);
    }

    public static class StudioCues
    {
        static List<IStudioCueType> all;
        public static IReadOnlyList<IStudioCueType> All => all ??= StudioRegistry.Create<IStudioCueType>().OrderBy(c => c.Label).ToList();
        public static IStudioCueType Get(string action) => All.FirstOrDefault(c => c.Action == action);

        public static string Run(StudioStage stage, StudioCue cue)
        {
            var type = Get(cue.action);
            if (type == null) return cue.action + ": not run (the Studio has no cue of this kind yet)";
            try { return cue.action + " " + type.Run(stage, cue); }
            catch (Exception error) { return cue.action + ": REJECTED. " + error.Message; }
        }
    }

    // Shove the dynamic blocks nearest a point, so a structure starts to fall.
    public sealed class CollapseCue : IStudioCueType
    {
        public string Action => "collapse";
        public string Label => "Collapse";
        public string Run(StudioStage stage, StudioCue cue)
        {
            Vector3 centre = stage.ToWorld(cue.point);
            int count = 0;
            foreach (var body in stage.world.Blocks.Where(b => b && b.gameObject.activeInHierarchy && !b.isKinematic)
                .OrderBy(b => (b.position - centre).sqrMagnitude).Take(10))
            {
                var outward = Vector3.ProjectOnPlane(body.position - centre, Vector3.up);
                body.AddForce((outward.normalized + Vector3.right * .75f + Vector3.up * .12f) * Mathf.Clamp(cue.strength, .1f, 30) * (1 + cue.jitter), ForceMode.VelocityChange);
                count++;
            }
            if (count == 0) throw new InvalidOperationException("No loose blocks near the cue.");
            return $"pushed {count} blocks";
        }
    }

    // Start a production disaster by its prefab id.
    public sealed class HazardCue : IStudioCueType
    {
        public string Action => "hazard";
        public string Label => "Disaster";
        public string Run(StudioStage stage, StudioCue cue)
        {
            // A disaster's own settings (strength, size, speed, heading...) travel as a list of numbers, in the order it lists them.
            var prefab = DemoDisasters.Prefabs.FirstOrDefault(p => p && p.name == cue.hazard);
            int wanted = prefab ? DemoDisasters.Parameters(prefab).Count : 0;
            float[] values = cue.values != null && cue.values.Length == wanted && wanted > 0 ? cue.values : null;
            bool directed = cue.aimed && prefab && prefab.GetComponent<IDirectedHazard>() != null;
            if (directed)
            {
                // The game starts disasters with a random direction. To set the direction, the Studio starts this
                // one itself, the same way the game's own start code does, with the heading set first.
                if (!(prefab.GetComponent<IHazard>() is IMapHazard definition) || !definition.TryValidate(stage.world, out string problem))
                    throw new InvalidOperationException("This map cannot run " + cue.hazard + ".");
                var instance = Object.Instantiate(prefab, stage.world.PlayableBounds.center, Quaternion.identity);
                instance.transform.SetParent(stage.session.transform, true);
                instance.GetComponent<IDirectedHazard>().Heading = cue.heading;
                if (values != null) instance.GetComponent<IDemoHazard>()?.Configure(values);
                var started = instance.GetComponent<IHazard>();
                started.Begin();
                if (!started.Active) { Object.Destroy(instance); throw new InvalidOperationException("The disaster did not start: " + cue.hazard); }
                instance.AddComponent<DemoDisasterLifetime>();
            }
            else if (!DemoDisasters.Execute(stage.session, cue.hazard, values: values)) throw new InvalidOperationException("The disaster did not start: " + cue.hazard);
            if (cue.duration > 0 && prefab)
            {
                // End it after the chosen time, without stopping other disasters.
                var type = prefab.GetComponent<IHazard>()?.Type;
                StudioDirector.At(StudioDirector.Now + cue.duration, () =>
                {
                    foreach (var hazard in stage.session.GetComponentsInChildren<MonoBehaviour>().OfType<IHazard>())
                        if (hazard.Active && hazard.Type == type) hazard.End();
                });
            }
            return cue.hazard + (directed ? $" from heading {cue.heading:0}" : "") + (values != null ? " with its own settings" : "") + (cue.duration > 0 ? $" for {cue.duration:0.#} s" : "");
        }
    }

    // The production tsunami with a chosen heading and height.
    public sealed class TsunamiCue : IStudioCueType
    {
        public string Action => "tsunami";
        public string Label => "Tsunami";
        public string Run(StudioStage stage, StudioCue cue)
        {
            var prefab = DemoDisasters.Prefabs.FirstOrDefault(p => p && p.GetComponent<TsunamiHazard>());
            if (!prefab) throw new InvalidOperationException("The tsunami prefab is missing.");
            var wave = Object.Instantiate(prefab, stage.session.transform).GetComponent<TsunamiHazard>();
            wave.randomDirection = false; wave.approachHeading = cue.heading;
            wave.offshoreDistance = 0; wave.warningTime = 0; wave.riseTime = .8f;
            wave.approachSpeed = cue.strength > 0 ? Mathf.Clamp(cue.strength, 1, 30) : 30;
            wave.height = cue.height > 0 ? cue.height : Mathf.Clamp(stage.anchor.y - stage.world.WaterHeight(stage.anchor) + 8, 18, 100);
            wave.affectPlayers = false;
            wave.Begin();
            if (!wave.Active) throw new InvalidOperationException("The tsunami did not start.");
            return $"heading {wave.approachHeading:F0}, height {wave.height:F0} m";
        }
    }

    public sealed class StopHazardsCue : IStudioCueType
    {
        public string Action => "stopHazards";
        public string Label => "Stop disasters";
        public string Run(StudioStage stage, StudioCue cue)
        {
            DemoDisasters.StopAll(stage.session);
            return "";
        }
    }
}
