using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;

// The lobby's main menu: Host, Join (with a code keypad), Solo practice, match rules, stamina and audio.
// Connection actions go to Services.Lobby (GameSession); choices go to MatchChoices (Core); volume and
// sound go to CandyAudioControls (Presentation's own audio). Once connected, the host's page, placement
// and rules are shown to everyone; only the host can change them. Draws only; decides nothing about the match.
public class LobbyMatchSetup : MonoBehaviour
{
    static float masterVolume = .8f, musicVolume = .22f;
    static MatchRules Rules => MatchChoices.Rules;
    static ILobbyControls Lobby => Services.Lobby;
    Transform content;
    RectTransform joinPanel;
    string[] survivorNames, handNames;
    int page;
    bool placed;
    Text message, roomStatus;
    LobbyRoomCode room;
    bool wasConnected, wasEditable = true;
    string shownChoices = "";

    bool Connected => Lobby != null && Lobby.Connected;
    bool Connecting => Lobby != null && Lobby.Connecting;
    bool CanEdit => Lobby == null || Lobby.CanEdit;

    void Awake()
    {
        // Option names come from Characters through Core.
        survivorNames = MatchOptions.SurvivorPersonalities();
        handNames = MatchOptions.HandPersonalities();
        if (survivorNames.Length == 0) survivorNames = new[] { "Default" };
        if (handNames.Length == 0) handNames = new[] { "Playful" };
        var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        GetComponent<RectTransform>().sizeDelta = new Vector2(1100, 820);
        transform.localScale = Vector3.one * .0015f;
        gameObject.AddComponent<Image>().color = new Color(.08f, .09f, .14f, .96f);
        gameObject.AddComponent<GraphicRaycaster>();
        gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        content = new GameObject("Page", typeof(RectTransform)).transform; content.SetParent(transform, false);
        ((RectTransform)content).anchoredPosition = new Vector2(-240, 0);
        BuildPage();
    }

    void Update()
    {
        if (!room) room = FindFirstObjectByType<LobbyRoomCode>();
        if (!joinPanel && room && room.CodeInput) BuildJoinPanel();
        if (joinPanel) joinPanel.gameObject.SetActive(!Connected && page == 0);

        bool connected = Connected, editable = CanEdit;
        bool changed = connected != wasConnected || editable != wasEditable;
        if (connected && !wasConnected && page >= 2) page = 0;
        if (Lobby != null && Lobby.HasSharedScreen)
        {
            // Follow the host: its page and placement. Its rules already arrive in MatchChoices.
            changed |= page != Lobby.SharedPage;
            page = Lobby.SharedPage;
            var pose = Lobby.SharedScreenPose;
            if (pose.rotation != default) transform.SetPositionAndRotation(pose.position, pose.rotation);
        }
        else if (editable && Lobby != null)
            Lobby.PublishScreen(page, new Pose(transform.position, transform.rotation));
        string choices = ChoicesText();
        changed |= choices != shownChoices;
        shownChoices = choices;
        wasConnected = connected; wasEditable = editable;
        if (changed) BuildPage();

        if (message && Lobby != null)
        {
            message.text = Lobby.Status;
            message.color = Lobby.StatusIsError ? new Color(1f, .55f, .4f) : Color.white;
        }
        if (joinPanel)
            foreach (var button in joinPanel.GetComponentsInChildren<Button>()) button.interactable = !Connecting;
        if (room && room.CodeInput) room.CodeInput.interactable = !Connecting;
        if (roomStatus) roomStatus.text = connected && Lobby != null ? Lobby.RoomLabel : "MATCH SETUP";
    }

    static string ChoicesText() =>
        JsonUtility.ToJson(Rules) + MatchChoices.BotTarget + "/" + MatchChoices.HandCount + "/" + MatchChoices.SurvivorPreset + "/" + MatchChoices.HandPreset;

    void LateUpdate()
    {
        if (placed || (Connected && !CanEdit) || Camera.main == null) return;
        var camera = Camera.main.transform;
        var forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
        transform.SetPositionAndRotation(camera.position + forward * 1.6f, Quaternion.LookRotation(forward, Vector3.up));
        placed = true;
    }

