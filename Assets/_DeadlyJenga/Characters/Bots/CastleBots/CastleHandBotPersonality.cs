using UnityEngine;
namespace DeadlyJenga.CastleBots
{
    [CreateAssetMenu(menuName="Deadly Jenga/Hand Bot Personality")]
    public sealed class CastleHandBotPersonality:ScriptableObject
    {
        public string displayName="Playful";
        [Range(0,1)]public float aggression=.45f,goofiness=.45f,targetSurvivors=.65f;
        [Range(.5f,6)]public float flightSpeed=3;
        [Range(1,10)]public float recoverySeconds=4;
        [Range(.4f,2)]public float attackSeconds=.9f;
        [Header("Contact attack")]
        [Range(1,10)] public float pushSpeed=4.5f;
        [Range(1,8)] public float pushDistance=4;
        [Range(.05f,1)] public float settleSeconds=.18f;
        public static CastleHandBotPersonality Create(int i)
        {
            var p=CreateInstance<CastleHandBotPersonality>();
            if(i==1){p.displayName="Gentle";p.aggression=.15f;p.flightSpeed=2;p.recoverySeconds=7;p.goofiness=.2f;}
            if(i==2){p.displayName="Tactical";p.aggression=.7f;p.goofiness=.05f;p.targetSurvivors=1;p.recoverySeconds=3;}
            if(i==3){p.displayName="Relentless";p.aggression=1;p.goofiness=.1f;p.flightSpeed=4;p.recoverySeconds=1.5f;}
            p.name=p.displayName;return p;
        }
    }
}
