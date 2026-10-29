using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;

namespace DeadlyJenga.CastlePrototype
{
    // Pick your character design whenever the menu is open (lobby or match).
    // VR: a small panel beside the menu, look at an arrow and select. Desktop: a button in the menu header (CandyDemoHud).
    // Only reads and sets Core's PlayerLooks and HandLooks; Characters puts the designs on the bodies.
    public sealed class CharacterPicker : MonoBehaviour
    {
        static CharacterPicker instance;
        Canvas panel;
        readonly Text[] names = new Text[2];             // row 0 survivor, row 1 giant hand
        readonly Image[] arrows = new Image[4];          // previous/next per row
        readonly BoxCollider[] hits = new BoxCollider[4];
        bool shown;
        Vector3 side;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance) return;
            var go = new GameObject("Character picker");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CharacterPicker>();
        }

        void Awake() => Build();

void Build()
        {
            var go = new GameObject("Character picker panel", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            panel = go.AddComponent<Canvas>(); panel.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(440, 330); go.transform.localScale = Vector3.one * .0018f;
            var bg = go.AddComponent<Image>(); bg.sprite = CandyHudVisuals.Pill; bg.type = Image.Type.Sliced; bg.color = CandyHudVisuals.Ink;
            for (int row = 0; row < 2; row++)
            {
                float y = row == 0 ? 75 : -55;
                Label(row == 0 ? "SURVIVOR" : "GIANT HAND", new Vector2(0, y + 45), 20).color = CandyHudVisuals.Muted;
                names[row] = Label("", new Vector2(0, y), 32);
                for (int i = 0; i < 2; i++)
                {
                    int k = row * 2 + i;
                    var b = new GameObject(i == 0 ? "Previous" : "Next", typeof(RectTransform), typeof(Image), typeof(BoxCollider));
                    b.transform.SetParent(go.transform, false);
                    var r = (RectTransform)b.transform; r.sizeDelta = new Vector2(64, 64); r.anchoredPosition = new Vector2(i == 0 ? -175 : 175, y);
                    arrows[k] = b.GetComponent<Image>(); arrows[k].sprite = CandyHudVisuals.Pill; arrows[k].type = Image.Type.Sliced; arrows[k].color = CandyHudVisuals.Card;
                    hits[k] = b.GetComponent<BoxCollider>(); hits[k].size = new Vector3(64, 64, 4); hits[k].isTrigger = true;
                    var t = Label(i == 0 ? "<" : ">", Vector2.zero, 38); t.transform.SetParent(b.transform, false); t.rectTransform.anchoredPosition = Vector2.zero;
                }
            }
            Label("look at an arrow · select", new Vector2(0, -140), 17).color = CandyHudVisuals.Muted;
            go.SetActive(false);
        }

        Text Label(string value, Vector2 position, int size)
        {
            var go = new GameObject(value, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(panel.transform, false);
            var t = go.GetComponent<Text>(); t.text = value; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false;
            t.rectTransform.sizeDelta = new Vector2(300, 60); t.rectTransform.anchoredPosition = position;
            return t;
        }

void Pick(int row, int step)
        {
            if (row == 0) PlayerLooks.Chosen += step; else HandLooks.Chosen += step;
            var e = CandyAudioEngine.Instance;
            if (e && e.Listener) e.Play(CandySound.UiClick, e.Listener.position, .5f, GetInstanceID(), 1, true);
        }

void LateUpdate()
        {
            var session = Services.Session;
            var cam = session?.ActiveCamera;
            bool show = session != null && session.MenuOpen && session.IsVR && cam && PlayerLooks.Names.Length > 0;
            if (show != shown) { panel.gameObject.SetActive(show); shown = show; if (show) Place(cam, true); }
            if (!show) return;
            Place(cam, false);
            names[0].text = PlayerLooks.ChosenName;
            names[1].text = HandLooks.ChosenName;
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            for (int k = 0; k < 4; k++)
            {
                bool hover = hits[k].Raycast(ray, out _, 4);
                arrows[k].color = hover ? CandyHudVisuals.Mint : CandyHudVisuals.Card;
                if (hover && session.MenuSelectPressed) Pick(k / 2, k % 2 == 0 ? -1 : 1);
            }
        }

        // Sits to the right of the main menu and follows the player without turning with the head.
        void Place(Camera cam, bool opening)
        {
            if (opening)
            {
                var forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
                if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
                side = Quaternion.Euler(0, 42, 0) * forward.normalized;
            }
            var t = panel.transform;
            t.position = cam.transform.position + side * 1.5f + Vector3.down * .15f;
            t.rotation = Quaternion.LookRotation(side, Vector3.up);
            Physics.SyncTransforms();
        }


    }
}