    void BuildPage()
    {
        foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        roomStatus = Label("MATCH SETUP", 325);
        Button("Match", -190, 270, () => { page = 0; BuildPage(); });
        Button("Stamina", 0, 270, () => { page = 1; BuildPage(); });
        if (!Connected) Button("Audio", 190, 270, () => { page = 2; BuildPage(); });
        if (page == 0)
        {
            if (!Connected)
            {
                Button("Host", -140, 210, () => Lobby?.Host(), 260);
                Button("Join", 140, 210, Join, 260);
                Button("Solo practice", 0, 160, StartSolo);
                // Comfort settings stay on this device.
                Button("Turn: " + Rules.turn, -140, 100, () => { Rules.turn = MatchOptions.Next(MatchOptions.Turns, Rules.turn); BuildPage(); }, 260);
                Button("Move: " + Rules.movement, 140, 100, () => { Rules.movement = MatchOptions.Next(MatchOptions.Movements, Rules.movement); BuildPage(); }, 260);
            }
            else
            {
                Label("Shared match rules", 210, 18);
                Label(CanEdit ? "You control the room" : "Host controls the room — view only", 160, 18);
            }
            Step("Population (host): " + MatchChoices.BotTarget, 35, () => MatchChoices.BotTarget = Mathf.Max(1, MatchChoices.BotTarget - 1), () => MatchChoices.BotTarget = Mathf.Min(20, MatchChoices.BotTarget + 1));
            Step("AI hands (host): " + MatchChoices.HandCount, -30, () => MatchChoices.HandCount = Mathf.Max(0, MatchChoices.HandCount - 1), () => MatchChoices.HandCount = Mathf.Min(9, MatchChoices.HandCount + 1));
            Button("Survivors: " + survivorNames[MatchChoices.SurvivorPreset % survivorNames.Length], 0, -100, () => { MatchChoices.SurvivorPreset = (MatchChoices.SurvivorPreset + 1) % survivorNames.Length; BuildPage(); }, 530);
            Button("Hands: " + handNames[MatchChoices.HandPreset % handNames.Length], 0, -160, () => { MatchChoices.HandPreset = (MatchChoices.HandPreset + 1) % handNames.Length; BuildPage(); }, 530);
            Label("Population includes you, survivor bots and AI hands.\nAI hands use available population slots.", -235, 17);
        }
        else if (page == 1)
        {
            Step("Grip capacity: " + Rules.gripSeconds.ToString("0.0"), 205, () => Rules.gripSeconds = Mathf.Max(.5f, Rules.gripSeconds - .5f), () => Rules.gripSeconds = Mathf.Min(20, Rules.gripSeconds + .5f));
            Step("Drain / sec: " + Rules.drainRate.ToString("0.00"), 145, () => Rules.drainRate = Mathf.Max(0, Rules.drainRate - .25f), () => Rules.drainRate = Mathf.Min(4, Rules.drainRate + .25f));
            Step("Recovery / sec: " + Rules.recoveryRate.ToString("0.00"), 85, () => Rules.recoveryRate = Mathf.Max(0, Rules.recoveryRate - .05f), () => Rules.recoveryRate = Mathf.Min(2, Rules.recoveryRate + .05f));
            Step("Recovery delay: " + Rules.recoveryDelay.ToString("0.00"), 25, () => Rules.recoveryDelay = Mathf.Max(0, Rules.recoveryDelay - .05f), () => Rules.recoveryDelay = Mathf.Min(5, Rules.recoveryDelay + .05f));
            Button("Drain curve: " + Rules.drainCurve, 0, -40, () => { Rules.drainCurve = MatchOptions.Next(MatchOptions.Curves, Rules.drainCurve); BuildPage(); }, 530);
            Step("Drain degree: " + Rules.drainDegree.ToString("0.0"), -100, () => Rules.drainDegree = Mathf.Max(1, Rules.drainDegree - .5f), () => Rules.drainDegree = Mathf.Min(6, Rules.drainDegree + .5f));
            Button("Recovery curve: " + Rules.recoveryCurve, 0, -160, () => { Rules.recoveryCurve = MatchOptions.Next(MatchOptions.Curves, Rules.recoveryCurve); BuildPage(); }, 530);
            Step("Recovery degree: " + Rules.recoveryDegree.ToString("0.0"), -220, () => Rules.recoveryDegree = Mathf.Max(1, Rules.recoveryDegree - .5f), () => Rules.recoveryDegree = Mathf.Min(6, Rules.recoveryDegree + .5f));
        }
        else
        {
            Step("Master: " + masterVolume.ToString("0.00"), 170, () => Mix(-.05f, false), () => Mix(.05f, false), true);
            Step("Music: " + musicVolume.ToString("0.00"), 100, () => Mix(-.05f, true), () => Mix(.05f, true), true);
            Button("Preview sound", 0, 25, Preview, 300, true);
            Button("Open desktop sound studio", 0, -50, Studio, 530, true);
            Label("The full sound editor uses the desktop Game view.\nVolume and preview controls above work in VR.", -130, 17);
        }
        message = Label("", -310, 16);
        if (Connected)
        {
            Button("Start", -190, -365, () => Lobby?.StartMatch());
            Button("Cancel", 0, -365, () => Lobby?.CancelMatch());
            Button(CanEdit ? "Close room" : "Leave room", 190, -365, () => Lobby?.Leave(), 175, true);
        }
    }

