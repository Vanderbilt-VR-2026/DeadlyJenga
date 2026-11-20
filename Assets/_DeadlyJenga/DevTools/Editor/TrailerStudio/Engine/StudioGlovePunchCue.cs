using System;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // A boxing-glove punch from one live actor to another: first through the real equipment command (the
    // "fire" cue). If gameplay refuses it, the target gets the same push and damage the glove gives
    // (MatchModeRuntime.AttackEquipment: 22 m/s along the punch plus 6 m/s up, 80 damage), and the cue log says so.
    public sealed class GlovePunchCue : IStudioCueType
    {
        public const string Id = "glovePunch";
        public string Action => Id;
        public string Label => "Boxing glove punch";

        public string Run(StudioStage stage, StudioCue cue)
        {
            string real;
            try { real = new FireCue().Run(stage, new StudioCue { action = "fire", actor = cue.actor, target = cue.target, at = cue.at, optional = true }); }
            catch (Exception error) { real = "refused: " + error.Message; }
            if (real.EndsWith(": True")) return "real glove punch (" + real + ")";
            var shooter = stage.Live(cue.actor);
            var victim = stage.Live(cue.target);
            if (!shooter || !victim) throw new InvalidOperationException("Both actors must be live.");
            Vector3 direction = Vector3.ProjectOnPlane(victim.transform.position - shooter.transform.position, Vector3.up).normalized;
            victim.GetComponent<IPushable>()?.Push(direction * 22 + Vector3.up * 6, shooter);
            victim.GetComponent<IDamageable>()?.Damage(80, shooter);
            return "FALLBACK push and damage with the glove's numbers (the equipment command said: " + real + ")";
        }
    }
}
