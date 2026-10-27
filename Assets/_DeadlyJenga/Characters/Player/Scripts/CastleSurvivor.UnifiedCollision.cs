using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleSurvivor
    {
        public static bool UnifiedRecoveryEnabled=true;
        static readonly Unity.Profiling.ProfilerMarker RecoveryMarker=new Unity.Profiling.ProfilerMarker("DJ.UnifiedRecovery");
        Collider[] recoveryContacts=new Collider[32];
        CapsuleCollider recoveryCapsule;
        int recoveryFrame=-1;
        void RecoverUnifiedOverlap()
        {
            if(!UnifiedRecoveryEnabled||!TraversalCollision.Unified||recoveryFrame==Time.frameCount)return;
            using var measurement=RecoveryMarker.Auto();
            recoveryFrame=Time.frameCount;
            if(!recoveryCapsule){recoveryCapsule=gameObject.AddComponent<CapsuleCollider>();recoveryCapsule.enabled=false;}
            recoveryCapsule.center=Capsule.center;recoveryCapsule.radius=Capsule.radius*.98f;recoveryCapsule.height=Capsule.height;
            for(int pass=0;pass<4;pass++)
            {
                var center=transform.TransformPoint(Capsule.center);float half=Mathf.Max(0,Capsule.height*.5f-Capsule.radius);
                int count;
                do
                {
                    count=Physics.OverlapCapsuleNonAlloc(center-Vector3.up*half,center+Vector3.up*half,Capsule.radius,recoveryContacts,1<<Layers.Block,QueryTriggerInteraction.Ignore);
                    if(count<recoveryContacts.Length||recoveryContacts.Length>=256)break;
                    System.Array.Resize(ref recoveryContacts,recoveryContacts.Length*2);
                }while(true);
                bool moved=false;
                for(int i=0;i<count;i++)
                {
                    var hit=recoveryContacts[i];if(!hit||!hit.enabled)continue;
                    var position=hit.transform.position;var rotation=hit.transform.rotation;var rb=hit.attachedRigidbody;
                    if(rb){position=rb.position+rb.rotation*rb.transform.InverseTransformPoint(position);rotation=rb.rotation*Quaternion.Inverse(rb.transform.rotation)*rotation;}
                    if(Physics.ComputePenetration(recoveryCapsule,transform.position,transform.rotation,hit,position,rotation,out var direction,out float distance)&&distance>.002f)
                    {Capsule.Move(direction*Mathf.Min(distance+.002f,2));moved=true;}
                }
                if(!moved)break;
            }
        }
    }
}
