using System;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyAudio;

public class LobbyMatchSetup : MonoBehaviour
{
    Transform content;
    RectTransform joinPanel;
    string[] survivorNames, handNames;
    int page;
    bool placed;
    CandyAudioEngine audioEngine;
    Text message, roomStatus;
    CanvasGroup visibility;
    LobbyRoomCode room;
    LobbyCountdown shared;
    bool wasConnected, wasEditable = true;
    LobbySettings shownSettings;
    public int Page => page;
    NetworkRunner ActiveRunner => NetworkRunner.Instances.FirstOrDefault(r => r && r.IsRunning);
    bool Connected => ActiveRunner != null;
    bool Connecting => room && room.GetComponent<FusionBootstrap>() && room.GetComponent<FusionBootstrap>().ConnectionBusy;
    bool CanEdit => !Connecting && (!Connected || (shared && shared.IsReady && shared.Object.HasStateAuthority));

    void Update()
    {
        if (!shared) shared = FindFirstObjectByType<LobbyCountdown>();
        if (!room) room = FindFirstObjectByType<LobbyRoomCode>();
        if (!joinPanel && room && room.CodeInput) BuildJoinPanel();
        if (joinPanel) joinPanel.gameObject.SetActive(!Connected && page == 0);
        bool show = true;
        visibility.alpha = show ? 1 : 0;
        visibility.interactable = show;
        visibility.blocksRaycasts = show;
        bool connected = Connected, editable = CanEdit;
        bool changed = connected != wasConnected || editable != wasEditable;
        if (connected && !wasConnected && page >= 2) page = 0;
        if (connected && shared && shared.IsReady && !editable)
        {
            shared.Settings.Apply();
            changed |= page != shared.SelectedPage || !shownSettings.Equals(shared.Settings);
            page = shared.SelectedPage;
            shownSettings = shared.Settings;
            transform.SetPositionAndRotation(shared.ScreenPosition, shared.ScreenRotation);
        }
        wasConnected = connected; wasEditable = editable;
        if (changed) BuildPage();
        if (message)
        {
            message.text = FusionBootstrap.ConnectionMessage;
            message.color = FusionBootstrap.ConnectionError ? new Color(1f, .55f, .4f) : Color.white;
        }
        if (joinPanel)
            foreach (var button in joinPanel.GetComponentsInChildren<Button>()) button.interactable = !Connecting;
        if (room && room.CodeInput) room.CodeInput.interactable = !Connecting;
        if (roomStatus)
        {
            var runner = ActiveRunner;
            roomStatus.text = runner ? "Room: " + runner.SessionInfo.Name : "MATCH SETUP";
            if (connected && shared && shared.TryGetLabel(out var label)) roomStatus.text = label;
        }
    }
    void Awake()
    {
        survivorNames = Resources.LoadAll<CastleBotPersonality>("CastleBots").OrderBy(p => p.displayName).Select(p => p.displayName).ToArray();
        handNames = Resources.LoadAll<CastleHandBotPersonality>("CastleHandBots").OrderBy(p => p.displayName).Select(p => p.displayName).ToArray();
        if (survivorNames.Length == 0) survivorNames = new[] { "Default" };
        if (handNames.Length == 0) handNames = new[] { "Playful" };
        visibility = gameObject.AddComponent<CanvasGroup>();
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
    void LateUpdate()
    {
        if (placed || (Connected && !CanEdit) || Camera.main == null) return;
        var camera = Camera.main.transform;
        var forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
        transform.SetPositionAndRotation(camera.position + forward * 1.6f,
            Quaternion.LookRotation(forward, Vector3.up));
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
                Button("Host", -140, 210, () => { if (room) room.Host(); }, 260);
                Button("Join", 140, 210, () => { if (room) room.Join(); }, 260);
                Button("Solo practice", 0, 160, StartSolo);
            }
            else
            {
                Label("Shared match rules", 210, 18);
                Label(CanEdit ? "You control the room" : "Host controls the room — view only", 160, 18);
            }
            if (!Connected) Button("Turn: " + MatchSetup.Turn, -140, 100, () => { MatchSetup.Turn = MatchSetup.Turn == SurvivorVRTurn.Snap ? SurvivorVRTurn.Smooth : SurvivorVRTurn.Snap; BuildPage(); }, 260);
            if (!Connected) Button("Move: " + MatchSetup.Movement, 140, 100, () => { MatchSetup.Movement = MatchSetup.Movement == SurvivorVRMovement.HeadRelative ? SurvivorVRMovement.BodyRelative : SurvivorVRMovement.HeadRelative; BuildPage(); }, 260);
            Step("Population (host): " + MatchSetup.BotTarget, 35, () => MatchSetup.BotTarget = Mathf.Max(1, MatchSetup.BotTarget - 1), () => MatchSetup.BotTarget = Mathf.Min(20, MatchSetup.BotTarget + 1));
            Step("AI hands (host): " + MatchSetup.HandCount, -30, () => MatchSetup.HandCount = Mathf.Max(0, MatchSetup.HandCount - 1), () => MatchSetup.HandCount = Mathf.Min(9, MatchSetup.HandCount + 1));
            Button("Survivors: " + survivorNames[MatchSetup.SurvivorPreset % survivorNames.Length], 0, -100, () => { MatchSetup.SurvivorPreset = (MatchSetup.SurvivorPreset + 1) % survivorNames.Length; BuildPage(); }, 530);
            Button("Hands: " + handNames[MatchSetup.HandPreset % handNames.Length], 0, -160, () => { MatchSetup.HandPreset = (MatchSetup.HandPreset + 1) % handNames.Length; BuildPage(); }, 530);
            Label("Population includes you, survivor bots and AI hands.\nAI hands use available population slots.", -235, 17);
        }
        else if (page == 1)
        {
            Step("Grip capacity: " + MatchSetup.GripSeconds.ToString("0.0"), 205, () => MatchSetup.GripSeconds = Mathf.Max(.5f, MatchSetup.GripSeconds - .5f), () => MatchSetup.GripSeconds = Mathf.Min(20, MatchSetup.GripSeconds + .5f));
            Step("Drain / sec: " + MatchSetup.DrainRate.ToString("0.00"), 145, () => MatchSetup.DrainRate = Mathf.Max(0, MatchSetup.DrainRate - .25f), () => MatchSetup.DrainRate = Mathf.Min(4, MatchSetup.DrainRate + .25f));
            Step("Recovery / sec: " + MatchSetup.RecoveryRate.ToString("0.00"), 85, () => MatchSetup.RecoveryRate = Mathf.Max(0, MatchSetup.RecoveryRate - .05f), () => MatchSetup.RecoveryRate = Mathf.Min(2, MatchSetup.RecoveryRate + .05f));
            Step("Recovery delay: " + MatchSetup.RecoveryDelay.ToString("0.00"), 25, () => MatchSetup.RecoveryDelay = Mathf.Max(0, MatchSetup.RecoveryDelay - .05f), () => MatchSetup.RecoveryDelay = Mathf.Min(5, MatchSetup.RecoveryDelay + .05f));
            Button("Drain curve: " + MatchSetup.DrainCurve, 0, -40, () => { MatchSetup.DrainCurve = (StaminaCurve)(((int)MatchSetup.DrainCurve + 1) % 4); BuildPage(); }, 530);
            Step("Drain degree: " + MatchSetup.DrainDegree.ToString("0.0"), -100, () => MatchSetup.DrainDegree = Mathf.Max(1, MatchSetup.DrainDegree - .5f), () => MatchSetup.DrainDegree = Mathf.Min(6, MatchSetup.DrainDegree + .5f));
            Button("Recovery curve: " + MatchSetup.RecoveryCurve, 0, -160, () => { MatchSetup.RecoveryCurve = (StaminaCurve)(((int)MatchSetup.RecoveryCurve + 1) % 4); BuildPage(); }, 530);
            Step("Recovery degree: " + MatchSetup.RecoveryDegree.ToString("0.0"), -220, () => MatchSetup.RecoveryDegree = Mathf.Max(1, MatchSetup.RecoveryDegree - .5f), () => MatchSetup.RecoveryDegree = Mathf.Min(6, MatchSetup.RecoveryDegree + .5f));
        }
        else
        {
            Step("Master: " + MatchSetup.MasterVolume.ToString("0.00"), 170, () => Mix(-.05f, false), () => Mix(.05f, false));
            Step("Music: " + MatchSetup.MusicVolume.ToString("0.00"), 100, () => Mix(-.05f, true), () => Mix(.05f, true));
            Button("Preview sound", 0, 25, Preview, 300);
            Button("Open desktop sound studio", 0, -50, Studio, 530);
            Label("The full sound editor uses the desktop Game view.\nVolume and preview controls above work in VR.", -130, 17);
        }
        message = Label("", -310, 16);
        if (Connected)
        {
            Button("Start", -190, -365, () => shared.StartCountdown());
            Button("Cancel", 0, -365, () => shared.CancelCountdown());
            Button(CanEdit ? "Close room" : "Leave room", 190, -365, Leave, 175, true);
        }
    }
    void BuildJoinPanel()
    {
        joinPanel = new GameObject("Join code and keyboard", typeof(RectTransform)).GetComponent<RectTransform>();
        joinPanel.SetParent(transform, false);
        joinPanel.anchoredPosition = new Vector2(320, 0);
        joinPanel.sizeDelta = new Vector2(400, 700);
        // Keep the existing TMP field and its scene wiring, but put it on this canvas.
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
            Button(letter, -150 + (i % 5) * 75, 130 - (i / 5) * 52,
                () => { if (input.text.Length < 4) input.text += letter; }, 65);
        }
        Button("Delete", -95, -260, () => { if (input.text.Length > 0) input.text = input.text.Substring(0, input.text.Length - 1); });
        Button("Join room", 95, -260, () => room.Join());
        Label("Type or use the buttons above", -320, 17);
        content = saved;
    }

    void Leave()
    {
        FusionBootstrap.MarkIntentionalLeave();
        var bootstrap = FindFirstObjectByType<FusionBootstrap>();
        if (bootstrap) bootstrap.Shutdown();
        else if (ActiveRunner) _ = ActiveRunner.Shutdown();
    }

    void StartSolo()
    {
        if (Connecting || NetworkRunner.Instances.Any(r => r != null && r.IsRunning))
        { FusionBootstrap.SetConnectionMessage("Wait for the connection to finish or leave the room before starting solo practice.", true); return; }
        MatchSetup.FromLobby = true; MatchSetup.StartLocalSession = false;
        SceneManager.LoadScene("Game");
    }
    void Mix(float delta, bool music)
    {
        if (music) MatchSetup.MusicVolume = Mathf.Clamp01(MatchSetup.MusicVolume + delta);
        else MatchSetup.MasterVolume = Mathf.Clamp01(MatchSetup.MasterVolume + delta);
        EnsureAudio();
    }
    void EnsureAudio()
    {
        audioEngine = CandyAudioEngine.Instance;
        if (!audioEngine || !audioEngine.bank) return;
        audioEngine.bank.master = MatchSetup.MasterVolume;
        audioEngine.bank.music = MatchSetup.MusicVolume;
    }

    void Preview() { EnsureAudio(); if (audioEngine) audioEngine.Play(CandySound.UiClick, transform.position, local: true); }
    void Studio()
    {
        EnsureAudio(); if (!audioEngine) return;
        var studio = audioEngine.GetComponent<CandyAudioStudio>() ?? audioEngine.gameObject.AddComponent<CandyAudioStudio>();
        studio.Open();
    }
    void Step(string title, float y, Action less, Action more)
    {
        Label(title, y, 18);
        Button("−", -245, y, () => { less(); BuildPage(); }, 65);
        Button("+", 245, y, () => { more(); BuildPage(); }, 65);
    }
    Text Label(string title, float y, int size = 24)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(content, false);
        var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.text = title;
        text.raycastTarget = false; text.rectTransform.sizeDelta = new Vector2(560, 55); text.rectTransform.anchoredPosition = new Vector2(0, y);
        return text;
    }
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
