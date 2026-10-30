using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        struct Sighting { public Vector3 position; public float seen, recognizedAt; }
        readonly Dictionary<string,Sighting> botSightings=new Dictionary<string,Sighting>();
        void ResetBotPerception(){botSightings.Clear();nextTeamRoles=0;teamRoles.Clear();}
        bool ObserveOpponent(MatchPlayerFrame observer,MatchPlayerFrame target,out Vector3 position)
        {
            // A new round must not retain information from the previous one.
            string key=observer.id+"/"+target.id;
            var delta=target.position-observer.position;
            bool visible=delta.sqrMagnitude<16 || Vector3.Dot(observer.viewRotation*Vector3.forward,delta.normalized)>-.15f;
            visible=visible&&VisibleTo(observer.position,target.position);
            bool known=botSightings.TryGetValue(key,out var sighting);
            if(visible)
            {
                if(!known||Time.time-sighting.seen>6)sighting.recognizedAt=Time.time+.18f;
                sighting.position=target.position;sighting.seen=Time.time;
                if(botSightings.Count>20000)botSightings.Clear();
                botSightings[key]=sighting;
            }
            position=sighting.position;
            return (visible||known&&Time.time-sighting.seen<6)&&Time.time>=sighting.recognizedAt;
        }
    }
}
