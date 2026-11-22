using System;
using System.Runtime.InteropServices;
using Fusion;

namespace DeadlyJenga.CastlePrototype
{
    // Bounded latest-state client pose input.
    [StructLayout(LayoutKind.Sequential)]
    public struct CastleLatestPoseInput : INetworkInput
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct WordBlock : INetworkStruct
        {
            public ulong a, b, c, d, e, f, g, h;
        }

        const int BlockCount = 12;
        public const int Capacity = BlockCount * 64;
        public int Sequence;
        public int Length;
        public int SceneEpoch;
        int reserved;
        WordBlock block0, block1, block2, block3, block4, block5;
        WordBlock block6, block7, block8, block9, block10, block11;

        static Span<byte> Bytes(ref CastleLatestPoseInput value)
            => MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value.block0, BlockCount));

        public static bool TryPack(byte[] payload, int sequence, out CastleLatestPoseInput input)
        {
            input = default;
            if (payload == null || payload.Length == 0 || payload.Length > Capacity) return false;
            input.Sequence = sequence;
            input.Length = payload.Length;
            payload.AsSpan().CopyTo(Bytes(ref input));
            return true;
        }

        public static byte[] Unpack(ref CastleLatestPoseInput input)
        {
            if (input.Length < 1 || input.Length > Capacity) return null;
            return Bytes(ref input).Slice(0, input.Length).ToArray();
        }
    }
}
