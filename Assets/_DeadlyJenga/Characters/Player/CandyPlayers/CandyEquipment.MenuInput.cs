using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;

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
                    return DesktopGamepad.Moving||Mathf.Abs(DesktopGamepad.Lift)>.1f||keys!=null&&(keys.wKey.isPressed||keys.aKey.isPressed||keys.sKey.isPressed||
                        keys.dKey.isPressed||keys.qKey.isPressed||keys.eKey.isPressed);
                }
                return HybridXRInput.Stick(SuitFlightLeft).magnitude > SuitMoveDeadzone ||
                    Mathf.Abs(SuitGripLift()) > SuitLiftDeadzone;
            }
        }
    }
}
