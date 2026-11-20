using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.World.Hazards;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // One meteor from the game's own meteor disaster, aimed at a chosen spot and arriving at a chosen time.
    // The game picks meteor targets at random; this adds one more meteor to the running disaster's own list,
    // so it flies, hits, pushes, damages and is drawn exactly like the others.
    //   hazard    "MeteorShower" (default) or "PlanetKillerMeteor"; started here with 'values' if it is not running
    //   point     the target, metres from the stage spot
    //   direction from the target toward where the meteor comes from (it is normalised)
    //   radius    how far away it starts, in metres (flight time = radius / speed)
    //   strength  speed in m/s (0 = the game's own: 45, or 28 for the planet killer)
    //   height    diameter in metres (0 = 3, or 45 for the planet killer)
    // The cue's time is when the meteor appears, not when it lands.
    public sealed class AimedMeteorCue : IStudioCueType
    {
        public const string Id = "aimedMeteor";
        public string Action => Id;
        public string Label => "Aimed meteor";

        static readonly FieldInfo Meteors = typeof(BasicDisaster).GetField("meteors", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo Launched = typeof(BasicDisaster).GetField("launched", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo NextId = typeof(BasicDisaster).GetField("meteorId", BindingFlags.Instance | BindingFlags.NonPublic);

        public static float ArrivalDelay(StudioCue cue) => Mathf.Max(1, cue.radius) / Speed(cue);
        static bool Planet(StudioCue cue) => cue.hazard == "PlanetKillerMeteor";
        static float Speed(StudioCue cue) => cue.strength > 0 ? Mathf.Clamp(cue.strength, 5, 120) : Planet(cue) ? 28 : 45;

        public string Run(StudioStage stage, StudioCue cue)
        {
            if (Meteors == null || Launched == null || NextId == null) throw new InvalidOperationException("The meteor disaster's code changed; this cue needs updating.");
            string kind = string.IsNullOrEmpty(cue.hazard) ? "MeteorShower" : cue.hazard;
            var disaster = Running(stage, kind);
            if (disaster == null)
            {
                if (!DemoDisasters.Execute(stage.session, kind, values: cue.values != null && cue.values.Length > 0 ? cue.values : null))
                    throw new InvalidOperationException(kind + " could not be started on this map.");
                disaster = Running(stage, kind) ?? throw new InvalidOperationException(kind + " started but cannot be found.");
            }
            // The planet killer launches its own random rock on its first step; this one replaces it.
            if (Planet(cue)) Launched.SetValue(disaster, true);
            var list = (List<DisasterPoint>)Meteors.GetValue(disaster);
            Vector3 target = stage.ToWorld(cue.point);
            Vector3 from = cue.direction.sqrMagnitude > .0001f ? cue.direction.normalized : new Vector3(.5f, .8f, 0).normalized;
            float distance = Mathf.Max(1, cue.radius), speed = Speed(cue);
            float size = cue.height > 0 ? cue.height : Planet(cue) ? 45 : 3;
            int id = (int)NextId.GetValue(disaster) + 1;
            NextId.SetValue(disaster, id);
            list.Add(new DisasterPoint { id = id, position = target + from * distance, velocity = -from * speed, size = size, life = distance / speed + 4 });
            return $"{kind} rock {size:0.#} m at {target}, lands in {distance / speed:0.00} s";
        }

        static BasicDisaster Running(StudioStage stage, string kind) =>
            stage.session.GetComponentsInChildren<BasicDisaster>().FirstOrDefault(d => d.Active && d.GetType().Name == kind + "Hazard");
    }
}
