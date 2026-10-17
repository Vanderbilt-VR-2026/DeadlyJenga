using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public enum StaminaCurve { Linear, PowerIn, PowerOut, Exponential }

    // Invert the current fraction before advancing: live tuning never resets spent stamina,
    // and curves reach their endpoints in finite time independently of frame rate.
    public static class CastleStaminaCurve
    {
        public static float Evaluate(float t,StaminaCurve curve,float degree)
        {
            t=Mathf.Clamp01(t);float p=Mathf.Clamp(degree,1,6);
            switch(curve)
            {
                case StaminaCurve.PowerIn:return Mathf.Pow(t,p);
                case StaminaCurve.PowerOut:return 1-Mathf.Pow(1-t,p);
                case StaminaCurve.Exponential:return (Mathf.Exp(p*t)-1)/(Mathf.Exp(p)-1);
                default:return t;
            }
        }
        public static float Advance(float fraction,float phaseDelta,StaminaCurve curve,float degree)
        {
            fraction=Mathf.Clamp01(fraction);if(phaseDelta<=0||fraction>=1)return fraction;
            float p=Mathf.Clamp(degree,1,6),t=fraction;
            switch(curve)
            {
                case StaminaCurve.PowerIn:t=Mathf.Pow(fraction,1/p);break;
                case StaminaCurve.PowerOut:t=1-Mathf.Pow(1-fraction,1/p);break;
                case StaminaCurve.Exponential:t=Mathf.Log(1+fraction*(Mathf.Exp(p)-1))/p;break;
            }
            return Evaluate(t+phaseDelta,curve,p);
        }
    }

    public sealed partial class CastleSurvivor
    {
        [Header("Stamina tuning (safe to change during play)")]
        [Tooltip("Effort units spent per second. Full moving climb duration = capacity / drain rate, for every curve.")]
        [Min(0)] public float climbDrainPerSecond=1;
        [Min(0)] public float climbRecoveryDelay=.45f;
        public StaminaCurve climbDrainCurve=StaminaCurve.Linear,climbRecoveryCurve=StaminaCurve.Linear;
        [Range(1,6)] public float climbDrainDegree=2,climbRecoveryDegree=2;
        public bool StaminaRecovering=>!Climbing&&GripRemaining<1&&climbGroundRest>Mathf.Max(0,climbRecoveryDelay)&&climbRecoveryPerSecond>0;
        public bool StaminaExhausted=>GripRemaining<=.001f;
    }
}
