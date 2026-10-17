using System;
using Fusion;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

// The lobby's shared state: countdown to the game, plus the host's match rules (Core's MatchChoices)
// and the host's menu page and placement, copied to every player. Draws nothing.
public class LobbyCountdown : NetworkBehaviour
{
#if UNITY_EDITOR
    [SerializeField] UnityEditor.SceneAsset destinationScene;

    void OnValidate()
    {
        destinationScenePath = destinationScene == null
            ? string.Empty
            : UnityEditor.AssetDatabase.GetAssetPath(destinationScene);
    }
#endif
    // SceneAsset is editor-only; retain its path for headset builds.
    [SerializeField, HideInInspector] string destinationScenePath;

    int GetDestinationIndex()
    {
        int index = string.IsNullOrEmpty(destinationScenePath)
            ? -1
            : SceneUtility.GetBuildIndexByScenePath(destinationScenePath);
        if (index < 0)
            Debug.LogError("Assign a Destination Scene on Lobby Countdown and enable it in the build's Scene List.", this);
        return index;
    }

    [Networked] public TickTimer Countdown { get; set; }
    [Networked] public NetworkBool Finished { get; set; }
    public bool IsReady { get; private set; }

    [Networked] public LobbySettings Settings { get; set; }
    [Networked] public int SelectedPage { get; set; }
    [Networked] public Vector3 ScreenPosition { get; set; }
    [Networked] public Quaternion ScreenRotation { get; set; }

    public override void Spawned()
    {
        IsReady = true;
        if (Object.HasStateAuthority) CaptureScreen();
    }

    // Host: copy the current choices and the page/placement the menu published to LobbyConnection.
    void CaptureScreen()
    {
        Settings = LobbySettings.Capture();
        var connection = LobbyConnection.Current;
        if (!connection || !connection.HasPublishedScreen) return;
        SelectedPage = connection.PublishedPage;
        ScreenPosition = connection.PublishedPose.position;
        ScreenRotation = connection.PublishedPose.rotation;
    }

    // Everyone else: follow the host's choices.
    public override void Render()
    {
        if (!Object.HasStateAuthority) Settings.Apply();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        IsReady = false;
    }

    public void StartCountdown()
    {
        if (!IsReady || !Runner.IsServer || !Object.HasStateAuthority || Countdown.IsRunning || Finished)
            return;

        if (GetDestinationIndex() < 0)
            return;

        Countdown = TickTimer.CreateFromSeconds(Runner, 10f);
    }

    public void CancelCountdown()
    {
        if (!IsReady || !Runner.IsServer || !Object.HasStateAuthority || Finished)
            return;

        Countdown = TickTimer.None;
    }

    public bool TryGetLabel(out string label)
    {
        label = null;
        if (!IsReady || Runner == null || !Runner.IsRunning)
            return false;

        if (Finished)
        {
            label = "Starting…";
            return true;
        }

        if (!Countdown.IsRunning)
            return false;

        int seconds = Mathf.Clamp(Mathf.CeilToInt(Countdown.RemainingTime(Runner) ?? 0f), 0, 10);
        label = $"Starting in {seconds}";
        return true;
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority) CaptureScreen();
        if (!Runner.IsServer || !Object.HasStateAuthority || Finished || !Countdown.Expired(Runner))
            return;

        Countdown = TickTimer.None;
        int sceneIndex = GetDestinationIndex();
        if (sceneIndex < 0)
            return;

        Finished = true;
        var spawner = Runner.GetComponent<PlayerSpawner>();
        if (spawner != null)
            spawner.PrepareForSceneChange();
        Runner.LoadScene(SceneRef.FromIndex(sceneIndex), LoadSceneMode.Single);
    }
}

// Only shared match rules travel over Fusion. Comfort (movement, turning) and volume stay on each device.
public struct LobbySettings : INetworkStruct
{
    public int Population, Hands, SurvivorPreset, HandPreset, DrainCurve, RecoveryCurve;
    public float Capacity, Drain, Recovery, Delay, DrainDegree, RecoveryDegree;

    public static LobbySettings Capture()
    {
        var rules = MatchChoices.Rules;
        return new LobbySettings
        {
            Population = MatchChoices.BotTarget, Hands = MatchChoices.HandCount,
            SurvivorPreset = MatchChoices.SurvivorPreset, HandPreset = MatchChoices.HandPreset,
            Capacity = rules.gripSeconds, Drain = rules.drainRate, Recovery = rules.recoveryRate, Delay = rules.recoveryDelay,
            DrainDegree = rules.drainDegree, RecoveryDegree = rules.recoveryDegree,
            DrainCurve = Array.IndexOf(MatchOptions.Curves, rules.drainCurve),
            RecoveryCurve = Array.IndexOf(MatchOptions.Curves, rules.recoveryCurve)
        };
    }

    public void Apply()
    {
        var rules = MatchChoices.Rules;
        MatchChoices.BotTarget = Population; MatchChoices.HandCount = Hands;
        MatchChoices.SurvivorPreset = SurvivorPreset; MatchChoices.HandPreset = HandPreset;
        rules.gripSeconds = Capacity; rules.drainRate = Drain; rules.recoveryRate = Recovery; rules.recoveryDelay = Delay;
        rules.drainDegree = DrainDegree; rules.recoveryDegree = RecoveryDegree;
        if (DrainCurve >= 0 && DrainCurve < MatchOptions.Curves.Length) rules.drainCurve = MatchOptions.Curves[DrainCurve];
        if (RecoveryCurve >= 0 && RecoveryCurve < MatchOptions.Curves.Length) rules.recoveryCurve = MatchOptions.Curves[RecoveryCurve];
    }
}
