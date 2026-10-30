using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Fires emotes for the local player (survivor emotes, or taunts while playing the giant hand).
    // Desktop: number keys 1-8. VR: hold Y on the left controller, look at an emote, let go.
    public sealed class EmoteWheel : MonoBehaviour
    {
        static EmoteWheel instance;
        readonly InputAction vrButton = new InputAction("Emote wheel", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");
        Canvas wheel;
        readonly Image[] backs = new Image[8];
        readonly Text[] labels = new Text[8];
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
        }

        void Awake() => Build();
        void OnEnable() => vrButton.Enable();
        void OnDisable() => vrButton.Disable();
        void OnDestroy() => vrButton.Dispose();

        void Build()
        {
            var go = new GameObject("Emote wheel panel", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            wheel = go.AddComponent<Canvas>(); wheel.renderMode = RenderMode.WorldSpace;
            ((RectTransform)go.transform).sizeDelta = new Vector2(560, 560); go.transform.localScale = Vector3.one * .0015f;
            for (int i = 0; i < 8; i++)
            {
                var slot = new GameObject("Slot " + i, typeof(RectTransform), typeof(Image));
                slot.transform.SetParent(go.transform, false);
                ((RectTransform)slot.transform).sizeDelta = new Vector2(170, 70);
                backs[i] = slot.GetComponent<Image>(); backs[i].sprite = CandyHudVisuals.Pill; backs[i].type = Image.Type.Sliced;
                labels[i] = Text(slot.transform, "", 22);
            }
            var c = new GameObject("Center", typeof(RectTransform), typeof(Image));
            c.transform.SetParent(go.transform, false); ((RectTransform)c.transform).sizeDelta = new Vector2(150, 150);
            var ci = c.GetComponent<Image>(); ci.sprite = CandyHudVisuals.Pill; ci.type = Image.Type.Sliced; ci.color = CandyHudVisuals.Ink;
            center = Text(c.transform, "", 18);
            go.SetActive(false);
        }

        static Text Text(Transform parent, string value, int size)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
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
            set = session.PlayingHand ? Emotes.Hand : Emotes.Survivor;

            var k = Keyboard.current;
            if (k != null && !session.MenuOpen && !session.IsVR)
                for (int i = 0; i < set.Length && i < 9; i++)
                    if (k[Key.Digit1 + i].wasPressedThisFrame) Emotes.Request(i);

            var cam = session.ActiveCamera;
            if (!session.IsVR || session.MenuOpen || !cam) { Close(); return; }
            bool held = vrButton.IsPressed();
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
            t.position = cam.transform.position + cam.transform.forward * .7f;
            t.rotation = Quaternion.LookRotation(t.position - cam.transform.position, Vector3.up);
            int n = set.Length;
            for (int i = 0; i < 8; i++)
            {
                bool used = i < n;
                backs[i].gameObject.SetActive(used);
                if (!used) continue;
                float a = (90 - i * 360f / n) * Mathf.Deg2Rad;
                ((RectTransform)backs[i].transform).anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 195;
                labels[i].text = EmoteText.Label(set[i]);
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
                if (local.magnitude > 80)
                {
                    int n = set.Length;
                    float angle = Mathf.Repeat(90 - Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg, 360);
                    hovered = Mathf.RoundToInt(angle / (360f / n)) % n;
                }
            }
            for (int i = 0; i < set.Length && i < 8; i++) backs[i].color = i == hovered ? CandyHudVisuals.Mint : CandyHudVisuals.Card;
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
