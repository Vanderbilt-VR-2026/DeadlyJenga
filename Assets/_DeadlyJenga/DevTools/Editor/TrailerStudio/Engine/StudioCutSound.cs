using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

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

        public static bool NeedsProcessedAudio(StudioSet set, List<StudioExport.Piece> pieces) =>
            set.worldGainDb != 0 || set.performerGainDb != 0 || set.worldMuted || set.performerMuted ||
            pieces.Any(p => p.missingFrameMap || p.shot.frameTapeTimes?.Length == p.shot.frames + 1 ||
                p.cut.audioLead > 0 || p.cut.audioTail > 0);

        public sealed class FinishingTrack
        {
            public string name, file;
        }

        // Timeline-aligned stems carry the results of retimes and split edits into other editors.
        public static List<FinishingTrack> WriteFinishingTracks(StudioSet set, string folder, string worldFile,
            List<Performed> sounds, List<StudioExport.Piece> pieces, int fps, List<string> problems)
        {
            var tracks = new List<FinishingTrack>();
            void Write(string name, string file, string source, float gain)
            {
                var clip = Mix(pieces, fps, new[] { source }, gain, Array.Empty<string>(), 0, "", 0, problems);
                if (clip == null) return;
                string path = Path.Combine(folder, file);
                StudioWave.Write(path, clip);
                tracks.Add(new FinishingTrack { name = name, file = path });
            }

            if (!set.worldMuted) Write("Game sound", "cut-world.wav", worldFile, set.worldGainDb);
            if (!set.performerMuted)
                foreach (var sound in sounds)
                    Write(Name(sound), "cut-performer-" + StudioRender.Safe(sound.takeId) + ".wav", sound.file, set.performerGainDb);
            return tracks;
        }

        // The handoff WAV continues to be the combined game and performer sound, without the music bed.
        public static string WriteCutSound(StudioSet set, string renderFolder, string worldFile, List<Performed> sounds,
            List<StudioExport.Piece> pieces, int fps, List<string> problems)
        {
            var cut = BuildMix(set, worldFile, sounds, pieces, fps, null, problems, false);
            if (cut == null) return "";
            string output = Path.Combine(renderFolder, MixName);
            StudioWave.Write(output, cut);
            return output;
        }

        public static StudioWave.Clip BuildMix(StudioSet set, string worldFile, List<Performed> sounds,
            List<StudioExport.Piece> pieces, int fps, string audition, List<string> problems, bool includeMusic = true)
        {
            bool AllOr(string bus) => audition == null || audition == bus;
            var world = AllOr("World") && (!set.worldMuted || audition != null) ? new[] { worldFile } : Array.Empty<string>();
            var performers = AllOr("Performers") && (!set.performerMuted || audition != null)
                ? sounds.Select(s => s.file) : Array.Empty<string>();
            string music = includeMusic && AllOr("Music") && (!set.musicMuted || audition != null) ? set.music : "";
            float duck = audition == null ? set.musicDuckDb : 0;
            return Mix(pieces, fps, world, set.worldGainDb, performers, set.performerGainDb, music, set.musicGainDb,
                problems, duck, set.musicDuckThresholdDb);
        }

        public static string WriteDuckedMusic(StudioSet set, string folder, List<Performed> sounds,
            List<StudioExport.Piece> pieces, int fps, List<string> problems)
        {
            if (set.musicMuted || set.performerMuted || set.musicDuckDb <= 0 || sounds.Count == 0) return "";
            var clip = Mix(pieces, fps, Array.Empty<string>(), 0, sounds.Select(s => s.file), set.performerGainDb,
                set.music, set.musicGainDb, problems, set.musicDuckDb, set.musicDuckThresholdDb, true);
            if (clip == null) return "";
            string file = Path.Combine(folder, "cut-music.wav");
            StudioWave.Write(file, clip);
            return file;
        }

        public static string Measure(StudioWave.Clip clip)
        {
            int peak = 0;
            foreach (short sample in clip.samples) peak = Math.Max(peak, Math.Abs((int)sample));
            string samplePeak = peak == 0 ? "silence" : $"{20 * Math.Log10(peak / 32768.0):0.0} dBFS";
            string encoder = StudioRender.FindEncoder();
            if (peak == 0 || string.IsNullOrEmpty(encoder)) return $"Sample peak {samplePeak}. Integrated loudness and true peak unavailable.";

            string path = Path.Combine(Path.GetTempPath(), "trailer-studio-meter-" + Guid.NewGuid().ToString("N") + ".wav");
            try
            {
                StudioWave.Write(path, clip);
                var start = new ProcessStartInfo(encoder)
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true,
                    Arguments = $"-hide_banner -nostdin -i \"{path}\" -af loudnorm=print_format=json -f null -"
                };
                using (var process = Process.Start(start))
                {
                    if (process == null) throw new InvalidOperationException("FFmpeg did not start.");
                    var output = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(8000)) { process.Kill(); throw new TimeoutException("FFmpeg metering timed out."); }
                    string report = output.GetAwaiter().GetResult();
                    string Metric(string name) => Regex.Match(report, "\"" + name + "\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
                    string lufs = Metric("input_i"), truePeak = Metric("input_tp");
                    if (process.ExitCode != 0 || string.IsNullOrEmpty(lufs) || string.IsNullOrEmpty(truePeak))
                        throw new InvalidOperationException("FFmpeg returned no loudness result.");
                    return $"Integrated {lufs} LUFS · true peak {truePeak} dBTP · sample peak {samplePeak}.";
                }
            }
            catch (Exception problem) { return $"Sample peak {samplePeak}. {problem.Message}"; }
            finally { try { File.Delete(path); } catch (IOException) { } }
        }

        static StudioWave.Clip Mix(List<StudioExport.Piece> pieces, int fps, IEnumerable<string> world, float worldDb,
            IEnumerable<string> performers, float performerDb, string music, float musicDb, List<string> problems,
            float duckDb = 0, float duckThresholdDb = -30, bool bedOnly = false)
        {
            const int rate = 48000, channels = 2;
            var game = Read(world, worldDb, problems).ToArray();
            var voices = Read(performers, performerDb, problems).ToArray();
            var bed = Read(new[] { music }, musicDb, problems, true).ToArray();
            if (pieces.Count == 0 || game.Length + voices.Length + bed.Length == 0 || bedOnly && bed.Length == 0) return null;

            int total = pieces.Sum(p => p.frames);
            var sum = new float[StudioWave.SampleAt(total, rate, fps) * channels];
            bool duck = duckDb > 0 && voices.Length > 0 && bed.Length > 0;
            var activity = duck ? new float[sum.Length / channels] : null;
            foreach (var piece in pieces)
            {
                if (piece.missingFrameMap || game.Length + voices.Length == 0) continue;
                int lead = Mathf.RoundToInt(Mathf.Max(0, piece.cut.audioLead) * fps);
                int tail = Mathf.RoundToInt(Mathf.Max(0, piece.cut.audioTail) * fps);
                int begin = Math.Max(0, piece.recordStart - lead);
                int end = Math.Min(total, piece.recordStart + piece.frames + tail);
                for (int frame = begin; frame < end; frame++)
                {
                    int sourceFrame = piece.inFrame + frame - piece.recordStart;
                    float source = piece.TapeTime(sourceFrame), next = piece.TapeTime(sourceFrame + 1);
                    // A freeze holds one image. Holding one audio sample would add DC.
                    if (next - source < .000001f) continue;
                    long first = StudioWave.SampleAt(frame, rate, fps), last = StudioWave.SampleAt(frame + 1, rate, fps);
                    for (long i = first; i < last; i++)
                    {
                        double time = source + (next - source) * ((double)(i - first) / (last - first));
                        for (int channel = 0; channel < channels; channel++)
                        {
                            if (!bedOnly)
                                foreach (var (clip, gain) in game)
                                    sum[i * channels + channel] += Sample(clip, time, channel) * gain;
                            float voice = 0;
                            foreach (var (clip, gain) in voices) voice += Sample(clip, time, channel) * gain;
                            if (!bedOnly) sum[i * channels + channel] += voice;
                            if (duck) activity[i] = Math.Max(activity[i], Math.Abs(voice));
                        }
                    }
                }
            }
            foreach (var (clip, gain) in bed)
            {
                long length = Math.Min(sum.LongLength / channels, (long)Math.Ceiling(clip.Frames * (double)rate / clip.rate));
                float threshold = Mathf.Pow(10, Mathf.Clamp(duckThresholdDb, -60, 0) / 20);
                float depth = Mathf.Pow(10, -Mathf.Clamp(duckDb, 0, 24) / 20);
                float envelope = 1;
                long heldUntil = 0;
                for (long i = 0; i < length; i++)
                {
                    if (duck && activity[i] >= threshold) heldUntil = i + rate / 10;
                    bool active = i < heldUntil;
                    envelope = Mathf.LerpUnclamped(envelope, active ? depth : 1, active ? 1f / (rate * .015f) : 1f / (rate * .2f));
                    for (int channel = 0; channel < channels; channel++)
                        sum[i * channels + channel] += Sample(clip, i / (double)rate, channel) * gain * envelope;
                }
            }

            float peak = 0;
            foreach (float value in sum) peak = Math.Max(peak, Math.Abs(value));
            float master = peak > .99f ? .99f / peak : 1;
            if (master < 1) problems.Add($"The audio mix was turned down by {20 * Math.Log10(master):0.0} dB to avoid clipping.");
            var samples = new short[sum.Length];
            for (int i = 0; i < samples.Length; i++) samples[i] = (short)Mathf.RoundToInt(sum[i] * master * short.MaxValue);
            return new StudioWave.Clip { rate = rate, channels = channels, samples = samples };
        }

        static IEnumerable<(StudioWave.Clip clip, float gain)> Read(IEnumerable<string> files, float db, List<string> problems, bool decode = false)
        {
            float gain = Mathf.Pow(10, Mathf.Clamp(db, -60, 12) / 20);
            foreach (string file in files)
            {
                if (string.IsNullOrEmpty(file) || !File.Exists(file)) continue;
                StudioWave.Clip clip;
                try { clip = decode ? ReadMusic(file) : StudioWave.Read(file); }
                catch (Exception error)
                {
                    problems.Add($"{Path.GetFileName(file)} could not be mixed as PCM audio: {error.Message}");
                    continue;
                }
                if (clip.rate > 0 && clip.Frames > 0) yield return (clip, gain);
            }
        }

        static StudioWave.Clip ReadMusic(string file)
        {
            try { return StudioWave.Read(file); }
            catch (InvalidDataException)
            {
                string encoder = StudioRender.FindEncoder();
                if (string.IsNullOrEmpty(encoder)) throw;
                string path = Path.Combine(Path.GetTempPath(), "trailer-studio-music-" + Guid.NewGuid().ToString("N") + ".wav");
                try
                {
                    var start = new ProcessStartInfo(encoder)
                    {
                        UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true,
                        Arguments = $"-hide_banner -loglevel error -nostdin -i \"{file}\" -ac 2 -ar 48000 -c:a pcm_s16le \"{path}\""
                    };
                    using (var process = Process.Start(start))
                    {
                        if (process == null) throw new InvalidOperationException("FFmpeg did not start.");
                        var stderr = process.StandardError.ReadToEndAsync();
                        if (!process.WaitForExit(8000)) { process.Kill(); throw new TimeoutException("FFmpeg audio decode timed out."); }
                        if (process.ExitCode != 0) throw new InvalidDataException(stderr.GetAwaiter().GetResult());
                        return StudioWave.Read(path);
                    }
                }
                finally { try { File.Delete(path); } catch (IOException) { } }
            }
        }

        static float Sample(StudioWave.Clip clip, double seconds, int channel)
        {
            double frame = seconds * clip.rate;
            if (frame < 0 || frame >= clip.Frames) return 0;
            long first = (long)frame, next = Math.Min(first + 1, clip.Frames - 1);
            channel %= clip.channels;
            return Mathf.LerpUnclamped(clip.samples[first * clip.channels + channel],
                clip.samples[next * clip.channels + channel], (float)(frame - first)) / 32768f;
        }
    }
}
