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
    bool changingScene;
    bool placedLocalRig;
    [SerializeField, Min(0.1f)] float spawnSpacing = 1.5f;

    void OnEnable()
    {
        networkEvents = GetComponent<NetworkEvents>();
        if (networkEvents == null)
            networkEvents = gameObject.AddComponent<NetworkEvents>();
        networkEvents.OnInput.AddListener(CollectInput);
        networkEvents.OnSceneLoadStart.AddListener(SceneLoadStarted);
        networkEvents.OnSceneLoadDone.AddListener(SceneLoadFinished);
        GetComponent<NetworkRunner>().ProvideInput = true;
    }

    void OnDisable()
    {
        if (networkEvents != null)
        {
            networkEvents.OnInput.RemoveListener(CollectInput);
            networkEvents.OnSceneLoadStart.RemoveListener(SceneLoadStarted);
            networkEvents.OnSceneLoadDone.RemoveListener(SceneLoadFinished);
        }
    }

    public void PrepareForSceneChange()
    {
        changingScene = true;
        placedLocalRig = false;
        localHands = null;
        if (!Runner.IsServer) return;
        foreach (var avatar in spawnedPlayers.Values)
            if (avatar != null) Runner.Despawn(avatar);
        spawnedPlayers.Clear();
    }

    void SceneLoadStarted(NetworkRunner runner) => PrepareForSceneChange();

    void SceneLoadFinished(NetworkRunner runner)
    {
        changingScene = false;
        localHands = null;
        placedLocalRig = false;
        if (runner.IsServer)
            foreach (var player in runner.ActivePlayers)
                PlayerJoined(player);
    }

    bool TryGetSpawnPose(PlayerRef player, out Vector3 position, out Quaternion rotation)
    {
        var spawn = GameObject.Find("Spawn Points");
        position = Vector3.zero;
        rotation = Quaternion.identity;
        if (spawn == null) return false;

        // Stable slots let host and clients agree without depending on join-list order.
        int slot = Mathf.Max(0, player.PlayerId - 1);
        position = spawn.transform.position + spawn.transform.right * slot * spawnSpacing;
        rotation = spawn.transform.rotation;
        return true;
    }

    void CollectInput(NetworkRunner runner, NetworkInput input)
    {
        if (changingScene) return;
        if (localHands == null)
            localHands = LocalHandInput.FindOrCreate();
        if (localHands != null && !placedLocalRig && runner.LocalPlayer != PlayerRef.None)
        {
            if (TryGetSpawnPose(runner.LocalPlayer, out var position, out var rotation))
                localHands.MoveToSpawn(position, rotation);
            placedLocalRig = true;
        }
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
        if (Runner.IsServer == false || changingScene)
        {
            return;
        }

        Vector3 spawnPosition = new Vector3(player.PlayerId * 1.5f, 0f, 0f);
        Quaternion spawnRotation = Quaternion.identity;
        if (TryGetSpawnPose(player, out var position, out var rotation))
        {
            spawnPosition = position;
            spawnRotation = rotation;
        }
        if (spawnedPlayers.ContainsKey(player))
            return;

        var avatar = Runner.Spawn(playerPrefab, spawnPosition, spawnRotation, player);
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
