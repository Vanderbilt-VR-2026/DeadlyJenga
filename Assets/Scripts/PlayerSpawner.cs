using Fusion;
using UnityEngine;

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined
{
    [SerializeField] NetworkObject playerPrefab;
    NetworkEvents networkEvents;
    LocalHandInput localHands;
    bool warnedMissingHands;

    void OnEnable()
    {
        networkEvents = GetComponent<NetworkEvents>();
        if (networkEvents == null)
            networkEvents = gameObject.AddComponent<NetworkEvents>();
        networkEvents.OnInput.AddListener(CollectInput);
        GetComponent<NetworkRunner>().ProvideInput = true;
    }

    void OnDisable()
    {
        if (networkEvents != null)
            networkEvents.OnInput.RemoveListener(CollectInput);
    }

    void CollectInput(NetworkRunner runner, NetworkInput input)
    {
        if (localHands == null)
            localHands = LocalHandInput.FindOrCreate();
        if (localHands != null && localHands.TryRead(out var pose))
        {
            input.Set(pose);
            warnedMissingHands = false;
        }
        else if (!warnedMissingHands)
        {
            Debug.LogWarning("No local hand poses are available. Check the local XR rig and LocalHandInput references.", this);
            warnedMissingHands = true;
        }
    }

    public void PlayerJoined(PlayerRef player)
    {
        if (Runner.IsServer == false)
        {
            return;
        }

        Vector3 spawnPosition = new Vector3(player.PlayerId * 1.5f, 0f, 0f);
        Runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, player);
    }
}
