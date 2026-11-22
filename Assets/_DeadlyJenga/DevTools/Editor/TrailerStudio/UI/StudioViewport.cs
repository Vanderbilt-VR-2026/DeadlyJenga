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
            ScreenLine(a, b, color, width);
            return true;
        }

        internal void ScreenLine(Vector2 a, Vector2 b, Color color, float width)
        {
            painter.strokeColor = color; painter.lineWidth = width;
            painter.BeginPath(); painter.MoveTo(a); painter.LineTo(b); painter.Stroke();
        }

        public void Arrow(Vector3 from, Vector3 to, Color color, float width = 2)
        {
            if (!viewport.ToPanel(from, out var a) || !viewport.ToPanel(to, out var b)) return;
            ScreenLine(a, b, color, width);
            Vector2 back = a - b;
            if (back.sqrMagnitude < 16) return;
            float size = Mathf.Min(10, back.magnitude * .45f);
            back = back.normalized * size;
            Vector2 side = new Vector2(-back.y, back.x) * .55f;
            ScreenLine(b, b + back + side, color, width);
            ScreenLine(b, b + back - side, color, width);
        }

        public void Dot(Vector3 at, Color color, float radius = 4)
        {
            if (!viewport.ToPanel(at, out var p)) return;
            painter.fillColor = color;
            painter.BeginPath(); painter.Arc(p, radius, 0, 360); painter.Fill();
        }

        // A rectangle in viewport pixels (for box selection).
        public void ScreenBox(Rect box, Color color)
        {
            painter.strokeColor = color; painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(box.xMin, box.yMin)); painter.LineTo(new Vector2(box.xMax, box.yMin));
            painter.LineTo(new Vector2(box.xMax, box.yMax)); painter.LineTo(new Vector2(box.xMin, box.yMax));
            painter.ClosePath(); painter.Stroke();
        }

        // The part of the viewport the picture covers, in viewport pixels.
        public Rect Picture => viewport.PictureRect();

        internal bool TryScreenPoint(Vector3 world, out Vector2 point) =>
            viewport.ToPanel(world, out point) && Picture.Contains(point);

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
        readonly List<Rect> nameplates = new List<Rect>();
        VisualElement empty;
        string dragging;                  // mark id being dragged
        IStudioViewportTool tool;
        bool toolActive;
        bool orbiting, panning, moved;
        Vector2 last;
        Vector2? hover;
        Vector3? placementPoint;
        bool placementOnSurface;

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
            help.style.right = 8;
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.color = Color.white;
            help.style.backgroundColor = new Color(.045f, .065f, .09f, .9f);
            help.style.paddingLeft = help.style.paddingRight = 7;
            help.style.paddingTop = help.style.paddingBottom = 4;
            host.Add(image); host.Add(lines); host.Add(markLayer); host.Add(help);

            host.RegisterCallback<PointerDownEvent>(Down);
            host.RegisterCallback<PointerMoveEvent>(Move);
            host.RegisterCallback<PointerUpEvent>(Up);
            host.RegisterCallback<PointerLeaveEvent>(_ => { hover = null; Tick(); });
            host.RegisterCallback<WheelEvent>(Wheel);
            host.RegisterCallback<GeometryChangedEvent>(_ => Fit());
        }

        Camera Camera => StudioCamera.Camera;

        void Fit()
        {
            if (!StudioRun.StageOpen) return;
            // Draw at the panel's size, but not larger than 720p to keep the editor quick.
            // Not laid out yet (or no real window, as in batch runs): leave the picture size alone.
            if (float.IsNaN(host.resolvedStyle.width) || host.resolvedStyle.width < 120 || host.resolvedStyle.height < 80) return;
            float width = host.resolvedStyle.width, height = host.resolvedStyle.height;
            float scale = Mathf.Min(1, 1280f / width, 720f / height);
            StudioCamera.Resize(Mathf.RoundToInt(width * scale), Mathf.RoundToInt(height * scale));
        }

        public void Rebuild()
        {
            bool open = StudioRun.StageOpen;
            if (!open || !(context.window.Mode is SetMode)) Placing = null;
            if (empty != null) { empty.RemoveFromHierarchy(); empty = null; }
            image.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            help.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
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
            if (context.Selection is StudioMark selected && handles.TryGetValue(selected.id, out var active))
                active.BringToFront();
        }

        // Called about 30 times a second while the stage is open.
        public void Tick()
        {
            if (!StudioRun.StageOpen || !Camera) return;
            LookThroughCamera();
            placementPoint = null;
            if (Placing.HasValue && hover.HasValue && StudioRun.State == StudioState.Held && PictureRect().Contains(hover.Value))
                placementPoint = PlacementAt(hover.Value, Placing.Value, out placementOnSurface);
            UpdateHelp();
            if (image.image != StudioCamera.Image) image.image = StudioCamera.Image;
            image.MarkDirtyRepaint();
            lines.MarkDirtyRepaint();
            var stage = context.Stage;
            if (stage == null || context.Set == null) return;
            nameplates.Clear();
            var selected = context.Selection as StudioMark;
            if (selected != null) PositionHandle(selected);
            foreach (var mark in context.Set.marks)
                if (mark != selected) PositionHandle(mark);

            void PositionHandle(StudioMark mark)
            {
                if (!handles.TryGetValue(mark.id, out var handle)) return;
                bool visible = ToPanel(stage.ToWorld(mark.position) + Vector3.up * (mark.role == StudioRole.Hand ? 0 : .9f), out var point);
                handle.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible) return;
                handle.style.left = point.x; handle.style.top = point.y;
                // Turn the little line so it points where the actor faces, as seen from this camera.
                handle.Q("facing").style.rotate = new Rotate(mark.yaw - StudioCamera.Yaw);
                handle.Q("facing").style.transformOrigin = new TransformOrigin(Length.Percent(50), new Length(25));
                string warning = StudioRun.State == StudioState.Held ? stage.MarkWarning(mark) : "";
                var label = handle.Q<Label>("label");
                label.text = warning.Length > 0 ? mark.actor + " · " + warning : mark.actor;
                label.EnableInClassList("ts-mark-label--warn", warning.Length > 0);
                float width = label.layout.width;
                if (!(width > 0)) width = 20 + label.text.Length * 7;
                var plate = new Rect(point.x - width / 2 - 2, point.y + 10, width + 4, 18);
                bool crowded = false;
                foreach (var other in nameplates)
                    if (other.Overlaps(plate)) { crowded = true; break; }
                label.style.visibility = crowded ? Visibility.Hidden : Visibility.Visible;
                if (!crowded) nameplates.Add(plate);
            }
        }

        void UpdateHelp()
        {
            const string navigation = "Drag empty space to orbit  ·  Right or middle drag to pan  ·  Scroll to zoom";
            string message = navigation;
            if (lookedThrough)
                message = "Shot camera preview  ·  Click Looking in Shoot to return to the free camera";
            else if (moved && (orbiting || panning))
                message = orbiting ? "Orbiting around the reticle" : "Panning the free camera";
            else if (Placing.HasValue && StudioRun.State == StudioState.Held)
                message = Placing.Value + " placement  ·  " +
                    (placementPoint.HasValue && !placementOnSurface ? "Click to place at the view target" : "Click to place at the reticle") + "  ·  Esc cancels";
            else if (StudioRun.State == StudioState.Held && context.window.Mode is DirectMode direct)
                message = direct.ViewportHint(context.Selection) + "\n" + navigation;
            else if (StudioRun.State == StudioState.Held && context.window.Mode is SetMode && context.Selection is StudioMark mark)
                message = "Drag mark to move  ·  Shift+drag to turn" + (mark.role == StudioRole.Hand ? "  ·  Alt+drag to change height" : "") +
                    "\n" + navigation;
            else if (context.window.Mode is ShootMode && context.Stage != null)
                message = "Free camera  ·  Frame your shot, then choose Camera from this view or Key here\n" + navigation;
            if (help.text != message) help.text = message;
        }

        // In the Shoot tab the viewport can show what a shot camera sees instead of the director's free view.
        bool lookedThrough;
        void LookThroughCamera()
        {
            var set = context.Set; var stage = context.Stage;
            // Looking through a shot camera belongs to the Shoot tab. Other tabs need the free view to orbit and drag marks.
            if (ShootMode.LookThrough.Length > 0 && !(context.window.Mode is ShootMode)) ShootMode.LookThrough = "";
            var camera = set == null || ShootMode.LookThrough.Length == 0 ? null : set.cameras.Find(c => c.id == ShootMode.LookThrough);
            if (camera == null || stage == null || StudioHandheld.Recording || StudioRender.Running)
            {
                if (lookedThrough && !StudioHandheld.Recording) { lookedThrough = false; StudioFirstPerson.Release(Camera); StudioCamera.Camera.fieldOfView = 60; StudioCamera.Place(); }
                return;
            }
            lookedThrough = true;
            float time = context.Frame / (float)set.fps;
            StudioCameras.Apply(Camera, StudioCameras.Evaluate(camera, set, stage.tape, stage.anchor, time, time));
        }

        void DrawOverlays(MeshGenerationContext mesh)
        {
            if (!StudioRun.StageOpen || !Camera) return;
            var painter = new StudioOverlayPainter(this, mesh.painter2D);
            foreach (var overlay in StudioExtensions.Overlays)
                try { overlay.Draw(painter, context); } catch (Exception error) { Debug.LogException(error); }
            if (!lookedThrough && moved && (orbiting || panning) && painter.TryScreenPoint(StudioCamera.Target, out var pivot))
            {
                var color = new Color(.72f, .88f, 1f, .95f);
                painter.ScreenLine(pivot - Vector2.right * 10, pivot + Vector2.right * 10, color, 1.5f);
                painter.ScreenLine(pivot - Vector2.up * 10, pivot + Vector2.up * 10, color, 1.5f);
            }
            var stage = context.Stage;
            if (stage == null || !(context.window.Mode is SetMode) || StudioRun.State != StudioState.Held) return;
            if (placementPoint.HasValue && Placing.HasValue)
            {
                Vector3 point = placementPoint.Value;
                Color color = placementOnSurface ? new Color(.32f, 1f, .76f) : new Color(1f, .73f, .35f);
                if (Placing.Value == StudioRole.Hand)
                    painter.Line(point - Vector3.up * HandPlacementHeight, point, color, 1.5f);
                DrawMarkReticle(painter, point, Quaternion.identity, color);
            }
            else if (context.Selection is StudioMark mark)
                DrawMarkReticle(painter, stage.ToWorld(mark.position), Quaternion.Euler(0, mark.yaw, 0), StudioColors.For(mark.color));
        }

        void DrawMarkReticle(StudioOverlayPainter painter, Vector3 point, Quaternion facing, Color color)
        {
            if (!ToPanel(point, out var panel) || !painter.Picture.Contains(panel)) return;
            var rect = new Rect(panel.x - 8, panel.y - 8, 16, 16);
            painter.ScreenBox(rect, color);
            painter.Dot(point, color, 2);
            painter.Arrow(point, point + facing * Vector3.forward * 1.35f, color, 2);
        }

        const float HandPlacementHeight = 6;

        Vector3 PlacementAt(Vector2 local, StudioRole role, out bool onSurface)
        {
            onSurface = Surface(local, out var hit);
            var point = onSurface ? hit : StudioCamera.Target;
            return role == StudioRole.Hand ? point + Vector3.up * HandPlacementHeight : point;
        }

        // ---- Picture <-> world ----

        // The rectangle the picture really covers inside the panel (it keeps its shape).
        internal Rect PictureRect()
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

        public Ray ToRay(Vector2 local)
        {
            var rect = PictureRect();
            return Camera.ViewportPointToRay(new Vector3((local.x - rect.x) / rect.width, 1 - (local.y - rect.y) / rect.height, 0));
        }

        public bool Surface(Vector2 local, out Vector3 point)
        {
            int mask = (1 << Layers.Block) | (1 << Layers.Surface) | 1;
            if (Physics.Raycast(ToRay(local), out var hit, 2000, mask, QueryTriggerInteraction.Ignore)) { point = hit.point; return true; }
            point = default;
            return false;
        }

        // The block under a point of the picture, as an index into the map's block list (-1 = none).
        public int BlockAt(Vector2 local)
        {
            var stage = context.Stage;
            if (stage == null || !Camera) return -1;
            if (!Physics.Raycast(ToRay(local), out var hit, 2000, 1 << Layers.Block, QueryTriggerInteraction.Ignore)) return -1;
            var body = hit.rigidbody ? hit.rigidbody : hit.collider.GetComponentInParent<Rigidbody>();
            return body ? System.Array.IndexOf(stage.blocks, body) : -1;
        }

        public VisualElement Host => host;

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
            if (!StudioRun.StageOpen || !Camera || !PictureRect().Contains(e.localPosition)) return;
            host.CapturePointer(e.pointerId);
            last = e.localPosition; moved = false;
            if (e.button != 0) { panning = true; return; }
            // The current mode may have its own way of using the viewport (picking blocks, aiming arrows, placing cameras).
            tool = context.window.Mode as IStudioViewportTool;
            if (tool != null && StudioRun.State == StudioState.Held && tool.Down(e, this, context))
            {
                toolActive = true;
                Tick();
                return;
            }
            if (Placing.HasValue && StudioRun.State == StudioState.Held && context.window.Mode is SetMode && context.Stage != null)
            {
                var role = Placing.Value; Placing = null;
                Vector3 world = PlacementAt(e.localPosition, role, out _);
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
            hover = e.localPosition;
            if (!host.HasPointerCapture(e.pointerId))
            {
                if (Placing.HasValue) Tick();
                return;
            }
            Vector2 delta = (Vector2)e.localPosition - last;
            last = e.localPosition;
            if (delta.sqrMagnitude > 0) moved = true;
            if (toolActive) { tool.Move(e, this, context); Tick(); return; }
            if (lookedThrough) return;   // the shot camera decides the view; press Looking again to look around
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
            if (!host.HasPointerCapture(e.pointerId)) return;
            host.ReleasePointer(e.pointerId);
            hover = host.contentRect.Contains(e.localPosition) ? (Vector2?)e.localPosition : null;
            if (toolActive)
            {
                toolActive = false;
                tool.Up(e, this, context);
                orbiting = panning = false;
                Tick();
                return;
            }
            if (dragging != null)
            {
                dragging = null;
                context.window.EndEdit(false);
            }
            else if (orbiting && !moved && context.Selection != null) context.Select(null);   // a plain click on empty space clears the selection
            orbiting = panning = false;
            Tick();
        }

        void Wheel(WheelEvent e)
        {
            if (!StudioRun.StageOpen || !Camera || lookedThrough) return;
            StudioCamera.Zoom(e.delta.y > 0 ? 1.12f : .89f);
            Tick();
            e.StopPropagation();
        }

        public void CancelPlacing() { Placing = null; if (StudioRun.StageOpen) Rebuild(); }
    }
}
