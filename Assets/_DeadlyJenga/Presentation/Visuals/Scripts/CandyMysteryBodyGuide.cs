using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    internal sealed class CandyMysteryBodyGuide
    {
        static readonly Color32 Ink = new(24, 21, 42, 249);
        static readonly Color32 Ivory = new(255, 247, 233, 255);
        internal static readonly Color32 Evidence = new(255, 204, 127, 255);
        internal static readonly Color32 Sheriff = new(141, 192, 255, 255);
        internal static readonly Color32 Revive = new(133, 240, 207, 255);

        readonly RectTransform root;
        readonly Canvas canvas;
        readonly Image card, bezel, dial, pin, pinCore;
        readonly Text arrow, title, detail, range;
        readonly Image[] bars = new Image[3];

        public CandyMysteryBodyGuide(RectTransform root)
        {
            this.root = root;
            canvas = root.GetComponent<Canvas>();
            card = Panel(root, "Body signal tracker", new Vector2(340, 74), new Vector2(0, -172), Ink);
            bezel = Panel(card.rectTransform, "Signal accent", new Vector2(4, 46), new Vector2(-157, 0), Evidence);
            dial = Panel(card.rectTransform, "Bearing dial", new Vector2(43, 43), new Vector2(-126, 0), new Color32(54, 44, 76, 255));
            arrow = Label(dial.rectTransform, "Direction", new Vector2(35, 35), Vector2.zero, 29, TextAnchor.MiddleCenter);
            arrow.text = "▲";
            title = Label(card.rectTransform, "Target", new Vector2(202, 25), new Vector2(0, 13), 18);
            detail = Label(card.rectTransform, "Bearing and visibility", new Vector2(204, 22), new Vector2(1, -12), 14);
            range = Label(card.rectTransform, "Distance", new Vector2(60, 27), new Vector2(127, 9), 21, TextAnchor.MiddleRight);
            for (int i = 0; i < bars.Length; i++)
                bars[i] = Panel(card.rectTransform, "Range segment", new Vector2(13, 4 + 2 * i), new Vector2(112 + 17 * i, -16 + i), Evidence);

            pin = Panel(root, "Visible body pin", new Vector2(23, 23), Vector2.zero, Evidence);
            pin.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            var inset = Panel(pin.rectTransform, "Pin inset", new Vector2(17, 17), Vector2.zero, Ink);
            pinCore = Panel(inset.rectTransform, "Pin center", new Vector2(7, 7), Vector2.zero, Evidence);
            Hide();
        }

        public void Show(Camera eye, Vector3 target, string label, Color accent, float interactDistance, bool reachable, bool visible, bool choices)
        {
            var delta = target - eye.transform.position;
            float distance = delta.magnitude;
            float height = delta.y;
            var flat = new Vector3(delta.x, 0, delta.z);
            var bearing = flat.sqrMagnitude < .04f ? 0 : Mathf.DeltaAngle(eye.transform.eulerAngles.y, Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg);
            string turn = Mathf.Abs(bearing) > 145 ? "BEHIND" : bearing > 22 ? "RIGHT" : bearing < -22 ? "LEFT" : "AHEAD";
            string level = height > 1.8f ? "ABOVE" : height < -2.5f ? "BELOW" : "";
            if (flat.sqrMagnitude < 1 && level.Length > 0)
            {
                turn = level;
                bearing = height < 0 ? 180 : 0;
                level = "";
            }

            bool nearby = distance <= interactDistance * 2.5f;
            string status = reachable ? "IN REACH" : nearby ? "NEARBY" : "FAR";
            string bearingText = level.Length > 0 ? turn + " / " + level : turn;
            if (!visible) status = "BLOCKED";

            card.gameObject.SetActive(true);
            bezel.color = arrow.color = pin.color = pinCore.color = accent;
            arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -bearing);
            SetText(title, label);
            SetText(detail, reachable ? status : bearingText + " · " + status);
            SetText(range, distance < 1 ? "<1 m" : Mathf.RoundToInt(distance) + " m");
            int filled = reachable ? 3 : nearby ? 2 : 1;
            for (int i = 0; i < bars.Length; i++)
                bars[i].color = i < filled ? accent : new Color32(78, 67, 96, 255);

            var viewport = eye.WorldToViewportPoint(target);
            var eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : eye;
            bool canPin = false;
            if (visible && viewport.z > 0 &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, eye.WorldToScreenPoint(target), eventCamera, out var local) &&
                local.x > -275 && local.x < 275 && local.y > -116 && local.y < (choices ? 35 : 142))
            {
                pin.rectTransform.anchoredPosition = local;
                canPin = true;
            }
            pin.gameObject.SetActive(canPin);
        }

        public void Hide()
        {
            card.gameObject.SetActive(false);
            pin.gameObject.SetActive(false);
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
}
