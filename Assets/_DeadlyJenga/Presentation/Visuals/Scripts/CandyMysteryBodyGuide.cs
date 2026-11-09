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

        public CandyMysteryBodyGuide(RectTransform root)
        {
            this.root = root;
            canvas = root.GetComponent<Canvas>();
            card = Panel(root, "Body signal tracker", new Vector2(365, 84), new Vector2(0, -176), Ink);
            bezel = Panel(card.rectTransform, "Signal accent", new Vector2(4, 51), new Vector2(-169, 0), Evidence);
            var dial = Panel(card.rectTransform, "Bearing dial", new Vector2(46, 46), new Vector2(-139, 0), new Color32(54, 44, 76, 255));
            arrow = Label(dial.rectTransform, "Direction", new Vector2(40, 40), Vector2.zero, 31, TextAnchor.MiddleCenter);
            arrow.text = "▲";
            title = Label(card.rectTransform, "Target", new Vector2(206, 24), new Vector2(-4, 24), 18);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 15;
            title.resizeTextMaxSize = 18;
            direction = Label(card.rectTransform, "Bearing and elevation", new Vector2(208, 21), new Vector2(-3, 1), 16);
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

        public void Show(Camera eye, Vector3 target, string label, Kind kind, float interactDistance, bool reachable, bool visible, bool choices)
        {
            var delta = target - eye.transform.position;
            var reading = Read(eye.transform.eulerAngles.y, delta, interactDistance, reachable, visible);
            Color accent = kind == Kind.Revive ? Revive : kind == Kind.Sheriff ? Sheriff : Evidence;
            card.gameObject.SetActive(true);
            bezel.color = arrow.color = pin.color = pinCore.color = pinCross.color = edge.color = accent;
            arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -reading.Bearing);
            SetText(title, label);
            SetText(direction, reading.Direction);
            SetText(state, reading.State);
            SetText(range, reading.Distance < 1 ? "<1 m" : Mathf.RoundToInt(reading.Distance) + " m");
            for (int i = 0; i < bars.Length; i++)
                bars[i].color = i < reading.Bars ? accent : new Color32(78, 67, 96, 255);

            bool canPin = false, showEdge = false;
            if (visible && eye.WorldToViewportPoint(target).z > 0 &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, eye.WorldToScreenPoint(target),
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : eye, out var local))
            {
                var safe = new Rect(-275, -112, 550, choices ? 147 : 254);
                canPin = safe.Contains(local);
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
            string turn = Mathf.Abs(bearing) > 145 ? "BEHIND" : bearing > 22 ? "RIGHT" : bearing < -22 ? "LEFT" : "AHEAD";
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
