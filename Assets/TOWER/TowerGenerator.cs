using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class TowerGenerator : MonoBehaviour
{
    [SerializeField] GameObject blockPrefab;
    [SerializeField, Min(1)] int layers = 18;
    [SerializeField, Min(1)] int blocksPerLayer = 3;
    [SerializeField, Min(0f)] float gap = 0.002f;
    [SerializeField, Min(0.05f)] float blockScale = 1f;

    readonly List<Rigidbody> blocks = new();
    NetworkRunner runner;
    bool generated;

    public IReadOnlyList<Rigidbody> Blocks => blocks;
    public int Layers { get => layers; set => layers = Mathf.Max(1, value); }
    public int BlocksPerLayer { get => blocksPerLayer; set => blocksPerLayer = Mathf.Max(1, value); }
    public float BlockScale { get => blockScale; set => blockScale = Mathf.Max(0.05f, value); }


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

        this.runner = runner;
        generated = true;

        Vector3 scale = blockPrefab.transform.localScale * blockScale;
        Vector3 size = Vector3.Scale(box.size, scale);

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
                        spawned.transform.localScale = scale;
                        var body = spawned.GetComponent<Rigidbody>();
                        body.solverIterations = 30;
                        body.solverVelocityIterations = 10;
                        body.sleepThreshold = 0.01f;
                    });
                block.name = $"Block_L{layer}_{i}";
                blocks.Add(block.GetComponent<Rigidbody>());
            }
        }

        foreach (var b in blocks)
            b.Sleep();
    }

    // only the host can use this function.
    public bool Rebuild()
    {
        if (runner == null || !runner.IsServer)
        {
            Debug.LogWarning("Rebuild can only run on the host.", this);
            return false;
        }
        Clear();
        Generate(runner);
        return true;
    }

    void Clear()
    {
        foreach (var rb in blocks)
            if (rb != null && rb.TryGetComponent<NetworkObject>(out var no) && no.IsValid)
                runner.Despawn(no);
        blocks.Clear();
        generated = false;
    }
}