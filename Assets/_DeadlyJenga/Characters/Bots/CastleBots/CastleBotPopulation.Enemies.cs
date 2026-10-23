using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastleBots
{
    public sealed partial class CastleBotPopulation
    {
        public int EnemyCount=>bots.Count(b=>b&&b.GetComponent<CastleEnemyActor>());
        public GameObject SpawnEnemy(EnemySpawnSettings settings)
        {
            if(settings==null||(settings.archetypeId!="panther"&&settings.archetypeId!="jaguar")||float.IsNaN(settings.hitDamage)||float.IsInfinity(settings.hitDamage)||EnemyCount>=32||MatchRuntime.Current?.Authority!=true)return null;
            return SpawnBot(settings)?.gameObject;
        }
        public void ClearEnemies()
        {
            for(int i=bots.Count-1;i>=0;i--)if(bots[i]&&bots[i].GetComponent<CastleEnemyActor>()){var bot=bots[i];bots.RemoveAt(i);bot.gameObject.SetActive(false);Destroy(bot.gameObject);}
        }
        void RemoveLastSurvivor()
        {
            for(int i=bots.Count-1;i>=0;i--)if(!bots[i]||!bots[i].GetComponent<CastleEnemyActor>()){var bot=bots[i];bots.RemoveAt(i);if(bot){bot.gameObject.SetActive(false);Destroy(bot.gameObject);}return;}
        }
        bool PlaceEnemy(CastleBotBrain bot)
        {
            var world=Session.world;if(world==null)return false;
            bot.Motor.ApplyModeRules(new ModeRules{island=IslandRule.Walkable,healthEnabled=true,punchEnabled=true,punchDamage=bot.GetComponent<CastleEnemyActor>().HitDamage});
            var bounds=world.PlayableBounds;Physics.SyncTransforms();
            for(int ring=0;ring<4;ring++)for(int step=0;step<48;step++)
            {
                float angle=(serial*137.508f+step*7.5f)*Mathf.Deg2Rad;
                float radius=.94f-ring*.08f;
                var point=new Vector3(bounds.center.x+Mathf.Cos(angle)*bounds.extents.x*radius,bounds.max.y+4,bounds.center.z+Mathf.Sin(angle)*bounds.extents.z*radius);
                if(!Physics.Raycast(point,Vector3.down,out var hit,bounds.size.y+12,(1<<Layers.Surface)|(1<<Layers.Block)|1,QueryTriggerInteraction.Ignore)||hit.normal.y<.65f)continue;
                point=hit.point+Vector3.up*.06f;
                if(!CastleBotNavigation.Landing(bot.Motor,point,out _)||Physics.CheckCapsule(point+Vector3.up*.35f,point+Vector3.up*bot.Motor.StandingHeight,.30f,1<<Layers.Survivor,QueryTriggerInteraction.Ignore)||bots.Any(b=>b&&Vector3.Distance(b.transform.position,point)<1))continue;
                var forward=Vector3.ProjectOnPlane(world.Center-point,Vector3.up);bot.Motor.Teleport(point,forward.sqrMagnitude>.01f?Quaternion.LookRotation(forward):Quaternion.identity);return true;
            }
            return false;
        }
    }
}
