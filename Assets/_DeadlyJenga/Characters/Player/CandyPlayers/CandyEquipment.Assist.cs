using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyEquipment
    {
        readonly RaycastHit[] assistHits=new RaycastHit[24];
        Pose AssistAim(Pose muzzle)
        {
            var state=Equipment;var spec=EquipmentSpecifications.Get(state.kind);
            if(motor.externallyDriven||spec.melee||!state.drawn||spec.gravity>0)return muzzle;
            var forward=muzzle.rotation*Vector3.forward;float range=Mathf.Min(65,spec.range>0?spec.range:28);
            int count=Physics.SphereCastNonAlloc(muzzle.position,.12f*Scale,forward,assistHits,range,(1<<Layers.Survivor)|(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Collide);
            if(count==assistHits.Length)return muzzle;float nearest=range;IModeActor target=null;
            var self=GetComponent<IModeActor>();
            for(int i=0;i<count;i++)
            {
                var hit=assistHits[i];var actor=hit.collider.GetComponentInParent<IModeActor>();if(actor==self||hit.collider.isTrigger&&actor==null||hit.distance>=nearest)continue;
                nearest=hit.distance;target=actor;
            }
            if(target==null||!target.Alive||self!=null&&self.Team>=0&&self.Team==target.Team)return muzzle;
            var point=target.transform.position+Vector3.up*Mathf.Max(.3f,(target.ViewPose.position.y-target.transform.position.y)*.55f);var delta=point-muzzle.position;
            if(Vector3.Angle(forward,delta)>2||Physics.Linecast(muzzle.position,point,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))return muzzle;
            muzzle.rotation=Quaternion.LookRotation(Vector3.RotateTowards(forward,delta.normalized,.6f*Mathf.Deg2Rad,0));return muzzle;
        }
    }
}
