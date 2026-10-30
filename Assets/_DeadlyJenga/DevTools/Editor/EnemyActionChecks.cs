using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyAudio;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static class EnemyActionChecks
    {
        public static MatchFrame Baseline {get;private set;}
        public static MatchFrame Accepted {get;private set;}
        static MatchFrame Clone(MatchFrame frame)=>JsonUtility.FromJson<MatchFrame>(JsonUtility.ToJson(frame));
        static void Call(MatchModeRuntime runtime,string name,params object[] args)=>typeof(MatchModeRuntime).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(runtime,args);
        sealed class RiflePickup:IEquipmentPickupSource
        {
            readonly Vector3 position;
            public RiflePickup(Vector3 point){position=point;}
            public EquipmentKind Kind=>EquipmentKind.DualRifle;
            public Vector3 Position=>position;
            public float Duration=>20;
        }
        public static IEnumerator Run(CastlePlaySession session,Action<bool,string> check)
        {
            var runtime=session.Modes;var player=session.Survivor();var oldPosition=player.transform.position;var oldRotation=player.transform.rotation;bool suspended=player.InputSuspended;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Enemy action validation floor";floor.layer=Layers.Surface;floor.transform.position=new Vector3(1500,10,1500);floor.transform.localScale=new Vector3(8,1,8);
            GameObject enemy=null;var savedEquipment=Clone(runtime.Frame);
            var equipment=(IEquipmentRuntime)runtime;
            try
            {
                player.Teleport(floor.transform.position+Vector3.up*.54f,Quaternion.identity);player.InputSuspended=true;yield return new WaitForSeconds(.3f);
                enemy=(session.SurvivorBots as IEnemyPopulation)?.SpawnEnemy(new EnemySpawnSettings{archetypeId="jaguar"});check(enemy,"Real enemy population spawns a jaguar for accepted event checks");
                enemy.GetComponent<DeadlyJenga.CastleBots.CastleBotBrain>().enabled=false;
                var motor=enemy.GetComponent<CastleSurvivor>();motor.InputSuspended=true;motor.Teleport(floor.transform.position+new Vector3(2,.54f,0),Quaternion.identity);yield return new WaitForSeconds(.3f);
                Call(runtime,"TickEnemyActions",2f);Call(runtime,"PublishEnemyActions");Baseline=Clone(runtime.Frame);
                var cursor=new EnemyActionPlayback();check(cursor.Consume(runtime,Baseline,_=>{})==0,"Enemy event receiver baselines without replaying history");
                var audio=Object.FindFirstObjectByType<CandyJaguarAudio>();check(audio,"Actual jaguar audio observer is active");int consumed=audio.ConsumedActions,played=audio.PlayedActions;
                int pickup=equipment.RegisterPickup(new RiflePickup(player.transform.position+Vector3.up*.35f));
                check(pickup>0&&equipment.RequestPickup(pickup,true)&&equipment.RequestEquipment(true,false,false),"Real authored pickup and draw commands produce a rifle event for network transport");
                check(equipment.RequestEquipmentAttack(new Pose(player.ViewCamera.transform.position,Quaternion.identity)),"Real accepted rifle attack shares the captured NPC action snapshot");
                EnemyActions.Raise(enemy,EnemyActionKind.Alert);EnemyActions.Raise(enemy,EnemyActionKind.Attack);
                enemy.GetComponent<IDamageable>().Damage(5,player.gameObject);yield return null;
                var batch=Clone(runtime.Frame);check(batch.enemyActions.Any(e=>e.action==EnemyActionKind.Alert)&&batch.enemyActions.Any(e=>e.action==EnemyActionKind.Attack)&&batch.enemyActions.Any(e=>e.action==EnemyActionKind.Hurt),"Typed enemy alert / attack and actual damage are published by the authority");
                check(EnemyActionValidation.Valid(batch),"Published enemy action frame passes bounded protocol validation");
                check(cursor.Consume(runtime,batch,_=>{})==3&&cursor.Consume(runtime,batch,_=>{})==0,"All accepted enemy actions play once across duplicate snapshots");
                yield return null;check(audio.ConsumedActions>=consumed+3&&audio.PlayedActions>played,"Real presentation observer consumes accepted combat actions and starts a pooled sound");
                int firstSequence=batch.enemyActionSequence;EnemyActions.Raise(player.gameObject,EnemyActionKind.Attack);check(runtime.Frame.enemyActionSequence==firstSequence,"Non-enemy actors cannot announce NPC combat actions");
                enemy.GetComponent<IDamageable>().Eliminate(player.gameObject);yield return new WaitForSeconds(.22f);
                Accepted=Clone(runtime.Frame);check(Accepted.enemyActions.Any(e=>e.action==EnemyActionKind.Death)&&cursor.Consume(runtime,Accepted,_=>{})==1,"Actual eliminated / removed enemy retains a positional death announcement");
                var late=new EnemyActionPlayback();check(late.Consume(runtime,Accepted,_=>{})==0,"Late joining enemy playback does not replay old combat / deaths");
                var invalid=Clone(Accepted);invalid.enemyActions[0].position=new Vector3(float.NaN,0,0);check(!EnemyActionValidation.Valid(invalid),"Network validation rejects nonfinite enemy event positions");
                invalid=Clone(Accepted);invalid.enemyActions[0].id=0;check(!EnemyActionValidation.Valid(invalid),"Network validation rejects invalid enemy action identifiers");
                invalid=Clone(Accepted);invalid.enemyActions[0].remaining=2;check(!EnemyActionValidation.Valid(invalid),"Network validation rejects excessive enemy event lifetime");
                int sequence=Accepted.enemyActionSequence;Call(runtime,"RestoreEnemyActions",Accepted);Call(runtime,"PublishEnemyActions");check(runtime.Frame.enemyActions.Length==0&&runtime.Frame.enemyActionSequence==sequence&&runtime.Frame.enemyActionEpoch==Accepted.enemyActionEpoch,"Authority restore preserves enemy event sequence / epoch without restoring stale sounds");
                enemy=(session.SurvivorBots as IEnemyPopulation).SpawnEnemy(new EnemySpawnSettings{archetypeId="jaguar"});check(enemy,"Enemy fixture respawns after the real elimination");
                enemy.GetComponent<DeadlyJenga.CastleBots.CastleBotBrain>().enabled=false;motor=enemy.GetComponent<CastleSurvivor>();motor.InputSuspended=true;motor.Teleport(floor.transform.position+new Vector3(2,.54f,0),Quaternion.identity);
                EnemyActions.Raise(enemy,EnemyActionKind.Attack);check(cursor.Consume(runtime,Clone(runtime.Frame),_=>{})==1,"First new enemy action after authority restore plays once");
                for(int i=0;i<80;i++)EnemyActions.Raise(enemy,EnemyActionKind.Attack);
                check(runtime.Frame.enemyActions.Length==64&&EnemyActionValidation.Valid(runtime.Frame),"Burst NPC actions remain bounded to 64 ordered events");
                Call(runtime,"TickEnemyActions",2f);Call(runtime,"PublishEnemyActions");check(runtime.Frame.enemyActions.Length==0,"Expired enemy action history is released");
            }
            finally
            {
                if(enemy)Object.Destroy(enemy);Object.Destroy(floor);player.Teleport(oldPosition,oldRotation);player.InputSuspended=suspended;
                Call(runtime,"TickEnemyActions",2f);Call(runtime,"PublishEnemyActions");savedEquipment.equipmentEpoch=runtime.Frame.equipmentEpoch;savedEquipment.equipmentSequence=runtime.Frame.equipmentSequence;Call(runtime,"RestoreEquipment",savedEquipment);Call(runtime,"PublishEquipment");
            }
        }
    }
}
