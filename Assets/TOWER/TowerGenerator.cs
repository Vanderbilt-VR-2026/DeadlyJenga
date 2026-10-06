using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class TowerGenerator : MonoBehaviour
{
    [SerializeField] GameObject blockPrefab;
    [SerializeField, Min(1)] int layers = 18;
    [SerializeField, Min(1)] int blocksPerLayer = 3;
    [SerializeField, Min(0f)] float gap = 0.002f;

    readonly List<Rigidbody> blocks = new();
    bool generated;

    public IReadOnlyList<Rigidbody> Blocks => blocks;
    public int BlocksPerLayer => blocksPerLayer;

    // Called by the persistent runner after Fusion finishes loading the scene.
    public void Generate(NetworkRunner runner)
    {
        if (runner == null || !runner.IsRunning || !runner.IsServer || generated)
            return;

        if (blockPrefab == null || !blockPrefab.TryGetComponent<NetworkObject>(out var networkPrefab)
            || !blockPrefab.TryGetComponent<BoxCollider>(out var box)
            || !blockPrefab.TryGetComponent<Rigidbody>(out _))
        {
            Debug.LogError("TowerGenerator needs a block prefab with NetworkObject, BoxCollider, and Rigidbody on its root.", this);
            return;
        }

        generated = true;
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

                var block = runner.Spawn(networkPrefab, transform.TransformPoint(local), rot,
                    onBeforeSpawned: (_, spawned) =>
                    {
                        var body = spawned.GetComponent<Rigidbody>();
                        body.solverIterations = 30;
                        body.solverVelocityIterations = 10;
                        body.sleepThreshold = 0.01f;
                    });
                block.name = $"Block_L{layer}_{i}";
                var rb = block.GetComponent<Rigidbody>();
                blocks.Add(rb);
            }
        }

        foreach (var b in blocks)
            b.Sleep();
    }
}
