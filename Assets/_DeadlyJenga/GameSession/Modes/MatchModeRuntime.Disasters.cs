using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        readonly ModeDisasterClock disasters=new ModeDisasterClock();
        readonly System.Random disasterRandom=new System.Random();
        const float SurvivalDisasterDuration=60;
        readonly List<(IHazard hazard,float end)> timedDisasters=new List<(IHazard,float)>();
        readonly List<GameObject> disasterChoices=new List<GameObject>();
        readonly List<GameObject> freshDisasters=new List<GameObject>();
        readonly HashSet<string> playedDisasters=new HashSet<string>();
        readonly HashSet<HazardType> runningDisasters=new HashSet<HazardType>();
        string lastDisaster="";

        bool PlayDisasters=>RoomGameExperience.ForManagedMatch(session)?.PlayerPlaylist==true;
        static bool StructuralDisaster(string key)=>key.Contains("earthquake")||key.Contains("tornado")||key.Contains("tsunami");

        void ResetDisasterRotation()
        {
            playedDisasters.Clear();
            lastDisaster="";
        }
        string ChooseDisaster()
        {
            disasterChoices.Clear();freshDisasters.Clear();runningDisasters.Clear();
            foreach(var component in session.GetComponentsInChildren<MonoBehaviour>())
                if(component is IHazard active&&active.Active)runningDisasters.Add(active.Type);
            bool play=PlayDisasters;
            bool opening=play&&simulation.Elapsed<60;
            bool freshActive=false;
            foreach(var prefab in DemoDisasters.Prefabs)
            {
                if(!prefab||!(prefab.GetComponent<IHazard>() is IMapHazard definition)||
                    !definition.TryValidate(Services.World,out _))continue;
                string key=prefab.name.ToLowerInvariant();
                if(opening&&!StructuralDisaster(key))continue;
                if(play&&ModeSimulation.TeamMode(Rules.mode)&&simulation.Elapsed<180&&
                    (key.Contains("fire")||key.Contains("meteor")||key.Contains("acid")||key.Contains("antigravity")))continue;

                bool fresh=!playedDisasters.Contains(prefab.name);
                if(runningDisasters.Contains(definition.Type)){freshActive|=fresh;continue;}
                disasterChoices.Add(prefab);
                if(fresh)freshDisasters.Add(prefab);
            }
            if(freshDisasters.Count>0)return freshDisasters[disasterRandom.Next(freshDisasters.Count)].name;
            if(freshActive||disasterChoices.Count==0)return null;
            if(!opening)playedDisasters.Clear();
            if(disasterChoices.Count>1)disasterChoices.RemoveAll(prefab=>prefab.name==lastDisaster);
            return disasterChoices[disasterRandom.Next(disasterChoices.Count)].name;
        }
        bool LaunchScheduled(string id)
        {
            GameObject prefab=null;foreach(var candidate in DemoDisasters.Prefabs)if(candidate&&candidate.name==id){prefab=candidate;break;}
            if(!prefab||!(prefab.GetComponent<IHazard>() is IMapHazard definition))return false;
            foreach(var component in session.GetComponentsInChildren<MonoBehaviour>())
                if(component is IHazard active&&active.Active&&active.Type==definition.Type)return false;
            bool play=PlayDisasters;
            bool structural=StructuralDisaster(prefab.name.ToLowerInvariant());
            var parameters=DemoDisasters.Parameters(prefab);var values=new float[parameters.Count];
            for(int i=0;i<values.Length;i++)
            {
                values[i]=parameters[i].initial;
                if(parameters[i].id=="warning")values[i]=parameters[i].min;
                if(Rules.mode==GameModeKind.Survival&&parameters[i].id=="duration")values[i]=parameters[i].Clamp(SurvivalDisasterDuration);
                if(ModeSimulation.TeamMode(Rules.mode))
                {
                    string parameter=parameters[i].id.ToLowerInvariant();
                    float scale=play&&structural&&!parameter.Contains("damage")?.85f:.55f;
                    if(parameter.Contains("force")||parameter.Contains("pull")||parameter.Contains("damage"))values[i]=parameters[i].Clamp(values[i]*scale);
                    if(parameter=="duration")values[i]=parameters[i].Clamp(Mathf.Min(values[i],18));
                }
            }
            bool started=DemoDisasters.ExecuteForMode(session,id,values,out var hazard);
            if(started&&hazard!=null)
            {
                if(Rules.mode==GameModeKind.Survival||prefab.GetComponent<IDemoHazard>()?.Toggle==true)
                    timedDisasters.Add((hazard,simulation.Elapsed+SurvivalDisasterDuration));
                playedDisasters.Add(id);lastDisaster=id;
            }
            return started;
        }
        int ActiveDisasters()
        {int count=0;foreach(var component in session.GetComponentsInChildren<MonoBehaviour>())if(component is IHazard hazard&&hazard.Active)count++;return count;}
        void TickDisasters()
        {
            if(!Authority||simulation.Phase!=RoundPhase.Playing)return;
            // Retire the oldest hazard before checking the overlap cap for the next activation.
            for(int i=timedDisasters.Count-1;i>=0;i--)
            {
                var scheduled=timedDisasters[i];
                if(!(scheduled.hazard is Component owner)||!owner||!scheduled.hazard.Active)
                    timedDisasters.RemoveAt(i);
                else if(simulation.Elapsed>=scheduled.end)
                {
                    scheduled.hazard.End();
                    timedDisasters.RemoveAt(i);
                }
            }
            if(!Rules.automaticDisasters)return;
            DemoDisasters.WarmCatalog();
            float elapsed=simulation.Elapsed;
            if(disasters.NextIn(elapsed)>0||disasters.WarningRemaining(elapsed)>0)return;

            string before=disasters.Pending;
            disasters.Tick(Rules,elapsed,ActiveDisasters(),ChooseDisaster,LaunchScheduled);
            if(before!=disasters.Pending&&disasters.Pending.Length>0)GameEvents.RaiseEmoted(gameObject,"DisasterWarning");
        }
        void WriteDisasterFrame()
        {
            if(simulation.Phase!=RoundPhase.Playing){frame.disasterWarning="";frame.disasterWarningRemaining=frame.disasterNextIn=0;return;}
            frame.disasterWarning=disasters.Pending;
            frame.disasterWarningRemaining=disasters.WarningRemaining(simulation.Elapsed);
            frame.disasterNextIn=disasters.NextIn(simulation.Elapsed);
        }
    }
}
