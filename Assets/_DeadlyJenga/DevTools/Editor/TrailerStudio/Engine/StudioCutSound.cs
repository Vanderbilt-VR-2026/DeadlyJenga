using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The cut's sound beyond the world take's: what each performer heard. The FCPXML export puts every
    // performed take that is heard on a lane of its own under the shots, and cut-sound.wav mixes the world
    // sound and all of them into one file laid out like the cut.
    public static class StudioCutSound
    {
        public sealed class Performed
        {
            public string actor = "", take = "", takeId = "", file = "";
            public long sampleFrames;
            public int rate;
        }

        public const string MixName = "cut-sound.wav";

        // The active take of every actor that is heard (not muted; soloed if any track is soloed) and has sound.
        public static List<Performed> PerformedSounds(StudioSet set)
        {
            var list = new List<Performed>();
            var tape = TapeOf(set);
            if (tape == null) return list;
            bool anySolo = tape.tracks.Any(t => t.solo);
            foreach (var track in tape.tracks)
            {
                if (track.type != SurvivorTrack.Id && track.type != HandTrack.Id) continue;
                if (track.mode == TrackMode.Muted || anySolo && !track.solo) continue;
                var take = track.ActiveTake;
                if (take == null) continue;
                string file = StudioRender.PerformedAudioFile(set.id, take.id);
                if (!File.Exists(file)) continue;
                var sound = new Performed { actor = set.Mark(track.id)?.actor ?? track.name ?? "Actor", take = take.name, takeId = take.id, file = file };
                try { var clip = StudioWave.Read(file); sound.sampleFrames = clip.Frames; sound.rate = clip.rate; }
                catch (Exception error) { Debug.LogWarning("[TrailerStudio] A performed sound could not be read: " + error.Message); continue; }
                list.Add(sound);
            }
            return list;
        }

        static StudioTape TapeOf(StudioSet set)
        {
            var stage = StudioRun.Stage;
            if (stage != null && stage.set != null && stage.set.id == set.id) return stage.tape;
            string file = StudioLibrary.TapePath(set.id);
            try { return StudioTape.IsTape(file) ? StudioTape.Load(file, new[] { SurvivorTrack.Id, HandTrack.Id }) : null; }
            catch (Exception error) { Debug.LogWarning("[TrailerStudio] The tape could not be read for its performed sounds: " + error.Message); return null; }
        }

        static string Rational(long value, int per) => value == 0 ? "0s" : $"{value}/{per}s";
        static string X(string text) => SecurityElement.Escape(text ?? "");
        static string Uri(string file) => new Uri(Path.GetFullPath(file)).AbsoluteUri;
        static string Name(Performed sound) => $"Performed: {sound.actor} ({sound.take})";

        // FCPXML resources: one audio asset per performed sound, ids p1, p2...
        public static void AppendAssets(StringBuilder xml, List<Performed> sounds)
        {
            for (int i = 0; i < sounds.Count; i++)
                xml.Append($"    <asset id=\"p{i + 1}\" name=\"{X(Name(sounds[i]))}\" start=\"0s\" duration=\"{Rational(sounds[i].sampleFrames, sounds[i].rate)}\" hasAudio=\"1\" audioSources=\"1\" audioChannels=\"2\">\n" +
                    $"      <media-rep kind=\"original-media\" src=\"{X(Uri(sounds[i].file))}\"/>\n    </asset>\n");
        }

        // Under one shot: each performed sound on its own lane (-3, -4...; the game sound is -1 and music -2),
        // taken from the same tape time as the game sound.
        public static void AppendClips(StringBuilder xml, List<Performed> sounds, StudioExport.Piece piece, int fps)
        {
            int tapeFrame = Mathf.RoundToInt(piece.shot.tapeStart * fps) + piece.inFrame;
            for (int i = 0; i < sounds.Count; i++)
                xml.Append($"              <asset-clip ref=\"p{i + 1}\" lane=\"{-3 - i}\" name=\"{X(Name(sounds[i]))}\" offset=\"{Rational(piece.inFrame, fps)}\" start=\"{Rational(tapeFrame, fps)}\" duration=\"{Rational(piece.frames, fps)}\"/>\n");
        }

        // One file with everything: the world take's sound plus every performed sound, summed at tape time,
        // turned down as a whole if the sum would clip, then laid out piece by piece like the cut (hard cuts).
        // Returns the file, or "" when there is no sound at all.
        public static string WriteCutSound(StudioSet set, string renderFolder, string worldFile, List<Performed> sounds, List<StudioExport.Piece> pieces, int fps, List<string> problems)
        {
            var sources = new List<StudioWave.Clip>();
            var files = new List<string>();
            if (!string.IsNullOrEmpty(worldFile) && File.Exists(worldFile)) files.Add(worldFile);
            files.AddRange(sounds.Select(s => s.file));
            foreach (string file in files)
            {
                try
                {
                    var clip = StudioWave.Read(file);
                    if (sources.Count > 0 && clip.rate != sources[0].rate) { problems.Add($"{Path.GetFileName(file)} has a different sample rate ({clip.rate} Hz), so it is not in {MixName}."); continue; }
                    sources.Add(clip);
                }
                catch (Exception error) { problems.Add($"{Path.GetFileName(file)} could not be read for {MixName}: {error.Message}"); }
            }
            if (sources.Count == 0 || pieces.Count == 0) return "";
            int rate = sources[0].rate, channels = sources[0].channels;
            long length = sources.Max(c => c.Frames);
            var sum = new float[length * channels];
            foreach (var clip in sources)
                for (long f = 0; f < clip.Frames; f++)
                    for (int c = 0; c < channels; c++)
                        sum[f * channels + c] += clip.samples[f * clip.channels + c % clip.channels] / 32768f;
            float peak = 0;
            foreach (float v in sum) peak = Math.Max(peak, Math.Abs(v));
            float gain = peak > .99f ? .99f / peak : 1;
            if (gain < 1) problems.Add($"{MixName} was turned down by {20 * Math.Log10(gain):0.0} dB so the sum does not clip.");

            int total = pieces.Sum(p => p.frames);
            var cut = new StudioWave.Clip { channels = channels, rate = rate, samples = new short[StudioWave.SampleAt(total, rate, fps) * channels] };
            foreach (var piece in pieces)
            {
                int tapeFrame = Mathf.RoundToInt(piece.shot.tapeStart * fps) + piece.inFrame;
                long to = StudioWave.SampleAt(piece.recordStart, rate, fps), end = StudioWave.SampleAt(piece.recordStart + piece.frames, rate, fps);
                long from = StudioWave.SampleAt(tapeFrame, rate, fps);
                for (long n = 0; n < end - to; n++)
                {
                    if (from + n >= length) break;
                    for (int c = 0; c < channels; c++)
                        cut.samples[(to + n) * channels + c] = (short)Mathf.Clamp(Mathf.RoundToInt(sum[(from + n) * channels + c] * gain * short.MaxValue), short.MinValue, short.MaxValue);
                }
            }
            string output = Path.Combine(renderFolder, MixName);
            StudioWave.Write(output, cut);
            return output;
        }
    }
}
