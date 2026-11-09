using System.Collections.Generic;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    internal sealed class CandyMysteryBodyGuide
    {
        internal enum Kind { Evidence, Sheriff, Revive }
        internal const float NearbyMultiplier = 2.5f;

        static readonly Color32 Ink = new(24, 21, 42, 249);
        static readonly Color32 Ivory = new(255, 247, 233, 255);
        static readonly Color32 Evidence = new(255, 204, 127, 255);
        static readonly Color32 Sheriff = new(141, 192, 255, 255);
        static readonly Color32 Revive = new(133, 240, 207, 255);

        readonly RectTransform root;
        readonly Canvas canvas;
        readonly Image card, bezel, pin, pinCore, pinCross;
        readonly Text arrow, title, direction, state, range, edge;
        readonly Image[] bars = new Image[3];
        MarkerGate marker;
        readonly string turnAroundGlyph;
        Camera headingCamera;
        float heading;

        internal struct MarkerGate
        {
            string target;
            Kind kind;
            bool wasVisible;
            float visibleSince;

            public bool Show(string id, Kind nextKind, bool clear, float now)
            {
                if (id != target || kind != nextKind)
                {
                    target = id;
                    kind = nextKind;
                    wasVisible = false;
                }

                if (!clear)
                {
                    wasVisible = false;
                    return false;
                }

                if (!wasVisible) visibleSince = now;
                wasVisible = true;
                // Hide immediately on cover and wait for a brief clear view before restoring the pin.
                return now - visibleSince >= .10f;
            }
        }

        public CandyMysteryBodyGuide(RectTransform root)
        {
            this.root = root;
            canvas = root.GetComponent<Canvas>();
            card = Panel(root, "Body signal tracker", new Vector2(365, 84), new Vector2(0, -176), Ink);
            bezel = Panel(card.rectTransform, "Signal accent", new Vector2(4, 51), new Vector2(-169, 0), Evidence);
            var dial = Panel(card.rectTransform, "Bearing dial", new Vector2(46, 46), new Vector2(-139, 0), new Color32(54, 44, 76, 255));
            arrow = Label(dial.rectTransform, "Direction", new Vector2(40, 40), Vector2.zero, 31, TextAnchor.MiddleCenter);
            arrow.text = "▲";
            turnAroundGlyph = arrow.font.HasCharacter('\u21b6') ? "\u21b6" : "U";
            title = Label(card.rectTransform, "Target", new Vector2(206, 24), new Vector2(-4, 24), 18);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 15;
            title.resizeTextMaxSize = 18;
            direction = Label(card.rectTransform, "Bearing and elevation", new Vector2(208, 21), new Vector2(-3, 1), 16);
            direction.resizeTextForBestFit = true;
            direction.resizeTextMinSize = 13;
            direction.resizeTextMaxSize = 16;
            state = Label(card.rectTransform, "Target state", new Vector2(208, 21), new Vector2(-3, -21), 16);
            range = Label(card.rectTransform, "Distance", new Vector2(72, 27), new Vector2(134, 8), 23, TextAnchor.MiddleRight);
            for (int i = 0; i < bars.Length; i++)
                bars[i] = Panel(card.rectTransform, "Range segment", new Vector2(15, 4 + 2 * i), new Vector2(125 + 18 * i, -17 + i), Evidence);

            pin = Panel(root, "Visible body pin", new Vector2(23, 23), Vector2.zero, Evidence);
            var inset = Panel(pin.rectTransform, "Pin inset", new Vector2(17, 17), Vector2.zero, Ink);
            pinCore = Panel(inset.rectTransform, "Pin center", new Vector2(7, 7), Vector2.zero, Evidence);
            pinCross = Panel(inset.rectTransform, "Revive cross", new Vector2(4, 13), Vector2.zero, Revive);
            edge = Label(root, "Off-screen target", new Vector2(34, 34), Vector2.zero, 25, TextAnchor.MiddleCenter);
            edge.text = "▲";
            var outline = edge.gameObject.AddComponent<Outline>();
            outline.effectColor = Ink;
            outline.effectDistance = new Vector2(2, -2);
            Hide();
        }

        public void Show(Camera eye, Vector3 target, string id, string label, Kind kind, float interactDistance,
            bool reachable, bool visible, bool choices, float reportDelay = 0)
        {
            if (eye != headingCamera)
            {
                headingCamera = eye;
                heading = eye.transform.eulerAngles.y;
                marker = default;
            }
            heading = ResolveHeading(heading, eye.transform.forward);
            var delta = target - eye.transform.position;
            var reading = Read(heading, delta, interactDistance, reachable, visible);
            bool turnAround = reading.Direction.StartsWith("TURN AROUND", System.StringComparison.Ordinal);
            Color accent = kind == Kind.Revive ? Revive : kind == Kind.Sheriff ? Sheriff : Evidence;
            card.gameObject.SetActive(true);
            bezel.color = arrow.color = pin.color = pinCore.color = pinCross.color = edge.color = accent;
            SetText(arrow, turnAround ? turnAroundGlyph : "\u25b2");
            arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, turnAround ? 0 : -reading.Bearing);
            SetText(title, label);
            SetText(direction, reading.Direction);
            bool ready = reachable && visible && reportDelay <= 0;
            state.color = !visible || reportDelay > 0 ? new Color32(255, 209, 146, 255) :
                ready ? new Color32(152, 255, 216, 255) : Ivory;
            SetText(state, reachable && visible && reportDelay > 0 ? ReportCountdown(reportDelay) : reading.State);
            SetText(range, reading.Distance < 1 ? "<1 m" : Mathf.RoundToInt(reading.Distance) + " m");
            for (int i = 0; i < bars.Length; i++)
                bars[i].color = i < reading.Bars ? accent : new Color32(78, 67, 96, 255);

            bool canPin = false, showEdge = false;
            bool inFront = eye.WorldToViewportPoint(target).z > eye.nearClipPlane;
            if (marker.Show(id, kind, visible && inFront, Time.unscaledTime) &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, eye.WorldToScreenPoint(target),
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : eye, out var local))
            {
                var safe = new Rect(-275, -112, 550, choices ? 147 : 254);
                canPin = ShouldPin(safe, local, pin.gameObject.activeSelf);
                if (canPin)
                {
                    pin.rectTransform.anchoredPosition = local;
                    pin.rectTransform.localRotation = Quaternion.Euler(0, 0, kind == Kind.Evidence ? 45 : 0);
                    pinCore.rectTransform.sizeDelta = kind == Kind.Revive ? new Vector2(13, 4) : new Vector2(7, 7);
                    pinCross.enabled = kind == Kind.Revive;
                }
                else
                {
                    showEdge = true;
                    edge.rectTransform.anchoredPosition = ClampRayToEdge(safe, local);
                    var offset = local - safe.center;
                    edge.rectTransform.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg);
                }
            }

            pin.gameObject.SetActive(canPin);
            edge.gameObject.SetActive(showEdge);
        }

        internal static float ResolveHeading(float previous, Vector3 forward)
        {
            float horizontalSquared = forward.x * forward.x + forward.z * forward.z;
            return horizontalSquared < .04f ? previous : Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        internal static bool ShouldPin(Rect safe, Vector2 projected, bool alreadyPinned)
        {
            if (!alreadyPinned)
                safe = Rect.MinMaxRect(safe.xMin + 8, safe.yMin + 8, safe.xMax - 8, safe.yMax - 8);
            return safe.Contains(projected);
        }

        internal static string ReportCountdown(float seconds) => "REPORT IN " + Mathf.CeilToInt(seconds) + " s";

        internal static Vector2 ClampRayToEdge(Rect bounds, Vector2 point)
        {
            var offset = point - bounds.center;
            float scale = Mathf.Max(Mathf.Abs(offset.x) / (bounds.width * .5f), Mathf.Abs(offset.y) / (bounds.height * .5f));
            return scale > 1 ? bounds.center + offset / scale : point;
        }

        internal static (float Bearing, string Direction, string State, float Distance, int Bars) Read(
            float yaw, Vector3 delta, float interactDistance, bool reachable, bool visible)
        {
            float distance = delta.magnitude;
            float horizontalSquared = delta.x * delta.x + delta.z * delta.z;
            float bearing = horizontalSquared < .04f ? 0 : Mathf.DeltaAngle(yaw, Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg);
            string turn = Mathf.Abs(bearing) >= 165 ? "TURN AROUND" :
                bearing > 110 ? "BACK RIGHT" : bearing < -110 ? "BACK LEFT" :
                bearing > 22 ? "RIGHT" : bearing < -22 ? "LEFT" : "AHEAD";
            string level = delta.y > 1.8f ? "ABOVE" : delta.y < -2.5f ? "BELOW" : "";
            if (level.Length > 0)
            {
                string height = level + " " + Mathf.RoundToInt(Mathf.Abs(delta.y)) + " m";
                if (horizontalSquared < 1)
                {
                    turn = height;
                    bearing = delta.y < 0 ? 180 : 0;
                }
                else turn += " · " + height;
            }

            bool nearby = distance <= interactDistance * NearbyMultiplier;
            string status = reachable && visible ? "IN REACH" : nearby ? "NEARBY" : "FAR";
            if (!visible) status = nearby ? "BLOCKED · NEARBY" : "BLOCKED";
            return (bearing, turn, status, distance, reachable && visible ? 3 : nearby ? 2 : 1);
        }

        public void Hide()
        {
            card.gameObject.SetActive(false);
            pin.gameObject.SetActive(false);
            edge.gameObject.SetActive(false);
            marker = default;
        }

        static void SetText(Text field, string text)
        {
            if (field.text != text) field.text = text;
        }

        static Image Panel(RectTransform parent, string name, Vector2 size, Vector2 position, Color color)
        {
            var image = Child<Image>(parent, name, size, position);
            image.sprite = CandyHudVisuals.Pill;
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        static Text Label(RectTransform parent, string name, Vector2 size, Vector2 position, int sizePx, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var text = Child<Text>(parent, name, size, position);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontStyle = FontStyle.Bold;
            text.fontSize = sizePx;
            text.alignment = anchor;
            text.color = Ivory;
            text.supportRichText = false;
            return text;
        }

        static T Child<T>(RectTransform parent, string name, Vector2 size, Vector2 position) where T : Graphic
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(T));
            child.transform.SetParent(parent, false);
            var graphic = child.GetComponent<T>();
            graphic.rectTransform.sizeDelta = size;
            graphic.rectTransform.anchoredPosition = position;
            graphic.raycastTarget = false;
            return graphic;
        }
    }

    // The selection follows a report ID as the feed changes, rather than the feed's row index.
    internal sealed class CandyMysteryReportFocus
    {
        string selectedId;
        bool wasTrackable;

        public MysteryBodyFrame Find(IReadOnlyList<MysteryBodyFrame> reports)
        {
            if (selectedId != null)
                foreach (var report in reports)
                    if (report.id == selectedId) return report;
            return null;
        }

        public bool RemoveUnavailable(IReadOnlyList<MysteryBodyFrame> reports)
        {
            if (selectedId == null) return false;
            var report = Find(reports);
            if (report != null && (!report.lost || !wasTrackable)) return false;
            Clear();
            return true;
        }

        public void Cycle(IReadOnlyList<MysteryBodyFrame> reports, int steps)
        {
            int count = 0, current = 0;
            foreach (var report in reports)
            {
                count++;
                if (report.id == selectedId) current = count;
            }

            int next = (current + steps % (count + 1)) % (count + 1);
            Clear();
            if (next == 0) return;
            foreach (var report in reports)
            {
                if (--next != 0) continue;
                selectedId = report.id;
                wasTrackable = !report.lost;
                return;
            }
        }

        public void Clear()
        {
            selectedId = null;
            wasTrackable = false;
        }
    }
}