    // Puts the scene's room-code field on this canvas with an on-screen keypad (works without a keyboard in VR).
    void BuildJoinPanel()
    {
        joinPanel = new GameObject("Join code and keyboard", typeof(RectTransform)).GetComponent<RectTransform>();
        joinPanel.SetParent(transform, false);
        joinPanel.anchoredPosition = new Vector2(320, 0);
        joinPanel.sizeDelta = new Vector2(400, 700);
        var input = room.CodeInput;
        input.transform.SetParent(joinPanel, false);
        var field = input.GetComponent<RectTransform>();
        field.anchorMin = field.anchorMax = field.pivot = new Vector2(.5f, .5f);
        field.localScale = Vector3.one;
        field.localRotation = Quaternion.identity;
        field.sizeDelta = new Vector2(360, 60);
        field.anchoredPosition = new Vector2(0, 210);
        input.gameObject.SetActive(true);
        input.characterLimit = 4;
        input.onValueChanged.AddListener(value => input.SetTextWithoutNotify(value.ToUpperInvariant()));
        var saved = content;
        content = joinPanel;
        Label("JOIN WITH A CODE", 290, 24);
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        for (int i = 0; i < alphabet.Length; i++)
        {
            string letter = alphabet[i].ToString();
            Button(letter, -150 + (i % 5) * 75, 130 - (i / 5) * 52, () => { if (input.text.Length < 4) input.text += letter; }, 65);
        }
        Button("Delete", -95, -260, () => { if (input.text.Length > 0) input.text = input.text.Substring(0, input.text.Length - 1); });
        Button("Join room", 95, -260, Join);
        Label("Type or use the buttons above", -320, 17);
        content = saved;
    }

    void Join() { if (room) room.Join(); }

    void StartSolo()
    {
        if (Lobby == null) { message.text = "The lobby connection is not ready yet."; return; }
        Lobby.TryStartSolo(out _);
    }
    void Mix(float delta, bool music)
    {
        if (music) musicVolume = Mathf.Clamp01(musicVolume + delta);
        else masterVolume = Mathf.Clamp01(masterVolume + delta);
        EnsureAudio();
    }
    void EnsureAudio()
    {
        CandyAudioControls.MasterVolume = masterVolume;
        CandyAudioControls.MusicVolume = musicVolume;
    }
    void Preview() { EnsureAudio(); CandyAudioControls.PlayUiClick(transform.position); }
    void Studio() { EnsureAudio(); CandyAudioControls.OpenStudio(); }

    void Step(string title, float y, Action less, Action more, bool localAction = false)
    {
        Label(title, y, 18);
        Button("−", -245, y, () => { less(); BuildPage(); }, 65, localAction);
        Button("+", 245, y, () => { more(); BuildPage(); }, 65, localAction);
    }
    Text Label(string title, float y, int size = 24)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(content, false);
        var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.text = title;
        text.raycastTarget = false; text.rectTransform.sizeDelta = new Vector2(560, 55); text.rectTransform.anchoredPosition = new Vector2(0, y);
        return text;
    }
    // Shared-room buttons only work for whoever controls the room; local ones (audio, leave) always work.
    void Button(string title, float x, float y, Action action, float width = 175, bool localAction = false)
    {
        var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(content, false);
        var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(width, 45); rect.anchoredPosition = new Vector2(x, y);
        go.GetComponent<Image>().color = new Color(.22f, .24f, .34f);
        var button = go.GetComponent<Button>();
        button.interactable = localAction || CanEdit;
        button.onClick.AddListener(() => { if (localAction || CanEdit) action(); });
        var text = Label(title, 0, 16); text.transform.SetParent(go.transform, false); text.rectTransform.sizeDelta = new Vector2(width - 8, 42);
    }
}
