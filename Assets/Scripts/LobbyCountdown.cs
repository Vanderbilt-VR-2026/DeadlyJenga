using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

// Add alongside a NetworkObject in the scene containing the lobby UI.
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

    public override void Spawned() => IsReady = true;

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
