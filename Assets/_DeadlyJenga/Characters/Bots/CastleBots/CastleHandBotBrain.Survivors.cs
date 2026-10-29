using DeadlyJenga.Core;
using DeadlyJenga.CandyPhysicsHands;
using DeadlyJenga.CandyPlayers;
using UnityEngine;
namespace DeadlyJenga.CastleBots
{
    public sealed partial class CastleHandBotBrain
    {
        Component nearbyActor;float nextActorChoice,actorCycle;
        void HarassSurvivors()
        {
            var motor=Actor.motor;
            if(Time.time>=nextActorChoice)
            {
                nextActorChoice=Time.time+1.2f;float nearest=3600;nearbyActor=null;int inspected=0;
                foreach(var mover in Tracked.Movers)
                {
                    if(++inspected>64)break;if(!(mover is Component candidate)||!candidate||!candidate.gameObject.activeInHierarchy)continue;
                    if(candidate.GetComponent<IDamageable>() is IDamageable health&&health.Health<=0)continue;
                    var delta=candidate.transform.position-ContactPosition;if(delta.sqrMagnitude>=nearest)continue;
                    if(Physics.Linecast(ContactPosition,candidate.transform.position+Vector3.up,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))continue;
                    nearest=delta.sqrMagnitude;nearbyActor=candidate;
                }
            }
            if(!nearbyActor||!nearbyActor.gameObject.activeInHierarchy){Intent="Waiting for an exposed surface or survivor";return;}
            actorCycle+=Time.deltaTime;float cycle=actorCycle%5;var target=nearbyActor.transform.position+Vector3.up*.9f;
            outward=Vector3.ProjectOnPlane(ContactPosition-target,Vector3.up).normalized;if(outward.sqrMagnitude<.1f)outward=Vector3.back;
            float size=Bridge.settings.size;Destination=target+outward*(cycle<2?size*.12f:cycle<2.7f?-size*.035f:size*.18f);
            Intent=cycle<2?"Telegraphing a survivor sweep":cycle<2.7f?"Sweeping past survivor":"Backing away after sweep";
            var velocity=Vector3.ClampMagnitude((Destination-motor.transform.position)*2,Mathf.Min(cycle<2.7f?Personality.pushSpeed:Personality.flightSpeed,motor.maximumSpeed));
            velocity=AvoidHands(velocity);var acceleration=(velocity-motor.Body.linearVelocity)*2+velocity*motor.drag;
            flight.moveWorld=new Vector3(acceleration.x/Mathf.Max(.1f,motor.thrust),acceleration.y/Mathf.Max(.1f,motor.verticalThrust),acceleration.z/Mathf.Max(.1f,motor.thrust));
            flight.yaw=Mathf.Clamp(Vector3.SignedAngle(motor.transform.forward,-outward,Vector3.up)/35,-1,1);
            pose.rotation=AttackRotation();pose.skeleton=DesktopHandPoses.Build(Bridge.useLeftHand,DesktopHandPose.Flat,0);
        }
    }
}
