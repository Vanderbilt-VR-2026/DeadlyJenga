using Fusion;

namespace DeadlyJenga.CastlePrototype
{
    // Live movement travels here: unreliable, so a lost packet is simply replaced by the next one
    // instead of holding up everything queued behind it on the reliable stream.
    public sealed class CastleFusionFastLane : SimulationBehaviour
    {
        public const int MaxPayload = 440;

        // PlayerRef.None targets the host.
        [Rpc(Channel = RpcChannel.Unreliable, TickAligned = false)]
        public static void RpcLivePose(NetworkRunner runner, [RpcTarget] PlayerRef target, byte[] data, RpcInfo info = default)
        {
            CastleFusionSession.ReceiveFastPose(runner, info.Source, data);
        }
    }
}
