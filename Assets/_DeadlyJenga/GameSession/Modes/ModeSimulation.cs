using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public enum EliminationOutcome { None, Respawn, FlyingHand, Infected, Spectator, Revive }

    // The host supplies observations. This model owns scores, teams, flags and round decisions.
    public sealed partial class ModeSimulation
    {
        public sealed class Participant
        {
            public readonly MatchPlayerFrame frame = new MatchPlayerFrame();
            public bool present = true, grounded;
            public float heightIntegral, observedSeconds, nextPunch, powerUntil;
            public string previousRole, noun;
        }
        readonly Dictionary<string, Participant> players = new Dictionary<string, Participant>(StringComparer.Ordinal);
        readonly List<MatchTeamFrame> teams = new List<MatchTeamFrame>();
        readonly ModeNames names;
        readonly System.Random random;
        int sequence;
        bool restoring;
        float phaseTime;
        public ModeRules Rules { get; private set; }
        public RoundPhase Phase { get; private set; }
        public float Elapsed { get; private set; }
        public string Winner { get; private set; } = "";
        public int WinnerTeam { get; private set; } = -1;
        public Vector3 Hill { get; set; }
        public IEnumerable<Participant> Participants => players.Values;
        public event Action<string, EliminationOutcome> Eliminated;
        public event Action<RoundPhase> PhaseChanged;
        public event Action<int, int> OwnershipChanged;
        public event Action<string> PowerFinished;

        public ModeSimulation(ModeRules rules, int seed = 7281) { names = new ModeNames(seed); random=new System.Random(seed^471); Configure(rules); }
        public static bool TeamMode(GameModeKind mode) => mode == GameModeKind.BlockControl || mode == GameModeKind.CaptureFlag || mode == GameModeKind.TeamTickets;
        public static ModeRules Defaults(GameModeKind mode)
        {
            var r = new ModeRules { mode = mode };
            if (mode == GameModeKind.KingOfHill) { r.island = IslandRule.Walkable; r.spawn = SpawnRule.Rim; r.punchEnabled = true; r.fallDamage = true; r.healthEnabled = true; r.metric = ScoreMetric.HillTime; }
            if (mode == GameModeKind.BlockControl) { r.spawn = SpawnRule.TeamPoles; r.island = IslandRule.Walkable; r.punchEnabled = true; r.metric = ScoreMetric.TeamPoints; }
            if (mode == GameModeKind.CaptureFlag) { r.spawn = SpawnRule.TeamPoles; r.island = IslandRule.Walkable; r.punchEnabled = true; r.healthEnabled = true; r.punchDistance = 1.8f; r.metric = ScoreMetric.TeamPoints; r.roundSeconds=0; }
            if (mode == GameModeKind.TeamTickets) { r.spawn = SpawnRule.TeamPoles; r.island = IslandRule.Walkable; r.punchEnabled = true; r.healthEnabled = true; r.metric = ScoreMetric.TeamPoints; r.roundSeconds=0; }
            if (mode == GameModeKind.Infection) { r.healthEnabled = true; r.punchEnabled = true; r.roundSeconds=0; }
            if (mode == GameModeKind.MurderMystery) { r.healthEnabled=true;r.punchEnabled=true;r.punchDamage=8;r.punchDistance=.7f;r.roundSeconds=480;r.island=IslandRule.Walkable;r.spawn=SpawnRule.Rim;r.automaticDisasters=false; }
            return r;
        }
        public void Configure(ModeRules input)
        {
            Rules = input?.Copy() ?? new ModeRules();
            Rules.teamCount = Mathf.Clamp(Rules.teamCount,2,8);
            Rules.claimSeconds = Mathf.Clamp(Rules.claimSeconds,.1f,120);
            Rules.reclaimSeconds = Mathf.Clamp(Rules.reclaimSeconds,.1f,120);
            Rules.claimDecaySeconds = Mathf.Max(0,Rules.claimDecaySeconds);
            Rules.hillInterval = Mathf.Max(1,Rules.hillInterval);
            Rules.hillRadius = Mathf.Max(.5f,Rules.hillRadius);
            Rules.flagTarget = Mathf.Clamp(Rules.flagTarget,1,100);
            Rules.tickets = Mathf.Max(0,Rules.tickets);
            Rules.punchCooldown = Mathf.Max(.25f,Rules.punchCooldown);
            Rules.punchDistance = Mathf.Clamp(Rules.punchDistance,0,20);
            Rules.punchDamage = Mathf.Clamp(Rules.punchDamage,0,100);
            Rules.roundSeconds = Rules.roundSeconds<=0?0:Mathf.Max(10,Rules.roundSeconds);
            teams.Clear();
            if (TeamMode(Rules.mode)) for (int t = 0; t < Rules.teamCount; t++)
                teams.Add(new MatchTeamFrame { id=t,name=ModeNames.ColorNames[t],color=ModeNames.Colors[t],tickets=Rules.tickets });
            foreach(var p in players.Values){p.frame.team=-1;p.frame.color=SurvivorColor(p);}
            foreach (var p in players.Values) { AssignTeam(p); Rename(p); }
            claims.Clear(); flags.Clear(); Winner=""; WinnerTeam=-1;
            ChangePhase(Rules.mode == GameModeKind.Demo ? RoundPhase.Playing : RoundPhase.Waiting);
        }
        public Participant Find(string id) => id != null && players.TryGetValue(id,out var p) ? p : null;
        public bool Tune(ModeRules input)
        {
            if(input==null)return false;
            bool structural=input.mode!=Rules.mode||input.teamCount!=Rules.teamCount||input.sameHumanTeam!=Rules.sameHumanTeam;
            if(structural){Configure(input);return true;}
            int previousTickets=Rules.tickets;
            Rules=new ModeSimulation(input).Rules;
            if(Rules.tickets!=previousTickets)foreach(var team in teams)team.tickets=Mathf.Max(0,team.tickets+Rules.tickets-previousTickets);
            if(Rules.mode==GameModeKind.CaptureFlag)CheckFlagWin();
            return false;
        }
        public Participant Register(string id, Color color, bool bot, bool local)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 128) return null;
            if (players.TryGetValue(id,out var old)) { old.present=true; old.frame.local=local; return old; }
            if (players.Count >= 256) return null;
            var p=new Participant(); p.frame.id=id; p.frame.survivorColor=color;p.frame.color=SurvivorColor(p);
            p.frame.alive=true; p.frame.bot=bot; p.frame.local=local; AssignTeam(p); p.frame.name=names.Next(p.frame.color);p.noun=ModeNames.NounOf(p.frame.name); players.Add(id,p);
            if(Phase==RoundPhase.Playing&&Elapsed>1&&Rules.mode==GameModeKind.Survival){p.frame.alive=false;p.frame.role="spectator";Eliminated?.Invoke(id,EliminationOutcome.Spectator);}
            if(Phase==RoundPhase.Playing&&Elapsed>1&&Rules.mode==GameModeKind.MurderMystery){p.frame.alive=false;p.frame.role="spectator";Eliminated?.Invoke(id,EliminationOutcome.Spectator);}
            if(Phase==RoundPhase.Playing&&Elapsed>1&&Rules.mode==GameModeKind.Infection)MakeInfected(p);
            return p;
        }
        public void PreserveIdentity(string id,string noun)
        {
            var p=Find(id);if(p==null||string.IsNullOrEmpty(noun))return;
            p.noun=noun;Rename(p);
        }
        void Rename(Participant p)
        {
            if(string.IsNullOrEmpty(p.noun))p.noun=ModeNames.NounOf(p.frame.name??"");
            names.Release(p.frame.name);p.frame.name=names.Preserve(p.frame.color,p.noun);
        }
        void AssignTeam(Participant p)
        {
            p.frame.team=-1;
            if(TeamMode(Rules.mode))
            {
                if(Rules.sameHumanTeam&&!p.frame.bot)p.frame.team=0;
                else
                {
                    int first=Rules.sameHumanTeam?1:0,min=int.MaxValue;var candidates=new List<int>();
                    for(int team=first;team<Rules.teamCount;team++)
                    {
                        int count=0;foreach(var other in players.Values)if(other!=p&&other.present&&other.frame.team==team)count++;
                        if(count<min){min=count;candidates.Clear();}if(count==min)candidates.Add(team);
                    }
                    p.frame.team=candidates[random.Next(candidates.Count)];
                }
            }
            if (p.frame.team>=0) p.frame.color=teams[p.frame.team].color;
        }
        Color SurvivorColor(Participant p)
        {
            var color=p.frame.survivorColor;
            if(Rules.mode==GameModeKind.MurderMystery&&(color.r>color.g*1.5f&&color.r>color.b*1.5f||color.b>color.g*1.5f&&color.b>color.r*1.5f))return ModeNames.Colors[2+Mathf.Abs(p.frame.id.GetHashCode()%Mathf.Max(1,ModeNames.Colors.Length-2))];
            return Rules.mode==GameModeKind.Infection&&color.g>color.r&&color.g>color.b?ModeNames.Colors[1]:color;
        }
        public void Observe(string id, Vector3 feet, Quaternion viewRotation, float height, float health, bool grounded, int standingBlock)
        {
            var p=Find(id); if(p==null)return;
            p.present=true; p.grounded=grounded; p.frame.position=feet; p.frame.viewRotation=viewRotation;
            p.frame.height=Finite(height)?height:0; p.frame.health=Mathf.Clamp01(health);
            p.frame.standingBlock=grounded?standingBlock:-1;
        }
        public void Remove(string id)
        {
            var p=Find(id); if(p==null)return;
            DropFlags(id); p.present=false; p.frame.alive=false; p.frame.role="disconnected";
        }
        public void StartRound()
        {
            Winner=""; WinnerTeam=-1; Elapsed=0; claims.Clear();
            foreach(var team in teams){team.points=0;team.captures=0;team.ownedBlocks=0;team.tickets=Rules.tickets;}
            foreach(var p in players.Values)
            {
                var f=p.frame; f.alive=p.present; f.role="survivor"; f.survivalSeconds=f.averageHeight=f.metric=0;f.kills=0;f.dying=false;f.deathProgress=0;f.sinking=false;f.sinkProgress=0;f.spectating=null;f.pendingOutcome=null;f.deathRemaining=0;f.handPowerRemaining=0;f.handReturnRole=null;
                if(!TeamMode(Rules.mode)){f.team=-1;f.color=SurvivorColor(p);}
                p.heightIntegral=p.observedSeconds=0;p.powerUntil=0;p.nextPunch=0;
            }
            foreach(var flag in flags){flag.carrier=null;flag.position=flag.home;flag.atHome=true;}
            ChangePhase(Rules.mode==GameModeKind.Demo?RoundPhase.Playing:RoundPhase.Countdown);
        }
        void ChangePhase(RoundPhase value) { Phase=value;phaseTime=0;if(!restoring)PhaseChanged?.Invoke(value); }
        public void Tick(float seconds)
        {
            if(!Finite(seconds)||seconds<=0)return;
            seconds=Mathf.Min(seconds,.5f);phaseTime+=seconds;
            if(Phase==RoundPhase.Countdown){if(phaseTime>=Mathf.Max(0,Rules.countdownSeconds)){ChangePhase(RoundPhase.Playing); SeedInfection();}return;}
            if(Phase==RoundPhase.Victory){if(phaseTime>=Mathf.Max(0,Rules.victorySeconds))ChangePhase(RoundPhase.Results);return;}
            if(Phase!=RoundPhase.Playing)return;
            Elapsed+=seconds;
            foreach(var p in players.Values)
            {
                if(!p.present)continue;
                var f=p.frame;
                if(f.alive && !f.dying && f.role!="infected" && f.role!="hand") f.survivalSeconds+=seconds;
                if(f.alive&&!f.dying){p.heightIntegral+=Mathf.Max(0,f.height)*seconds;p.observedSeconds+=seconds;f.averageHeight=p.heightIntegral/Mathf.Max(.001f,p.observedSeconds);}
                if(p.powerUntil>0 && Elapsed>=p.powerUntil){f.role=p.previousRole??"survivor";p.powerUntil=0;PowerFinished?.Invoke(f.id);}
                if(Rules.mode==GameModeKind.KingOfHill && f.alive && !f.dying && f.role!="hand" && f.role!="spectator" && p.grounded && Vector3.Distance(f.position,Hill)<=Rules.hillRadius)f.metric+=seconds;
            }
            if(Rules.mode==GameModeKind.BlockControl)TickClaims(seconds);
            if(Rules.mode==GameModeKind.CaptureFlag)TickFlags();
            TickSpectators();
            if(Rules.mode!=GameModeKind.Demo)CheckWinner();
        }
        void SeedInfection()
        {
            if(Rules.mode!=GameModeKind.Infection||!Rules.starterInfected)return;
            var list=new List<Participant>();foreach(var p in players.Values)if(p.present)list.Add(p);
            if(list.Count>1)MakeInfected(list[(sequence+17)%list.Count]);
        }
        public EliminationOutcome Kill(string id, string killerId)
        {
            var p=Find(id);if(p==null||!p.frame.alive||p.frame.dying||Phase!=RoundPhase.Playing)return EliminationOutcome.None;
            DropFlags(id);var killer=Find(killerId);if(killer!=null&&killer!=p)killer.frame.kills++;
            EliminationOutcome outcome;
            if(Rules.mode==GameModeKind.Demo || Rules.mode==GameModeKind.KingOfHill || Rules.mode==GameModeKind.BlockControl || Rules.mode==GameModeKind.CaptureFlag)outcome=EliminationOutcome.Respawn;
            else if(Rules.mode==GameModeKind.Infection){MakeInfected(p);return EliminationOutcome.Infected;}
            else if(Rules.mode==GameModeKind.TeamTickets && p.frame.team>=0 && teams[p.frame.team].tickets>0){teams[p.frame.team].tickets--;outcome=EliminationOutcome.Respawn;}
            else outcome=EliminationOutcome.Spectator;
            if(outcome!=EliminationOutcome.Respawn){p.frame.alive=false;p.frame.role="spectator";}
            Eliminated?.Invoke(id,outcome);return outcome;
        }
        void MakeInfected(Participant p)
        {
            p.frame.role="infected";p.frame.team=-2;p.frame.color=new Color(.15f,1,.12f);p.frame.alive=true;
            Eliminated?.Invoke(p.frame.id,EliminationOutcome.Infected);
        }
        public void HandPower(string id,float duration)
        {
            var p=Find(id);if(p==null||!p.frame.alive||p.frame.dying||Phase!=RoundPhase.Playing)return;
            DropFlags(id);
            p.previousRole=p.frame.role;p.frame.role="hand";p.powerUntil=Elapsed+Mathf.Clamp(duration,1,120);
            Eliminated?.Invoke(id,EliminationOutcome.FlyingHand);
        }
        void CheckWinner()
        {
            if(Rules.mode==GameModeKind.MurderMystery)return;
            int present=0,alive=0;Participant last=null;
            foreach(var p in players.Values)if(p.present){present++;if(p.frame.alive&&p.frame.role!="infected"){alive++;last=p;}}
            if(present<2)return;
            if(Rules.mode==GameModeKind.Survival && alive<=1){Finish(last?.frame.name??"Nobody",-1);return;}
            if(Rules.mode==GameModeKind.Infection && alive==0){Finish("Infected",-2);return;}
            if(Rules.mode==GameModeKind.TeamTickets)
            {
                var active=new HashSet<int>();foreach(var p in players.Values)if(p.present&&p.frame.alive&&p.frame.team>=0)active.Add(p.frame.team);
                if(active.Count<=1){int t=-1;foreach(int value in active)t=value;Finish(t>=0?teams[t].name:"Nobody",t);return;}
            }
            if(Rules.roundSeconds<=0||Elapsed<Rules.roundSeconds)return;
            if(teams.Count>0){int best=0;bool tie=false;for(int i=1;i<teams.Count;i++){float a=TeamScore(teams[i]),b=TeamScore(teams[best]);if(a>b){best=i;tie=false;}else if(a==b)tie=true;}Finish(tie?"Draw":teams[best].name,tie?-1:best);}
            else{var top=TopTen();Finish(top.Length>0?top[0].name:"Nobody",-1);}
        }
        float TeamScore(MatchTeamFrame t) => Rules.mode==GameModeKind.CaptureFlag?t.captures:Rules.mode==GameModeKind.TeamTickets?t.tickets:t.points;
        void Finish(string winner,int team){Winner=winner;WinnerTeam=team;ChangePhase(RoundPhase.Victory);}
        public void FinishMystery(string winner){if(Rules.mode==GameModeKind.MurderMystery&&Phase==RoundPhase.Playing)Finish(winner,-1);}
        static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
    }
}
