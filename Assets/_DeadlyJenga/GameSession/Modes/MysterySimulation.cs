using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public enum MysteryHit { Ordinary, Murder, SheriffKill, InnocentMistake, Downed, Blocked }

    // Host-only rules. Private roles are deliberately absent from Public().
    public sealed class MysterySimulation
    {
        sealed class Person
        {
            public MysteryRole role;
            public bool alive=true, downed, special;
            public int mistakes;
            public float killedAt;
        }
        sealed class Body
        {
            public MysteryBodyFrame frame;
            public string killer;
            public float killedAt,reportedAt;
            public bool badgeTaken;
            public readonly HashSet<string> voters=new HashSet<string>();
        }
        readonly Dictionary<string,Person> people=new Dictionary<string,Person>(StringComparer.Ordinal);
        readonly Dictionary<string,Body> bodies=new Dictionary<string,Body>(StringComparer.Ordinal);
        readonly Dictionary<int,MysteryTrailFrame> trails=new Dictionary<int,MysteryTrailFrame>();
        readonly System.Random random;
        readonly MysterySettings settings;
        string[] hazards=Array.Empty<string>(), offer=Array.Empty<string>();
        string chooser;
        bool veto;
        float nextOffer,offerDeadline,choiceAvailableAt;
        int revision,trailSequence;
        public float Elapsed { get; private set; }
        public string Winner { get; private set; }="";
        public bool SheriffFallen { get; private set; }
        public event Action<string> DisasterChosen;
        public MysterySimulation(MysterySettings settings=null,int seed=1741)
        {this.settings=settings??new MysterySettings();random=new System.Random(seed);}

        public void Start(IEnumerable<string> ids,IEnumerable<string> hazardIds,Dictionary<string,MysteryRole> preferences=null)
        {
            people.Clear();bodies.Clear();trails.Clear();offer=Array.Empty<string>();chooser=null;
            Elapsed=0;Winner="";SheriffFallen=false;trailSequence=0;revision++;
            choiceAvailableAt=0;
            hazards=(hazardIds??Array.Empty<string>()).Where(s=>!string.IsNullOrEmpty(s)).Distinct().ToArray();
            var list=ids.Where(id=>!string.IsNullOrEmpty(id)).Distinct().ToList();
            for(int i=list.Count-1;i>0;i--){int j=random.Next(i+1);var s=list[i];list[i]=list[j];list[j]=s;}
            foreach(var id in list)people[id]=new Person{role=MysteryRole.Innocent};
            if(list.Count>=2)
            {
                string murderer=list.FirstOrDefault(id=>preferences!=null&&preferences.TryGetValue(id,out var r)&&r==MysteryRole.Murderer)??list.FirstOrDefault(id=>preferences==null||!preferences.TryGetValue(id,out var r)||r==MysteryRole.Random)??list[0];
                people[murderer].role=MysteryRole.Murderer;
                string sheriff=list.FirstOrDefault(id=>id!=murderer&&preferences!=null&&preferences.TryGetValue(id,out var r)&&r==MysteryRole.Sheriff)??list.First(id=>id!=murderer);
                people[sheriff].role=MysteryRole.Sheriff;
            }
            nextOffer=Mathf.Max(0,this.settings.firstDisasterSeconds-this.settings.vetoSeconds);
        }
        public MysteryRole Role(string id)=>id!=null&&people.TryGetValue(id,out var p)?p.role:MysteryRole.Random;
        public void RegisterLate(string id){if(!string.IsNullOrEmpty(id)&&!people.ContainsKey(id))people[id]=new Person{role=MysteryRole.Innocent};}
        public void Rekey(string oldId,string newId)
        {
            if(oldId==newId||!people.TryGetValue(oldId,out var p)||people.ContainsKey(newId))return;
            people.Remove(oldId);people[newId]=p;if(chooser==oldId)chooser=newId;
            foreach(var b in bodies.Values)if(b.killer==oldId)b.killer=newId;
            if(bodies.TryGetValue(oldId,out var body)){bodies.Remove(oldId);body.frame.id=newId;bodies[newId]=body;}
        }
        public bool IsDowned(string id)=>id!=null&&people.TryGetValue(id,out var p)&&p.alive&&p.downed;
        public bool Alive(string id)=>id!=null&&people.TryGetValue(id,out var p)&&p.alive;
        public void SetSpecial(string id,bool enabled)
        {if(people.TryGetValue(id,out var p)&&p.alive&&!p.downed)p.special=enabled&&p.role!=MysteryRole.Innocent;}
        public MysteryPrivateFrame Personal(string id)
        {
            if(!people.TryGetValue(id,out var p))return new MysteryPrivateFrame();
            bool available=id==chooser&&(veto||Elapsed>=choiceAvailableAt);
            return new MysteryPrivateFrame{role=p.role,specialAttack=p.special,mistakes=p.mistakes,attackReadyIn=Mathf.Max(0,settings.attackGraceSeconds-Elapsed),veto=available&&veto,choiceRemaining=available?Mathf.Max(0,offerDeadline-Elapsed):0,disasterChoices=available?(string[])offer.Clone():Array.Empty<string>(),reportLocks=bodies.Values.Where(b=>b.killer==id&&Elapsed<b.killedAt+settings.reportDelaySeconds).Select(b=>new MysteryReportLock{body=b.frame.id,remaining=b.killedAt+settings.reportDelaySeconds-Elapsed}).ToArray()};
        }
        public MysteryHit Hit(string attacker,string victim)
        {
            if(attacker==victim||!people.TryGetValue(attacker,out var a)||!people.TryGetValue(victim,out var b)||!a.alive||a.downed||!b.alive||b.downed&&a.role!=MysteryRole.Murderer||Elapsed<settings.attackGraceSeconds||Winner.Length>0)return MysteryHit.Blocked;
            if(a.special&&a.role==MysteryRole.Murderer)return MysteryHit.Murder;
            if(a.special&&a.role==MysteryRole.Sheriff)return MysteryHit.SheriffKill;
            return MysteryHit.Ordinary;
        }
        // Called when a punch would be lethal; ordinary attacks never reveal roles.
        public MysteryHit Lethal(string attacker,string victim,bool special,MysteryBodyFrame body)
        {
            var hit=Hit(attacker,victim);if(hit==MysteryHit.Blocked)return hit;
            var a=people[attacker];var b=people[victim];
            if(!special)hit=MysteryHit.Ordinary;
            if(a.role==MysteryRole.Innocent&&b.role!=MysteryRole.Murderer)
            {a.alive=false;a.special=false;b.downed=true;b.special=false;CheckWinner();return MysteryHit.InnocentMistake;}
            b.alive=false;b.special=false;b.killedAt=Elapsed;
            if(a.role==MysteryRole.Murderer&&body!=null)
            {
                body.id=victim;body.reported=false;body.suspected=Array.Empty<Color>();body.sheriff=b.role==MysteryRole.Sheriff;
                bodies[victim]=new Body{frame=body,killer=attacker,killedAt=Elapsed};
                if(body.sheriff)SheriffFallen=true;
            }
            if(a.role==MysteryRole.Sheriff&&b.role!=MysteryRole.Murderer)
            {a.mistakes++;if(a.mistakes>=Mathf.Max(1,settings.sheriffMistakes)){a.alive=false;a.special=false;}}
            CheckWinner();return hit;
        }
        public void NaturalDeath(string id)
        {if(people.TryGetValue(id,out var p)){p.alive=false;p.downed=false;p.special=false;CheckWinner();}}
        public bool Revive(string helper,string victim)
        {
            if(!people.TryGetValue(helper,out var h)||!h.alive||h.downed||!people.TryGetValue(victim,out var p)||!p.alive||!p.downed||helper==victim)return false;
            p.downed=false;return true;
        }
        public bool RecoverSheriff(string id,string bodyId)
        {
            if(!people.TryGetValue(id,out var p)||!p.alive||p.downed||p.role!=MysteryRole.Innocent||people.Values.Any(v=>v.alive&&v.role==MysteryRole.Sheriff)||!bodies.TryGetValue(bodyId,out var body)||!body.frame.sheriff||body.frame.lost||body.badgeTaken)return false;
            p.role=MysteryRole.Sheriff;body.badgeTaken=true;return true;
        }
        public bool CanRecoverDroppedSheriff(string id)=>people.TryGetValue(id,out var p)&&p.alive&&!p.downed&&p.role==MysteryRole.Innocent&&!people.Values.Any(v=>v.alive&&v.role==MysteryRole.Sheriff);
        public bool RecoverDroppedSheriff(string id)
        {
            if(!CanRecoverDroppedSheriff(id))return false;
            people[id].role=MysteryRole.Sheriff;
            foreach(var body in bodies.Values)if(body.frame.sheriff)body.badgeTaken=true;
            return true;
        }
        public bool Report(string id,string bodyId,Color finderColor)
        {
            if(!people.TryGetValue(id,out var p)||!p.alive||p.downed||!bodies.TryGetValue(bodyId,out var body)||body.frame.lost||body.frame.reported)return false;
            if(body.killer==id&&Elapsed<body.killedAt+settings.reportDelaySeconds)return false;
            body.frame.reported=true;body.frame.finder=id;body.frame.finderColor=finderColor;body.reportedAt=Elapsed;return true;
        }
        public float ReportCooldown(string id,string bodyId)=>bodies.TryGetValue(bodyId,out var b)&&b.killer==id?Mathf.Max(0,b.killedAt+settings.reportDelaySeconds-Elapsed):0;
        public bool Suspect(string id,string bodyId,int trailSequence)
        {
            if(!people.TryGetValue(id,out var p)||!p.alive||p.downed||!bodies.TryGetValue(bodyId,out var b)||!b.frame.reported||b.frame.lost||Elapsed-b.reportedAt>settings.suspicionSeconds||b.voters.Contains(id)||!trails.TryGetValue(trailSequence,out var trail))return false;
            // The runtime supplies world distance after resolving each moving block.
            if(b.frame.suspected.Contains(trail.color)){b.voters.Add(id);return true;}
            if(b.frame.suspected.Length>=3)return false;
            b.voters.Add(id);b.frame.suspected=b.frame.suspected.Concat(new[]{trail.color}).ToArray();return true;
        }
        public int AddTrail(MysteryTrailFrame trail)
        {trail.sequence=++trailSequence;trails[trail.sequence]=trail;return trail.sequence;}
        public MysteryTrailFrame Trail(int sequence)=>trails.TryGetValue(sequence,out var t)?t:null;
        public void UpdateBody(string id,Vector3 position,bool lost)
        {if(bodies.TryGetValue(id,out var b)){b.frame.position=position;b.frame.lost=lost;}}
        public MysteryPublicFrame Public()
        {
            var result=new List<MysteryBodyFrame>();
            foreach(var b in bodies.Values)
            {
                var f=b.frame;
                result.Add(new MysteryBodyFrame{id=f.id,name=f.name,color=f.color,position=f.position,supportBlock=f.supportBlock,supportPoint=f.supportPoint,sheriff=f.sheriff,reported=f.reported,lost=f.lost,finder=f.finder,finderColor=f.finderColor,reportedAge=f.reported?Mathf.Max(0,Elapsed-b.reportedAt):0,suspected=(Color[])f.suspected.Clone()});
            }
            return new MysteryPublicFrame{revision=++revision,sheriffFallen=SheriffFallen,bodies=result.ToArray(),downed=people.Where(p=>p.Value.alive&&p.Value.downed).Select(p=>p.Key).ToArray()};
        }
        public void ForceRole(string id,MysteryRole role)
        {
            if(!people.TryGetValue(id,out var p)||!p.alive||role==MysteryRole.Random)return;
            var previous=p.role;
            foreach(var other in people.Values)if(other!=p&&other.role==role&&role!=MysteryRole.Innocent){other.role=previous;other.special=false;}
            p.role=role;p.special=false;p.mistakes=0;Winner="";
            if(previous!=MysteryRole.Innocent&&!people.Values.Any(v=>v.alive&&v.role==previous))
            {var replacement=people.Values.FirstOrDefault(v=>v!=p&&v.alive&&v.role==MysteryRole.Innocent);if(replacement!=null)replacement.role=previous;}
        }
        public void Tick(float seconds)
        {
            if(!float.IsFinite(seconds)||seconds<=0||Winner.Length>0)return;
            Elapsed+=seconds;CheckWinner();if(Winner.Length>0||hazards.Length==0)return;
            if(offer.Length==0&&Elapsed>=nextOffer)BeginOffer();
            if(offer.Length>0&&(Elapsed>=offerDeadline||chooser==null||!Alive(chooser)))
                Choose(chooser,offer[random.Next(offer.Length)],true);
        }
        void BeginOffer()
        {
            offer=hazards.OrderBy(_=>random.Next()).Take(3).ToArray();veto=true;
            chooser=people.FirstOrDefault(p=>p.Value.alive&&!p.Value.downed&&p.Value.role==MysteryRole.Sheriff).Key;
            offerDeadline=Elapsed+Mathf.Max(.1f,settings.vetoSeconds);
        }
        public bool Choose(string id,string hazard,bool automatic=false)
        {
            if(offer.Length==0||!offer.Contains(hazard)||!veto&&Elapsed<choiceAvailableAt||!automatic&&(id!=chooser||!Alive(id)||IsDowned(id)))return false;
            if(veto)
            {
                if(offer.Length>1)offer=offer.Where(h=>h!=hazard).ToArray();
                veto=false;chooser=people.FirstOrDefault(p=>p.Value.alive&&p.Value.role==MysteryRole.Murderer).Key;choiceAvailableAt=Mathf.Max(Elapsed,nextOffer+settings.vetoSeconds);offerDeadline=choiceAvailableAt+Mathf.Max(.1f,settings.chooseSeconds);return true;
            }
            offer=Array.Empty<string>();chooser=null;nextOffer=Elapsed+Mathf.Max(1,(float)(Mathf.Max(1,settings.disasterMinSeconds)+random.NextDouble()*Mathf.Max(0,settings.disasterMaxSeconds-settings.disasterMinSeconds))-settings.vetoSeconds);DisasterChosen?.Invoke(hazard);return true;
        }
        void CheckWinner()
        {
            if(people.Count<2)return;
            if(!people.Values.Any(p=>p.alive&&p.role==MysteryRole.Murderer)){Winner="Innocents";return;}
            if(!people.Values.Any(p=>p.alive&&!p.downed&&p.role!=MysteryRole.Murderer))Winner="Murderer";
        }
    }
}
