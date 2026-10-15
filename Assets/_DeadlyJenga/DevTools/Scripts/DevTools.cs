using System.Collections;
using UnityEngine;

// dev only tools. Not networked, and tower rebuild only works on the host
public class DevTools : MonoBehaviour
{
    [Header("Tower (host only)")]
    [SerializeField, Min(1)] int layers = 18;
    [SerializeField, Min(1)] int blocksPerLayer = 3;
    [SerializeField, Min(0.05f)] float blockScale = 1f;

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
        if (tower == null || !tower.CanRebuild) return;
        tower.Layers = layers;
        tower.BlocksPerLayer = blocksPerLayer;
        tower.BlockScale = blockScale;

        // only move the player if they're on or inside the tower area
        bool inTheWay = IsPlayerInTowerArea();
        if (inTheWay) MovePlayerAboveTower();

        if (tower.Rebuild() && inTheWay)
            StartCoroutine(RespawnAfterDelay());
    }

    // checks both the old and new tower size
    bool IsPlayerInTowerArea()
    {
        var cam = Camera.main;
        if (cam == null) return false;

        Vector3 local = tower.transform.InverseTransformPoint(cam.transform.position);
        float half = Mathf.Max(tower.HalfWidth, tower.PlannedHalfWidth) + LocalRigTools.Scale;
        return Mathf.Abs(local.x) <= half && Mathf.Abs(local.z) <= half;
    }

    // parks the player above where the new tower will be, so blocks don't spawn inside them
    void MovePlayerAboveTower()
    {
        var t = tower.transform;
        Vector3 above = t.position + Vector3.up * (tower.PlannedHeight + 2f * LocalRigTools.Scale);
        LocalRigTools.TeleportTo(above, t.rotation);
    }

    [ContextMenu("Respawn Me On Tower")]
    public void RespawnMeOnTower() => LocalRigTools.RespawnOnTower(tower);

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
        yield return new WaitForSeconds(0.5f);
        LocalRigTools.RespawnOnTower(tower);
    }

    int selLayer, selIndex;
    JengaBlock selected;
    MaterialPropertyBlock highlight;

    public void SelectLayer(int delta)
    {
        selLayer = Mathf.Clamp(selLayer + delta, 0, tower.Layers - 1);
        UpdateSelection();
    }

    public void SelectIndex(int delta)
    {
        selIndex = Mathf.Clamp(selIndex + delta, 0, tower.BlocksPerLayer - 1);
        UpdateSelection();
    }

    public void TestPullSelected() { if (selected != null) selected.TestPull(); }
    public void RemoveSelected() { if (selected != null) selected.Remove(); }

    public string PullSummary
    {
        get
        {
            if (selected == null) UpdateSelection();
            if (selected == null) return $"Layer {selLayer}  Block {selIndex}: none";
            float load = selected.GetLoad(out bool last);
            return $"Layer {selLayer}  Block {selIndex}  load {load:0.00}{(last ? "  LOCKED" : "")}";
        }
    }

    // turns the selected block yellow so you can see it in the headset
    void UpdateSelection()
    {
        if (selected != null) selected.GetComponentInChildren<Renderer>().SetPropertyBlock(null);
        selected = FindBlock(selLayer, selIndex);
        if (selected == null) return;

        highlight ??= new MaterialPropertyBlock();
        highlight.SetColor("_BaseColor", Color.yellow);
        highlight.SetColor("_Color", Color.yellow);
        selected.GetComponentInChildren<Renderer>().SetPropertyBlock(highlight);
    }

    JengaBlock FindBlock(int layer, int index)
    {
        if (tower == null) return null;
        foreach (var rb in tower.Blocks)
            if (rb != null && rb.TryGetComponent<JengaBlock>(out var b) && b.Layer == layer && b.Index == index)
                return b;
        return null;
    }
}