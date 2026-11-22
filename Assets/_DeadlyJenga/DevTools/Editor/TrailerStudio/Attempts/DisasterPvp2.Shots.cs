using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DeadlyJenga.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The shots, every camera placed from where the take put people at the event frame, the actor layers, and the render.
    public static partial class DisasterPvp2
    {
        sealed class Shot
        {
            public string id, what;
            public int cutIn, cutOut, key;
            public int shift;          // tape frames added so this shot's event lands on its beat (from the take)
            public int Length => cutOut - cutIn;
        }

        static List<Shot> MakeShots(Events e)
        {
            int Shift(int found, int planFrame) => found < 0 ? 0 : Mathf.Clamp(found - TapeFrame(planFrame), -24, 24);
            int s1 = Shift(e.punch1, Punch1Frame), s3 = Shift(e.punch2, Punch2Frame), s4 = Shift(e.planet, PlanetFrame);
            var list = new List<Shot>
            {
                new Shot { id = "S01", what = "hook: the gorilla and the hero square up on the summit, the dragon behind, rocks streak in", cutIn = 0, cutOut = 48, key = 25, shift = s1 },
                new Shot { id = "S02", what = "POV: the hero closes in and the glove throws the gorilla off the summit", cutIn = 48, cutOut = 110, key = Punch1Frame - 48, shift = s1 },
                new Shot { id = "S03", what = "wide: the summit and block 2 at war, the first rock smashes into the tower", cutIn = 110, cutOut = 140, key = WideRockFrame - 110, shift = Shift(e.wideRock, WideRockFrame) },
                new Shot { id = "S04", what = "close: a rock lands between the fox and the penguin mid shoot-out", cutIn = 140, cutOut = 180, key = Rock2Frame - 140, shift = Shift(e.rock2, Rock2Frame) },
                new Shot { id = "S05", what = "POV: the hero turns to find the dragon charging", cutIn = 180, cutOut = 208, key = 14, shift = s3 },
                new Shot { id = "S06", what = "close: the hero's counter-punch throws the dragon off", cutIn = 208, cutOut = 236, key = Punch2Frame - 208, shift = s3 },
                new Shot { id = "S07", what = "MS: the hero looks up, shading his eyes", cutIn = 236, cutOut = 284, key = 32, shift = s4 },
                new Shot { id = "S08", what = "POV payoff: the planet killer fills the sky and lands on the summit", cutIn = 284, cutOut = 318, key = PlanetFrame - 284, shift = s4 },
                new Shot { id = "S09", what = "aftermath: the hero thrown off the summit among the blocks", cutIn = 318, cutOut = 338, key = 6, shift = s4 },
                new Shot { id = "S10", what = "aftermath: the tower top blown apart into the sea", cutIn = 338, cutOut = 359, key = 10, shift = s4 },
                new Shot { id = "S11", what = "title: the wreck settles", cutIn = 359, cutOut = 450, key = 0, shift = s4 },
            };
            Note("Shot shifts (tape frames): " + string.Join(", ", list.Select(s => s.id + " " + s.shift)));
            return list;
        }

        static Vector3 FeetAt(StudioStage stage, string role, int tapeFrame, Plan plan)
        {
            var pose = StudioActorPoses.At(stage.tape, role, tapeFrame / (float)Fps, Fps);
            return pose.found ? pose.feet : plan.at.TryGetValue(role, out var p) ? p : plan.arena.C;
        }

        static Vector3 HeadAt(StudioStage stage, string role, int tapeFrame, Plan plan)
        {
            var pose = StudioActorPoses.At(stage.tape, role, tapeFrame / (float)Fps, Fps);
            return pose.found ? pose.head : FeetAt(stage, role, tapeFrame, plan) + Vector3.up * .85f;
        }

        static StudioCameraTrack ShotCam(Shot shot, float focal)
        {
            var c = StudioMoves.Camera(shot.id, (TapeFrame(shot.cutIn) + shot.shift - Handle) / (float)Fps, (TapeFrame(shot.cutOut) + shot.shift + Handle) / (float)Fps, focal);
            c.id = shot.id; c.name = shot.id; c.sensorHeight = 24; c.keys[0].focal = focal; c.keys[0].aperture = 11; c.keys[0].focus = 6;
            c.vignette = .18f; c.grain = .025f; c.contrast = 10; c.saturation = 8; c.exposure = .1f;
            c.motionBlur = 0;
            foreach (string role in Roles) c.measure.Add("actor:" + role);
            return c;
        }

        static float FocalFor(float distance, float frameHeight) => Mathf.Clamp(24 * distance / frameHeight, 16, 90);

        static List<StudioCameraTrack> ShotCameras(StudioStage stage, Plan plan, Events e, List<Shot> shots)
        {
            var A = plan.arena; Vector3 up = Vector3.up;
            var cams = new List<StudioCameraTrack>();
            var lines = new StringBuilder("Shot cameras (positions world, frame height at the subject):\n");
            Shot Sh(string id) => shots.First(s => s.id == id);
            int Tf(Shot s, int trailerFrame) => TapeFrame(trailerFrame) + s.shift;
            void Add(StudioCameraTrack c, Vector3 at, Vector3 subject, string about)
            {
                float d = Vector3.Distance(at, subject), hf = d * 24 / c.keys[0].focal;
                lines.Append($"- {c.id} at {V(at)}{(Clear(at, .3f) ? "" : " INSIDE GEOMETRY")}, {c.keys[0].focal:0} mm, {d:0.0} m to {about}: Hf {hf:0.00} m, a survivor there is {.94f / hf:0.00} of frame height\n");
                cams.Add(c);
            }
            StudioCameraTrack Pov(Shot s, float move, float turn)
            {
                var c = ShotCam(s, 24);
                c.behavior = "firstPerson"; c.subject = "hero"; c.fpSmoothMove = move; c.fpSmoothTurn = turn; c.fpLevel = true; c.fpFieldOfView = 62; c.fpOwnBody = true;
                lines.Append($"- {c.id} first person (hero), smoothing {move:0.00} s / {turn:0.00} s, 62 deg\n");
                cams.Add(c);
                return c;
            }

            // S01: low beside the summit (planned from the marks), the three summit fighters, slow push in.
            {
                var s = Sh("S01");
                // Round 2: beside the gorilla's end, off the side-on axis: the gorilla near, the hero in the middle, the dragon beyond.
                // Aim at the middle of all three and choose the lens that just holds them (record 4 framed the gorilla out).
                var three = new[] { "hero", "rival", "dragon" }.Select(r => FeetAt(stage, r, Tf(s, 24), plan) + up * .45f).ToList();
                Vector3 mid = (three[0] + three[1] + three[2]) / 3;
                Vector3 look = (mid - plan.hookCam).normalized, side = Vector3.Cross(Vector3.up, look).normalized;
                float half = three.Max(p => Mathf.Abs(Mathf.Atan2(Vector3.Dot(p - plan.hookCam, side), Vector3.Dot(p - plan.hookCam, look))));
                float vHalf = Mathf.Atan(Mathf.Tan(half / .78f) / 1.778f);
                float d = Vector3.Distance(plan.hookCam, mid);
                var c = ShotCam(s, Mathf.Clamp(12 / Mathf.Tan(vHalf), 16, 40));
                StudioMoves.LockedOff(c, c.start, stage.ToStage(plan.hookCam), P(stage, mid));
                StudioMoves.PushIn(c, c.start, c.end, stage.ToStage(mid), .14f);
                c.noise = "subtle"; c.noiseStrength = .6f;
                Add(c, plan.hookCam, mid, "the hero and the gorilla");
            }
            Pov(Sh("S02"), .2f, .32f).fpFieldOfView = 68;
            // S03: the geography wide (planned from the marks), the first rock lands in it.
            {
                var s = Sh("S03");
                Vector3 mid = Vector3.Lerp(A.C, A.F, .5f) - up * .6f;
                float d = Vector3.Distance(plan.wideCam, mid);
                var c = ShotCam(s, FocalFor(d, 9.5f));
                StudioMoves.LockedOff(c, c.start, stage.ToStage(plan.wideCam), P(stage, mid));
                if (e.wideRock >= 0) StudioMoves.Shake(c, (e.wideRock + 1) / (float)Fps, 1.0f, .6f);
                Add(c, plan.wideCam, mid, "the middle of the two blocks");
            }
            // S04: side-on to the fox and the penguin, where they stand just before the rock lands.
            {
                var s = Sh("S04");
                int at = (e.rock2 >= 0 ? e.rock2 : Tf(s, Rock2Frame)) - 4;
                Vector3 fox = FeetAt(stage, "fox", at, plan), pen = FeetAt(stage, "penguin", at, plan);
                Vector3 mid = Vector3.Lerp(fox, pen, .5f);
                // On the same side of their line as the wide before it (critic round 1: S03 to S04 crossed the line).
                Vector3 across = Vector3.Cross(Vector3.up, Flat(fox - pen)).normalized;
                if (Vector3.Dot(across, plan.wideCam - mid) < 0) across = -across;
                Vector3 spot = Spot(mid, 3.5f, .7f, new[] { fox, pen }, across);
                var c = ShotCam(s, FocalFor(Vector3.Distance(spot, mid + up * .5f), 3.3f));
                StudioMoves.LockedOff(c, c.start, stage.ToStage(spot), P(stage, mid + up * .7f));
                if (e.rock2 >= 0) StudioMoves.Shake(c, (e.rock2 + 1) / (float)Fps, 2.2f, .8f);
                Add(c, spot, mid + up * .5f, "the fox and the penguin");
            }
            Pov(Sh("S05"), .22f, .4f).fpFieldOfView = 68;
            // S06: side-on two-shot of the counter-punch, from where both stand at the contact frame.
            {
                var s = Sh("S06");
                int at = e.punch2 >= 0 ? e.punch2 : Tf(s, Punch2Frame);
                Vector3 hero = FeetAt(stage, "hero", at, plan), dragon = FeetAt(stage, "dragon", at, plan);
                Vector3 mid = Vector3.Lerp(hero, dragon, .5f);
                Vector3 line = Flat(dragon - hero), perp = Vector3.Cross(up, line).normalized;
                if (Vector3.Dot(perp, A.s) < 0) perp = -perp;
                Vector3 spot = Spot(mid, 3.1f, .45f, new[] { hero, dragon }, (perp + line * .25f).normalized);
                var c = ShotCam(s, FocalFor(Vector3.Distance(spot, mid + up * .5f), 3.4f));
                StudioMoves.LockedOff(c, c.start, stage.ToStage(spot), P(stage, mid + up * .5f - line * .25f));
                if (e.punch2 >= 0) StudioMoves.Shake(c, (e.punch2 + 1) / (float)Fps, 1.2f, .45f);
                Add(c, spot, mid + up * .5f, "the hero and the dragon");
            }
            // S07: MCU of the hero from the side the planet killer comes from, below his eye line, slow push in.
            {
                var s = Sh("S07");
                // Rehearsal 1: 1.15 m dead ahead, the glove at his face filled the frame. Now 1.5 m out, 35 deg to his left
                // (the glove is in his right hand), under his eye line, toward where the planet killer comes from.
                int at = Tf(s, 266);
                Vector3 head = HeadAt(stage, "hero", at, plan);
                Vector3 toward = Flat(plan.planetFrom), right = Vector3.Cross(Vector3.up, toward).normalized;
                // Rehearsal 2: 35 deg read as the back of a faceless head. 50 deg shows the head's shape tipping up.
                // Round 2 (owner): a faceless body from below with the glove over his head. Now three-quarter front (30 deg to
                // his left), only a little under his eyes, no scream: his head tips up and his left hand shades his eyes.
                Vector3 spot = head + toward * 1.5f - up * .18f;
                foreach (float turn in new[] { 30f, 22f, 38f, 15f, 45f })
                {
                    Vector3 dir = (toward * Mathf.Cos(turn * Mathf.Deg2Rad) - right * Mathf.Sin(turn * Mathf.Deg2Rad)).normalized;
                    Vector3 p = head + dir * 1.5f - up * .18f;
                    if (Clear(p, .25f) && Sees(p, head)) { spot = p; break; }
                }
                // Rehearsal 3: an 80 mm MCU of a faceless head read as the back of it; an MS shows the head tipping up and both hands.
                var c = ShotCam(s, 48);
                c.keys[0].aperture = 11f; c.keys[0].focus = 1.5f;
                StudioMoves.LockedOff(c, c.start, stage.ToStage(spot), "head:hero", new Vector3(0, -.2f, 0));
                StudioMoves.PushIn(c, c.start, c.end, stage.ToStage(head), .15f);
                c.aims[0].blend = .35f;
                c.noise = "subtle"; c.noiseStrength = .5f;
                StudioMoves.Shake(c, Tf(s, 270) / (float)Fps, .6f, 1.2f);   // the ground trembles as the rock comes in
                Add(c, spot, head, "the hero's head");
            }
            Pov(Sh("S08"), .2f, .4f).fpFieldOfView = 80;   // round 2: wider, so a rim of sky keeps the planet killer a rock
            // S09: the hero's fate: the camera rides beside him as the blast throws him off the summit with the blocks.
            {
                var s = Sh("S09");
                Vector3 path = Flat(-plan.planetFrom), perp = Vector3.Cross(up, path).normalized;
                if (Vector3.Dot(perp, A.s) < 0) perp = -perp;
                var c = ShotCam(s, 24);
                StudioMoves.Follow(c, "actor:hero", perp * 3.2f + up * .9f - path * 1.2f, .15f);
                c.aims[0].offset = new Vector3(0, .4f, 0);
                c.lag = .12f;
                Add(c, FeetAt(stage, "hero", Tf(s, 318), plan) + perp * 3.2f, FeetAt(stage, "hero", Tf(s, 318), plan), "the hero (riding beside him)");
            }
            // S10: side-on wide from the sun side, the whole top of the tower and the sea.
            {
                var s = Sh("S10");
                Vector3 path = Flat(-plan.planetFrom), perp = Vector3.Cross(up, path).normalized;
                if (Vector3.Dot(perp, A.s) < 0) perp = -perp;
                Vector3 at = A.C + perp * 52 + path * 6 - up * 5;
                var c = ShotCam(s, 20);
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), P(stage, A.C - up * 9));
                if (e.planet >= 0) StudioMoves.Shake(c, (e.planet + 1) / (float)Fps, 1.4f, 1.2f);
                Add(c, at, A.C, "the summit");
            }
            // S11: high three-quarter from the other side, pulling out slowly over the wreck for the title.
            {
                var s = Sh("S11");
                Vector3 path = Flat(-plan.planetFrom), perp = Vector3.Cross(up, path).normalized;
                if (Vector3.Dot(perp, A.s) < 0) perp = -perp;
                Vector3 dir = (-perp * .6f + path * .8f).normalized;
                Vector3 at = A.C + dir * 44 + up * 14;
                var c = ShotCam(s, 20);
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), P(stage, A.C - up * 14));
                StudioMoves.PushIn(c, c.start, c.end, stage.ToStage(at + dir * 18 + up * 6), .5f);
                Add(c, at, A.C - up * 14, "the wreck");
            }
            Note(lines.ToString());
            return cams;
        }

        // ---- Actor layers, timed from the event table (they change only what is drawn) ----
        static void AddLayers(StudioStage stage, Plan plan, Events e, List<Shot> shots)
        {
            var list = stage.set.overrides; list.Clear();
            float T(int tapeFrame) => tapeFrame / (float)Fps;
            void Add(string role, string kind, int from, int to, string name = "", string target = "", float blendIn = .2f, float blendOut = .3f, Vector3 point = default, float flinch = 0)
            {
                if (stage.set.Mark(role) == null || to <= from) return;
                list.Add(new StudioOverride { actor = role, kind = kind, name = name, target = target, point = point, from = T(from), to = T(to), blendIn = blendIn, blendOut = blendOut, flinch = flinch });
            }
            int end = TapeFrame(LengthFrames) + 30;
            // The opening, out of step (record 5 measured the gorilla and the dragon 1-4 frames from the hero): the gorilla
            // glances past the hero at the dragon early, the dragon fixes on the hero a little later; both hold through S01.
            var opening = shots.First(s => s.id == "S01");
            int o0 = TapeFrame(opening.cutIn) + opening.shift;
            // (Final 1 measured the dragon at 30, the gorilla at 34 and the hero at 35: the dragon now fixes on the hero
            // early and the hero glances up at a streaking rock at about 26, so the starts sit 8 frames apart or more.)
            Add("rival", "lookAt", o0 + 22, o0 + 56, target: "actor:dragon", blendIn: .35f, blendOut: .4f);
            Add("dragon", "lookAt", o0 + 17, o0 + 60, target: "actor:hero", blendIn: .4f, blendOut: .4f);
            Add("hero", "lookAt", o0 + 25, o0 + 44, point: stage.ToStage(plan.arena.C + Vector3.up * 14 + plan.arena.a * 22 + plan.arena.s * 5), blendIn: .35f, blendOut: .35f);   // the sky beyond the gorilla, in front of him (a point behind him spun his head the long way)
            void AddPunch(string victim, int contact)
            {
                Vector3 aim = HeadAt(stage, victim, contact, plan) - Vector3.up * .12f;
                list.Add(new StudioOverride { actor = "hero", kind = "punch", point = stage.ToStage(aim), from = T(contact) - StudioOverrides.PunchContact, to = T(contact) - StudioOverrides.PunchContact + .36f, blendIn = 0, blendOut = 0, strength = 1.7f });
            }
            // The left hand to the brow, following his head: one layer that holds the hand on his own body.
            // (It was a chain of short reaches to fixed points every 5 frames, which made the hand step.)
            void ShadeEyes(int from, int to, float blendIn, float blendOut) =>
                list.Add(new StudioOverride { actor = "hero", kind = "reach", leftHand = true, target = "self", point = new Vector3(-.05f, .06f, .13f), from = T(from), to = T(to + 5), blendIn = blendIn, blendOut = blendOut });
            // Nobody is seen coming back after a fall into the sea (deaths are off on the stage).
            foreach (var r in e.respawn) Add(r.Key, "hide", r.Value - 2, end, blendIn: 0, blendOut: 0);
            Vector3 planetPoint = plan.arena.C + Vector3.up * .85f + plan.planetFrom * 40;
            if (e.punch1 >= 0)
            {
                // Eyes on the gorilla's chest as the hero closes in, the glove arm thrown through the contact frame, a fist pump.
                Vector3 chest = FeetAt(stage, "rival", e.punch1 - 12, plan) + Vector3.up * .5f;
                Add("hero", "lookAt", e.punch1 - 26, e.punch1 - 1, point: stage.ToStage(chest), blendIn: .3f, blendOut: .15f);
                // Round 2 (owner): the reach layer stretched the arm into a ribbon. The game's glove is held, not sprung, so a
                // punch layer throws the arm with the game's punch timing, clamped to its reach, and the body lunges in.
                AddPunch("rival", e.punch1);
                // A person does not track a body leaving at 22 m/s: his eyes jump to where the gorilla is going, then follow him down.
                // Critic round 1: the first-person view dropped to the floor after the hit. The eyes stay on the gorilla's fall.
                // Eased so the first-person head stays under about 350 deg/s (round 1 measured 430).
                Add("hero", "lookAt", e.punch1 + 1, e.punch1 + 24, point: stage.ToStage(HeadAt(stage, "rival", e.punch1 + 14, plan)), blendIn: .32f, blendOut: .35f);
                Add("hero", "lookAt", e.punch1 + 16, e.punch1 + 50, point: stage.ToStage(HeadAt(stage, "rival", e.punch1 + 34, plan)), blendIn: .6f, blendOut: .45f);
                // No cheer: in his own eyes it raised the glove over the lens (final 2). The lowered guard stays on.
                Add("rival", "reaction", e.punch1 + 1, e.punch1 + 70, "scream", blendIn: .05f);
            }
            if (e.punch2 >= 0)
            {
                AddPunch("dragon", e.punch2);
                Add("hero", "lookAt", e.punch2 + 1, e.punch2 + 26, point: stage.ToStage(HeadAt(stage, "dragon", e.punch2 + 14, plan)), blendIn: .12f, blendOut: .35f);
                Add("dragon", "reaction", e.punch2 + 1, e.punch2 + 60, "scream", blendIn: .05f);
            }
            if (e.rock2 >= 0)
            {
                // The rock runs into the penguin (thrown); the fox, 3 m away and watching him, flinches, then looks where he went.
                Add("penguin", "reaction", e.rock2 + 1, e.rock2 + 50, "scream", blendIn: .05f);
                Add("fox", "pose", e.rock2 + 2, e.rock2 + 22, blendIn: .08f, blendOut: .35f, flinch: .9f);
                // The fox's recorded head flips 108 degrees at rock2 + 17 (its drive's neck limit). He holds his eyes on the
                // impact through it, then turns slowly to where the penguin went (round 1: 634 deg/s; final 1: 3311).
                // A chain of looks that overlap by a second, so his recorded head (which flips at rock2 + 17 and again at
                // rock2 + 53) never shows through until S08: the impact, where the penguin went, the hero, the sky.
                Add("fox", "lookAt", e.rock2 + 1, e.rock2 + 62, point: stage.ToStage(plan.rock2), blendIn: .25f, blendOut: .3f);
                Add("fox", "lookAt", e.rock2 + 24, e.rock2 + 100, point: stage.ToStage(HeadAt(stage, "penguin", e.rock2 + 18, plan)), blendIn: .7f, blendOut: .5f);
                Add("fox", "lookAt", e.rock2 + 66, e.rock2 + 128, target: "actor:hero", blendIn: .7f, blendOut: .5f);
                // Witnesses, staggered by attention: the panda was looking elsewhere, close (startle 9-15 f late).
                Add("panda", "pose", e.rock2 + 11, e.rock2 + 30, blendIn: .1f, blendOut: .35f, flinch: .8f);
                Add("panda", "lookAt", e.rock2 + 13, e.rock2 + 48, target: "actor:fox", blendIn: .25f, blendOut: .45f);
                Add("astro", "lookAt", e.rock2 + 19, e.rock2 + 52, target: "actor:penguin", blendIn: .3f, blendOut: .45f);
            }
            // The planet killer: the hero looks up, screams, cowers; the two below look up, each at their own moment.
            int pk = e.planet >= 0 ? e.planet : TapeFrame(PlanetFrame);
            // Rehearsal 1: scream and cower in the first-person payoff put his hands and glove over the lens, then his own legs.
            // So the scream ends as the POV starts (S08 begins about 25 frames before the impact) and the POV only looks up.
            Add("hero", "lookAt", pk - 70, pk + 40, point: stage.ToStage(planetPoint), blendIn: .8f, blendOut: .5f);
            // Round 2: no scream (the glove covered his head). He flinches at the rumble and shades his eyes with his left hand.
            // (Record 4: a flinch pose raised the glove over his head too, and the shading arm filled the payoff POV, so the
            // shading hand stays in the MS only and the payoff POV keeps his left hand low.)
            ShadeEyes(pk - 52, pk - 24, .4f, .3f);
            GuardLow(stage, plan, e, shots);
            Add("panda", "lookAt", pk - 75, pk + 20, point: stage.ToStage(planetPoint), blendIn: .45f, blendOut: .4f);
            Add("astro", "lookAt", pk - 66, pk + 20, point: stage.ToStage(planetPoint), blendIn: .5f, blendOut: .4f);
            Add("panda", "reaction", pk - 30, pk + 20, "cower", blendIn: .2f);
            Add("astro", "reaction", pk - 24, pk + 20, "scream", blendIn: .2f);
            // No "level-head business": a pass that added a random look up or down wherever a head stayed level for 20
            // frames, to satisfy a lint. It made every head nod about on a timer; a fighter's level eyes are natural.
            StudioOverrides.Resolve(stage.set, stage.tape, stage.anchor);
            Note($"Layers: {list.Count} ({string.Join(", ", list.GroupBy(l => l.actor).Select(g => g.Key + " " + g.Count()))}).");
        }

        // The glove guard at the face fills a first-person lens (rehearsal 1: a third of the frame) and hides the face in the
        // close-up. In those shots the hero holds the glove lower, at his chest and to the right, as a player resting the arm
        // would. Each hold is ONE layer that keeps the hand on his own body (reach, target "self"), split only around his
        // punches. (The first two cuts used a chain of short reaches to fixed points every 5 frames: the hands stepped,
        // 55 one-frame pops in 15 s, which the owner saw as twitching arms.)
        static void GuardLow(StudioStage stage, Plan plan, Events e, List<Shot> shots)
        {
            var list = stage.set.overrides; int added = 0;
            var punches = new List<int>(); if (e.punch1 >= 0) punches.Add(e.punch1); if (e.punch2 >= 0) punches.Add(e.punch2);
            // Stretches of [a, b) with the punches cut out (the punch layer owns the arm from 14 frames before to 6 after).
            IEnumerable<(int a, int b)> Outside(int a, int b)
            {
                int from = a;
                foreach (int p in punches.OrderBy(p => p))
                {
                    if (p + 6 <= from || p - 14 >= b) continue;
                    if (p - 14 > from) yield return (from, p - 14);
                    from = Mathf.Max(from, p + 6);
                }
                if (b > from) yield return (from, b);
            }
            void Hold(bool left, Vector3 offset, int a, int b)
            {
                if (b - a < 6) return;
                list.Add(new StudioOverride { actor = "hero", kind = "reach", leftHand = left, target = "self", point = offset, from = a / (float)Fps, to = b / (float)Fps, blendIn = .3f, blendOut = .3f });
                added++;
            }
            foreach (var s in shots.Where(s => s.id == "S02" || s.id == "S05" || s.id == "S08"))
            {
                // Lowered before the cut-in (round 1: glove at the lens on every POV cut). S08 follows the MS: a short pre-roll only.
                int a = TapeFrame(s.cutIn) + s.shift - (s.id == "S08" ? 8 : 14), b = TapeFrame(s.cutOut) + s.shift + Handle;
                // Round 2 (owner): the left hand was the same flat palm in every POV. A guard by the chin before the first
                // punch, an open hand out at the charging dragon, low in the payoff.
                Vector3 leftOffset = s.id == "S02" ? new Vector3(-.13f, -.16f, .30f) : s.id == "S08" ? new Vector3(-.22f, -.38f, .30f) : new Vector3(-.17f, -.22f, .40f);
                foreach (var (x, y) in Outside(a, b))
                {
                    Hold(false, new Vector3(.27f, -.46f, .42f), x, y);
                    Hold(true, leftOffset, x, y);
                }
            }
            // The MS (S07): the glove held low and close at his chest, so his head and the shading hand read.
            var ms = shots.First(s => s.id == "S07");
            Hold(false, new Vector3(.12f, -.32f, .25f), TapeFrame(ms.cutIn) + ms.shift - Handle, TapeFrame(ms.cutOut) + ms.shift + 2);
            Note($"Lowered guard: {added} hold layers on the hero.");
        }

        static void ContactCameras(StudioStage stage, Plan plan, Events events, List<Shot> shots, List<StudioCameraTrack> cameras)
        {
            const int lead = 48, tail = 72;
            int contact = events.punch2;
            if (contact < 0) throw new InvalidOperationException("The take has no second punch event to review.");
            var world = stage.tape.FindType(WorldTrack.Id)?.ActiveTake;
            if (world == null || !world.Covers(contact - lead - Handle) || !world.Covers(contact + tail + Handle - 1))
                throw new InvalidOperationException("The take does not cover the full contact review.");

            int start = contact - TapeFrame(0) - lead;
            var pov = new Shot { id = "POV", cutIn = start, cutOut = start + lead + tail, key = lead };
            var witness = new Shot { id = "Witness", cutIn = pov.cutIn, cutOut = pov.cutOut, key = lead };
            var eye = ShotCam(pov, 24);
            eye.behavior = "firstPerson";
            eye.subject = "hero";
            eye.fpFieldOfView = 68;
            eye.fpLevel = true;
            eye.fpOwnBody = true;
            Vector3 hero = FeetAt(stage, "hero", contact, plan), rival = FeetAt(stage, "dragon", contact, plan);
            // Hold the guard through the approach and release it before the authored strike starts.
            stage.set.overrides.Add(new StudioOverride
            {
                actor = "hero", kind = "reach", target = "self", leftHand = false,
                point = new Vector3(.32f, -.46f, .40f),
                from = (contact - 30) / (float)Fps, to = contact / (float)Fps - StudioOverrides.PunchContact,
                blendIn = .2f, blendOut = .08f
            });
            Vector3 mid = (hero + rival) * .5f;
            Vector3 across = Vector3.Cross(Vector3.up, Flat(rival - hero));
            Vector3 position = Spot(mid, 3.4f, .65f, new[] { hero, rival }, across);
            var bounds = new Bounds(mid, Vector3.zero);
            var endpoints = new List<Vector3>();
            for (int frame = contact - 12; frame <= contact + 9; frame += 3)
                foreach (string role in new[] { "hero", "dragon" })
                {
                    endpoints.Add(FeetAt(stage, role, frame, plan) - across * .18f);
                    endpoints.Add(HeadAt(stage, role, frame, plan) + across * .18f + Vector3.up * .25f);
                }
            foreach (Vector3 point in endpoints) bounds.Encapsulate(point);
            Vector3 aim = bounds.center;
            Quaternion fromCamera = Quaternion.Inverse(Quaternion.LookRotation(aim - position, Vector3.up));
            float opening = .5f;
            foreach (Vector3 point in endpoints)
            {
                Vector3 at = fromCamera * (point - position);
                if (at.z <= .1f) throw new InvalidOperationException("A witness subject passed behind the camera.");
                opening = Mathf.Max(opening, Mathf.Max(Mathf.Abs(at.y), Mathf.Abs(at.x) * 9 / 16) / at.z);
            }
            var side = ShotCam(witness, Mathf.Clamp(12f * .85f / opening, 8f, 24f));
            StudioMoves.LockedOff(side, side.start, stage.ToStage(position), P(stage, aim));
            shots.Clear();
            shots.AddRange(new[] { pov, witness });
            cameras.Clear();
            cameras.AddRange(new[] { eye, side });
            File.WriteAllText(Path.Combine(Folder, "contact.txt"),
                $"Recorded punch cue or inferred health drop at tape frame {contact} ({Fps} fps).\n" +
                $"Both cameras start at tape frame {contact - lead - Handle}; review marker at output frame {lead + Handle}.\n");
        }

        [Serializable] sealed class RenderManifest
        {
            public int version = 1;
            public string revision, takeSha256, setSha256, cueLogSha256, audioSha256;
            public RenderedClip[] clips;
        }

        [Serializable] sealed class RenderedClip
        {
            public string id, sha256;
            public int frames, fps, width, height;
        }

        static string FileHash(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        static string SourceRevision()
        {
            using var git = Process.Start(new ProcessStartInfo("git", "rev-parse --verify HEAD")
            {
                WorkingDirectory = StudioLibrary.ProjectRoot,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (git == null) throw new InvalidOperationException("Could not start Git for render provenance.");
            if (!git.WaitForExit(5000))
            {
                git.Kill();
                throw new TimeoutException("Git did not return the render revision.");
            }
            string revision = git.StandardOutput.ReadToEnd().Trim();
            if (git.ExitCode != 0 || (revision.Length != 40 && revision.Length != 64) || !revision.All(Uri.IsHexDigit))
                throw new InvalidOperationException("Could not identify the render's source revision.");
            return revision;
        }

        static void WriteRenderManifest(StudioStage stage, List<StudioRender.Finished> results, string revision)
        {
            string setPath = Path.Combine(Folder, "set-rendered.json");
            var set = stage.set.Clone();
            set.id = SetId;
            File.WriteAllText(setPath, JsonUtility.ToJson(set, true));
            string cuesPath = Path.Combine(Folder, "cuelog.txt");
            File.WriteAllLines(cuesPath, cueLog);
            string audio = Path.Combine(Folder, "tape.wav");
            string inputTape = Path.Combine(Recording ? Path.Combine(Folder, "bundle") : Bundle, "main.djtape");
            var manifest = new RenderManifest
            {
                revision = revision,
                takeSha256 = FileHash(inputTape),
                setSha256 = FileHash(setPath),
                cueLogSha256 = FileHash(cuesPath),
                audioSha256 = File.Exists(audio) ? FileHash(audio) : "",
                clips = results.Select(r => new RenderedClip
                {
                    id = r.cameraId,
                    frames = r.frames, fps = r.fps, width = r.width, height = r.height,
                    sha256 = FileHash(Path.Combine(Folder, "shots", r.cameraId + ".mp4"))
                }).ToArray()
            };
            string path = Path.Combine(Folder, "render-manifest.json");
            File.WriteAllText(path + ".tmp", JsonUtility.ToJson(manifest, true));
            File.Move(path + ".tmp", path);
        }

        static IEnumerable<int> RenderShots(StudioStage stage, Plan plan, Events e, string mode)
        {
            var shots = MakeShots(e);
            var cameras = ShotCameras(stage, plan, e, shots);
            stage.set.alive.smooth = .12f;   // the driven cast's recorded heads and hands, averaged over about 0.12 s (StudioSmooth)
            AddLayers(stage, plan, e, shots);
            if (mode == "contactReview") ContactCameras(stage, plan, e, shots, cameras);
            stage.set.cameras.Clear(); stage.set.cameras.AddRange(cameras);
            StudioLibrary.Save(stage.set);
            StudioRun.Refresh();
            yield return 0;
            if (mode != "contactReview")
            {
                try { MotionTable(stage, shots, cameras); } catch (Exception error) { Note("Motion table failed: " + error); }
                try { MotionDump(stage, shots); } catch (Exception error) { Note("Joint table failed: " + error); }
            }
            if (mode == "motion") yield break;
            string revision = SourceRevision();
            bool final = mode.StartsWith("final");
            if (mode == "finalA") cameras = cameras.Where(c => c.id == "S01" || c.id == "S02" || c.id == "S03").ToList();
            if (mode == "finalB") cameras = cameras.Where(c => c.id == "S04" || c.id == "S05" || c.id == "S06").ToList();
            if (mode == "finalC") cameras = cameras.Where(c => string.CompareOrdinal(c.id, "S07") >= 0).ToList();
            var settings = final
                ? new StudioRenderSettings { width = 1920, height = 1080, fps = Fps, subFrames = 1, format = "mp4", cinema = true, filmstrip = 12 }
                : mode == "preview" || mode == "contactReview"
                    ? new StudioRenderSettings { width = 640, height = 360, fps = Fps, subFrames = 1, format = "mp4", cinema = false, filmstrip = 10 }
                    : new StudioRenderSettings { width = 480, height = 270, fps = Fps, subFrames = 1, format = "mp4", cinema = false, filmstrip = 8 };
            string renders = Path.Combine(Folder, "renders");
            var clock = Stopwatch.StartNew();
            var hidden = new HashSet<Renderer>();
            void Tidy(Camera _) => HideStrays(hidden);
            StudioRender.BeforeFrame += Tidy;
            try
            {
                StudioRender.Start(cameras, settings, renders);
                while (StudioRender.Running) yield return 0;
            }
            finally { StudioRender.BeforeFrame -= Tidy; }
            Note($"Render {settings.width}x{settings.height}: {StudioRender.Progress} in {clock.Elapsed.TotalSeconds:0} s.");
            var results = StudioRender.Results.ToList();
            if (results.Count != cameras.Count)
                throw new InvalidOperationException($"Expected {cameras.Count} completed shots, received {results.Count}. {StudioRender.Progress}");
            foreach (var camera in cameras)
            {
                var matches = results.Where(r => r.cameraId == camera.id).ToList();
                int frames = StudioCameras.Timeline(camera, stage.set, settings.fps).Count;
                if (matches.Count != 1) throw new InvalidOperationException($"Missing or duplicate render for {camera.id}.");
                var result = matches[0];
                if (result.frames != frames || result.fps != settings.fps || result.width != settings.width ||
                    result.height != settings.height || !string.IsNullOrEmpty(result.error) ||
                    !File.Exists(result.movie) || new FileInfo(result.movie).Length == 0)
                    throw new InvalidOperationException($"Incomplete render for {camera.id}: {result.frames}/{frames} frames. {result.error}");
            }
            KeepShots(results, shots);
            string audio = WorldTakeAudio(stage);
            if (File.Exists(audio)) File.Copy(audio, Path.Combine(Folder, "tape.wav"), true);
            File.WriteAllText(Path.Combine(Folder, "shots.txt"), string.Join("\n", shots.Select(s => $"{s.id} {s.cutIn} {s.cutOut} {s.key} {s.shift} {Handle}")) + "\n");
            if (SourceRevision() != revision) throw new InvalidOperationException("The Git revision changed during rendering.");
            WriteRenderManifest(stage, results, revision);
        }

        // Left-over weapon impact views from the live match draw as big discs in loaded takes: off before every frame.
        static void HideStrays(HashSet<Renderer> hidden)
        {
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r) continue;
                var t = r.transform; bool stray = false;
                for (int up = 0; t && up < 3 && !stray; up++, t = t.parent)
                    stray = t.name.StartsWith("Visual - plasma impact") || t.name.StartsWith("Visual - steaming neon spike");
                if (!stray) continue;
                r.forceRenderingOff = true; r.enabled = false;
                if (hidden.Add(r)) Note($"Left-over impact view switched off: {r.transform.parent?.name}/{r.name}");
            }
        }

        static void KeepShots(List<StudioRender.Finished> results, List<Shot> shots)
        {
            string dir = Path.Combine(Folder, "shots");
            Directory.CreateDirectory(dir);
            foreach (var result in results)
            {
                var shot = shots.FirstOrDefault(s => s.id == result.cameraId);
                if (shot == null) continue;
                Note($"{shot.id}: {result.frames} frames in {result.seconds:0} s {result.error}");
                if (!string.IsNullOrEmpty(result.movie) && File.Exists(result.movie)) File.Copy(result.movie, Path.Combine(dir, shot.id + ".mp4"), true);
                foreach (string name in new[] { "metrics.csv", "metrics.json", "filmstrip.png" })
                    if (File.Exists(Path.Combine(result.folder, name))) File.Copy(Path.Combine(result.folder, name), Path.Combine(dir, shot.id + "-" + name), true);
                void Still(string label, int frame)
                {
                    string file = Path.Combine(result.folder, $"{Mathf.Clamp(frame, 0, result.frames - 1):D05}.png");
                    if (File.Exists(file)) File.Copy(file, Path.Combine(dir, $"{shot.id}-{label}.png"), true);
                }
                Still("first", Handle); Still("key", Handle + shot.key); Still("last", Handle + shot.Length - 1);
            }
        }
    }
}
