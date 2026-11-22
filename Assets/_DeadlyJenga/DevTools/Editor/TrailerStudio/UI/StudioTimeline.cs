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
        internal enum Lanes { All, Actors, Sound, Cameras }
        Lanes shownLanes;
        readonly Button laneFilter;

        internal void ShowLanes(Lanes lanes)
        {
            shownLanes = lanes;
            Rebuild();
        }

        void ChooseLanes()
        {
            var menu = new UnityEditor.GenericMenu();
            foreach (Lanes choice in System.Enum.GetValues(typeof(Lanes)))
                menu.AddItem(new UnityEngine.GUIContent(choice == Lanes.All ? "All tracks" : choice.ToString()),
                    choice == shownLanes, () => ShowLanes(choice));
            menu.ShowAsContext();
        }
        int? rangeAnchor;
        int rangeEnd;

        public StudioTimeline(StudioContext context, VisualElement host)
        {
            this.context = context; this.host = host;
            laneFilter = StudioUi.Small("All tracks", ChooseLanes, "Show timeline rows by type. Playback and export still use the whole set.");
            laneFilter.AddToClassList("ts-lane-filter");
            host.Add(laneFilter);
            ruler = new VisualElement();
            ruler.AddToClassList("ts-ruler");
            ruler.tooltip = "Click or drag to move the playhead. Shift+drag to set the preview range.";
            ruler.generateVisualContent += DrawRuler;
            BindRuler();
            rows = new ScrollView();
            rows.AddToClassList("ts-scroll");
            playhead = new VisualElement { pickingMode = PickingMode.Ignore };
            playhead.AddToClassList("ts-playhead");
            host.Add(ruler); host.Add(rows); host.Add(playhead);
            host.RegisterCallback<GeometryChangedEvent>(_ => MovePlayhead());
            rows.contentViewport.RegisterCallback<GeometryChangedEvent>(e =>
            {
                ruler.style.width = Mathf.Max(1, e.newRect.width - NameWidth);
                MovePlayhead();
            });
            ruler.RegisterCallback<GeometryChangedEvent>(_ => MovePlayhead());
        }

        int Frames => Mathf.Max(1, context.Set?.Frames ?? 1);
        public bool RangeZoom { get; private set; }
        int ViewFirst => RangeZoom ? context.window.InFrame : 0;
        int ViewAfter => RangeZoom ? context.window.OutFrame + 1 : Frames;
        int ViewCount => Mathf.Max(1, ViewAfter - ViewFirst);
        bool InView(float frame) => frame >= ViewFirst && frame < ViewAfter;
        float X(float frame, VisualElement lane) => FrameToX(frame - ViewFirst, ViewCount, lane.contentRect.width);
        int FrameAt(float x, VisualElement lane) => ViewFirst + XToFrame(x, ViewCount, lane.contentRect.width);

        public void SetRangeZoom(bool zoom)
        {
            if (RangeZoom == zoom) return;
            RangeZoom = zoom;
            Rebuild();
        }

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
            laneFilter.text = shownLanes == Lanes.All ? "All tracks ▾" : shownLanes + " ▾";
            laneFilter.EnableInClassList("ts-small--on", shownLanes != Lanes.All);
            ruler.MarkDirtyRepaint();
            MovePlayhead();
            var set = context.Set; var tape = context.Tape;
            if (set == null || tape == null) return;

            foreach (var type in StudioTrackTypes.All)
                foreach (var track in tape.tracks.Where(t => t.type == type.TypeId))
                {
                    var mark = set.Mark(track.id);
                    bool soundTrack = track.type == AudioEventTrack.Id;
                    if (shownLanes == Lanes.Actors && mark == null || shownLanes == Lanes.Sound && !soundTrack || shownLanes == Lanes.Cameras)
                        continue;
                    Color color = mark != null ? StudioColors.For(mark.color) : type.Color;
                    object select = mark != null ? (object)mark : track;
                    if (soundTrack)
                    {
                        bool suppressed = !track.solo && tape.tracks.Any(other => other.solo);
                        var activity = new StudioSoundActivity(track, set.fps, suppressed);
                        var row = Row(track.name, color, select, mesh =>
                        {
                            var take = track.ActiveTake;
                            if (take != null) Bar(mesh, take.start, take.End, color, .12f, 5, 5);
                            activity.Draw(mesh, frame => X(frame, mesh.visualElement));
                        });
                        var lane = row.Q(className: "ts-lane");
                        activity.AttachHover(lane, frame => X(frame, lane));
                        rows.Add(row);
                    }
                    else rows.Add(Row(track.name, color, select, lane => DrawTakes(lane, track, color)));
                }
            // Tracks whose type this build does not know are still shown, greyed, so nothing looks lost.
            foreach (var track in tape.tracks.Where(t => shownLanes == Lanes.All && StudioTrackTypes.Get(t.type) == null))
                rows.Add(Row(track.name + " (" + track.type + ")", Color.gray, track, lane => DrawTakes(lane, track, Color.gray)));
            rows.Add(CueRow());   // Keep world cues as timing references in every view.
            // Cameras: the shot's range as a bar, its keys as ticks. Click the name to select the camera.
            foreach (var camera in set.cameras.Where(_ => shownLanes == Lanes.All || shownLanes == Lanes.Cameras))
            {
                var captured = camera;
                Color color = StudioColors.For(camera.color);
                rows.Add(Row(camera.name, color, camera, lane =>
                {
                    Bar(lane, captured.start * set.fps, captured.End(set) * set.fps, color, ReferenceEquals(captured, context.Selection) ? .55f : .3f, 7, 7);
                    foreach (var key in captured.keys)
                        Bar(lane, key.time * set.fps - .2f, key.time * set.fps + .2f, lane.visualElement.resolvedStyle.color, .9f, 4, 4);
                    foreach (var mark in captured.speeds) Bar(lane, mark.time * set.fps - .2f, mark.time * set.fps + .2f, StudioColors.Hazard, .9f, 9, 9);
                }));
            }
            // Layers on top of takes (Phase 4): one row per actor that has any.
            foreach (var group in set.overrides.Where(_ => shownLanes == Lanes.All || shownLanes == Lanes.Actors).GroupBy(o => o.actor))
            {
                var mark = set.Mark(group.Key);
                Color color = mark != null ? StudioColors.For(mark.color) : Color.gray;
                var layers = group.ToList();
                rows.Add(Row((mark?.actor ?? "?") + " layers", color, layers[0], lane =>
                {
                    foreach (var layer in layers) Bar(lane, layer.Start * set.fps, layer.Stop * set.fps, color, layer.muted ? .2f : .6f, 8, 8);
                }));
            }
            foreach (var shot in set.shots.Where(_ => shownLanes == Lanes.All || shownLanes == Lanes.Cameras))
                rows.Add(Row(shot.name, StudioColors.Camera, null, lane =>
                    Bar(lane, shot.start * set.fps, (shot.start + shot.duration) * set.fps, StudioColors.Camera, .35f, 5, 6)));
        }

        VisualElement Row(string name, Color color, object select, System.Action<MeshGenerationContext> draw)
        {
            var row = new VisualElement();
            row.AddToClassList("ts-lane-row");
            var title = new VisualElement();
            title.AddToClassList("ts-lane-name");
            title.Add(StudioUi.Dot(color));
            var nameLabel = new Label(name);
            nameLabel.AddToClassList("ts-item-name");
            title.Add(nameLabel);
            title.tooltip = select != null ? "Select " + name : name;
            bool selected = select != null && ReferenceEquals(select, context.Selection);
            row.EnableInClassList("ts-lane-row--selected", selected);
            // The selected row's accent belongs inside the fixed-width name gutter.
            title.style.borderLeftWidth = selected ? 2 : 0;
            if (selected) title.style.borderLeftColor = color;
            row.style.borderLeftWidth = 0;
            if (select != null) title.RegisterCallback<ClickEvent>(_ => context.Select(select));
            var lane = new VisualElement();
            lane.AddToClassList("ts-lane");
            lane.style.overflow = Overflow.Hidden;
            lane.generateVisualContent += mesh => { DrawRange(mesh); draw(mesh); };
            Scrubbable(lane);
            row.Add(title); row.Add(lane);
            return row;
        }

        // ---- Drawing ----

        void Bar(MeshGenerationContext mesh, float fromFrame, float toFrame, Color color, float alpha, float top, float bottom)
        {
            if (toFrame <= ViewFirst || fromFrame >= ViewAfter || toFrame <= fromFrame) return;
            var painter = mesh.painter2D;
            var lane = mesh.visualElement;
            float width = lane.contentRect.width, height = lane.contentRect.height;
            if (width < 1 || height <= top + bottom) return;
            float left = X(Mathf.Max(ViewFirst, fromFrame), lane);
            float right = Mathf.Min(width, Mathf.Max(left + 2, X(Mathf.Min(ViewAfter, toFrame), lane)));
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
            if (!RangeZoom && (window.InFrame > 0 || window.OutFrame < Frames - 1))
                Bar(mesh, window.InFrame, window.OutFrame + 1, mesh.visualElement.resolvedStyle.color, .09f, 0, 0);
        }

        void DrawRuler(MeshGenerationContext mesh)
        {
            var set = context.Set;
            if (set == null) return;
            var painter = mesh.painter2D;
            float width = ruler.contentRect.width, height = ruler.contentRect.height;
            if (width < 1 || height < 1) return;
            if (rangeAnchor.HasValue)
                Bar(mesh, Mathf.Min(rangeAnchor.Value, rangeEnd), Mathf.Max(rangeAnchor.Value, rangeEnd) + 1, StudioColors.Camera, .26f, 0, 0);
            else
                DrawRange(mesh);

            // Keep ticks on whole seconds when the visible span is long and show frame times for close edits.
            int tick = Mathf.Max(1, Mathf.CeilToInt(ViewCount * 9f / width));
            if (tick > set.fps / 4) tick = Mathf.CeilToInt(tick / (float)set.fps) * set.fps;
            const int labelSize = 11;
            const float labelGap = 72;
            int labelEvery = tick * Mathf.Max(1, Mathf.CeilToInt(ViewCount * labelGap / (width * tick)));
            int firstTick = Mathf.CeilToInt(ViewFirst / (float)tick) * tick;
            var labelColor = ruler.resolvedStyle.color;
            var majorTick = labelColor;
            majorTick.a *= .73f;
            var minorTick = labelColor;
            minorTick.a *= .34f;
            painter.lineWidth = 1;
            bool startLabel = firstTick != ViewFirst || ViewFirst % labelEvery != 0;
            if (startLabel && width > labelGap - 10)
                mesh.DrawText(StudioUi.Time(ViewFirst, set.fps), new Vector2(3, 2), labelSize, labelColor);
            for (int frame = firstTick; frame < ViewAfter; frame += tick)
            {
                float x = X(frame, ruler);
                bool label = frame % labelEvery == 0 && (!startLabel || x >= labelGap);
                painter.strokeColor = label ? majorTick : minorTick;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, label ? height - 7 : height - 4)); painter.LineTo(new Vector2(x, height));
                painter.Stroke();
                if (label && x < width - (labelGap - 10))
                    mesh.DrawText(StudioUi.Time(frame, set.fps), new Vector2(x + 3, 2), labelSize, labelColor);
            }
        }

        public void MovePlayhead()
        {
            if (context.Set == null || !InView(context.Frame)) { playhead.style.display = DisplayStyle.None; return; }
            playhead.style.display = DisplayStyle.Flex;
            playhead.style.left = NameWidth + X(context.Frame, ruler);
        }

        // ---- Scrubbing ----

        void BindRuler()
        {
            ruler.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0 || context.Set == null) return;
                int frame = FrameAt(e.localPosition.x, ruler);
                if (e.shiftKey)
                {
                    rangeAnchor = rangeEnd = frame;
                    ruler.MarkDirtyRepaint();
                }
                else context.window.Scrub(frame);
                ruler.CapturePointer(e.pointerId);
            });
            ruler.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!ruler.HasPointerCapture(e.pointerId)) return;
                int frame = FrameAt(e.localPosition.x, ruler);
                if (!rangeAnchor.HasValue) { context.window.Scrub(frame); return; }
                if (rangeEnd == frame) return;
                rangeEnd = frame;
                ruler.MarkDirtyRepaint();
            });
            ruler.RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.button != 0 || !ruler.HasPointerCapture(e.pointerId)) return;
                var anchor = rangeAnchor;
                int end = FrameAt(e.localPosition.x, ruler);
                ClearRangePreview();
                ruler.ReleasePointer(e.pointerId);
                if (anchor.HasValue)
                    context.window.SetReviewRange(Mathf.Min(anchor.Value, end), Mathf.Max(anchor.Value, end));
            });
            ruler.RegisterCallback<PointerCaptureOutEvent>(_ => ClearRangePreview());
        }

        void ClearRangePreview()
        {
            if (!rangeAnchor.HasValue) return;
            rangeAnchor = null;
            ruler.MarkDirtyRepaint();
        }

        void Scrubbable(VisualElement lane)
        {
            lane.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                lane.CapturePointer(e.pointerId);
                context.window.Scrub(FrameAt(e.localPosition.x, lane));
            });
            lane.RegisterCallback<PointerMoveEvent>(e => { if (lane.HasPointerCapture(e.pointerId)) context.window.Scrub(FrameAt(e.localPosition.x, lane)); });
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
            lane.style.overflow = Overflow.Hidden;
            lane.generateVisualContent += mesh =>
            {
                DrawRange(mesh);
                var painter = mesh.painter2D;
                float middle = lane.resolvedStyle.height / 2;
                var labelColor = lane.resolvedStyle.color;
                foreach (var cue in set.cues)
                {
                    float frame = cue.at * set.fps;
                    if (!InView(frame)) continue;
                    float x = X(frame, lane);
                    bool selected = ReferenceEquals(cue, context.Selection);
                    painter.fillColor = StudioColors.Hazard;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x, middle - 7)); painter.LineTo(new Vector2(x + 6, middle));
                    painter.LineTo(new Vector2(x, middle + 7)); painter.LineTo(new Vector2(x - 6, middle));
                    painter.ClosePath(); painter.Fill();
                    if (selected)
                    {
                        painter.strokeColor = labelColor;
                        painter.lineWidth = 1.5f;
                        painter.Stroke();
                    }
                    mesh.DrawText(StudioCues.Get(cue.action)?.Label ?? cue.action, new Vector2(x + 9, middle - 7), 11, labelColor);
                }
            };
            // Click a diamond to select it; drag it to change its time.
            lane.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                var cue = set.cues.Where(c => InView(c.at * set.fps))
                    .OrderBy(c => Mathf.Abs(X(c.at * set.fps, lane) - e.localPosition.x)).FirstOrDefault();
                if (cue == null || Mathf.Abs(X(cue.at * set.fps, lane) - e.localPosition.x) > 8)
                {
                    context.window.Scrub(FrameAt(e.localPosition.x, lane));
                    return;
                }
                // Selecting rebuilds the timeline, which would drop this lane and its drag, so the cue is selected on release.
                draggingCue = cue; cueMoved = false;
                lane.CapturePointer(e.pointerId);
            });
            lane.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (draggingCue == null || !lane.HasPointerCapture(e.pointerId)) return;
                if (!cueMoved) { cueMoved = true; context.window.BeginEdit("Move cue"); }
                draggingCue.at = FrameAt(e.localPosition.x, lane) / (float)set.fps;
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
