using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;

namespace DeadlyJenga.CastlePrototype
{
    // Pick your character design whenever the menu is open (lobby or match).
    // VR: a small panel beside the menu, look at an arrow and select. Desktop: a bar at the bottom.
    // Only reads and sets Core's PlayerLooks; Characters puts the design on the avatar.
    public sealed class CharacterPicker : MonoBehaviour
    {
        static CharacterPicker instance;
        Canvas panel;
        Text title;
        Image[] arrows = new Image[2];
        BoxCollider[] hits = new BoxCollider[2];
        bool shown;
        Vector3 side;
        GUIStyle barStyle, buttonStyle;

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
            var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(440, 190); go.transform.localScale = Vector3.one * .0018f;
            var bg = go.AddComponent<Image>(); bg.sprite = CandyHudVisuals.Pill; bg.type = Image.Type.Sliced; bg.color = CandyHudVisuals.Ink;
            Label("CHARACTER", new Vector2(0, 62), 20).color = CandyHudVisuals.Muted;
            title = Label("", new Vector2(0, 0), 34);
            Label("look at an arrow · select", new Vector2(0, -62), 17).color = CandyHudVisuals.Muted;
            for (int i = 0; i < 2; i++)
            {
                var b = new GameObject(i == 0 ? "Previous" : "Next", typeof(RectTransform), typeof(Image), typeof(BoxCollider));
                b.transform.SetParent(go.transform, false);
                var r = (RectTransform)b.transform; r.sizeDelta = new Vector2(70, 70); r.anchoredPosition = new Vector2(i == 0 ? -170 : 170, 0);
                arrows[i] = b.GetComponent<Image>(); arrows[i].sprite = CandyHudVisuals.Pill; arrows[i].type = Image.Type.Sliced; arrows[i].color = CandyHudVisuals.Card;
                hits[i] = b.GetComponent<BoxCollider>(); hits[i].size = new Vector3(70, 70, 4); hits[i].isTrigger = true;
                var t = Label(i == 0 ? "<" : ">", Vector2.zero, 40); t.transform.SetParent(b.transform, false); t.rectTransform.anchoredPosition = Vector2.zero;
            }
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

        void Pick(int step)
        {
            PlayerLooks.Chosen += step;
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
            title.text = PlayerLooks.ChosenName;
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            for (int i = 0; i < 2; i++)
            {
                bool hover = hits[i].Raycast(ray, out _, 4);
                arrows[i].color = hover ? CandyHudVisuals.Mint : CandyHudVisuals.Card;
                if (hover && session.MenuSelectPressed) Pick(i == 0 ? -1 : 1);
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

        void OnGUI()
        {
            var session = Services.Session;
            if (session == null || !session.MenuOpen || session.IsVR || PlayerLooks.Names.Length == 0) return;
            barStyle ??= new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            buttonStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 22 };
            float w = 420, h = 54, x = (Screen.width - w) * .5f, y = Screen.height - h - 16;
            GUI.Box(new Rect(x, y, w, h), "Character:  " + PlayerLooks.ChosenName, barStyle);
            if (GUI.Button(new Rect(x + 6, y + 6, 46, h - 12), "<", buttonStyle)) Pick(-1);
            if (GUI.Button(new Rect(x + w - 52, y + 6, 46, h - 12), ">", buttonStyle)) Pick(1);
        }
    }
}
