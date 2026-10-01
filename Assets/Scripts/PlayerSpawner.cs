using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined, IPlayerLeft
{
    [SerializeField] NetworkObject playerPrefab;
    readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new();
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
        if (spawnedPlayers.ContainsKey(player))
            return;

        var avatar = Runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, player);
        if (avatar != null)
            spawnedPlayers.Add(player, avatar);
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (!Runner.IsServer || !spawnedPlayers.TryGetValue(player, out var avatar))
            return;

        spawnedPlayers.Remove(player);
        if (avatar != null)
            Runner.Despawn(avatar);
    }
}
