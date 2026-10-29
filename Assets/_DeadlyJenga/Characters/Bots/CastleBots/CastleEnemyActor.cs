using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;
using UnityEngine;

namespace DeadlyJenga.CastleBots
{
    [DefaultExecutionOrder(280)]
    public sealed class CastleEnemyActor : MonoBehaviour,IEnemyActor,IEnemyMovement,IEnemyPose
    {
        [SerializeField] float hitDamage=20;
        CastleBotBrain brain;
        int accepted;
        bool pursuing;
        public bool IsEnemy=>true;
        public bool PreparingMelee=>brain&&(brain.CombatState==BotCombatState.Windup||brain.CombatState==BotCombatState.Face||brain.CombatState==BotCombatState.Recover);
        [SerializeField] string archetype="panther";
        public string ArchetypeId=>archetype;
        public bool CanClimb=>archetype!="mushroom";
        public float StrikeReach=>archetype=="longhand"?3.25f:2.05f;
        public float Windup=>archetype=="longhand"?.5f:archetype=="gum-brute"?.65f:0;
        public float JumpMultiplier=>archetype=="mushroom"?5:1;
        float naturalJump;
        public float HitDamage=>hitDamage;
        public void Configure(EnemySpawnSettings settings){hitDamage=Mathf.Clamp(settings.hitDamage,1,99);archetype=settings.archetypeId;
            var motor=GetComponent<DeadlyJenga.CastlePrototype.CastleSurvivor>();
            if(motor&&(archetype=="longhand"||archetype=="gum-brute"))motor.playerScale=archetype=="longhand"?1.3f:1.1f;
        }
        public void Despawn(){gameObject.SetActive(false);Destroy(gameObject);}
        void Start()
        {
            brain=GetComponent<CastleBotBrain>();
            bool mayhem=archetype=="longhand"||archetype=="gum-brute";
            if(mayhem){gameObject.name=archetype=="longhand"?"Longhand enemy":"Gum Brute enemy";gameObject.AddComponent<CandyMayhemEnemyVisual>();}
            var avatar=GetComponent<CandyPlayerAvatar>();if(avatar)avatar.ApplyAppearance(mayhem?default:archetype=="mushroom"?new AvatarAppearance{skin=AvatarSkin.MushroomSprite}:new AvatarAppearance{accessories=AvatarAccessory.PantherHead|AvatarAccessory.PantherClaws|AvatarAccessory.PantherTail});
            if(archetype!="mushroom")EnemyActions.Raise(gameObject,EnemyActionKind.Alert);
            if(brain){naturalJump=brain.Motor.jumpHeight;brain.Motor.jumpHeight=naturalJump*JumpMultiplier;if(archetype=="mushroom")brain.Motor.playerScale=.65f;}
        }
        void LateUpdate()
        {
            if(!brain)return;
            bool active=brain.CombatState!=BotCombatState.None;
            if(active&&!pursuing)EnemyActions.Raise(gameObject,EnemyActionKind.Alert);
            pursuing=active;
            if(brain.PunchesAccepted!=accepted){accepted=brain.PunchesAccepted;EnemyActions.Raise(gameObject,EnemyActionKind.Attack);}
        }
    }
}
