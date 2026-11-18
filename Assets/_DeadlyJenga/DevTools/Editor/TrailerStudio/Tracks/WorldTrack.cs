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
                    int key = index - index % Mathf.Max(1, take.keyEvery);
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

            // Exact playback: put every block exactly where the tape says. Used when nothing live shares the scene.
            public override void Show(int frame, bool running)
            {
                if (running && stage.physicalPlayback) { ShowActive(); return; }
                Seek(frame);
                for (int i = 0; i < blocks.Length; i++)
                {
                    var body = blocks[i];
                    if (!body || (now[i].flags & Destroyed) != 0) continue;
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
                    if (!body || (now[i].flags & Destroyed) != 0) continue;
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
