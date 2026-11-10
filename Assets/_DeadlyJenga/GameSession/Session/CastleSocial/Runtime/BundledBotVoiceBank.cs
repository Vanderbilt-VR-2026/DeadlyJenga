using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeadlyJenga.CastleSocial
{
    internal sealed class BundledBotVoiceBank
    {
        internal const int SampleRate = 16000;
        internal const int VoiceCount = 8;

        internal readonly struct Clip
        {
            readonly byte[] pcm;
            readonly int start;
            internal int Count { get; }

            internal Clip(byte[] pcm, int start, int count)
            {
                this.pcm = pcm;
                this.start = start;
                Count = count;
            }

            internal float[] Read(int offset, int count)
            {
                var samples = new float[count];
                CopyTo(samples, 0, offset, count);
                return samples;
            }

            internal void CopyTo(float[] samples, int destination, int offset, int count)
            {
                int index = start + offset * 2;
                for (int i = 0; i < count; i++, index += 2)
                    samples[destination + i] = unchecked((short)(pcm[index] | pcm[index + 1] << 8)) / 32768f;
            }
        }

        readonly Dictionary<string, Clip[]> lines = new Dictionary<string, Clip[]>(StringComparer.Ordinal);

        static readonly Dictionary<string, BundledBotVoiceBank> loaded = new(StringComparer.Ordinal);

        internal static BundledBotVoiceBank Load(string bankName = "BundledBotVoices")
        {
            if (loaded.TryGetValue(bankName, out var bank)) return bank;
            var asset = Resources.Load<TextAsset>("CastleSocial/" + bankName);
            if (!asset) throw new InvalidDataException("Bundled bot voice bank is missing.");
            try { return loaded[bankName] = new BundledBotVoiceBank(asset.bytes); }
            finally { Resources.UnloadAsset(asset); }
        }

        BundledBotVoiceBank(byte[] pcm)
        {
            using var input = new BinaryReader(new MemoryStream(pcm, false), Encoding.UTF8);
            if (input.ReadUInt32() != 0x31424a44 || input.ReadUInt16() != VoiceCount)
                throw new InvalidDataException("Unsupported bot voice bank.");
            int lineCount = input.ReadUInt16();
            if (lineCount < 1 || lineCount > 128) throw new InvalidDataException("Invalid voice line count.");
            for (int i = 0; i < lineCount; i++)
            {
                int length = input.ReadUInt16();
                if (length < 1 || length > 960) throw new InvalidDataException("Invalid voice line text.");
                var key = Encoding.UTF8.GetString(input.ReadBytes(length));
                var takes = new Clip[VoiceCount];
                for (int voice = 0; voice < VoiceCount; voice++)
                {
                    int count = input.ReadInt32();
                    long start = input.BaseStream.Position;
                    if (count < 1 || count > SampleRate * 6 || start + count * 2L > pcm.Length)
                        throw new InvalidDataException("Invalid bundled voice clip length.");
                    takes[voice] = new Clip(pcm, (int)start, count);
                    input.BaseStream.Position += count * 2;
                }
                if (!lines.TryAdd(key, takes)) throw new InvalidDataException("Duplicate bundled voice line.");
            }
            if (input.BaseStream.Position != pcm.Length) throw new InvalidDataException("Trailing voice bank data.");
        }

        internal static int VoiceFor(string identity)
        {
            // SocialPopulation assigns an ordinal to each identity. The first eight
            // bots get distinct voices and keep them across respawns and config reloads.
            int separator = identity.LastIndexOf(':');
            return int.TryParse(identity.AsSpan(separator + 1), out int ordinal) && ordinal >= 0
                ? ordinal % VoiceCount : 0;
        }

        internal bool TryRenderLine(string identity, string text, out float[] samples)
        {
            samples = null;
            if (TryGet(identity, text, out var whole))
            {
                samples = whole.Read(0, whole.Count);
                return true;
            }

            // Match exact spoken phrases. A missing phrase keeps the whole
            // line on the live synthesizer instead of changing the dialogue.
            int split = -1;
            for (int i = 0; i + 1 < text.Length; i++)
            {
                if (text[i + 1] == ' ' && (text[i] == '.' || text[i] == '?' || text[i] == '!'))
                { split = i; break; }
            }
            if (split < 0 || !TryGet(identity, text.Substring(0, split + 1), out var first) ||
                !TryGet(identity, text.Substring(split + 2), out var second)) return false;

            const int pauseSamples = SampleRate / 20;
            samples = new float[first.Count + pauseSamples + second.Count];
            first.CopyTo(samples, 0, 0, first.Count);
            second.CopyTo(samples, first.Count + pauseSamples, 0, second.Count);
            return true;
        }

        internal bool TryGet(string identity, string text, out Clip clip)
        {
            if (lines.TryGetValue(text, out var takes))
            {
                clip = takes[VoiceFor(identity)];
                return true;
            }
            clip = default;
            return false;
        }
    }
}
