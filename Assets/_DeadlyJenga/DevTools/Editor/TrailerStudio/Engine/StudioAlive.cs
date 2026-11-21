using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The "alive" layer: nobody stands frozen. On every recorded survivor, whenever no stronger layer owns
    // that part of the body, it adds what a person does without thinking:
    //   head   glances at nearby players, a disaster, loud hits and the horizon. Each look is held 0.4-2 s,
    //          then the head moves quickly with a slow start and a slow stop (never on a beat).
    //   body   weight shifts, a small step now and then, sway and small turns on the spot.
    //   hands  rest, ready, guard, hands-on-hips and fidget changes; with a weapon, ready and lowered;
    //          arm swing in step with the legs while walking or running.
    //   breath slow breathing.
    // Everything comes from a seed and the tape time, so a replay or a render is the same every time.
    // Explicit layers win: a look-at takes the head, a reach takes the hands, a reaction takes everything,
    // and each blends in and out with its own weight. Switches: Set.alive (on, strengths), Set.alive.actors
    // (off or stronger per actor) and StudioCameraTrack.aliveOff (per shot).
    public sealed class StudioAlive : IActorFrameFilter
    {
        public int Order => 5;   // before the overrides (10), so they blend from the alive pose and win

        public bool Wants(StudioStage stage, StudioTrack track) => track.type == SurvivorTrack.Id && On(stage.set, track.id);

        // Is the alive layer on for this actor right now (Set switch, actor switch, shot switch)?
        public static bool On(StudioSet set, string markId)
        {
            var alive = set?.alive;
            if (alive == null || !alive.on || alive.strength <= 0) return false;
            if (StudioRender.CurrentShot != null && StudioRender.CurrentShot.aliveOff) return false;
            var who = Who(set, markId);
            return who == null || !who.off && who.strength > 0;
        }

        static StudioAliveActor Who(StudioSet set, string markId)
        {
            foreach (var a in set.alive.actors) if (a.actor == markId) return a;
            return null;
        }

        public void Apply(StudioStage stage, StudioTrack track, float time, ActorFrame frame) => Apply(stage.set, stage.tape, track, time, frame);

        // ---- What explicit layers own right now (0 = free, 1 = taken) ----

        struct Owned { public float head, hands, body; }

        static Owned Taken(StudioSet set, string markId, float time)
        {
            var owned = new Owned();
            foreach (var layer in set.overrides)
            {
                if (layer.actor != markId || layer.muted) continue;
                float w;
                if (layer.kind == "reaction")
                {
                    // A fall or death holds until a get-up; every other reaction eases in and out quickly.
                    bool stays = layer.name == "death" || layer.name == "fall";
                    float start = layer.Start, stop = stays ? float.MaxValue : layer.Stop;
                    if (stays && set.overrides.Any(o => o != layer && o.kind == "reaction" && o.name == "getUp" && !o.muted && o.actor == markId && o.Start > start))
                        stop = set.overrides.Where(o => o != layer && o.kind == "reaction" && o.name == "getUp" && !o.muted && o.actor == markId && o.Start > start).Min(o => o.Stop);
                    w = time < start - .15f || time > stop + .3f ? 0 : Mathf.Min(Mathf.Clamp01((time - start + .15f) / .15f), Mathf.Clamp01((stop + .3f - time) / .3f));
                    owned.head = Mathf.Max(owned.head, w); owned.hands = Mathf.Max(owned.hands, w); owned.body = Mathf.Max(owned.body, w);
                    continue;
                }
                w = layer.Weight(time) / Mathf.Max(.01f, Mathf.Clamp01(layer.strength));
                w = Mathf.Clamp01(w);
                switch (layer.kind)
                {
                    case "lookAt": owned.head = Mathf.Max(owned.head, w); break;
                    case "reach": owned.hands = Mathf.Max(owned.hands, w); break;
                    case "pose":
                        if (layer.crouch > .001f) owned.body = Mathf.Max(owned.body, w);
                        if (layer.lean > .001f || layer.flinch > .001f) { owned.head = Mathf.Max(owned.head, w); owned.body = Mathf.Max(owned.body, w); owned.hands = Mathf.Max(owned.hands, w); }
                        if (layer.armRaise > .001f) owned.hands = Mathf.Max(owned.hands, w);
                        break;
                }
            }
            return owned;
        }

        // ---- What the take itself does (worked out once per take) ----

        sealed class TakeInfo
        {
            public float[] speed;   // feet speed along the ground, m/s, lightly smoothed
            public float[] turn;    // recorded head turn speed, degrees per second
            public float[] phase;   // walk phase as the avatar's legs use it (radians)
        }

        static readonly ConditionalWeakTable<StudioTake, TakeInfo> infos = new ConditionalWeakTable<StudioTake, TakeInfo>();

        static TakeInfo Info(StudioTrack track, StudioTake take, int fps)
        {
            if (infos.TryGetValue(take, out var info) && info.speed.Length == take.frames.Count) return info;
            int n = take.frames.Count;
            info = new TakeInfo { speed = new float[n], turn = new float[n], phase = new float[n] };
            var raw = new float[n];
            var gait = default(CandyPlayerRig.GaitState);
            Vector3 lastFeet = default; float lastYaw = 0; bool had = false;
            for (int i = 0; i < n; i++)
            {
                var pose = ActorFrame.Read(track.type, take.frames[i]);
                if (!pose.visible) { had = false; continue; }
                float yaw = pose.body.headRotation.eulerAngles.y;
                if (had)
                {
                    raw[i] = Vector3.ProjectOnPlane(pose.body.feet - lastFeet, Vector3.up).magnitude * fps;
                    info.turn[i] = Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw)) * fps;
                }
                gait = CandyPlayerRig.StepGait(gait, pose.body, 1f / fps);
                info.phase[i] = gait.walkPhase;
                lastFeet = pose.body.feet; lastYaw = yaw; had = true;
            }
            for (int i = 0; i < n; i++)
            {
                float sum = 0; int count = 0;
                for (int j = Mathf.Max(0, i - 3); j <= Mathf.Min(n - 1, i + 3); j++) { sum += raw[j]; count++; }
                info.speed[i] = sum / count;
            }
            infos.Remove(take); infos.Add(take, info);
            return info;
        }

        // ---- Loud moments: the strongest recorded hits and match sounds ----

        [System.Serializable] sealed class SoundFrame { public AudioEventTrack.Sound[] sounds; }
        sealed class Loud { public float time; public Vector3 position; }
        sealed class LoudList { public List<Loud> list = new List<Loud>(); }
        static readonly ConditionalWeakTable<StudioTake, LoudList> louds = new ConditionalWeakTable<StudioTake, LoudList>();

        static List<Loud> LoudMoments(StudioTape tape, int fps)
        {
            var take = tape?.FindType(AudioEventTrack.Id)?.ActiveTake;
            if (take == null) return null;
            if (louds.TryGetValue(take, out var found)) return found.list;
            var all = new List<(float time, Vector3 position, float strength, bool match)>();
            for (int i = 0; i < take.frames.Count; i++)
            {
                var data = take.frames[i];
                if (data == null || data.Length == 0) continue;
                SoundFrame frame;
                try { frame = JsonUtility.FromJson<SoundFrame>(Encoding.UTF8.GetString(data)); } catch { continue; }
                if (frame?.sounds == null) continue;
                foreach (var s in frame.sounds) all.Add(((take.start + i) / (float)fps, s.position, s.strength, s.kind == 1));
            }
            float loudest = all.Where(a => !a.match).Select(a => a.strength).DefaultIfEmpty(0).Max();
            var kept = new LoudList();
            foreach (var a in all.Where(a => a.match || a.strength >= loudest * .35f && loudest > 0).OrderBy(a => a.time))
            {
                // One startle per 0.8 s: a collapse makes hundreds of hits.
                if (kept.list.Count > 0 && a.time - kept.list[kept.list.Count - 1].time < .8f) continue;
                kept.list.Add(new Loud { time = a.time, position = a.position });
            }
            louds.Add(take, kept);
            return kept.list;
        }

        // ---- Seeded schedules: when each look, stance and hand change happens ----

        sealed class Beat { public float start, move, hold; }
        static readonly Dictionary<string, List<Beat>> schedules = new Dictionary<string, List<Beat>>();

        // A list of moments for one actor and one kind of change. 'hold' is how long each lasts.
        static List<Beat> Schedule(int seed, int salt, float length, System.Func<int, float> move, System.Func<int, float> hold)
        {
            string key = seed + ":" + salt + ":" + Mathf.CeilToInt(length);
            if (schedules.TryGetValue(key, out var list)) return list;
            list = new List<Beat>();
            // Start a little before the tape, at a random point of the first beat, so actors are not in step.
            float t = -StudioHuman.Range(seed, 0, salt + 50, .2f, 2.5f);
            for (int k = 0; t < length + 3 && k < 4000; k++)
            {
                var beat = new Beat { start = t, move = move(k), hold = hold(k) };
                list.Add(beat);
                t += beat.move + beat.hold;
            }
            if (schedules.Count > 400) schedules.Clear();
            schedules[key] = list;
            return list;
        }

        static int Find(List<Beat> list, float time)
        {
            int lo = 0, hi = list.Count - 1;
            while (lo < hi) { int mid = (lo + hi + 1) / 2; if (list[mid].start <= time) lo = mid; else hi = mid - 1; }
            return lo;
        }

        // ---- The layer ----

        const int Glance = 1, Stance = 2, Hands = 3;

        public static void Apply(StudioSet set, StudioTape tape, StudioTrack track, float time, ActorFrame frame)
        {
            if (!frame.visible || frame.isHand || set == null || !On(set, track.id)) return;
            var take = track.ActiveTake;
            if (take == null || take.frames.Count == 0) return;
            var alive = set.alive;
            var who = Who(set, track.id);
            float amount = Mathf.Clamp(alive.strength, 0, 2) * Mathf.Clamp(who?.strength ?? 1, 0, 3);
            int seed = StudioHuman.SeedOf(track.id) ^ (alive.seed * 7919) ^ ((who?.seed ?? 0) * 104729);
            int fps = set.fps;
            float length = set.length;

            var info = Info(track, take, fps);
            int index = Mathf.Clamp(Mathf.RoundToInt(time * fps) - take.start, 0, take.frames.Count - 1);
            float speed = info.speed[index];
            float moving = Mathf.InverseLerp(.25f, .8f, speed);
            var owned = Taken(set, track.id, time);
            bool driven = set.drives.Any(d => d.actor == track.id && !d.muted && d.steps.Count > 0);

            var body = frame.body;
            float size = Mathf.Max(.1f, body.scale);   // offsets below are for a 1.7 m person; survivors are scaled down
            Quaternion recorded = body.headRotation;
            float facing = recorded.eulerAngles.y;

            // Hands the recording left in the game's default "no hand input" place (in front of the face) are free.
            bool LifelessHand(CandyHandPose hand, float side) =>
                Vector3.Distance(hand.position, body.head + recorded * (new Vector3(side * .23f, -.20f, .40f) * size)) < .02f * size / .5f;
            bool leftFree = LifelessHand(body.left, -1), rightFree = LifelessHand(body.right, 1);
            var equipment = Equipment(frame.equipment);
            bool armed = equipment != null && equipment.drawn && equipment.kind >= EquipmentKind.Blaster;

            float bodyAmt = Mathf.Clamp(alive.body, 0, 2) * amount * (1 - owned.body) * (1 - moving);
            float headAmt = Mathf.Clamp(alive.head, 0, 2) * amount * (1 - owned.head) * (driven ? 0 : 1) * (1 - .7f * moving)
                * (1 - Mathf.InverseLerp(20, 60, info.turn[index]));
            float handAmt = Mathf.Clamp(alive.hands, 0, 2) * amount * (1 - owned.hands);
            float breathAmt = Mathf.Clamp(alive.breath, 0, 2) * amount * (1 - .7f * moving) * (1 - owned.body);

            // ---- Body: weight shifts, small steps, small turns, sway ----
            var stances = Schedule(seed, Stance, length, k => StudioHuman.Range(seed, k, 201, .45f, .75f), k => StudioHuman.Range(seed, k, 202, 1.8f, 5f));
            int s = Find(stances, time);
            float su = StudioHuman.Ease((time - stances[s].start) / stances[s].move);
            Vector3 StanceShift(int k) => new Vector3(StudioHuman.Range(seed, k, 203, -1, 1) * .035f, 0, StudioHuman.Range(seed, k, 204, -1, 1) * .012f);
            Vector3 StanceStep(int k)
            {
                // A small step about one stance change in three; otherwise the feet stay where the last step put them.
                for (int j = k; j > k - 12 && j >= 0; j--)
                    if (StudioHuman.Rand(seed, j, 205) < .35f)
                    {
                        float angle = StudioHuman.Rand(seed, j, 206) * 360, reach = StudioHuman.Range(seed, j, 207, .03f, .09f);
                        return Quaternion.Euler(0, angle, 0) * Vector3.forward * reach;
                    }
                return Vector3.zero;
            }
            float StanceTurn(int k) => StudioHuman.Range(seed, k, 208, -9, 9);
            Vector3 shift = Vector3.Lerp(StanceShift(Mathf.Max(0, s - 1)), StanceShift(s), su);
            Vector3 step = Vector3.Lerp(StanceStep(Mathf.Max(0, s - 1)), StanceStep(s), su);
            float bodyTurn = Mathf.Lerp(StanceTurn(Mathf.Max(0, s - 1)), StanceTurn(s), su) * bodyAmt;
            Quaternion around = Quaternion.Euler(0, facing + bodyTurn, 0);
            Vector3 sway = new Vector3(StudioHuman.Wobble(seed, 210, time, .19f) * .012f, 0, StudioHuman.Wobble(seed, 211, time, .15f) * .009f);
            Vector3 feetMove = around * step * (size * bodyAmt);
            body.feet += feetMove;
            body.head += feetMove + around * ((shift + sway) * (size * bodyAmt));
            body.left.position += feetMove; body.right.position += feetMove;
            facing += bodyTurn;

            // ---- Breathing ----
            float period = StudioHuman.Range(seed, 0, 220, 3.3f, 4.6f);
            float breath = Mathf.Sin(time * 6.283f / period + .4f * Mathf.Sin(time * .48f + seed % 7)) * breathAmt;
            body.head.y += .010f * size * breath;

            // ---- Head: where the eyes and head go ----
            Vector2 gaze = Vector2.zero; float roll = 0;
            if (headAmt > .001f)
            {
                gaze = Gaze(set, tape, track, seed, time, length, body.head, facing, out roll);
                gaze.x += StudioHuman.Wobble(seed, 230, time, .38f) * 1.8f;
                gaze.y += StudioHuman.Wobble(seed, 231, time, .45f) * 1.1f;
                gaze *= headAmt; roll *= headAmt;
            }
            float pitchBreath = -.6f * breath;
            body.headRotation = Quaternion.Euler(0, bodyTurn + gaze.x, 0) * recorded * Quaternion.Euler(gaze.y + pitchBreath, 0, roll);

            // ---- Hands ----
            if (handAmt > .001f)
            {
                // The avatar's torso turns after the head once the head is more than 35 degrees round.
                float headYaw = body.headRotation.eulerAngles.y;
                float torsoYaw = headYaw - Mathf.Clamp(Mathf.DeltaAngle(facing, headYaw), -35, 35);
                Quaternion torso = Quaternion.Euler(0, torsoYaw, 0);
                bool alert = Alert(set, tape, track.id, time, body.head);
                var states = Schedule(seed, Hands, length, k => StudioHuman.Range(seed, k, 301, .35f, .6f), k => StudioHuman.Range(seed, k, 302, 1.4f, 4.2f));
                bool weaponLeft = armed && equipment.leftHanded;
                for (int side = -1; side <= 1; side += 2)
                {
                    bool left = side < 0;
                    bool free = left ? leftFree : rightFree;
                    bool weaponHand = armed && left == weaponLeft;
                    ref CandyHandPose hand = ref (left ? ref body.left : ref body.right);
                    // One hand leads the other a little when they change together.
                    float lag = StudioHuman.Rand(seed, 0, 303) < .5f == left ? .09f : 0;
                    int h = Find(states, time - lag);
                    float hu = StudioHuman.Ease((time - lag - states[h].start) / states[h].move);
                    int was = HandState(seed, Mathf.Max(0, h - 1), alert, armed), now = HandState(seed, h, alert, armed);
                    Vector3 micro = new Vector3(StudioHuman.Wobble(seed, 310 + side, time, .3f), StudioHuman.Wobble(seed, 312 + side, time, .26f), StudioHuman.Wobble(seed, 314 + side, time, .34f)) * .008f;
                    Vector3 lift = Vector3.up * (.008f * breath);
                    if (weaponHand)
                    {
                        // The weapon stays where the game put it; it is held ready or lowered a little.
                        float low = Mathf.Lerp(Lowered(was), Lowered(now), hu) * (1 - moving);
                        hand.position += (torso * (new Vector3(0, -.07f, -.02f) * low) + micro + lift) * (size * handAmt);
                        hand.rotation = Quaternion.Slerp(hand.rotation, hand.rotation * Quaternion.Euler(20 * low, 0, 0), handAmt);
                        continue;
                    }
                    if (!free)
                    {
                        // Hands a person performed: only a little life on top.
                        hand.position += (micro * .6f + lift) * (size * handAmt);
                        continue;
                    }
                    var (fromOffset, fromAngles, fromCurl) = HandPose(was, side);
                    var (toOffset, toAngles, toCurl) = HandPose(now, side);
                    Vector3 offset = Vector3.Lerp(fromOffset, toOffset, hu) + micro + lift;
                    Vector3 angles = Vector3.Lerp(fromAngles, toAngles, hu);
                    float curl = Mathf.Lerp(fromCurl, toCurl, hu);
                    if (moving > .001f)
                    {
                        // Walking and running: the arms swing in step with the legs (the same phase the avatar's legs use).
                        float run = Mathf.InverseLerp(.3f, 4f, speed);
                        float swing = Mathf.Sin(info.phase[index]) * side;
                        Vector3 swingOffset = new Vector3(side * (.25f - run * .05f), -.58f + run * .17f + Mathf.Abs(swing) * .03f * run, .12f + run * .08f + swing * (.10f + .10f * run));
                        offset = Vector3.Lerp(offset, swingOffset, moving);
                        angles = Vector3.Lerp(angles, new Vector3(60 - run * 25 - swing * 15, -side * 10, side * 30), moving);
                        curl = Mathf.Lerp(curl, .55f + .3f * run, moving);
                    }
                    var target = new CandyHandPose
                    {
                        position = body.head + torso * (offset * size),
                        rotation = torso * Quaternion.Euler(angles),
                        skeleton = CandyHandSkeleton.Rest(left, curl), tracked = hand.tracked, skeletonTracked = true
                    };
                    hand.position = Vector3.Lerp(hand.position, target.position, handAmt);
                    hand.rotation = Quaternion.Slerp(hand.rotation, target.rotation, handAmt);
                    if (handAmt > .5f) { hand.skeleton = target.skeleton; hand.skeletonTracked = true; }
                }
            }
            frame.body = body;
        }

        // ---- Glances ----

        // Yaw and pitch (degrees from where the body faces; pitch positive = down) the head turns to now, and its roll.
        static Vector2 Gaze(StudioSet set, StudioTape tape, StudioTrack track, int seed, float time, float length, Vector3 head, float facing, out float roll)
        {
            var looks = Schedule(seed, Glance, length,
                k => StudioHuman.Range(seed, k, 101, .14f, .32f),
                k => .4f + 1.6f * Mathf.Pow(StudioHuman.Rand(seed, k, 102), 1.6f));
            int k = Find(looks, time);
            var beat = looks[k];
            Vector2 to = Aim(set, tape, track, seed, k, beat.start, time, head, facing, out float rollTo, out bool big);
            Vector2 from = Vector2.zero; float rollFrom = 0;
            if (k > 0) from = Aim(set, tape, track, seed, k - 1, looks[k - 1].start, beat.start, head, facing, out rollFrom, out _);
            // Bigger head moves take longer (about 300 degrees a second at most); a look over the shoulder longer still.
            // The move always ends before the next look starts, so one look hands over to the next without a jump.
            float size = Mathf.Abs(to.x - from.x) + .5f * Mathf.Abs(to.y - from.y);
            float move = Mathf.Clamp(.1f + size / 300 + (big ? .25f : 0), beat.move, beat.move + .8f * beat.hold);
            float u = StudioHuman.Ease((time - beat.start) / move);
            Vector2 gaze = Vector2.Lerp(from, to, u);
            roll = Mathf.Lerp(rollFrom, rollTo, u);

            // A loud hit or a match sound nearby: a quick look toward it, then back. Several in a row hand over smoothly.
            var loud = LoudMoments(tape, set.fps);
            if (loud != null)
            {
                float delay = StudioHuman.Range(seed, 0, 120, .16f, .3f), hold = StudioHuman.Range(seed, 0, 121, .7f, 1.3f);
                foreach (var e in loud)
                {
                    float since = time - e.time - delay;
                    if (since > hold + .5f) continue;
                    if (since < 0) break;
                    if (Vector3.Distance(e.position, head) > 25) continue;
                    float w = since < hold ? StudioHuman.Ease(since / .25f) : 1 - StudioHuman.Ease((since - hold) / .5f);
                    gaze = Vector2.Lerp(gaze, Toward(head, e.position, facing, .9f), w);
                }
            }
            return gaze;
        }

        // Where look number k goes, decided by what is around at the moment it starts, followed at time 'now'.
        static Vector2 Aim(StudioSet set, StudioTape tape, StudioTrack track, int seed, int k, float start, float now, Vector3 head, float facing, out float roll, out bool big)
        {
            big = false;
            roll = StudioHuman.Range(seed, k, 103, -3.5f, 3.5f);
            float r = StudioHuman.Rand(seed, k, 104), a = StudioHuman.Rand(seed, k, 105), b = StudioHuman.Rand(seed, k, 106);
            int fps = set.fps;

            // What is there to look at when this look starts?
            Vector3 threat = default;
            var hazards = tape?.FindType(HazardTrack.Id)?.ActiveTake;
            bool danger = hazards != null && HazardTrack.CenterAt(hazards, Mathf.RoundToInt(start * fps), out threat) && Vector3.Distance(threat, head) < 90;
            var others = new List<(string id, float distance)>();
            foreach (var other in tape.tracks)
            {
                if (other == track || other.type != SurvivorTrack.Id || other.mode == TrackMode.Muted) continue;
                if (!RawHead(other, start, fps, out var there)) continue;
                float d = Vector3.Distance(there, head);
                if (d < set.alive.glanceRange && d > .2f) others.Add((other.id, d));
            }
            others.Sort((x, y) => string.CompareOrdinal(x.id, y.id));

            float wDanger = danger ? .45f : 0, wActor = others.Count > 0 ? (others.Any(o => o.distance < 2.5f) ? .45f : .32f) : 0;
            if (r < wDanger)
            {
                if (hazards != null && HazardTrack.CenterAt(hazards, Mathf.RoundToInt(now * fps), out var moved)) threat = moved;
                return Toward(head, threat, facing, .9f);
            }
            float u = a;   // used if the person looked at has gone
            if (r < wDanger + wActor)
            {
                // Nearer people are looked at more often.
                float total = others.Sum(o => 1 / (1 + o.distance)), choose = a * total;
                string id = others[others.Count - 1].id;
                foreach (var o in others) { choose -= 1 / (1 + o.distance); if (choose <= 0) { id = o.id; break; } }
                if (RawHead(tape.Find(id), now, fps, out var there)) return Toward(head, there, facing, .85f);
            }
            else u = (r - wDanger - wActor) / Mathf.Max(.01f, 1 - wDanger - wActor);
            // Nobody and nothing: rest ahead, the horizon either side, the ground, now and then over the shoulder.
            float sign = b < .5f ? -1 : 1;
            if (u < .38f) return new Vector2((a - .5f) * 26, -4 + b * 7);
            if (u < .66f) return new Vector2(sign * Mathf.Lerp(25, 68, a), -3 + b * 4);
            if (u < .9f) return new Vector2((a - .5f) * 34, 12 + b * 14);
            big = true;
            return new Vector2(sign * Mathf.Lerp(100, 125, a), -2 + b * 6);
        }

        // Head yaw and pitch toward a point. The head does most of the turn; the eyes do the rest ('share').
        // Something straight behind is not looked at by the head alone (a person would turn round): the look fades
        // out toward 180 degrees, so it never flips from one shoulder to the other.
        static Vector2 Toward(Vector3 head, Vector3 point, float facing, float share)
        {
            float raw = Mathf.DeltaAngle(facing, StudioHuman.Yaw(point - head));
            float fade = 1 - Mathf.InverseLerp(125, 170, Mathf.Abs(raw));
            return new Vector2(Mathf.Clamp(raw * share, -100, 100) * fade, Mathf.Clamp(StudioHuman.Pitch(head, point) * share, -35, 40) * Mathf.Lerp(.3f, 1, fade));
        }

        // Another actor's head straight from its take (no layers), so actors looking at each other never loop.
        static bool RawHead(StudioTrack track, float time, int fps, out Vector3 head)
        {
            head = default;
            var take = track?.ActiveTake;
            if (take == null || take.frames.Count == 0) return false;
            int f = Mathf.RoundToInt(time * fps);
            if (!take.Covers(f)) return false;
            var pose = ActorFrame.Read(track.type, take.frames[f - take.start]);
            if (!pose.visible) return false;
            head = pose.isHand ? pose.target.position : pose.body.head;
            return true;
        }

        // Someone within 2.5 m, or a disaster near: hands come up more often.
        static bool Alert(StudioSet set, StudioTape tape, string markId, float time, Vector3 head)
        {
            int f = Mathf.RoundToInt(time * set.fps);
            var hazards = tape?.FindType(HazardTrack.Id)?.ActiveTake;
            if (hazards != null && HazardTrack.CenterAt(hazards, f, out var centre) && Vector3.Distance(centre, head) < 40) return true;
            foreach (var other in tape.tracks)
                if (other.id != markId && other.type == SurvivorTrack.Id && other.mode != TrackMode.Muted && RawHead(other, time, set.fps, out var there) && Vector3.Distance(there, head) < 2.5f) return true;
            return false;
        }

        // ---- Hand poses ----

        const int Relaxed = 0, Neutral = 1, Guard = 2, Hip = 3, Fidget = 4;

        static int HandState(int seed, int k, bool alert, bool armed)
        {
            float r = StudioHuman.Rand(seed, k, 304);
            if (alert) return r < .45f ? Guard : r < .8f ? Neutral : Relaxed;
            if (armed) return r < .4f ? Neutral : r < .75f ? Relaxed : Guard;
            return r < .32f ? Relaxed : r < .62f ? Neutral : r < .76f ? Guard : r < .89f ? Hip : Fidget;
        }

        static float Lowered(int state) => state == Relaxed ? 1 : state == Neutral ? .45f : 0;

        // Offset from the head (metres for a 1.7 m person, in the torso's frame), wrist angles, finger curl.
        static (Vector3 offset, Vector3 angles, float curl) HandPose(int state, float side)
        {
            switch (state)
            {
                case Relaxed: return (new Vector3(side * .21f, -.72f, .07f), new Vector3(78, -side * 6, side * 12), .2f);
                case Guard: return (new Vector3(side * .14f, -.31f, .30f), new Vector3(18, -side * 25, side * 68), .95f);
                case Hip: return (new Vector3(side * .28f, -.62f, -.03f), new Vector3(70, -side * 30, side * 60), .4f);
                case Fidget: return (new Vector3(side * .05f, -.54f, .24f), new Vector3(50, -side * 42, side * 32), .45f);
                default: return (new Vector3(side * .27f, -.53f, .24f), new Vector3(51, -side * 10, side * 24), .3f);
            }
        }

        static string lastEquipment;
        static EquipmentState lastState;

        static EquipmentState Equipment(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            if (json == lastEquipment) return lastState;
            lastEquipment = json;
            try { lastState = JsonUtility.FromJson<EquipmentState>(json); } catch { lastState = null; }
            return lastState;
        }
    }
}
