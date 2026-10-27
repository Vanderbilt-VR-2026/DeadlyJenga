using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        const int EnemyActionCapacity=64;
        const float EnemyActionLifetime=1.2f;
        readonly List<EnemyActionFrame> enemyActionHistory=new List<EnemyActionFrame>(EnemyActionCapacity);
        readonly List<ExpressionFrame> expressions=new List<ExpressionFrame>();
        readonly Dictionary<string,float> expressionCooldown=new Dictionary<string,float>();
        int expressionSequence;
        string enemyActionEpoch;
        int enemyActionSequence;
        void InitializeEnemyActions()
        {
            enemyActionEpoch=Guid.NewGuid().ToString("N");enemyActionSequence=0;
            GameEvents.Emoted+=AnnounceExpression;EnemyActions.Occurred+=AnnounceEnemyAction;GameEvents.Damaged+=EnemyDamaged;GameEvents.Eliminated+=EnemyDied;
            PublishEnemyActions();
        }
        void ClearEnemyActions()
        {
            GameEvents.Emoted-=AnnounceExpression;EnemyActions.Occurred-=AnnounceEnemyAction;GameEvents.Damaged-=EnemyDamaged;GameEvents.Eliminated-=EnemyDied;
            enemyActionHistory.Clear();
        }
        void AnnounceExpression(GameObject who,string name)
        {
            if(!Authority||!who||string.IsNullOrEmpty(name)||name.Length>64)return;var actor=who.GetComponent<IModeActor>();if(actor==null||!actor.Alive)return;
            if(expressionCooldown.TryGetValue(actor.ActorId,out float until)&&Time.time<until)return;expressionCooldown[actor.ActorId]=Time.time+2;
            if(expressions.Count>=32)expressions.RemoveAt(0);expressions.Add(new ExpressionFrame{id=++expressionSequence,actor=actor.ActorId,name=name,position=actor.ViewPose.position,remaining=3});
        }
        void EnemyDamaged(GameObject who,float amount)
        {if(amount>0)AnnounceEnemyAction(who,EnemyActionKind.Hurt,Mathf.Clamp(amount/25,.4f,1));}
        void EnemyDied(GameObject who,GameObject source)=>AnnounceEnemyAction(who,EnemyActionKind.Death,1);
        void AnnounceEnemyAction(GameObject who,EnemyActionKind action,float strength)
        {
            if(!Authority||!ReferenceEquals(MatchRuntime.Current,this)||!who||!Enum.IsDefined(typeof(EnemyActionKind),action)||float.IsNaN(strength)||float.IsInfinity(strength))return;
            var enemy=who.GetComponent<IEnemyActor>();if(enemy?.IsEnemy!=true)return;
            var actor=who.GetComponent<IModeActor>();if(actor==null)return;
            string id=actorIds.TryGetValue(who,out var registered)?registered:who.GetComponent<ISocialBot>()?.NetworkId??actor.ActorId;
            if(string.IsNullOrEmpty(id))id="npc:"+who.GetInstanceID();
            if(string.IsNullOrEmpty(enemy.ArchetypeId)||enemy.ArchetypeId.Length>40||id.Length>128)return;
            Vector3 position=who.transform.position+Vector3.up*Mathf.Clamp(actor.ViewPose.position.y-who.transform.position.y,.3f,3)*.7f;
            if(!EnemyActionValidation.Position(position))return;
            if(enemyActionSequence==int.MaxValue){enemyActionEpoch=Guid.NewGuid().ToString("N");enemyActionSequence=0;enemyActionHistory.Clear();}
            if(enemyActionHistory.Count>=EnemyActionCapacity)enemyActionHistory.RemoveAt(0);
            enemyActionHistory.Add(new EnemyActionFrame{id=++enemyActionSequence,actor=id,archetype=enemy.ArchetypeId,action=action,position=position,strength=Mathf.Clamp01(strength),remaining=EnemyActionLifetime});
            PublishEnemyActions();
        }
        void TickEnemyActions(float dt)
        {
            if(!Authority){enemyActionHistory.Clear();return;}
            for(int i=expressions.Count-1;i>=0;i--){expressions[i].remaining-=Mathf.Max(0,dt);if(expressions[i].remaining<=0)expressions.RemoveAt(i);}
            for(int i=enemyActionHistory.Count-1;i>=0;i--){enemyActionHistory[i].remaining-=Mathf.Max(0,dt);if(enemyActionHistory[i].remaining<=0)enemyActionHistory.RemoveAt(i);}
        }
        void PublishEnemyActions()
        {
            if(frame==null||!Authority)return;
            frame.enemyActionEpoch=enemyActionEpoch??"";frame.enemyActionSequence=enemyActionSequence;
            var copy=new EnemyActionFrame[enemyActionHistory.Count];
            for(int i=0;i<copy.Length;i++){var item=enemyActionHistory[i];copy[i]=new EnemyActionFrame{id=item.id,actor=item.actor,archetype=item.archetype,action=item.action,position=item.position,strength=item.strength,remaining=item.remaining};}
            frame.enemyActions=copy;frame.expressionSequence=expressionSequence;frame.expressions=expressions.ToArray();
        }
        void RestoreEnemyActions(MatchFrame saved)
        {
            enemyActionHistory.Clear();expressions.Clear();expressionCooldown.Clear();expressionSequence=saved?.expressionSequence??0;
            enemyActionEpoch=!string.IsNullOrEmpty(saved?.enemyActionEpoch)?saved.enemyActionEpoch:Guid.NewGuid().ToString("N");
            enemyActionSequence=Mathf.Max(0,saved?.enemyActionSequence??0);
        }
    }
    public static class EnemyActionValidation
    {
        static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n)&&Mathf.Abs(n)<100000000;
        public static bool Position(Vector3 p)=>Finite(p.x)&&Finite(p.y)&&Finite(p.z);
        public static bool Valid(MatchFrame frame)
        {
            if(frame.enemyActions==null||frame.enemyActions.Length>64||frame.enemyActionSequence<0||frame.enemyActionEpoch==null||frame.enemyActionEpoch.Length>64)return false;
            if(frame.enemyActionSequence>0&&frame.enemyActionEpoch.Length==0)return false;
            int previous=0;
            foreach(var item in frame.enemyActions)
            {
                if(item==null||item.id<=previous||item.id>frame.enemyActionSequence||string.IsNullOrEmpty(item.actor)||item.actor.Length>128||string.IsNullOrEmpty(item.archetype)||item.archetype.Length>40||!Enum.IsDefined(typeof(EnemyActionKind),item.action)||!Position(item.position)||!Finite(item.strength)||item.strength<0||item.strength>1||!Finite(item.remaining)||item.remaining<=0||item.remaining>1.2f)return false;
                previous=item.id;
            }
            return true;
        }
    }
}
