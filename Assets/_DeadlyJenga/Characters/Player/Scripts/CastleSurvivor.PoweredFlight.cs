using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleSurvivor
    {
        public bool HasInputFocus=>vrInputFocused;
        public void ApplyPoweredFlight(Vector3 acceleration,float seconds)
        {
            if(!Alive||InputSuspended||!vrInputFocused||externallyDriven||!Capsule||!Capsule.enabled||!float.IsFinite(seconds)||seconds<=0||!float.IsFinite(acceleration.x)||!float.IsFinite(acceleration.y)||!float.IsFinite(acceleration.z))return;
            float dt=Mathf.Min(seconds,Mathf.Clamp(Time.maximumDeltaTime,.05f,.5f));
            if(Climbing)ReleaseClimb("Released for powered flight",false);
            Vaulting=false;support=null;Grounded=false;jumped=true;lastGrounded=-100;
            verticalSpeed=Mathf.Clamp(verticalSpeed+acceleration.y*dt,-8,9*playerScale);
            slopeVelocity=Vector3.ClampMagnitude(slopeVelocity+Vector3.ProjectOnPlane(acceleration,Vector3.up)*dt,8*playerScale);
            ActionStatus="Powered flight";
        }
    }
}
