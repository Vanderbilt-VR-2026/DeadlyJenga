using System;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        public event Action<string, MatchSoundCue, Vector3> RemoteMatchSound;
        string impactVictim;
        Vector3 impactPoint;

        void ApplyCombatImpact(IModeActor source, IModeActor target, float damage, Vector3 push, Vector3 point, bool shot)
        {
            string victim=target.ActorId;
            var participant=simulation.Find(victim);
            int deaths=participant?.frame.deaths??0;
            bool damaged=false;
            string previousVictim=impactVictim;
            Vector3 previousPoint=impactPoint;
            impactVictim=victim;
            impactPoint=point;
            try
            {
                if(target.IsLocal||target.IsBot)
                {
                    target.gameObject.GetComponent<IPushable>()?.Push(push,source.gameObject);
                    var health=target.gameObject.GetComponent<IDamageable>();
                    if(health!=null&&damage>0)
                    {
                        float before=health.Health;
                        health.Damage(damage,source.gameObject);
                        damaged=health.Health<before;
                    }
                }
                else
                {
                    if(damage>0)damaged=ApplyRemoteDamage(victim,damage,source.ActorId);
                    RemoteImpact?.Invoke(victim,push,damage);
                }
            }
            finally
            {
                impactVictim=previousVictim;
                impactPoint=previousPoint;
            }
            if(!damaged||participant==null||participant.frame.deaths!=deaths||
                target.gameObject.GetComponent<IEnemyActor>()?.IsEnemy==true) return;
            RouteMatchSound(victim,source.ActorId,point,MatchSoundCue.HitConfirm,
                shot?MatchSoundCue.PersonalShot:MatchSoundCue.PersonalHit,
                shot?MatchSoundCue.NearbyShot:MatchSoundCue.NearbyHit);
        }

        void OnOtherDamage(GameObject who,float amount)
        {
            if(!Authority||simulation?.Phase!=RoundPhase.Playing||!who||amount<=0||
                !actorIds.TryGetValue(who,out string id)||id==impactVictim||
                who.GetComponent<IEnemyActor>()?.IsEnemy==true||
                who.GetComponent<IDamageable>()?.Health<=0)return;
            var actor=FindActor(id);
            if(actor==null||!actor.IsLocal&&!actor.IsBot)return;
            var participant=simulation.Find(id);
            if(participant==null||!participant.frame.alive||participant.frame.dying)return;
            RouteOtherDamage(id,who.transform.position+Vector3.up*.6f);
        }

        void RouteOtherDamage(string victim,Vector3 point)
        {
            RouteMatchSound(victim,null,point,MatchSoundCue.HitConfirm,MatchSoundCue.PersonalHit,MatchSoundCue.NearbyHit);
        }

        void OnCombatDeath(string victim,string attacker,MatchSoundCue? cause)
        {
            var actor=FindActor(victim);
            if(actor==null)return;
            if(FindActor(attacker)?.gameObject.GetComponent<IEnemyActor>()?.IsEnemy==true)attacker=null;
            Vector3 point=impactVictim==victim?impactPoint:actor.transform.position+Vector3.up*.6f;
            RouteMatchSound(victim,attacker,point,MatchSoundCue.KillConfirm,
                cause??(attacker!=null?MatchSoundCue.PlayerDeath:MatchSoundCue.NearbyDeath),MatchSoundCue.NearbyDeath);
        }

        void RouteMatchSound(string victim,string attacker,Vector3 point,MatchSoundCue confirm,MatchSoundCue personal,MatchSoundCue nearby)
        {
            if(!Authority||!Finite(point))return;
            if(attacker==victim)attacker=null;
            foreach(string listener in connectedHumans)
            {
                MatchSoundCue cue;
                if(listener==attacker)cue=confirm;
                else if(listener==victim)cue=personal;
                else
                {
                    var actor=FindActor(listener);
                    if(actor==null||(actor.ViewPose.position-point).sqrMagnitude>45*45)continue;
                    cue=nearby;
                }
                if(listener==LocalActorId)GameEvents.RaiseMatchSound(cue,point);
                else RemoteMatchSound?.Invoke(listener,cue,point);
            }
        }
    }
}
