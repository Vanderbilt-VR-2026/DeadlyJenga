using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Plays one survivor from a small timed script (a StudioDrive in the Set) through the same controls a
    // player uses: the move stick, turning, where the head looks, jump, climb, punch and fire. It plugs in
    // where a bot's brain plugs in (CastleSurvivor.InputDriver), so the game's own movement code moves the
    // body and the take that is recorded is a real performance.
    //
    // A person is never exact, so neither is this (all from the drive's seed, so a re-record gets the same inputs):
    //   the head goes to where it is going before the feet move, and leads the body into every turn;
    //   the stick speeds up and slows down instead of jumping to full speed;
    //   paths bend a little, the actor overshoots a stop slightly and steps back;
    //   while travelling or waiting the head glances at other people and around, then comes back.
    // It never walks off an edge unless the step says so (it checks for ground ahead every frame).
    public sealed class StudioPerformer : ICastleSurvivorDriver
    {
        public static readonly List<string> Actions = new List<string> { "goTo", "path", "face", "look", "jump", "punch", "fire", "gear", "climb", "crouch", "stop", "wait" };

        // ---- Which marks are driven in the current run ----

        static readonly List<StudioPerformer> active = new List<StudioPerformer>();
        public static IReadOnlyList<StudioPerformer> Active => active;

        public static StudioDrive For(StudioSet set, string markId) =>
            set?.drives?.FirstOrDefault(d => d.actor == markId && !d.muted && d.steps.Count > 0);

        // Called by StudioCast when it puts a driven mark's stand-in body on the stage.
        public static StudioPerformer Attach(StudioStage stage, StudioDrive drive, CastleSurvivor motor)
        {
            var performer = new StudioPerformer(stage, drive, motor);
            active.Add(performer);
            return performer;
        }

        public static void Clear() => active.Clear();

        // A recording pass for driven marks, the way StudioPerform.BuildPass makes one for a person:
        // only these marks are recorded, everything else plays back around them. With livePhysics the world
        // is simulated and recorded as a new world take too (needed when the actor changes the world).
        public static StudioPass Pass(StudioStage stage, IEnumerable<string> markIds, bool livePhysics)
        {
            var ids = markIds.ToList();
            var pass = new StudioPass { label = "Drive " + string.Join(", ", ids.Select(id => stage.set.Mark(id)?.actor ?? id)), fixedClock = true, liveWorld = livePhysics, paths = false };
            pass.record.AddRange(ids);
            if (livePhysics)
                foreach (var track in stage.tape.tracks)
                    if (!track.locked && track.type != SurvivorTrack.Id && track.type != HandTrack.Id && StudioTrackTypes.Get(track.type) != null)
                        pass.record.Add(track.id);
            return pass;
        }

        // Plain-language problems with the Set's drives (shown with the Set's other problems).
        public static List<string> Problems(StudioSet set)
        {
            var problems = new List<string>();
            if (set?.drives == null) return problems;
            foreach (var drive in set.drives)
            {
                var mark = set.Mark(drive.actor);
                if (mark == null) { problems.Add($"A drive is for a mark that is not in the Set: \"{drive.actor}\"."); continue; }
                if (mark.role != StudioRole.Survivor) problems.Add($"{mark.actor}: only Survivor marks can be driven (this one is a {mark.role}).");
                foreach (var step in drive.steps)
                {
                    if (!Actions.Contains(step.action)) problems.Add($"{mark.actor}: a drive step does \"{step.action}\", which is not one of {string.Join(", ", Actions)}.");
                    if (step.action == "path" && step.points.Count == 0) problems.Add($"{mark.actor}: a path step has no points.");
                    if ((step.action == "punch" || step.action == "fire") && string.IsNullOrEmpty(step.target)) problems.Add($"{mark.actor}: a {step.action} step needs a target.");
                    if (!string.IsNullOrEmpty(step.target) && !ValidTarget(set, step.target)) problems.Add($"{mark.actor}: a {step.action} step has a target the Studio does not understand: \"{step.target}\".");
                }
            }
            return problems;
        }

        static bool ValidTarget(StudioSet set, string target)
        {
            if (target == "hazard") return true;
            if (target.StartsWith("actor:") || target.StartsWith("head:")) return set.Mark(target.Substring(target.IndexOf(':') + 1)) != null;
            if (target.StartsWith("point:")) return StudioOverrides.TryParsePoint(target.Substring(6), out _);
            if (target.StartsWith("block:")) return int.TryParse(target.Substring(6), out int i) && i >= 0;
            return false;
        }

        // ---- One driven actor ----

        readonly StudioStage stage;
        readonly StudioDrive drive;
        readonly CastleSurvivor motor;
        readonly int seed;
        readonly float human;
        readonly string name;

        int current = -1;            // which step is running
        bool finished = true;        // the current step is over (waiting for the next one's time, or all done)
        float stepStart;
        bool stepBegun;

        Vector3 stick;               // the move stick as the game gets it (world direction, length 0..1)
        float bodyYaw;
        float headYaw, headPitch, headRoll;
        bool ready;
        // The head moves like a person's: quick, eased moves between held looks.
        float fromYaw, fromPitch, toYaw, toPitch, moveStart, moveTime = .01f;

        // Standing orders that outlast their step.
        string lookTarget = ""; Vector3 lookPoint; bool looking; float lookUntil;
        bool jumpArmed; Vector3 jumpAt; float jumpArmedUntil, edgeAllowedUntil = -1;
        float crouchWanted, crouch, crouchUntil = float.MaxValue;
        int glances; float nextGlance = -1, glanceUntil = -1; Vector3 glancePoint; bool glancing;
        int jumpPresses; float lastJump = -10, stuckFor;

        // goTo / path
        readonly List<Vector3> route = new List<Vector3>();
        int leg; Vector3 legStart; float bend, overshoot, feetWait; bool settling; float settleStart;
        // punch / fire
        bool swung; float swungAt;
        bool edgeLogged;

        StudioPerformer(StudioStage stage, StudioDrive drive, CastleSurvivor motor)
        {
            this.stage = stage; this.drive = drive; this.motor = motor;
            seed = drive.seed * 7919 ^ StudioHuman.SeedOf(drive.actor);
            human = Mathf.Clamp(drive.human, 0, 2);
            name = stage.set.Mark(drive.actor)?.actor ?? drive.actor;
        }

        void Log(float time, string line) => StudioRun.CueLog.Add($"{time:F3}s drive {name}: {line}");

        float R(int index, int salt) => StudioHuman.Rand(seed, index, salt);
        float Range(int index, int salt, float min, float max) => StudioHuman.Range(seed, index, salt, min, max);

        Vector3 Feet => motor.FootPosition;
        Vector3 Head => motor.viewCamera.transform.position;
        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        public CastleBotCommand ReadCommand(CastleSurvivor body, float dt)
        {
            var command = default(CastleBotCommand);
            if (StudioRun.Stage != stage || !motor) return command;
            dt = Mathf.Clamp(dt, .001f, .1f);
            if (!ready)
            {
                bodyYaw = headYaw = motor.transform.eulerAngles.y;
                headPitch = 0; fromYaw = toYaw = headYaw;
                ready = true;
            }
            // Nothing happens until the tape really records (lead-in, countdown).
            if (!StudioRun.Recording || StudioRun.LeadIn || StudioRun.Countdown > 0) { PoseHead(bodyYaw, dt); return command; }
            float time = StudioRun.Frame / (float)stage.Fps;

            var intent = Run(time, dt);
            // The stick speeds up and slows down like a thumb on a stick (an exact robot snaps).
            Vector3 wanted = Flat(intent.move) * Mathf.Clamp01(intent.speed);
            float up = human > .001f ? 2.6f / human : 1000, down = human > .001f ? 3.6f / human : 1000;
            float length = Mathf.MoveTowards(stick.magnitude, wanted.magnitude, (wanted.magnitude > stick.magnitude ? up : down) * dt);
            Vector3 direction = stick.sqrMagnitude > .0001f ? stick.normalized : wanted.sqrMagnitude > .0001f ? wanted.normalized : Vector3.zero;
            if (wanted.sqrMagnitude > .0001f) direction = Vector3.RotateTowards(direction.sqrMagnitude > .0001f ? direction : wanted.normalized, wanted.normalized, 300 * Mathf.Deg2Rad * dt, 0);
            stick = direction * length;

            // Never off an edge by accident. An armed jump near the edge goes now instead.
            bool edgeOk = !drive.safeEdges || intent.edge || time < edgeAllowedUntil || !motor.Grounded;
            if (stick.sqrMagnitude > .0025f && !edgeOk && EdgeAhead(stick.normalized))
            {
                if (jumpArmed && Vector3.Distance(Flat(Feet), Flat(jumpAt)) < 1.4f) intent.jump = true;
                else
                {
                    if (!edgeLogged) { Log(time, "stopped at an edge (no ground ahead)"); edgeLogged = true; }
                    stick = Vector3.zero;
                }
            }
            if (jumpArmed && (time > jumpArmedUntil || Vector3.Distance(Flat(Feet), Flat(jumpAt)) < .35f)) { if (time <= jumpArmedUntil) intent.jump = true; jumpArmed = false; }

            // Blocked by a low step while pushing forward: hop up it, as a person would.
            float moved = Flat(motor.Capsule.velocity).magnitude;
            stuckFor = motor.Grounded && stick.magnitude > .45f && moved < .35f ? stuckFor + dt : 0;
            if (stuckFor > .25f && LedgeAhead(stick.normalized)) { intent.jump = true; stuckFor = 0; }
            // A second press in the air grabs a ledge (the game's vault).
            if (!motor.Grounded && jumpPresses == 1 && time - lastJump > .14f && time - lastJump < .6f && (intent.climb || LedgeAhead(stick.sqrMagnitude > .0001f ? stick.normalized : Forward)))
            { command.jump = true; jumpPresses = 2; }
            if (intent.jump && motor.Grounded && time - lastJump > .3f)
            {
                command.jump = true; lastJump = time; jumpPresses = 1;
                edgeAllowedUntil = time + .9f;
                Log(time, "jump");
            }
            if (motor.Grounded && time - lastJump > .3f) jumpPresses = 0;

            // The body faces where it goes; standing, it turns after the head once the head is far round.
            Vector3 face = intent.face.sqrMagnitude > .0001f ? Flat(intent.face - Feet) : Vector3.zero;
            float faceYaw = bodyYaw;
            if (stick.sqrMagnitude > .01f) faceYaw = StudioHuman.Yaw(stick);
            else if (face.sqrMagnitude > .0001f) faceYaw = StudioHuman.Yaw(face);
            else if (Mathf.Abs(Mathf.DeltaAngle(bodyYaw, headYaw)) > 50) faceYaw = headYaw;
            command.move = stick;
            command.look = Quaternion.Euler(0, faceYaw, 0) * Vector3.forward;
            command.climbHeld = intent.climb;
            // The motor turns the body at up to 360 degrees a second; the head is set against where the body will be.
            bodyYaw = Mathf.MoveTowardsAngle(motor.transform.eulerAngles.y, faceYaw, 360 * dt);

            // Where the head looks.
            if (intent.gaze.HasValue) Aim(intent.gaze.Value, time);
            PoseHead(bodyYaw, dt);
            Crouch(time, dt);
            return command;
        }

        Vector3 Forward => Quaternion.Euler(0, bodyYaw, 0) * Vector3.forward;

        // ---- Head ----

        // Start a quick eased head move to look at a point, or follow it if it only moved a little.
        void Aim(Vector3 point, float time)
        {
            Vector3 head = Head;
            float yaw = StudioHuman.Yaw(Flat(point - head)), pitch = Mathf.Clamp(StudioHuman.Pitch(head, point), -50, 55);
            if (Flat(point - head).sqrMagnitude < .0004f) yaw = toYaw;
            float jump = Mathf.Abs(Mathf.DeltaAngle(toYaw, yaw)) + Mathf.Abs(pitch - toPitch);
            if (jump > 7)
            {
                fromYaw = headYaw; fromPitch = headPitch; moveStart = time;
                float size = Mathf.Abs(Mathf.DeltaAngle(headYaw, yaw)) + Mathf.Abs(pitch - headPitch) * .5f;
                moveTime = human < .001f ? .01f : Mathf.Clamp(.1f + size / 420 + Range(++glances, 30, 0, .05f) * human, .12f, .7f);
            }
            toYaw = yaw; toPitch = pitch;
        }

        void PoseHead(float body, float dt)
        {
            float time = StudioRun.Frame / (float)stage.Fps;
            float u = StudioHuman.Ease((time - moveStart) / Mathf.Max(.01f, moveTime));
            headYaw = fromYaw + Mathf.DeltaAngle(fromYaw, toYaw) * u;
            headPitch = Mathf.Lerp(fromPitch, toPitch, u);
            if (u >= 1) { fromYaw = headYaw; fromPitch = headPitch; }
            // A little roll into turns, and a little drift while holding a look.
            headRoll = Mathf.Clamp(Mathf.DeltaAngle(body, headYaw) * -.04f, -4, 4) * human;
            float drift = human * StudioHuman.Wobble(seed, 40, time, .4f) * 1.2f;
            float localYaw = Mathf.Clamp(Mathf.DeltaAngle(body, headYaw) + drift, -120, 120);
            float pitch = headPitch + human * StudioHuman.Wobble(seed, 41, time, .5f) * .8f;
            motor.viewCamera.transform.localRotation = Quaternion.Euler(pitch, localYaw, headRoll);
        }

        void Crouch(float time, float dt)
        {
            if (time > crouchUntil) { crouchWanted = 0; crouchUntil = float.MaxValue; }
            if (crouchWanted <= 0 && crouch <= 0) return;
            crouch = Mathf.MoveTowards(crouch, crouchWanted, dt * 2.5f);
            // In VR a person crouches by lowering their head; the avatar bends to match.
            float eye = motor.eyeHeight * motor.playerScale;
            motor.viewCamera.transform.localPosition = Vector3.up * eye * (1 - .45f * crouch);
        }

        // ---- Ground checks ----

        bool EdgeAhead(Vector3 direction)
        {
            float speed = Flat(motor.Capsule.velocity).magnitude;
            Vector3 probe = Feet + direction * (motor.Capsule.radius + .2f + speed * .12f);
            int mask = (1 << Layers.Block) | (1 << Layers.Surface) | 1;
            float drop = .65f;
            if (!Physics.Raycast(probe + Vector3.up * .5f, Vector3.down, out var hit, .5f + drop, mask, QueryTriggerInteraction.Ignore)) return true;
            float water = Services.World?.WaterHeight(hit.point) ?? float.NegativeInfinity;
            return hit.point.y <= water + .05f;
        }

        // A step or block face just in front, low enough to hop onto.
        bool LedgeAhead(Vector3 direction)
        {
            int mask = (1 << Layers.Block) | (1 << Layers.Surface) | 1;
            Vector3 knee = Feet + Vector3.up * .15f;
            return Physics.Raycast(knee, direction, motor.Capsule.radius + .35f, mask, QueryTriggerInteraction.Ignore);
        }

        // ---- Targets ----

        bool Where(string target, Vector3 point, float time, bool head, out Vector3 world)
        {
            world = stage.ToWorld(point);
            if (string.IsNullOrEmpty(target)) return true;
            if (target.StartsWith("actor:") || target.StartsWith("head:"))
            {
                string id = target.Substring(target.IndexOf(':') + 1);
                bool wantHead = head || target.StartsWith("head:");
                var live = stage.Live(id);
                if (live)
                {
                    var other = live.GetComponent<CastleSurvivor>();
                    world = other ? (wantHead ? other.viewCamera.transform.position : other.FootPosition) : live.transform.position;
                    return true;
                }
                var pose = StudioActorPoses.At(stage.tape, id, time, stage.Fps);
                if (!pose.found) return false;
                world = wantHead ? pose.head : pose.feet;
                return true;
            }
            if (target.StartsWith("point:")) { if (!StudioOverrides.TryParsePoint(target.Substring(6), out var p)) return false; world = stage.ToWorld(p); return true; }
            if (target.StartsWith("block:") && int.TryParse(target.Substring(6), out int index) && index >= 0 && index < stage.blocks.Length && stage.blocks[index])
            { world = stage.blocks[index].position; return true; }
            if (target == "hazard")
            {
                var hazards = stage.tape.FindType(HazardTrack.Id)?.ActiveTake;
                return HazardTrack.CenterAt(hazards, StudioRun.Frame, out world);
            }
            return false;
        }

        // ---- Steps ----

        struct Intent
        {
            public Vector3 move; public float speed;
            public Vector3? gaze; public Vector3 face;
            public bool jump, climb, edge;
        }

        static float Pace(string pace) =>
            pace == "run" ? 1 : pace == "jog" ? .7f : pace == "walk" || string.IsNullOrEmpty(pace) ? .42f :
            float.TryParse(pace, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? Mathf.Clamp01(v) : .42f;

        Intent Run(float time, float dt)
        {
            var intent = new Intent();
            // Move on when the current step is done and the next one's time has come. Steps that only set
            // something (look, gear, crouch, an armed jump) start the next step in the same frame.
            for (int guard = 0; guard < 32; guard++)
            {
                bool running = current >= 0 && current < drive.steps.Count && !finished;
                if (running && !Done(drive.steps[current], time)) break;
                finished = true;
                int next = current + 1;
                if (next >= drive.steps.Count) break;
                if (drive.steps[next].at >= 0 && time < drive.steps[next].at) break;
                current = next; finished = false;
                stepStart = time;
                Begin(drive.steps[current], time);
                stepBegun = true;
            }
            bool busy = current >= 0 && current < drive.steps.Count && !finished;
            if (busy) Tick(drive.steps[current], time, ref intent);
            // Idle (between steps, after the last one, or during a wait): the head wanders like anyone's.
            if (busy && (drive.steps[current].action == "wait" || drive.steps[current].action == "stop")) busy = false;
            string doing = busy ? drive.steps[current].action : "";
            bool focused = doing == "punch" || doing == "fire" || doing == "face" || doing == "climb";
            if (!focused)
            {
                // An explicit look holds the head; otherwise glances while travelling, and more often while idle.
                if (looking && time <= lookUntil && Where(lookTarget, lookPoint, time, true, out var seen)) intent.gaze = seen;
                else if (doing == "goTo" || doing == "path") { if (intent.speed > .2f) Glance(time, ref intent, true); }
                else Glance(time, ref intent, false);
            }
            return intent;
        }

        void Begin(StudioDriveStep step, float time)
        {
            edgeLogged = false;
            int n = current;
            switch (step.action)
            {
                case "goTo":
                case "path":
                    route.Clear();
                    if (step.action == "path") route.AddRange(step.points.Select(p => stage.ToWorld(p)));
                    else if (!Where(step.target, step.point, time, false, out var to)) { Log(time, "goTo: the target is not there"); route.Add(Feet); }
                    else route.Add(to);
                    leg = 0; legStart = Feet; settling = false;
                    // A gentle curve, a small overshoot, and the feet start a moment after the head turns.
                    bend = human * Range(n, 1, -.07f, .07f);
                    overshoot = human * Range(n, 2, .04f, .12f) * Mathf.Lerp(.6f, 1.4f, Pace(step.pace));
                    feetWait = time + human * Range(n, 3, .18f, .34f);
                    Log(time, $"{step.action} to {route[route.Count - 1] - stage.anchor:F2} at {step.pace}");
                    break;
                case "look":
                    lookTarget = step.target ?? ""; lookPoint = step.point;
                    looking = !(string.IsNullOrEmpty(lookTarget) && step.point == Vector3.zero);
                    lookUntil = step.duration > 0 ? time + step.duration : float.MaxValue;
                    break;
                case "jump":
                    if (!string.IsNullOrEmpty(step.target) && Where(step.target, step.point, time, false, out var at))
                    { jumpArmed = true; jumpAt = at; jumpArmedUntil = time + (step.duration > 0 ? step.duration : 8); }
                    else jumpArmed = false;
                    break;
                case "gear":
                    try { Log(time, "gear " + new GearCue().Run(stage, new StudioCue { action = "gear", actor = drive.actor, weapon = step.weapon })); }
                    catch (Exception error) { Log(time, "gear REJECTED: " + error.Message); }
                    break;
                case "crouch":
                    crouchWanted = Mathf.Clamp01(step.amount);
                    crouchUntil = step.duration > 0 ? time + step.duration : float.MaxValue;
                    break;
                case "punch":
                case "fire":
                    swung = false;
                    break;
            }
        }

        bool Done(StudioDriveStep step, float time)
        {
            float age = time - stepStart;
            switch (step.action)
            {
                case "goTo": case "path": return stepBegun && (settling && age > 0 && Arrived(time) || age > (step.duration > 0 ? step.duration : 10));
                case "face": return stepBegun && (Facing(step, time) < 8 || age > (step.duration > 0 ? step.duration : 1.5f));
                case "punch": case "fire": return stepBegun && (swung && time - swungAt > .35f || age > (step.duration > 0 ? step.duration : 4));
                case "climb": return stepBegun && (Climbed(step, time) || age > (step.duration > 0 ? step.duration : 6));
                case "stop": return age >= (step.duration > 0 ? step.duration : .4f);
                case "wait": return age >= (step.duration > 0 ? step.duration : 1);
                case "jump": return stepBegun && (jumpArmed || time - lastJump < .05f || age > 0);
                default: return stepBegun;   // look, gear, crouch: start and go on
            }
        }

        void Tick(StudioDriveStep step, float time, ref Intent intent)
        {
            switch (step.action)
            {
                case "goTo": case "path": Travel(step, time, ref intent); break;
                case "face":
                    if (Where(step.target, step.point, time, true, out var faceAt)) { intent.gaze = faceAt; intent.face = faceAt; }
                    break;
                case "jump":
                    if (!jumpArmed) intent.jump = true;
                    break;
                case "punch": case "fire": Strike(step, time, ref intent); break;
                case "climb":
                    if (!Where(step.target, step.point, time, false, out var top)) break;
                    Vector3 toward = Flat(top - Feet);
                    intent.gaze = top; intent.climb = true; intent.face = top;
                    intent.move = toward; intent.speed = toward.magnitude > .2f ? Pace(step.pace) : 0;
                    if (motor.Grounded && toward.magnitude < .9f && top.y > Feet.y + .3f) intent.jump = true;
                    intent.edge = true;
                    break;
            }
        }

        // ---- Travelling ----

        void Travel(StudioDriveStep step, float time, ref Intent intent)
        {
            if (route.Count == 0) return;
            Vector3 feet = Feet;
            Vector3 last = route[route.Count - 1];
            Vector3 lastLeg = Flat(last - (route.Count > 1 ? route[route.Count - 2] : legStart));
            Vector3 stopAt = last + (lastLeg.sqrMagnitude > .0001f ? lastLeg.normalized : Vector3.zero) * overshoot;
            intent.edge = step.edge;
            float pace = Pace(step.pace);
            // The head goes first: toward the next waypoint (or a bit beyond it on the last one).
            Vector3 lookAhead = leg < route.Count - 1 ? route[leg + 1] : last;
            intent.gaze = Vector3.Lerp(route[leg], lookAhead, .35f) + Vector3.up * (Head.y - feet.y) * .6f;
            if (settling)
            {
                // Stepping back onto the spot after the overshoot.
                Vector3 back = Flat(last - feet);
                intent.move = back; intent.speed = back.magnitude > .05f ? .2f : 0;
                intent.gaze = last + lastLeg.normalized * 3 + Vector3.up * (Head.y - feet.y);
                return;
            }
            if (time < feetWait) return;
            Vector3 a = leg == 0 ? legStart : route[leg - 1], b = leg == route.Count - 1 ? stopAt : route[leg];
            Vector3 segment = Flat(b - a);
            float length = Mathf.Max(.01f, segment.magnitude);
            // Next waypoint once close to this one.
            if (leg < route.Count - 1 && Flat(route[leg] - feet).magnitude < .6f) { leg++; return; }
            float along = Mathf.Clamp01(Vector3.Dot(Flat(feet - a), segment) / (length * length));
            float ahead = Mathf.Clamp01(along + (.6f + stick.magnitude * 1.2f) / length);
            Vector3 side = Vector3.Cross(Vector3.up, segment / length);
            Vector3 carrot = Vector3.Lerp(a, b, ahead) + side * (bend * length * Mathf.Sin(Mathf.PI * ahead));
            Vector3 toCarrot = Flat(carrot - feet);
            float remaining = leg == route.Count - 1 ? Flat(stopAt - feet).magnitude : float.MaxValue;
            // Slow down before the stop, the way a person brakes.
            float brake = .25f + pace * motor.walkSpeed * .32f;
            intent.move = toCarrot.sqrMagnitude > .0001f ? toCarrot : segment;
            intent.speed = pace * Mathf.Clamp01(remaining / brake + .08f);
            if (leg == route.Count - 1 && (remaining < .07f || Vector3.Dot(Flat(stopAt - feet), segment) < 0))
            {
                intent.speed = 0;
                if (overshoot > .02f && human > .001f) { settling = true; settleStart = time; }
                else { settling = true; settleStart = time - 1; }
            }
        }

        bool Arrived(float time)
        {
            if (route.Count == 0) return true;
            return Flat(route[route.Count - 1] - Feet).magnitude < .07f || time - settleStart > .8f;
        }

        float Facing(StudioDriveStep step, float time)
        {
            if (!Where(step.target, step.point, time, true, out var at)) return 0;
            float yaw = StudioHuman.Yaw(Flat(at - Feet));
            return Mathf.Max(Mathf.Abs(Mathf.DeltaAngle(bodyYaw, yaw)), Mathf.Abs(Mathf.DeltaAngle(headYaw, yaw)));
        }

        bool Climbed(StudioDriveStep step, float time) =>
            Where(step.target, step.point, time, false, out var top) && Feet.y > top.y - .25f && Flat(top - Feet).magnitude < .7f;

        // ---- Punch and fire ----

        void Strike(StudioDriveStep step, float time, ref Intent intent)
        {
            if (!Where(step.target, step.point, time, false, out var feet) || !Where(step.target, step.point, time, true, out var head)) return;
            intent.gaze = head; intent.face = feet;
            if (swung) return;
            Vector3 toward = Flat(feet - Feet);
            float distance = toward.magnitude;
            bool punch = step.action == "punch";
            float reach = punch ? Mathf.Max(.4f, step.reach) : 0;
            if (punch && distance > reach)
            {
                // Close the gap, braking into range.
                intent.move = toward;
                intent.speed = Pace(step.pace) * Mathf.Clamp01((distance - reach * .85f) / .8f + .1f);
                return;
            }
            // Square up, then swing (or shoot).
            if (Facing(step, time) > (punch ? 15 : 10)) return;
            swung = true; swungAt = time;
            Log(time, (punch ? "punch " : "fire ") + Hit(step, head, distance));
        }

        string Hit(StudioDriveStep step, Vector3 head, float distance)
        {
            string target = step.target.StartsWith("actor:") || step.target.StartsWith("head:") ? step.target.Substring(step.target.IndexOf(':') + 1) : "";
            var cue = new StudioCue { actor = drive.actor, target = target, point = stage.ToStage(head), optional = true };
            var equipment = motor.GetComponent<IEquipmentControls>()?.Equipment;
            bool armed = equipment != null && equipment.drawn && equipment.kind >= EquipmentKind.Blaster;
            try
            {
                if (armed && equipment.kind == EquipmentKind.BoxingGlove && target.Length > 0 && stage.Live(target)) { cue.action = GlovePunchCue.Id; return new GlovePunchCue().Run(stage, cue) + $" at {distance:0.00} m"; }
                if (armed) { cue.action = "fire"; return new FireCue().Run(stage, cue) + $" at {distance:0.00} m"; }
                // No weapon: the same punch request the game's own controls send.
                bool sent = motor.RequestPunch(Head, (head - Head).normalized, 2);
                return $"unarmed punch request at {distance:0.00} m: {(sent ? "sent" : "refused (punching is off in this mode, or cooling down)")}";
            }
            catch (Exception error) { return "REJECTED: " + error.Message; }
        }

        // ---- Glances ----

        // While travelling or waiting the head looks around now and then: at other people, then the horizon or ground.
        void Glance(float time, ref Intent intent, bool busy)
        {
            if (human < .001f) return;
            if (nextGlance < 0) nextGlance = time + Range(0, 50, .6f, 1.6f);
            if (glancing && time > glanceUntil) { glancing = false; nextGlance = time + Range(glances, 51, busy ? .9f : .5f, busy ? 2.4f : 1.6f) / human; }
            if (!glancing && time >= nextGlance)
            {
                glances++;
                glancing = true;
                glanceUntil = time + Range(glances, 52, busy ? .35f : .5f, busy ? .9f : 1.8f);
                glancePoint = PickGlance(time);
            }
            if (glancing) intent.gaze = glancePoint;
            else if (!intent.gaze.HasValue) intent.gaze = Head + Forward * 6 + Vector3.down * .6f;
        }

        Vector3 PickGlance(float time)
        {
            Vector3 head = Head;
            float r = R(glances, 53);
            // Somebody nearby is the most interesting thing.
            var people = new List<Vector3>();
            foreach (var mark in stage.set.marks)
            {
                if (mark.id == drive.actor || mark.role == StudioRole.Hand) continue;
                if (Where("head:" + mark.id, Vector3.zero, time, true, out var other) && Vector3.Distance(other, head) < 14) people.Add(other);
            }
            if (people.Count > 0 && r < .55f) return people[Mathf.Min(people.Count - 1, Mathf.FloorToInt(R(glances, 54) * people.Count))];
            float sign = R(glances, 55) < .5f ? -1 : 1;
            float yaw = bodyYaw + sign * Range(glances, 56, 25, 70);
            float down = R(glances, 57) < .35f ? Range(glances, 58, 8, 22) : Range(glances, 58, -4, 3);
            return head + Quaternion.Euler(down, yaw, 0) * Vector3.forward * 8;
        }
    }
}
