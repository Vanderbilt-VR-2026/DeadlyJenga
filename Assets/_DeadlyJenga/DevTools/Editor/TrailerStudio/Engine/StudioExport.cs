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

        static string Rational(int frames, int fps) => frames == 0 ? "0s" : $"{frames}/{fps}s";
        static string Uri(string file) => new Uri(Path.GetFullPath(file)).AbsoluteUri;
        static string X(string text) => SecurityElement.Escape(text ?? "");

        // FCPXML 1.9: one sequence, shots on the main storyline, sound underneath.
        public static string WriteFcpxml(StudioSet set, string renderFolder, string audioFile, out List<string> problems)
        {
            var pieces = Resolve(set, renderFolder, out problems);
            if (pieces.Count == 0) throw new InvalidOperationException(problems.Count > 0 ? problems[0] : "The cut is empty. Add shots in the Cut tab.");
            int fps = pieces[0].shot.fps, width = pieces[0].shot.width, height = pieces[0].shot.height;
            int total = pieces.Sum(p => p.frames);
            var xml = new StringBuilder();
            xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE fcpxml>\n<fcpxml version=\"1.9\">\n  <resources>\n");
            xml.Append($"    <format id=\"r1\" name=\"FFVideoFormat{height}p{fps}\" frameDuration=\"1/{fps}s\" width=\"{width}\" height=\"{height}\"/>\n");
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
            if (sound) xml.Append($"    <asset id=\"s1\" name=\"Game sound\" start=\"0s\" hasAudio=\"1\" audioSources=\"1\" audioChannels=\"2\">\n      <media-rep kind=\"original-media\" src=\"{X(Uri(audioFile))}\"/>\n    </asset>\n");
            if (music) xml.Append($"    <asset id=\"m1\" name=\"Music\" start=\"0s\" hasAudio=\"1\" audioSources=\"1\" audioChannels=\"2\">\n      <media-rep kind=\"original-media\" src=\"{X(Uri(set.music))}\"/>\n    </asset>\n");
            xml.Append("  </resources>\n  <library>\n    <event name=\"Trailer Studio\">\n");
            xml.Append($"      <project name=\"{X(set.title)}\">\n        <sequence format=\"r1\" duration=\"{Rational(total, fps)}\" tcStart=\"0s\" tcFormat=\"NDF\">\n          <spine>\n");
            for (int i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                xml.Append($"            <asset-clip ref=\"{assets[piece.media]}\" name=\"{X(piece.shot.name)}\" offset=\"{Rational(piece.recordStart, fps)}\" start=\"{Rational(piece.inFrame, fps)}\" duration=\"{Rational(piece.frames, fps)}\" format=\"r1\">\n");
                // The game sound for this piece, taken from the tape time the shot starts at (right for shots at real speed).
                if (sound)
                {
                    int tapeFrame = Mathf.RoundToInt(piece.shot.tapeStart * fps) + piece.inFrame;
                    xml.Append($"              <asset-clip ref=\"s1\" lane=\"-1\" name=\"Game sound\" offset=\"{Rational(piece.inFrame, fps)}\" start=\"{Rational(tapeFrame, fps)}\" duration=\"{Rational(piece.frames, fps)}\"/>\n");
                }
                if (music && i == 0)
                    xml.Append($"              <asset-clip ref=\"m1\" lane=\"-2\" name=\"Music\" offset=\"{Rational(piece.inFrame, fps)}\" start=\"0s\" duration=\"{Rational(total, fps)}\"/>\n");
                xml.Append("            </asset-clip>\n");
                if (i + 1 < pieces.Count && pieces[i + 1].cut.dissolve > 0)
                {
                    int length = Mathf.Max(2, Mathf.RoundToInt(pieces[i + 1].cut.dissolve * fps));
                    xml.Append($"            <transition name=\"Cross Dissolve\" offset=\"{Rational(pieces[i + 1].recordStart - length / 2, fps)}\" duration=\"{Rational(length, fps)}\"/>\n");
                }
            }
            xml.Append("          </spine>\n        </sequence>\n      </project>\n    </event>\n  </library>\n</fcpxml>\n");
            string file = Path.Combine(renderFolder, StudioRender.Safe(set.title) + ".fcpxml");
            File.WriteAllText(file, xml.ToString());
            return file;
        }

        static string Timecode(int frame, int fps) =>
            $"{frame / (fps * 3600):00}:{frame / (fps * 60) % 60:00}:{frame / fps % 60:00}:{frame % fps:00}";

        // CMX 3600 EDL: the plainest edit list there is; every editor reads it.
        public static string WriteEdl(StudioSet set, string renderFolder, out List<string> problems)
        {
            var pieces = Resolve(set, renderFolder, out problems);
            if (pieces.Count == 0) throw new InvalidOperationException(problems.Count > 0 ? problems[0] : "The cut is empty. Add shots in the Cut tab.");
            int fps = pieces[0].shot.fps;
            var edl = new StringBuilder($"TITLE: {set.title}\nFCM: NON-DROP FRAME\n\n");
            for (int i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                string reel = ("S" + (i + 1).ToString("000")).PadRight(8);
                bool dissolve = piece.cut.dissolve > 0 && i > 0;
                string transition = dissolve ? "D    " + Mathf.RoundToInt(piece.cut.dissolve * fps).ToString("000") : "C        ";
                edl.Append($"{i + 1:000}  {reel} V     {transition} {Timecode(piece.inFrame, fps)} {Timecode(piece.inFrame + piece.frames, fps)} {Timecode(piece.recordStart, fps)} {Timecode(piece.recordStart + piece.frames, fps)}\n");
                edl.Append($"* FROM CLIP NAME: {Path.GetFileName(piece.media)}\n* SHOT: {piece.shot.name}\n\n");
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
