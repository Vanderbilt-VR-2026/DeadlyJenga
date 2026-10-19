using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        readonly RemoteHealthLedger remoteHealth=new RemoteHealthLedger();
        sealed class FallSample{public bool airborne;public float peak,last;}
        readonly Dictionary<string,FallSample> remoteFalls=new Dictionary<string,FallSample>();
        float RemoteHealth(string id)=>remoteHealth.Read(id);
        void TickRemoteRecovery(float seconds)
        {
            if(!Authority||simulation.Phase!=RoundPhase.Playing||!Rules.healthEnabled)return;
            foreach(var pair in actors)
            {
                var actor=pair.Value;var p=simulation.Find(pair.Key);
                if(actor==null||actor.IsLocal||actor.IsBot||p==null||!p.frame.alive||p.frame.dying||RulesFor(actor.gameObject).downed)continue;
                remoteHealth.Recover(pair.Key,seconds);
            }
        }
        void ResetRemoteHealth(string id)
        {remoteHealth.Reset(id);remoteFalls.Remove(id);var p=simulation.Find(id);if(p!=null)p.frame.health=1;}
        public void RecordRemoteDamage(string id,float damage,string sourceId=null)
        {
            if(!Authority||simulation.Phase!=RoundPhase.Playing||float.IsNaN(damage)||float.IsInfinity(damage)||damage<=0)return;
            var p=simulation.Find(id);if(p==null||!p.present||!p.frame.alive||p.frame.dying||transitionUntil.ContainsKey(id))return;
            bool lethal=remoteHealth.Damage(id,damage,Rules.deathEnabled);
            p.frame.health=remoteHealth.Read(id);
            if(lethal)simulation.Kill(id,sourceId);
        }
        void ObserveRemoteFall(string id,IModeActor actor)
        {
            var p=simulation.Find(id);if(p==null||p.frame.role=="hand"||p.frame.role=="spectator"||p.frame.dying||!Rules.fallDamage)return;
            float y=actor.transform.position.y;
            if(!remoteFalls.TryGetValue(id,out var fall))remoteFalls[id]=fall=new FallSample{peak=y,last=Time.time};
            if(Time.time-fall.last>1){fall.peak=y;fall.airborne=false;}fall.last=Time.time;
            if(!actor.Grounded){fall.airborne=true;fall.peak=Mathf.Max(fall.peak,y);return;}
            if(fall.airborne)
            {
                float distance=fall.peak-y;
                if(distance>Rules.fallDamageStart)
                {
                    float damage=(distance-Rules.fallDamageStart)*Rules.fallDamagePerMeter;
                    RecordRemoteDamage(id,damage);RemoteImpact?.Invoke(id,Vector3.zero,damage);
                }
            }
            fall.airborne=false;fall.peak=y;
        }
    }
}
