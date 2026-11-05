using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Fires emotes for the local player (survivor emotes, or taunts while playing the giant hand).
    // Desktop: number keys 1-0. VR: hold the left stick click, look at an emote, let go.
    public sealed class EmoteWheel : MonoBehaviour
    {
        static EmoteWheel instance;
        readonly InputAction vrButton = new InputAction("Emote wheel", InputActionType.Button, "<XRController>{LeftHand}/primary2DAxisClick");
        Canvas wheel;
        const int SlotCount = 10;
        readonly Image[] backs = new Image[SlotCount];
        readonly Image[] icons = new Image[SlotCount];
        readonly Text[] labels = new Text[SlotCount];
        readonly System.Collections.Generic.Dictionary<string, Sprite> sprites = new();
        static readonly Key[] shortcuts = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0 };
        Text center;
        bool open;
        int hovered = -1;
        string[] set = Emotes.Survivor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance) return;
            var go = new GameObject("Emote wheel");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<EmoteWheel>();
            go.AddComponent<EmoteOrbit>();
        }

        void Awake() => Build();
        void OnEnable() => vrButton.Enable();
        void OnDisable() { vrButton.Disable(); Close(); }
        void OnDestroy()
        {
            vrButton.Dispose();
            foreach (var sprite in sprites.Values)
            {
                Destroy(sprite.texture);
                Destroy(sprite);
            }
        }

        void Build()
        {
            var go = new GameObject("Emote wheel panel", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            wheel = go.AddComponent<Canvas>(); wheel.renderMode = RenderMode.WorldSpace;
            ((RectTransform)go.transform).sizeDelta = new Vector2(560, 560); go.transform.localScale = Vector3.one * .0015f;
            go.layer = 5;
            for (int i = 0; i < SlotCount; i++)
            {
                var slot = new GameObject("Slot " + i, typeof(RectTransform), typeof(Image));
                slot.transform.SetParent(go.transform, false);
                slot.layer = 5;
                ((RectTransform)slot.transform).sizeDelta = new Vector2(132, 54);
                backs[i] = slot.GetComponent<Image>(); backs[i].sprite = CandyHudVisuals.Pill; backs[i].type = Image.Type.Sliced;
                var icon = new GameObject("Pose icon", typeof(RectTransform), typeof(Image));
                icon.layer = 5;
                icon.transform.SetParent(slot.transform, false);
                icons[i] = icon.GetComponent<Image>();
                icons[i].raycastTarget = false;
                icons[i].rectTransform.sizeDelta = new Vector2(39, 39);
                icons[i].rectTransform.anchoredPosition = new Vector2(-42, 0);
                labels[i] = Text(slot.transform, "", 17);
                labels[i].rectTransform.sizeDelta = new Vector2(85, 50);
                labels[i].rectTransform.anchoredPosition = new Vector2(21, 0);
            }
            var c = new GameObject("Center", typeof(RectTransform), typeof(Image));
            c.layer = 5;
            c.transform.SetParent(go.transform, false); ((RectTransform)c.transform).sizeDelta = new Vector2(150, 150);
            var ci = c.GetComponent<Image>(); ci.sprite = CandyHudVisuals.Pill; ci.type = Image.Type.Sliced; ci.color = CandyHudVisuals.Ink;
            center = Text(c.transform, "", 18);
            go.SetActive(false);
        }

        static Text Text(Transform parent, string value, int size)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>(); t.text = value; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false;
            t.rectTransform.sizeDelta = new Vector2(170, 70);
            return t;
        }

        void Update()
        {
            var session = Services.Session;
            if (session == null) { Close(); return; }
            var choices = session.PlayingHand ? Emotes.Hand : Emotes.Survivor;
            if (choices != set) { Close(); set = choices; }

            var k = Keyboard.current;
            if (k != null && !session.MenuOpen && !session.IsVR)
                for (int i = 0; i < set.Length && i < shortcuts.Length; i++)
                    if (k[shortcuts[i]].wasPressedThisFrame) Emotes.Request(i);

            var cam = session.ActiveCamera;
            if (!session.IsVR || session.MenuOpen || !cam) { Close(); return; }
            bool held = vrButton.IsPressed();
            if (open && wheel.worldCamera != cam) Close();
            if (held && !open) Open(cam);
            if (!open) return;
            Hover(cam);
            if (!held)
            {
                if (hovered >= 0) Emotes.Request(hovered);
                Close();
            }
        }

        void Open(Camera cam)
        {
            open = true; hovered = -1;
            var t = wheel.transform;
            wheel.worldCamera = cam;
            t.position = cam.transform.position + cam.transform.forward * .85f;
            t.rotation = Quaternion.LookRotation(t.position - cam.transform.position, Vector3.up);
            int n = set.Length;
            for (int i = 0; i < SlotCount; i++)
            {
                bool used = i < n;
                backs[i].gameObject.SetActive(used);
                if (!used) continue;
                float a = (90 - i * 360f / n) * Mathf.Deg2Rad;
                backs[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a) * 238, Mathf.Sin(a) * 220);
                labels[i].text = EmoteText.Label(set[i]);
                if (!sprites.TryGetValue(set[i], out var sprite))
                    sprites.Add(set[i], sprite = EmoteIcons.Create(set[i]));
                icons[i].sprite = sprite;
            }
            center.text = "look + let go";
            wheel.gameObject.SetActive(true);
        }

        void Hover(Camera cam)
        {
            var t = wheel.transform;
            var plane = new Plane(-t.forward, t.position);
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            hovered = -1;
            if (plane.Raycast(ray, out float d))
            {
                Vector2 local = ((RectTransform)t).InverseTransformPoint(ray.GetPoint(d));
                for (int i = 0; i < set.Length && i < SlotCount; i++)
                {
                    var slot = backs[i].rectTransform;
                    if (!slot.rect.Contains(local - slot.anchoredPosition)) continue;
                    hovered = i;
                    break;
                }
            }
            for (int i = 0; i < set.Length && i < SlotCount; i++)
            {
                bool selected = i == hovered;
                backs[i].color = selected ? CandyHudVisuals.Mint : CandyHudVisuals.Card;
                icons[i].color = labels[i].color = selected ? CandyHudVisuals.Ink : Color.white;
            }
            center.text = hovered >= 0 ? EmoteText.Label(set[hovered]) : "look + let go";
        }

        void Close()
        {
            if (!open) return;
            open = false; hovered = -1;
            wheel.gameObject.SetActive(false);
        }
    }

    // Display text for every emote and taunt.
    public static class EmoteText
    {
        public static string Label(string emote) => emote switch
        {
            "FriendlyWave" => "Wave", "TotallyMeantThat" => "Meant That", "Phew" => "Phew", "WhatWasThat" => "What?!",
            "GolfClap" => "Clap", "JustChilling" => "Chill", "NopeNopeNope" => "Nope", "TinyVictoryDance" => "Victory",
            "Breakdance" => "Breakdance", "IrishDance" => "Irish Dance",
            "FingerWag" => "Finger Wag", "ThumbsDown" => "Boo", "SlowClap" => "Slow Clap", "PointAndLaugh" => "Laugh",
            _ => emote
        };

        public static string Bubble(string emote) => emote switch
        {
            "FriendlyWave" => "hi!  o/", "TotallyMeantThat" => "totally meant that", "Phew" => "phew...", "WhatWasThat" => "?!",
            "GolfClap" => "clap clap clap", "JustChilling" => "just chilling ~", "NopeNopeNope" => "NOPE NOPE NOPE", "TinyVictoryDance" => "\\o/  victory!",
            "FingerWag" => "tsk tsk tsk", "ThumbsDown" => "BOOOOO", "SlowClap" => "clap...   clap...", "PointAndLaugh" => "HA HA HA",
            _ => null
        };
    }
}
