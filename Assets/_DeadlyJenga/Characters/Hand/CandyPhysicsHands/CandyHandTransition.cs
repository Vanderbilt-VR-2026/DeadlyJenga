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
        float missing, stable;
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
            // Every source drives the same glove: its own knuckles and bone lengths, bent within range.
            if(raw.tracked)raw.skeleton=CandyHandPoseLimits.Fit(raw.skeleton,left);
            if(source==CandyHandMode.Inactive||!raw.tracked)
            {
                missing+=dt;stable=0;candidate=CandyHandMode.Inactive;
                Strength=missing<=settings.dropoutHold?Mathf.Min(Strength,.35f):Mathf.Clamp01(1-(missing-settings.dropoutHold)/Mathf.Max(.001f,settings.releaseTime))*.35f;
                if(missing>settings.dropoutHold)
                {
                    var p=Pose;var rest=CandyHandSkeleton.Rest(left);
                    p.skeleton=CandyHandPoseLimits.Blend(p.skeleton,rest,1-Mathf.Exp(-dt*12));
                    Pose=p;
                }
                if(Strength<=0)Mode=CandyHandMode.Inactive;
                return;
            }
            if(candidate!=source){candidate=source;stable=0;}
            stable+=dt;
            if(stable<settings.stableTime)
            {
                // A hand that is still showing holds its pose while the new source settles. It never blinks out.
                if(!Initialized||Mode==CandyHandMode.Inactive)Strength=0;
                return;
            }
            // Strength only ever ramps, so switching between controller and tracked hand keeps the grip and the picture.
            missing=0;Mode=source;Strength=Mathf.MoveTowards(Strength,1,dt/Mathf.Max(.001f,settings.recoveryTime));
            if(!Initialized){Pose=raw;Initialized=true;return;}
            var next=Pose;float distance=Vector3.Distance(next.position,raw.position);
            // Faster response on intentional movement; limited target speed on recovery spikes.
            float response=1-Mathf.Exp(-dt/Mathf.Lerp(settings.jitterTime,.006f,Mathf.Clamp01(distance/.08f)));
            next.position=Vector3.MoveTowards(next.position,Vector3.Lerp(next.position,raw.position,response),settings.maxTargetSpeed*dt);
            next.rotation=Quaternion.RotateTowards(next.rotation,Quaternion.Slerp(next.rotation,raw.rotation,response),settings.maxTargetAngularSpeed*dt);
            next.skeleton=CandyHandPoseLimits.Blend(next.skeleton,raw.skeleton,response);
            next.tracked=true;next.skeletonTracked=raw.skeletonTracked;Pose=next;
        }
    }

    // Keeps finger bones whole and bent within a natural range.
    public static class CandyHandPoseLimits
    {
        // Rest and desktop poses bend about 30 degrees per joint. Anything past this folds into a wire.
        public const float MaximumBendDegrees=100f;

        // Puts tracked finger directions onto the candy hand's own knuckles and bone lengths.
        // A real hand is longer than the glove. Used as-is, its joints sat off the palm and the skin stretched to reach them.
        public static CandyHandSkeleton Fit(CandyHandSkeleton tracked,bool left)
        {
            var rest=CandyHandSkeleton.Rest(left);var result=rest;
            for(int f=0;f<5;f++)
            {
                Vector3 at=rest.Joint(f*4);
                for(int j=0;j<3;j++)
                {
                    int i=f*4+j;
                    Vector3 restBone=rest.Joint(i+1)-rest.Joint(i);
                    Vector3 bone=tracked.Joint(i+1)-tracked.Joint(i);
                    at+=(bone.sqrMagnitude>1e-8f?bone.normalized:restBone.normalized)*restBone.magnitude;
                    result.SetJoint(i+1,at);
                }
            }
            return LimitFlexion(result);
        }

        // Blends each finger from its base. Bone lengths stay fixed, so a half-way pose never shrinks a finger.
        public static CandyHandSkeleton Blend(CandyHandSkeleton from,CandyHandSkeleton to,float t)
        {
            var result=to;
            for(int f=0;f<5;f++)
            {
                Vector3 at=Vector3.Lerp(from.Joint(f*4),to.Joint(f*4),t);
                result.SetJoint(f*4,at);
                for(int j=0;j<3;j++)
                {
                    Vector3 fromBone=from.Joint(f*4+j+1)-from.Joint(f*4+j);
                    Vector3 toBone=to.Joint(f*4+j+1)-to.Joint(f*4+j);
                    float length=toBone.magnitude;
                    Vector3 direction=fromBone.sqrMagnitude>1e-6f&&toBone.sqrMagnitude>1e-6f
                        ?Vector3.Slerp(fromBone.normalized,toBone.normalized,t)
                        :toBone.normalized;
                    at+=direction.normalized*length;
                    result.SetJoint(f*4+j+1,at);
                }
            }
            // Slerping each bone separately can bend a blended finger past the limit, so clamp the result too.
            return LimitFlexion(result);
        }

        // Limits how far each finger segment can bend from the one before it. Only the bend is changed, never the length.
        // Joints are stored at 1 mm, so the stored bend is checked after rounding and pulled in if rounding pushed it over.
        public static CandyHandSkeleton LimitFlexion(CandyHandSkeleton skeleton)
        {
            for(int f=0;f<5;f++)
            {
                Vector3 previous=Vector3.zero;
                for(int j=0;j<3;j++)
                {
                    int i=f*4+j;
                    Vector3 start=skeleton.Joint(i);
                    Vector3 bone=skeleton.Joint(i+1)-start;
                    float length=bone.magnitude;
                    if(length<1e-5f){previous=Vector3.zero;continue;}
                    Vector3 direction=bone/length;
                    if(previous.sqrMagnitude>0&&Vector3.Angle(previous,direction)>MaximumBendDegrees)
                    {
                        Vector3 wanted=direction;
                        float limit=MaximumBendDegrees;
                        for(int attempt=0;attempt<16;attempt++)
                        {
                            direction=Vector3.RotateTowards(previous,wanted,limit*Mathf.Deg2Rad,0f).normalized;
                            skeleton.SetJoint(i+1,start+direction*length);
                            direction=(skeleton.Joint(i+1)-start).normalized;
                            if(Vector3.Angle(previous,direction)<=MaximumBendDegrees)break;
                            limit-=.25f;
                        }
                    }
                    previous=direction;
                }
            }
            return skeleton;
        }
    }
}
