using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        enum BotTeamRole { Attack, Defend, Recover, Escort }
        readonly Dictionary<string,BotTeamRole> teamRoles=new Dictionary<string,BotTeamRole>();
        float nextTeamRoles;
        BotTeamRole TeamRoleFor(MatchPlayerFrame self)
        {
            if(Time.time>=nextTeamRoles)
            {
                nextTeamRoles=Time.time+.75f;teamRoles.Clear();
                foreach(var home in simulation.Flags)
                {
                    var available=new List<MatchPlayerFrame>();
                    foreach(var p in simulation.Participants)
                        if(p.present&&p.frame.bot&&p.frame.alive&&!p.frame.dying&&p.frame.team==home.team&&p.frame.role!="hand"&&p.frame.role!="spectator"&&!simulation.CarriesFlag(p.frame.id))available.Add(p.frame);
                    available.Sort((a,b)=>{int c=(a.position-home.home).sqrMagnitude.CompareTo((b.position-home.home).sqrMagnitude);return c!=0?c:string.CompareOrdinal(a.id,b.id);});
                    int defenders=Mathf.Min(available.Count,Mathf.Max(1,available.Count/5));
                    for(int i=0;i<defenders;i++)teamRoles[available[i].id]=home.atHome?BotTeamRole.Defend:BotTeamRole.Recover;
                    foreach(var flag in simulation.Flags)
                    {
                        if(flag.carrier==null||flag.team==home.team)continue;
                        var carrier=simulation.Find(flag.carrier);if(carrier==null||carrier.frame.team!=home.team)continue;
                        available.Sort((a,b)=>(a.position-carrier.frame.position).sqrMagnitude.CompareTo((b.position-carrier.frame.position).sqrMagnitude));
                        int escorts=0;foreach(var member in available)
                        {if(teamRoles.ContainsKey(member.id))continue;teamRoles[member.id]=BotTeamRole.Escort;if(++escorts>=2)break;}
                    }
                }
            }
            return teamRoles.TryGetValue(self.id,out var role)?role:BotTeamRole.Attack;
        }
    }
}
