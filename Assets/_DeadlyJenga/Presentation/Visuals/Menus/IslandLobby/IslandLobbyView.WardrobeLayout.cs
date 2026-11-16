using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobbyView
    {
        float wardrobeResetUntil;
        // Layout stays separate from appearance selection and compatibility rules.
        void PolishWardrobeLayout(bool desktop)
        {
            var root = desktop ? wardrobeDesktop : wardrobePanel;
            var panel = desktop ? (RectTransform)root.Find("Options") : root;
            bool hasColors = panel.Cast<Transform>().Any(t => t.name.StartsWith("Cosmetic shade ") || t.name.StartsWith("wardrobe-cosmetic-shade-"));
            if (desktop)
            {
                panel.anchoredPosition = new Vector2(250, 0);
                panel.sizeDelta = new Vector2(720, 760);
                PlaceWardrobeRect(wardrobePreview, -370, 32, 440, 440);
                var rotate = wardrobePreview.Find("Mirror rotation") as RectTransform;
                if (rotate) rotate.anchoredPosition = new Vector2(0, -185);
                WardrobeCaption(root, "Preview hint", "Drag your character to rotate", -370, -218, 450, 38, 20);
                DesktopButton(root, "Reset view", -268, -370, 210, 44, ResetWardrobeView, "Reset view");
                DesktopButton(root, "Player name", -328, -370, 420, 54, () =>
                {
                    SetWardrobeOpen(false);
                    DeadlyJenga.Presentation.NearbyRoomUi.OpenProfile();
                }, "Wardrobe identity");
                WardrobeCaption(root, "Preview heading", "YOUR LOOK", -370, 308, 440, 52, 32);
            }

            foreach (var text in panel.Cast<Transform>().Select(t => t.GetComponent<Text>()).Where(t => t))
            {
                if (text.text == "Wardrobe")
                    PlaceWardrobeRect(text.rectTransform, 0, desktop ? 338 : 380, desktop ? 700 : 1030, 65);
                else if (text.rectTransform.rect.height >= 60)
                    PlaceWardrobeRect(text.rectTransform, desktop ? 75 : 0, desktop ? -252 : -310,
                        desktop ? 520 : 1020, 76);
            }

            for (int i = 0; i < wardrobeCategories.Length; i++)
                MoveWardrobeControl(panel, desktop ? "Category " + i : "wardrobe-category-" + i,
                    desktop ? -278 : -410, (desktop ? 220 : 235) - i * (desktop ? 61 : 72),
                    desktop ? 164 : 190, desktop ? 51 : 60, desktop ? 21 : 28);
            for (int i = 0; i < WardrobePageSize; i++)
                MoveWardrobeControl(panel, desktop ? "Accessory " + i : "wardrobe-" + i,
                    desktop ? (i % 2 == 0 ? -44 : 194) : (i % 2 == 0 ? -110 : 300),
                    (hasColors ? (desktop ? 143 : 174) : (desktop ? 214 : 236)) - i / 2 *
                        (hasColors ? (desktop ? 53 : 70) : (desktop ? 66 : 83)),
                    desktop ? 224 : 390, desktop ? 45 : 59, desktop ? 21 : 28);

            MoveWardrobeControl(panel, desktop ? "Previous category" : "wardrobe-clear",
                desktop ? -75 : -110, desktop ? -183 : -242, desktop ? 150 : 240, 46, desktop ? 21 : 26);
            MoveWardrobeControl(panel, desktop ? "Next category" : "wardrobe-reset",
                desktop ? 225 : 300, desktop ? -183 : -242, desktop ? 150 : 240, 46, desktop ? 21 : 26);
            PlaceWardrobeRect((desktop ? desktopPageLabel : wardrobePageLabel).rectTransform,
                desktop ? 75 : 95, desktop ? -183 : -242, desktop ? 130 : 150, 44);
            MoveWardrobeControl(panel, desktop ? "Clear" : "wardrobe-clear-look",
                desktop ? -75 : -365, desktop ? -328 : -389, desktop ? 220 : 270, 58, desktop ? 22 : 27);
            MoveWardrobeControl(panel, desktop ? "Done" : "wardrobe-open",
                desktop ? 195 : 365, desktop ? -328 : -389, desktop ? 240 : 270, 58, desktop ? 26 : 30);
            if (!desktop)
                MoveWardrobeControl(panel, "wardrobe-player-name", 0, -389, 420, 58, 24);

            if (desktop)
            {
                int resetIndex = wardrobeButtons.FindIndex(b => b.Item1.name == "Clear");
                if (resetIndex >= 0) wardrobeButtons[resetIndex] = System.Tuple.Create(wardrobeButtons[resetIndex].Item1, (System.Action)ConfirmWardrobeReset);
            }
            else
            {
                var reset = CandyChoice("wardrobe-clear-look");
                reset.action = ConfirmWardrobeReset;
                reset.title = WardrobeResetTitle;
            }

            WardrobeCaption(panel, "Wardrobe section", "CHOOSE YOUR LOOK", desktop ? 75 : 95,
                desktop ? 270 : 300, desktop ? 520 : 800, 42, desktop ? 27 : 34);
            // Optional shade controls are supplied by the wardrobe feature, not this layout.
            foreach (var child in panel.Cast<Transform>().ToArray())
            {
                if (!child.name.StartsWith("Cosmetic shade ") && !child.name.StartsWith("wardrobe-cosmetic-shade-")) continue;
                var suffix = child.name.Substring(child.name.LastIndexOf(' ') + 1);
                if (!int.TryParse(suffix, out int index))
                    int.TryParse(child.name.Substring(child.name.LastIndexOf('-') + 1), out index);
                MoveWardrobeControl(panel, child.name, (desktop ? -90 : -170) + index * (desktop ? 110 : 174),
                    desktop ? 211 : 240, desktop ? 100 : 164, desktop ? 36 : 44, desktop ? 17 : 23);
            }
            foreach (var text in panel.GetComponentsInChildren<Text>(true))
            {
                if (text.text == "Cosmetic shade")
                {
                    text.text = "COLOR";
                    PlaceWardrobeRect(text.rectTransform, desktop ? 75 : 95, desktop ? 239 : 270,
                        desktop ? 470 : 790, 22);
                    text.fontSize = desktop ? 14 : 18;
                }
            }
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                text.fontStyle = FontStyle.Normal;
                text.resizeTextMinSize = Mathf.Min(text.fontSize, desktop ? 18 : 24);
                text.resizeTextMaxSize = text.fontSize;
                text.color = text.color == Color.white ? CandyCream : text.color;
            }
        }

        string WardrobeResetTitle() => Time.unscaledTime < wardrobeResetUntil ? "Confirm reset" : "Reset outfit";

        void ConfirmWardrobeReset()
        {
            if (Time.unscaledTime < wardrobeResetUntil)
            {
                ClearAccessories();
                wardrobeResetUntil = 0;
            }
            else wardrobeResetUntil = Time.unscaledTime + 4;
        }

        void RefreshWardrobeLayout()
        {
            foreach (var root in new[] { wardrobePanel, wardrobeDesktop ? wardrobeDesktop.Find("Options") as RectTransform : null })
            {
                if (!root) continue;
                var section = root.Find("Wardrobe section");
                if (section) section.GetComponent<Text>().text = wardrobeCategories[wardrobeCategory].ToUpperInvariant();
            }
            if (!wardrobeDesktop) return;
            var reset = wardrobeDesktop.Find("Options/Clear");
            if (reset) reset.GetComponentInChildren<Text>().text = WardrobeResetTitle();
        }

        void MoveWardrobeControl(RectTransform root, string name, float x, float y, float width, float height, int size)
        {
            var rect = root.Find(name) as RectTransform;
            if (!rect) return;
            PlaceWardrobeRect(rect, x, y, width, height);
            var collider = rect.GetComponent<BoxCollider>();
            if (collider) collider.size = new Vector3(width, height, 8);
            var text = rect.GetComponentInChildren<Text>(true);
            if (text)
            {
                PlaceWardrobeRect(text.rectTransform, 0, 0, width - 24, height - 8);
                text.fontSize = size;
            }
        }

        static void PlaceWardrobeRect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        Text WardrobeCaption(RectTransform root, string name, string title, float x, float y, float width, float height, int size)
        {
            var text = Label(root, title, y, size, width, height);
            text.name = name;
            text.rectTransform.anchoredPosition = new Vector2(x, y);
            text.color = CandyMuted;
            return text;
        }
    }
}
