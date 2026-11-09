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
        const float SurvivalFirstDisaster=20;
        public void Reset(ModeRules rules,float now)
        {
            Pending="";planned=false;
            next=now+(rules.mode==GameModeKind.Survival?SurvivalFirstDisaster-WarningLead(rules):Interval(rules,0));
        }

        static float WarningLead(ModeRules rules)
        {
            if(rules.mode!=GameModeKind.Survival)return Mathf.Clamp(rules.disasterWarningSeconds,2,30);
            float shortest=Mathf.Max(10,Mathf.Min(rules.disasterMinInterval,rules.disasterLateInterval));
            return Mathf.Clamp(rules.disasterWarningSeconds,2,Mathf.Min(SurvivalFirstDisaster,shortest));
        }
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
            bool survival=rules.mode==GameModeKind.Survival;
            int cap=Mathf.Clamp(rules.maxConcurrentDisasters,1,4);
            if(planned)
            {
                if(elapsed<readyAt)return false;
                if(active>=cap){if(!survival)readyAt=elapsed+2;return false;}
                string id=Pending;planned=false;Pending="";
                bool started=launch(id);
                next=elapsed+(started?Interval(rules,elapsed)-(survival?WarningLead(rules):0):10);
                return started;
            }
            if(elapsed<next||(!survival&&active>=cap))return false;
            Pending=choose()??"";
            if(Pending.Length==0){next=elapsed+10;return false;}
            // Survival counts from activation to activation. It can warn while the previous pair is still active.
            readyAt=(survival?next:elapsed)+WarningLead(rules);planned=true;return false;
        }
    }
}
