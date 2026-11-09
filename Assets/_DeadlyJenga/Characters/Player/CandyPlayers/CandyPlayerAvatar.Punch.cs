using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyPlayerAvatar
    {
        const float PunchDuration=.36f;
        float punchBegan=float.NegativeInfinity;
        Vector3 punchDirection,punchContact;
        Transform punchTarget;
        readonly Collider[] punchCandidates=new Collider[32];

        IMatchRuntime punchRuntime;
        string punchEpoch;
        int punchSequence;
        float vrContactAt=float.NegativeInfinity;
        Vector3 vrContactPoint;

        void ResetPunch(){punchBegan=float.NegativeInfinity;punchTarget=null;vrContactAt=float.NegativeInfinity;}

        void ApplyVrPunchContact(ref CandyAvatarFrame frame)
        {
            var runtime=MatchRuntime.Current;
            var match=runtime?.Frame;
            if(match==null){ResetPunch();return;}
            if(!ReferenceEquals(punchRuntime,runtime)||punchEpoch!=match.enemyActionEpoch)
            {
                punchRuntime=runtime;punchEpoch=match.enemyActionEpoch;punchSequence=match.expressionSequence;
                vrContactAt=float.NegativeInfinity;
            }
            if(match.expressionSequence>punchSequence)
            {
                foreach(var contact in match.expressions??System.Array.Empty<ExpressionFrame>())
                {
                    if(contact.id<=punchSequence||contact.name!="PunchContact"||contact.actor!=survivor.ActorId||contact.remaining<.25f)continue;
                    vrContactPoint=contact.position;vrContactAt=Time.time;
                }
                punchSequence=match.expressionSequence;
            }
            var hand=frame.right;
            float age=Time.time-vrContactAt;
            if(age<0||age>=.16f||!hand.tracked||survivor.InputSuspended||!survivor.Alive||
                GetComponent<IEquipmentControls>()?.Equipment?.drawn==true)
            {vrContactAt=float.NegativeInfinity;return;}
            float scale=Mathf.Max(.1f,frame.scale);
            var knuckles=hand.position+hand.rotation*(Vector3.forward*.074f*scale);
            var correction=vrContactPoint-knuckles;
            if(correction.sqrMagnitude>.28f*.28f*scale*scale){vrContactAt=float.NegativeInfinity;return;}
            float blend=Mathf.SmoothStep(0,1,age/.035f)*(1-Mathf.SmoothStep(0,1,(age-.065f)/.095f));
            hand.position+=Vector3.ClampMagnitude(correction,.19f*scale)*blend;
            var fist=CandyHandSkeleton.Rest(false,1);
            if(!hand.skeletonTracked)hand.skeleton=fist;
            else for(int i=0;i<20;i++)hand.skeleton.SetJoint(i,Vector3.Lerp(hand.skeleton.Joint(i),fist.Joint(i),blend));
            hand.skeletonTracked=true;frame.right=hand;
        }

        void BeginPunch(CastleSurvivor actor,Vector3 origin,Vector3 direction,float speed)
        {
            // VR keeps the real tracked hand. Bots use the same animation as desktop players.
            if(actor.IsVR&&!actor.externallyDriven)return;
            punchBegan=Time.time;punchDirection=direction.normalized;punchTarget=null;
            float nearest=2.2f;
            int count=Physics.OverlapSphereNonAlloc(origin,nearest,punchCandidates,1<<Layers.Survivor,QueryTriggerInteraction.Collide);
            for(int i=0;i<count;i++)
            {
                var collider=punchCandidates[i];var target=collider.GetComponentInParent<IModeActor>();
                if(target==null||target.gameObject==gameObject||!target.Alive)continue;
                Vector3 delta=target.transform.position+Vector3.up*.6f-origin;float distance=delta.magnitude;
                if(distance<.01f||distance>=nearest||Vector3.Dot(delta/distance,punchDirection)<.45f)continue;
                if(Physics.Raycast(origin,delta/distance,distance,(1<<Layers.Block)|(1<<Layers.Surface),QueryTriggerInteraction.Ignore))continue;
                nearest=distance;punchTarget=target.transform;
                punchContact=punchTarget.InverseTransformPoint(collider.ClosestPoint(origin));
            }
        }
        void ApplyPunch(ref CandyAvatarFrame frame)
        {
            if(!survivor||!survivor.PunchEnabled||survivor.CarryingFlag||survivor.Climbing||survivor.Vaulting)
            {ResetPunch();return;}
            if(survivor.IsVR&&!survivor.externallyDriven){ApplyVrPunchContact(ref frame);return;}
            float age=Time.time-punchBegan;if(age<0||age>=PunchDuration)return;
            float scale=Mathf.Max(.1f,frame.scale);
            var hand=frame.right;
            Vector3 direction=punchDirection.sqrMagnitude>.1f?punchDirection:frame.headRotation*Vector3.forward;
            var strikeRotation=Quaternion.LookRotation(direction,Vector3.up)*Quaternion.Euler(0,0,-80);
            Vector3 shoulder=frame.head+frame.headRotation*new Vector3(.21f,-.345f,0)*scale;
            Vector3 strike=frame.head+direction*(.72f*scale)+frame.headRotation*new Vector3(.12f,-.18f,0)*scale;
            if(punchTarget)strike=punchTarget.TransformPoint(punchContact)-direction*(.055f*scale);
            // The aim assist meets nearby bodies; it never stretches an arm across the damage cone.
            strike=shoulder+Vector3.ClampMagnitude(strike-shoulder,.75f*scale);
            Vector3 windup=hand.position-direction*(.12f*scale)+Vector3.down*(.035f*scale);
            float extension=age<.045f?0:age<.125f?Mathf.SmoothStep(0,1,(age-.045f)/.08f):age<.155f?1:1-Mathf.SmoothStep(0,1,(age-.155f)/.205f);
            Vector3 start=Vector3.Lerp(hand.position,windup,Mathf.SmoothStep(0,1,Mathf.Min(age/.045f,1)));
            if(age>=.155f)start=hand.position;
            hand.position=Vector3.Lerp(start,strike,extension);
            float close=Mathf.SmoothStep(0,1,age/.07f)*(1-Mathf.SmoothStep(0,1,(age-.24f)/.12f));
            hand.rotation=Quaternion.Slerp(hand.rotation,strikeRotation,close);hand.skeletonTracked=true;
            var fist=CandyHandSkeleton.Rest(false,1);
            fist.SetJoint(1,new Vector3(-.038f,-.013f,.030f));
            fist.SetJoint(2,new Vector3(-.025f,-.022f,.043f));
            fist.SetJoint(3,new Vector3(-.010f,-.025f,.047f));
            for(int i=0;i<20;i++)hand.skeleton.SetJoint(i,Vector3.Lerp(hand.skeleton.Joint(i),fist.Joint(i),close));
            frame.right=hand;
        }
    }
}
