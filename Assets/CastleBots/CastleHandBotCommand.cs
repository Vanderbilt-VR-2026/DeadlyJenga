using UnityEngine;
using DeadlyJenga.CandyPlayers;

namespace DeadlyJenga.CastleBots
{
    // Adapter for a future human hand ability. Commands still use the shared motors.
    public interface ICastleHandBotAbility
    { bool TryAct(CastleHandBotBrain brain,Rigidbody target,out FlyingHand.FlyingHandCommand flight,out CandyHandPose pose); }
}
