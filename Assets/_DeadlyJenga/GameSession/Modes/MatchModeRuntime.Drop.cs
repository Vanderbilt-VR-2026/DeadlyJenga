using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime : IEquipmentDropRuntime
    {
        readonly Dictionary<string,int> manualDropRequests=new Dictionary<string,int>();
        int nextManualDropRequest;

        public bool RequestDropEquipment()
        {
            if(nextManualDropRequest==int.MaxValue)return false;
            return SendEquipment(new EquipmentCommand{action="drop",dropRequest=++nextManualDropRequest});
        }

        bool BeginManualDropRequest(string id,int request)
        {
            if(!Authority||string.IsNullOrEmpty(id)||request<=0||
                manualDropRequests.TryGetValue(id,out int seen)&&request<=seen)return false;
            // Consume before the actor/life gate, including requests that fail while dead.
            manualDropRequests[id]=request;
            return true;
        }

        void OnParticipantConnectionLeft(string id)
        {
            manualDropRequests.Remove(id);
            UnregisterRemote(id);
        }

        bool DropEquipmentManually(string id)
        {
            if(!Authority||!equipment.TryGetValue(id,out var state)||state.kind<EquipmentKind.Speed||
                state.kind>EquipmentKind.Dagger||state.builtIn||state.roleIssued||
                state.kind==EquipmentKind.PoweredArmor&&state.drawn||pickups.Count>=128||
                float.IsNaN(state.remaining)||float.IsInfinity(state.remaining))return false;
            bool timed=state.kind==EquipmentKind.Speed||state.kind==EquipmentKind.Jump;
            if(timed&&state.remaining<=0)return false;
            var actor=FindActor(id);
            if(actor==null||!TryDropPosition(actor,out var point))return false;
            var drop=AddPickup(state.kind,point,state.remaining);
            if(drop==null)return false;
            drop.availableIn=1;
            drop.rounds=state.magazineInitialized?state.rounds:-1;
            StopArsenalTrigger(id);
            equipment.Remove(id);weaponCooldown.Remove(id);thrustSeen.Remove(id);
            AnnounceEquipment(id,state,EquipmentAction.Drop,point);
            PublishEquipment();
            return true;
        }

        Vector3 FallenEquipmentPosition(string id,Vector3 feet)
        {
            var actor=FindActor(id);
            if(actor!=null&&TryDropPosition(actor,out var nearby,.68f))return nearby;
            const int floorMask=(1<<Layers.Block)|(1<<Layers.Surface)|1;
            if(Physics.Raycast(feet+Vector3.up*.25f,Vector3.down,out var floor,6,floorMask,
                QueryTriggerInteraction.Ignore)&&floor.normal.y>=.55f)
                return floor.point+Vector3.up*.25f;
            return feet+Vector3.up*.25f;
        }

        bool TryDropPosition(IModeActor actor,out Vector3 point,float distance=1.45f)
        {
            point=Vector3.zero;
            var feet=actor.transform.position;
            var forward=Vector3.ProjectOnPlane(actor.ViewPose.rotation*Vector3.forward,Vector3.up);
            if(!Finite(feet)||!Finite(forward))return false;
            if(forward.sqrMagnitude<.0001f)forward=Vector3.forward;
            forward.Normalize();
            var side=Vector3.Cross(Vector3.up,forward);
            var origin=feet+Vector3.up*.5f;
            const int mask=(1<<Layers.Block)|(1<<Layers.Surface)|1;
            for(int i=0;i<4;i++)
            {
                var direction=i==0?forward:i==1?side:i==2?-side:-forward;
                var candidate=feet+direction*distance+Vector3.up*.35f;
                if(!Physics.Raycast(candidate+Vector3.up*.3f,Vector3.down,out var floor,1.5f,mask,QueryTriggerInteraction.Ignore)||
                    floor.normal.y<.55f)continue;
                candidate=floor.point+Vector3.up*.35f;
                if(!Finite(candidate)||(candidate-feet).sqrMagnitude>1.65f*1.65f||
                    Physics.CheckSphere(candidate,.18f,mask,QueryTriggerInteraction.Ignore))continue;
                var path=candidate-origin;
                if(Physics.SphereCast(origin,.16f,path.normalized,out _,path.magnitude,mask,QueryTriggerInteraction.Ignore))continue;
                point=candidate;
                return true;
            }
            return false;
        }
    }
}
