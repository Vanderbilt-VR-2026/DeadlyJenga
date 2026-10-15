using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

// in-headset menu for DevTools. left menu button (or F1 in editor) opens/closes it
[RequireComponent(typeof(DevTools))]
public class DevMenu : MonoBehaviour
{
    [SerializeField] float distance = 1.2f;
    [SerializeField] float followSpeed = 4f;

    DevTools tools;
    GameObject panel;
    TextMeshProUGUI summary;
    TextMeshProUGUI pullInfo;
    InputAction toggle;

    void Awake()
    {
        tools = GetComponent<DevTools>();

        toggle = new InputAction("DevMenu", InputActionType.Button);
        toggle.AddBinding("<XRController>{LeftHand}/menuButton");
        toggle.AddBinding("<Keyboard>/f1");
        toggle.performed += _ => Toggle();

        BuildPanel();
        panel.SetActive(false);
    }

    void OnEnable() => toggle.Enable();
    void OnDisable() => toggle.Disable();

    void OnDestroy()
    {
        toggle.Dispose();
        if (panel != null) Destroy(panel);
    }

    void Update()
    {
        if (!panel.activeSelf) return;
        summary.text = tools.Summary;
        pullInfo.text = $"{tools.PullSummary}\n{JengaBlock.LastTestResult}";
        FollowPlayer(false);
    }
    void Toggle()
    {
        bool open = !panel.activeSelf;
        panel.SetActive(open);
        if (open) FollowPlayer(true);
    }

    // keeps the panel in front of you
    void FollowPlayer(bool snap)
    {
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.01f) forward = cam.transform.up;
        forward.Normalize();

        float s = LocalRigTools.Scale;
        Vector3 pos = cam.transform.position + forward * (distance * s);
        Quaternion rot = Quaternion.LookRotation(forward);

        float t = snap ? 1f : 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        panel.transform.position = Vector3.Lerp(panel.transform.position, pos, t);
        panel.transform.rotation = Quaternion.Slerp(panel.transform.rotation, rot, t);
        panel.transform.localScale = Vector3.one * (0.0008f * s);
    }

    void BuildPanel()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));

        panel = new GameObject("DevMenu", typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
        panel.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        ((RectTransform)panel.transform).sizeDelta = new Vector2(1100, 650);

        var bg = AddChild(panel.transform, "Background").AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.8f);
        Stretch(bg.rectTransform);

        var cols = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
        cols.padding = new RectOffset(20, 20, 20, 20);
        cols.spacing = 20;
        cols.childControlWidth = cols.childControlHeight = true;
        cols.childForceExpandWidth = cols.childForceExpandHeight = true;

        // tower, player
        var left = Column(bg.transform);
        summary = Label(left, "", 26);
        Row(left, "Layers", () => tools.AddLayers(-1), () => tools.AddLayers(1));
        Row(left, "Width", () => tools.AddWidth(-1), () => tools.AddWidth(1));
        Row(left, "Block Scale", () => tools.AddBlockScale(-0.1f), () => tools.AddBlockScale(0.1f));
        Button(left, "Rebuild Tower (host)", tools.RebuildTower);
        Row(left, "Player Scale", () => tools.AddPlayerScale(-0.1f), () => tools.AddPlayerScale(0.1f));
        Button(left, "Reset Player Scale", tools.ResetPlayerScale);
        Button(left, "Respawn On Tower", tools.RespawnMeOnTower);
        Button(left, "Close", Toggle);

        // pull testing
        var right = Column(bg.transform);
        pullInfo = Label(right, "", 24);
        pullInfo.GetComponent<LayoutElement>().preferredHeight = 80;
        Row(right, "Layer", () => tools.SelectLayer(-1), () => tools.SelectLayer(1));
        Row(right, "Block", () => tools.SelectIndex(-1), () => tools.SelectIndex(1));
        Button(right, "Test Pull (host)", tools.TestPullSelected);
        Button(right, "Remove Block (host)", tools.RemoveSelected);
    }

    static Transform Column(Transform parent)
    {
        var col = AddChild(parent, "Column");
        var v = col.AddComponent<VerticalLayoutGroup>();
        v.spacing = 10;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandHeight = false;
        return col.transform;
    }

    // helpers
    static GameObject AddChild(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI Label(Transform parent, string text, float size)
    {
        var tmp = AddChild(parent, "Label").AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;
        return tmp;
    }

    static void Button(Transform parent, string text, UnityAction onClick)
    {
        var go = AddChild(parent, text);
        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.15f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        go.AddComponent<LayoutElement>().preferredHeight = 60;
        Stretch(Label(go.transform, text, 28).rectTransform);
    }

    static void Row(Transform parent, string name, UnityAction minus, UnityAction plus)
    {
        var row = AddChild(parent, name);
        row.AddComponent<LayoutElement>().preferredHeight = 60;
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 10;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = true;
        Button(row.transform, "-", minus);
        Label(row.transform, name, 28);
        Button(row.transform, "+", plus);
    }
}