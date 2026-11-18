using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // What overlays draw with: world points in, lines and dots on the picture out.
    public sealed class StudioOverlayPainter
    {
        readonly StudioViewport viewport;
        readonly Painter2D painter;
        internal StudioOverlayPainter(StudioViewport viewport, Painter2D painter) { this.viewport = viewport; this.painter = painter; }

        public bool Line(Vector3 from, Vector3 to, Color color, float width = 2)
        {
            if (!viewport.ToPanel(from, out var a) || !viewport.ToPanel(to, out var b)) return false;
            painter.strokeColor = color; painter.lineWidth = width;
            painter.BeginPath(); painter.MoveTo(a); painter.LineTo(b); painter.Stroke();
            return true;
        }

        public void Dot(Vector3 at, Color color, float radius = 4)
        {
            if (!viewport.ToPanel(at, out var p)) return;
            painter.fillColor = color;
            painter.BeginPath(); painter.Arc(p, radius, 0, 360); painter.Fill();
        }

        public void Path(IReadOnlyList<Vector3> points, Color color, float width = 2)
        {
            for (int i = 1; i < points.Count; i++) Line(points[i - 1], points[i], color, width);
        }
    }

    // The middle of the window: the stage as the director's camera sees it, with marks you can drag.
    //   Drag empty space: orbit.  Right or middle drag: slide.  Wheel: zoom.
    //   Drag a mark: move it along the surface.  Shift+drag a mark: turn it.  Alt+drag a hand mark: raise or lower it.
    public sealed class StudioViewport
    {
        readonly StudioContext context;
        readonly VisualElement host, lines, markLayer;
        readonly Image image;
        readonly Label help;
        readonly Dictionary<string, VisualElement> handles = new Dictionary<string, VisualElement>();
        VisualElement empty;
        string dragging;                  // mark id being dragged
        bool orbiting, panning, moved;
        Vector2 last;

        // Set by the Set mode: the next click on the picture places a new mark of this role.
        public StudioRole? Placing;

        public StudioViewport(StudioContext context, VisualElement host)
        {
            this.context = context; this.host = host;
            image = new Image { scaleMode = ScaleMode.ScaleToFit };
            image.AddToClassList("ts-view-image");
            lines = new VisualElement { pickingMode = PickingMode.Ignore };
            lines.AddToClassList("ts-view-lines");
            lines.generateVisualContent += DrawOverlays;
            markLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            markLayer.AddToClassList("ts-view-lines");
            help = new Label { pickingMode = PickingMode.Ignore };
            help.AddToClassList("ts-view-help");
            host.Add(image); host.Add(lines); host.Add(markLayer); host.Add(help);

            host.RegisterCallback<PointerDownEvent>(Down);
            host.RegisterCallback<PointerMoveEvent>(Move);
            host.RegisterCallback<PointerUpEvent>(Up);
            host.RegisterCallback<WheelEvent>(Wheel);
            host.RegisterCallback<GeometryChangedEvent>(_ => Fit());
        }

        Camera Camera => StudioCamera.Camera;

        void Fit()
        {
            if (!StudioRun.StageOpen) return;
            // Draw at the panel's size, but not larger than 720p to keep the editor quick.
            float width = Mathf.Max(64, host.resolvedStyle.width), height = Mathf.Max(64, host.resolvedStyle.height);
            float scale = Mathf.Min(1, 1280f / width, 720f / height);
            StudioCamera.Resize(Mathf.RoundToInt(width * scale), Mathf.RoundToInt(height * scale));
        }

        public void Rebuild()
        {
            bool open = StudioRun.StageOpen;
            if (empty != null) { empty.RemoveFromHierarchy(); empty = null; }
            image.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open)
            {
                bool opening = StudioRun.State == StudioState.Opening;
                empty = context.Set == null
                    ? StudioCastList.Empty("Trailer Studio", "Make a Set to begin: pick a map, place your actors, record, then film it as many times as you like.", null, null)
                    : StudioCastList.Empty(opening ? "Opening the stage…" : "The stage is closed",
                        opening ? "Loading the map in Play Mode." : "Open the stage to see \"" + context.Set.title + "\", place marks, record and play.",
                        opening ? null : "Open stage", () => { try { StudioRun.OpenStage(context.Set); } catch (Exception error) { context.Say(error.Message); } });
                host.Add(empty);
                markLayer.Clear(); handles.Clear();
                help.text = "";
                return;
            }
            Fit();
            image.image = StudioCamera.Image;
            help.text = Placing.HasValue ? "Click where the new mark should stand.  Esc cancels." :
                "Drag: orbit   Right-drag: slide   Wheel: zoom   Drag a mark: move   Shift+drag a mark: turn";
            BuildHandles();
            Tick();
        }

        void BuildHandles()
        {
            markLayer.Clear(); handles.Clear();
            if (context.Set == null) return;
            foreach (var mark in context.Set.marks)
            {
                var handle = new VisualElement { pickingMode = PickingMode.Ignore };
                handle.AddToClassList("ts-mark");
                handle.style.backgroundColor = StudioColors.For(mark.color);
                handle.EnableInClassList("ts-mark--selected", ReferenceEquals(mark, context.Selection));
                var facing = new VisualElement { name = "facing", pickingMode = PickingMode.Ignore };
                facing.AddToClassList("ts-mark-facing");
                handle.Add(facing);
                var label = new Label { name = "label", pickingMode = PickingMode.Ignore };
                label.AddToClassList("ts-mark-label");
                handle.Add(label);
                markLayer.Add(handle);
                handles[mark.id] = handle;
            }
        }

        // Called about 30 times a second while the stage is open.
        public void Tick()
        {
            if (!StudioRun.StageOpen || !Camera) return;
            if (image.image != StudioCamera.Image) image.image = StudioCamera.Image;
            image.MarkDirtyRepaint();
            lines.MarkDirtyRepaint();
            var stage = context.Stage;
            if (stage == null || context.Set == null) return;
            foreach (var mark in context.Set.marks)
            {
                if (!handles.TryGetValue(mark.id, out var handle)) continue;
                bool visible = ToPanel(stage.ToWorld(mark.position) + Vector3.up * (mark.role == StudioRole.Hand ? 0 : .9f), out var point);
                handle.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible) continue;
                handle.style.left = point.x; handle.style.top = point.y;
                // Turn the little line so it points where the actor faces, as seen from this camera.
                handle.Q("facing").style.rotate = new Rotate(mark.yaw - StudioCamera.Yaw);
                handle.Q("facing").style.transformOrigin = new TransformOrigin(Length.Percent(50), new Length(25));
                string warning = StudioRun.State == StudioState.Held ? stage.MarkWarning(mark) : "";
                var label = handle.Q<Label>("label");
                label.text = warning.Length > 0 ? mark.actor + " · " + warning : mark.actor;
                label.EnableInClassList("ts-mark-label--warn", warning.Length > 0);
            }
        }

        void DrawOverlays(MeshGenerationContext mesh)
        {
            if (!StudioRun.StageOpen || !Camera) return;
            var painter = new StudioOverlayPainter(this, mesh.painter2D);
            foreach (var overlay in StudioExtensions.Overlays)
                try { overlay.Draw(painter, context); } catch (Exception error) { Debug.LogException(error); }
        }

        // ---- Picture <-> world ----

        // The rectangle the picture really covers inside the panel (it keeps its shape).
        Rect PictureRect()
        {
            float width = host.resolvedStyle.width, height = host.resolvedStyle.height;
            var texture = StudioCamera.Image;
            if (!texture || width <= 0 || height <= 0) return new Rect(0, 0, width, height);
            float scale = Mathf.Min(width / texture.width, height / texture.height);
            float w = texture.width * scale, h = texture.height * scale;
            return new Rect((width - w) / 2, (height - h) / 2, w, h);
        }

        public bool ToPanel(Vector3 world, out Vector2 point)
        {
            point = default;
            if (!Camera) return false;
            Vector3 view = Camera.WorldToViewportPoint(world);
            if (view.z <= .05f) return false;
            var rect = PictureRect();
            point = new Vector2(rect.x + view.x * rect.width, rect.y + (1 - view.y) * rect.height);
            return true;
        }

        Ray ToRay(Vector2 local)
        {
            var rect = PictureRect();
            return Camera.ViewportPointToRay(new Vector3((local.x - rect.x) / rect.width, 1 - (local.y - rect.y) / rect.height, 0));
        }

        bool Surface(Vector2 local, out Vector3 point)
        {
            int mask = (1 << Layers.Block) | (1 << Layers.Surface) | 1;
            if (Physics.Raycast(ToRay(local), out var hit, 2000, mask, QueryTriggerInteraction.Ignore)) { point = hit.point; return true; }
            point = default;
            return false;
        }

        StudioMark MarkAt(Vector2 local)
        {
            var stage = context.Stage;
            if (stage == null || context.Set == null) return null;
            StudioMark best = null; float bestDistance = 14;
            foreach (var mark in context.Set.marks)
            {
                if (!ToPanel(stage.ToWorld(mark.position) + Vector3.up * (mark.role == StudioRole.Hand ? 0 : .9f), out var point)) continue;
                float distance = Vector2.Distance(point, local);
                if (distance < bestDistance) { best = mark; bestDistance = distance; }
            }
            return best;
        }

        // ---- Mouse ----

        void Down(PointerDownEvent e)
        {
            if (!StudioRun.StageOpen || !Camera) return;
            host.CapturePointer(e.pointerId);
            last = e.localPosition; moved = false;
            if (e.button != 0) { panning = true; return; }
            if (Placing.HasValue && StudioRun.State == StudioState.Held)
            {
                var role = Placing.Value; Placing = null;
                Vector3 world = Surface(e.localPosition, out var hit) ? hit : StudioCamera.Target;
                if (role == StudioRole.Hand) world += Vector3.up * 6;
                StudioMark made = null;
                context.Edit("Add " + role.ToString().ToLowerInvariant(), () => made = context.Set.AddMark(role, context.Stage.ToStage(world)), true);
                context.Select(made);
                return;
            }
            var mark = StudioRun.State == StudioState.Held ? MarkAt(e.localPosition) : null;
            if (mark != null)
            {
                dragging = mark.id;
                if (!ReferenceEquals(context.Selection, mark)) context.Select(mark);
                context.window.BeginEdit("Move " + mark.actor);
                return;
            }
            orbiting = true;
        }

        void Move(PointerMoveEvent e)
        {
            if (!host.HasPointerCapture(e.pointerId)) return;
            Vector2 delta = (Vector2)e.localPosition - last;
            last = e.localPosition;
            if (delta.sqrMagnitude > 0) moved = true;
            if (orbiting) StudioCamera.Orbit(delta.x * .35f, delta.y * .25f);
            else if (panning) StudioCamera.Pan(delta);
            else if (dragging != null)
            {
                var mark = context.Set?.Mark(dragging);
                var stage = context.Stage;
                if (mark == null || stage == null) return;
                if (e.shiftKey) mark.yaw = Mathf.Repeat(mark.yaw + delta.x * 1.2f, 360);
                else if (mark.role == StudioRole.Hand)
                {
                    if (e.altKey) mark.position.y -= delta.y * StudioCamera.Distance * .002f;
                    else
                    {
                        // Hands float: slide on a level plane at the hand's own height.
                        var plane = new Plane(Vector3.up, stage.ToWorld(mark.position));
                        var ray = ToRay(e.localPosition);
                        if (plane.Raycast(ray, out float distance)) mark.position = stage.ToStage(ray.GetPoint(distance));
                    }
                }
                else if (Surface(e.localPosition, out var hit)) mark.position = stage.ToStage(hit);
            }
            Tick();
        }

        void Up(PointerUpEvent e)
        {
            if (host.HasPointerCapture(e.pointerId)) host.ReleasePointer(e.pointerId);
            if (dragging != null)
            {
                dragging = null;
                context.window.EndEdit(false);
            }
            else if (orbiting && !moved && context.Selection != null) context.Select(null);   // a plain click on empty space clears the selection
            orbiting = panning = false;
        }

        void Wheel(WheelEvent e)
        {
            if (!StudioRun.StageOpen) return;
            StudioCamera.Zoom(e.delta.y > 0 ? 1.12f : .89f);
            Tick();
            e.StopPropagation();
        }

        public void CancelPlacing() { Placing = null; if (StudioRun.StageOpen) Rebuild(); }
    }
}
