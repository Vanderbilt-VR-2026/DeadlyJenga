using System.Collections.Generic;
using DeadlyJenga.Core;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime:IDemoLoadoutControls
    {
        EquipmentKind respawnEquipment;
        float nextLoadoutCheck;
        readonly Dictionary<string,bool> loadoutAlive=new Dictionary<string,bool>();
        readonly HashSet<string> loadoutSeen=new HashSet<string>();
        readonly List<string> loadoutStale=new List<string>();
        public EquipmentKind RespawnEquipment=>Authority?respawnEquipment:frame.demoRespawnEquipment;
        bool LoadoutRecipient(string id)
        {
            var p=simulation.Find(id);var actor=FindActor(id);
            return p!=null&&p.present&&p.frame.alive&&!p.frame.dying&&p.frame.role!="hand"&&p.frame.role!="spectator"&&actor!=null&&actor.Alive&&actor.gameObject.GetComponent<IEnemyActor>()?.IsEnemy!=true;
        }
        public bool GiveEveryoneEquipment(EquipmentKind kind,bool everyRespawn)
        {
            if(!Authority||kind<EquipmentKind.None||kind>EquipmentKind.Dagger)return false;
            if(kind==EquipmentKind.None)
            {
                if(everyRespawn)return false;respawnEquipment=EquipmentKind.None;loadoutAlive.Clear();PublishEquipment();return true;
            }
            if(simulation.Phase!=RoundPhase.Playing)return false;
            respawnEquipment=everyRespawn?kind:EquipmentKind.None;loadoutAlive.Clear();bool given=false;
            foreach(var p in simulation.Participants)
            {
                bool alive=LoadoutRecipient(p.frame.id);loadoutAlive[p.frame.id]=alive;
                if(alive)given|=GiveEquipmentTo(p.frame.id,kind,false,false);
            }
            PublishEquipment();return given||everyRespawn;
        }
        void TickDemoLoadout()
        {
            if(!Authority||respawnEquipment==EquipmentKind.None||simulation.Phase!=RoundPhase.Playing)return;
            if(UnityEngine.Time.unscaledTime<nextLoadoutCheck)return;nextLoadoutCheck=UnityEngine.Time.unscaledTime+.1f;
            loadoutSeen.Clear();
            foreach(var p in simulation.Participants)
            {
                string id=p.frame.id;loadoutSeen.Add(id);bool alive=LoadoutRecipient(id);
                bool previous=loadoutAlive.TryGetValue(id,out bool value)&&value;
                if(alive&&!previous)GiveEquipmentTo(id,respawnEquipment,false,false);
                loadoutAlive[id]=alive;
            }
            loadoutStale.Clear();foreach(var pair in loadoutAlive)if(!loadoutSeen.Contains(pair.Key))loadoutStale.Add(pair.Key);
            foreach(string id in loadoutStale)loadoutAlive.Remove(id);
        }
    }
}
