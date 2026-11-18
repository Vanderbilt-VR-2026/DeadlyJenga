using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The cut: an ordered list of pieces of rendered shots. This writes it out as an FCPXML file and an
    // EDL, next to the rendered media, so DaVinci Resolve or Premiere can open the cut directly.
    public static class StudioExport
    {
        public sealed class Piece
        {
            public StudioCut cut;
            public StudioRender.ShotRecord shot;
            public string media = "";     // movie file, or the first image of the sequence
            public bool sequence;
            public int inFrame, frames, recordStart;
        }

        // Match each cut to its rendered shot inside a render folder.
        public static List<Piece> Resolve(StudioSet set, string renderFolder, out List<string> problems)
        {
            problems = new List<string>();
            var pieces = new List<Piece>();
            int at = 0;
            foreach (var cut in set.cuts)
            {
                var camera = set.cameras.FirstOrDefault(c => c.id == cut.camera);
                if (camera == null) { problems.Add("A piece of the cut points at a camera that no longer exists."); continue; }
                string folder = Path.Combine(renderFolder, StudioRender.ShotFolder(camera));
                string record = Path.Combine(folder, "shot.json");
                if (!File.Exists(record)) { problems.Add($"\"{camera.name}\" has not been rendered into this folder yet."); continue; }
                var shot = JsonUtility.FromJson<StudioRender.ShotRecord>(File.ReadAllText(record));
                string movie = Directory.GetFiles(folder, "*.mov").Concat(Directory.GetFiles(folder, "*.mp4")).FirstOrDefault();
                if (movie == null && !problems.Any(p => p.StartsWith("Pictures only")))
                    problems.Add("Pictures only: no movie file was rendered (that needs FFmpeg), so each shot points at its first picture. Most editors import that as one still. Import the shot folders as image sequences, or render as MP4 or ProRes.");
                if ((camera.speeds.Count > 0 || (camera.freezeAt >= camera.start && camera.freezeFor > 0)) && !problems.Any(p => p.Contains(camera.name + "\" has a speed change")))
                    problems.Add($"\"{camera.name}\" has a speed change or a freeze. The game sound under it is placed at real speed, so it will not stay in sync with the picture.");
                int first = Mathf.Clamp(Mathf.RoundToInt(cut.from * shot.fps), 0, Mathf.Max(0, shot.frames - 1));
                int count = Mathf.Clamp(Mathf.RoundToInt((cut.to - cut.from) * shot.fps), 1, shot.frames - first);
                pieces.Add(new Piece { cut = cut, shot = shot, media = movie ?? Path.Combine(folder, "00000." + (shot.format == "exr" ? "exr" : "png")),
                    sequence = movie == null, inFrame = first, frames = count, recordStart = at });
                at += count;
            }
            return pieces;
        }

        // FCPXML time: a rational number of seconds, "frames/fps" + "s" (whole seconds are written as "5s").
        static string Rational(long frames, int fps) => frames == 0 ? "0s" : frames % fps == 0 ? $"{frames / fps}s" : $"{frames}/{fps}s";

        // How long a WAV file is: sample frames and sample rate from its header. False if it is not a PCM WAV we can read.
        public static bool WavLength(string file, out long samples, out int rate, out int channels)
        {
            samples = 0; rate = 0; channels = 0;
            try
            {
                using (var r = new BinaryReader(File.OpenRead(file)))
                {
                    if (r.BaseStream.Length < 12 || new string(r.ReadChars(4)) != "RIFF") return false;
                    r.ReadInt32();
                    if (new string(r.ReadChars(4)) != "WAVE") return false;
                    int blockAlign = 0;
                    while (r.BaseStream.Position + 8 <= r.BaseStream.Length)
                    {
                        string chunk = new string(r.ReadChars(4));
                        long size = r.ReadUInt32(), body = r.BaseStream.Position;
                        if (chunk == "fmt ")
                        {
                            r.ReadInt16(); channels = r.ReadInt16(); rate = r.ReadInt32(); r.ReadInt32(); blockAlign = r.ReadInt16();
                        }
                        else if (chunk == "data")
                        {
                            // A file whose writer never went back to fill in the size: use what is really there.
                            long available = r.BaseStream.Length - body;
                            if (size == 0 || size > available) size = available;
                            if (blockAlign <= 0 || rate <= 0) return false;
                            samples = size / blockAlign;
                            return true;
                        }
                        r.BaseStream.Position = body + size + (size & 1);
                    }
                }
            }
            catch (IOException) { }
            return false;
        }

        // The dissolve into piece i, centred on the edit point: 'head' frames before it come from the outgoing
        // shot's extra rendered frames, the rest from the incoming shot's frames before its in point.
        // It is shortened when the renders do not have enough frames on either side (0 = a straight cut).
        static int Dissolve(List<Piece> pieces, int i, int fps, out int head)
        {
            head = 0;
            if (i <= 0 || i >= pieces.Count || pieces[i].cut.dissolve <= 0) return 0;
            int length = Mathf.Max(2, Mathf.RoundToInt(pieces[i].cut.dissolve * fps));
            var before = pieces[i - 1];
            int spareAfter = before.shot.frames - (before.inFrame + before.frames), spareBefore = pieces[i].inFrame;
            head = length / 2;
            int tail = length - head;
            if (head > spareBefore || tail > spareAfter)
            {
                head = tail = Mathf.Max(0, Mathf.Min(head, Mathf.Min(spareBefore, spareAfter)));
                length = head + tail;
            }
            if (length < 2) { head = 0; return 0; }
            return length;
        }

        static void CheckDissolves(List<Piece> pieces, List<string> problems)
        {
            if (pieces.Count == 0) return;
            int fps = pieces[0].shot.fps;
            for (int i = 1; i < pieces.Count; i++)
            {
                if (pieces[i].cut.dissolve <= 0) continue;
                int wanted = Mathf.Max(2, Mathf.RoundToInt(pieces[i].cut.dissolve * fps)), got = Dissolve(pieces, i, fps, out _);
                if (got < wanted)
                    problems.Add($"The dissolve into \"{pieces[i].shot.name}\" is {got} frames instead of {wanted}: a dissolve uses rendered frames from after the outgoing piece and before the incoming one, and the renders do not have enough." + (got == 0 ? " It is written as a straight cut." : ""));
            }
        }

        // Final Cut Pro's own names for its standard formats. Anything else is written by size and rate only.
        static string FormatName(int width, int height, int fps)
        {
            string size = width == 1920 && height == 1080 ? "1080" : width == 1280 && height == 720 ? "720" : width == 3840 && height == 2160 ? "3840x2160" : null;
            bool rate = fps == 24 || fps == 25 || fps == 30 || fps == 50 || fps == 60;
            return size != null && rate ? $" name=\"FFVideoFormat{size}p{fps}\"" : "";
        }
        static string Uri(string file) => new Uri(Path.GetFullPath(file)).AbsoluteUri;
        static string X(string text) => SecurityElement.Escape(text ?? "");

        // FCPXML 1.9: one sequence, shots on the main storyline, sound underneath.
        public static string WriteFcpxml(StudioSet set, string renderFolder, string audioFile, out List<string> problems)
        {
            var pieces = Resolve(set, renderFolder, out problems);
            if (pieces.Count == 0) throw new InvalidOperationException(problems.Count > 0 ? problems[0] : "The cut is empty. Add shots in the Cut tab.");
            CheckDissolves(pieces, problems);
            int fps = pieces[0].shot.fps, width = pieces[0].shot.width, height = pieces[0].shot.height;
            int total = pieces.Sum(p => p.frames);
            var xml = new StringBuilder();
            xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE fcpxml>\n<fcpxml version=\"1.9\">\n  <resources>\n");
            xml.Append($"    <format id=\"r1\"{FormatName(width, height, fps)} frameDuration=\"{Rational(1, fps)}\" width=\"{width}\" height=\"{height}\"/>\n");
            var assets = new Dictionary<string, string>();
            foreach (var piece in pieces)
            {
                if (assets.ContainsKey(piece.media)) continue;
                string id = "a" + (assets.Count + 1);
                assets[piece.media] = id;
                xml.Append($"    <asset id=\"{id}\" name=\"{X(piece.shot.name)}\" start=\"0s\" duration=\"{Rational(piece.shot.frames, fps)}\" hasVideo=\"1\" format=\"r1\">\n");
                xml.Append($"      <media-rep kind=\"original-media\" src=\"{X(Uri(piece.media))}\"/>\n    </asset>\n");
            }
            bool sound = !string.IsNullOrEmpty(audioFile) && File.Exists(audioFile);
            bool music = !string.IsNullOrEmpty(set.music) && File.Exists(set.music);
            if (sound) xml.Append(AudioAsset("s1", "Game sound", audioFile, problems));
            long soundFrames = sound && WavLength(audioFile, out long soundSamples, out int soundRate, out _) ? soundSamples * fps / soundRate : -1;
            if (music) xml.Append(AudioAsset("m1", "Music", set.music, problems));
            xml.Append("  </resources>\n  <library>\n    <event name=\"Trailer Studio\">\n");
            xml.Append($"      <project name=\"{X(set.title)}\">\n        <sequence format=\"r1\" duration=\"{Rational(total, fps)}\" tcStart=\"0s\" tcFormat=\"NDF\" audioLayout=\"stereo\" audioRate=\"48k\">\n          <spine>\n");
            for (int i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                xml.Append($"            <asset-clip ref=\"{assets[piece.media]}\" name=\"{X(piece.shot.name)}\" offset=\"{Rational(piece.recordStart, fps)}\" start=\"{Rational(piece.inFrame, fps)}\" duration=\"{Rational(piece.frames, fps)}\" format=\"r1\">\n");
                // The game sound for this piece, taken from the tape time the shot starts at (right for shots at real speed).
                if (sound)
                {
                    int tapeFrame = Mathf.RoundToInt(piece.shot.tapeStart * fps) + piece.inFrame;
                    // Never past the end of the sound file (it can be a frame shorter than the tape).
                    long heard = soundFrames >= 0 ? Math.Min(piece.frames, soundFrames - tapeFrame) : piece.frames;
                    if (heard > 0)
                        xml.Append($"              <asset-clip ref=\"s1\" lane=\"-1\" name=\"Game sound\" offset=\"{Rational(piece.inFrame, fps)}\" start=\"{Rational(tapeFrame, fps)}\" duration=\"{Rational(heard, fps)}\"/>\n");
                }
                if (music && i == 0)
                {
                    // The music runs under the whole cut, but never past the end of its own file.
                    int musicFrames = total;
                    if (WavLength(set.music, out long samples, out int rate, out _)) musicFrames = (int)Math.Min(total, samples * fps / rate);
                    xml.Append($"              <asset-clip ref=\"m1\" lane=\"-2\" name=\"Music\" offset=\"{Rational(piece.inFrame, fps)}\" start=\"0s\" duration=\"{Rational(Math.Max(1, musicFrames), fps)}\"/>\n");
                }
                xml.Append("            </asset-clip>\n");
                // A transition sits between the two clips it overlaps; the clips keep their edit point.
                int length = Dissolve(pieces, i + 1, fps, out int head);
                if (length > 0)
                    xml.Append($"            <transition name=\"Cross Dissolve\" offset=\"{Rational(pieces[i + 1].recordStart - head, fps)}\" duration=\"{Rational(length, fps)}\"/>\n");
            }
            xml.Append("          </spine>\n        </sequence>\n      </project>\n    </event>\n  </library>\n</fcpxml>\n");
            string file = Path.Combine(renderFolder, StudioRender.Safe(set.title) + ".fcpxml");
            File.WriteAllText(file, xml.ToString());
            return file;
        }

        // An audio-only asset. FCPXML wants its length; a WAV header gives it exactly. Other files are left to the editor.
        static string AudioAsset(string id, string name, string file, List<string> problems)
        {
            string length = "";
            int channels = 2;
            if (WavLength(file, out long samples, out int rate, out int found))
            {
                long whole = samples % rate == 0 ? samples / rate : -1;
                length = $" duration=\"{(whole >= 0 ? whole + "s" : samples + "/" + rate + "s")}\" audioRate=\"{rate}\"";
                channels = Mathf.Max(1, found);
            }
            else problems.Add($"\"{Path.GetFileName(file)}\" is not a WAV file the Studio can read, so its length is not written in the FCPXML; the editor reads it from the file.");
            return $"    <asset id=\"{id}\" name=\"{X(name)}\" start=\"0s\"{length} hasAudio=\"1\" audioSources=\"1\" audioChannels=\"{channels}\">\n" +
                $"      <media-rep kind=\"original-media\" src=\"{X(Uri(file))}\"/>\n    </asset>\n";
        }

        static string Timecode(int frame, int fps) =>
            $"{frame / (fps * 3600):00}:{frame / (fps * 60) % 60:00}:{frame / fps % 60:00}:{frame % fps:00}";

        // CMX 3600 EDL: the plainest edit list there is; every editor reads it.
        public static string WriteEdl(StudioSet set, string renderFolder, out List<string> problems)
        {
            var pieces = Resolve(set, renderFolder, out problems);
            if (pieces.Count == 0) throw new InvalidOperationException(problems.Count > 0 ? problems[0] : "The cut is empty. Add shots in the Cut tab.");
            CheckDissolves(pieces, problems);
            int fps = pieces[0].shot.fps;
            if (fps != 24 && fps != 25 && fps != 30)
                problems.Add($"EDL at {fps} fps: CMX 3600 timecode is defined for 24, 25 and 30 fps. Import it into a timeline set to {fps} fps, or use the FCPXML.");
            // CMX 3600. Each event line: number, reel, track, transition (C = cut, D = dissolve + length in frames),
            // source in/out, record in/out; "out" times are the first frame after the piece.
            // Rendered files have no tape reel, so the reel is AX ("auxiliary source") and the editor finds
            // the file by the "FROM CLIP NAME" comment, the way Premiere and Resolve write their own EDLs.
            const string Reel = "AX      ";
            string Line(int number, string kind, int length, int sourceIn, int sourceOut, int recordIn, int recordOut) =>
                $"{number:000}  {Reel} V     {kind,-4} {(length > 0 ? length.ToString("000") : "   ")} " +
                $"{Timecode(sourceIn, fps)} {Timecode(sourceOut, fps)} {Timecode(recordIn, fps)} {Timecode(recordOut, fps)}\r\n";
            var edl = new StringBuilder($"TITLE: {set.title}\r\nFCM: NON-DROP FRAME\r\n\r\n");
            int[] heads = new int[pieces.Count], lengths = new int[pieces.Count];
            for (int i = 1; i < pieces.Count; i++) lengths[i] = Dissolve(pieces, i, fps, out heads[i]);
            for (int i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                // A dissolve starts 'head' frames before the edit point, so the outgoing event ends there.
                int end = i + 1 < pieces.Count ? heads[i + 1] : 0;
                int sourceIn = piece.inFrame, recordIn = piece.recordStart, sourceOut = piece.inFrame + piece.frames - end, recordOut = piece.recordStart + piece.frames - end;
                if (lengths[i] == 0)
                {
                    edl.Append(Line(i + 1, "C", 0, sourceIn, sourceOut, recordIn, recordOut));
                    edl.Append($"* FROM CLIP NAME: {Path.GetFileName(piece.media)}\r\n");
                }
                else
                {
                    // A dissolve is two lines with one event number: the outgoing shot as a zero-length cut at the
                    // frame where the dissolve starts, then the incoming shot with D and the length.
                    var before = pieces[i - 1];
                    int outgoing = before.inFrame + before.frames - heads[i], start = piece.recordStart - heads[i];
                    edl.Append(Line(i + 1, "C", 0, outgoing, outgoing, start, start));
                    edl.Append(Line(i + 1, "D", lengths[i], piece.inFrame - heads[i], sourceOut, start, recordOut));
                    edl.Append($"* FROM CLIP NAME: {Path.GetFileName(before.media)}\r\n* TO CLIP NAME: {Path.GetFileName(piece.media)}\r\n");
                }
                edl.Append($"* SHOT: {piece.shot.name}\r\n\r\n");
            }
            string file = Path.Combine(renderFolder, StudioRender.Safe(set.title) + ".edl");
            File.WriteAllText(file, edl.ToString());
            return file;
        }

        // The cut's beat times, for snapping cuts to the music.
        public static float SnapToBeat(StudioSet set, float seconds)
        {
            if (set.bpm <= 0) return seconds;
            float beat = 60f / set.bpm;
            return Mathf.Max(0, Mathf.Round((seconds - set.beatOffset) / beat) * beat + set.beatOffset);
        }
    }
}
