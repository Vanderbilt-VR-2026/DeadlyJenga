using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyEquipment
    {
        Vector3 lastOffhand;bool offhandReady;
        void CheckOffhandClaws()
        {
            var hand=avatar.tracking.WorldHand(!left);var relative=hand.position-avatar.viewCamera.transform.position;
            if(hand.tracked&&offhandReady&&Time.time>=nextFire&&(relative-lastOffhand).magnitude/Mathf.Max(.001f,Time.deltaTime)/Scale>1.4f)
                FireAt(new Pose(hand.position+hand.rotation*Vector3.forward*.1f*Scale,hand.rotation),true);
            lastOffhand=relative;offhandReady=hand.tracked;
        }
        bool sentHeld;float nextHeld;
        void ReleaseHeldArsenal(){offhandReady=false;if(sentHeld){(Runtime as IHeldEquipmentRuntime)?.RequestEquipmentTrigger(false,MuzzlePose);sentHeld=false;}}
        void UpdateHeldArsenal(bool held,EquipmentState state)
        {
            if(!held||motor.IsVR&&(!avatar.tracking||!avatar.tracking.WorldHand(left).tracked)){ReleaseHeldArsenal();return;}
            if(Time.unscaledTime<nextHeld&&sentHeld)return;
            nextHeld=Time.unscaledTime+.08f;sentHeld=(Runtime as IHeldEquipmentRuntime)?.RequestEquipmentTrigger(true,AssistAim(MuzzlePose))==true;
        }
    }
}
