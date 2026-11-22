using Fusion;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // A runtime-baked state page for one connected human. Replication supersedes old poses.
    public sealed class CastleLatestPosePage : NetworkBehaviour
    {
        internal const uint PrefabNumber = 100001;
        internal const int ByteCapacity = 1792;
        [Networked, Capacity(ByteCapacity / 4)] NetworkArray<int> Words => default;
        [Networked] int SceneEpoch { get; set; }
        [Networked] int Length { get; set; }
        [Networked] int Sequence { get; set; }

        CastleLatestPoseProvider provider;
        int appliedSequence;
        public override void Spawned() => provider = Runner.GetComponent<CastleLatestPoseProvider>();

        internal void Publish(int scene, int sequence, byte[] payload)
        {
            var words = Words;
            for (int start = 0; start < payload.Length; start += 4)
            {
                int value = 0;
                for (int i = 0; i < 4 && start + i < payload.Length; i++)
                    value |= payload[start + i] << (8 * i);
                if (words[start / 4] != value) words.Set(start / 4, value);
            }
            SceneEpoch = scene;
            Length = payload.Length;
            Sequence = sequence;
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority && Runner.IsForward && provider && provider.Session)
                provider.Session.ReadLatestInputs(Runner);
        }

        public override void Render()
        {
            if (HasStateAuthority || !provider || !provider.Session ||
                !CastleFusionSession.LivePoseWindow.Newer(Sequence, appliedSequence, appliedSequence != 0) ||
                Length < 1 || Length > ByteCapacity || !provider.Session.AcceptLatestPage(Runner, SceneEpoch))
                return;
            var bytes = new byte[Length];
            var words = Words;
            for (int start = 0; start < bytes.Length; start += 4)
            {
                int value = words[start / 4];
                for (int i = 0; i < 4 && start + i < bytes.Length; i++)
                    bytes[start + i] = (byte)(value >> (8 * i));
            }
            appliedSequence = Sequence;
            provider.Session.ReadLatestPage(bytes, appliedSequence);
        }
    }

    internal sealed class CastleLatestPoseProvider : NetworkObjectProviderDefault
    {
        internal CastleFusionSession Session;
        readonly NetworkObjectBaker baker = new NetworkObjectBaker();

        public override NetworkObjectAcquireResult AcquireInstance(NetworkRunner runner,
            in NetworkObjectAcquireContext context, out NetworkObject instance)
        {
            if (!context.TypeId.IsPrefab || context.TypeId.AsPrefabId.RawValue != CastleLatestPosePage.PrefabNumber)
                return base.AcquireInstance(runner, context, out instance);
            var root = new GameObject("Castle human pose");
            instance = root.AddComponent<NetworkObject>();
            root.AddComponent<CastleLatestPosePage>();
            baker.Bake(root);
            if (context.DontDestroyOnLoad) runner.MakeDontDestroyOnLoad(root);
            else runner.MoveToRunnerScene(root);
            return NetworkObjectAcquireResult.Success;
        }

        public override void ReleaseInstance(NetworkRunner runner, in NetworkObjectReleaseContext context)
        {
            if (!context.TypeId.IsPrefab || context.TypeId.AsPrefabId.RawValue != CastleLatestPosePage.PrefabNumber)
                base.ReleaseInstance(runner, context);
            else if (!context.IsBeingDestroyed && context.Object)
                Destroy(context.Object.gameObject);
        }
    }
}
