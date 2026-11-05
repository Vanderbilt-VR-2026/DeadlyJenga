using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyEquipment
    {
        const float SuitMoveDeadzone=.18f,SuitLiftDeadzone=.12f;
        internal bool SuitMovementHeld
        {
            get
            {
                if(Equipment.kind!=EquipmentKind.PoweredArmor||!Equipment.drawn)return false;
                if(!motor.IsVR)
                {
                    var keys=Keyboard.current;
                    return keys!=null&&(keys.wKey.isPressed||keys.aKey.isPressed||keys.sKey.isPressed||
                        keys.dKey.isPressed||keys.qKey.isPressed||keys.eKey.isPressed);
                }
                bool controllerLeft=!SuitShootingLeft;
                var controller=HybridXRInput.Controller(controllerLeft);
                float Axis(string name)=>controller?.TryGetChildControl<AxisControl>(name)?.ReadValue()??0;
                return HybridXRInput.Stick(controllerLeft).magnitude>SuitMoveDeadzone||
                    Mathf.Abs(Axis("trigger")-Axis("grip"))>SuitLiftDeadzone;
            }
        }
    }
}
