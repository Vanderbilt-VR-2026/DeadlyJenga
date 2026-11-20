using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Where everyone stands, what they carry, and what happens when (cues and reaction layers).
    public static partial class DisasterPvpMoment
    {
        sealed class Arena
        {
            public int summit;
            public Vector3 centre, along, side, toSun, size;
            public float top;
            public readonly List<Vector3> spots = new List<Vector3>();
        }

        sealed class Actor
        {
            public string role, id, weapon = "";
            public int color;
            public Vector3 feet;
            public float yaw;
            public bool bot;
        }

        sealed class Plan
        {
            public Arena arena;
            public readonly Dictionary<string, Actor> cast = new Dictionary<string, Actor>();
            public readonly List<(string label, int arriveFrame, Vector3 target)> meteors = new List<(string, int, Vector3)>();
            public readonly List<Vector3> meteorFrom = new List<Vector3>();
            public Vector3 planetFrom, planetTarget;
            public int planetFrame;
            public string Id(string role) => cast.TryGetValue(role, out var a) ? a.id : "";
            public Actor this[string role] => cast[role];
        }

        // ---- The choreography, in trailer frames (30 fps), on measured onsets of the music edit (v02/music-map) ----
        const int PunchFrame = 25;                 // the hero's glove hits the rival (onset 25.2)
        const int PlanetFrame = 308;               // the planet killer lands (the drop, onset 307.8)
        static readonly int[] BlueShots = { 64, 77 };   // beats 2.13 s, 2.56 s
        const int RocketFrame = 90;                // beat 2.99 s
        const int UziFrame = 115;                  // beat 3.84 s
        const int ShowerFrame = 100;               // the build starts at 102
        // Aimed meteors: who they land beside, and when (every beat of the build from 5.13 s).
        static readonly (string near, int frame)[] MeteorPlan =
        {
            ("purple", 154), ("green", 173), ("blue", 192), ("rivalEnd", 205), ("pink", 218), ("cyan", 244), ("heroFar", 257), ("summitFar", 270), ("spare", 283)
        };
        const float MeteorDistance = 60, MeteorElevation = 28, MeteorSize = 4.2f, PlanetDistance = 130, PlanetSize = 45;

        static readonly string[] Roles = { "hero", "rival", "blue", "green", "purple", "pink", "cyan" };

        static Arena FindArena(StudioStage stage)
        {
            var a = new Arena();
            var blocks = Enumerable.Range(0, stage.blocks.Length).Where(i => stage.blocks[i] && stage.blocks[i].gameObject.activeInHierarchy).ToList();
            var tops = blocks.ToDictionary(i => i, i => TopOf(stage.blocks[i]));
            a.summit = blocks.OrderByDescending(i => tops[i]).First();
            var s = stage.blocks[a.summit];
            a.top = tops[a.summit];
            a.size = StudioHandDirection.BlockSize(s);
            a.along = Flat(s.rotation * (a.size.x >= a.size.z ? Vector3.right : Vector3.forward));
            a.centre = new Vector3(s.position.x, a.top, s.position.z);
            var sun = RenderSettings.sun ? RenderSettings.sun : Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional).OrderByDescending(l => l.intensity).FirstOrDefault();
            a.toSun = sun ? -sun.transform.forward : new Vector3(-.56f, .67f, -.49f).normalized;
            a.side = Vector3.Cross(Vector3.up, a.along).normalized;
            if (Vector3.Dot(a.side, Flat(a.toSun)) < 0) a.side = -a.side;

            var line = new StringBuilder($"Summit block #{a.summit} at {V(s.position)}, size {V(a.size)}, top y {a.top:0.00}, long axis {V(a.along)}, sun side {V(a.side)}, to sun {V(a.toSun)}.\nHeight profile (top y: blocks, x range, z range):\n");
            foreach (var level in blocks.Where(i => tops[i] > a.top - 16).GroupBy(i => Mathf.Round(tops[i] * 2) / 2).OrderByDescending(g => g.Key))
            {
                var p = level.Select(i => stage.blocks[i].position).ToList();
                line.Append($"- {level.Key:0.0}: {level.Count()} blocks, x {p.Min(v => v.x):0.0}..{p.Max(v => v.x):0.0}, z {p.Min(v => v.z):0.0}..{p.Max(v => v.z):0.0} ({string.Join(" ", level.Select(i => "#" + i))})\n");
            }
            int mask = (1 << Layers.Block) | (1 << Layers.Surface);
            foreach (int i in blocks.Where(i => tops[i] > a.top - 6.5f))
            {
                var body = stage.blocks[i];
                var size = StudioHandDirection.BlockSize(body);
                Vector3 longLocal = size.x >= size.z ? Vector3.right : Vector3.forward, wideLocal = size.x >= size.z ? Vector3.forward : Vector3.right;
                float length = Mathf.Max(size.x, size.z), width = Mathf.Min(size.x, size.z);
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
            line.Append($"Free standing spots in the top 6.5 m: {a.spots.Count} (by height: {string.Join(", ", a.spots.GroupBy(p => Mathf.Round(p.y)).OrderByDescending(g => g.Key).Select(g => g.Key + " m x" + g.Count()))}).");
            Note(line.ToString());
            return a;
        }

        static bool Standable(Vector3 p)
        {
            int mask = (1 << Layers.Block) | (1 << Layers.Surface);
            return StudioStage.SurfaceBelow(p + Vector3.up * .05f, out var hit) && Mathf.Abs(hit.y - p.y) < .12f &&
                !Physics.CheckCapsule(p + Vector3.up * .45f, p + Vector3.up * 1.35f, .22f, mask, QueryTriggerInteraction.Ignore);
        }

        static float YawTo(Vector3 from, Vector3 to) => Quaternion.LookRotation(Flat(to - from)).eulerAngles.y;

        static Plan MakePlan(StudioStage stage)
        {
            var plan = new Plan { arena = FindArena(stage) };
            var a = plan.arena;
            Vector3 heroAt = a.centre - a.along * .6f, rivalAt = a.centre + a.along * .45f;
            if (!Standable(heroAt) || !Standable(rivalAt)) Note($"WARNING summit spots not clear: hero {Standable(heroAt)}, rival {Standable(rivalAt)}.");
            plan.cast["hero"] = new Actor { role = "hero", color = 6, weapon = "BoxingGlove", feet = heroAt, yaw = YawTo(heroAt, rivalAt) };
            plan.cast["rival"] = new Actor { role = "rival", color = 0, feet = rivalAt, yaw = YawTo(rivalAt, heroAt) };
            // Everyone else: spread over the free spots below the summit, at least 1.7 m apart, nearest levels first.
            var chosen = new List<Vector3> { heroAt, rivalAt };
            // Only the sun-side half of the tower, so the cameras on that side see every fighter lit and unhidden.
            var below = a.spots.Where(p => p.y < a.top - .5f && Vector3.Distance(Flat2(p), Flat2(a.centre)) < 8 && Vector3.Dot(Flat2(p - a.centre), a.side) > -.3f).ToList();
            string[] others = { "blue", "green", "purple", "pink", "cyan" };
            int[] colors = { 2, 3, 5, 4, 7 };
            string[] weapons = { "Blaster", "RocketLauncher", "DualUzi", "", "Blaster" };
            for (int n = 0; n < others.Length; n++)
            {
                var pick = below.Where(p => chosen.All(c => Vector3.Distance(c, p) > 1.7f))
                    .OrderByDescending(p => Mathf.Round(p.y) * 10 + chosen.Min(c => Vector3.Distance(Flat2(c), Flat2(p))))
                    .Cast<Vector3?>().FirstOrDefault();
                if (pick == null) { Note("WARNING no spot left for " + others[n]); continue; }
                chosen.Add(pick.Value);
                plan.cast[others[n]] = new Actor { role = others[n], color = colors[n], weapon = weapons[n], feet = pick.Value, yaw = YawTo(pick.Value, a.centre), bot = others[n].StartsWith("bot") };
            }
            Note("Cast: " + string.Join("; ", plan.cast.Values.Select(c => $"{c.role} colour {c.color} {(c.weapon.Length > 0 ? c.weapon : "unarmed")}{(c.bot ? " (bot brain)" : "")} at {V(c.feet)} yaw {c.yaw:0}")));

            // Meteors come in low (28 deg) and land 1.2 m short of a fighter, so the blast throws them along the rock's path.
            // Spare spots: free places far from everyone, for rocks that should land in frame without hitting anybody.
            var spare = below.Where(p => Vector3.Distance(Flat2(p), Flat2(heroAt)) > 3.6f).OrderByDescending(p => chosen.Min(c => Vector3.Distance(c, p))).ToList();
            var rng = new System.Random(11);
            int spareUsed = 0;
            foreach (var (near, frame) in MeteorPlan)
            {
                float turn = (float)(rng.NextDouble() * 60 - 30);
                Vector3 travel = Quaternion.AngleAxis(turn, Vector3.up) * a.side;
                Vector3 target;
                if (near == "rivalEnd") target = a.centre + a.along * 2.5f;
                else if ((near == "heroFar" || near == "summitFar" || near == "spare") && spare.Count > spareUsed) target = spare[spareUsed++];
                else if (plan.cast.TryGetValue(near, out var who)) target = who.feet - travel * 1.2f;
                else continue;
                var from = (-travel * Mathf.Cos(MeteorElevation * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(MeteorElevation * Mathf.Deg2Rad)).normalized;
                plan.meteors.Add((near, frame, target + Vector3.up * .2f));
                plan.meteorFrom.Add(from);
            }
            plan.planetTarget = a.centre;
            plan.planetFrame = PlanetFrame;
            // The planet killer comes in low from the far side of the sun side (it is seen over the tower from the sun side).
            plan.planetFrom = (-a.side * Mathf.Cos(25 * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(25 * Mathf.Deg2Rad) + a.along * .25f).normalized;
            return plan;
        }

        static Vector3 Flat2(Vector3 v) => new Vector3(v.x, 0, v.z);

        static StudioCue Fire(Plan plan, string shooter, string target, int frame) => new StudioCue
        { action = "fire", actor = plan.Id(shooter), target = plan.Id(target), at = TapeF(frame), optional = true };

        static StudioCue Meteor(StudioStage stage, Vector3 target, Vector3 from, int arriveFrame, string kind, float distance, float size, float speed)
        {
            var cue = new StudioCue { action = AimedMeteorCue.Id, hazard = kind, point = stage.ToStage(target), direction = from, radius = distance, height = size, strength = speed };
            cue.at = TapeF(arriveFrame) - AimedMeteorCue.ArrivalDelay(cue);
            return cue;
        }

        static void Direct(StudioStage stage, Plan plan)
        {
            var set = stage.set;
            set.marks.Clear();
            foreach (string role in Roles)
            {
                if (!plan.cast.TryGetValue(role, out var who)) continue;
                var mark = set.AddMark(who.bot ? StudioRole.Bot : StudioRole.Survivor, stage.ToStage(who.feet));
                mark.actor = role; mark.yaw = who.yaw; mark.color = who.color; mark.equipment = who.weapon;
                // A plain candy body for everyone (the bot's random outfit hid the rival in a dark robot suit in the scout).
                var hair = new[] { AvatarHair.Spikes, AvatarHair.Afro, AvatarHair.Ponytail, AvatarHair.Buzz, AvatarHair.Curls, AvatarHair.TwinTails, AvatarHair.Pompadour };
                mark.customLook = true;
                mark.appearance = new AvatarAppearance { hair = hair[Array.IndexOf(Roles, role) % hair.Length], hairShade = role == "hero" ? AvatarHairShade.Match : AvatarHairShade.Dark };
                who.id = mark.id;
            }

            set.cues.Clear();
            // Weapons go through the match's own equipment path once the stand-ins are part of the match
            // (in the scout, marks' equipment given at spawn was refused: every shot fired "None").
            foreach (var who in plan.cast.Values.Where(c => c.weapon.Length > 0))
                set.cues.Add(new StudioCue { action = "gear", actor = who.id, weapon = who.weapon, at = .3f, optional = true });
            set.cues.Add(new StudioCue { action = GlovePunchCue.Id, actor = plan.Id("hero"), target = plan.Id("rival"), at = TapeF(PunchFrame) });
            foreach (int f in BlueShots) set.cues.Add(Fire(plan, "blue", "green", f));
            set.cues.Add(Fire(plan, "green", "cyan", RocketFrame));
            set.cues.Add(Fire(plan, "purple", "blue", UziFrame));
            set.cues.Add(Fire(plan, "cyan", "purple", UziFrame + 13));
            // Background meteors over the whole map (no warning, force at its maximum), then the aimed ones by the fighters.
            set.cues.Add(new StudioCue { action = "hazard", hazard = "MeteorShower", at = TapeF(ShowerFrame), values = new[] { 3f, 1f, 8f, 40f, 0f, 12f } });
            for (int i = 0; i < plan.meteors.Count; i++)
                set.cues.Add(Meteor(stage, plan.meteors[i].target, plan.meteorFrom[i], plan.meteors[i].arriveFrame, "MeteorShower", MeteorDistance, MeteorSize, 45));
            var planet = Meteor(stage, plan.planetTarget, plan.planetFrom, plan.planetFrame, "PlanetKillerMeteor", PlanetDistance, PlanetSize, 28);
            planet.values = new[] { PlanetSize, 0f };   // size, warning (the scout's default 3 s warning made it land 2.2 s late)
            set.cues.Add(planet);
            set.cues.Sort((x, y) => x.at.CompareTo(y.at));
            set.EnsureCueIds();

            set.overrides.Clear();
            AddLayers(stage, plan);
            StudioLibrary.Save(set);
            StudioRun.Refresh();
            Note("Cues: " + string.Join(" | ", set.cues.Select(c => $"{c.at:0.00}s {c.action} {c.actor}{(c.target.Length > 0 ? "->" + c.target : "")}{c.hazard}")));
        }

        // Reaction layers on the recorded takes (they only change what is drawn, so they can be tuned after recording).
        static void AddLayers(StudioStage stage, Plan plan)
        {
            var list = stage.set.overrides;
            void Add(string role, string kind, int from, int to, string name = "", string target = "", float blendIn = .2f, float blendOut = .25f, bool left = false)
            {
                string id = plan.Id(role);
                if (id.Length == 0) return;
                list.Add(new StudioOverride { actor = id, kind = kind, name = name, target = target, from = TapeF(from), to = TapeF(to), blendIn = blendIn, blendOut = blendOut, leftHand = left });
            }
            string Actor(string role) => "actor:" + plan.Id(role);
            // The punch: the glove hand goes to the rival's head, then the hero taunts.
            Add("hero", "lookAt", 0, 40, target: Actor("rival"), blendIn: .05f);
            Add("hero", "reach", PunchFrame - 6, PunchFrame + 6, target: Actor("rival"), blendIn: .12f, blendOut: .2f);
            Add("hero", "reaction", PunchFrame + 10, PunchFrame + 46, "cheer");
            Add("rival", "reaction", PunchFrame + 2, PunchFrame + 90, "scream", blendIn: .05f);
            // The fighters watch and aim at each other; the gun hand goes toward the target around each shot.
            Add("blue", "lookAt", 0, 150, target: Actor("green"));
            Add("green", "lookAt", 0, 150, target: Actor("cyan"));
            Add("purple", "lookAt", 0, 150, target: Actor("blue"));
            Add("cyan", "lookAt", 0, 150, target: Actor("purple"));
            Add("pink", "lookAt", 0, 60, target: Actor("hero"));
            Add("blue", "reach", BlueShots[0] - 8, BlueShots[BlueShots.Length - 1] + 8, target: Actor("green"), blendIn: .15f);
            Add("green", "reach", RocketFrame - 10, RocketFrame + 8, target: Actor("cyan"), blendIn: .15f);
            Add("purple", "reach", UziFrame - 8, UziFrame + 10, target: Actor("blue"), blendIn: .15f);
            Add("cyan", "reach", UziFrame + 5, UziFrame + 22, target: Actor("purple"), blendIn: .15f);
            Add("pink", "reaction", 60, 210, "cower");
            // Thrown survivors scream in the air.
            foreach (var (near, frame, _) in plan.meteors)
                if (plan.cast.ContainsKey(near)) Add(near, "reaction", frame, frame + 70, "scream", blendIn: .08f);
            // Everyone looks up at the planet killer, the hero screams.
            Vector3 sky = plan.planetTarget + plan.planetFrom * 55;
            foreach (string role in Roles)
                Add(role, "lookAt", PlanetFrame - 50, LengthFrames, target: P(stage, sky), blendIn: .4f);
            Add("hero", "reaction", PlanetFrame - 26, PlanetFrame + 4, "scream", blendIn: .15f);
        }
    }
}
