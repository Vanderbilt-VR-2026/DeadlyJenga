using System.IO;
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

        // Where the actor stood on a recorded frame (for path lines). False if the frame holds no pose.
        public static bool TryReadPosition(string typeId, byte[] frame, out Vector3 position)
        {
            position = default;
            if (frame == null || frame.Length < 8) return false;
            using (var r = new BinaryReader(new MemoryStream(frame)))
            {
                if (!r.ReadBoolean()) return false;
                int length = r.ReadInt32();
                // Survivor state starts with the feet. Hand state starts with: left, size, two flags, then the position.
                int skip = typeId == HandTrack.Id ? 7 : 0;
                if (length < skip + 12) return false;
                r.BaseStream.Position += skip;
                position = NetWire.ReadVector(r);
                return true;
            }
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
            bool failed;

            public Player(StudioStage stage, StudioTrack track, StudioTake take, string kind)
            {
                this.stage = stage; this.track = track; this.take = take;
                root = new GameObject("Studio playback: " + track.name);
                var session = stage.session;
                view = NetworkActors.CreateView(kind, root, new NetworkContext
                {
                    host = false, actorId = track.id,
                    localSurvivor = NetworkActors.Alive(session.Player) ? session.Player.gameObject : null,
                    localHand = NetworkActors.Alive(session.Hand) ? session.Hand.gameObject : null
                });
                stage.playback[track.id] = root;
            }

            public override void Show(int frame, bool running)
            {
                if (view == null || failed) return;
                if (!take.Covers(frame)) { root.SetActive(false); return; }
                byte[] data = take.frames[frame - take.start];
                try
                {
                    using (var r = new BinaryReader(new MemoryStream(data)))
                    {
                        bool active = r.ReadBoolean();
                        byte[] state = r.ReadBytes(r.ReadInt32());
                        root.SetActive(active);
                        if (!active) return;
                        using (var inner = new BinaryReader(new MemoryStream(state))) view.ReadState(inner, true);
                        // The copy normally eases toward new poses to hide network lag. A recording has no lag,
                        // so it is told to land exactly on the recorded pose.
                        if (view is INetworkActorSnap snap) { snap.SnapToState(); view.Render(running ? Time.deltaTime : 0); }
                        else view.Render(1000f);
                        if (stage.recording) look.Apply(root, StudioTrackNotes.Of(track), StudioColors.For(stage.set.Mark(track.id)?.color ?? 0));
                    }
                }
                catch (System.Exception error)
                {
                    failed = true;
                    track.badge = "Could not play: " + error.Message;
                    Debug.LogWarning($"Trailer Studio: {track.name} could not be played back. {error.Message}");
                }
            }

            public override void Dispose()
            {
                stage.playback.Remove(track.id);
                look.Dispose();
                if (root) Object.Destroy(root);
            }
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
