using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Core
{
    // One ground-level piece of a map, in the map's local space (x/z).
    public struct Footprint
    {
        public Vector2 center;
        public Vector2 halfSize;
        public Quaternion rotation;
    }

    // The outline of a map at ground level, for art that shapes itself around it (sand, terrain).
    // Works outside Play mode too, so art can reference the map component directly.
    public interface IMapFootprint
    {
        int FootprintVersion { get; }      // changes whenever the ground outline changes
        float FootprintHalfSize { get; }   // half the width of the map's base
        void GetGroundFootprints(List<Footprint> into);
    }
}
