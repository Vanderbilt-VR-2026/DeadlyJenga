using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Phase 4 acceptance tests: polishing performances.
    public static partial class StudioAcceptance
    {
        public static void CheckCrowdAlgorithms()
        {
            var set = new StudioSet { length = 2 };
            var tape = new StudioTape();
            var source = set.AddMark(StudioRole.Survivor, Vector3.zero);
            var world = tape.GetOrAdd("world", WorldTrack.Id, "World").AddTake(0, 1);
            var sourceTake = tape.GetOrAdd(source.id, SurvivorTrack.Id, source.actor).AddTake(0, 1);
            sourceTake.meta = StudioTakes.WorldMeta(world.id);
            for (int frame = 0; frame < set.Frames; frame++)
            {
                var feet = Vector3.right * (frame * .06f);
                sourceTake.frames.Add(new ActorFrame
                {
                    visible = true,
                    body = new DeadlyJenga.CandyPlayers.CandyAvatarFrame
                    {
                        feet = feet, head = feet + Vector3.up * 1.6f, headRotation = Quaternion.identity, scale = 1,
                        left = new DeadlyJenga.CandyPlayers.CandyHandPose { rotation = Quaternion.identity },
                        right = new DeadlyJenga.CandyPlayers.CandyHandPose { rotation = Quaternion.identity }
                    }
                }.Write());
            }
            var options = new StudioTakes.CrowdOptions
            {
                directed = true, group = "East stairs", start = Vector3.forward * 10, destination = Vector3.forward * 15,
                count = 2, radius = 2, timeSpread = .15f, seed = 18
            };
            var sources = new List<StudioMark> { source };
            int groundQueries = 0;
            options.ground = _ => ++groundQueries == 1 ? 0f : (float?)null;
            var rejected = StudioTakes.Crowd(set, tape, sources, options, out var notes);
            Require(rejected.Count == 0 && set.marks.Count == 1 && tape.tracks.Count == 2 && notes.Count > 0,
                "A directed group with only one valid placement changed the cast or tape.");

            options.ground = _ => 0;
            var group = StudioTakes.Crowd(set, tape, sources, options, out _);
            Require(group.Count == 2 && group.All(mark => mark.crowdGroup == options.group), "A valid directed group was not created.");
            group[0].heroLocked = true;
            var hero = tape.Find(group[0].id).ActiveTake;
            var extraTrack = tape.Find(group[1].id);
            var extra = extraTrack.ActiveTake;
            sourceTake.meta = StudioTakes.WorldMeta("another-world");
            bool refused = false;
            try { StudioTakes.RetakeCrowd(set, tape, options.group, .1f, 24); }
            catch (InvalidOperationException) { refused = true; }
            Require(refused && extraTrack.ActiveTake == extra && extraTrack.takes.Count == 1,
                "A retake from another world partially changed the group.");
            sourceTake.meta = StudioTakes.WorldMeta(world.id);
            Require(StudioTakes.RetakeCrowd(set, tape, options.group, .1f, 24) == 1 && extraTrack.ActiveTake != extra &&
                tape.Find(group[0].id).ActiveTake == hero, "The approved hero changed or the background retake was skipped.");
            Require(StudioTakes.RestoreCrowdTakes(set, tape, options.group) == 1 && extraTrack.ActiveTake == extra,
                "Restoring the group's previous take failed.");
            Debug.Log("Trailer Studio crowd algorithms passed: directed placement is atomic, exact-world retakes exclude the hero, and restore is reversible.");

            void Require(bool passed, string message)
            {
                if (!passed) throw new InvalidOperationException(message);
            }
        }

        // How fast the lower foot of a played-back survivor moves along the ground, per frame (metres per second).
        sealed class FootWatch
        {
            public float slide, bodySpeed; public int samples; public string found = "";
            Transform[] feet; Vector3[] last; Vector3 lastRoot; bool started;

            public void Sample(GameObject actor, int fps)
            {
                if (!actor || !actor.activeSelf) return;
                if (feet == null)
                {
                    feet = actor.GetComponentsInChildren<Transform>(true).Where(t => t.name.ToLowerInvariant().Contains("foot")).Take(2).ToArray();
                    found = feet.Length == 2 ? feet[0].name + ", " + feet[1].name : "";
                    last = new Vector3[feet.Length];
                }
                if (feet.Length < 2) return;
                // The planted foot is the lower one.
                int low = feet[0].position.y <= feet[1].position.y ? 0 : 1;
                if (started)
                {
                    slide += Vector3.ProjectOnPlane(feet[low].position - last[low], Vector3.up).magnitude * fps;
                    bodySpeed += Vector3.ProjectOnPlane(actor.transform.position - lastRoot, Vector3.up).magnitude * fps;
                    samples++;
                }
                for (int i = 0; i < feet.Length; i++) last[i] = feet[i].position;
                lastRoot = actor.transform.position; started = true;
            }

            public float Slide => samples == 0 ? -1 : slide / samples;
            public float Body => samples == 0 ? 0 : bodySpeed / samples;
        }

        static IEnumerator<int> Phase4Tests()
        {
            var stage = StudioRun.Stage;
            var set = stage.set;
            var tape = stage.tape;
            int fps = set.fps;
            set.cues.Clear();
            var cy = set.AddMark(StudioRole.Survivor, new Vector3(0, 0, 1.6f)); cy.actor = "Cy";
            foreach (var mark in set.marks)
                if (StudioStage.SurfaceBelow(stage.ToWorld(mark.position), out var surface)) mark.position = stage.ToStage(surface);
            StudioLibrary.Save(set);
            StudioRun.Refresh();
            int frames = set.Frames;
            StudioCamera.Resize(640, 360);
            StudioCamera.Target = stage.anchor + Vector3.up; StudioCamera.Distance = 13; StudioCamera.Pitch = 22; StudioCamera.Place();
            var ada = set.marks[0]; var ben = set.marks[1];

            var motor = stage.session.Survivor();
            var performer = new ScriptedPerformer();
            motor.externallyDriven = true; motor.InputDriver = performer; motor.RefreshMode();

            // The world, with a slow shove on the stage block at 1.0 s for the contact test.
            int hero = Enumerable.Range(0, stage.blocks.Length).Where(i => stage.blocks[i] && stage.blocks[i].gameObject.activeSelf)
                .OrderByDescending(i => stage.blocks[i].position.y).Skip(3).First();
            set.cues.Add(new StudioCue { at = 1, action = PushCue.Id, blocks = new List<int> { hero }, direction = new Vector3(1, .3f, 0), strength = 6 });
            StudioLibrary.Save(set);
            StudioRun.RecordWholeSet();
            foreach (int wait in UntilHeld()) yield return wait;
            var worldTake = tape.FindType(WorldTrack.Id).ActiveTake;

            // Three short performances through the local player: three different walks.
            Vector3[] ways = { new Vector3(1, 0, .2f), new Vector3(-.3f, 0, 1), new Vector3(-1, 0, -.4f) };
            var performers = new[] { ada, ben, cy };
            for (int i = 0; i < 3; i++)
            {
                Vector3 way = ways[i].normalized; int who = i;
                performer.Reset();
                performer.script = t => new CastleBotCommand { move = t > .2f + who * .2f && t < 2.2f + who * .3f ? way * .35f : Vector3.zero, look = way, jump = who == 1 && t >= 1.2f && t < 1.27f };
                StudioRun.Record(PerformPass(stage, performers[i]));
                foreach (int wait in UntilHeld()) yield return wait;
            }
            StudioRun.LeaveBody();
            yield return 0;
            var adaTrack = tape.Find(ada.id);
            var original = adaTrack.ActiveTake;
            var originalPath = Positions(adaTrack, original);
            Result("Setup: three performed takes", performers.All(p => tape.Find(p.id)?.ActiveTake?.frames.Count == frames),
                $"Ada, Ben and Cy each performed a {set.length:0} s take. Ada walked {Vector3.Distance(originalPath[0], originalPath[originalPath.Count - 1]):0.00} m.");

            bool previousOptIn = StudioPerform.StarredGhostEnabled(set, ada);
            bool previousHidden = StudioPerform.GhostsHidden;
            bool previousStar = original.starred;
            bool previousLive = StudioPerform.LivePhysics;
            string previousActor = StudioPerform.Actor;
            bool selectedReference = false, visibleReference = false, madePassThrough = false, disappeared = false;
            try
            {
                original.starred = true;
                StudioPerform.GhostsHidden = false;
                StudioPerform.Actor = ada.id;
                StudioPerform.LivePhysics = false;
                StudioPerform.SetStarredGhost(set, ada, false);
                bool offByChoice = string.IsNullOrEmpty(StudioPerform.BuildPass(stage, 0).starredGhost);
                StudioPerform.SetStarredGhost(set, ada, true);
                var actorType = (ActorTrack)StudioTrackTypes.Get(SurvivorTrack.Id);
                var reference = StudioPerform.StarredGhostFor(tape, ada, false, 0, frames - 1, null, out _);
                selectedReference = offByChoice && StudioPerform.BuildPass(stage, 0).starredGhost == original.id && reference == original &&
                    StudioPerform.StarredGhostFor(tape, ben, false, 0, frames - 1, original.id, out _) == null &&
                    StudioPerform.StarredGhostFor(tape, ada, true, 0, frames - 1, original.id, out _) == null;
                if (reference != null)
                {
                    stage.playback.TryGetValue(ada.id, out var normal);
                    using (var ghost = actorType.CreateStarredGhost(stage, adaTrack, reference, worldTake))
                    {
                        ghost.Show(fps, true);
                        var root = stage.starredGhost;
                        visibleReference = root && root.activeInHierarchy && root.GetComponentsInChildren<Renderer>().Length > 0;
                        madePassThrough = root && root.GetComponentsInChildren<Collider>().All(c => !c.enabled) &&
                            stage.playback.TryGetValue(ada.id, out var currentPlayback) == (normal != null) && currentPlayback == normal;
                        StudioPerform.GhostsHidden = true;
                        ghost.Show(fps, true);
                        disappeared = root && !root.activeSelf;
                    }
                }
            }
            finally
            {
                original.starred = previousStar;
                StudioPerform.SetStarredGhost(set, ada, previousOptIn);
                StudioPerform.GhostsHidden = previousHidden;
                StudioPerform.Actor = previousActor;
                StudioPerform.LivePhysics = previousLive;
            }
            Result("Starred reference ghost uses this actor's same-world take and stays isolated",
                selectedReference && visibleReference && madePassThrough && disappeared && !stage.starredGhost,
                $"Eligible {selectedReference}, drew a reference {visibleReference}, isolated playback and collisions {madePassThrough}, hid on request {disappeared}.");

            // ---- Test 4 first (it needs the three untouched takes): a 20-person crowd from 3 takes ----
            const string crowdName = "Courtyard evacuees";
            var crowd = StudioTakes.Crowd(set, tape, performers.ToList(), new StudioTakes.CrowdOptions { group = crowdName, count = 20, radius = 5, spacing = .9f, timeSpread = .8f, seed = 11, ground = stage.GroundAt }, out var crowdNotes);
            int floating = crowd.Count(m => !stage.GroundAt(m.position + Vector3.up * .1f).HasValue);
            StudioLibrary.Save(set); StudioRun.SaveTape(); StudioRun.Refresh();
            float closest = float.MaxValue;
            for (int a = 0; a < set.marks.Count; a++)
                for (int b = a + 1; b < set.marks.Count; b++) closest = Mathf.Min(closest, Vector3.Distance(set.marks[a].position, set.marks[b].position));
            // "In sync" = two people whose speed, frame by frame, is the same for the whole take.
            List<float> Speeds(StudioMark mark)
            {
                var track = tape.Find(mark.id); var path = Positions(track, track.ActiveTake);
                var speeds = new float[frames];
                for (int f = 1; f < path.Count && track.ActiveTake.start + f < frames; f++) speeds[track.ActiveTake.start + f] = Vector3.Distance(path[f], path[f - 1]) * fps;
                return speeds.ToList();
            }
            var everyone = performers.Concat(crowd).ToList();
            var profiles = everyone.Select(Speeds).ToList();
            int inStep = 0; float nearest = float.MaxValue;
            for (int a = 0; a < everyone.Count; a++)
                for (int b = a + 1; b < everyone.Count; b++)
                {
                    float difference = 0;
                    for (int f = 0; f < frames; f++) difference += Mathf.Abs(profiles[a][f] - profiles[b][f]);
                    difference /= frames;
                    nearest = Mathf.Min(nearest, difference);
                    if (difference < .03f) inStep++;
                }
            int outfits = crowd.Select(m => JsonUtility.ToJson(m.appearance)).Distinct().Count();
            int shownMost = 0;
            void CountCrowd(int frame)
            {
                shownMost = Mathf.Max(shownMost, stage.playback.Values.Count(o => o && o.activeSelf));
                if (frame % 6 == 0) StudioCamera.SavePng(Path.Combine(Folder, "crowd", $"{frame / 6:D05}.png"));
            }
            StudioRun.FrameShown += CountCrowd;
            StudioRun.Play(0, -1, false);
            foreach (int wait in UntilHeld()) yield return wait;
            StudioRun.FrameShown -= CountCrowd;
            Result("4. A 20-person crowd from 3 performed takes, with no two people in step",
                crowd.Count == 20 && closest >= .88f && inStep == 0 && shownMost >= 20 && floating == 0,
                $"{crowd.Count} people made from Ada's, Ben's and Cy's takes ({outfits} different outfits). Closest two start {closest:0.00} m apart (asked for 0.90). " +
                $"Comparing every pair's speed frame by frame: {inStep} pairs move in step; the most alike pair still differs by {nearest:0.00} m/s on average. " +
                $"{shownMost} actors on screen at once in playback. People placed with nothing under their feet: {floating}. Notes: {(crowdNotes.Count == 0 ? "none" : string.Join(" ", crowdNotes))} Pictures: crowd/.");
            if (crowd.Count >= 2)
            {
                var protectedMember = crowd[0];
                protectedMember.heroLocked = true;
                var protectedTake = tape.Find(protectedMember.id).ActiveTake;
                var previous = tape.Find(crowd[1].id).ActiveTake;
                var roster = JsonUtility.FromJson<StudioSet>(JsonUtility.ToJson(set));
                int retaken = StudioTakes.RetakeCrowd(set, tape, crowdName, .15f, 123);
                var fresh = tape.Find(crowd[1].id).ActiveTake;
                int restored = StudioTakes.RestoreCrowdTakes(set, tape, crowdName);
                Result("Named group retakes keep the hero and world take, then restore prior performances",
                    roster.marks.Count(m => m.crowdGroup == crowdName) == crowd.Count && retaken == crowd.Count - 1 &&
                    restored == retaken && fresh != previous && StudioTakes.WorldOf(fresh) == worldTake.id &&
                    tape.Find(protectedMember.id).ActiveTake == protectedTake && tape.Find(crowd[1].id).ActiveTake == previous,
                    $"{retaken} retakes against {worldTake.id}, {restored} prior takes restored.");
                var reactions = StudioReactionLibrary.Make(set, tape, crowd[1], new StudioReactionLibrary.Preset
                {
                    reaction = "stagger", duration = .8f, spread = .3f, strength = .5f, mirror = true, preserveGrip = true
                }, fps, true, 33);
                var layer = reactions.FirstOrDefault();
                bool gripKept = false;
                if (layer != null)
                {
                    var source = tape.Find(layer.actor);
                    var active = source.ActiveTake;
                    int frame = Mathf.RoundToInt((layer.from + .3f) * fps);
                    var reactionPose = ActorFrame.Read(source.type, active.frames[frame - active.start]);
                    reactionPose.equipment = JsonUtility.ToJson(new EquipmentState { kind = EquipmentKind.Blaster, drawn = true });
                    var originalFeet = reactionPose.body.feet;
                    var originalGrip = reactionPose.body.right.position - originalFeet;
                    StudioOverrides.Apply(set, tape, stage.anchor, layer, frame / (float)fps, 1, reactionPose);
                    gripKept = (reactionPose.body.feet - originalFeet).sqrMagnitude > .00001f &&
                        (reactionPose.body.right.position - reactionPose.body.feet - originalGrip).sqrMagnitude < .00001f;
                }
                var wardrobe = StudioWardrobeSheet.Appearance(crowd[1], tape);
                Result("Reaction preset staggers group timing and keeps the drawn grip, hero and wardrobe",
                    reactions.Count == crowd.Count - 1 && reactions.All(r => r.actor != protectedMember.id && r.mirrorReaction && r.strength == .5f) &&
                    reactions.Select(r => r.from).Distinct().Count() > 1 && gripKept && wardrobe.look.Equals(crowd[1].appearance),
                    $"{reactions.Count} layers, {reactions.Select(r => r.from).Distinct().Count()} start frames and compatible costume resolution.");
            }
            // Take the crowd away again so the next tests are easy to see.
            set.marks.RemoveAll(m => m.crowdGroup == crowdName);
            foreach (var mark in crowd) { var t = tape.Find(mark.id); if (t != null) t.mode = TrackMode.Muted; }
            StudioLibrary.Save(set); StudioRun.Refresh();

            // ---- Test 1: retime one second of a walk to half speed; feet do not slide ----
            int from = Mathf.RoundToInt(.8f * fps), to = from + fps;
            var before = new FootWatch(); var after = new FootWatch();

            // ---- Legs when frames are shown one at a time (scrubbing and renders) ----
            // Where the avatar's two feet are, measured from its own body, on the frame now shown.
            Vector3[] FootPose()
            {
                if (!stage.playback.TryGetValue(ada.id, out var shown) || !shown) return null;
                var feet = shown.GetComponentsInChildren<Transform>(true).Where(x => x.name.ToLowerInvariant().Contains("foot")).Take(2).ToArray();
                return feet.Length < 2 ? null : feet.Select(x => shown.transform.InverseTransformPoint(x.position)).ToArray();
            }
            float FeetApart(Vector3[] a, Vector3[] b) => a == null || b == null ? -1 : Mathf.Max(Vector3.Distance(a[0], b[0]), Vector3.Distance(a[1], b[1]));
            StudioRun.ShowExact(from + 5); yield return 0;
            var earlyFeet = FootPose();
            StudioRun.ShowExact(from + 12); yield return 0;
            var laterFeet = FootPose();
            // The same frame reached by playing up to it, and by jumping to it from far away.
            StudioRun.Play(from - 5, from + 12, false);
            foreach (int wait in UntilHeld()) yield return wait;
            var playedFeet = FootPose();
            StudioRun.ShowExact(from + 60); yield return 0;
            StudioRun.ShowExact(from + 12); yield return 0;
            var jumpedFeet = FootPose();
            float legsMoved = FeetApart(earlyFeet, laterFeet), playedGap = FeetApart(laterFeet, playedFeet), jumpedGap = FeetApart(laterFeet, jumpedFeet);
            Result("Legs move when frames are shown one at a time, and a frame looks the same however it is reached",
                legsMoved > .02f && playedGap >= 0 && playedGap < .03f && jumpedGap >= 0 && jumpedGap < .03f,
                legsMoved < 0 ? "The avatar's feet could not be found, so this was not measured."
                    : $"Ada walking, shown frame by frame the way a render does: between tape frames {from + 5} and {from + 12} her feet moved {legsMoved * 100:0.0} cm relative to her body. " +
                      $"Frame {from + 12} reached by playing up to it differs by {playedGap * 100:0.0} cm; reached by jumping back from frame {from + 60}, by {jumpedGap * 100:0.0} cm. " +
                      "(Body turning and landing bends still follow game time, so small differences are expected.)");
            void WatchBefore(int frame) { if (frame >= from && frame < to) before.Sample(stage.playback.TryGetValue(ada.id, out var o) ? o : null, fps); }
            StudioRun.FrameShown += WatchBefore;
            StudioRun.Play(from - 5, to + 2, false);
            foreach (int wait in UntilHeld()) yield return wait;
            StudioRun.FrameShown -= WatchBefore;
            var slowed = StudioMotionEdit.Retime(adaTrack, from, to, .5f, frames);
            StudioRun.SaveTape(); StudioRun.Refresh();
            var slowedPath = Positions(adaTrack, slowed);
            float matchError = 0;
            for (int k = 0; k < fps && from + 2 * k < slowedPath.Count; k++) matchError = Mathf.Max(matchError, Vector3.Distance(slowedPath[from + 2 * k], originalPath[from + k]));
            float oldStep = Vector3.Distance(originalPath[to - 1], originalPath[from]) / (to - 1 - from) * fps;
            float newStep = Vector3.Distance(slowedPath[from + 2 * fps - 2], slowedPath[from]) / (2 * fps - 2) * fps;
            void WatchAfter(int frame) { if (frame >= from && frame < from + 2 * fps) after.Sample(stage.playback.TryGetValue(ada.id, out var o) ? o : null, fps); }
            StudioCamera.Target = slowedPath[from + fps] + Vector3.up; StudioCamera.Distance = 6; StudioCamera.Place();
            void ShootRetime(int frame) { if (frame % 3 == 0) StudioCamera.SavePng(Path.Combine(Folder, "retime", $"{frame / 3:D05}.png")); }
            StudioRun.FrameShown += WatchAfter; StudioRun.FrameShown += ShootRetime;
            StudioRun.Play(from - 5, from + 2 * fps + 2, false);
            foreach (int wait in UntilHeld()) yield return wait;
            StudioRun.FrameShown -= WatchAfter; StudioRun.FrameShown -= ShootRetime;
            // Lock feet: after the edit the feet sit on the surface under them.
            var locked = StudioMotionEdit.LockFeet(stage, adaTrack, from, from + 2 * fps);
            StudioRun.SaveTape(); StudioRun.Refresh();
            float offGround = 0; int groundChecks = 0;
            for (int f = from; f < from + 2 * fps && f < locked.End; f += 6)
            {
                StudioRun.Scrub(f); yield return 0;
                Vector3 feet = ActorFrame.Read(adaTrack.type, locked.frames[f - locked.start]).body.feet;
                if (!Physics.Raycast(feet + Vector3.up * .7f, Vector3.down, out var hit, 1.4f, (1 << Layers.Block) | (1 << Layers.Surface) | 1, QueryTriggerInteraction.Ignore)) continue;
                offGround = Mathf.Max(offGround, Mathf.Abs(hit.point.y - feet.y)); groundChecks++;
            }
            bool feetMeasured = before.Slide >= 0 && after.Slide >= 0;
            Result("1. Retime 1 s of a walk to half speed; feet do not slide",
                slowed.frames.Count == Mathf.Min(frames, original.frames.Count + fps) && matchError < .01f && Mathf.Abs(newStep - oldStep * .5f) < oldStep * .12f + .02f &&
                feetMeasured && after.Slide <= before.Slide * .75f && groundChecks > 0 && offGround < .03f && adaTrack.takes.Contains(original),
                $"Frames {from} to {to} of Ada's take now last {2 * fps} frames ({slowed.frames.Count} frames in the new take; the old take is still on the track). " +
                $"Every slowed pose matches the original performance (largest difference {matchError * 1000:0.0} mm); body speed went from {oldStep:0.00} to {newStep:0.00} m/s. " +
                (feetMeasured ? $"Planted-foot slide along the ground, measured on the avatar's own foot bones ({before.found}) during playback: {before.Slide:0.00} m/s before the edit (body {before.Body:0.00} m/s), {after.Slide:0.00} m/s after it (body {after.Body:0.00} m/s). "
                    : "The avatar has no transforms named \"foot\", so foot slide could not be measured on the bones; the legs are animated by the game from the body's speed, which halved. ") +
                $"After \"Lock feet\", the feet are within {offGround * 1000:0.0} mm of the surface on {groundChecks} checked frames. Pictures: retime/.");

            // ---- Test 2: a recorded block hits an actor and triggers a knockback that blends back ----
            // Put Ben where the pushed block will be half a second after the push, standing still.
            int pushFrame = fps, meet = pushFrame + fps / 2;
            WorldTrack.Positions(worldTake, meet, out var atMeet, out var meetOn);
            WorldTrack.Positions(worldTake, meet - 1, out var beforeMeet, out _);
            var benTrack = tape.Find(ben.id);
            var benTake = benTrack.ActiveTake;
            var standing = ActorFrame.Read(benTrack.type, benTake.frames[0]);
            Vector3 spot = atMeet[hero] + Vector3.down * .9f;   // the block passes through chest height
            var still = new StudioTake { start = 0, keyEvery = 1, meta = benTake.meta, name = "Standing in the block's path" };
            standing.Transform(standing.body.feet, Quaternion.identity, spot - standing.body.feet, false, Vector3.right);
            byte[] pose = standing.Write();
            for (int f = 0; f < frames; f++) still.frames.Add(pose);
            benTrack.takes.Add(still); benTrack.active = benTrack.takes.Count - 1;
            var knock = new StudioOverride { actor = ben.id, kind = "reaction", name = "knockback", trigger = "contact", from = 0, to = 1.6f, blendOut = .5f, strength = 1 };
            set.overrides.Add(knock);
            var resolveNotes = StudioOverrides.Resolve(set, tape, stage.anchor);
            StudioLibrary.Save(set); StudioRun.SaveTape(); StudioRun.Refresh();
            Vector3 blockVelocity = (atMeet[hero] - beforeMeet[hero]) * fps;
            float found = knock.resolvedFrom;
            Vector3 Shown(float time) { StudioRun.Scrub(Mathf.RoundToInt(time * fps)); return stage.playback.TryGetValue(ben.id, out var o) && o ? o.transform.position : Vector3.zero; }
            Vector3 atHit = Shown(Mathf.Max(0, found - .05f)); yield return 0;
            Vector3 flying = Shown(found + .5f); yield return 0;
            Vector3 settled = Shown(found + 1.6f + .05f); yield return 0;
            for (int f = Mathf.RoundToInt(Mathf.Max(0, found - .2f) * fps); f < Mathf.RoundToInt((found + 1.7f) * fps) && f < frames; f += 3)
            {
                StudioRun.Scrub(f); yield return 0;
                StudioCamera.Target = spot + Vector3.up; StudioCamera.Distance = 7; StudioCamera.Place();
                StudioCamera.SavePng(Path.Combine(Folder, "knockback", $"{f:D05}.png"));
            }
            float thrown = Vector3.Distance(flying, atHit), along = Vector3.Dot((flying - atHit).normalized, Vector3.ProjectOnPlane(blockVelocity, Vector3.up).normalized);
            float returned = Vector3.Distance(settled, spot);
            Result("2. A recorded block hits an actor and triggers a knockback that blends back into the take",
                found >= 0 && Mathf.Abs(found - meet / (float)fps) <= .5f && thrown > .5f && along > .7f && returned < .03f && Vector3.Distance(atHit, spot) < .03f,
                $"Block #{hero} was pushed at 1.0 s; Ben stands in its path. {string.Join(" ", resolveNotes)} (The block is at Ben's spot at {meet / (float)fps:0.00} s.) " +
                $"Just before the hit Ben is on his take ({Vector3.Distance(atHit, spot) * 1000:0.0} mm off). Half a second after it he has been thrown {thrown:0.00} m, " +
                $"in the direction the block was moving (alignment {along:0.00}, 1 = exactly). At the end of the reaction he is back on his take ({returned * 1000:0.0} mm off). " +
                "The take itself was not changed. Pictures: knockback/.");

            // ---- Cameras and look-ats must see Ben where the knockback puts him, not where his take has him ----
            float midKnock = found + .5f;
            var seen = StudioActorPoses.At(tape, ben.id, midKnock, fps);
            StudioRun.ShowExact(midKnock * fps); yield return 0;
            Vector3 drawn = stage.playback.TryGetValue(ben.id, out var benCopy) && benCopy ? benCopy.transform.position : Vector3.zero;
            Result("Cameras see an actor where its overrides put it", seen.found && Vector3.Distance(seen.feet, spot) > .5f && Vector3.Distance(seen.feet, drawn) < .01f,
                $"In the middle of the knockback, the pose cameras aim at is {Vector3.Distance(seen.feet, spot):0.00} m from Ben's recorded spot and {Vector3.Distance(seen.feet, drawn) * 100:0.0} cm from where he is drawn.");

            // ---- A hit pause holds the picture in a render too ----
            int pauseFrom = pushFrame + 5;
            var pause = new StudioOverride { kind = "hitPause", from = pauseFrom / (float)fps, to = (pauseFrom + 10) / (float)fps };
            set.overrides.Add(pause);
            StudioRun.Scrub(pauseFrom); yield return 0;
            Vector3 heldAt = stage.blocks[hero].transform.position;
            StudioRun.ShowExact(pauseFrom + 5.5f); yield return 0;
            float duringPause = Vector3.Distance(heldAt, stage.blocks[hero].transform.position);
            StudioRun.ShowExact(pauseFrom + 10.5f); yield return 0;
            float afterPause = Vector3.Distance(heldAt, stage.blocks[hero].transform.position);
            Result("A hit pause holds the picture when frames are shown the way a render shows them", duringPause < .0001f && afterPause > .01f,
                $"Pause from frame {pauseFrom} for 10 frames. Block #{hero} shown at frame {pauseFrom + 5.5f:0.0}: {duringPause * 1000:0.00} mm from where the pause began. At frame {pauseFrom + 10.5f:0.0}: {afterPause * 100:0.0} cm.");
            set.overrides.Clear();

            // ---- A fall holds until a get-up, and the get-up really gets up ----
            set.overrides.Add(new StudioOverride { actor = ben.id, kind = "reaction", name = "fall", from = 1, to = 2 });
            set.overrides.Add(new StudioOverride { actor = ben.id, kind = "reaction", name = "getUp", from = 3, to = 4 });
            var upright = ActorFrame.Read(benTrack.type, pose);
            float fullHeight = upright.body.head.y - upright.body.feet.y;
            ActorFrame Layered(float time)
            {
                var frame = ActorFrame.Read(benTrack.type, pose);
                foreach (var filter in ActorFrameFilters.For(stage, benTrack)) filter.Apply(stage, benTrack, time, frame);
                return frame;
            }
            var lying = Layered(2.5f); var risen = Layered(4.5f);
            float lyingHeight = lying.body.head.y - lying.body.feet.y, risenOff = Vector3.Distance(risen.body.head, upright.body.head);
            Result("A fall holds its pose until a get-up, and the get-up ends standing", lyingHeight < fullHeight * .4f && risenOff < .01f,
                $"Standing head height {fullHeight:0.00} m. Between the fall and the get-up (2.5 s) the head is {lyingHeight:0.00} m above the feet. After the get-up (4.5 s) the head is {risenOff * 1000:0.0} mm from the standing pose.");
            set.overrides.Clear();

            // ---- Lock feet leaves a hop alone and pulls a hover down ----
            var grounded = ActorFrame.Read(adaTrack.type, original.frames[0]);
            float groundY = grounded.body.feet.y;
            var hopTake = new StudioTake { start = 0, keyEvery = 1, meta = original.meta, name = "Hop then hover (test)" };
            for (int f = 0; f < 30; f++)
            {
                float lift = f >= 4 && f < 14 ? .3f * Mathf.Sin(Mathf.PI * (f - 4) / 10f) : f >= 14 && f < 28 ? .1f : 0;
                var frame = ActorFrame.Read(adaTrack.type, original.frames[0]);
                frame.Transform(frame.body.feet, Quaternion.identity, Vector3.up * lift, false, Vector3.right);
                hopTake.frames.Add(frame.Write());
            }
            int adaWas = adaTrack.active;
            adaTrack.takes.Add(hopTake); adaTrack.active = adaTrack.takes.Count - 1;
            var hopLocked = StudioMotionEdit.LockFeet(stage, adaTrack, 0, 30);
            float hopTop = 0, hoverOff = 0;
            for (int f = 4; f < 14; f++) hopTop = Mathf.Max(hopTop, ActorFrame.Read(adaTrack.type, hopLocked.frames[f]).body.feet.y - groundY);
            for (int f = 16; f < 28; f++) hoverOff = Mathf.Max(hoverOff, Mathf.Abs(ActorFrame.Read(adaTrack.type, hopLocked.frames[f]).body.feet.y - groundY));
            adaTrack.takes.Remove(hopLocked); adaTrack.takes.Remove(hopTake); adaTrack.active = Mathf.Min(adaWas, adaTrack.takes.Count - 1);
            StudioRun.Refresh();
            Result("Lock feet leaves a hop alone and pulls a hover down to the ground", hopTop > .25f && hoverOff < .01f,
                $"A made-up take: a 0.30 m hop, then 14 frames hovering 0.10 m up. After Lock feet the hop still reaches {hopTop:0.00} m, and the hovering frames are within {hoverOff * 1000:0.0} mm of the ground.");

            // ---- Test 3: five actors look at the incoming tornado ----
            string tornado = SetLists.Hazards.FirstOrDefault(h => h.ToLowerInvariant().Contains("tornado"));
            if (tornado == null) Result("3. A look-at override on 5 actors toward the incoming tornado", false, "The disaster catalog has no tornado.");
            else
            {
                var prefab = DemoDisasters.Prefabs.First(p => p && p.name == tornado);
                var parameters = DemoDisasters.Parameters(prefab);
                var cue = new StudioCue { at = .2f, action = "hazard", hazard = tornado, values = DirectCueFields.Defaults(tornado), aimed = true, heading = 90 };
                for (int i = 0; i < parameters.Count; i++)
                {
                    string name = (parameters[i].id + " " + parameters[i].label).ToLowerInvariant();
                    if (name.Contains("warning")) cue.values[i] = parameters[i].min;
                    if (name.Contains("duration")) cue.values[i] = parameters[i].Clamp(12);
                    if (parameters[i].id == "force") cue.values[i] = parameters[i].min;   // a gentle one: the actors' ground should stay put
                }
                set.cues.Clear(); set.cues.Add(cue);
                // Two more watchers: clones of Cy standing nearby.
                var watchers = new List<StudioMark> { ada, ben, cy };
                watchers.Add(StudioTakes.Clone(set, tape, cy, new StudioTakes.CloneOptions { offset = new Vector3(1.2f, 0, .4f), delay = .2f }));
                watchers.Add(StudioTakes.Clone(set, tape, cy, new StudioTakes.CloneOptions { offset = new Vector3(-1.2f, 0, .6f), delay = .35f, mirror = true }));
                StudioLibrary.Save(set); StudioRun.Refresh();
                StudioRun.Record(WorldPass(stage, "Tornado for the look-at"));
                foreach (int wait in UntilHeld()) yield return wait;
                foreach (var watcher in watchers)
                    set.overrides.Add(new StudioOverride { actor = watcher.id, kind = "lookAt", target = "hazard", from = 1, to = 5.5f, blendIn = .4f, blendOut = .4f });
                StudioLibrary.Save(set); StudioRun.SaveTape(); StudioRun.Refresh();
                var hazards = tape.FindType(HazardTrack.Id)?.ActiveTake;
                float checkTime = 3; int checkFrame = Mathf.RoundToInt(checkTime * fps);
                bool there = HazardTrack.CenterAt(hazards, checkFrame, out var funnel);
                int turned = 0, onTarget = 0; var angles = new List<string>();
                foreach (var watcher in watchers)
                {
                    var track = tape.Find(watcher.id); var take = track.ActiveTake;
                    if (take == null || !take.Covers(checkFrame)) { angles.Add(watcher.actor + ": no take"); continue; }
                    var plain = ActorFrame.Read(track.type, take.frames[checkFrame - take.start]);
                    var looking = ActorFrame.Read(track.type, take.frames[checkFrame - take.start]);
                    var layer = set.overrides.First(o => o.actor == watcher.id);
                    StudioOverrides.Apply(set, tape, stage.anchor, layer, checkTime, layer.Weight(checkTime), looking);
                    Vector3 toFunnel = Vector3.ProjectOnPlane(funnel - plain.body.head, Vector3.up);
                    float was = Vector3.Angle(Vector3.ProjectOnPlane(plain.body.headRotation * Vector3.forward, Vector3.up), toFunnel);
                    float now = Vector3.Angle(Vector3.ProjectOnPlane(looking.body.headRotation * Vector3.forward, Vector3.up), toFunnel);
                    if (now < was - 1 || was < 3) turned++;
                    if (now < 12 || was > 85 && Mathf.Abs(was - now - 85) < 3) onTarget++;
                    angles.Add($"{watcher.actor} {was:0}° -> {now:0}°");
                }
                StudioCamera.Target = stage.anchor + Vector3.up; StudioCamera.Distance = 9; StudioCamera.Yaw = -90; StudioCamera.Pitch = 12; StudioCamera.Place();
                foreach (float t in new[] { .5f, 2f, 3f, 4f })
                {
                    StudioRun.Scrub(Mathf.RoundToInt(t * fps)); yield return 0;
                    StudioCamera.SavePng(Path.Combine(Folder, "look-at", $"{Mathf.RoundToInt(t * 10):D03}.png"));
                }
                Result("3. A look-at override on 5 actors toward the incoming tornado", there && watchers.Count == 5 && turned == 5 && onTarget == 5,
                    $"Tornado \"{tornado}\" from heading 90, recorded on the hazard track. At {checkTime:0.0} s, angle between each head and the tornado, before -> with the layer: {string.Join(", ", angles)}. " +
                    $"{turned} of 5 turned toward it; {onTarget} of 5 look straight at it or as far as a neck turns (85°). The takes underneath are unchanged. Pictures: look-at/.");
            }

            Result("Runtime errors during the Studio tests", exceptions == 0, exceptions == 0 && errorLines.Count == 0 ? "None." : $"{exceptions} exceptions. First messages: {string.Join(" // ", errorLines)}");
            StudioLibrary.Save(set);
            StudioRun.SaveTape();
        }
    }
}
