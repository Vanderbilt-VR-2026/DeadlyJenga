using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleSurvivor
    {
        public bool HasInputFocus=>vrInputFocused;
        float hoverUntil,hoverVertical;Vector3 hoverVelocity;
        public void ApplyHoverFlight(Vector3 velocity,float seconds,bool takeoff)
        {
            if(!Alive||InputSuspended||!vrInputFocused||externallyDriven||!Capsule||!Capsule.enabled||!float.IsFinite(seconds)||seconds<=0||!float.IsFinite(velocity.x)||!float.IsFinite(velocity.y)||!float.IsFinite(velocity.z))return;
            if(Climbing)ReleaseClimb("Released for flight",false);
            bool nearGround=Grounded;
            Vaulting=false;support=null;Grounded=false;jumped=true;lastGrounded=-100;peakFallSpeed=0;
            hoverUntil=Time.time+Mathf.Max(.05f,Mathf.Min(seconds,.1f)*2);
            hoverVelocity=Vector3.MoveTowards(hoverVelocity,Vector3.ClampMagnitude(Vector3.ProjectOnPlane(velocity,Vector3.up),7*playerScale),20*seconds);
            hoverVertical=takeoff?3*playerScale:Mathf.MoveTowards(hoverVertical,Mathf.Clamp(velocity.y,-3,4)*playerScale,12*seconds);
            if(nearGround)hoverVertical=Mathf.Max(hoverVertical,1.5f*playerScale);
            slopeVelocity=hoverVelocity;verticalSpeed=hoverVertical;ActionStatus="Suit hover flight";
        }
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
