using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Applies a Set's overrides to recorded actor poses just before they are shown:
    // look-ats, reaches, pose layers, reactions, emotes, outfit and weapon changes.
    // The take underneath is never changed, and a muted override does nothing.
    public sealed class StudioOverrides : IActorFrameFilter
    {
        public int Order => 10;

        public static readonly List<string> Kinds = new List<string> { "lookAt", "reach", "pose", "reaction", "emote", "outfit", "colorFlash", "equipment", "hitPause" };
        public static readonly List<string> KindLabels = new List<string> { "Look at", "Reach", "Pose", "Reaction", "Emote", "Outfit change", "Color flash", "Weapon swap", "Hit pause" };
        public static readonly List<string> Reactions = new List<string> { "stagger", "knockback", "fall", "tumble", "death", "getUp", "cheer", "scream", "cower" };
        public static readonly List<string> ReactionLabels = new List<string> { "Hit stagger", "Knockback", "Fall", "Tumble", "Death", "Get up", "Cheer", "Scream", "Cower" };

        public bool Wants(StudioStage stage, StudioTrack track)
        {
            foreach (var layer in stage.set.overrides) if (layer.actor == track.id && !layer.muted && layer.kind != "hitPause") return true;
            return false;
        }

        public void Apply(StudioStage stage, StudioTrack track, float time, ActorFrame frame)
        {
            if (!frame.visible) return;
            foreach (var layer in stage.set.overrides)
            {
                if (layer.actor != track.id || layer.muted) continue;
                float weight = layer.Weight(time);
                if (weight <= 0 && layer.kind != "reaction") continue;
                Apply(stage.set, stage.tape, stage.anchor, layer, time, weight, frame);
            }
        }

        // Also used by tests and tools that want an override's result without a stage.
        public static void Apply(StudioSet set, StudioTape tape, Vector3 anchor, StudioOverride layer, float time, float weight, ActorFrame frame)
        {
            if (frame.isHand) return;   // the hand has no head, arms or outfit
            switch (layer.kind)
            {
                case "lookAt":
                    if (Target(set, tape, anchor, layer, time, out var see)) LookAt(frame, see, weight);
                    break;
                case "reach":
                    if (Target(set, tape, anchor, layer, time, out var reach))
                    {
                        // The game's avatar already solves the arm from the hand's position (IK), so moving the hand is enough.
                        if (layer.leftHand) { frame.body.left.position = Vector3.Lerp(frame.body.left.position, reach, weight); frame.body.left.tracked = true; }
                        else { frame.body.right.position = Vector3.Lerp(frame.body.right.position, reach, weight); frame.body.right.tracked = true; }
                    }
                    break;
                case "pose": Pose(frame, layer.crouch, layer.lean, layer.flinch, layer.armRaise, weight); break;
                case "reaction":
                    // A fall or death holds its pose until a later get-up on the same actor takes over.
                    // (Any other reaction placed after it plays on top of the lying pose.)
                    bool stays = layer.name == "death" || layer.name == "fall";
                    if (stays && set.overrides.Any(o => o != layer && o.kind == "reaction" && o.name == "getUp" && !o.muted && o.actor == layer.actor && o.Start > layer.Start && o.Start <= time)) break;
                    React(layer, time, frame, anchor);
                    break;
                case "emote":
                    int emote = Array.IndexOf(Emotes.Survivor, layer.name);
                    if (emote >= 0) frame.color = (frame.color & ~0xFF0000) | ((emote + 1) << 16);
                    break;
                case "outfit": frame.look = layer.look; break;
                case "colorFlash":
                    // Flash between the actor's own color and the chosen one, five times a second.
                    if (Mathf.FloorToInt((time - layer.Start) * 10) % 2 == 0) frame.color = (frame.color & ~0xFF) | (layer.color & 0xFF);
                    break;
                case "equipment":
                    if (Enum.TryParse(layer.name, true, out EquipmentKind kind))
                        frame.equipment = kind == EquipmentKind.None ? "" : JsonUtility.ToJson(new EquipmentState { kind = kind, drawn = true });
                    break;
            }
        }

        // ---- Targets ----

        public static bool Target(StudioSet set, StudioTape tape, Vector3 anchor, StudioOverride layer, float time, out Vector3 point)
        {
            point = anchor + layer.point;
            string target = layer.target ?? "";
            if (target.Length == 0) return true;
            if (target.StartsWith("actor:"))
            {
                var pose = StudioActorPoses.At(tape, target.Substring(6), time, set.fps);
                point = pose.head;
                return pose.found;
            }
            if (target.StartsWith("block:") && int.TryParse(target.Substring(6), out int block))
            {
                var world = tape?.FindType(WorldTrack.Id)?.ActiveTake;
                if (world == null || !WorldTrack.Positions(world, Mathf.RoundToInt(time * set.fps), out var positions, out var on) || block < 0 || block >= positions.Length || !on[block]) return false;
                point = positions[block];
                return true;
            }
            if (target.StartsWith("camera:"))
            {
                var camera = set.cameras.FirstOrDefault(c => c.id == target.Substring(7));
                if (camera == null) return false;
                point = StudioCameras.Evaluate(camera, set, tape, anchor, time, time).position;
                return true;
            }
            if (target == "hazard")
            {
                var hazards = tape?.FindType(HazardTrack.Id)?.ActiveTake;
                // Look for it at this moment; if it is not there yet, look where it will first appear.
                int frame = Mathf.RoundToInt(time * set.fps);
                if (HazardTrack.CenterAt(hazards, frame, out point)) { point.y = Mathf.Max(point.y, anchor.y); return true; }
                if (hazards != null)
                    for (int f = frame; f < hazards.End; f += 3)
                        if (HazardTrack.CenterAt(hazards, f, out point)) { point.y = Mathf.Max(point.y, anchor.y); return true; }
                return false;
            }
            return false;
        }

        // ---- Look-at ----

        // Turn the head toward a point. The neck only turns so far, so the body's facing limits it.
        static void LookAt(ActorFrame frame, Vector3 point, float weight)
        {
            Vector3 toward = point - frame.body.head;
            if (toward.sqrMagnitude < .0001f) return;
            float facing = frame.body.headRotation.eulerAngles.y;
            var wanted = Quaternion.LookRotation(toward).eulerAngles;
            float yaw = facing + Mathf.Clamp(Mathf.DeltaAngle(facing, wanted.y), -85, 85);
            float pitch = Mathf.Clamp(Mathf.DeltaAngle(0, wanted.x), -55, 55);
            frame.body.headRotation = Quaternion.Slerp(frame.body.headRotation, Quaternion.Euler(pitch, yaw, 0), weight);
        }

        // ---- Pose layer ----

        static void Pose(ActorFrame frame, float crouch, float lean, float flinch, float armRaise, float weight)
        {
            var body = frame.body;
            float size = Mathf.Max(.1f, body.scale);
            Quaternion facing = Quaternion.Euler(0, body.headRotation.eulerAngles.y, 0);
            float standing = Mathf.Max(.2f, body.head.y - body.feet.y);
            // Crouch: the head comes down (0 to 1 = up to 60% of standing height).
            body.head.y -= standing * .6f * Mathf.Clamp01(crouch) * weight;
            // Lean: tilt forward (positive) or back, moving the head with it.
            if (Mathf.Abs(lean) > .001f)
            {
                body.headRotation = body.headRotation * Quaternion.Euler(lean * 45 * weight, 0, 0);
                body.head += facing * Vector3.forward * (lean * .3f * size * weight);
            }
            // Flinch: head back and down, hands up in front of the face.
            if (flinch > .001f)
            {
                float amount = Mathf.Clamp01(flinch) * weight;
                body.head += facing * new Vector3(0, -.12f, -.18f) * (size * amount);
                body.headRotation = body.headRotation * Quaternion.Euler(-18 * amount, 0, 0);
                body.left.position = Vector3.Lerp(body.left.position, body.head + facing * new Vector3(-.16f, .0f, .28f) * size, amount);
                body.right.position = Vector3.Lerp(body.right.position, body.head + facing * new Vector3(.16f, .0f, .28f) * size, amount);
            }
            // Arm raise: both hands up above the shoulders.
            if (armRaise > .001f)
            {
                float amount = Mathf.Clamp01(armRaise) * weight;
                body.left.position = Vector3.Lerp(body.left.position, body.head + facing * new Vector3(-.34f, .38f, .08f) * size, amount);
                body.right.position = Vector3.Lerp(body.right.position, body.head + facing * new Vector3(.34f, .38f, .08f) * size, amount);
            }
            frame.body = body;
        }

        // ---- Reactions ----

        // Beats that normal gameplay does not make well, built from the same four things a performance is
        // made of (feet, head, two hands). They ease in, play, and blend back into the take.
        static void React(StudioOverride layer, float time, ActorFrame frame, Vector3 anchor)
        {
            float start = layer.Start, length = layer.Length;
            if (layer.muted || time < start) return;
            float u = Mathf.Clamp01((time - start) / length);          // 0 at the start of the reaction, 1 at its end
            bool stays = layer.name == "death" || layer.name == "fall";  // these do not blend back by themselves
            if (time > layer.Stop && !stays) return;
            float back = stays ? 1 : 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1 - Mathf.Clamp01(layer.blendOut / length), 1, u));
            float amount = Mathf.Clamp(layer.strength, 0, 3);
            var body = frame.body;
            float size = Mathf.Max(.1f, body.scale);
            Quaternion facing = Quaternion.Euler(0, body.headRotation.eulerAngles.y, 0);
            // Which way the hit pushes: stored by the contact trigger, or straight back by default.
            Vector3 flat = Vector3.ProjectOnPlane(layer.point, Vector3.up);
            Vector3 push = flat.sqrMagnitude > .01f ? flat.normalized : facing * Vector3.back;
            float standing = Mathf.Max(.2f, body.head.y - body.feet.y);
            void Shift(Vector3 move) { body.feet += move; body.head += move; body.left.position += move; body.right.position += move; }
            void Down(float part)   // 0 standing, 1 lying on the ground
            {
                body.head = Vector3.Lerp(body.head, body.feet + push * (.75f * size) + Vector3.up * (.22f * size), part);
                body.headRotation = Quaternion.Slerp(body.headRotation, Quaternion.LookRotation(Vector3.up, -push), part);
                body.left.position = Vector3.Lerp(body.left.position, body.feet + push * (.55f * size) + facing * Vector3.left * (.45f * size) + Vector3.up * .08f, part);
                body.right.position = Vector3.Lerp(body.right.position, body.feet + push * (.55f * size) + facing * Vector3.right * (.45f * size) + Vector3.up * .08f, part);
            }
            switch (layer.name)
            {
                case "stagger":
                {
                    float hit = Mathf.Sin(u * Mathf.PI) * back;
                    Shift(push * (.35f * amount * Mathf.SmoothStep(0, 1, Mathf.Min(1, u * 3)) * back));
                    body.head += push * (.18f * hit * amount) + Vector3.down * (.08f * hit);
                    body.headRotation = body.headRotation * Quaternion.Euler(-16 * hit * amount, 0, 8 * hit);
                    break;
                }
                case "knockback":
                {
                    // Thrown back fast, slowing down, with a low arc; leaning back while flying, then recovering.
                    float fly = 1 - (1 - Mathf.Min(1, u * 1.6f)) * (1 - Mathf.Min(1, u * 1.6f));
                    Shift((push * (1.6f * amount * fly) + Vector3.up * (.35f * amount * Mathf.Sin(Mathf.Min(1, u * 1.6f) * Mathf.PI))) * back);
                    float lean = Mathf.Sin(Mathf.Min(1, u * 1.3f) * Mathf.PI) * back;
                    body.head += push * (.22f * lean) + Vector3.down * (.1f * lean);
                    body.headRotation = body.headRotation * Quaternion.Euler(-25 * lean, 0, 0);
                    body.left.position += (Vector3.up * .25f - push * .2f) * lean; body.right.position += (Vector3.up * .25f - push * .2f) * lean;
                    break;
                }
                case "fall": case "death":
                    Down(Mathf.SmoothStep(0, 1, Mathf.Min(1, u * (layer.name == "death" ? 1.4f : 2f))));
                    break;
                case "tumble":
                {
                    // Roll along the ground: travel, and turn head over heels once.
                    Shift(push * (2.2f * amount * Mathf.SmoothStep(0, 1, u)) * back);
                    float roll = Mathf.Sin(u * Mathf.PI) * back;
                    Vector3 axis = Vector3.Cross(Vector3.up, push);
                    Vector3 pivot = body.feet + Vector3.up * (standing * .35f);
                    Quaternion turn = Quaternion.AngleAxis(360 * Mathf.SmoothStep(0, 1, u) * (back > .99f ? 1 : back), axis);
                    // Ease into and out of the rolled-up pose over the first and last sixth, so nothing pops.
                    float curl = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Min(u, 1 - u) * 6)) * Mathf.Clamp01(back);
                    body.head = Vector3.Lerp(body.head, pivot + turn * (Vector3.up * standing * .45f), curl);
                    body.headRotation = Quaternion.Slerp(body.headRotation, turn * body.headRotation, curl);
                    body.left.position = Vector3.Lerp(body.left.position, pivot + turn * (facing * new Vector3(-.3f, .1f, .1f) * size), curl);
                    body.right.position = Vector3.Lerp(body.right.position, pivot + turn * (facing * new Vector3(.3f, .1f, .1f) * size), curl);
                    break;
                }
                case "getUp":
                    Down(1 - Mathf.SmoothStep(0, 1, u));
                    break;
                case "cheer":
                {
                    float hop = Mathf.Abs(Mathf.Sin(u * length * Mathf.PI * 2.2f)) * back;
                    Shift(Vector3.up * (.12f * hop * amount));
                    float wave = Mathf.Sin(u * length * Mathf.PI * 4.4f);
                    body.left.position = Vector3.Lerp(body.left.position, body.head + facing * new Vector3(-.36f, .42f + .08f * wave, .05f) * size, back);
                    body.right.position = Vector3.Lerp(body.right.position, body.head + facing * new Vector3(.36f, .42f - .08f * wave, .05f) * size, back);
                    break;
                }
                case "scream":
                    body.headRotation = Quaternion.Slerp(body.headRotation, body.headRotation * Quaternion.Euler(-28, 0, 0), back);
                    body.left.position = Vector3.Lerp(body.left.position, body.head + facing * new Vector3(-.17f, .04f, .06f) * size, back);
                    body.right.position = Vector3.Lerp(body.right.position, body.head + facing * new Vector3(.17f, .04f, .06f) * size, back);
                    body.head += facing * Vector3.forward * (.03f * Mathf.Sin(u * length * 40) * back);   // a tremble
                    break;
                case "cower":
                    body.head.y -= standing * .5f * back;
                    body.headRotation = Quaternion.Slerp(body.headRotation, body.headRotation * Quaternion.Euler(35, 0, 0), back);
                    body.left.position = Vector3.Lerp(body.left.position, body.head + facing * new Vector3(-.14f, .16f, .02f) * size, back);
                    body.right.position = Vector3.Lerp(body.right.position, body.head + facing * new Vector3(.14f, .16f, .02f) * size, back);
                    break;
            }
            frame.body = body;
        }

        // ---- Triggers ----

        // Work out when cue-triggered and contact-triggered overrides start, by reading the tape.
        // Returns a line of plain text per override about what was found.
        public static List<string> Resolve(StudioSet set, StudioTape tape, Vector3 anchor)
        {
            var notes = new List<string>();
            var world = tape?.FindType(WorldTrack.Id)?.ActiveTake;
            var hazards = tape?.FindType(HazardTrack.Id)?.ActiveTake;
            foreach (var layer in set.overrides)
            {
                if (string.IsNullOrEmpty(layer.trigger)) { layer.resolvedFrom = -1; continue; }
                string actor = set.Mark(layer.actor)?.actor ?? layer.actor;
                layer.resolvedFrom = -1;
                var track = tape?.Find(layer.actor);
                var take = track?.ActiveTake;
                if (take == null) { notes.Add($"{actor}: no take to check."); continue; }
                if (layer.trigger == "contact" && world != null)
                {
                    // The first frame a recorded block moving faster than 1.5 m/s comes within reach of the actor's body.
                    Vector3[] before = null;
                    for (int f = Mathf.Max(world.start, take.start); f < Mathf.Min(world.End, take.End); f++)
                    {
                        if (!WorldTrack.Positions(world, f, out var now, out var on)) break;
                        var pose = ActorFrame.Read(track.type, take.frames[f - take.start]);
                        if (before != null && pose.visible)
                        {
                            Vector3 chest = Vector3.Lerp(pose.body.feet, pose.body.head, .6f);
                            for (int i = 0; i < now.Length; i++)
                            {
                                if (!on[i]) continue;
                                Vector3 velocity = (now[i] - before[i]) * set.fps;
                                if (velocity.magnitude < 1.5f || Vector3.Distance(now[i], chest) > 1.0f) continue;
                                layer.resolvedFrom = f / (float)set.fps;
                                layer.point = velocity;   // which way the hit pushes
                                notes.Add($"{actor}: block #{i} reaches them at {layer.resolvedFrom:0.00} s moving {velocity.magnitude:0.0} m/s.");
                                break;
                            }
                        }
                        if (layer.resolvedFrom >= 0) break;
                        before = now;
                    }
                    if (layer.resolvedFrom < 0) notes.Add($"{actor}: no recorded block touches them, so the reaction keeps its own time.");
                }
                else if (layer.trigger.StartsWith("cue:"))
                {
                    var cue = set.cues.FirstOrDefault(c => c.id == layer.trigger.Substring(4));
                    if (cue == null) { notes.Add($"{actor}: the cue it waits for is gone."); continue; }
                    layer.resolvedFrom = cue.at;
                    // A disaster cue: wait until the disaster itself gets close to the actor.
                    if (hazards != null && (cue.action == "hazard" || cue.action == "tsunami"))
                        for (int f = Mathf.RoundToInt(cue.at * set.fps); f < Mathf.Min(hazards.End, take.End); f++)
                        {
                            if (!HazardTrack.CenterAt(hazards, f, out var centre) || f < take.start) continue;
                            var pose = ActorFrame.Read(track.type, take.frames[f - take.start]);
                            if (!pose.visible || Vector3.ProjectOnPlane(centre - pose.body.feet, Vector3.up).magnitude > Mathf.Max(4, layer.strength * 8)) continue;
                            layer.resolvedFrom = f / (float)set.fps;
                            break;
                        }
                    notes.Add($"{actor}: starts at {layer.resolvedFrom:0.00} s (cue \"{cue.action}\").");
                }
            }
            return notes;
        }

        // ---- Hit pause ----

        // The tape frame playback should show for "frame": inside a hit pause it stays on the pause's first frame.
        public static int Held(StudioSet set, int frame)
        {
            if (set == null) return frame;
            foreach (var layer in set.overrides)
            {
                if (layer.kind != "hitPause" || layer.muted) continue;
                int from = Mathf.RoundToInt(layer.Start * set.fps), to = Mathf.RoundToInt(layer.Stop * set.fps);
                if (frame >= from && frame < to) return from;
            }
            return frame;
        }
    }
}
