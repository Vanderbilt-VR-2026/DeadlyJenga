using UnityEngine;

public class TowerGenerator : MonoBehaviour
{
    [SerializeField] private GameObject blockPrefab;

    [Header("Tower Shape")]
    [SerializeField, Min(1)] private int layers = 18;
    [SerializeField, Min(1)] private int blocksPerLayer = 3;
    [SerializeField, Min(0.01f)] private float blockScale = 3f;

    [Header("Spacing (as a fraction of block size)")]
    [SerializeField, Min(0f)] private float gap = 0.06f;
    [SerializeField, Min(0f)] private float layerGap = 0.01f;

    [Header("Player Spawn")]
    [SerializeField] private Transform xrOrigin;
    [SerializeField] private bool spawnOnTop = true;
    [SerializeField, Min(0f)] private float spawnLift = 0.05f;

    private void Start()
    {
        if (spawnOnTop) PlacePlayerOnTop();
    }

    private void GetDimensions(out float height, out float width, out float vertGap, out float sideGap)
    {
        Vector3 size = blockPrefab.transform.GetChild(0).localScale * blockScale;
        height = size.y;
        width = size.z;
        vertGap = height * layerGap;
        sideGap = width * gap;
    }

    public Vector3 GetTopPosition()
    {
        GetDimensions(out float height, out _, out float vertGap, out _);
        float topY = (layers - 1) * (height + vertGap) + height;
        return transform.TransformPoint(new Vector3(0f, topY, 0f));
    }

    [ContextMenu("Place Player On Top")]
    public void PlacePlayerOnTop()
    {
        if (xrOrigin == null || blockPrefab == null)
        {
            Debug.LogWarning("TowerGenerator: assign the XR Origin and block prefab to spawn on top.", this);
            return;
        }

        var cc = xrOrigin.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        xrOrigin.position = GetTopPosition() + Vector3.up * spawnLift;

        if (cc != null) cc.enabled = true;
    }

    [ContextMenu("Generate Tower")]
    public void Generate()
    {
        if (blockPrefab == null)
        {
            Debug.LogError("TowerGenerator: no block prefab assigned.", this);
            return;
        }

        Clear();

        GetDimensions(out float height, out float width, out float vertGap, out float sideGap);

        for (int i = 0; i < layers; i++)
        {
            float y = i * (height + vertGap) + height / 2f;
            Quaternion rot = (i % 2 == 0) ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);

            for (int j = 0; j < blocksPerLayer; j++)
            {
                float offset = (j - (blocksPerLayer - 1) / 2f) * (width + sideGap);

                GameObject block = Instantiate(blockPrefab, transform);
                block.name = $"Block_L{i}_{j}";
                block.transform.localPosition = rot * new Vector3(0f, 0f, offset) + Vector3.up * y;
                block.transform.localRotation = rot;
                block.transform.localScale = blockPrefab.transform.localScale * blockScale;
            }
        }
    }

    [ContextMenu("Clear Tower")]
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }
}