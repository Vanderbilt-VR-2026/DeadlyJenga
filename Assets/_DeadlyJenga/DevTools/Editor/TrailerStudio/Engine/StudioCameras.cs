using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Where an actor is at any tape time, read straight from the tape (so cameras and overrides get the
    // same answer whether the tape is playing, paused or being rendered).
    public static class StudioActorPoses
    {
        public struct Pose { public bool found; public Vector3 feet, head; public Quaternion headRotation; }

        // True while an override is asking where another actor is, so two actors looking at each other
        // do not ask about each other forever.
        static bool insideOverride;

        public static Pose At(StudioTape tape, string markId, float time, int fps)
        {
            var track = tape?.Find(markId);
            var take = track?.ActiveTake;
            if (take == null || take.frames.Count == 0) return default;
            float exact = Mathf.Clamp(time * fps, take.start, take.End - 1);
            int a = Mathf.FloorToInt(exact), b = Mathf.Min(a + 1, take.End - 1);
            var pose = ActorFrame.Read(track.type, take.frames[a - take.start]);
            if (!pose.visible) return default;
            if (a != b)
            {
                var next = ActorFrame.Read(track.type, take.frames[b - take.start]);
                if (next.visible) pose = ActorFrame.Blend(pose, next, exact - a);
            }
            // Where the actor is really shown: with its override layers (knockbacks, falls, look-ats) applied.
            var stage = StudioRun.Stage;
            if (stage != null && stage.tape == tape && !insideOverride)
            {
                insideOverride = true;
                try { foreach (var filter in ActorFrameFilters.For(stage, track)) filter.Apply(stage, track, time, pose); }
                finally { insideOverride = false; }
            }
            if (pose.isHand) return new Pose { found = true, feet = pose.target.position, head = pose.target.position, headRotation = pose.target.rotation };
            return new Pose { found = true, feet = pose.body.feet, head = pose.body.head, headRotation = pose.body.headRotation };
        }
    }

    // Works out where a camera is and what its lens does at a tape time, from its keys and behaviors.
    public static class StudioCameras
    {
        public struct Shot
        {
            public Vector3 position;
            public Quaternion rotation;
            public float focal, focus, aperture, fieldOfView;
            public string ownBody;     // first person: the mark id whose own arms, hands and weapon this lens shows (else empty)
        }

        public static float FieldOfView(float focal, float sensorHeight) =>
            2 * Mathf.Atan(sensorHeight / (2 * Mathf.Max(4, focal))) * Mathf.Rad2Deg;
        public static float FocalFor(float fieldOfView, float sensorHeight) =>
            sensorHeight / (2 * Mathf.Tan(Mathf.Clamp(fieldOfView, 4, 150) * Mathf.Deg2Rad / 2));

        static float Ease(int ease, float u)
        {
            switch (ease)
            {
                case 0: return u;
                case 2: return u * u;
                case 3: return 1 - (1 - u) * (1 - u);
                case 4: return u < 1 ? 0 : 1;
                case 5: return u * u * u * (u * (u * 6 - 15) + 10);   // extra smooth start and stop
                default: return u * u * (3 - 2 * u);
            }
        }

        static Vector3 CatmullRom(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u) =>
            .5f * ((2 * b) + (-a + c) * u + (2 * a - 5 * b + 4 * c - d) * u * u + (-a + 3 * b - 3 * c + d) * u * u * u);

        // The keys' own answer at a time: position on the path, rotation and lens, before behaviors.
        static StudioCameraKey Keyed(StudioCameraTrack camera, float time, out Quaternion rotation)
        {
            var keys = camera.keys;
            rotation = Quaternion.identity;
            if (keys.Count == 0) return new StudioCameraKey { position = new Vector3(6, 3, -8) };
            if (camera.constantSpeed && keys.Count > 2) time = EvenTime(camera, time);
            int next = keys.FindIndex(k => k.time > time);
            if (next == 0) { rotation = Quaternion.Euler(keys[0].euler); return keys[0]; }
            if (next < 0) { rotation = Quaternion.Euler(keys[keys.Count - 1].euler); return keys[keys.Count - 1]; }
            var from = keys[next - 1]; var to = keys[next];
            float u = Ease(to.ease, Mathf.InverseLerp(from.time, to.time, time));
            var result = new StudioCameraKey
            {
                time = time, focal = Mathf.Lerp(from.focal, to.focal, u), focus = Mathf.Lerp(from.focus, to.focus, u),
                aperture = Mathf.Lerp(from.aperture, to.aperture, u), shake = Mathf.Lerp(from.shake, to.shake, u)
            };
            if (camera.spline && keys.Count > 2)
            {
                var before = keys[Mathf.Max(0, next - 2)].position; var after = keys[Mathf.Min(keys.Count - 1, next + 1)].position;
                result.position = CatmullRom(before, from.position, to.position, after, u);
            }
            else result.position = Vector3.Lerp(from.position, to.position, u);
            rotation = Quaternion.Slerp(Quaternion.Euler(from.euler), Quaternion.Euler(to.euler), u);
            return result;
        }

        // Constant speed: spread time evenly along the path's length instead of by key spacing.
        static float EvenTime(StudioCameraTrack camera, float time)
        {
            var keys = camera.keys;
            float first = keys[0].time, last = keys[keys.Count - 1].time;
            if (time <= first || time >= last) return time;
            var lengths = new float[keys.Count];
            for (int i = 1; i < keys.Count; i++) lengths[i] = lengths[i - 1] + Vector3.Distance(keys[i - 1].position, keys[i].position);
            float wanted = Mathf.InverseLerp(first, last, time) * lengths[keys.Count - 1];
            for (int i = 1; i < keys.Count; i++)
                if (wanted <= lengths[i])
                    return Mathf.Lerp(keys[i - 1].time, keys[i].time, Mathf.InverseLerp(lengths[i - 1], lengths[i], wanted));
            return time;
        }

        static readonly Dictionary<string, (float turn, float move, float rate)> Noise = new Dictionary<string, (float, float, float)>
        {
            { "subtle", (.18f, .004f, .7f) }, { "walking", (.6f, .022f, 1.8f) }, { "running", (1.3f, .045f, 2.9f) }, { "impact", (2.6f, .05f, 11f) }
        };
        public static readonly List<string> NoiseNames = new List<string> { "None", "subtle", "walking", "running", "impact" };
        public static readonly List<string> Behaviors = new List<string> { "", "follow", "ride", "twoShot", "orbit", "dollyZoom", "firstPerson" };
        public static readonly List<string> BehaviorLabels = new List<string> { "Just the keys", "Aim at an actor (follow)", "Ride along with an actor", "Keep two actors in frame", "Orbit an actor", "Dolly zoom on an actor", "First person: an actor's eyes" };

        // cameraTime may run ahead of tapeTime during a freeze frame (the camera keeps moving).
        public static Shot Evaluate(StudioCameraTrack camera, StudioSet set, StudioTape tape, Vector3 anchor, float tapeTime, float cameraTime)
        {
            var key = Keyed(camera, cameraTime, out var rotation);
            Vector3 position = anchor + key.position;
            float focal = key.focal, focus = key.focus;
            string ownBody = "";
            var subject = string.IsNullOrEmpty(camera.subject) ? default : StudioActorPoses.At(tape, camera.subject, tapeTime - Mathf.Max(0, camera.lag), set.fps);
            var subjectNow = string.IsNullOrEmpty(camera.subject) ? default : StudioActorPoses.At(tape, camera.subject, tapeTime, set.fps);
            // The aim trails a little behind the actor (lag), but never by more than half a metre,
            // so a falling or thrown actor stays in the picture.
            Vector3 Trail(Vector3 now, Vector3 late) => Vector3.MoveTowards(now, late, .5f);
            Vector3 aim = subject.found ? subject.feet + camera.aimOffset : position + rotation * Vector3.forward * 5;
            if (subject.found && subjectNow.found) aim = Trail(subjectNow.feet, subject.feet) + camera.aimOffset;

            switch (camera.behavior)
            {
                case "follow":
                    if (subject.found) rotation = Quaternion.LookRotation(aim - position) * Quaternion.Euler(0, 0, rotation.eulerAngles.z);
                    break;
                case "ride":
                    // The keys are offsets from the actor instead of places on the map.
                    if (subjectNow.found) { position = subjectNow.feet + key.position; rotation = Quaternion.LookRotation(aim - position) * Quaternion.Euler(0, 0, rotation.eulerAngles.z); }
                    break;
                case "twoShot":
                    var other = string.IsNullOrEmpty(camera.second) ? default : StudioActorPoses.At(tape, camera.second, tapeTime - Mathf.Max(0, camera.lag), set.fps);
                    var otherNow = string.IsNullOrEmpty(camera.second) ? default : StudioActorPoses.At(tape, camera.second, tapeTime, set.fps);
                    if (subject.found && other.found && otherNow.found)
                    {
                        Vector3 otherAim = Trail(otherNow.feet, other.feet) + camera.aimOffset;
                        // Widen the lens until both fit, with some room around them.
                        float apart = Vector3.Angle(aim - position, otherAim - position);
                        float aspect = set.render.width / (float)Mathf.Max(1, set.render.height);
                        float needed = Mathf.Clamp(apart / Mathf.Min(1, aspect) * 1.5f + 8, 8, 110);
                        focal = Mathf.Min(focal, FocalFor(needed, camera.sensorHeight));
                        aim = (aim + otherAim) * .5f;
                        rotation = Quaternion.LookRotation(aim - position);
                    }
                    break;
                case "orbit":
                    if (subjectNow.found)
                    {
                        Vector3 arm = key.position;
                        float angle = camera.orbitSpeed * (cameraTime - camera.start);
                        position = subjectNow.feet + Quaternion.Euler(0, angle, 0) * arm;
                        rotation = Quaternion.LookRotation(aim - position);
                    }
                    break;
                case "dollyZoom":
                    if (subjectNow.found && camera.keys.Count > 0)
                    {
                        // Keep the actor the same size while the camera moves: longer lens as it backs away.
                        var first = camera.keys[0];
                        var atFirst = StudioActorPoses.At(tape, camera.subject, first.time, set.fps);
                        float startDistance = Vector3.Distance(anchor + first.position, (atFirst.found ? atFirst.feet : subjectNow.feet) + camera.aimOffset);
                        float nowDistance = Vector3.Distance(position, subjectNow.feet + camera.aimOffset);
                        focal = Mathf.Clamp(first.focal * nowDistance / Mathf.Max(.2f, startDistance), 8, 400);
                        rotation = Quaternion.LookRotation(subjectNow.feet + camera.aimOffset - position);
                    }
                    break;
                case "firstPerson":
                    // An actor's eyes, smoothed and level, with the game's own first-person body (StudioFirstPerson).
                    if (subjectNow.found && StudioFirstPerson.Eye(camera, set, tape, tapeTime, out var eye, out var look))
                    {
                        position = eye; rotation = look;
                        if (camera.fpFieldOfView > 1) focal = FocalFor(camera.fpFieldOfView, camera.sensorHeight);
                        if (camera.fpOwnBody) ownBody = camera.subject;
                    }
                    break;
            }

            // Targets beyond actors: ride on any target, aim at a list of targets that ease into each other.
            var stage = StudioRun.Stage != null && StudioRun.Stage.tape == tape ? StudioRun.Stage : null;
            bool aimedByList = false;
            if (stage != null && !string.IsNullOrEmpty(camera.rideTarget) && StudioTargets.TryPoint(stage, camera.rideTarget, tapeTime, out var ridden))
                position = ridden + key.position;
            if (stage != null && camera.aims != null && camera.aims.Count > 0 && AimPoint(stage, camera, tapeTime, cameraTime, out var aimed) && (aimed - position).sqrMagnitude > .0001f)
            {
                aim = aimed; aimedByList = true;
                rotation = Quaternion.LookRotation(aim - position) * Quaternion.Euler(0, 0, rotation.eulerAngles.z);
            }

            if (camera.avoidBlocks && camera.behavior != "firstPerson")
            {
                // If a block is between the camera and what it looks at, move in front of the block.
                Vector3 toCamera = position - aim;
                if (toCamera.sqrMagnitude > .04f && Physics.SphereCast(aim, .12f, toCamera.normalized, out var hit, toCamera.magnitude,
                    (1 << Layers.Block) | (1 << Layers.Surface), QueryTriggerInteraction.Ignore))
                    position = aim + toCamera.normalized * Mathf.Max(.25f, hit.distance - .12f);
            }

            if (!string.IsNullOrEmpty(camera.noise) && Noise.TryGetValue(camera.noise, out var noise))
            {
                // Handheld shake from smooth noise of tape time, so every replay shakes the same way.
                float amount = camera.noiseStrength * key.shake;
                // The shake's own starting point, from the camera's id (a plain sum, so it never changes between Unity versions).
                int idSum = 0;
                foreach (char letter in camera.id) idSum = (idSum * 31 + letter) % 97;
                float t = cameraTime * noise.rate, seed = idSum;
                Vector3 N(float offset) => new Vector3(Mathf.PerlinNoise(t, seed + offset) - .5f, Mathf.PerlinNoise(t + 31.7f, seed + offset + 5) - .5f, Mathf.PerlinNoise(t + 63.1f, seed + offset + 11) - .5f) * 2;
                position += rotation * (N(0) * noise.move * amount);
                rotation *= Quaternion.Euler(N(40) * noise.turn * amount);
            }

            // Impacts: rotation only, strongest at the hit, gone after 'decay' seconds (trauma squared).
            if (camera.shakes != null)
                foreach (var shake in camera.shakes)
                {
                    float since = cameraTime - shake.time;
                    if (since < 0 || since > shake.decay || shake.decay <= 0) continue;
                    float trauma = 1 - since / shake.decay, amount = shake.degrees * trauma * trauma, wobble = since * shake.rate;
                    float W(float seed) => (Mathf.PerlinNoise(wobble, seed) - .5f) * 2;
                    rotation *= Quaternion.Euler(W(3.1f) * amount, W(17.3f) * amount, W(29.7f) * amount * .4f);
                }

            if (camera.focusOnSubject && aimedByList) focus = Vector3.Distance(position, aim);
            else if (camera.focusOnSubject && subjectNow.found && camera.behavior != "firstPerson") focus = Vector3.Distance(position, subjectNow.feet + camera.aimOffset);
            // A player's own eye has no depth of field (f/16 switches it off in renders).
            float aperture = ownBody.Length > 0 ? Mathf.Max(16, key.aperture) : Mathf.Max(.7f, key.aperture);
            return new Shot { position = position, rotation = rotation, focal = focal, focus = Mathf.Max(.2f, focus), aperture = aperture,
                fieldOfView = FieldOfView(focal, camera.sensorHeight), ownBody = ownBody };
        }

        // Where the aim list points at a time: the newest aim key, eased in from the one before it.
        static bool AimPoint(StudioStage stage, StudioCameraTrack camera, float tapeTime, float cameraTime, out Vector3 point)
        {
            point = default;
            var aims = camera.aims;
            int k = 0;
            for (int i = 0; i < aims.Count; i++) if (aims[i].time <= cameraTime) k = i;
            bool Resolve(StudioAimKey key, out Vector3 p)
            {
                if (!StudioTargets.TryPoint(stage, key.target, tapeTime, out p)) return false;
                if (camera.leadRoom > 0 && StudioTargets.Timeless(key.target) && StudioTargets.TryPoint(stage, key.target, tapeTime + camera.leadRoom, out var ahead)) p = ahead;
                p += key.offset;
                return true;
            }
            bool now = Resolve(aims[k], out var current);
            if (k == 0 || cameraTime >= aims[k].time + aims[k].blend)
            {
                if (now) { point = current; return true; }
                return k > 0 && Resolve(aims[k - 1], out point);
            }
            bool before = Resolve(aims[k - 1], out var previous);
            if (!now) { point = previous; return before; }
            if (!before) { point = current; return true; }
            float u = Ease(5, Mathf.InverseLerp(aims[k].time, aims[k].time + aims[k].blend, cameraTime));
            point = Vector3.Lerp(previous, current, u);
            return true;
        }

        public static void Apply(Camera target, Shot shot)
        {
            target.transform.SetPositionAndRotation(shot.position, shot.rotation);
            target.fieldOfView = shot.fieldOfView;
            // First person: this camera (only) sees the actor the way its player did; any other shot gives the actor back.
            StudioFirstPerson.Show(target, shot.ownBody);
        }

        // A key that matches the director's current view, at a tape time.
        public static StudioCameraKey KeyFromView(StudioStage stage, StudioCameraTrack camera, float time)
        {
            var view = StudioCamera.Camera.transform;
            return new StudioCameraKey
            {
                time = time, position = camera.behavior == "ride" || camera.behavior == "orbit" ? Relative(stage, camera, time, view.position) : stage.ToStage(view.position),
                euler = view.rotation.eulerAngles, focal = Mathf.Round(FocalFor(StudioCamera.Camera.fieldOfView, camera.sensorHeight)),
                focus = Mathf.Max(.5f, Vector3.Distance(view.position, StudioCamera.Target))
            };
        }

        static Vector3 Relative(StudioStage stage, StudioCameraTrack camera, float time, Vector3 world)
        {
            var subject = StudioActorPoses.At(stage.tape, camera.subject, time, stage.Fps);
            return subject.found ? world - subject.feet : stage.ToStage(world);
        }

        public static void AddKey(StudioCameraTrack camera, StudioCameraKey key)
        {
            camera.keys.RemoveAll(k => Mathf.Abs(k.time - key.time) < .001f);
            camera.keys.Add(key);
            camera.keys.Sort((a, b) => a.time.CompareTo(b.time));
        }

        // ---- Time remap ----

        // The playback speed at a tape time, eased between the shot's speed marks.
        public static float SpeedAt(StudioCameraTrack camera, float tapeTime)
        {
            var marks = camera.speeds;
            if (marks.Count == 0) return 1;
            if (tapeTime <= marks[0].time) return Mathf.Max(.01f, marks[0].speed);
            for (int i = 1; i < marks.Count; i++)
                if (tapeTime < marks[i].time)
                {
                    float u = Mathf.InverseLerp(marks[i - 1].time, marks[i].time, tapeTime);
                    u = u * u * (3 - 2 * u);
                    return Mathf.Max(.01f, Mathf.Lerp(marks[i - 1].speed, marks[i].speed, u));
                }
            return Mathf.Max(.01f, marks[marks.Count - 1].speed);
        }

        // For each output frame of a shot: the tape time shown and the camera's own time.
        // With speed ramps the tape advances by speed/fps per frame; a freeze holds the tape while the camera keeps going.
        public static List<(float tape, float camera)> Timeline(StudioCameraTrack camera, StudioSet set, int fps, int subSteps = 1)
        {
            var times = new List<(float, float)>();
            float end = camera.End(set), tape = Mathf.Clamp(camera.start, 0, end), ahead = 0, frozen = 0;
            float step = 1f / (fps * Mathf.Max(1, subSteps));
            int guard = 0;
            bool freezes = camera.freezeAt >= camera.start && camera.freezeFor > 0;
            while (tape < end - .0001f && guard++ < 400000)
            {
                times.Add((tape, tape + ahead));
                if (freezes && tape >= camera.freezeAt && frozen < camera.freezeFor) { frozen += step; ahead += step; continue; }
                tape += step * SpeedAt(camera, tape);
            }
            return times;
        }

        public static float OutputLength(StudioCameraTrack camera, StudioSet set, int fps) => Timeline(camera, set, fps).Count / (float)fps;
    }
}
