using UnityEngine;
namespace FlyingHand
{
    public struct FlyingHandCommand { public Vector3 moveWorld;public float yaw; }
    public interface IFlyingHandInputSource
    { FlyingHandCommand ReadFlightInput(FlyingHandController motor,float dt); }
}
