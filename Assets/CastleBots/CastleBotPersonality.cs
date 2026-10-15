using UnityEngine;

namespace DeadlyJenga.CastleBots
{
    [CreateAssetMenu(menuName="Deadly Jenga/Bot Personality")]
    public sealed class CastleBotPersonality : ScriptableObject
    {
        public string displayName="Balanced";
        [Range(0,1)] public float goofiness=.15f, bravery=.45f, sociability=.4f, exploration=.65f;
        [Range(0,1)] public float survivalPriority=.8f;
        [Range(.15f,1.5f)] public float decisionInterval=.4f;
        [Range(.5f,1.2f)] public float pace=.9f;
        [Header("Trips and resting")]
        [Range(0,1)] public float journeyDrive=.7f,verticalVariety=.7f,hidePreference=.25f;
        [Range(5,60)] public float travelSeconds=24;
        [Range(1,20)] public float restSeconds=6;
        public static CastleBotPersonality Create(int preset)
        {
            var p=CreateInstance<CastleBotPersonality>();
            switch(preset)
            {
                case 1:p.displayName="Careful";p.goofiness=.05f;p.bravery=.15f;p.survivalPriority=1;p.exploration=.4f;p.pace=.8f;p.journeyDrive=.45f;p.verticalVariety=.5f;p.hidePreference=.6f;p.travelSeconds=18;p.restSeconds=10;break;
                case 2:p.displayName="Daredevil";p.goofiness=.25f;p.bravery=.9f;p.exploration=1;p.survivalPriority=.55f;p.pace=1;p.journeyDrive=1;p.verticalVariety=1;p.hidePreference=.05f;p.travelSeconds=35;p.restSeconds=2;break;
                case 3:p.displayName="Goofy";p.goofiness=.8f;p.bravery=.6f;p.sociability=.85f;p.survivalPriority=.65f;p.pace=.85f;p.journeyDrive=.85f;p.verticalVariety=.85f;p.hidePreference=.15f;p.travelSeconds=25;p.restSeconds=4;break;
            }
            p.name=p.displayName;return p;
        }
    }
}
