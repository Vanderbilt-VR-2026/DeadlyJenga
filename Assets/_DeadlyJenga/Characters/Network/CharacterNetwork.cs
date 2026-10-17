using System.IO;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CandyPhysicsHands;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.CharacterNetwork
{
    // Tells networking how to make remote copies of survivors and hands. Runs on load; no scene setup.
    static class CharacterNetwork
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register()
        {
            NetworkActors.Register(NetworkKinds.Survivor, (root, context) => root.AddComponent<SurvivorNetworkView>().Init(context), WriteSurvivorSpawn);
            NetworkActors.Register(NetworkKinds.Hand, (root, context) => root.AddComponent<HandNetworkView>().Init(context), WriteHandSpawn);
        }

        static void WriteSurvivorSpawn(BinaryWriter w, Pose spawn, NetworkContext context)
        {
            var player = context.localSurvivor ? context.localSurvivor.GetComponent<CastleSurvivor>() : null;
            float scale = player ? player.playerScale : 1;
            var rest = new CandyHandPose { position = spawn.position, rotation = Quaternion.identity };
            var frame = new CandyAvatarFrame { feet = spawn.position, head = spawn.position + Vector3.up * 1.6f * scale, headRotation = spawn.rotation, scale = scale, left = rest, right = rest };
            CharacterWire.WriteSurvivor(w, frame, 0);
        }

        static void WriteHandSpawn(BinaryWriter w, Pose spawn, NetworkContext context)
        {
            var bridge = context.localHand ? context.localHand.GetComponent<CandyFlyingHandBridge>() : null;
            bool left = bridge && bridge.useLeftHand;
            float size = bridge && bridge.settings ? bridge.settings.size : 20;
            var target = new CandyHandPose { position = spawn.position, rotation = spawn.rotation, tracked = true, skeleton = CandyHandSkeleton.Rest(left) };
            CharacterWire.WriteHand(w, left, size, target, (Pose[])null);
        }
    }
}
