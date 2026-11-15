using System;
using Fusion;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Opt-in development probe. Does not replace or drive match state.
    public sealed class CastleNativeStateProbe : NetworkBehaviour
    {
        [Networked] public int Sequence { get; set; }
        [Networked, Capacity(256)] public NetworkArray<int> Payload => default;
        public static int Received { get; private set; }
        public static float MaxGap { get; private set; }
        public static double GapSum { get; private set; }
        int seen;
        float last;

        static int Value(int sequence, int index) => unchecked(sequence * 1103515245 + index * 12345);
        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;
            Sequence++;
            for (int i = 0; i < Payload.Length; i++) Payload.Set(i, Value(Sequence, i));
        }
        public override void Render()
        {
            if (Object.HasStateAuthority || Sequence == 0 || Sequence == seen) return;
            for (int i = 0; i < Payload.Length; i++)
                if (Payload[i] != Value(Sequence, i)) throw new InvalidOperationException("Native state probe was not atomic");
            float now = Time.unscaledTime;
            if (Received > 0) { float gap = now - last; GapSum += gap; MaxGap = Mathf.Max(MaxGap, gap); }
            Received++; last = now; seen = Sequence;
        }
        public static void Configure(NetworkRunner runner)
        {
            if (runner.GetComponent<CastleNativeStateProbeSetup>()) return;
            Received = 0; MaxGap = 0; GapSum = 0;
            runner.gameObject.AddComponent<CastleNativeStateProbeSetup>().Initialize(runner);
        }
    }

    internal sealed class CastleNativeStateProbeSetup : MonoBehaviour
    {
        NetworkRunner runner;
        NetworkObject template;
        NetworkPrefabId prefabId;
        float spawnAt;
        bool spawned;
        public void Initialize(NetworkRunner value)
        {
            runner = value;
            var root = new GameObject("Native state probe template");
            template = root.AddComponent<NetworkObject>();
            root.AddComponent<CastleNativeStateProbe>();
            new NetworkObjectBaker().Bake(root);
            var source = new NetworkPrefabSourceStatic {
                Object = template, AssetGuid = NetworkObjectGuid.Parse("403b32da57c84b078edbb5cc19b3dcb9")
            };
            if (!runner.Prefabs.TryAddSource(source, out prefabId))
                throw new InvalidOperationException("Native state probe registration failed");
            DontDestroyOnLoad(root);
            spawnAt = Time.unscaledTime + 3;
        }
        void Update()
        {
            if (spawned || !runner || !runner.IsRunning || !runner.IsServer || Time.unscaledTime < spawnAt) return;
            spawned = true;
            runner.Spawn(prefabId);
        }
        void OnDestroy() { if (template) Destroy(template.gameObject); }
    }
}
