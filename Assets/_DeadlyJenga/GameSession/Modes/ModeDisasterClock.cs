using System;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed class ModeDisasterClock
    {
        readonly System.Random random;
        float next,readyAt;
        bool planned;
        public string Pending {get;private set;}="";
        public float WarningRemaining(float now)=>planned?Mathf.Max(0,readyAt-now):0;
        public float NextIn(float now)=>planned?0:Mathf.Max(0,next-now);
        public ModeDisasterClock(int seed=1189){random=new System.Random(seed);}
        public void Reset(ModeRules rules,float now){Pending="";planned=false;next=now+Interval(rules,0);}
        public void Restore(float now,string warning,float warningRemaining,float nextIn)
        {Pending=warning??"";planned=Pending.Length>0;readyAt=now+Mathf.Clamp(warningRemaining,0,30);next=now+Mathf.Max(0,nextIn);}
        float Interval(ModeRules rules,float elapsed)
        {
            float min=Mathf.Max(10,rules.disasterMinInterval),max=Mathf.Max(min,rules.disasterMaxInterval);
            float progress=rules.roundSeconds>0?Mathf.Clamp01(elapsed/rules.roundSeconds):Mathf.Clamp01(elapsed/600);
            float late=Mathf.Clamp(rules.disasterLateInterval,10,max);
            return Mathf.Lerp(min+(float)random.NextDouble()*(max-min),late,progress);
        }
        // choose returns a currently valid, inactive catalog ID. launch returns whether Begin succeeded.
        public bool Tick(ModeRules rules,float elapsed,int active,Func<string> choose,Func<string,bool> launch)
        {
            if(!rules.automaticDisasters||rules.mode==GameModeKind.Demo)return false;
            if(planned)
            {
                if(elapsed<readyAt)return false;
                if(active>=Mathf.Clamp(rules.maxConcurrentDisasters,1,4)){readyAt=elapsed+2;return false;}
                string id=Pending;planned=false;Pending="";
                bool started=launch(id);next=elapsed+(started?Interval(rules,elapsed):10);return started;
            }
            if(elapsed<next||active>=Mathf.Clamp(rules.maxConcurrentDisasters,1,4))return false;
            Pending=choose()??"";
            if(Pending.Length==0){next=elapsed+10;return false;}
            readyAt=elapsed+Mathf.Clamp(rules.disasterWarningSeconds,2,30);planned=true;return false;
        }
    }
}
