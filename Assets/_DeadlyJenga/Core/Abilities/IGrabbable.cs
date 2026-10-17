using UnityEngine;

namespace DeadlyJenga.Core
{
    // A hand can grab and pull it.
    public interface IGrabbable
    {
        float Weight { get; }
        bool CanGrab(GameObject hand);
        bool TryStartGrab(GameObject hand, Vector3 handPosition);
        void Hold(Vector3 handPosition);
        void Release();
    }
}
