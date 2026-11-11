using System;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    internal sealed class CandyDisasterWarningCard
    {
        static readonly System.Collections.Generic.Dictionary<string, Texture2D> Images = new();
        internal static Texture2D GetImage(string label)
        {
            var key=ImageKey(label);
            if(!Images.TryGetValue(key,out var image))
                Images[key]=image=Resources.Load<Texture2D>("DisasterWarnings/"+key);
            return image;
        }
        static string ImageKey(string label)
        {
            var key=new string((label??"").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            return key switch { "antigravity"=>"lowgravity", "planetkillermeteor"=>"meteorplanetkiller", _=>key };
        }

        static readonly Color32 Peach = new(255, 208, 136, 255);
        static readonly Color32 Ivory = new(255, 248, 237, 255);
        readonly Image border;
        readonly RawImage art;
        internal static string DisplayName(string label) => ImageKey(label) switch
        {
            "acidrain" => "Acid Rain",
            "lowgravity" => "Low Gravity",
            "meteorshower" => "Meteor Shower",
            "meteorplanetkiller" => "Planet Killer Meteor",
            "poweroutage" => "Power Outage",
            "timeslip" => "Time Slip",
            _ => CandyHudContent.Safe(label, 39)
        };

        readonly Text eyebrow, title, countdown;
        string currentLabel;
        int displayedSeconds;

        public CandyDisasterWarningCard(RectTransform parent)
        {
            border = Panel(parent, "Disaster warning", Peach, new Vector2(480, 108), new Vector2(0, 233), 0.8f);
            var background = Panel(border.rectTransform, "Background", new Color32(23, 21, 43, 255),
                new Vector2(477, 105), Vector2.zero, 0.8f);

            var thumbnail = Panel(background.rectTransform, "Art crop", Color.white,
                new Vector2(135, 90), new Vector2(-163, 0), 1.6f);
            thumbnail.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            art = Child<RawImage>(thumbnail.rectTransform, "Illustration", new Vector2(135, 90), Vector2.zero);

            eyebrow = Label(background.rectTransform, "DISASTER INCOMING", Peach, 15, new Vector2(217, 22), new Vector2(26, 17));
            title = Label(background.rectTransform, "", Ivory, 29, new Vector2(217, 40), new Vector2(26, -11));
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 18;
            title.resizeTextMaxSize = 29;
            countdown = Label(background.rectTransform, "", Peach, 27, new Vector2(77, 45), new Vector2(188, 0));
            countdown.alignment = TextAnchor.MiddleRight;
            Hide();
        }

        public void Show(string label, float seconds)
        {
            if (string.IsNullOrEmpty(label))
            {
                Hide();
                return;
            }

            if (!string.Equals(currentLabel, label, StringComparison.Ordinal))
            {
                currentLabel = label;
                art.texture = GetImage(label);
                bool illustrated = art.texture != null;
                art.transform.parent.gameObject.SetActive(illustrated);
                PlaceCopy(eyebrow, illustrated);
                PlaceCopy(title, illustrated);

                title.text = DisplayName(label);
            }

            int remaining = Mathf.CeilToInt(seconds);
            if (displayedSeconds != remaining)
            {
                displayedSeconds = remaining;
                countdown.text = remaining + " s";
            }

            if (!border.gameObject.activeSelf)
                border.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (border.gameObject.activeSelf)
                border.gameObject.SetActive(false);
        }

        static void PlaceCopy(Text text, bool illustrated)
        {
            var rect = text.rectTransform;
            rect.anchoredPosition = new Vector2(illustrated ? 26 : -50, rect.anchoredPosition.y);
            rect.sizeDelta = new Vector2(illustrated ? 217 : 369, rect.sizeDelta.y);
        }

        static Image Panel(RectTransform parent, string name, Color color, Vector2 size, Vector2 position, float cornerScale)
        {
            var image = Child<Image>(parent, name, size, position);
            image.color = color;
            image.sprite = CandyHudVisuals.Pill;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = cornerScale;
            return image;
        }

        static Text Label(RectTransform parent, string value, Color color, int fontSize, Vector2 size, Vector2 position)
        {
            var text = Child<Text>(parent, string.IsNullOrEmpty(value) ? "Value" : value, size, position);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontStyle = FontStyle.Bold;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            text.color = color;
            text.text = value;
            return text;
        }

        static T Child<T>(RectTransform parent, string name, Vector2 size, Vector2 position) where T : Graphic
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(T));
            child.transform.SetParent(parent, false);
            var graphic = child.GetComponent<T>();
            graphic.raycastTarget = false;
            graphic.rectTransform.sizeDelta = size;
            graphic.rectTransform.anchoredPosition = position;
            return graphic;
        }
    }
}
