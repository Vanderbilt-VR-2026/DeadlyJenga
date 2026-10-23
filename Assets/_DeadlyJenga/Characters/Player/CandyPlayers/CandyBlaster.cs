using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    // Compatibility for the existing demo controls. All weapons use CandyEquipment.
    public sealed class CandyBlaster:MonoBehaviour,IBlasterControls
    {
        CandyEquipment Equipment=>GetComponent<CandyEquipment>();
        IBlasterRuntime Runtime=>MatchRuntime.Current as IBlasterRuntime;
        public bool TestingEnabled=>Runtime?.BlasterTestingEnabled==true;
        public bool Drawn=>Equipment?.Equipment.kind==EquipmentKind.Blaster&&Equipment.Equipment.drawn;
        public bool LeftHanded=>PlayerPrefs.GetInt("CandyBlaster.LeftHanded",0)!=0;
        public bool JumpHeld=>Equipment?.JumpHeld==true;
        public Pose AimPose=>Equipment?Equipment.GripPose:new Pose(transform.position,transform.rotation);
        public Pose MuzzlePose=>Equipment?Equipment.MuzzlePose:AimPose;
        public void SetTestingEnabled(bool value){Runtime?.SetBlasterTesting(value);GetComponent<CastleSurvivor>()?.ResetVRJump();}
        public void SetDrawn(bool value){if(Equipment?.Equipment.kind==EquipmentKind.Blaster)Equipment.SetEquipped(value);}
        public void SetLeftHanded(bool value)=>Equipment?.SetWeaponHand(value);
        public bool FireOnce()=>Drawn&&Equipment.FireOnce();
    }
}
