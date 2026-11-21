using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // A driven actor: a small timed script that plays one survivor mark through the game's own controls
    // (move stick, turning, head direction, jump, climb, punch, fire) while that mark is recorded.
    // The survivor's real movement code moves the body, so the take is a real performance.
    // See StudioPerformer for what each step does and the human touches added on top.
    [Serializable] public sealed class StudioDrive
    {
        public string actor = "";         // mark id (a Survivor mark; it is spawned as a stand-in body and driven)
        public bool muted;                // true = the mark is a plain stand-in again
        public int seed = 1;              // the human touches (glances, curves, overshoot) come from this
        public float human = 1;           // 0 = exact and robotic, 1 = normal person, up to 2 = sloppy
        public bool safeEdges = true;     // never walk off an edge unless a step allows it
        public List<StudioDriveStep> steps = new List<StudioDriveStep>();
    }

    [Serializable] public sealed class StudioDriveStep
    {
        // goTo, path, face, look, jump, punch, fire, gear, climb, crouch, stop, wait
        public string action = "goTo";
        public float at = -1;             // tape seconds this step may start (-1 = as soon as the step before ends)
        public float duration;            // wait/stop/crouch/look: seconds; goTo/path/climb/punch: give up after this (0 = default)
        public Vector3 point;             // metres from the stage spot (goTo, climb, jump-at-a-place, face/look when target is empty)
        public List<Vector3> points = new List<Vector3>();   // path: waypoints, metres from the stage spot
        public string target = "";        // face/look/punch/fire: "actor:<mark id>", "head:<mark id>", "point:x,y,z", "block:<index>", "hazard"
        public string pace = "walk";      // walk, jog, run, or a stick amount 0..1
        public bool edge;                 // true = this step may go over an edge (jump down, leave the tower)
        public float amount = 1;          // crouch: 0..1 depth
        public float reach = 1.05f;       // punch: how close (metres, feet to feet) before swinging
        public string weapon = "";        // gear: an EquipmentKind name (BoxingGlove, Blaster...)
    }

    // The always-on "alive" layer: small human motion on every recorded survivor whenever no stronger
    // layer owns that part of the body. See StudioAlive.
    [Serializable] public sealed class StudioAliveSettings
    {
        public bool on = true;            // false = off for the whole Set
        public int seed;                  // added to every actor's own seed (change it for a different but repeatable result)
        public float strength = 1;        // 0..2 for everything below
        public float head = 1;            // glances: fixations of 0.4-2 s, quick eased head moves
        public float body = 1;            // weight shifts, small steps, sway, small turns
        public float hands = 1;           // hand and arm rest, guard and fidget changes; arm swing while moving
        public float breath = 1;          // breathing
        public float glanceRange = 14;    // metres: other actors nearer than this are looked at
        public List<StudioAliveActor> actors = new List<StudioAliveActor>();
    }

    [Serializable] public sealed class StudioAliveActor
    {
        public string actor = "";         // mark id
        public bool off;                  // true = no alive layer on this actor
        public float strength = 1;        // this actor's own amount (multiplies the Set's)
        public int seed;                  // added to the actor's seed
    }
}
