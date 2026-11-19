using System;
using System.IO;
using System.Text;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Small helpers for the 16-bit PCM WAV files the Studio records: read, write, join and mix.
    // Tape frame N starts at sample N * rate / fps (rounded), the same rule the sound capture uses
    // when it pads or trims a recording to whole frames.
    public static class StudioWave
    {
        public sealed class Clip
        {
            public int channels, rate;
            public short[] samples = new short[0];   // interleaved
            public long Frames => channels == 0 ? 0 : samples.Length / channels;
        }

        public static long SampleAt(int frame, int rate, int fps) => (long)Math.Round(frame * (double)rate / fps);

        public static Clip Read(string file)
        {
            using (var r = new BinaryReader(File.OpenRead(file), Encoding.ASCII))
            {
                long length = r.BaseStream.Length;
                if (length < 12 || Tag(r) != "RIFF") throw new InvalidDataException("Not a WAV file: " + file);
                r.ReadInt32();
                if (Tag(r) != "WAVE") throw new InvalidDataException("Not a WAV file: " + file);
                var clip = new Clip();
                bool format = false;
                while (r.BaseStream.Position + 8 <= length)
                {
                    string id = Tag(r);
                    long size = (uint)r.ReadInt32();
                    long next = r.BaseStream.Position + size + (size & 1);
                    if (id == "fmt ")
                    {
                        int kind = r.ReadInt16(); clip.channels = r.ReadInt16(); clip.rate = r.ReadInt32();
                        r.ReadInt32(); r.ReadInt16(); int bits = r.ReadInt16();
                        if (kind != 1 || bits != 16) throw new InvalidDataException("Only 16-bit PCM WAV files are supported: " + file);
                        format = true;
                    }
                    else if (id == "data")
                    {
                        int bytes = (int)Math.Min(size, length - r.BaseStream.Position) & ~1;
                        byte[] raw = r.ReadBytes(bytes);
                        clip.samples = new short[raw.Length / 2];
                        Buffer.BlockCopy(raw, 0, clip.samples, 0, clip.samples.Length * 2);
                    }
                    r.BaseStream.Position = Math.Min(next, length);
                }
                if (!format || clip.channels <= 0) throw new InvalidDataException("The WAV file has no format chunk: " + file);
                return clip;
            }
        }

        static string Tag(BinaryReader r) => Encoding.ASCII.GetString(r.ReadBytes(4));

        public static void Write(string file, Clip clip)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            using (var w = new BinaryWriter(File.Create(file), Encoding.ASCII))
            {
                int bytes = clip.samples.Length * 2;
                Header(w, clip.channels, clip.rate, bytes);
                var raw = new byte[bytes];
                Buffer.BlockCopy(clip.samples, 0, raw, 0, bytes);
                w.Write(raw);
            }
        }

        static void Header(BinaryWriter w, int channels, int rate, int bytes)
        {
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes);
            w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
            w.Write((short)1); w.Write((short)channels); w.Write(rate);
            w.Write(rate * channels * 2); w.Write((short)(channels * 2)); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(bytes);
        }

        // Join two recordings of the same tape: "before" up to tape frame splitFrame, "after" from there on.
        // Both are lined up with the tape: before's sample 0 is tape frame 0, after's sample 0 is tape
        // frame afterStartsAt. The result is totalFrames long and starts at tape frame 0.
        // A missing or different-format "before" becomes silence; the returned note says so ("" = all fine).
        public static string Splice(string before, string after, int afterStartsAt, int splitFrame, int totalFrames, int fps, string output)
        {
            var late = Read(after);
            string note = "";
            Clip early = null;
            if (string.IsNullOrEmpty(before) || !File.Exists(before)) note = "there was no sound for the earlier part, so it is silent";
            else
            {
                early = Read(before);
                if (early.channels != late.channels || early.rate != late.rate)
                {
                    note = $"the earlier sound has a different format ({early.channels} ch, {early.rate} Hz), so that part is silent";
                    early = null;
                }
            }
            int ch = late.channels;
            long total = SampleAt(totalFrames, late.rate, fps);
            long split = Math.Min(total, SampleAt(splitFrame, late.rate, fps));
            long afterZero = SampleAt(afterStartsAt, late.rate, fps);
            var joined = new Clip { channels = ch, rate = late.rate, samples = new short[total * ch] };
            if (early != null) Array.Copy(early.samples, 0, joined.samples, 0, Math.Min(split * ch, early.samples.Length));
            // From the split on, take "after" at the matching tape time.
            long from = (split - afterZero) * ch;
            if (from >= 0)
            {
                long count = Math.Min((total - split) * ch, late.samples.Length - from);
                if (count > 0) Array.Copy(late.samples, from, joined.samples, split * ch, count);
            }
            Write(output, joined);
            return note;
        }

        public static int Loudest(Clip clip, long fromFrame = 0, long toFrame = long.MaxValue)
        {
            int loudest = 0;
            long end = Math.Min(clip.samples.Length, Math.Min(toFrame, clip.Frames) * clip.channels);
            for (long i = Math.Max(0, fromFrame) * clip.channels; i < end; i++) loudest = Math.Max(loudest, Math.Abs((int)clip.samples[i]));
            return loudest;
        }

        // Writes a WAV file a piece at a time (for sound that arrives while a take is performed).
        public sealed class Writer : IDisposable
        {
            readonly BinaryWriter writer;
            public readonly int channels, rate;
            long written;   // samples (all channels)

            public long Frames => written / channels;

            public Writer(string file, int channels, int rate)
            {
                this.channels = Math.Max(1, channels); this.rate = rate;
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                writer = new BinaryWriter(File.Create(file), Encoding.ASCII);
                Header(writer, this.channels, rate, 0);
            }

            public void Silence(long sampleFrames)
            {
                long bytes = Math.Max(0, sampleFrames) * channels * 2;
                var zeros = new byte[65536];
                while (bytes > 0) { int n = (int)Math.Min(bytes, zeros.Length); writer.Write(zeros, 0, n); bytes -= n; }
                written += Math.Max(0, sampleFrames) * channels;
            }

            public void Write(float[] data, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    float v = Math.Max(-1f, Math.Min(1f, data[i]));
                    writer.Write((short)Math.Round(v * short.MaxValue));
                }
                written += count;
            }

            // Pad with silence, or cut, so the file lasts exactly this many tape frames.
            public void Complete(int frames, int fps)
            {
                long wanted = SampleAt(frames, rate, fps) * channels;
                if (wanted > written) Silence(wanted / channels - Frames);
                else if (wanted < written) { writer.Flush(); writer.BaseStream.SetLength(44 + wanted * 2); writer.BaseStream.Position = 44 + wanted * 2; }
                written = wanted;
            }

            public void Dispose()
            {
                try { writer.Flush(); writer.BaseStream.Position = 0; Header(writer, channels, rate, (int)(written * 2)); }
                finally { writer.Dispose(); }
            }
        }
    }
}
