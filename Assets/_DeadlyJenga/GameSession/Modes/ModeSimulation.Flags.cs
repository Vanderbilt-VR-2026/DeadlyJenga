using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class ModeSimulation
    {
        readonly List<FlagFrame> flags=new List<FlagFrame>();
        public IEnumerable<FlagFrame> Flags => flags;
        public void SetFlagHomes(Vector3[] homes)
        {
            flags.Clear();
            if(Rules.mode!=GameModeKind.CaptureFlag||homes==null)return;
            for(int t=0;t<Mathf.Min(teams.Count,homes.Length);t++)flags.Add(new FlagFrame{team=t,home=homes[t],position=homes[t],atHome=true});
        }
        public bool CarriesFlag(string id) {foreach(var f in flags)if(f.carrier==id)return true;return false;}
        public void PlaceDroppedFlag(int team,Vector3 position)
        {
            if(team<0||team>=flags.Count)return;
            var f=flags[team];if(f.carrier==null && !f.atHome)f.position=position;
        }
        void DropFlags(string id)
        {
            var p=Find(id);
            foreach(var f in flags)if(f.carrier==id){f.position=p?.frame.position??f.position;f.carrier=null;f.atHome=false;}
        }
        void TickFlags()
        {
            foreach(var flag in flags)
            {
                if(flag.carrier!=null)
                {
                    var p=Find(flag.carrier);
                    if(p==null||!p.present||!p.frame.alive||p.frame.dying||p.frame.role=="hand"||p.frame.role=="spectator"){DropFlags(flag.carrier);continue;}
                    flag.position=p.frame.position+Vector3.up*1.2f;
                    if(p.frame.team<0||p.frame.team>=flags.Count)continue;
                    var home=flags[p.frame.team];
                    if(Vector3.Distance(p.frame.position,home.home)>2.5f)continue;
                    if(flag.team==p.frame.team){Return(flag);continue;}
                    if(!home.atHome)continue;
                    teams[p.frame.team].captures++;teams[p.frame.team].points++;
                    Return(flag);CheckFlagWin();
                    continue;
                }
                Participant nearest=null;float best=1.4f*1.4f;
                foreach(var p in players.Values)
                {
                    var f=p.frame;
                    if(!p.present||!f.alive||f.dying||f.team<0||f.role=="hand"||f.role=="spectator"||CarriesFlag(f.id)||flag.atHome&&flag.team==f.team)continue;
                    float d=(f.position-flag.position).sqrMagnitude;
                    if(d<best){best=d;nearest=p;}
                }
                if(nearest!=null){flag.carrier=nearest.frame.id;flag.atHome=false;}
            }
        }
        static void Return(FlagFrame f){f.carrier=null;f.atHome=true;f.position=f.home;}
        void CheckFlagWin()
        {
            foreach(var team in teams)
            {
                if(team.captures<Rules.flagTarget)continue;
                int second=0;foreach(var other in teams)if(other!=team)second=Mathf.Max(second,other.captures);
                if(team.captures-second>=(Rules.flagWinByTwo?2:1)){Finish(team.name,team.id);return;}
            }
        }
        public bool Shielded(string id,Vector3 attackDirection)
        {
            var p=Find(id);if(p==null||!CarriesFlag(id))return false;
            var forward=p.frame.viewRotation*Vector3.forward;
            return Vector3.Dot(Vector3.ProjectOnPlane(-attackDirection,Vector3.up).normalized,Vector3.ProjectOnPlane(forward,Vector3.up).normalized)>=.5f;
        }
    }
}
