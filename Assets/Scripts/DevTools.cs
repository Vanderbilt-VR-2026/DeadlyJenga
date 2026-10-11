using System.Collections;
using UnityEngine;

// dev only tools. Not networked, and tower rebuild only works on the host
public class DevTools : MonoBehaviour
{
    [Header("Tower (host only)")]
    [SerializeField, Min(1)] int layers = 18;
    [SerializeField, Min(1)] int blocksPerLayer = 3;
    [SerializeField, Min(0.05f)] float blockScale = 1f;
    [SerializeField] bool respawnAfterRebuild = true;

    [Header("Local player (only affects you)")]
    [SerializeField, Range(0.1f, 5f)] float playerScale = 1f;

    [SerializeField] TowerGenerator tower;

    public static DevTools Instance { get; private set; }

    float appliedScale = -1f;

    void Awake()
    {
        Instance = this;
        if (tower == null) tower = FindFirstObjectByType<TowerGenerator>();
        if (tower != null)
        {
            layers = tower.Layers;
            blocksPerLayer = tower.BlocksPerLayer;
            blockScale = tower.BlockScale;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // adjusts players scale live
        if (!Mathf.Approximately(playerScale, appliedScale) && LocalRigTools.FindRig() != null)
        {
            LocalRigTools.Scale = playerScale;
            appliedScale = playerScale;
        }
    }

    [ContextMenu("Rebuild Tower (host)")]
    public void RebuildTower()
    {
        if (tower == null) return;
        tower.Layers = layers;
        tower.BlocksPerLayer = blocksPerLayer;
        tower.BlockScale = blockScale;

        if (tower.Rebuild() && respawnAfterRebuild)
            StartCoroutine(RespawnAfterDelay());
    }

    [ContextMenu("Respawn Me On Tower")]
    public void RespawnMeOnTower() => LocalRigTools.RespawnOnTower(tower);

    [ContextMenu("Respawn Me At Spawn Point")]
    public void RespawnMeAtSpawn() => LocalRigTools.RespawnAtSpawnPoint();

    [ContextMenu("Reset Player Scale")]
    public void ResetPlayerScale() => playerScale = 1f;

    public void AddLayers(int delta) => layers = Mathf.Max(1, layers + delta);
    public void AddWidth(int delta) => blocksPerLayer = Mathf.Max(1, blocksPerLayer + delta);
    public void AddBlockScale(float delta) => blockScale = Mathf.Max(0.05f, blockScale + delta);
    public void AddPlayerScale(float delta) => playerScale = Mathf.Clamp(playerScale + delta, 0.1f, 5f);

    public string Summary =>
        $"Tower {layers}x{blocksPerLayer}  block x{blockScale:0.00}  player x{playerScale:0.00}";

    IEnumerator RespawnAfterDelay()
    {
        // waits for the new blocks to spawn first
        yield return new WaitForSeconds(0.5f);
        LocalRigTools.RespawnOnTower(tower);
    }
}