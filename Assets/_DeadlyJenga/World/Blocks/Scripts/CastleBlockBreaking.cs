using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // IBreakable. A broken block is parked far below the map instead of destroyed,
    // so block counts and network indexes never change. Rewinding can bring it back.
    public sealed partial class CastleBlock
    {
        public const float GraveyardY = -5000, GraveyardLine = -4000;
        float stress;
        bool broken;

        public float Stress => stress;
        public bool Broken => broken;
        public static bool InGraveyard(Vector3 position) => position.y < GraveyardLine;

        public void Break(GameObject source)
        {
            if (broken || Body.isKinematic) return;
            var position = Body.position;
            var velocity = Body.linearVelocity;
            broken = true; stress = 0;
            Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero;
            Body.detectCollisions = false;
            Body.useGravity = false;
            Body.constraints = RigidbodyConstraints.FreezeAll;
            Body.position = new Vector3(position.x, GraveyardY, position.z);
            transform.position = Body.position;
            SyncSurface();
            GameEvents.RaiseBlockBroken(gameObject, position, velocity);
        }

        // A copy of the host's block saw it move into the graveyard.
        internal void MarkBroken(Vector3 lastPosition, Vector3 velocity)
        {
            if (broken) return;
            broken = true; stress = 0;
            Body.detectCollisions = false;
            GameEvents.RaiseBlockBroken(gameObject, lastPosition, velocity);
        }

        // Back in play (after a rewind).
        internal void Restore()
        {
            if (!broken) return;
            broken = false;
            Body.detectCollisions = true;
            Body.useGravity = true;
            Body.constraints = RigidbodyConstraints.None;
        }

        internal void SetStress(float value) => stress = value;
    }
}
