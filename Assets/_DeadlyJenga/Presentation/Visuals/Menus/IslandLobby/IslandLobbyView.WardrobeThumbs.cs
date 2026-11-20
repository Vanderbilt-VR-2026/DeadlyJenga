using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    // Shows the rendered picture of each wardrobe item on its button, in the player's current body colour.
    public sealed partial class IslandLobbyView
    {
        readonly Transform[] wardrobeItemButtons = new Transform[WardrobePageSize];

        static AvatarAppearance CatalogEntry(WardrobeSlot slot, WardrobeItem item)
        {
            var entry = new AvatarAppearance { accessories = item.accessory, clothing = item.clothing };
            if (item.accessory == 0 && item.clothing == 0)
            {
                if (slot == WardrobeSlot.Skin) entry.skin = (AvatarSkin)item.style;
                else if (slot == WardrobeSlot.Hair) entry.hair = (AvatarHair)item.style;
                else entry.eyewear = (AvatarEyewear)item.style;
            }
            return entry;
        }

        Color WardrobeBodyColor() =>
            wardrobePlayer?.gameObject.GetComponent<IModeActor>()?.Color ?? Color.white;

        void ShowWardrobeThumb(Transform button, int index)
        {
            if (!button) return;
            var picture = button.Find("Item picture")?.GetComponent<RawImage>();
            Texture2D texture = null;
            if (HasWardrobeItem(index))
                texture = WardrobeThumbnailLibrary.Load(wardrobeCategory, CatalogEntry((WardrobeSlot)wardrobeCategory, ItemAt(index)), WardrobeBodyColor());
            if (!texture) { if (picture) picture.enabled = false; return; }
            if (!picture)
            {
                var go = new GameObject("Item picture", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(button, false);
                go.layer = button.gameObject.layer;
                picture = go.GetComponent<RawImage>();
                picture.raycastTarget = false;
                var rect = picture.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
                rect.pivot = new Vector2(0, .5f);
                float height = ((RectTransform)button).rect.height;
                rect.sizeDelta = Vector2.one * Mathf.Max(24, height - 6);
                rect.anchoredPosition = new Vector2(6, 0);
            }
            picture.texture = texture;
            picture.enabled = true;
        }

        void RefreshWardrobeThumbs()
        {
            for (int i = 0; i < wardrobeItemButtons.Length; i++) ShowWardrobeThumb(wardrobeItemButtons[i], i);
            var options = wardrobeDesktop ? wardrobeDesktop.Find("Options") : null;
            if (options) for (int i = 0; i < WardrobePageSize; i++) ShowWardrobeThumb(options.Find("Accessory " + i), i);
        }
    }
}
