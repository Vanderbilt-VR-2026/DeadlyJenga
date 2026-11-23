using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    public static partial class StudioAcceptance
    {
        public static void RunCutAudioOnly() => RetimedSoundTests(
            Path.Combine(StudioLibrary.ProjectRoot, "Logs", "Validation", "TrailerStudio-Cut-Audio"), true);

        static void RetimedSoundTests(string root, bool standalone = false)
        {
            const int fps = 30, rate = 48000;
            var set = new StudioSet { title = "Retimed sound", fps = fps, length = 3 };
            var slow = new StudioCameraTrack { name = "Ramp and hold", end = .4f, freezeAt = 1f / 6, freezeFor = 2f / 30 };
            slow.speeds.Add(new StudioSpeedMark { time = 0, speed = .5f });
            var next = new StudioCameraTrack { name = "Next shot", start = 2, end = 2.2f };
            set.cameras.AddRange(new[] { slow, next });
            set.cuts.Add(new StudioCut { camera = slow.id, from = 0, to = 8f / fps, audioTail = 1f / fps });
            set.cuts.Add(new StudioCut { camera = next.id, from = 0, to = 3f / fps, audioLead = 1f / fps });
            var map = new[] { 0f, .02f, .1f, 1f / 6, 1f / 6, 1f / 6, .2f, .3f, .4f };
            void Shot(StudioCameraTrack camera, int frames, float[] times)
            {
                string folder = Path.Combine(root, StudioRender.ShotFolder(camera));
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "shot.json"), JsonUtility.ToJson(new StudioRender.ShotRecord
                { name = camera.name, id = camera.id, fps = fps, width = 1280, height = 720, frames = frames,
                    tapeStart = camera.start, tapeEnd = camera.end, frameTapeTimes = times, format = "mp4" }));
                File.WriteAllBytes(Path.Combine(folder, camera.name + ".mp4"), new byte[] { 1, 2, 3 });
            }
            Shot(slow, 8, map);
            Shot(next, 6, null);
            string source = Path.Combine(root, "impulses.wav");
            var track = new StudioWave.Clip { channels = 1, rate = rate, samples = new short[3 * rate] };
            void Pulse(int sample, short amplitude)
            {
                for (int n = 0; n < 12; n++) track.samples[sample + n] = amplitude;
            }
            Pulse(4800, 12000);       // Source 0.1 s maps to cut frame 2.
            Pulse(8000, 12000);       // This impulse resumes after the two frozen frames.
            Pulse(95040, 6000);       // The audio lead precedes the second shot's picture.
            Pulse(19680, 5000);       // The audio tail continues under the second shot.
            StudioWave.Write(source, track);
            string xmlPath = StudioExport.WriteFcpxml(set, root, source, out var problems);
            var doc = new XmlDocument { XmlResolver = null };
            doc.Load(xmlPath);
            string resource = doc.SelectSingleNode("//asset[@name='Game sound']/media-rep")?.Attributes?["src"]?.Value;
            var cut = StudioWave.Read(Path.Combine(root, StudioCutSound.MixName));
            var stem = resource == null ? null : StudioWave.Read(new Uri(resource).LocalPath);
            int Peak(int start, int end)
            {
                int peak = 0;
                for (long i = StudioWave.SampleAt(start, rate, fps) * 2; i < StudioWave.SampleAt(end, rate, fps) * 2; i++)
                    peak = Math.Max(peak, Math.Abs((int)cut.samples[i]));
                return peak;
            }
            bool audio = Peak(2, 3) > 11000 && Peak(3, 5) == 0 && Peak(5, 6) > 11000 && Peak(7, 8) > 5500 && Peak(8, 9) > 4500 &&
                cut.Frames == StudioWave.SampleAt(11, rate, fps) && stem != null && stem.samples.SequenceEqual(cut.samples);
            string package = StudioExport.WritePortable(set, root, source, out _);
            doc.Load(Directory.GetFiles(package, "*.fcpxml").Single());
            bool portable = File.Exists(Path.Combine(package, "handoff.json")) && doc.SelectNodes("//media-rep").Cast<XmlNode>()
                .All(n => File.Exists(Path.Combine(package, Uri.UnescapeDataString(n.Attributes["src"].Value))));
            Shot(slow, 8, null);
            StudioExport.WriteFcpxml(set, root, source, out var legacy);
            var silent = StudioWave.Read(Path.Combine(root, StudioCutSound.MixName));
            bool old = legacy.Any(p => p.Contains("fresh render")) && silent.samples.Take(6 * rate / fps * 2).All(s => s == 0);
            Shot(slow, 8, map);
            StudioExport.WriteFcpxml(set, root, source, out _);
            bool versions = VersionsRoundTrip(set);
            bool duck = MusicDucking(root, fps, rate);
            bool pass = audio && portable && old && versions && duck && problems.Count == 0;
            string detail = $"Retime, hold and split edits: {audio}; packaged references resolve: {portable}; stale map silences the piece: {old}; saved and legacy cuts: {versions}; music duck: {duck}. " +
                $"Output peaks (frames 2, 3–4, 5, 7, 8): {Peak(2, 3)}, {Peak(3, 5)}, {Peak(5, 6)}, {Peak(7, 8)}, {Peak(8, 9)}. {string.Join(" · ", problems)}";
            if (standalone)
            {
                File.WriteAllText(Path.Combine(root, "result.txt"), $"{(pass ? "PASS" : "FAIL")} {detail}");
                if (!pass) throw new InvalidOperationException(detail);
            }
            else Result("Cut audio follows the exported ramp, hold and split edits", pass, detail);
        }

        static bool VersionsRoundTrip(StudioSet set)
        {
            set.cutAlternatives = new List<StudioCutAlternative>
            {
                new StudioCutAlternative { name = "First pass", cuts = StudioExport.CopyCuts(set.cuts) }
            };
            var reloaded = JsonUtility.FromJson<StudioSet>(JsonUtility.ToJson(set));
            var restored = StudioExport.CopyCuts(reloaded.cutAlternatives.Single().cuts);
            bool matched = reloaded.cutAlternatives[0].name == "First pass" && restored.Count == 2 &&
                restored[0].camera == set.cuts[0].camera && restored[0].audioTail == set.cuts[0].audioTail &&
                restored[1].audioLead == set.cuts[1].audioLead;
            restored[0].audioTail = 0;
            var legacy = JsonUtility.FromJson<StudioCut>("{\"camera\":\"old-camera\",\"from\":0.2,\"to\":0.8}");
            return matched && reloaded.cutAlternatives[0].cuts[0].audioTail > 0 &&
                legacy.camera == "old-camera" && legacy.audioLead == 0 && legacy.audioTail == 0;
        }

        static bool MusicDucking(string root, int fps, int rate)
        {
            var voice = new StudioWave.Clip { rate = rate, channels = 1, samples = new short[2 * rate] };
            for (int i = rate / 2; i < rate; i++) voice.samples[i] = 12000;
            var bed = new StudioWave.Clip { rate = rate, channels = 1, samples = Enumerable.Repeat((short)8000, 2 * rate).ToArray() };
            string voiceFile = Path.Combine(root, "performer.wav"), bedFile = Path.Combine(root, "bed.wav");
            StudioWave.Write(voiceFile, voice); StudioWave.Write(bedFile, bed);
            var set = new StudioSet { music = bedFile, musicDuckThresholdDb = -25 };
            var voices = new List<StudioCutSound.Performed> { new StudioCutSound.Performed { file = voiceFile } };
            var pieces = new List<StudioExport.Piece> { new StudioExport.Piece { cut = new StudioCut(), frames = fps * 2,
                shot = new StudioRender.ShotRecord { frames = fps * 2, fps = fps } } };
            var problems = new List<string>();
            bool optional = StudioCutSound.WriteDuckedMusic(set, root, voices, pieces, fps, problems) == "";
            set.musicDuckDb = 12;
            var ducked = StudioWave.Read(StudioCutSound.WriteDuckedMusic(set, root, voices, pieces, fps, problems));
            int At(float seconds) => ducked.samples[(int)(seconds * rate) * ducked.channels];
            bool signal = At(.25f) > 7950 && At(.8f) is > 1900 and < 2200 && At(1.12f) < 4000 && At(1.95f) > 7800;
            set.performerMuted = true;
            bool muted = StudioCutSound.WriteDuckedMusic(set, root, voices, pieces, fps, problems) == "";
            return optional && signal && muted && problems.Count == 0;
        }
    }
}
