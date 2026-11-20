using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DeadlyJenga.CandyPhysicsHands;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastleBots;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The directed hand: a "handPull" cue flies the giant hand along a path, closes its fingers on one block
    // and yanks it out on the cue's time. Everything is worked out from the cue and the block's pose, so the
    // same cues give the same take every time:
    //   - while recording, the block is moved along the pull (kinematic, so it shoves its neighbours);
    //   - after recording, the hand mark gets a take written frame by frame from the same path.
    public static class StudioHandDirection
    {
        public const float DefaultSize = 20;

        // ---- The path (pure functions of the cue and the block's pose at the grab) ----

        public struct Grab
        {
            public Vector3 blockPosition; public Quaternion blockRotation;
            public Vector3 pull;          // unit, horizontal
            public float halfLength, halfHeight;
            public Pose wrist;            // hand target pose at the moment of the yank
        }

        // Where the hand holds a block: palm down over the block's outer end, fingers curled over its top.
        public static Grab GripFor(Vector3 blockPosition, Quaternion blockRotation, Vector3 blockSize, Vector3 pull, float size)
        {
            pull = Vector3.ProjectOnPlane(pull, Vector3.up);
            pull = pull.sqrMagnitude < .0001f ? Vector3.forward : pull.normalized;
            // How far the block reaches along the pull: its extent along that direction.
            Vector3 local = Quaternion.Inverse(blockRotation) * pull;
            float halfLength = .5f * (Mathf.Abs(local.x) * blockSize.x + Mathf.Abs(local.y) * blockSize.y + Mathf.Abs(local.z) * blockSize.z);
            float halfHeight = .5f * blockSize.y;
            float s = size / DefaultSize;
            Vector3 end = blockPosition + pull * halfLength;
            Vector3 fingers = (Quaternion.AngleAxis(18, Vector3.Cross(Vector3.up, -pull)) * -pull).normalized;   // pointing back in, a little down
            var wrist = new Pose(end + pull * (1.4f * s) + Vector3.up * (halfHeight + 1.0f * s), Quaternion.LookRotation(fingers, Vector3.up));
            return new Grab { blockPosition = blockPosition, blockRotation = blockRotation, pull = pull, halfLength = halfLength, halfHeight = halfHeight, wrist = wrist };
        }

        static float Smooth(float u) { u = Mathf.Clamp01(u); return u * u * u * (u * (u * 6 - 15) + 10); }

        // The yank: slow lift-off, fast finish (u in 0..1, ends moving at distance/pullTime).
        static float Yank(float u) { u = Mathf.Clamp01(u); return u * u * (2 - u); }

        // Where the hand's wrist is after the yank started (t = seconds since the yank).
        static Pose Carry(StudioCue cue, Grab grab, float t)
        {
            float pullTime = Mathf.Max(.05f, cue.pullTime), distance = Mathf.Max(0, cue.strength);
            Vector3 offset; float tilt;
            if (t <= pullTime) { offset = grab.pull * distance * Yank(t / pullTime); tilt = 0; }
            else
            {
                float after = t - pullTime, speed = distance / pullTime, slow = .35f;
                float carryTime = Mathf.Max(.2f, cue.carryTime);
                offset = grab.pull * (distance + speed * slow * (1 - Mathf.Exp(-after / slow))) + cue.carry * Smooth(after / carryTime);
                tilt = 22 * Smooth(after / (carryTime * .8f));
            }
            var rotation = Quaternion.AngleAxis(-tilt, Vector3.Cross(Vector3.up, grab.pull)) * grab.wrist.rotation;
            return new Pose(grab.wrist.position + offset, rotation);
        }

        // The block follows the hand from the yank on, as if held.
        public static Pose BlockAt(StudioCue cue, Grab grab, float time)
        {
            if (time < cue.at) return new Pose(grab.blockPosition, grab.blockRotation);
            var hand = Carry(cue, grab, time - cue.at);
            Quaternion turn = hand.rotation * Quaternion.Inverse(grab.wrist.rotation);
            return new Pose(hand.position + turn * (grab.blockPosition - grab.wrist.position), turn * grab.blockRotation);
        }

        // The hand at any tape time. visible = false before its flight starts.
        public static bool HandAt(StudioStage stage, StudioCue cue, Grab grab, float time, out CandyHandPose pose)
        {
            pose = default;
            float grip = Mathf.Max(.05f, cue.grip), flight = Mathf.Max(.2f, cue.duration);
            float landed = cue.at - grip, start = landed - flight;
            if (time < start) return false;
            Vector3 position; Quaternion rotation; float curl;
            if (time < landed)
            {
                float u = (time - start) / flight;
                // Ease out: the hand slows down as it arrives (it starts out of frame).
                float w = 1 - Mathf.Pow(1 - Mathf.Clamp01(u), 3);
                Vector3 from = stage.ToWorld(cue.point), to = grab.wrist.position;
                Vector3 bend = (from + to) * .5f + Vector3.up * cue.height;
                position = (1 - w) * (1 - w) * from + 2 * (1 - w) * w * bend + w * w * to;
                Vector3 travel = Vector3.ProjectOnPlane(to - from, Vector3.up);
                var flying = Quaternion.LookRotation(travel.sqrMagnitude < .01f ? grab.wrist.rotation * Vector3.forward : travel.normalized, Vector3.up) * Quaternion.Euler(30, 0, 0);
                rotation = Quaternion.Slerp(flying, grab.wrist.rotation, Smooth(u * 1.1f));
                curl = .1f;
            }
            else if (time < cue.at)
            {
                // The held beat: the fingers close, the wrist settles a little.
                float u = (time - landed) / grip;
                position = grab.wrist.position + Vector3.down * (.12f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(u)));
                rotation = grab.wrist.rotation;
                curl = Mathf.Lerp(.1f, .8f, Smooth(u));
            }
            else
            {
                var held = Carry(cue, grab, time - cue.at);
                position = held.position; rotation = held.rotation; curl = .8f;
            }
            pose = new CandyHandPose { tracked = true, skeletonTracked = true, position = position, rotation = rotation, skeleton = CastleHandBotBrain.Articulate(false, curl) };
            return true;
        }

        // The 16 bones the hand copy is posed with, worked out from the target pose (same rule as the physical hand).
        public static Pose[] BonesFor(CandyHandPose target, float size)
        {
            var bones = new Pose[16];
            bones[0] = new Pose(target.position, target.rotation);
            for (int i = 1; i < 16; i++)
            {
                int f = (i - 1) / 3, j = (i - 1) % 3;
                Vector3 a = target.skeleton.Joint(f * 4 + j), b = target.skeleton.Joint(f * 4 + j + 1);
                bones[i] = new Pose(target.position + target.rotation * a * size,
                    CandyPhysicalHand.SegmentRotation(target.rotation * (b - a), target.rotation * Vector3.up));
            }
            return bones;
        }

        // A hand frame without bones (the hand bot writes none until its physical hand exists) can never be
        // drawn: the copy only switches its skin on when it has 16 bones. Fill them in from the target.
        public static void FillBones(ActorFrame frame)
        {
            if (frame == null || !frame.isHand || !frame.visible || frame.bones.Length == 16) return;
            frame.bones = BonesFor(frame.target, frame.size);
        }

        // The hand copy draws its skin from bones it moves in LateUpdate. A render draws right after the
        // pose is set (before LateUpdate), so it would show the previous frame's hand. Move them now.
        static MethodInfo lateUpdate;
        // 'frame' (optional) is the frame being shown: its bones are put on the hand directly. The Studio's copy is a
        // host copy, and a host copy is moved by physics, not by the recorded bones (HandNetworkView.SnapToState skips it).
        public static void SyncVisual(GameObject root, ActorFrame frame = null)
        {
            if (!root || !root.activeInHierarchy) return;
            lateUpdate ??= typeof(CandyPhysicalHand).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var hand in root.GetComponentsInChildren<CandyPhysicalHand>())
            {
                if (hand.bodies != null)
                    foreach (var body in hand.bodies)
                        if (body && body.interpolation != RigidbodyInterpolation.None) body.interpolation = RigidbodyInterpolation.None;
                if (frame != null && frame.visible && frame.bones != null && frame.bones.Length == 16 && hand.bodies != null && hand.bodies.Length == 16)
                    for (int i = 0; i < 16; i++)
                    {
                        if (!hand.bodies[i]) continue;
                        hand.bodies[i].transform.SetPositionAndRotation(frame.bones[i].position, frame.bones[i].rotation);
                        hand.bodies[i].position = frame.bones[i].position; hand.bodies[i].rotation = frame.bones[i].rotation;
                    }
                try { lateUpdate?.Invoke(hand, null); } catch (Exception) { }
                if (hand.skin) DrawBaked(root, hand.skin);
                ShowDesignParts(hand);
            }
        }

        // A hand design's extra shapes (gems, bands) hide whenever the skin is switched off, and a host copy
        // switches its skin off at the grab. The Studio draws the baked skin instead, so it shows the parts too.
        static FieldInfo designParts;
        static void ShowDesignParts(CandyPhysicalHand hand)
        {
            var look = hand.GetComponent<CandyHandLook>();
            if (!look) return;
            designParts ??= typeof(CandyHandLook).GetField("renderers", BindingFlags.Instance | BindingFlags.NonPublic);
            if (!(designParts?.GetValue(look) is List<Renderer> parts)) return;
            bool visible = hand.gameObject.activeInHierarchy;
            foreach (var part in parts) if (part) part.enabled = visible;
        }

        // The copy's skinned mesh is not drawn in Studio renders (the pilot measured 0 % of pixels changing when
        // it was hidden, even its shadow). So the Studio draws a plain mesh baked from the bones as they are now,
        // and switches the skinned one off. The baked copy sits under the playback root, so hiding the root hides it.
        public const string BakedName = "Studio baked hand";
        static MaterialPropertyBlock bakedBlock;
        static void DrawBaked(GameObject root, SkinnedMeshRenderer skin)
        {
            string name = BakedName + " " + skin.GetInstanceID();
            var baked = root.transform.Find(name);
            if (!baked)
            {
                baked = new GameObject(name).transform;
                baked.SetParent(root.transform, false);
                baked.gameObject.AddComponent<MeshFilter>().sharedMesh = new Mesh { name = BakedName, hideFlags = HideFlags.DontSave };
                baked.gameObject.AddComponent<MeshRenderer>();
            }
            var filter = baked.GetComponent<MeshFilter>();
            var shown = baked.GetComponent<MeshRenderer>();
            // Shown whenever the frame shows the hand (the copy is active). Not skin.enabled: a host copy switches its
            // skin off when it is switched on while touching a block, which is exactly the moment of a grab.
            bool visible = skin.gameObject.activeInHierarchy;
            shown.enabled = visible;
            skin.forceRenderingOff = true;
            if (!visible) return;
            skin.BakeMesh(filter.sharedMesh, true);
            filter.sharedMesh.RecalculateBounds();
            baked.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
            var parentScale = root.transform.lossyScale;
            baked.localScale = new Vector3(1 / Mathf.Max(.0001f, parentScale.x), 1 / Mathf.Max(.0001f, parentScale.y), 1 / Mathf.Max(.0001f, parentScale.z));
            shown.sharedMaterials = skin.sharedMaterials;
            shown.shadowCastingMode = skin.shadowCastingMode;
            shown.receiveShadows = skin.receiveShadows;
            baked.gameObject.layer = skin.gameObject.layer;
            bakedBlock ??= new MaterialPropertyBlock();
            skin.GetPropertyBlock(bakedBlock);
            shown.SetPropertyBlock(bakedBlock);
        }

        public static float HandSize(StudioStage stage)
        {
            var bridge = stage?.session && stage.session.Hand != null ? stage.session.Hand.gameObject.GetComponent<CandyFlyingHandBridge>() : null;
            return bridge && bridge.settings ? bridge.settings.size : DefaultSize;
        }

        // A block's size in its own axes (from its box collider, else its renderer).
        public static Vector3 BlockSize(Rigidbody block)
        {
            var box = block.GetComponentsInChildren<BoxCollider>().FirstOrDefault(c => c.enabled && !c.isTrigger);
            if (box) return Vector3.Scale(box.size, box.transform.lossyScale);
            var shown = block.GetComponentInChildren<Renderer>();
            return shown ? shown.localBounds.size : Vector3.one;
        }

        // ---- While recording ----

        // The grab from the cue's stored block pose when it has one, else from where the block is now.
        static Grab GrabOf(StudioStage stage, StudioCue cue, Rigidbody body) => cue.fixedGrab
            ? GripFor(stage.ToWorld(cue.grabPosition), cue.grabRotation, BlockSize(body), cue.direction, HandSize(stage))
            : GripFor(body.position, body.rotation, BlockSize(body), cue.direction, HandSize(stage));

        sealed class Pull { public StudioCue cue; public Rigidbody body; public Grab grab; }
        static readonly List<Pull> pulls = new List<Pull>();
        static readonly Dictionary<string, Grab> grabs = new Dictionary<string, Grab>();

        public static void Begin() { pulls.Clear(); grabs.Clear(); }

        internal static string StartPull(StudioStage stage, StudioCue cue)
        {
            var body = StudioDirector.Blocks(stage, cue).FirstOrDefault();
            if (!body) throw new InvalidOperationException("The block to pull is gone.");
            var grab = GrabOf(stage, cue, body);
            foreach (var joint in body.GetComponents<Joint>()) Object.Destroy(joint);
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            pulls.Add(new Pull { cue = cue, body = body, grab = grab });
            grabs[cue.id] = grab;
            return $"block #{cue.blocks.FirstOrDefault()} out {cue.strength:0.#} m in {cue.pullTime:0.##} s";
        }

        // Every frame while recording: put each pulled block exactly where the pull says, so the tape has it there.
        internal static void FrameTick(float time)
        {
            foreach (var pull in pulls)
            {
                if (!pull.body) continue;
                var pose = BlockAt(pull.cue, pull.grab, time);
                pull.body.transform.SetPositionAndRotation(pose.position, pose.rotation);
                pull.body.position = pose.position; pull.body.rotation = pose.rotation;
            }
        }

        // Every physics step: move toward the next pose through physics, so neighbours are pushed for real.
        internal static void FixedTick(float time)
        {
            foreach (var pull in pulls)
            {
                if (!pull.body) continue;
                var pose = BlockAt(pull.cue, pull.grab, time + Time.fixedDeltaTime);
                pull.body.MovePosition(pose.position); pull.body.MoveRotation(pose.rotation);
            }
        }

        // ---- After recording: the hand's take ----

        // The hand design (CandyHandDesigns index, 0 = Classic) written into directed takes. Set it before recording.
        public static int BakeLook;

        // Writes a new take on each hand mark that has handPull cues. Returns a note for the log.
        public static string Bake(StudioStage stage)
        {
            var notes = new List<string>();
            float size = HandSize(stage);
            foreach (var group in stage.set.cues.Where(c => c.action == HandPullCue.Id && !string.IsNullOrEmpty(c.actor)).GroupBy(c => c.actor))
            {
                var track = stage.tape.Find(group.Key);
                if (track == null || track.type != HandTrack.Id) { notes.Add("no hand track " + group.Key); continue; }
                var cues = group.OrderBy(c => c.at).ToList();
                var grabsFor = new List<Grab>();
                foreach (var cue in cues)
                {
                    if (grabs.TryGetValue(cue.id, out var grab)) grabsFor.Add(grab);
                    else
                    {
                        var body = StudioDirector.Blocks(stage, cue).FirstOrDefault();
                        if (!body) { notes.Add("cue " + cue.id + ": block gone"); grabsFor.Add(default); continue; }
                        grabsFor.Add(GrabOf(stage, cue, body));
                    }
                }
                var take = new StudioTake { start = 0, keyEvery = 1, name = "Directed " + (track.takes.Count + 1) };
                var hidden = new ActorFrame { isHand = true, visible = false }.Write();
                int shown = 0;
                for (int frame = 0; frame < stage.Frames; frame++)
                {
                    float time = frame / (float)stage.Fps;
                    // The cue whose flight has started most recently is the one in charge.
                    int index = -1;
                    for (int i = 0; i < cues.Count; i++)
                        if (time >= cues[i].at - Mathf.Max(.05f, cues[i].grip) - Mathf.Max(.2f, cues[i].duration)) index = i;
                    if (index < 0 || !HandAt(stage, cues[index], grabsFor[index], time, out var pose)) { take.frames.Add(hidden); continue; }
                    var hand = new ActorFrame { isHand = true, visible = true, left = false, size = size, handLook = BakeLook, target = pose, bones = BonesFor(pose, size), health = 100 };
                    take.frames.Add(hand.Write());
                    shown++;
                }
                track.takes.Add(take);
                track.active = track.takes.Count - 1;
                StudioBadges.ClearAfterRecording(track);
                notes.Add($"{track.name}: directed take, {shown} frames shown");
            }
            return string.Join("; ", notes);
        }
    }

    // Fly the giant hand to a block and yank it out on the beat. See StudioHandDirection.
    public sealed class HandPullCue : IStudioCueType
    {
        public const string Id = "handPull";
        public string Action => Id;
        public string Label => "Hand pulls a block";
        public string Run(StudioStage stage, StudioCue cue) => StudioHandDirection.StartPull(stage, cue);
    }
}
