using Fusion;
using UnityEngine;

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined
{
    [SerializeField] NetworkObject playerPrefab;

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
