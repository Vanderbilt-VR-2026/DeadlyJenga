using UnityEngine;
using DeadlyJenga.CandyPlayers;
namespace DeadlyJenga.CandyPhysicsHands
{
    public enum CandyHandMode { Inactive, HandTracking, Controller, Desktop }
    // This filter owns target continuity, never moves a physical body or a held object.
    public sealed class CandyHandTransition
    {
        public CandyHandPose Pose {get;private set;}
        public CandyHandMode Mode {get;private set;}
        public float Strength {get;private set;}
        public bool Initialized {get;private set;}
        float missing, stable, recovery;
        CandyHandMode candidate;
        public static bool IsValidPose(CandyHandPose raw)
        {
            bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
            bool valid=Finite(raw.position.x)&&Finite(raw.position.y)&&Finite(raw.position.z)&&Finite(raw.rotation.x)&&Finite(raw.rotation.y)&&Finite(raw.rotation.z)&&Finite(raw.rotation.w)&&Quaternion.Dot(raw.rotation,raw.rotation)>.1f;
            if(raw.skeletonTracked)for(int f=0;f<5;f++)for(int j=0;j<3;j++){float length=Vector3.Distance(raw.skeleton.Joint(f*4+j),raw.skeleton.Joint(f*4+j+1));valid&=length>.002f&&length<.12f;}
            return valid;
        }
        public void Step(CandyHandPose raw,CandyHandMode source,bool left,float dt,CandyHandSettings settings)
        {
            dt=Mathf.Clamp(dt,0,.05f);
            if(!IsValidPose(raw)){raw.tracked=false;source=CandyHandMode.Inactive;}
            if(source==CandyHandMode.Inactive||!raw.tracked)
            {
                missing+=dt;stable=0;candidate=CandyHandMode.Inactive;
                Strength=missing<=settings.dropoutHold?Mathf.Min(Strength,.35f):Mathf.Clamp01(1-(missing-settings.dropoutHold)/Mathf.Max(.001f,settings.releaseTime))*.35f;
                if(missing>settings.dropoutHold)
                {
                    var p=Pose;var rest=CandyHandSkeleton.Rest(left);
                    for(int i=0;i<20;i++)p.skeleton.SetJoint(i,Vector3.Lerp(p.skeleton.Joint(i),rest.Joint(i),1-Mathf.Exp(-dt*12)));
                    Pose=p;
                }
                if(Strength<=0)Mode=CandyHandMode.Inactive;
                recovery=0;return;
            }
            if(candidate!=source){candidate=source;stable=0;recovery=0;}
            stable+=dt;
            if(stable<settings.stableTime){Strength=0;return;}
            bool sourceChanged=Mode!=source;
            missing=0;Mode=source;recovery+=dt;Strength=Mathf.Clamp01(recovery/Mathf.Max(.001f,settings.recoveryTime));
            if(!Initialized){Pose=raw;Initialized=true;return;}
            var next=Pose;float distance=Vector3.Distance(next.position,raw.position);
            // Faster response on intentional movement; limited target speed on recovery spikes.
            float response=1-Mathf.Exp(-dt/Mathf.Lerp(settings.jitterTime,.006f,Mathf.Clamp01(distance/.08f)));
            next.position=Vector3.MoveTowards(next.position,Vector3.Lerp(next.position,raw.position,response),settings.maxTargetSpeed*dt);
            next.rotation=Quaternion.RotateTowards(next.rotation,Quaternion.Slerp(next.rotation,raw.rotation,response),settings.maxTargetAngularSpeed*dt);
            for(int i=0;i<20;i++)next.skeleton.SetJoint(i,sourceChanged?raw.skeleton.Joint(i):Vector3.Lerp(next.skeleton.Joint(i),raw.skeleton.Joint(i),response));
            next.tracked=true;next.skeletonTracked=raw.skeletonTracked;Pose=next;
        }
    }
}
