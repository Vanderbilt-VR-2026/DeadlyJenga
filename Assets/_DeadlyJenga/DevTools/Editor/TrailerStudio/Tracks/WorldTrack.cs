using System;
using System.IO;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Every block of the map: where it is, and whether it is active, asleep or gone.
    // A key frame (all blocks) is written once a second; the frames between only list blocks that changed.
    public sealed class WorldTrack : IStudioTrackType
    {
        public const string Id = "world";
        public const byte Active = 1, Sleeping = 2, Destroyed = 4;

        public string TypeId => Id;
        public string Label => "World";
        public Color Color => StudioColors.World;
        public int Order => 0;

        public void EnsureTracks(StudioSet set, StudioTape tape) => tape.GetOrAdd("world", Id, "World");
        public StudioRecorder CreateRecorder(StudioStage stage, StudioTrack track) => new Recorder(stage);
        public StudioPlayer CreatePlayer(StudioStage stage, StudioTrack track, StudioTake take) => new Player(stage, take);

        [Serializable] sealed class TakeMeta { public int blocks; }

        // What one block looked like on one frame, in tape units.
        struct Sample
        {
            public byte flags;
            public int x, y, z;
            public ulong rotation;
            public bool Same(Sample o) => flags == o.flags && x == o.x && y == o.y && z == o.z && rotation == o.rotation;
            public Vector3 Position => new Vector3(TapePack.Metres(x), TapePack.Metres(y), TapePack.Metres(z));
        }

        static void Write(BinaryWriter w, Sample s)
        {
            w.Write(s.flags);
            if ((s.flags & Active) == 0) return;
            w.Write(s.x); w.Write(s.y); w.Write(s.z);
            TapePack.WriteRotation(w, s.rotation);
        }

        static Sample Read(BinaryReader r, Sample previous)
        {
            var s = previous;
            s.flags = r.ReadByte();
            if ((s.flags & Active) == 0) return s;
            s.x = r.ReadInt32(); s.y = r.ReadInt32(); s.z = r.ReadInt32();
            s.rotation = TapePack.ReadRotation(r);
            return s;
        }

        // Turns block poses into frame bytes. Also used to convert old Theatre tapes.
        public sealed class Encoder
        {
            readonly Sample[] last, now;
            public int Blocks => last.Length;
            public string Meta => JsonUtility.ToJson(new TakeMeta { blocks = last.Length });

            public Encoder(int blocks) { last = new Sample[blocks]; now = new Sample[blocks]; }

            public void Set(int block, byte flags, Vector3 position, Quaternion rotation)
            {
                now[block] = (flags & Active) == 0 ? new Sample { flags = flags } : new Sample
                {
                    flags = flags, x = TapePack.Mm(position.x), y = TapePack.Mm(position.y), z = TapePack.Mm(position.z),
                    rotation = TapePack.PackRotation(rotation)
                };
            }

            // Call Set for every block, then Frame once.
            public byte[] Frame(bool key)
            {
                using (var memory = new MemoryStream())
                using (var w = new BinaryWriter(memory))
                {
                    w.Write(key);
                    var changed = new bool[now.Length];
                    for (int i = 0; i < now.Length; i++)
                    {
                        changed[i] = key || !now[i].Same(last[i]);
                        last[i] = now[i];
                    }
                    if (!key)
                    {
                        // One bit per block: did it change since the frame before?
                        var bits = new byte[(now.Length + 7) / 8];
                        for (int i = 0; i < now.Length; i++) if (changed[i]) bits[i >> 3] |= (byte)(1 << (i & 7));
                        w.Write(bits);
                    }
                    for (int i = 0; i < now.Length; i++) if (changed[i]) Write(w, last[i]);
                    w.Flush();
                    return memory.ToArray();
                }
            }
        }

        // The first tape frame where two world takes put any block more than "millimetres" apart
        // (or one has a block the other lost). -1 = they never differ.
        public static int FirstDifference(StudioTake a, StudioTake b, float millimetres)
        {
            int Count(StudioTake take) => string.IsNullOrEmpty(take.meta) ? 0 : JsonUtility.FromJson<TakeMeta>(take.meta)?.blocks ?? 0;
            int blocks = Count(a);
            if (blocks == 0 || blocks != Count(b)) return Mathf.Max(a.start, b.start);
            var first = new Sample[blocks]; var second = new Sample[blocks];
            int from = Mathf.Max(a.start, b.start), to = Mathf.Min(a.End, b.End);
            // Both takes are decoded forward from their own start, one frame at a time.
            for (int f = a.start; f < from; f++) DecodeInto(a.frames[f - a.start], first);
            for (int f = b.start; f < from; f++) DecodeInto(b.frames[f - b.start], second);
            int limit = Mathf.RoundToInt(millimetres);
            for (int f = from; f < to; f++)
            {
                DecodeInto(a.frames[f - a.start], first);
                DecodeInto(b.frames[f - b.start], second);
                for (int i = 0; i < blocks; i++)
                {
                    bool on = (first[i].flags & Active) != 0;
                    if (on != ((second[i].flags & Active) != 0)) return f;
                    if (on && (Mathf.Abs(first[i].x - second[i].x) > limit || Mathf.Abs(first[i].y - second[i].y) > limit || Mathf.Abs(first[i].z - second[i].z) > limit)) return f;
                }
            }
            return -1;
        }

        // Where every block is on one tape frame of a take (decoded from the take's start). active[i] is false for hidden or lost blocks.
        // Decoding a frame means decoding every frame before it. Callers usually ask for frames in
        // order (or the same frame again), so the last answer is kept and continued from.
        static StudioTake cachedTake;
        static int cachedIndex = -1, cachedCount;
        static Sample[] cachedState;

        public static bool Positions(StudioTake take, int frame, out Vector3[] positions, out bool[] active)
        {
            int blocks = string.IsNullOrEmpty(take.meta) ? 0 : JsonUtility.FromJson<TakeMeta>(take.meta)?.blocks ?? 0;
            positions = new Vector3[blocks]; active = new bool[blocks];
            if (blocks == 0 || take.frames.Count == 0) return false;
            int last = Mathf.Clamp(frame, take.start, take.End - 1) - take.start;
            bool goOn = cachedTake == take && cachedState != null && cachedState.Length == blocks && cachedCount == take.frames.Count && cachedIndex <= last;
            if (!goOn) { cachedTake = take; cachedState = new Sample[blocks]; cachedIndex = -1; cachedCount = take.frames.Count; }
            for (int i = cachedIndex + 1; i <= last; i++) if (take.frames[i] != null && take.frames[i].Length > 0) DecodeInto(take.frames[i], cachedState);
            cachedIndex = last;
            for (int i = 0; i < blocks; i++) { active[i] = (cachedState[i].flags & Active) != 0; positions[i] = cachedState[i].Position; }
            return true;
        }

        static void DecodeInto(byte[] frame, Sample[] state)
        {
            using (var r = new BinaryReader(new MemoryStream(frame)))
            {
                bool key = r.ReadBoolean();
                byte[] bits = key ? null : r.ReadBytes((state.Length + 7) / 8);
                for (int i = 0; i < state.Length; i++)
                    if (key || (bits[i >> 3] & (1 << (i & 7))) != 0) state[i] = Read(r, state[i]);
            }
        }

        sealed class Recorder : StudioRecorder
        {
            readonly Rigidbody[] blocks;
            readonly Encoder encoder;
            readonly int keyEvery;
            public override int KeyEvery => keyEvery;
            public override string Meta => encoder.Meta;

            public Recorder(StudioStage stage)
            {
                blocks = stage.blocks;
                encoder = new Encoder(blocks.Length);
                keyEvery = Mathf.Max(1, stage.Fps);
            }

            public override byte[] Capture(int frame, bool key)
            {
                for (int i = 0; i < blocks.Length; i++)
                {
                    var body = blocks[i];
                    if (!body) encoder.Set(i, Destroyed, default, Quaternion.identity);
                    else if (!body.gameObject.activeSelf) encoder.Set(i, 0, default, Quaternion.identity);
                    // The transform is what the camera saw this frame (it includes physics interpolation).
                    else encoder.Set(i, (byte)(Active | (body.IsSleeping() ? Sleeping : 0)), body.transform.position, body.transform.rotation);
                }
                return encoder.Frame(key);
            }
        }

        sealed class Player : StudioPlayer
        {
            readonly StudioStage stage;
            readonly StudioTake take;
            readonly Rigidbody[] blocks;
            readonly Sample[] now, before;   // decoded frame, and the frame before it (for blending between frames)
            int decoded = -1;                // index into take.frames that "now" holds
            bool hasBefore;

            public Player(StudioStage stage, StudioTake take)
            {
                this.stage = stage; this.take = take;
                blocks = stage.blocks;
                var meta = string.IsNullOrEmpty(take.meta) ? null : JsonUtility.FromJson<TakeMeta>(take.meta);
                if (meta != null && meta.blocks != blocks.Length)
                    throw new InvalidDataException($"This world take has {meta.blocks} blocks but the map has {blocks.Length}. Was the map changed?");
                now = new Sample[blocks.Length];
                before = new Sample[blocks.Length];
            }

            void Seek(int frame)
            {
                int index = Mathf.Clamp(frame - take.start, 0, take.frames.Count - 1);
                if (index == decoded || take.frames.Count == 0) return;
                if (index != decoded + 1 || decoded < 0)
                {
                    // Jump: start from the key frame at or before the wanted one.
                    // Key frames sit on whole multiples of keyEvery in tape frames (and on the take's first frame).
                    // Start one frame early, so the frame before the wanted one is known too (for blending).
                    int tapeFrame = take.start + Mathf.Max(0, index - 1);
                    int key = Mathf.Max(0, tapeFrame - tapeFrame % Mathf.Max(1, take.keyEvery) - take.start);
                    decoded = -1; hasBefore = false;
                    for (int i = key; i < index; i++) Decode(i);
                }
                Array.Copy(now, before, now.Length);
                hasBefore = decoded >= 0;
                Decode(index);
            }

            void Decode(int index)
            {
                using (var r = new BinaryReader(new MemoryStream(take.frames[index])))
                {
                    bool key = r.ReadBoolean();
                    byte[] bits = key ? null : r.ReadBytes((blocks.Length + 7) / 8);
                    for (int i = 0; i < blocks.Length; i++)
                        if (key || (bits[i >> 3] & (1 << (i & 7))) != 0) now[i] = Read(r, now[i]);
                }
                decoded = index;
            }

            // A moment between two frames: every block part of the way from one pose to the next.
            public override void ShowBetween(int frame, float part, bool running)
            {
                if (part <= .001f || !take.Covers(frame + 1) || running && stage.physicalPlayback) { Show(frame, running); return; }
                Seek(frame + 1);
                if (!hasBefore) { Show(frame + 1, running); return; }
                for (int i = 0; i < blocks.Length; i++)
                {
                    var body = blocks[i];
                    if (!body) continue;
                    // Gone on the tape but back on a freshly built map: keep it hidden.
                    if ((now[i].flags & Destroyed) != 0) { if (body.gameObject.activeSelf) body.gameObject.SetActive(false); continue; }
                    bool active = (now[i].flags & Active) != 0;
                    if (body.gameObject.activeSelf != active) body.gameObject.SetActive(active);
                    if (!active) continue;
                    bool blend = (before[i].flags & Active) != 0;
                    body.transform.SetPositionAndRotation(
                        blend ? Vector3.Lerp(before[i].Position, now[i].Position, part) : now[i].Position,
                        blend ? Quaternion.Slerp(TapePack.UnpackRotation(before[i].rotation), TapePack.UnpackRotation(now[i].rotation), part) : TapePack.UnpackRotation(now[i].rotation));
                }
                Physics.SyncTransforms();
            }

            // Exact playback: put every block exactly where the tape says. Used when nothing live shares the scene.
            public override void Show(int frame, bool running)
            {
                if (running && stage.physicalPlayback) { ShowActive(); return; }
                Seek(frame);
                for (int i = 0; i < blocks.Length; i++)
                {
                    var body = blocks[i];
                    if (!body) continue;
                    // Gone on the tape but back on a freshly built map: keep it hidden.
                    if ((now[i].flags & Destroyed) != 0) { if (body.gameObject.activeSelf) body.gameObject.SetActive(false); continue; }
                    bool active = (now[i].flags & Active) != 0;
                    if (body.gameObject.activeSelf != active) body.gameObject.SetActive(active);
                    if (active) body.transform.SetPositionAndRotation(now[i].Position, TapePack.UnpackRotation(now[i].rotation));
                }
                Physics.SyncTransforms();
            }

            // Physical playback: move kinematic blocks through physics so live characters can stand on them
            // and get pushed. Each physics step aims at where the tape puts the block at that moment.
            public override void FixedStep(float time)
            {
                if (!stage.physicalPlayback) return;
                float exact = Mathf.Max(0, time * stage.Fps);
                int next = Mathf.CeilToInt(exact);
                Seek(next);
                float blend = hasBefore ? 1 - (next - exact) : 1;
                for (int i = 0; i < blocks.Length; i++)
                {
                    var body = blocks[i];
                    if (!body || (now[i].flags & Active) == 0 || !body.gameObject.activeSelf) continue;
                    bool blendable = hasBefore && (before[i].flags & Active) != 0;
                    Vector3 position = blendable ? Vector3.Lerp(before[i].Position, now[i].Position, blend) : now[i].Position;
                    Quaternion rotation = blendable
                        ? Quaternion.Slerp(TapePack.UnpackRotation(before[i].rotation), TapePack.UnpackRotation(now[i].rotation), blend)
                        : TapePack.UnpackRotation(now[i].rotation);
                    body.MovePosition(position);
                    body.MoveRotation(rotation);
                }
            }

            void ShowActive()
            {
                if (decoded < 0) return;
                for (int i = 0; i < blocks.Length; i++)
                {
                    var body = blocks[i];
                    if (!body) continue;
                    // Gone on the tape but back on a freshly built map: keep it hidden.
                    if ((now[i].flags & Destroyed) != 0) { if (body.gameObject.activeSelf) body.gameObject.SetActive(false); continue; }
                    bool active = (now[i].flags & Active) != 0;
                    if (body.gameObject.activeSelf == active) continue;
                    body.gameObject.SetActive(active);
                    if (active) body.transform.SetPositionAndRotation(now[i].Position, TapePack.UnpackRotation(now[i].rotation));
                }
            }

            public override void CheckDrift(int frame, StudioDrift report)
            {
                if (stage.physicalPlayback || decoded < 0) return;
                for (int i = 0; i < blocks.Length; i++)
                {
                    var body = blocks[i];
                    if (!body || (now[i].flags & Active) == 0) continue;
                    if (!body.gameObject.activeSelf) { report.Add(frame, body.name + " is hidden", 1000, 0); continue; }
                    float mm = Vector3.Distance(body.transform.position, now[i].Position) * 1000;
                    float degrees = Quaternion.Angle(body.transform.rotation, TapePack.UnpackRotation(now[i].rotation));
                    report.Add(frame, body.name + " #" + i, mm, degrees);
                }
            }
        }
    }
}
