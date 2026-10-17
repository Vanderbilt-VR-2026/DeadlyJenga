using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;

// The lobby's match setup panel. Choices go to MatchChoices (Core); solo practice goes to Services.Lobby;
// volume and sound go to CandyAudioControls (Presentation's own audio). Draws only; decides nothing about the match.
public class LobbyMatchSetup : MonoBehaviour
{
    static float masterVolume = .8f, musicVolume = .22f;
    static MatchRules Rules => MatchChoices.Rules;
    Transform content;
    string[] survivorNames, handNames;
    int page;
    bool placed;
    Text message;
    void Awake()
    {
        // Option names come from Characters through Core.
        survivorNames = MatchOptions.SurvivorPersonalities();
        handNames = MatchOptions.HandPersonalities();
        if (survivorNames.Length == 0) survivorNames = new[] { "Default" };
        if (handNames.Length == 0) handNames = new[] { "Playful" };
        var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        GetComponent<RectTransform>().sizeDelta = new Vector2(600, 740);
        transform.localScale = Vector3.one * .0015f;
        gameObject.AddComponent<Image>().color = new Color(.08f, .09f, .14f, .96f);
        gameObject.AddComponent<GraphicRaycaster>();
        gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        content = new GameObject("Page", typeof(RectTransform)).transform; content.SetParent(transform, false);
        BuildPage();
    }
    void LateUpdate()
    {
        if (placed || Camera.main == null) return;
        var camera = Camera.main.transform;
        var forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
        transform.SetPositionAndRotation(camera.position + forward * 1.6f + camera.right * .8f,
            Quaternion.LookRotation(forward, Vector3.up));
        placed = true;
    }
    void BuildPage()
    {
        foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        Label("MATCH SETUP", 325);
        Button("Match", -190, 270, () => { page = 0; BuildPage(); });
        Button("Stamina", 0, 270, () => { page = 1; BuildPage(); });
        Button("Audio", 190, 270, () => { page = 2; BuildPage(); });
        if (page == 0)
        {
            Label("Host / Join uses Fusion, including on the same Wi-Fi.", 210, 16);
            Button("Solo practice", 0, 160, StartSolo);
            Button("Turn: " + Rules.turn, -140, 100, () => { Rules.turn = MatchOptions.Next(MatchOptions.Turns, Rules.turn); BuildPage(); }, 260);
            Button("Move: " + Rules.movement, 140, 100, () => { Rules.movement = MatchOptions.Next(MatchOptions.Movements, Rules.movement); BuildPage(); }, 260);
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
            Step("Master: " + masterVolume.ToString("0.00"), 170, () => Mix(-.05f, false), () => Mix(.05f, false));
            Step("Music: " + musicVolume.ToString("0.00"), 100, () => Mix(-.05f, true), () => Mix(.05f, true));
            Button("Preview sound", 0, 25, Preview, 300);
            Button("Open desktop sound studio", 0, -50, Studio, 530);
            Label("The full sound editor uses the desktop Game view.\nVolume and preview controls above work in VR.", -130, 17);
        }
        message = Label("", -310, 16);
    }
    void StartSolo()
    {
        if (Services.Lobby == null) { message.text = "The lobby connection is not ready yet."; return; }
        if (!Services.Lobby.TryStartSolo(out var problem)) message.text = problem;
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
    void Button(string title, float x, float y, Action action, float width = 175)
    {
        var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(content, false);
        var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(width, 45); rect.anchoredPosition = new Vector2(x, y);
        go.GetComponent<Image>().color = new Color(.22f, .24f, .34f);
        go.GetComponent<Button>().onClick.AddListener(() => action());
        var text = Label(title, 0, 16); text.transform.SetParent(go.transform, false); text.rectTransform.sizeDelta = new Vector2(width - 8, 42);
    }
}
