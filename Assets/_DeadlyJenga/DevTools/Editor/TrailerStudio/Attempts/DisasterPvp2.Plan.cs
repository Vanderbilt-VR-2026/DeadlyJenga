using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Where everyone stands, how each survivor is driven, and the disasters.
    public static partial class DisasterPvp2
    {
        // ---- The choreography, in trailer frames (30 fps), on measured onsets of the music edit (music/map/music.yaml) ----
        const int Punch1Frame = 76;        // the hero's glove knocks the gorilla off the summit (onset 76.5)
        const int WideRockFrame = 122;     // the first rock smashes into the tower in the wide (onset 121.5)
        const int Rock2Frame = 154;        // a rock lands between the fox and the penguin (onset 153.6)
        const int ChargeFrame = 182;       // the dragon starts his charge
        const int Punch2Frame = 218;       // the hero's counter-punch throws the dragon (onset 218.1)
        const int PlanetFrame = 309;       // the planet killer lands on the summit (the drop, 308.6, after a stop-down)

        // Lead times (tape seconds between a punch step's start and its contact), measured in rehearsal and corrected here.
        static float Lead1 = .6f, Lead2 = .42f;   // rehearsal 1: the second punch landed 0.40 s after its step began

        const float RockSize = 4.0f, RockSpeed = 45, RockDistance = 60, PlanetSize = 45, PlanetSpeed = 40, PlanetDistance = 130, PlanetElevation = 27;

        sealed class Arena
        {
            public int summit, side2;              // the summit block and the top block the fox and the penguin fight on
            public Vector3 C, a, s, F, a2, s2;     // summit top centre, its long axis (toward the gorilla), its side (toward the sun); the same for block 2
            public float top;
            public readonly List<Vector3> spots = new List<Vector3>();   // free standing spots in the top 6.5 m
        }

        sealed class Plan
        {
            public Arena arena;
            public readonly Dictionary<string, Vector3> at = new Dictionary<string, Vector3>();   // starting feet (world)
            public readonly Dictionary<string, float> yaw = new Dictionary<string, float>();
            public Vector3 planetFrom, wideRock, rock2, wideCam, hookCam, rockCam;
            public readonly List<(Vector3 target, Vector3 from, int frame, string why, float radius)> rocks = new List<(Vector3, Vector3, int, string, float)>();
        }

        static readonly string[] Roles = { "hero", "rival", "dragon", "fox", "penguin", "panda", "astro" };

        static Arena FindArena(StudioStage stage)
        {
            var a = new Arena();
            var blocks = Enumerable.Range(0, stage.blocks.Length).Where(i => stage.blocks[i] && stage.blocks[i].gameObject.activeInHierarchy).ToList();
            var tops = blocks.ToDictionary(i => i, i => TopOf(stage.blocks[i]));
            float highest = blocks.Max(i => tops[i]);
            // The top blocks tie within millimetres: a fixed rule (largest x + z among blocks within 5 cm of the top).
            var level = blocks.Where(i => tops[i] > highest - .05f).OrderByDescending(i => stage.blocks[i].position.x + stage.blocks[i].position.z).ToList();
            a.summit = level[0];
            var s = stage.blocks[a.summit];
            a.top = tops[a.summit];
            var size = StudioHandDirection.BlockSize(s);
            a.a = Flat(s.rotation * (size.x >= size.z ? Vector3.right : Vector3.forward));
            a.C = new Vector3(s.position.x, a.top, s.position.z);
            var sun = RenderSettings.sun ? RenderSettings.sun : Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional).OrderByDescending(l => l.intensity).FirstOrDefault();
            Vector3 toSun = Flat(sun ? -sun.transform.forward : new Vector3(-.56f, .67f, -.49f));
            a.s = Vector3.Cross(Vector3.up, a.a).normalized;
            if (Vector3.Dot(a.s, toSun) < 0) a.s = -a.s;
            // Block 2: the nearest other top block on the sun side (the cameras look from the sun side).
            a.side2 = level.Skip(1).OrderBy(i => Vector3.Distance(Flat0(stage.blocks[i].position), Flat0(s.position)) - Vector3.Dot(Flat0(stage.blocks[i].position - s.position), toSun) * .5f).First();
            var b2 = stage.blocks[a.side2];
            var size2 = StudioHandDirection.BlockSize(b2);
            a.a2 = Flat(b2.rotation * (size2.x >= size2.z ? Vector3.right : Vector3.forward));
            a.F = new Vector3(b2.position.x, tops[a.side2], b2.position.z);
            a.s2 = Vector3.Cross(Vector3.up, a.a2).normalized;
            if (Vector3.Dot(a.s2, Flat0(a.F - a.C)) < 0) a.s2 = -a.s2;   // block 2's side away from the summit
            // The gorilla stands at the end of the summit away from block 2, so he flies out over the sea, not onto it.
            if (Vector3.Dot(a.a, Flat0(a.F - a.C)) > 0) a.a = -a.a;
            int mask = (1 << Layers.Block) | (1 << Layers.Surface);
            foreach (int i in blocks.Where(i => tops[i] > a.top - 6.5f))
            {
                var body = stage.blocks[i];
                var bs = StudioHandDirection.BlockSize(body);
                Vector3 longLocal = bs.x >= bs.z ? Vector3.right : Vector3.forward, wideLocal = bs.x >= bs.z ? Vector3.forward : Vector3.right;
                float length = Mathf.Max(bs.x, bs.z), width = Mathf.Min(bs.x, bs.z);
                for (float t = -length / 2 + .45f; t <= length / 2 - .45f + .001f; t += .6f)
                    foreach (float w in width >= 1.5f ? new[] { -.45f, 0, .45f } : new[] { 0f })
                    {
                        Vector3 p = body.position + body.rotation * (longLocal * t + wideLocal * w);
                        p.y = tops[i];
                        if (!StudioStage.SurfaceBelow(p + Vector3.up * .05f, out var hit) || Mathf.Abs(hit.y - tops[i]) > .12f) continue;
                        if (Physics.CheckCapsule(p + Vector3.up * .45f, p + Vector3.up * 1.35f, .22f, mask, QueryTriggerInteraction.Ignore)) continue;
                        a.spots.Add(p);
                    }
            }
            Note($"Summit block #{a.summit} top centre {V(a.C)} (top y {a.top:0.00}), axis toward the gorilla {V(a.a)}, sun side {V(a.s)}. " +
                 $"Block 2 #{a.side2} top centre {V(a.F)} ({Vector3.Distance(Flat0(a.F), Flat0(a.C)):0.0} m away), axis {V(a.a2)}. Free spots in the top 6.5 m: {a.spots.Count}.");
            return a;
        }

        static bool Standable(Vector3 p)
        {
            int mask = (1 << Layers.Block) | (1 << Layers.Surface);
            return StudioStage.SurfaceBelow(p + Vector3.up * .05f, out var hit) && Mathf.Abs(hit.y - p.y) < .15f &&
                !Physics.CheckCapsule(p + Vector3.up * .45f, p + Vector3.up * 1.2f, .22f, mask, QueryTriggerInteraction.Ignore);
        }

        static float YawTo(Vector3 from, Vector3 to) => StudioHuman.Yaw(Flat0(to - from));
        static bool OpenSky(Vector3 p) => !Physics.Raycast(p + Vector3.up * 1.2f, Vector3.up, 12, (1 << Layers.Block) | (1 << Layers.Surface), QueryTriggerInteraction.Ignore);

        // A clear camera spot about 'distance' from 'centre' that sees every point in 'must', preferring direction 'prefer'.
        static Vector3 Spot(Vector3 centre, float distance, float height, IEnumerable<Vector3> must, Vector3 prefer, IEnumerable<Vector3> avoid = null)
        {
            Vector3 best = centre + prefer * distance + Vector3.up * height; float bestScore = float.MinValue;
            var points = must.ToList(); var others = avoid?.ToList();
            for (int k = 0; k < 36; k++)
                foreach (float m in new[] { 1f, 1.15f, .9f, 1.3f })
                    foreach (float up in new[] { height, height + .5f, height - .3f, height + 1.2f })
                    {
                        Vector3 dir = Quaternion.Euler(0, k * 10, 0) * Vector3.forward;
                        Vector3 p = centre + dir * distance * m + Vector3.up * up;
                        if (!Clear(p, .4f)) continue;
                        int seen = points.Count(t => Sees(p, t + Vector3.up * .6f) && Sees(p, t + Vector3.up * .25f));
                        float blocked = 0;
                        if (others != null)
                            foreach (var o in others)
                            {
                                Vector3 q = o + Vector3.up * .5f, d = centre + Vector3.up * .5f - p;
                                float t = Mathf.Clamp01(Vector3.Dot(q - p, d) / d.sqrMagnitude);
                                float gap = Vector3.Distance(q, p + d * t);
                                if (gap < .8f) blocked = Mathf.Max(blocked, 1 - gap / .8f);
                            }
                        float score = seen * 10 + Vector3.Dot(dir, prefer) * 4 - Mathf.Abs(m - 1) * 3 - Mathf.Abs(up - height) - blocked * 12;
                        if (score > bestScore) { bestScore = score; best = p; }
                    }
            return best;
        }

        static Plan MakePlan(StudioStage stage)
        {
            var plan = new Plan { arena = FindArena(stage) };
            var A = plan.arena; Vector3 up = Vector3.up;
            Vector3 Fix(Vector3 p) { if (Standable(p)) return p; foreach (float d in new[] { .2f, -.2f, .4f, -.4f }) foreach (var dir in new[] { A.a, A.s }) if (Standable(p + dir * d)) return p + dir * d; Note("WARNING spot not standable " + V(p)); return p; }
            plan.at["hero"] = Fix(A.C - A.a * .15f);
            plan.at["rival"] = Fix(A.C + A.a * 2.25f);
            plan.at["dragon"] = Fix(A.C - A.a * 2.45f);
            plan.at["fox"] = Fix(A.F + A.a2 * 1.45f);
            plan.at["penguin"] = Fix(A.F - A.a2 * 1.45f);
            // Two more on the level below, sun side, open sky, away from both fights.
            var below = A.spots.Where(p => p.y < A.top - 1 && p.y > A.top - 3 && OpenSky(p) && Vector3.Dot(Flat0(p - A.C), A.s) > -1
                && Vector3.Distance(p, plan.at["fox"]) > 3.2f && Vector3.Distance(p, plan.at["penguin"]) > 3.2f && Vector3.Distance(Flat0(p), Flat0(A.C)) > 3).ToList();
            var chosen = new List<Vector3>(plan.at.Values);
            foreach (string role in new[] { "panda", "astro" })
            {
                var pick = below.Where(p => chosen.All(c => Vector3.Distance(c, p) > 2.6f)).OrderBy(p => Vector3.Distance(Flat0(p), Flat0(Vector3.Lerp(A.C, A.F, .5f))) - Vector3.Dot(Flat0(p - A.C), A.s) * .4f).Cast<Vector3?>().FirstOrDefault();
                if (pick == null) { Note("WARNING no spot for " + role); continue; }
                plan.at[role] = pick.Value; chosen.Add(pick.Value);
            }
            plan.yaw["hero"] = YawTo(plan.at["hero"], plan.at["rival"]) - 12;
            plan.yaw["rival"] = YawTo(plan.at["rival"], plan.at["hero"]) + 15;
            plan.yaw["dragon"] = YawTo(plan.at["dragon"], plan.at["fox"]);
            plan.yaw["fox"] = YawTo(plan.at["fox"], plan.at["penguin"]) + 20;
            plan.yaw["penguin"] = YawTo(plan.at["penguin"], plan.at["fox"]) - 25;
            foreach (string role in new[] { "panda", "astro" }) if (plan.at.ContainsKey(role)) plan.yaw[role] = YawTo(plan.at[role], A.C) + (role == "panda" ? 40 : -50);
            Note("Cast: " + string.Join("; ", plan.at.Select(p => $"{p.Key} at {V(p.Value)} yaw {plan.yaw[p.Key]:0}")));

            // Cameras the rocks are aimed for (the same rule places them for the shots).
            plan.hookCam = HookSpot(plan);
            plan.wideCam = WideSpot(plan);
            plan.rockCam = RockSpot(plan);
            Note($"Planning cameras: hook {V(plan.hookCam)}, wide {V(plan.wideCam)}, rock {V(plan.rockCam)}.");

            Vector3 heroHead = plan.at["hero"] + up * .85f;
            float SegmentDistance(Vector3 p, Vector3 x0, Vector3 x1) { var d = x1 - x0; float t = Mathf.Clamp01(Vector3.Dot(p - x0, d) / d.sqrMagnitude); return Vector3.Distance(p, x0 + d * t); }
            // Nothing solid on the rock's way in (it must land where it is aimed, not on a block above it).
            bool PathClear(Vector3 target, Vector3 from) => !Physics.Linecast(target + from * RockDistance, target + from * 1.2f, (1 << Layers.Block) | (1 << Layers.Surface) | 1, QueryTriggerInteraction.Ignore);
            bool SafeForHero(Vector3 target, Vector3 from) => SegmentDistance(heroHead, target + from * RockDistance, target) > 5f && Vector3.Distance(Flat0(target), Flat0(plan.at["hero"])) > 5.5f;

            // The opening: rocks streak across the sky behind the brawl (as the hook camera sees it) and fall in the sea.
            {
                Vector3 fwd = Flat(A.C - plan.hookCam), right = Vector3.Cross(Vector3.up, fwd).normalized;
                foreach (var (lat, dist, frame) in new[] { (-40f, 130f, 16), (34f, 160f, 34), (-10f, 190f, 52) })
                {
                    Vector3 target = plan.hookCam + fwd * dist + right * lat; target.y = 0;
                    Vector3 from = (right * (lat < 0 ? 1 : -1) * .8f + up * .5f + fwd * .3f).normalized;
                    plan.rocks.Add((target, from, frame, "hook streak", 230));
                }
            }
            // The wide: the first rock lands on the level below, in the wide camera's view, away from everyone.
            {
                Vector3 wf = Flat(Vector3.Lerp(A.C, A.F, .5f) - plan.wideCam);
                // Rehearsal 1: a rock aimed 6 m down came in through block 2 and threw the fox 30 frames early. Now: the level just
                // below the top (open sky), from steeply above and outside, with a clear path that passes 3 m from everyone.
                var cand = A.spots.Where(p => p.y < A.top - 1 && p.y > A.top - 3 && OpenSky(p) && chosen.All(c => Vector3.Distance(c, p) > 3.6f) && Vector3.Distance(Flat0(p), Flat0(plan.at["hero"])) > 5.5f && Sees(plan.wideCam, p + up * .5f))
                    .OrderBy(p => Vector3.Angle(Flat0(p - plan.wideCam), wf)).ToList();
                foreach (var p in cand)
                {
                    Vector3 from = (up * .88f + Flat(p - A.C) * .47f).normalized;
                    if (!SafeForHero(p, from) || !PathClear(p, from) || chosen.Any(c => SegmentDistance(c + up * .5f, p + from * RockDistance, p) < 3)) continue;
                    plan.wideRock = p; plan.rocks.Add((p + up * .2f, from, WideRockFrame, "wide", RockDistance)); break;
                }
                if (plan.wideRock == Vector3.zero) Note("WARNING no clear spot for the wide's rock");
            }
            // K2: rehearsal 2 dropped the rock steeply between them and it only hurt them. A rock throws whoever it ploughs into,
            // so (as disaster-pvp-1 did) it comes in low (28 deg) along block 2 from the penguin's end and lands 1.2 m short of
            // him: it runs into the penguin, then the fox. Across the rock camera's frame, never toward its lens.
            {
                Vector3 pen = plan.at["penguin"], travel = Flat(plan.at["fox"] - pen);
                Vector3 target = pen - travel * .6f;
                foreach (float k in new[] { 1.2f, 1.0f, .8f, .6f })
                {
                    var p = pen - travel * k;
                    if (StudioStage.SurfaceBelow(p + up * .3f, out var hit) && Mathf.Abs(hit.y - pen.y) < .15f) { target = p; break; }
                }
                Vector3 from = (-travel * Mathf.Cos(28 * Mathf.Deg2Rad) + up * Mathf.Sin(28 * Mathf.Deg2Rad)).normalized;
                if (!SafeForHero(target, from) || !PathClear(target, from)) { from = (-travel * Mathf.Cos(40 * Mathf.Deg2Rad) + up * Mathf.Sin(40 * Mathf.Deg2Rad)).normalized; Note("K2 rock: 28 deg path not clear, using 40 deg"); }
                plan.rock2 = target; plan.rocks.Add((target + up * .2f, from, Rock2Frame, "K2 into the penguin, then the fox", RockDistance));
            }
            // Mayhem in the background: rocks into the levels below, far from the hero.
            {
                var far = A.spots.Where(p => p.y < A.top - 3 && Vector3.Distance(Flat0(p), Flat0(plan.at["hero"])) > 6.5f && chosen.All(c => Vector3.Distance(c, p) > 4)).ToList();
                int n = 0;
                foreach (int frame in new[] { 192, 238, 266 })
                {
                    if (far.Count == 0) break;
                    var p = far[(n * 7 + 3) % far.Count]; n++;
                    Vector3 from = (up * .75f + Flat(p - A.C) * .6f).normalized;
                    if (SafeForHero(p, from)) plan.rocks.Add((p + up * .2f, from, frame, "background", RockDistance));
                }
            }
            // The planet killer comes from beyond the dragon's end of the summit (the way the hero faces after his second punch).
            plan.planetFrom = (-A.a * Mathf.Cos(PlanetElevation * Mathf.Deg2Rad) + up * Mathf.Sin(PlanetElevation * Mathf.Deg2Rad) + A.s * .12f).normalized;
            Note("Rocks: " + string.Join("; ", plan.rocks.Select(r => $"f{r.frame} {r.why} at {V(r.target)} from {V(r.from)}")));
            return plan;
        }

        // The hook: low beside the summit, all three summit fighters in frame.
        static Vector3 HookSpot(Plan plan)
        {
            var A = plan.arena;
            // Round 2: closer and off the flat side-on axis, beside the gorilla's end: gorilla near, hero middle, dragon far.
            return Spot(A.C + A.a * .3f, 2.9f, .5f, new[] { plan.at["hero"], plan.at["rival"], plan.at["dragon"] }, (A.s * .87f + A.a * .5f).normalized);
        }

        // The geography wide: high three-quarter from outside, the summit and block 2 in one frame.
        static Vector3 WideSpot(Plan plan)
        {
            var A = plan.arena;
            Vector3 mid = Vector3.Lerp(A.C, A.F, .5f);
            Vector3 perp = Vector3.Cross(Vector3.up, Flat(A.F - A.C)).normalized;
            if (Vector3.Dot(perp, A.s) < 0) perp = -perp;
            var must = new List<Vector3> { plan.at["hero"], plan.at["dragon"], plan.at["fox"], plan.at["penguin"] };
            if (plan.at.ContainsKey("panda")) must.Add(plan.at["panda"]);
            return Spot(mid, 11f, 4.5f, must, (perp * .8f + Flat(A.F - A.C) * .35f).normalized);
        }

        // The rock on the fox and the penguin: side-on to their line, from outside the tower.
        static Vector3 RockSpot(Plan plan)
        {
            var A = plan.arena;
            Vector3 mid = Vector3.Lerp(plan.at["fox"], plan.at["penguin"], .5f);
            return Spot(mid, 3.6f, .7f, new[] { plan.at["fox"], plan.at["penguin"] }, A.s2);
        }

        static StudioCue Meteor(StudioStage stage, Vector3 target, Vector3 from, int arriveFrame, bool planet, float radius = 0)
        {
            var cue = new StudioCue
            {
                action = AimedMeteorCue.Id, hazard = planet ? "PlanetKillerMeteor" : "MeteorShower", point = stage.ToStage(target), direction = from,
                radius = radius > 0 ? radius : planet ? PlanetDistance : RockDistance, height = planet ? PlanetSize : RockSize, strength = planet ? PlanetSpeed : RockSpeed
            };
            cue.at = Mathf.Max(0, TapeF(arriveFrame) - AimedMeteorCue.ArrivalDelay(cue));
            // The first rock starts the shower: a light one (1.5 rocks a second, 1.8 m, no warning, 14 s) so the brawl stays on its block;
            // force 40 (the most), because at 22 the rock that landed 0.8 m from the penguin in rehearsal 1 only hurt him.
            cue.values = planet ? new[] { PlanetSize, 0f } : new[] { 1.8f, .6f, 1.5f, 40f, 0f, 14f };
            return cue;
        }

        static StudioDriveStep S(string action, float at = -1, string target = "", float duration = 0, string pace = "walk", float reach = 1.0f, string weapon = "") =>
            new StudioDriveStep { action = action, at = at, target = target, duration = duration, pace = pace, reach = reach, weapon = weapon };
        static StudioDriveStep Go(StudioStage stage, Vector3 world, float at = -1, string pace = "walk") =>
            new StudioDriveStep { action = "goTo", at = at, point = stage.ToStage(world), pace = pace, duration = 1.1f };
        static string Pt(StudioStage stage, Vector3 world) => P(stage, world);

        static void Direct(StudioStage stage, Plan plan)
        {
            var set = stage.set; var A = plan.arena; Vector3 up = Vector3.up;
            set.marks.Clear(); set.drives.Clear(); set.overrides.Clear(); set.cues.Clear();
            var skins = new Dictionary<string, AvatarSkin> { ["rival"] = AvatarSkin.Gorilla, ["dragon"] = AvatarSkin.Dragon, ["fox"] = AvatarSkin.Fox, ["penguin"] = AvatarSkin.Penguin, ["panda"] = AvatarSkin.Panda, ["astro"] = AvatarSkin.Astronaut };
            int color = 1;
            foreach (string role in Roles)
            {
                if (!plan.at.ContainsKey(role)) continue;
                var mark = set.AddMark(StudioRole.Survivor, stage.ToStage(plan.at[role]));
                mark.id = role; mark.actor = role; mark.yaw = plan.yaw[role]; mark.color = role == "hero" ? 6 : color++;
                mark.customLook = true;
                mark.appearance = new AvatarAppearance { hair = role == "hero" ? AvatarHair.Spikes : AvatarHair.None, hairShade = AvatarHairShade.Match, skin = skins.TryGetValue(role, out var skin) ? skin : AvatarSkin.Default };
            }

            Vector3 hero = plan.at["hero"], rival = plan.at["rival"], dragon = plan.at["dragon"], fox = plan.at["fox"], penguin = plan.at["penguin"];
            Vector3 sky(Vector3 from, Vector3 dir, float elev) => from + up * .85f + (Flat(dir) * Mathf.Cos(elev * Mathf.Deg2Rad) + up * Mathf.Sin(elev * Mathf.Deg2Rad)) * 25;
            Vector3 hookFwd = Flat(A.C - plan.hookCam);
            float T(float f) => TapeF(f);

            // The hero: circles the gorilla, glances at the falling sky, closes in and punches; watches him fall; looks up at the rocks;
            // turns to find the dragon charging, counter-punches him; looks up at the planet killer and braces.
            var h = new StudioDrive { actor = "hero", seed = 11, human = 1 };
            // Round 2 (owner's notes): the opening was three identical guards moving in step. The hero now fights low (a
            // crouched stance) and is already circling the gorilla when the cut opens.
            h.steps.Add(S("gear", .3f, weapon: "BoxingGlove"));
            h.steps.Add(new StudioDriveStep { action = "crouch", at = .32f, amount = .3f, duration = 2.4f });
            h.steps.Add(S("look", .35f, "actor:rival"));
            h.steps.Add(new StudioDriveStep { action = "path", at = .4f, pace = "walk", duration = 2.3f, points = new List<Vector3> {
                stage.ToStage(A.C - A.a * .1f + A.s * .45f), stage.ToStage(A.C + A.a * .2f + A.s * .4f), stage.ToStage(A.C + A.a * .3f - A.s * .05f) } });
            h.steps.Add(S("punch", T(Punch1Frame) - Lead1, "actor:rival", pace: "jog", reach: 1.0f));
            h.steps.Add(S("look", -1, Pt(stage, A.C + A.a * 7 - up * 9), 1.1f));
            h.steps.Add(S("look", T(112), Pt(stage, sky(A.C, A.F - A.C + A.s * .5f, 32)), 1.0f));
            h.steps.Add(Go(stage, A.C + A.s * .2f, T(118)));
            h.steps.Add(S("look", T(146), "actor:fox", 1.0f));
            h.steps.Add(S("look", T(ChargeFrame + 2), "actor:dragon"));
            h.steps.Add(S("punch", T(Punch2Frame) - Lead2, "actor:dragon", pace: "walk", reach: 1.0f));
            h.steps.Add(S("look", -1, Pt(stage, A.C - A.a * 8 - up * 8), .7f));
            h.steps.Add(S("look", T(256), Pt(stage, A.C + up * .85f + plan.planetFrom * 40)));
            set.drives.Add(h);

            // The gorilla: squares up, feints in, backs off, never sees the punch coming in time.
            var r = new StudioDrive { actor = "rival", seed = 23, human = 1 };
            // He stands tall, glove high, and feints: a jab-step in, back out, then a second step in.
            r.steps.Add(S("gear", .3f, weapon: "BoxingGlove"));
            r.steps.Add(S("look", .5f, "actor:hero"));
            r.steps.Add(Go(stage, rival - A.a * .7f - A.s * .1f, .62f, "jog"));
            // Record 4 measured his starts 2 frames from the hero's: staggered by 5 frames or more now.
            r.steps.Add(Go(stage, rival + A.s * .2f, T(18)));
            r.steps.Add(S("look", T(24), "actor:dragon", .45f));
            r.steps.Add(Go(stage, rival - A.a * .35f, T(43)));
            r.steps.Add(S("look", -1, "actor:hero"));
            r.steps.Add(S("face", -1, "actor:hero", 1.0f));
            set.drives.Add(r);

            // The dragon: watches the shoot-out on block 2, sees the gorilla go, then charges the hero.
            var d = new StudioDrive { actor = "dragon", seed = 31, human = 1 };
            // He is walking along his end when the cut opens, half crouched, watching the shoot-out, then the hero.
            d.steps.Add(S("gear", .3f, weapon: "BoxingGlove"));
            d.steps.Add(new StudioDriveStep { action = "crouch", at = .3f, amount = .14f, duration = 3f });
            d.steps.Add(S("look", .45f, "actor:penguin", 1.3f));
            d.steps.Add(Go(stage, dragon + A.s * .55f + A.a * .3f, .5f));
            d.steps.Add(Go(stage, dragon - A.s * .35f, T(20)));
            d.steps.Add(S("look", T(29), "actor:hero", .8f));
            d.steps.Add(S("look", T(Punch1Frame - 4), "actor:rival", 1.3f));
            d.steps.Add(Go(stage, dragon - A.s * .2f, T(104)));
            d.steps.Add(S("look", T(118), Pt(stage, sky(dragon, A.s, 35)), .8f));
            d.steps.Add(S("look", T(150), "actor:fox", .7f));
            // Back to the very end for a runway, then a real charge (round 2: he walked in).
            d.steps.Add(Go(stage, A.C - A.a * 2.7f, T(158)));
            d.steps.Add(S("look", T(170), "actor:hero"));
            d.steps.Add(new StudioDriveStep { action = "goTo", at = T(ChargeFrame - 4), point = stage.ToStage(A.C - A.a * .9f), pace = "run", duration = 1.4f });
            d.steps.Add(S("face", -1, "actor:hero", 1.0f));
            set.drives.Add(d);

            // The fox and the penguin trade blaster shots on block 2, strafing.
            var fx = new StudioDrive { actor = "fox", seed = 41, human = 1 };
            fx.steps.Add(S("gear", .3f, weapon: "Blaster"));
            fx.steps.Add(S("look", .5f, "actor:penguin"));
            fx.steps.Add(Go(stage, fox + A.s2 * .35f, .9f));
            fx.steps.Add(S("fire", T(40), "actor:penguin"));
            fx.steps.Add(Go(stage, fox - A.s2 * .3f - A.a2 * .2f));
            fx.steps.Add(S("look", T(76), "actor:hero", .6f));
            fx.steps.Add(S("fire", T(100), "actor:penguin"));
            fx.steps.Add(Go(stage, fox + A.s2 * .25f + A.a2 * .15f));
            fx.steps.Add(S("fire", T(140), "actor:penguin"));
            fx.steps.Add(S("look", T(146), Pt(stage, plan.rock2 + up * 8), .5f));
            set.drives.Add(fx);
            var pg = new StudioDrive { actor = "penguin", seed = 43, human = 1 };
            pg.steps.Add(S("gear", .3f, weapon: "Blaster"));
            pg.steps.Add(S("look", .55f, "actor:fox"));
            pg.steps.Add(Go(stage, penguin - A.s2 * .3f, 1.0f));
            pg.steps.Add(S("fire", T(54), "actor:fox"));
            pg.steps.Add(Go(stage, penguin + A.s2 * .3f + A.a2 * .2f));
            pg.steps.Add(S("look", T(90), Pt(stage, sky(penguin, A.s2, 30)), .6f));
            pg.steps.Add(S("fire", T(118), "actor:fox"));
            pg.steps.Add(Go(stage, penguin - A.s2 * .2f));
            pg.steps.Add(S("fire", T(147), "actor:fox"));
            set.drives.Add(pg);

            // The panda: peeks over the edge, watches the fights, looks for the rocks, shuffles away from them.
            if (plan.at.TryGetValue("panda", out var panda))
            {
                Vector3 outward = Flat(panda - A.C);
                var pd = new StudioDrive { actor = "panda", seed = 51, human = 1 };
                pd.steps.Add(S("look", .5f, "actor:fox", 1.0f));
                pd.steps.Add(Go(stage, panda + outward * .55f, 1.0f));
                pd.steps.Add(S("look", -1, Pt(stage, panda + outward * 5 - up * 12), .9f));
                pd.steps.Add(Go(stage, panda - outward * .3f));
                pd.steps.Add(S("look", T(50), "actor:hero", 1.0f));
                pd.steps.Add(S("look", T(100), Pt(stage, sky(panda, -outward, 38)), .9f));
                pd.steps.Add(Go(stage, panda + Vector3.Cross(up, outward) * .7f, T(130), "jog"));
                pd.steps.Add(S("look", T(156), "actor:fox", .8f));
                pd.steps.Add(Go(stage, panda, T(190)));
                pd.steps.Add(S("look", T(230), Pt(stage, A.C + up * .85f + plan.planetFrom * 40), 3));
                set.drives.Add(pd);
            }
            // The astronaut: shoots at the falling rocks.
            if (plan.at.TryGetValue("astro", out var astro))
            {
                Vector3 outward = Flat(astro - A.C);
                var ad = new StudioDrive { actor = "astro", seed = 61, human = 1 };
                ad.steps.Add(S("gear", .3f, weapon: "Blaster"));
                ad.steps.Add(S("look", .6f, Pt(stage, sky(astro, outward, 30)), .9f));
                ad.steps.Add(S("fire", T(30), Pt(stage, sky(astro, outward + Vector3.Cross(up, outward), 35))));
                ad.steps.Add(Go(stage, astro + Vector3.Cross(up, outward) * .5f));
                ad.steps.Add(S("look", T(70), "actor:panda", .8f));
                ad.steps.Add(S("fire", T(96), Pt(stage, sky(astro, outward - Vector3.Cross(up, outward), 40))));
                ad.steps.Add(Go(stage, astro - Vector3.Cross(up, outward) * .35f));
                ad.steps.Add(S("look", T(132), "actor:hero", 1.0f));
                ad.steps.Add(S("fire", T(165), Pt(stage, sky(astro, -outward, 45))));
                ad.steps.Add(S("look", T(230), Pt(stage, A.C + up * .85f + plan.planetFrom * 40), 3));
                set.drives.Add(ad);
            }

            foreach (var rock in plan.rocks) set.cues.Add(Meteor(stage, rock.target, rock.from, rock.frame, false, rock.radius));
            set.cues.Add(Meteor(stage, A.C, plan.planetFrom, PlanetFrame + 4, true));   // rehearsal 1: it touched 4 frames early
            set.cues.Sort((x, y) => x.at.CompareTo(y.at));
            set.EnsureCueIds();
            var problems = StudioPerformer.Problems(set);
            if (problems.Count > 0) Note("Drive problems: " + string.Join(" | ", problems));
            StudioLibrary.Save(set);
            StudioRun.Refresh();
            Note("Cues: " + string.Join(" | ", set.cues.Select(c => $"{c.at:0.00}s {c.action} {c.hazard}")));
        }
    }
}
