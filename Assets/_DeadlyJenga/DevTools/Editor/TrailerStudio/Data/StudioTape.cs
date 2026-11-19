using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // How a track behaves during a run.
    public enum TrackMode { Playback = 0, Live = 1, Muted = 2 }

    // One attempt at a track. frames[i] is that track type's own bytes for tape frame start + i.
    public sealed class StudioTake
    {
        public string id = Guid.NewGuid().ToString("N").Substring(0, 8);
        public string name = "Take";
        public long created = DateTime.UtcNow.Ticks;
        public int start;                 // tape frame of frames[0]
        public int keyEvery = 1;          // 1 = every frame stands alone; larger = deltas between key frames
        public bool trashed, starred;
        public string meta = "";          // free JSON for the track type (for example the block count)
        public readonly List<byte[]> frames = new List<byte[]>();

        public int End => start + frames.Count;
        public bool Covers(int frame) => frame >= start && frame < End;
        public long Bytes => frames.Sum(f => (long)(f?.Length ?? 0));
    }

    // One row of the timeline: an actor, the world, the hazards...
    public sealed class StudioTrack
    {
        public string id, type, name;
        public bool locked, solo;
        public TrackMode mode = TrackMode.Playback;
        public int active = -1;           // index into takes, -1 = nothing recorded yet
        public string meta = "";          // free JSON for the track type
        public string badge = "";         // honest status shown on the row; one line per kind, see StudioBadges
        public readonly List<StudioTake> takes = new List<StudioTake>();

        public StudioTake ActiveTake => active >= 0 && active < takes.Count ? takes[active] : null;
        public IEnumerable<StudioTake> Kept => takes.Where(t => !t.trashed);

        public StudioTake AddTake(int start, int keyEvery)
        {
            var take = new StudioTake { start = start, keyEvery = keyEvery, name = "Take " + (takes.Count + 1) };
            takes.Add(take);
            active = takes.Count - 1;
            return take;
        }
    }

    // A track can have several warnings at once ("Stale: ...", "Retimed: ...", "Could not play: ...").
    // They share track.badge, one per line, each starting with its kind, so setting or clearing one
    // never touches the others. Old tapes with a single warning read as one line: nothing changes on disk.
    public static class StudioBadges
    {
        public const string Stale = "Stale:", Retimed = "Retimed:", CouldNotPlay = "Could not play:";
        static readonly string[] Order = { Stale, Retimed, CouldNotPlay };

        public static List<string> Parts(string badge) =>
            string.IsNullOrEmpty(badge) ? new List<string>() : badge.Split('\n').Where(p => p.Trim().Length > 0).ToList();

        public static List<string> Parts(StudioTrack track) => Parts(track?.badge);

        // The kind of a warning line: the known prefix it starts with, or "" for any other text.
        public static string KindOf(string part) => Order.FirstOrDefault(k => part.StartsWith(k)) ?? "";

        public static bool Has(StudioTrack track, string kind) => Parts(track).Any(p => p.StartsWith(kind));

        // Set one kind of warning. 'message' is the text after the kind ("Stale:" + " " + message).
        public static string With(string badge, string kind, string message)
        {
            var parts = Parts(badge).Where(p => !p.StartsWith(kind)).ToList();
            parts.Add(kind + " " + (message ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim());
            // Always the same order, so the row reads the same whichever was written last.
            return string.Join("\n", parts.OrderBy(p => { int i = Array.IndexOf(Order, KindOf(p)); return i < 0 ? Order.Length : i; }));
        }

        public static string Without(string badge, string kind) => string.Join("\n", Parts(badge).Where(p => !p.StartsWith(kind)));

        public static void Set(StudioTrack track, string kind, string message) => track.badge = With(track.badge, kind, message);

        public static void Clear(StudioTrack track, string kind)
        {
            if (!string.IsNullOrEmpty(track.badge) && track.badge.Contains(kind)) track.badge = Without(track.badge, kind);
        }

        // A track was just recorded again: its new take is not stale or retimed, and gets a fresh chance to play.
        public static void ClearAfterRecording(StudioTrack track)
        {
            Clear(track, Stale); Clear(track, Retimed); Clear(track, CouldNotPlay);
        }
    }

    [Serializable] public sealed class TapeHeader
    {
        public string setId = "", map = "", created = "";
        public int fps = 30, frames;
        public Vector3 stage;
        public int blocks;
        public string notes = "";
    }

    // The recording of a Set over time. See progress/EXTENSION-POINTS.md for the file layout.
    public sealed class StudioTape
    {
        public const int Magic = 0x354A5444;   // "DTJ5", one up from Theatre's v4
        public TapeHeader header = new TapeHeader();
        public readonly List<StudioTrack> tracks = new List<StudioTrack>();
        public string path = "";

        public StudioTrack Find(string id) => tracks.FirstOrDefault(t => t.id == id);
        public StudioTrack FindType(string type) => tracks.FirstOrDefault(t => t.type == type);
        public long Bytes => tracks.Sum(t => t.takes.Sum(k => k.Bytes));

        public StudioTrack GetOrAdd(string id, string type, string name)
        {
            var track = Find(id);
            if (track != null) return track;
            track = new StudioTrack { id = id, type = type, name = name };
            tracks.Add(track);
            return track;
        }

        public void Save(string file)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string temporary = file + ".saving";
            using (var stream = File.Create(temporary))
            {
                using (var plain = new BinaryWriter(stream, Encoding.UTF8, true)) plain.Write(Magic);
                using (var zip = new GZipStream(stream, System.IO.Compression.CompressionLevel.Fastest))
                using (var w = new BinaryWriter(zip, Encoding.UTF8))
                {
                    w.Write(JsonUtility.ToJson(header));
                    w.Write(tracks.Count);
                    foreach (var track in tracks)
                    {
                        byte[] chunk = WriteChunk(track);
                        w.Write(track.type); w.Write(track.id);
                        w.Write(chunk.Length); w.Write(chunk);
                    }
                }
            }
            // Never lose a take: only replace the old file once the new one is complete.
            // Another program (a virus scanner, a backup) can hold the old file for a moment: try a few times.
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(file)) File.Replace(temporary, file, null);
                    else File.Move(temporary, file);
                    break;
                }
                catch (IOException) when (attempt < 8) { System.Threading.Thread.Sleep(150); }
            }
            path = file;
        }

        public static bool IsTape(string file)
        {
            if (!File.Exists(file)) return false;
            using (var r = new BinaryReader(File.OpenRead(file)))
                return r.BaseStream.Length >= 4 && r.ReadInt32() == Magic;
        }

        // only = type ids to load (null loads everything). Skipped tracks are not kept in memory.
        public static StudioTape Load(string file, ICollection<string> only = null)
        {
            var tape = new StudioTape { path = file };
            using (var stream = File.OpenRead(file))
            {
                using (var plain = new BinaryReader(stream, Encoding.UTF8, true))
                    if (plain.ReadInt32() != Magic) throw new InvalidDataException("Not a Trailer Studio tape (v5): " + file);
                using (var zip = new GZipStream(stream, CompressionMode.Decompress))
                using (var r = new BinaryReader(zip, Encoding.UTF8))
                {
                    tape.header = JsonUtility.FromJson<TapeHeader>(r.ReadString()) ?? new TapeHeader();
                    int count = r.ReadInt32();
                    if (count < 0 || count > 4096) throw new InvalidDataException("Invalid track count.");
                    for (int i = 0; i < count; i++)
                    {
                        string type = r.ReadString(), id = r.ReadString();
                        int length = r.ReadInt32();
                        byte[] chunk = ReadExactly(r, length);
                        if (only != null && !only.Contains(type)) continue;   // an unknown or unwanted track is skipped whole
                        tape.tracks.Add(ReadChunk(type, id, chunk));
                    }
                }
            }
            return tape;
        }

        static byte[] WriteChunk(StudioTrack track)
        {
            using (var memory = new MemoryStream())
            using (var w = new BinaryWriter(memory, Encoding.UTF8))
            {
                w.Write(track.name ?? ""); w.Write(track.locked); w.Write(track.solo);
                w.Write((int)track.mode); w.Write(track.active); w.Write(track.meta ?? ""); w.Write(track.badge ?? "");
                w.Write(track.takes.Count);
                foreach (var take in track.takes)
                {
                    w.Write(take.id); w.Write(take.name ?? ""); w.Write(take.created);
                    w.Write(take.start); w.Write(take.keyEvery); w.Write(take.trashed); w.Write(take.starred);
                    w.Write(take.meta ?? "");
                    w.Write(take.frames.Count);
                    foreach (var frame in take.frames)
                    {
                        w.Write(frame?.Length ?? 0);
                        if (frame != null) w.Write(frame);
                    }
                }
                w.Flush();
                return memory.ToArray();
            }
        }

        static StudioTrack ReadChunk(string type, string id, byte[] chunk)
        {
            using (var r = new BinaryReader(new MemoryStream(chunk), Encoding.UTF8))
            {
                var track = new StudioTrack { id = id, type = type, name = r.ReadString(), locked = r.ReadBoolean(), solo = r.ReadBoolean(),
                    mode = (TrackMode)r.ReadInt32(), active = r.ReadInt32(), meta = r.ReadString(), badge = r.ReadString() };
                int takes = r.ReadInt32();
                for (int t = 0; t < takes; t++)
                {
                    var take = new StudioTake { id = r.ReadString(), name = r.ReadString(), created = r.ReadInt64(),
                        start = r.ReadInt32(), keyEvery = r.ReadInt32(), trashed = r.ReadBoolean(), starred = r.ReadBoolean(), meta = r.ReadString() };
                    int frames = r.ReadInt32();
                    for (int f = 0; f < frames; f++) take.frames.Add(ReadExactly(r, r.ReadInt32()));
                    track.takes.Add(take);
                }
                return track;
            }
        }

        static byte[] ReadExactly(BinaryReader r, int length)
        {
            if (length < 0) throw new InvalidDataException("Negative length in tape.");
            byte[] bytes = r.ReadBytes(length);
            if (bytes.Length != length) throw new EndOfStreamException("Tape ended early.");
            return bytes;
        }

        public static string Size(long bytes) =>
            bytes < 1024 ? bytes + " B" : bytes < 1048576 ? (bytes / 1024f).ToString("F0") + " KB" : (bytes / 1048576f).ToString("F1") + " MB";
    }
}
