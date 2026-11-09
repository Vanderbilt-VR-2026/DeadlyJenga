using System;
using System.IO;
using Fusion;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Independent pages keep busy block batches from delaying player samples.
    public sealed class CastleMotionPage : NetworkBehaviour
    {
        internal const uint PrefabNumber = 100001;
        internal const int ByteCapacity = 1792;
        [Networked, Capacity(ByteCapacity / 4)] NetworkArray<int> Words => default;
        [Networked] public int Lane { get; set; }
        [Networked] int SceneEpoch { get; set; }
        [Networked] int WorldRevision { get; set; }
        [Networked] int SampleTick { get; set; }
        [Networked] int Length { get; set; }
        [Networked] int Sequence { get; set; }

        readonly byte[] receiveBytes = new byte[ByteCapacity];
        CastleMotionProvider provider;
        int lastSequence;

        public override void Spawned() => provider = Runner.GetComponent<CastleMotionProvider>();

        internal void Publish(int scene, int revision, int tick, byte[] source, int length)
        {
            var words = Words;
            for (int start = 0; start < length; start += 4)
            {
                int value = 0;
                for (int i = 0; i < 4 && start + i < length; i++)
                    value |= source[start + i] << (8 * i);
                if (words[start / 4] != value) words.Set(start / 4, value);
            }
            SceneEpoch = scene;
            WorldRevision = revision;
            SampleTick = tick;
            Length = length;
            Sequence++;
        }

        public override void FixedUpdateNetwork()
        {
            if (Lane == 0 && HasStateAuthority && Runner.IsForward && provider && provider.Session)
                provider.Session.ReadMotionInput(Runner);
        }

        public override void Render()
        {
            if (HasStateAuthority || !provider || !provider.Session || Sequence == lastSequence ||
                Length <= 0 || Length > receiveBytes.Length) return;
            if (!provider.Session.CanReadMotion(Runner, SceneEpoch, WorldRevision)) return;

            int length = Length;
            var words = Words;
            for (int start = 0; start < length; start += 4)
            {
                int value = words[start / 4];
                for (int i = 0; i < 4 && start + i < length; i++)
                    receiveBytes[start + i] = (byte)(value >> (8 * i));
            }
            provider.Session.ReadMotionPage(SampleTick, Runner.DeltaTime, receiveBytes, length);
            lastSequence = Sequence;
        }
    }

    internal sealed class CastleMotionProvider : NetworkObjectProviderDefault
    {
        internal CastleFusionSession Session;
        readonly NetworkObjectBaker baker = new NetworkObjectBaker();

        public override NetworkObjectAcquireResult AcquireInstance(NetworkRunner runner,
            in NetworkObjectAcquireContext context, out NetworkObject instance)
        {
            if (!context.TypeId.IsPrefab || context.TypeId.AsPrefabId.RawValue != CastleMotionPage.PrefabNumber)
                return base.AcquireInstance(runner, context, out instance);
            var root = new GameObject("Castle motion page");
            instance = root.AddComponent<NetworkObject>();
            root.AddComponent<CastleMotionPage>();
            baker.Bake(root);
            if (context.DontDestroyOnLoad) runner.MakeDontDestroyOnLoad(root);
            else runner.MoveToRunnerScene(root);
            return NetworkObjectAcquireResult.Success;
        }

        public override void ReleaseInstance(NetworkRunner runner, in NetworkObjectReleaseContext context)
        {
            if (!context.TypeId.IsPrefab || context.TypeId.AsPrefabId.RawValue != CastleMotionPage.PrefabNumber)
                base.ReleaseInstance(runner, context);
            else if (!context.IsBeingDestroyed && context.Object)
                Destroy(context.Object.gameObject);
        }
    }
}
