using UnityEngine;

namespace DeadlyJenga.Core
{
    // A map that can be copied over the network. The host's map is the real one;
    // on other players' machines the same map becomes a replica that just follows the host.
    // Block poses travel through IWorldQuery.Blocks, so a map only has to describe its layout here.
    public interface ISyncedWorld
    {
        // Where the map sits. Block poses are sent in world space.
        Transform Root { get; }

        // Goes up every time the map rebuilds, so other players know to rebuild too.
        int Revision { get; }

        // The map's layout as text (for example its settings as JSON).
        string SaveLayout();

        // Rebuild to match a host's layout, placed at origin and scale.
        // Returns false, and changes nothing, if the layout wouldn't produce expectedBlocks blocks.
        bool LoadLayout(string layout, int expectedBlocks, Pose origin, Vector3 scale);

        // true: blocks stop simulating and only follow poses they're given.
        // false: blocks go back to normal physics.
        void SetReplica(bool replica);
    }
}
