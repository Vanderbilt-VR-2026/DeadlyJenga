using UnityEngine;
using FlyingHand;
using DeadlyJenga.CandyPlayers;

namespace DeadlyJenga.CandyPhysicsHands
{
    // Pinch the spare hand, then move it like a joystick. Release to stop thrust.
    public sealed class CandyHandFlightGesture
    {
        bool waitingForRelease=true, pinching;
        Vector3 anchor;
        Quaternion rotation;
        public void Reset(){waitingForRelease=true;pinching=false;}
        public FlyingHandCommand Read(CandyHandPose hand,Quaternion heading,bool suspended)
        {
            if(suspended||!hand.tracked||!hand.skeletonTracked||!CandyHandTransition.IsValidPose(hand)){Reset();return default;}
            float gap=Vector3.Distance(hand.skeleton.Joint(3),hand.skeleton.Joint(7));
            bool held=gap<(pinching?.032f:.023f);
            if(waitingForRelease){if(gap>.032f)waitingForRelease=false;return default;}
            if(!held){pinching=false;return default;}
            if(!pinching){anchor=hand.position;rotation=hand.rotation;pinching=true;return default;}
            float Axis(float value)=>Mathf.Sign(value)*Mathf.Clamp01((Mathf.Abs(value)-.025f)/.125f);
            Vector3 offset=hand.position-anchor;
            Vector3 movement=new Vector3(Axis(offset.x),Axis(offset.y),Axis(offset.z));
            float yaw=Mathf.DeltaAngle(0,(hand.rotation*Quaternion.Inverse(rotation)).eulerAngles.y);
            return new FlyingHandCommand{moveWorld=heading*Vector3.ClampMagnitude(movement,1),yaw=Mathf.Sign(yaw)*Mathf.Clamp01((Mathf.Abs(yaw)-10)/35)};
        }
    }
}
