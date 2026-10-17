using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeadlyJenga.Core
{
    // Something other players see: a player, a bot, a flying hand.
    // The character packs its own state; networking only ships the bytes.
    public interface INetworkActor
    {
        string NetworkKind { get; }        // picks the remote copy on other machines (see NetworkKinds)
        bool NetworkActive { get; }        // false = hidden for now (respawning, hand not built yet)
        Vector3 NetworkPosition { get; }   // used to keep join spawns apart
        void WriteState(BinaryWriter writer);
    }

    // A copy of someone else's character on this machine.
    public interface INetworkActorView : INetworkActor
    {
        void ReadState(BinaryReader reader, bool active);
        void Render(float deltaTime);
        void Suspend();                    // the owner stopped sending for a moment
    }

    // A local hand on a joined machine shows its own movement right away while the host decides contact.
    public interface INetworkPreview
    {
        bool NetworkPreview { get; set; }
    }

    // What a remote copy may need from this machine.
    public struct NetworkContext
    {
        public bool host;                  // the host simulates remote hands against the real map
        public GameObject localSurvivor;   // this machine's own characters, used as templates
        public GameObject localHand;
        public string actorId;             // the network id of the actor this view shows
        // Host only: pass a hazard's push (velocity change) and damage on to the player who owns the actor.
        public System.Action<string, Vector3, float> sendImpact;
    }

    public static class NetworkKinds
    {
        public const string Survivor = "survivor", Hand = "hand";
    }

    // Characters registers one maker per kind. Networking creates copies by kind name.
    public static class NetworkActors
    {
        public delegate INetworkActorView ViewMaker(GameObject root, NetworkContext context);
        public delegate void SpawnWriter(BinaryWriter writer, Pose spawn, NetworkContext context);

        // Characters simulated on this machine (players, bots, hands) add themselves while enabled.
        public static readonly List<INetworkActor> Local = new List<INetworkActor>();

        static readonly Dictionary<string, (ViewMaker make, SpawnWriter spawn)> kinds = new Dictionary<string, (ViewMaker, SpawnWriter)>();

        public static void Register(string kind, ViewMaker make, SpawnWriter spawn) => kinds[kind] = (make, spawn);

        public static INetworkActorView CreateView(string kind, GameObject root, NetworkContext context) =>
            kind != null && kinds.TryGetValue(kind, out var k) ? k.make(root, context) : null;

        // Starting state for someone who just joined, before they send their own.
        public static bool WriteSpawn(string kind, BinaryWriter writer, Pose spawn, NetworkContext context)
        {
            if (kind == null || !kinds.TryGetValue(kind, out var k)) return false;
            k.spawn(writer, spawn, context);
            return true;
        }

        // Interfaces can't use Unity's "destroyed object" check, so use this instead.
        public static bool Alive(object thing) => thing is Object unityObject ? unityObject : thing != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Local.Clear();
    }
}
