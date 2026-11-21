using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
                new Shot { id = "S06", what = "close: the hero's counter-punch throws the dragon off", cutIn = 208, cutOut = 248, key = Punch2Frame - 208, shift = s3 },
                new Shot { id = "S07", what = "MCU: the hero looks up and screams", cutIn = 248, cutOut = 284, key = 20, shift = s4 },
                new Shot { id = "S08", what = "POV payoff: the planet killer fills the sky and lands on the summit", cutIn = 284, cutOut = 318, key = PlanetFrame - 284, shift = s4 },
                new Shot { id = "S09", what = "aftermath: the tower top blown apart into the sea", cutIn = 318, cutOut = 359, key = 10, shift = s4 },
                new Shot { id = "S10", what = "title: the wreck settles", cutIn = 359, cutOut = 450, key = 0, shift = s4 },
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
                Vector3 dragon = FeetAt(stage, "dragon", Tf(s, 20), plan), rival = FeetAt(stage, "rival", Tf(s, 20), plan);
                Vector3 mid = Vector3.Lerp(dragon, rival, .5f) + up * .55f;
                float d = Vector3.Distance(plan.hookCam, mid);
                var c = ShotCam(s, FocalFor(d, 3.4f));
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
                Vector3 spot = Spot(mid, 3.5f, .7f, new[] { fox, pen }, Flat(plan.rockCam - mid));
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
                Vector3 spot = head + toward * 1.6f - up * .45f;
                foreach (float turn in new[] { 50f, 40f, 60f, 30f })
                {
                    Vector3 dir = (toward * Mathf.Cos(turn * Mathf.Deg2Rad) - right * Mathf.Sin(turn * Mathf.Deg2Rad)).normalized;
                    Vector3 p = head + dir * 1.6f - up * .45f;
                    if (Clear(p, .25f) && Sees(p, head)) { spot = p; break; }
                }
                // Rehearsal 3: an 80 mm MCU of a faceless head read as the back of it; an MS shows the head tipping up and both hands.
                var c = ShotCam(s, 50);
                c.keys[0].aperture = 11f; c.keys[0].focus = 1.6f;
                StudioMoves.LockedOff(c, c.start, stage.ToStage(spot), "head:hero", new Vector3(0, -.25f, 0));
                StudioMoves.PushIn(c, c.start, c.end, stage.ToStage(head), .15f);
                c.aims[0].blend = .35f;
                c.noise = "subtle"; c.noiseStrength = .5f;
                StudioMoves.Shake(c, Tf(s, 270) / (float)Fps, .6f, 1.2f);   // the ground trembles as the rock comes in
                Add(c, spot, head, "the hero's head");
            }
            Pov(Sh("S08"), .2f, .4f).fpFieldOfView = 68;
            // S09: side-on wide from the sun side, the whole top of the tower and the sea.
            {
                var s = Sh("S09");
                Vector3 path = Flat(-plan.planetFrom), perp = Vector3.Cross(up, path).normalized;
                if (Vector3.Dot(perp, A.s) < 0) perp = -perp;
                Vector3 at = A.C + perp * 52 + path * 6 - up * 5;
                var c = ShotCam(s, 20);
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), P(stage, A.C - up * 9));
                if (e.planet >= 0) StudioMoves.Shake(c, (e.planet + 1) / (float)Fps, 1.4f, 1.2f);
                Add(c, at, A.C, "the summit");
            }
            // S10: high three-quarter from the other side, pulling out slowly over the wreck for the title.
            {
                var s = Sh("S10");
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
            // Nobody is seen coming back after a fall into the sea (deaths are off on the stage).
            foreach (var r in e.respawn) Add(r.Key, "hide", r.Value - 2, end, blendIn: 0, blendOut: 0);
            Vector3 planetPoint = plan.arena.C + Vector3.up * .85f + plan.planetFrom * 40;
            if (e.punch1 >= 0)
            {
                // Eyes on the gorilla's chest as the hero closes in, the glove arm thrown through the contact frame, a fist pump.
                Vector3 chest = FeetAt(stage, "rival", e.punch1 - 12, plan) + Vector3.up * .5f;
                Add("hero", "lookAt", e.punch1 - 26, e.punch1 - 1, point: stage.ToStage(chest), blendIn: .3f, blendOut: .15f);
                Add("hero", "reach", e.punch1 - 5, e.punch1 + 5, target: "actor:rival", blendIn: .1f, blendOut: .18f);
                // A person does not track a body leaving at 22 m/s: his eyes jump to where the gorilla is going, then follow him down.
                Add("hero", "lookAt", e.punch1 + 1, e.punch1 + 34, point: stage.ToStage(HeadAt(stage, "rival", e.punch1 + 16, plan)), blendIn: .12f, blendOut: .35f);
                Add("hero", "reaction", e.punch1 + 18, e.punch1 + 42, "cheer", blendIn: .15f, blendOut: .3f);
                Add("rival", "reaction", e.punch1 + 1, e.punch1 + 70, "scream", blendIn: .05f);
            }
            if (e.punch2 >= 0)
            {
                Add("hero", "reach", e.punch2 - 5, e.punch2 + 5, target: "actor:dragon", blendIn: .1f, blendOut: .18f);
                Add("hero", "lookAt", e.punch2 + 1, e.punch2 + 26, point: stage.ToStage(HeadAt(stage, "dragon", e.punch2 + 14, plan)), blendIn: .12f, blendOut: .35f);
                Add("dragon", "reaction", e.punch2 + 1, e.punch2 + 60, "scream", blendIn: .05f);
            }
            if (e.rock2 >= 0)
            {
                // The rock runs into the penguin (thrown); the fox, 3 m away and watching him, flinches, then looks where he went.
                Add("penguin", "reaction", e.rock2 + 1, e.rock2 + 50, "scream", blendIn: .05f);
                Add("fox", "pose", e.rock2 + 2, e.rock2 + 22, blendIn: .08f, blendOut: .35f, flinch: .9f);
                Add("fox", "lookAt", e.rock2 + 6, e.rock2 + 44, point: stage.ToStage(HeadAt(stage, "penguin", e.rock2 + 18, plan)), blendIn: .3f, blendOut: .45f);
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
            Add("hero", "reaction", pk - 48, pk - 26, "scream", blendIn: .3f, blendOut: .15f);
            GuardLow(stage, plan, e, shots);
            Add("panda", "lookAt", pk - 75, pk + 20, point: stage.ToStage(planetPoint), blendIn: .45f, blendOut: .4f);
            Add("astro", "lookAt", pk - 66, pk + 20, point: stage.ToStage(planetPoint), blendIn: .5f, blendOut: .4f);
            Add("panda", "reaction", pk - 30, pk + 20, "cower", blendIn: .2f);
            Add("astro", "reaction", pk - 24, pk + 20, "scream", blendIn: .2f);
            StudioOverrides.Resolve(stage.set, stage.tape, stage.anchor);
            BreakLevelHeads(stage, plan, e, shots);
            StudioOverrides.Resolve(stage.set, stage.tape, stage.anchor);
            Note($"Layers: {list.Count} ({string.Join(", ", list.GroupBy(l => l.actor).Select(g => g.Key + " " + g.Count()))}).");
        }

        // Business that keeps heads off the dead level: wherever a visible actor's head stays level for more than 22 frames,
        // a look down at the drop or up at the rocks (seeded, each a different length), never near the actor's own strikes
        // and never in the hero's first-person shots (his eyes are the camera there).
        // The glove guard at the face fills a first-person lens (rehearsal 1: a third of the frame) and hides the face in the
        // close-up. In those shots the hero holds the glove lower, at his chest and to the right, as a player resting the arm
        // would: short reach layers every 5 frames that follow his head, never near his punches.
        static void GuardLow(StudioStage stage, Plan plan, Events e, List<Shot> shots)
        {
            var list = stage.set.overrides; int added = 0;
            var windows = shots.Where(s => s.id == "S02" || s.id == "S05" || s.id == "S08")
                .Select(s => (a: TapeFrame(s.cutIn) + s.shift - Handle - 4, b: TapeFrame(s.cutOut) + s.shift + Handle + 4)).ToList();
            var punches = new List<int>(); if (e.punch1 >= 0) punches.Add(e.punch1); if (e.punch2 >= 0) punches.Add(e.punch2);
            foreach (var (a, b) in windows)
                for (int tf = a; tf < b; tf += 5)
                {
                    if (punches.Any(p => tf > p - 14 && tf < p + 9)) continue;
                    var pose = Shown(stage, "hero", tf);
                    if (pose == null || !pose.visible) continue;
                    Vector3 fwd = Flat(pose.body.headRotation * Vector3.forward), right = Vector3.Cross(Vector3.up, fwd).normalized;
                    Vector3 hand = pose.body.head + fwd * .42f + right * .27f - Vector3.up * .46f;
                    list.Add(new StudioOverride { actor = "hero", kind = "reach", point = stage.ToStage(hand), from = tf / (float)Fps, to = (tf + 10) / (float)Fps, blendIn = .15f, blendOut = .15f });
                    added++;
                }
            Note($"Lowered guard: {added} reach layers on the hero.");
        }

        static void BreakLevelHeads(StudioStage stage, Plan plan, Events e, List<Shot> shots)
        {
            var list = stage.set.overrides; int added = 0;
            var pov = shots.Where(s => s.id == "S02" || s.id == "S05" || s.id == "S08").Select(s => (a: TapeFrame(s.cutIn) + s.shift - 6, b: TapeFrame(s.cutOut) + s.shift + 6)).ToList();
            foreach (string role in Roles)
            {
                if (stage.set.Mark(role) == null) continue;
                var busy = new List<(int a, int b)>();
                if (role == "hero") { busy.AddRange(pov); if (e.punch1 >= 0) busy.Add((e.punch1 - 30, e.punch1 + 12)); if (e.punch2 >= 0) busy.Add((e.punch2 - 30, e.punch2 + 12)); }
                if (role == "rival" && e.punch1 >= 0) busy.Add((e.punch1 - 4, 9999));
                if (role == "dragon" && e.punch2 >= 0) busy.Add((e.punch2 - 40, 9999));
                if ((role == "fox" || role == "penguin") && e.rock2 >= 0) busy.Add((e.rock2 - 3, 9999));
                int run = 0, n = 0;
                for (int tf = TapeFrame(0) - 10; tf < TapeFrame(LengthFrames); tf++)
                {
                    var p = Shown(stage, role, tf);
                    if (p == null || !p.visible || busy.Any(x => tf >= x.a && tf <= x.b)) { run = 0; continue; }
                    var (pitch, _, roll) = Angles(p.body.headRotation);
                    run = Mathf.Abs(pitch) <= 4 && Mathf.Abs(roll) <= 1.5f ? run + 1 : 0;
                    if (run < 20) continue;
                    n++;
                    float r = StudioHuman.Rand(StudioHuman.SeedOf(role), n, 7);
                    int length = 18 + Mathf.RoundToInt(r * 20);
                    // Within about 30 deg of where the head already points (preview 1: looks at the drop behind them swung heads at 700+ deg/s).
                    Vector3 feet = p.body.feet, fwd = Flat(p.body.headRotation * Vector3.forward), side = Vector3.Cross(Vector3.up, fwd);
                    Vector3 look = n % 2 == 1
                        ? feet + (fwd + side * (r - .5f) * .8f).normalized * 3f - Vector3.up * (3 + r * 3)          // down at the block's edge and the drop
                        : p.body.head + (fwd + side * (r - .5f) * .8f).normalized * 8 + Vector3.up * (3.5f + r * 3);   // up at the sky
                    list.Add(new StudioOverride { actor = role, kind = "lookAt", point = stage.ToStage(look), from = (tf - 4) / (float)Fps, to = (tf - 4 + length) / (float)Fps, blendIn = .4f, blendOut = .45f });
                    added++;
                    tf += length + 4; run = 0;
                }
            }
            Note($"Level-head business added: {added} looks.");
        }

        static IEnumerable<int> RenderShots(StudioStage stage, Plan plan, Events e, string mode)
        {
            var shots = MakeShots(e);
            var cameras = ShotCameras(stage, plan, e, shots);
            stage.set.cameras.Clear(); stage.set.cameras.AddRange(cameras);
            AddLayers(stage, plan, e, shots);
            StudioLibrary.Save(stage.set);
            StudioRun.Refresh();
            yield return 0;
            try { MotionTable(stage, shots, cameras); } catch (Exception error) { Note("Motion table failed: " + error); }
            bool final = mode.StartsWith("final");
            if (mode == "finalA") cameras = cameras.Where(c => c.id == "S01" || c.id == "S02" || c.id == "S03").ToList();
            if (mode == "finalB") cameras = cameras.Where(c => c.id == "S04" || c.id == "S05" || c.id == "S06").ToList();
            if (mode == "finalC") cameras = cameras.Where(c => c.id == "S07" || c.id == "S08" || c.id == "S09" || c.id == "S10").ToList();
            var settings = final
                ? new StudioRenderSettings { width = 1920, height = 1080, fps = Fps, subFrames = 1, format = "png", cinema = true, filmstrip = 12 }
                : mode == "preview"
                    ? new StudioRenderSettings { width = 640, height = 360, fps = Fps, subFrames = 1, format = "png", cinema = false, filmstrip = 10 }
                    : new StudioRenderSettings { width = 480, height = 270, fps = Fps, subFrames = 1, format = "png", cinema = false, filmstrip = 8 };
            string renders = Path.Combine(Folder, "renders");
            var clock = Stopwatch.StartNew();
            var hidden = new HashSet<Renderer>();
            void Tidy(Camera _) => HideStrays(hidden);
            StudioRender.BeforeFrame += Tidy;
            StudioRender.Start(cameras, settings, renders);
            try { while (StudioRender.Running) yield return 0; }
            finally { StudioRender.BeforeFrame -= Tidy; }
            Note($"Render {settings.width}x{settings.height}: {StudioRender.Progress} in {clock.Elapsed.TotalSeconds:0} s.");
            KeepShots(StudioRender.Results.ToList(), shots);
            string audio = StudioRender.AudioFile(stage.set.id);
            if (File.Exists(audio)) File.Copy(audio, Path.Combine(Folder, "tape.wav"), true);
            File.WriteAllText(Path.Combine(Folder, "shots.txt"), string.Join("\n", shots.Select(s => $"{s.id} {s.cutIn} {s.cutOut} {s.key} {s.shift} {Handle}")) + "\n");
            File.WriteAllText(Path.Combine(Folder, "set-rendered.json"), JsonUtility.ToJson(stage.set, true));
            if (Directory.Exists(renders)) Directory.Delete(renders, true);
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
