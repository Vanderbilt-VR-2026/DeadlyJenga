using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The bottom of the window: one time ruler shared by every track, like a music multitrack.
    // Takes are colored bars, cues are diamonds. Click or drag the ruler (or any lane) to scrub.
    public sealed class StudioTimeline
    {
        const float NameWidth = 150;
        readonly StudioContext context;
        readonly VisualElement host, ruler, playhead;
        readonly ScrollView rows;
        StudioCue draggingCue;
        bool cueMoved;

        public StudioTimeline(StudioContext context, VisualElement host)
        {
            this.context = context; this.host = host;
            ruler = new VisualElement();
            ruler.AddToClassList("ts-ruler");
            ruler.generateVisualContent += DrawRuler;
            Scrubbable(ruler);
            rows = new ScrollView();
            rows.AddToClassList("ts-scroll");
            playhead = new VisualElement { pickingMode = PickingMode.Ignore };
            playhead.AddToClassList("ts-playhead");
            host.Add(ruler); host.Add(rows); host.Add(playhead);
            host.RegisterCallback<GeometryChangedEvent>(_ => MovePlayhead());
        }

        int Frames => Mathf.Max(1, context.Set?.Frames ?? 1);
        float LaneWidth => ruler.resolvedStyle.width;
        float X(float frame) => FrameToX(frame, Frames, LaneWidth);
        int FrameAt(float x) => XToFrame(x, Frames, LaneWidth);

        // Frame <-> pixels along a lane. A lane that is not laid out yet (NaN or 0 wide) counts as 1 pixel.
        public static float FrameToX(float frame, int frames, float width) => frame / Mathf.Max(1, frames) * Usable(width);
        public static int XToFrame(float x, int frames, float width)
        {
            frames = Mathf.Max(1, frames);
            float at = x / Usable(width) * frames;
            return float.IsNaN(at) ? 0 : Mathf.RoundToInt(Mathf.Clamp(at, 0, frames - 1));
        }
        static float Usable(float width) => float.IsNaN(width) || width < 1 ? 1 : width;

        public void Rebuild()
        {
            rows.Clear();
            ruler.MarkDirtyRepaint();
            var set = context.Set; var tape = context.Tape;
            if (set == null || tape == null) return;

            foreach (var type in StudioTrackTypes.All)
                foreach (var track in tape.tracks.Where(t => t.type == type.TypeId))
                {
                    var mark = set.Mark(track.id);
                    Color color = mark != null ? StudioColors.For(mark.color) : type.Color;
                    object select = mark != null ? (object)mark : track;
                    rows.Add(Row(track.name, color, select, lane => DrawTakes(lane, track, color)));
                }
            // Tracks whose type this build does not know are still shown, greyed, so nothing looks lost.
            foreach (var track in tape.tracks.Where(t => StudioTrackTypes.Get(t.type) == null))
                rows.Add(Row(track.name + " (" + track.type + ")", Color.gray, track, lane => DrawTakes(lane, track, Color.gray)));
            rows.Add(CueRow());
            // Cameras: the shot's range as a bar, its keys as ticks. Click the name to select the camera.
            foreach (var camera in set.cameras)
            {
                var captured = camera;
                Color color = StudioColors.For(camera.color);
                rows.Add(Row(camera.name, color, camera, lane =>
                {
                    Bar(lane, captured.start * set.fps, captured.End(set) * set.fps, color, ReferenceEquals(captured, context.Selection) ? .55f : .3f, 7, 7);
                    foreach (var key in captured.keys) Bar(lane, key.time * set.fps - .2f, key.time * set.fps + .2f, Color.white, .9f, 4, 4);
                    foreach (var mark in captured.speeds) Bar(lane, mark.time * set.fps - .2f, mark.time * set.fps + .2f, StudioColors.Hazard, .9f, 9, 9);
                }));
            }
            // Layers on top of takes (Phase 4): one row per actor that has any.
            foreach (var group in set.overrides.GroupBy(o => o.actor))
            {
                var mark = set.Mark(group.Key);
                Color color = mark != null ? StudioColors.For(mark.color) : Color.gray;
                var layers = group.ToList();
                rows.Add(Row((mark?.actor ?? "?") + " layers", color, layers[0], lane =>
                {
                    foreach (var layer in layers) Bar(lane, layer.Start * set.fps, layer.Stop * set.fps, color, layer.muted ? .2f : .6f, 8, 8);
                }));
            }
            foreach (var shot in set.shots)
                rows.Add(Row(shot.name, StudioColors.Camera, null, lane =>
                    Bar(lane, shot.start * set.fps, (shot.start + shot.duration) * set.fps, StudioColors.Camera, .35f, 5, 6)));
            MovePlayhead();
        }

        VisualElement Row(string name, Color color, object select, System.Action<MeshGenerationContext> draw)
        {
            var row = new VisualElement();
            row.AddToClassList("ts-lane-row");
            var title = new VisualElement();
            title.AddToClassList("ts-lane-name");
            title.Add(StudioUi.Dot(color));
            title.Add(new Label(name));
            if (select != null) title.RegisterCallback<ClickEvent>(_ => context.Select(select));
            var lane = new VisualElement();
            lane.AddToClassList("ts-lane");
            lane.generateVisualContent += mesh => { DrawRange(mesh); draw(mesh); };
            Scrubbable(lane);
            row.Add(title); row.Add(lane);
            return row;
        }

        // ---- Drawing ----

        void Bar(MeshGenerationContext mesh, float fromFrame, float toFrame, Color color, float alpha, float top, float bottom)
        {
            var painter = mesh.painter2D;
            float left = X(fromFrame), right = Mathf.Max(left + 2, X(toFrame));
            float height = mesh.visualElement.resolvedStyle.height;
            painter.fillColor = new Color(color.r, color.g, color.b, alpha);
            painter.BeginPath();
            painter.MoveTo(new Vector2(left, top)); painter.LineTo(new Vector2(right, top));
            painter.LineTo(new Vector2(right, height - bottom)); painter.LineTo(new Vector2(left, height - bottom));
            painter.ClosePath(); painter.Fill();
        }

        void DrawTakes(MeshGenerationContext mesh, StudioTrack track, Color color)
        {
            // Other takes sit behind as thin faded bars; the active take is the solid one.
            foreach (var take in track.Kept)
                if (take != track.ActiveTake) Bar(mesh, take.start, take.End, color, .22f, 9, 9);
            var active = track.ActiveTake;
            if (active != null) Bar(mesh, active.start, active.End, color, track.mode == TrackMode.Muted ? .3f : .9f, 5, 5);
        }

        // The play range ([ and ]) as a soft band across every lane.
        void DrawRange(MeshGenerationContext mesh)
        {
            var window = context.window;
            if (window.InFrame <= 0 && window.OutFrame >= Frames - 1) return;
            Bar(mesh, window.InFrame, window.OutFrame + 1, Color.white, .06f, 0, 0);
        }

        void DrawRuler(MeshGenerationContext mesh)
        {
            var set = context.Set;
            if (set == null) return;
            var painter = mesh.painter2D;
            float height = ruler.resolvedStyle.height;
            // A tick each second; a label every few seconds so they never overlap.
            int seconds = Mathf.CeilToInt(set.length);
            int labelEvery = Mathf.Max(1, Mathf.CeilToInt(seconds / Mathf.Max(1, Usable(LaneWidth) / 60)));
            painter.strokeColor = new Color(.6f, .6f, .7f, .7f); painter.lineWidth = 1;
            for (int s = 0; s <= seconds; s++)
            {
                float x = X(s * set.fps);
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, s % labelEvery == 0 ? height - 9 : height - 4)); painter.LineTo(new Vector2(x, height));
                painter.Stroke();
                if (s % labelEvery == 0 && x < LaneWidth - 24)
                    mesh.DrawText(s + "s", new Vector2(x + 3, 3), 10, new Color(.62f, .63f, .7f));
            }
        }

        public void MovePlayhead()
        {
            if (context.Set == null) { playhead.style.display = DisplayStyle.None; return; }
            playhead.style.display = DisplayStyle.Flex;
            playhead.style.left = NameWidth + X(context.Frame);
        }

        // ---- Scrubbing ----

        void Scrubbable(VisualElement lane)
        {
            lane.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                lane.CapturePointer(e.pointerId);
                context.window.Scrub(FrameAt(e.localPosition.x));
            });
            lane.RegisterCallback<PointerMoveEvent>(e => { if (lane.HasPointerCapture(e.pointerId)) context.window.Scrub(FrameAt(e.localPosition.x)); });
            lane.RegisterCallback<PointerUpEvent>(e => { if (lane.HasPointerCapture(e.pointerId)) lane.ReleasePointer(e.pointerId); });
        }

        // ---- Cues ----

        VisualElement CueRow()
        {
            var set = context.Set;
            var row = new VisualElement();
            row.AddToClassList("ts-lane-row");
            var title = new VisualElement();
            title.AddToClassList("ts-lane-name");
            title.Add(StudioUi.Dot(StudioColors.Hazard));
            title.Add(new Label("Cues"));
            var lane = new VisualElement();
            lane.AddToClassList("ts-lane");
            lane.generateVisualContent += mesh =>
            {
                DrawRange(mesh);
                var painter = mesh.painter2D;
                float middle = lane.resolvedStyle.height / 2;
                foreach (var cue in set.cues)
                {
                    float x = X(cue.at * set.fps);
                    bool selected = ReferenceEquals(cue, context.Selection);
                    painter.fillColor = selected ? Color.white : StudioColors.Hazard;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x, middle - 7)); painter.LineTo(new Vector2(x + 6, middle));
                    painter.LineTo(new Vector2(x, middle + 7)); painter.LineTo(new Vector2(x - 6, middle));
                    painter.ClosePath(); painter.Fill();
                    mesh.DrawText(StudioCues.Get(cue.action)?.Label ?? cue.action, new Vector2(x + 9, middle - 7), 10, new Color(.8f, .8f, .86f));
                }
            };
            // Click a diamond to select it; drag it to change its time.
            lane.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                var cue = set.cues.OrderBy(c => Mathf.Abs(X(c.at * set.fps) - e.localPosition.x)).FirstOrDefault();
                if (cue == null || Mathf.Abs(X(cue.at * set.fps) - e.localPosition.x) > 8) { context.window.Scrub(FrameAt(e.localPosition.x)); return; }
                // Selecting rebuilds the timeline, which would drop this lane and its drag, so the cue is selected on release.
                draggingCue = cue; cueMoved = false;
                lane.CapturePointer(e.pointerId);
            });
            lane.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (draggingCue == null || !lane.HasPointerCapture(e.pointerId)) return;
                if (!cueMoved) { cueMoved = true; context.window.BeginEdit("Move cue"); }
                draggingCue.at = Mathf.Clamp(FrameAt(e.localPosition.x) / (float)set.fps, 0, set.length - 1f / set.fps);
                lane.MarkDirtyRepaint();
            });
            lane.RegisterCallback<PointerUpEvent>(e =>
            {
                var cue = draggingCue;
                draggingCue = null;
                if (lane.HasPointerCapture(e.pointerId)) lane.ReleasePointer(e.pointerId);
                if (cue == null) return;
                if (cueMoved) context.window.EndEdit(false);
                if (!ReferenceEquals(context.Selection, cue)) context.Select(cue);
            });
            // Capture can also be lost without a release (the window lost focus): finish the drag there.
            lane.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                if (draggingCue == null) return;
                draggingCue = null;
                if (cueMoved) context.window.EndEdit(false);
            });
            row.Add(title); row.Add(lane);
            return row;
        }
    }
}
