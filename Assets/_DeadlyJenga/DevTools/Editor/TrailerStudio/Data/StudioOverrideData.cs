using System;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Something layered on top of an actor's recorded take for a stretch of time: a look-at, a reach,
    // a pose, a reaction, an emote, an outfit change... The take underneath is never changed, and any
    // override can be muted. (Phase 4.)
    [Serializable] public sealed class StudioOverride
    {
        public string id = Guid.NewGuid().ToString("N").Substring(0, 8);
        public string actor = "";         // mark id
        public string kind = "lookAt";    // lookAt, reach, pose, reaction, emote, outfit, colorFlash, equipment, hitPause
        public float from, to = 1;        // tape seconds
        public float blendIn = .2f, blendOut = .25f;
        public bool muted;
        public string name = "";          // which reaction, emote, pose preset or weapon
        public float strength = 1;

        // What it points at: another actor, a block, a camera, or a fixed point.
        public string target = "";        // "", "actor:<mark id>", "block:<index>", "camera:<camera id>", "hazard", "point:x,y,z"
        public Vector3 point;             // metres from the stage spot (used when target is empty)
                                          // Any other target text stops StudioOverrides.Resolve and renders with an error.
        public bool leftHand;             // reach: which hand

        // Pose layer amounts.
        public float crouch, lean, flinch, armRaise;

        // What starts it: its own time, a cue, or a recorded block touching the actor.
        public string trigger = "";       // "", "cue:<cue id>", "contact"
        public float resolvedFrom = -1;   // tape seconds worked out for cue and contact triggers (-1 = not worked out yet)

        // Outfit and color changes.
        public AvatarAppearance look;
        public int color;

        public float Length => Mathf.Max(.01f, to - from);
        public float Start => resolvedFrom >= 0 ? resolvedFrom : from;
        public float Stop => Start + Length;

        // 0 outside the range, easing up to 1 inside it.
        public float Weight(float time)
        {
            float start = Start, stop = Stop;
            if (muted || time < start || time > stop) return 0;
            float rise = blendIn <= 0 ? 1 : Mathf.Clamp01((time - start) / blendIn);
            float fall = blendOut <= 0 ? 1 : Mathf.Clamp01((stop - time) / blendOut);
            float weight = Mathf.Min(rise, fall);
            return weight * weight * (3 - 2 * weight) * Mathf.Clamp01(strength);
        }
    }
}
