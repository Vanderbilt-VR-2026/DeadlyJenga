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
    string[] survivorNames, handNames;
    int page;
    bool placed;
    CandyAudioEngine audioEngine;
    Text message;
    void Awake()
    {
        survivorNames = Resources.LoadAll<CastleBotPersonality>("CastleBots").OrderBy(p => p.displayName).Select(p => p.displayName).ToArray();
        handNames = Resources.LoadAll<CastleHandBotPersonality>("CastleHandBots").OrderBy(p => p.displayName).Select(p => p.displayName).ToArray();
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
        MatchSetup.RefreshLocalControls();
        foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        Label("MATCH SETUP", 325);
        Button("Match", -190, 270, () => { page = 0; BuildPage(); });
        Button("Stamina", 0, 270, () => { page = 1; BuildPage(); });
        Button("Audio", 190, 270, () => { page = 2; BuildPage(); });
        if (page == 0)
        {
            Label("Host / Join uses Fusion, including on the same Wi-Fi.", 210, 16);
            Button("Solo practice", 0, 160, StartSolo);
            Button("Controls: " + MatchSetup.PlayStyle, -140, 100, () => { MatchSetup.TogglePlayStyle(); BuildPage(); }, 260);
            Button("Move: " + MatchSetup.Movement, 140, 100, () => { MatchSetup.Movement = MatchSetup.Movement == SurvivorVRMovement.HeadRelative ? SurvivorVRMovement.BodyRelative : SurvivorVRMovement.HeadRelative; BuildPage(); }, 260);
            Step("Population (host): " + MatchSetup.BotTarget, 35, () => MatchSetup.BotTarget = Mathf.Max(1, MatchSetup.BotTarget - 1), () => MatchSetup.BotTarget = MatchSetup.BotTarget + 1);
            Step("AI hands (host): " + MatchSetup.HandCount, -30, () => MatchSetup.HandCount = Mathf.Max(0, MatchSetup.HandCount - 1), () => MatchSetup.HandCount = MatchSetup.HandCount + 1);
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
    }
    void StartSolo()
    {
        if (NetworkRunner.Instances.Any(r => r != null && r.IsRunning))
        { message.text = "Leave the current room before starting solo practice."; return; }
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
    void Button(string title, float x, float y, Action action, float width = 175)
    {
        var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(content, false);
        var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(width, 45); rect.anchoredPosition = new Vector2(x, y);
        go.GetComponent<Image>().color = new Color(.22f, .24f, .34f);
        go.GetComponent<Button>().onClick.AddListener(() => action());
        var text = Label(title, 0, 16); text.transform.SetParent(go.transform, false); text.rectTransform.sizeDelta = new Vector2(width - 8, 42);
    }
}
