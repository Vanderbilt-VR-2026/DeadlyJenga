using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Framing numbers for every rendered frame, so a shot can be fixed without looking at it:
    // for each measured target, how much of the frame it fills, where it sits (0..1, y up), whether
    // it is hidden behind something, which way a person faces; and how fast the camera moves.
    // Written next to the render as metrics.csv (one row per frame) and metrics.json (a summary).
    public sealed class StudioFraming
    {
        public sealed class Sample
        {
            public bool onScreen;
            public float area, height, x, y, top, occluded = -1, facing = -1;
        }

        sealed class Row
        {
            public int frame; public float tape, speed, turn, pitch;
            public Vector3 position;
            public readonly List<Sample> samples = new List<Sample>();
        }

        readonly StudioStage stage;
        readonly List<string> targets;
        readonly int fps;
        readonly List<Row> rows = new List<Row>();
        Vector3 lastPosition; Quaternion lastRotation; bool hasLast;

        public StudioFraming(StudioStage stage, IEnumerable<string> targets, int fps)
        {
            this.stage = stage; this.targets = targets.Where(t => !string.IsNullOrEmpty(t)).ToList(); this.fps = fps;
        }

        public void Measure(Camera camera, int frame, float tapeTime)
        {
            var row = new Row { frame = frame, tape = tapeTime, position = camera.transform.position };
            var rotation = camera.transform.rotation;
            if (hasLast)
            {
                row.speed = Vector3.Distance(row.position, lastPosition) * fps;
                row.turn = Quaternion.Angle(rotation, lastRotation) * fps;
            }
            row.pitch = Mathf.Asin(Mathf.Clamp((rotation * Vector3.forward).y, -1, 1)) * Mathf.Rad2Deg;   // up is positive
            lastPosition = row.position; lastRotation = rotation; hasLast = true;
            foreach (var target in targets) row.samples.Add(MeasureOne(camera, target, tapeTime));
            rows.Add(row);
        }

        public static Sample MeasureOne(Camera camera, string target, float tapeTime)
        {
            var stage = StudioRun.Stage;
            var sample = new Sample();
            var root = StudioTargets.Object(stage, target);
            if (!root || !root.activeInHierarchy || !StudioTargets.TryBounds(root, out var bounds)) return sample;
            // The box on screen: the 8 corners of the bounds, clipped to the frame.
            // Points on a 5x5x5 grid through the bounds; the ones in front of the lens make the box
            // (a target partly behind the camera is measured by its part in front).
            float minX = 1, minY = 1, maxX = 0, maxY = 0; bool front = false;
            for (int i = 0; i < 125; i++)
            {
                var grid = new Vector3(i % 5, i / 5 % 5, i / 25) / 2f - Vector3.one;
                var v = camera.WorldToViewportPoint(bounds.center + Vector3.Scale(bounds.extents, grid));
                if (v.z <= camera.nearClipPlane) continue;
                front = true;
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x); minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
            }
            float cx0 = Mathf.Clamp01(minX), cx1 = Mathf.Clamp01(maxX), cy0 = Mathf.Clamp01(minY), cy1 = Mathf.Clamp01(maxY);
            sample.onScreen = front && cx1 > cx0 && cy1 > cy0;
            if (!sample.onScreen) return sample;
            sample.area = (cx1 - cx0) * (cy1 - cy0);
            sample.height = cy1 - cy0;
            sample.x = (cx0 + cx1) * .5f; sample.y = (cy0 + cy1) * .5f; sample.top = maxY;

            // Hidden? Rays from the camera to nine points inside the box; a ray is blocked when it hits
            // something that is not part of the target before reaching the point.
            int tried = 0, blocked = 0;
            Vector3 eye = camera.transform.position;
            for (int i = 0; i < 9; i++)
            {
                var point = i == 8 ? bounds.center : bounds.center + Vector3.Scale(bounds.extents * .45f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var v = camera.WorldToViewportPoint(point);
                if (v.z <= 0 || v.x < 0 || v.x > 1 || v.y < 0 || v.y > 1) continue;
                tried++;
                Vector3 to = point - eye;
                float distance = to.magnitude;
                var hits = Physics.RaycastAll(eye, to / distance, distance - .05f, ~0, QueryTriggerInteraction.Ignore);
                if (hits.Any(h => !h.collider.transform.IsChildOf(root.transform) && h.distance < distance - Mathf.Max(.1f, bounds.extents.magnitude * .1f))) blocked++;
            }
            sample.occluded = tried == 0 ? -1 : blocked / (float)tried;

            // Facing: 0 = looking at the camera, 90 = side on, 180 = back to camera (people only).
            int colon = target.IndexOf(':');
            string id = colon < 0 ? target : target.Substring(colon + 1);
            var pose = StudioActorPoses.At(stage.tape, id, tapeTime, stage.Fps);
            var mark = stage.set.Mark(id);
            if (pose.found && mark != null && mark.role != StudioRole.Hand)
            {
                Vector3 forward = Vector3.ProjectOnPlane(pose.headRotation * Vector3.forward, Vector3.up);
                Vector3 toCamera = Vector3.ProjectOnPlane(eye - pose.head, Vector3.up);
                if (forward.sqrMagnitude > .0001f && toCamera.sqrMagnitude > .0001f) sample.facing = Vector3.Angle(forward, toCamera);
            }
            return sample;
        }

        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        public void Write(string folder)
        {
            var csv = new StringBuilder("frame,tape,cam_x,cam_y,cam_z,cam_speed_mps,cam_turn_dps,cam_pitch_deg");
            foreach (var t in targets) { string n = t.Replace(':', '_'); csv.Append($",{n}_on,{n}_area,{n}_height,{n}_x,{n}_y,{n}_top,{n}_occluded,{n}_facing"); }
            csv.Append('\n');
            foreach (var row in rows)
            {
                csv.Append($"{row.frame},{F(row.tape)},{F(row.position.x)},{F(row.position.y)},{F(row.position.z)},{F(row.speed)},{F(row.turn)},{F(row.pitch)}");
                foreach (var s in row.samples) csv.Append($",{(s.onScreen ? 1 : 0)},{F(s.area)},{F(s.height)},{F(s.x)},{F(s.y)},{F(s.top)},{F(s.occluded)},{F(s.facing)}");
                csv.Append('\n');
            }
            File.WriteAllText(Path.Combine(folder, "metrics.csv"), csv.ToString());

            // Summary: per target, median and 10th/90th percentile of each number over the frames it is on screen.
            var json = new StringBuilder("{\n");
            json.Append($"  \"frames\": {rows.Count},\n  \"fps\": {fps},\n");
            json.Append($"  \"camera_speed_mps\": {Stats(rows.Select(r => r.speed))},\n  \"camera_turn_dps\": {Stats(rows.Skip(1).Select(r => r.turn))},\n");
            json.Append("  \"targets\": {\n");
            for (int i = 0; i < targets.Count; i++)
            {
                var on = rows.Select(r => r.samples[i]).Where(s => s.onScreen).ToList();
                json.Append($"    \"{targets[i]}\": {{ \"on_screen\": {F(rows.Count == 0 ? 0 : on.Count / (float)rows.Count)}, " +
                    $"\"area\": {Stats(on.Select(s => s.area))}, \"height\": {Stats(on.Select(s => s.height))}, \"x\": {Stats(on.Select(s => s.x))}, \"y\": {Stats(on.Select(s => s.y))}, " +
                    $"\"occluded\": {Stats(on.Where(s => s.occluded >= 0).Select(s => s.occluded))}, \"facing\": {Stats(on.Where(s => s.facing >= 0).Select(s => s.facing))} }}{(i < targets.Count - 1 ? "," : "")}\n");
            }
            json.Append("  }\n}\n");
            File.WriteAllText(Path.Combine(folder, "metrics.json"), json.ToString());
        }

        static string Stats(IEnumerable<float> values)
        {
            var list = values.OrderBy(v => v).ToList();
            if (list.Count == 0) return "null";
            float P(float q) => list[Mathf.Clamp(Mathf.RoundToInt(q * (list.Count - 1)), 0, list.Count - 1)];
            return $"{{ \"p10\": {F(P(.1f))}, \"median\": {F(P(.5f))}, \"p90\": {F(P(.9f))}, \"max\": {F(list[list.Count - 1])} }}";
        }

        // ---- Contact sheet ----

        // 'count' frames spread evenly over the shot, in one PNG (4 across), with filmstrip.txt naming them.
        public static string Filmstrip(string folder, int frames, int fps, float tapeStart, int count)
        {
            if (count <= 0 || frames <= 0) return "";
            count = Mathf.Min(count, frames);
            int across = Mathf.Min(4, count), down = Mathf.CeilToInt(count / (float)across);
            Texture2D sheet = null, picture = new Texture2D(2, 2);
            var notes = new StringBuilder("Tiles left to right, top to bottom:\n");
            try
            {
                int tileW = 0, tileH = 0;
                for (int i = 0; i < count; i++)
                {
                    int frame = count == 1 ? 0 : Mathf.RoundToInt(i * (frames - 1) / (float)(count - 1));
                    string file = Path.Combine(folder, $"{frame:D05}.png");
                    if (!File.Exists(file) || !picture.LoadImage(File.ReadAllBytes(file))) continue;
                    if (sheet == null)
                    {
                        tileW = Mathf.Min(480, picture.width); tileH = Mathf.RoundToInt(tileW * picture.height / (float)picture.width);
                        sheet = new Texture2D(across * tileW + (across + 1) * 4, down * tileH + (down + 1) * 4, TextureFormat.RGB24, false);
                        var fill = new Color32[sheet.width * sheet.height];
                        for (int p = 0; p < fill.Length; p++) fill[p] = new Color32(20, 20, 24, 255);
                        sheet.SetPixels32(fill);
                    }
                    int col = i % across, rowIndex = i / across;
                    int ox = 4 + col * (tileW + 4), oy = sheet.height - (rowIndex + 1) * (tileH + 4);
                    for (int y = 0; y < tileH; y++)
                        for (int x = 0; x < tileW; x++)
                            sheet.SetPixel(ox + x, oy + y, picture.GetPixelBilinear((x + .5f) / tileW, (y + .5f) / tileH));
                    notes.Append($"{i + 1}: frame {frame}, shot {frame / (float)fps:0.00} s, tape {tapeStart + frame / (float)fps:0.00} s\n");
                }
                if (sheet == null) return "";
                sheet.Apply(false);
                string path = Path.Combine(folder, "filmstrip.png");
                File.WriteAllBytes(path, sheet.EncodeToPNG());
                File.WriteAllText(Path.Combine(folder, "filmstrip.txt"), notes.ToString());
                return path;
            }
            finally { Object.DestroyImmediate(picture); if (sheet) Object.DestroyImmediate(sheet); }
        }
    }
}
