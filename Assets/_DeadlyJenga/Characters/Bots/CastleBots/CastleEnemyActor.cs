using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;
using UnityEngine;

namespace DeadlyJenga.CastleBots
{
    [DefaultExecutionOrder(280)]
    public sealed class CastleEnemyActor : MonoBehaviour,IEnemyActor
    {
        [SerializeField] float hitDamage=20;
        CastleBotBrain brain;
        int accepted;
        bool pursuing;
        public bool IsEnemy=>true;
        public string ArchetypeId=>"panther";
        public float HitDamage=>hitDamage;
        public void Configure(EnemySpawnSettings settings){hitDamage=Mathf.Clamp(settings.hitDamage,1,99);}
        public void Despawn(){gameObject.SetActive(false);Destroy(gameObject);}
        void Start()
        {
            brain=GetComponent<CastleBotBrain>();
            var avatar=GetComponent<CandyPlayerAvatar>();if(avatar)avatar.ApplyAppearance(new AvatarAppearance{accessories=AvatarAccessory.PantherHead|AvatarAccessory.PantherClaws|AvatarAccessory.PantherTail});
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
