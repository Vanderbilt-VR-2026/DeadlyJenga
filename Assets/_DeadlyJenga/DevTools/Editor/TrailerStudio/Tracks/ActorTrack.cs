using System.IO;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CharacterNetwork;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // A survivor or a hand. The character packs its own state (the same bytes multiplayer sends),
    // and playback shows it with the same "someone else's character" copy multiplayer uses.
    // That copy is solid, so a live player can bump into a played-back actor.
    public abstract class ActorTrack : IStudioTrackType
    {
        public abstract string TypeId { get; }
        public abstract string Label { get; }
        public abstract int Order { get; }
        protected abstract string Kind { get; }                // NetworkKinds.Survivor or NetworkKinds.Hand
        protected abstract bool Wants(StudioMark mark);
        public Color Color => StudioColors.For(0);

        public void EnsureTracks(StudioSet set, StudioTape tape)
        {
            foreach (var mark in set.marks)
                if (Wants(mark)) tape.GetOrAdd(mark.id, TypeId, mark.actor).name = mark.actor;
        }

        public StudioRecorder CreateRecorder(StudioStage stage, StudioTrack track)
        {
            var body = stage.Live(track.id);
            var actor = body ? body.GetComponentInChildren<INetworkActor>(true) : null;
            return actor == null ? null : new Recorder(body, actor, track.id);
        }

        public StudioPlayer CreatePlayer(StudioStage stage, StudioTrack track, StudioTake take) =>
            new Player(stage, track, take, Kind);

        internal StudioPlayer CreateStarredGhost(StudioStage stage, StudioTrack track, StudioTake take, StudioTake world) =>
            new Player(stage, track, take, Kind, world);

        // Where the actor stood on a recorded frame (for path lines). False if the frame holds no pose.
        public static bool TryReadPosition(string typeId, byte[] frame, out Vector3 position)
        {
            position = default;
            if (frame == null || frame.Length < 8) return false;
            try
            {
                var pose = ActorFrame.Read(typeId, frame);
                position = pose.Position;
                return pose.visible;
            }
            catch (System.Exception) { return false; }
        }

        // One actor frame: is it visible, its own state bytes, its health and its equipment.
        public static byte[] Pack(bool active, byte[] state, float health, string equipmentJson)
        {
            using (var memory = new MemoryStream())
            using (var w = new BinaryWriter(memory))
            {
                w.Write(active);
                w.Write(state?.Length ?? 0);
                if (state != null) w.Write(state);
                w.Write(health);
                w.Write(equipmentJson ?? "");
                w.Flush();
                return memory.ToArray();
            }
        }

        sealed class Recorder : StudioRecorder
        {
            readonly GameObject body;
            readonly INetworkActor actor;
            readonly string id;

            public Recorder(GameObject body, INetworkActor actor, string id) { this.body = body; this.actor = actor; this.id = id; }

            public override byte[] Capture(int frame, bool key)
            {
                bool active = body && body.activeInHierarchy && actor.NetworkActive;
                byte[] state = null;
                if (active)
                    using (var inner = new MemoryStream())
                    using (var innerWriter = new BinaryWriter(inner))
                    {
                        actor.WriteState(innerWriter);
                        innerWriter.Flush();
                        state = inner.ToArray();
                    }
                var health = body ? body.GetComponent<IDamageable>() : null;
                var equipment = body ? body.GetComponent<IEquipmentControls>()?.Equipment?.Copy() : null;
                if (equipment != null) equipment.actor = id;
                return Pack(active, state, health != null ? health.Health : 0f, equipment != null ? JsonUtility.ToJson(equipment) : "");
            }
        }

        sealed class Player : StudioPlayer
        {
            readonly StudioStage stage;
            readonly StudioTrack track;
            readonly StudioTake take;
            readonly GameObject root;
            readonly INetworkActorView view;
            readonly StudioGhostLook look = new StudioGhostLook();
            static readonly StudioTrackNotes ghostNotes = new StudioTrackNotes { ghost = 1, passThrough = true };
            static readonly System.Collections.Generic.List<IActorFrameFilter> ghostFilters = new System.Collections.Generic.List<IActorFrameFilter>();
            readonly StudioTake ghostWorld;
            CandyWeaponVisual weapon;
            bool failed;

            public Player(StudioStage stage, StudioTrack track, StudioTake take, string kind, StudioTake ghostWorld = null)
            {
                this.stage = stage; this.track = track; this.take = take; this.ghostWorld = ghostWorld;
                root = new GameObject(ghostWorld == null ? "Studio playback: " + track.name : $"Studio reference ghost: {track.name} · {take.name}");
                var session = stage.session;
                view = NetworkActors.CreateView(kind, root, new NetworkContext
                {
                    host = false, actorId = ghostWorld == null ? track.id : "studio-reference-" + track.id,
                    localSurvivor = NetworkActors.Alive(session.Player) ? session.Player.gameObject : null,
                    localHand = NetworkActors.Alive(session.Hand) ? session.Hand.gameObject : null
                });
                if (ghostWorld == null) stage.playback[track.id] = root;
                else stage.starredGhost = root;
            }

            public override void Show(int frame, bool running) => ShowBetween(frame, 0, running);

            // The avatar works its walk cycle out from one frame to the next (how far the feet moved, how
            // far through a step it is). Left alone, the legs would depend on what was shown before, and
            // would stand still in a render. So the Studio walks through the take once, keeps the walk
            // state after every frame, and hands the right one back before showing any frame.
            CandyPlayerRig.GaitState[] gaitAfter;

            // Finished takes do not change, so the walk states are worked out once per take and kept
            // for as long as the take is in memory (players are rebuilt often; takes are not).
            static readonly System.Runtime.CompilerServices.ConditionalWeakTable<StudioTake, CandyPlayerRig.GaitState[]> gaitCache =
                new System.Runtime.CompilerServices.ConditionalWeakTable<StudioTake, CandyPlayerRig.GaitState[]>();

            void BuildGait()
            {
                if (gaitCache.TryGetValue(take, out gaitAfter) && gaitAfter.Length == take.frames.Count) return;
                gaitAfter = new CandyPlayerRig.GaitState[take.frames.Count];
                float step = 1f / stage.Fps;
                var gait = default(CandyPlayerRig.GaitState);
                for (int i = 0; i < take.frames.Count; i++)
                {
                    // Only the numbers are needed here: no avatar is posed and no game event is raised.
                    var pose = ActorFrame.Read(track.type, take.frames[i]);
                    if (pose.visible) gait = CandyPlayerRig.StepGait(gait, pose.body, step);
                    gaitAfter[i] = gait;
                }
                gaitCache.Remove(take);
                gaitCache.Add(take, gaitAfter);
            }

            // Put the copy on one packed frame. False if the actor is hidden on that frame.
            bool Pose(byte[] data, float step, SurvivorNetworkView walker, CandyPlayerRig.GaitState gait, out string equipment)
            {
                using (var r = new BinaryReader(new MemoryStream(data)))
                {
                    bool active = r.ReadBoolean();
                    byte[] state = r.ReadBytes(r.ReadInt32());
                    r.ReadSingle();
                    equipment = r.ReadString();
                    root.SetActive(active);
                    if (!active) return false;
                    using (var inner = new BinaryReader(new MemoryStream(state))) view.ReadState(inner, true);
                    // The copy normally eases toward new poses to hide network lag. A recording has no lag,
                    // so it is told to land exactly on the recorded pose.
                    if (view is INetworkActorSnap snap)
                    {
                        snap.SnapToState();
                        if (walker != null) walker.Gait = gait;
                        view.Render(step);
                    }
                    else view.Render(1000f);
                    return true;
                }
            }

            // part = how far between this frame and the next (slow motion and renders ask for in-between poses).
            public override void ShowBetween(int frame, float part, bool running)
            {
                if (view == null || failed) return;
                if (!take.Covers(frame) || (ghostWorld != null && (!ghostWorld.Covers(frame) || StudioPerform.GhostsHidden)))
                { root.SetActive(false); return; }
                try
                {
                    int index = frame - take.start;
                    byte[] data = take.frames[index];
                    float time = (frame + part) / stage.Fps;
                    // Only open the frame up when something needs to change it: an in-between pose, or an override.
                    bool between = part > .001f && take.Covers(frame + 1);
                    var filters = ghostWorld == null ? ActorFrameFilters.For(stage, track) : ghostFilters;
                    bool hand = track.type == HandTrack.Id;
                    ActorFrame pose = null;
                    if (between || filters.Count > 0 || hand)
                    {
                        pose = ActorFrame.Read(track.type, data);
                        if (between)
                        {
                            // Only blend two shown frames: a hidden frame has no pose (zero rotations), and a blend
                            // with it is refused by the network reader, which stopped the hand playing back for good.
                            var next = ActorFrame.Read(track.type, take.frames[index + 1]);
                            if (pose.visible && next.visible) pose = ActorFrame.Blend(pose, next, part);
                        }
                        foreach (var filter in filters) filter.Apply(stage, track, time, pose);
                        // A hand frame with no bones is never drawn by the copy; give it bones from its target.
                        if (hand) StudioHandDirection.FillBones(pose);
                        data = pose.Write();
                    }

                    // Survivors: the walk state from just before this moment, and the tape time since then.
                    var walker = view as SurvivorNetworkView;
                    var gait = default(CandyPlayerRig.GaitState);
                    float step = walker != null ? 1f / stage.Fps : running ? Time.deltaTime : 0;
                    if (walker != null)
                    {
                        if (gaitAfter == null || gaitAfter.Length != take.frames.Count) BuildGait();
                        int from = between ? index : index - 1;      // the tape frame the walk continues from
                        if (from >= 0) gait = gaitAfter[from];
                        if (between) step *= part;
                        if (from >= 0 && filters.Count > 0 && gait.ready)
                        {
                            // An override may have moved that frame too; measure the step from where it was really shown.
                            var last = ActorFrame.Read(track.type, take.frames[from]);
                            foreach (var filter in filters) filter.Apply(stage, track, (take.start + from) / (float)stage.Fps, last);
                            if (last.visible) gait.previousFeet = last.body.feet;
                        }
                    }

                    if (!Pose(data, step, walker, gait, out string equipment)) { weapon?.Hide(); return; }
                    if (hand) StudioHandDirection.SyncVisual(root, pose);
                    ShowWeapon(equipment, pose, data);
                    if (ghostWorld != null) look.Apply(root, ghostNotes, new Color(.34f, .9f, 1));
                    else if (stage.recording) look.Apply(root, StudioTrackNotes.Of(track), StudioColors.For(stage.set.Mark(track.id)?.color ?? 0));
                }
                catch (System.Exception error)
                {
                    failed = true;
                    if (ghostWorld == null) StudioBadges.Set(track, StudioBadges.CouldNotPlay, error.Message);
                    else root.SetActive(false);
                    Debug.LogWarning($"Trailer Studio: {track.name} could not be played back. {error.Message} (frame {frame} + {part:0.###})\n{error}");
                }
            }

            // The copy asks the live match what its actor holds, and a recorded actor is not in the match.
            // So the Studio draws the recorded weapon itself, with the game's own weapon model code.
            void ShowWeapon(string equipmentJson, ActorFrame pose, byte[] data)
            {
                if (track.type != SurvivorTrack.Id) return;
                var state = string.IsNullOrEmpty(equipmentJson) ? null : JsonUtility.FromJson<EquipmentState>(equipmentJson);
                if (state == null || state.kind < EquipmentKind.Blaster || !state.drawn) { weapon?.Hide(); return; }
                if (weapon == null)
                {
                    var rig = root.GetComponentInChildren<CandyPlayerRig>();
                    if (!rig) return;
                    weapon = new CandyWeaponVisual(root.transform, rig.style ? rig.style.material : null);
                }
                pose ??= ActorFrame.Read(track.type, data);
                weapon.Present(state, pose.body, true);
            }

            public override void Dispose()
            {
                if (ghostWorld == null) stage.playback.Remove(track.id);
                else if (stage.starredGhost == root) stage.starredGhost = null;
                look.Dispose();
                weapon?.Hide();
                if (root) { root.SetActive(false); Object.Destroy(root); }
            }
        }
    }

    // Changes a recorded actor frame just before it is shown (look-ats, reaches, poses, reactions...).
    // Implement this in a new file; the Studio finds it by itself. The take underneath is never changed.
    public interface IActorFrameFilter
    {
        int Order { get; }
        // Quick check, so actors with nothing layered on them are not opened up every frame.
        bool Wants(StudioStage stage, StudioTrack track);
        void Apply(StudioStage stage, StudioTrack track, float time, ActorFrame frame);
    }

    public static class ActorFrameFilters
    {
        static System.Collections.Generic.List<IActorFrameFilter> all;
        static readonly System.Collections.Generic.List<IActorFrameFilter> none = new System.Collections.Generic.List<IActorFrameFilter>();

        public static System.Collections.Generic.List<IActorFrameFilter> For(StudioStage stage, StudioTrack track)
        {
            all ??= System.Linq.Enumerable.ToList(System.Linq.Enumerable.OrderBy(StudioRegistry.Create<IActorFrameFilter>(), f => f.Order));
            if (all.Count == 0) return none;
            System.Collections.Generic.List<IActorFrameFilter> wanted = null;
            foreach (var filter in all)
                if (filter.Wants(stage, track)) (wanted ??= new System.Collections.Generic.List<IActorFrameFilter>()).Add(filter);
            return wanted ?? none;
        }
    }
    public sealed class SurvivorTrack : ActorTrack
    {
        public const string Id = "survivor";
        public override string TypeId => Id;
        public override string Label => "Survivors";
        public override int Order => 30;
        protected override string Kind => NetworkKinds.Survivor;
        protected override bool Wants(StudioMark mark) => mark.role != StudioRole.Hand;
    }

    public sealed class HandTrack : ActorTrack
    {
        public const string Id = "hand";
        public override string TypeId => Id;
        public override string Label => "Hand";
        public override int Order => 20;
        protected override string Kind => NetworkKinds.Hand;
        protected override bool Wants(StudioMark mark) => mark.role == StudioRole.Hand;
    }
}
