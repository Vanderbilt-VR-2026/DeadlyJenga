using System.Collections.Generic;
using UnityEngine;

public class TowerGenerator : MonoBehaviour
{
    [SerializeField] GameObject blockPrefab;
    [SerializeField, Min(1)] int layers = 18;
    [SerializeField, Min(1)] int blocksPerLayer = 3;
    [SerializeField, Min(0f)] float gap = 0.002f;

    readonly List<Rigidbody> blocks = new();

    public IReadOnlyList<Rigidbody> Blocks => blocks;
    public int BlocksPerLayer => blocksPerLayer;

    void Start()
    {
        Generate();
    }

    public void Generate()
    {
        var box = blockPrefab.GetComponent<BoxCollider>();
        Vector3 size = Vector3.Scale(box.size, blockPrefab.transform.localScale);

        float pitch = size.x + gap;
        float center = (blocksPerLayer - 1) * 0.5f;

        for (int layer = 0; layer < layers; layer++)
        {
            bool odd = layer % 2 == 1;
            float y = size.y * (layer + 0.5f);
            Quaternion rot = transform.rotation * Quaternion.Euler(0f, odd ? 90f : 0f, 0f);

            for (int i = 0; i < blocksPerLayer; i++)
            {
                float offset = (i - center) * pitch;
                Vector3 local = odd ? new Vector3(0f, y, offset) : new Vector3(offset, y, 0f);

                var block = Instantiate(blockPrefab, transform.TransformPoint(local), rot, transform);
                block.name = $"Block_L{layer}_{i}";
                var rb = block.GetComponent<Rigidbody>();
                rb.solverIterations = 30;
                rb.solverVelocityIterations = 10;
                rb.sleepThreshold = 0.01f;
                blocks.Add(rb);
            }
        }

        foreach (var b in blocks)
            b.Sleep();
    }
}