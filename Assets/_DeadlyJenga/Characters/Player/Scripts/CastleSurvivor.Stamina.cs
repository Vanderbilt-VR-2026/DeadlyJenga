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
        public bool StaminaRecovering=>!Climbing&&!sprintStaminaActive&&GripRemaining<1&&climbGroundRest>Mathf.Max(0,climbRecoveryDelay)&&climbRecoveryPerSecond>0;
        public bool StaminaExhausted=>GripRemaining<=.001f;
        public bool SprintRequested {get;private set;}
        bool sprintToggleArmed,sprintStaminaActive;
        float sprintFeedbackUntil;
        string sprintFeedback;
        string SprintStatusText=>Sprinting?"SPRINTING · stamina "+Mathf.CeilToInt(GripRemaining*100)+"%":
            SprintRequested?"SPRINT READY · move to run · stamina "+Mathf.CeilToInt(GripRemaining*100)+"%":sprintFeedback;

        void UpdateSprintToggle(bool available,bool pressed,bool held)
        {
            if(!available){ClearSprint();return;}
            if(!held){sprintToggleArmed=true;return;}
            if(!pressed||!sprintToggleArmed)return;
            sprintToggleArmed=false;
            if(SprintRequested)
            {
                ClearSprint();sprintFeedback="SPRINT OFF";sprintFeedbackUntil=Time.unscaledTime+1.5f;
            }
            else if(GripRemaining>.001f||PowerupUnlimitedStamina)SprintRequested=true;
            else
            {
                sprintFeedback="SPRINT EXHAUSTED · recover first";sprintFeedbackUntil=Time.unscaledTime+2;
            }
        }
        bool CanSprint(Vector2 input)=>SprintRequested&&Grounded&&input.sqrMagnitude>.001f&&
            Alive&&!InputSuspended&&vrInputFocused&&!externallyDriven&&!Climbing&&!Vaulting&&!Sinking&&
            modeRules?.downed!=true&&movementMultiplier>0&&(GripRemaining>.001f||PowerupUnlimitedStamina);

        void UpdateSprintStamina(Vector2 input,float dt)
        {
            Sprinting=CanSprint(input);sprintStaminaActive=Sprinting;
            if(!Sprinting)return;
            climbGroundRest=0;
            if(!PowerupUnlimitedStamina)
                GripRemaining=1-CastleStaminaCurve.Advance(1-GripRemaining,
                    dt*Mathf.Max(0,climbDrainPerSecond)/Mathf.Max(.2f,climbGripSeconds),climbDrainCurve,climbDrainDegree);
            if(GripRemaining<=.001f&&!PowerupUnlimitedStamina)
            {
                ClearSprint();sprintFeedback="SPRINT EXHAUSTED · recover, then click to sprint";
                sprintFeedbackUntil=Time.unscaledTime+2;
            }
        }
    }
}
