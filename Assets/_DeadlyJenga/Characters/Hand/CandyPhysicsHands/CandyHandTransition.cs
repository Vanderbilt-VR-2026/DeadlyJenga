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
        CandyHandShape shape;bool shapeReady;
        // Finger bone directions in palm space at full precision (the skeleton itself is stored to 1 mm).
        public Vector3[] BoneDirections=>shapeReady?shape.Directions(bonesOut):null;
        readonly Vector3[] bonesOut=new Vector3[15];
        CandyHandMode candidate;
        // The hand's carrier moved (flying, or being held back at a wall). The pose rides with it, so smoothing only
        // ever applies to what the player's own hand did. Without this the pose lagged behind every carrier move
        // and the carrier then backed away from its own stale target.
        public void Shift(Vector3 delta){if(!Initialized)return;var p=Pose;p.position+=delta;Pose=p;}
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
                missing+=dt;
                // Tracking that flickers for a few frames is still the same hand: keep its pose, grip and stability.
                if(missing<=settings.dropoutHold&&Initialized&&Mode!=CandyHandMode.Inactive)return;
                stable=0;candidate=CandyHandMode.Inactive;
                Strength=Mathf.Min(Strength,Mathf.Clamp01(1-(missing-settings.dropoutHold)/Mathf.Max(.001f,settings.releaseTime)));
                if(shapeReady)
                {
                    shape.Follow(CandyHandShape.From(CandyHandSkeleton.Rest(left),left),dt,.083f);
                    var p=Pose;p.skeleton=shape.ToSkeleton(left);Pose=p;
                }
                if(Strength<=0)Mode=CandyHandMode.Inactive;
                return;
            }
            // Every source drives the same glove through the same finger joints, so no input can produce
            // a shape a hand cannot make, however wild or broken the tracking gets.
            var wanted=CandyHandShape.From(raw.skeleton,left);
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
            if(!Initialized||!shapeReady){shape=wanted;shapeReady=true;raw.skeleton=shape.ToSkeleton(left);if(!Initialized){Pose=raw;Initialized=true;return;}}
            var next=Pose;float distance=Vector3.Distance(next.position,raw.position);
            // Faster response on intentional movement; limited target speed on recovery spikes.
            float response=1-Mathf.Exp(-dt/Mathf.Lerp(settings.jitterTime,.006f,Mathf.Clamp01(distance/.08f)));
            next.position=Vector3.MoveTowards(next.position,Vector3.Lerp(next.position,raw.position,response),settings.maxTargetSpeed*dt);
            next.rotation=Quaternion.RotateTowards(next.rotation,Quaternion.Slerp(next.rotation,raw.rotation,response),settings.maxTargetAngularSpeed*dt);
            shape.Follow(wanted,dt,settings.jitterTime);
            next.skeleton=shape.ToSkeleton(left);
            next.tracked=true;next.skeletonTracked=raw.skeletonTracked;Pose=next;
        }
    }

    // A hand pose as joint angles, the way a real hand has them. Each finger fans a little at its knuckle and
    // then only curls in one plane; the thumb swings in a cone. Reading any tracked skeleton into this form
    // throws away everything a hand cannot do (fingers bent sideways, backwards or through each other's joints).
    public struct CandyHandShape
    {
        // Degrees. Fingers 1-4: how far the finger fans sideways, then the curl of its three joints.
        float fan1,fan2,fan3,fan4;
        float c10,c11,c12,c20,c21,c22,c30,c31,c32,c40,c41,c42;
        Vector3 thumb0,thumb1,thumb2;
        const float FanLimit=28,KnuckleBack=20,KnuckleCurl=88,MiddleCurl=102,TipCurl=72,JointBack=5;
        const float ThumbCone=95,ThumbBend=55;
        // No finger joint moves faster than this, so one bad tracking frame cannot snap a finger.
        const float CurlDegreesPerSecond=2500,FanDegreesPerSecond=700,ThumbDegreesPerSecond=1800;

        float Fan(int f)=>f==1?fan1:f==2?fan2:f==3?fan3:fan4;
        void SetFan(int f,float v){if(f==1)fan1=v;else if(f==2)fan2=v;else if(f==3)fan3=v;else fan4=v;}
        float Curl(int f,int j)=>(f*3+j) switch{3=>c10,4=>c11,5=>c12,6=>c20,7=>c21,8=>c22,9=>c30,10=>c31,11=>c32,12=>c40,13=>c41,_=>c42};
        void SetCurl(int f,int j,float v)
        {switch(f*3+j){case 3:c10=v;break;case 4:c11=v;break;case 5:c12=v;break;case 6:c20=v;break;case 7:c21=v;break;case 8:c22=v;break;case 9:c30=v;break;case 10:c31=v;break;case 11:c32=v;break;case 12:c40=v;break;case 13:c41=v;break;default:c42=v;break;}}
        Vector3 Thumb(int j)=>j==0?thumb0:j==1?thumb1:thumb2;
        void SetThumb(int j,Vector3 v){if(j==0)thumb0=v;else if(j==1)thumb1=v;else thumb2=v;}

        public static CandyHandShape From(CandyHandSkeleton skeleton,bool left)
        {
            var rest=CandyHandSkeleton.Rest(left);var shape=new CandyHandShape();
            Vector3 Bone(in CandyHandSkeleton s,int f,int j)
            {
                Vector3 d=s.Joint(f*4+j+1)-s.Joint(f*4+j);
                bool finite=!float.IsNaN(d.x)&&!float.IsNaN(d.y)&&!float.IsNaN(d.z)&&!float.IsInfinity(d.sqrMagnitude);
                return finite&&d.sqrMagnitude>1e-8f?d.normalized:Vector3.zero;
            }
            Vector3 previous=Vector3.zero;
            for(int j=0;j<3;j++)
            {
                Vector3 restBone=(rest.Joint(j+1)-rest.Joint(j)).normalized,d=Bone(skeleton,0,j);
                if(d==Vector3.zero)d=j==0?restBone:previous;
                d=j==0?Vector3.RotateTowards(restBone,d,ThumbCone*Mathf.Deg2Rad,0):Vector3.RotateTowards(previous,d,ThumbBend*Mathf.Deg2Rad,0);
                shape.SetThumb(j,d.normalized);previous=d.normalized;
            }
            for(int f=1;f<5;f++)
            {
                Vector3 d0=Bone(skeleton,f,0);if(d0==Vector3.zero)d0=Vector3.forward;
                // The knuckle bone points mostly forward unless the finger is curled right down; only then is its fan unknowable.
                float flat=new Vector2(d0.x,d0.z).magnitude;
                float fan=flat>.3f&&d0.z>0?Mathf.Clamp(Mathf.Atan2(d0.x,d0.z)*Mathf.Rad2Deg,-FanLimit,FanLimit):0;
                shape.SetFan(f,fan);
                Vector3 along=new Vector3(Mathf.Sin(fan*Mathf.Deg2Rad),0,Mathf.Cos(fan*Mathf.Deg2Rad));
                float before=0;
                for(int j=0;j<3;j++)
                {
                    Vector3 d=j==0?d0:Bone(skeleton,f,j);
                    float total=d==Vector3.zero?before:Mathf.Atan2(-d.y,Vector3.Dot(d,along))*Mathf.Rad2Deg;
                    float bend=Mathf.DeltaAngle(before,total);
                    bend=j==0?Mathf.Clamp(bend,-KnuckleBack,KnuckleCurl):Mathf.Clamp(bend,-JointBack,j==1?MiddleCurl:TipCurl);
                    shape.SetCurl(f,j,bend);
                    // Measure the next joint from where the tracked bone really was, so one clamped joint does not shift the rest.
                    before=total;
                }
            }
            return shape;
        }

        // Moves toward another shape: gently when the difference is tiny (tracking jitter), straight away when it is
        // a real movement, and never faster than a finger can move.
        public void Follow(CandyHandShape to,float dt,float jitterTime)
        {
            float Step(float from,float target,float fastest)
            {
                float off=Mathf.DeltaAngle(from,target);
                float time=Mathf.Lerp(jitterTime,.01f,Mathf.Clamp01(Mathf.Abs(off)/12f));
                float step=off*(1-Mathf.Exp(-dt/Mathf.Max(.001f,time)));
                return from+Mathf.Clamp(step,-fastest*dt,fastest*dt);
            }
            for(int f=1;f<5;f++)
            {
                SetFan(f,Step(Fan(f),to.Fan(f),FanDegreesPerSecond));
                for(int j=0;j<3;j++)SetCurl(f,j,Step(Curl(f,j),to.Curl(f,j),CurlDegreesPerSecond));
            }
            for(int j=0;j<3;j++)
            {
                Vector3 from=Thumb(j),target=to.Thumb(j);float off=Vector3.Angle(from,target);
                float time=Mathf.Lerp(jitterTime,.01f,Mathf.Clamp01(off/12f));
                float step=Mathf.Min(off*(1-Mathf.Exp(-dt/Mathf.Max(.001f,time))),ThumbDegreesPerSecond*dt);
                SetThumb(j,Vector3.RotateTowards(from,target,step*Mathf.Deg2Rad,0).normalized);
            }
        }

        // Bone directions in palm space, thumb first, three per finger.
        public Vector3[] Directions(Vector3[] into)
        {
            for(int j=0;j<3;j++)into[j]=Thumb(j);
            for(int f=1;f<5;f++)
            {
                float fan=Fan(f)*Mathf.Deg2Rad,total=0;
                for(int j=0;j<3;j++)
                {
                    total+=Curl(f,j);float c=total*Mathf.Deg2Rad;
                    into[f*3+j]=new Vector3(Mathf.Cos(c)*Mathf.Sin(fan),-Mathf.Sin(c),Mathf.Cos(c)*Mathf.Cos(fan));
                }
            }
            return into;
        }

        // The glove's own knuckles and bone lengths with these joint angles.
        public CandyHandSkeleton ToSkeleton(bool left)
        {
            var rest=CandyHandSkeleton.Rest(left);var result=rest;var bones=Directions(new Vector3[15]);
            for(int f=0;f<5;f++)
            {
                Vector3 at=rest.Joint(f*4);
                for(int j=0;j<3;j++)
                {
                    at+=bones[f*3+j]*(rest.Joint(f*4+j+1)-rest.Joint(f*4+j)).magnitude;
                    result.SetJoint(f*4+j+1,at);
                }
            }
            return result;
        }
    }

    // Keeps finger bones whole and bent within a natural range.
    public static class CandyHandPoseLimits
    {
        // Rest and desktop poses bend about 30 degrees per joint. Anything past this folds into a wire.
        public const float MaximumBendDegrees=100f;

        // Puts tracked finger directions onto the candy hand's own knuckles and bone lengths.
        // A real hand is longer than the glove. Used as-is, its joints sat off the palm and the skin stretched to reach them.
        public static CandyHandSkeleton Fit(CandyHandSkeleton tracked,bool left)=>CandyHandShape.From(tracked,left).ToSkeleton(left);

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
