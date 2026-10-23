using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class ModeSimulation
    {
        sealed class Claim
        {
            public readonly BlockClaimFrame frame=new BlockClaimFrame();
            public float emptySeconds;
            public readonly int[] occupants=new int[8];
        }
        readonly Dictionary<int,Claim> claims=new Dictionary<int,Claim>();
        public int OwnerOf(int block)=>claims.TryGetValue(block,out var c)?c.frame.owner:-1;
        public float ProgressOf(int block)=>claims.TryGetValue(block,out var c)?c.frame.progress:0;
        public float EmptySecondsOf(int block)=>claims.TryGetValue(block,out var c)?c.emptySeconds:0;
        void TickClaims(float seconds)
        {
            foreach(var c in claims.Values)System.Array.Clear(c.occupants,0,c.occupants.Length);
            foreach(var p in players.Values)
            {
                var f=p.frame;f.claimProgress=0;f.blockOwner=-1;
                if(!p.present||!f.alive||f.dying||f.role=="hand"||f.role=="spectator"||!p.grounded||f.standingBlock<0||f.team<0||f.team>=teams.Count)continue;
                if(!claims.TryGetValue(f.standingBlock,out var c))
                {c=new Claim();c.frame.block=f.standingBlock;claims.Add(f.standingBlock,c);}
                c.occupants[f.team]++;
            }
            foreach(var c in claims.Values)
            {
                int total=0,winner=-1,strongest=0;
                for(int t=0;t<teams.Count;t++){int n=c.occupants[t];total+=n;if(n>strongest){winner=t;strongest=n;}}
                if(total==0)
                {
                    c.emptySeconds+=seconds;
                    if(Rules.claimDecaySeconds>0)
                    {
                        c.frame.progress=Mathf.Max(0,c.frame.progress-seconds/Rules.claimDecaySeconds);
                        if(c.emptySeconds>=Rules.claimDecaySeconds && c.frame.owner>=0)SetOwner(c,-1);
                    }
                    continue;
                }
                c.emptySeconds=0;
                int advantage=strongest-(total-strongest);
                if(advantage<=0)continue;
                if(c.frame.owner==winner){c.frame.claimant=winner;c.frame.progress=1;continue;}
                if(c.frame.claimant!=winner){c.frame.claimant=winner;c.frame.progress=0;}
                float duration=c.frame.owner<0?Rules.claimSeconds:Rules.reclaimSeconds;
                c.frame.progress=Mathf.Min(1,c.frame.progress+seconds*advantage/duration);
                if(c.frame.progress>=1)SetOwner(c,winner);
            }
            foreach(var t in teams)t.ownedBlocks=0;
            foreach(var c in claims.Values)if(c.frame.owner>=0)teams[c.frame.owner].ownedBlocks++;
            foreach(var t in teams)t.points+=t.ownedBlocks*seconds*Mathf.Max(0,Rules.pointsPerBlockSecond);
            foreach(var p in players.Values)
                if(claims.TryGetValue(p.frame.standingBlock,out var c)){p.frame.blockOwner=c.frame.owner;p.frame.claimProgress=c.frame.progress;}
        }
        void SetOwner(Claim c,int owner)
        {
            if(owner>=0&&owner!=c.frame.owner)
                foreach(var participant in players.Values)
                    if(participant.present&&participant.frame.alive&&!participant.frame.dying&&participant.grounded&&participant.frame.team==owner&&participant.frame.standingBlock==c.frame.block)participant.frame.blocksClaimed++;
            c.frame.owner=owner;c.frame.claimant=owner;c.frame.progress=owner<0?0:1;
            OwnershipChanged?.Invoke(c.frame.block,owner);
        }
        public bool MayClimb(string actorId,int block)
        {
            var p=Find(actorId);int owner=OwnerOf(block);
            return !Rules.enemyClimbBlocked || Rules.mode!=GameModeKind.BlockControl || owner<0 || p==null || owner==p.frame.team;
        }
    }
}
