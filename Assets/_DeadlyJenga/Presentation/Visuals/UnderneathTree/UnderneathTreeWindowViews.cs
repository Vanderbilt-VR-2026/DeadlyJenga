using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    public sealed class UnderneathTreeWindowViews : ScriptableObject
    {
        const string Guide = "Optional exterior prefab. Empty uses the original snowy view. Origin is the sill centre, +Y is up and +Z points outdoors. Set distance and scale on the prefab.";

        [Tooltip(Guide)] public GameObject north;
        [Tooltip(Guide)] public GameObject south;
    }
}
