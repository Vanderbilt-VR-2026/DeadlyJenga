using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype.Editor
{
    public static class ModeRoundChecks
    {
        static readonly List<string> results=new List<string>();
        static void Check(bool condition,string label){if(!condition)throw new Exception(label);results.Add("PASS "+label);}
        static void Step(ModeSimulation s,float seconds){int ticks=Mathf.CeilToInt(seconds/.1f);for(int i=0;i<ticks;i++)s.Tick(.1f);}
        static ModeSimulation Populate(GameModeKind mode,int count=10)
        {
            var rules=ModeSimulation.Defaults(mode);rules.countdownSeconds=2;
            var s=new ModeSimulation(rules,131);
            for(int i=0;i<count;i++)s.Register("p"+i,ModeNames.Colors[i%ModeNames.Colors.Length],i>0,i==0);
            return s;
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Full mode rounds")]
        public static void RunBatch()
        {
            results.Clear();
            try{Names();Survival();Capture();Flags();FlagRecovery();Infection();InfectionColors();SearchGeometry();HazardTactics();Tickets();Hill();Migration();Schedules();TuningAndHealth();ObjectiveEligibility();Performance();results.Add("NOT VERIFIED: headset frame time, real Fusion peers, XR punch gestures, headset passthrough.");}
            catch(Exception e){results.Add("FAIL "+e);throw;}
            finally{Directory.CreateDirectory("Logs/Validation");File.WriteAllLines("Logs/Validation/ModeRounds.txt",results);}
        }
        static void Names()
        {
            var names=new ModeNames(1);var seen=new HashSet<string>();bool unique=true;
            for(int i=0;i<300;i++)unique&=seen.Add(names.Next(Color.red));
            Check(unique&&ModeNames.Nouns.Length==50,"50 nouns generate unique names even beyond a single-color pool");
        }
        static void Survival()
        {
            var s=Populate(GameModeKind.Survival);
            for(int round=0;round<3;round++)
            {
                s.StartRound();Step(s,1);Check(s.Phase==RoundPhase.Countdown&&s.Elapsed==0,"Round "+round+" countdown excludes survival time");
                Step(s,1.2f);Check(s.Phase==RoundPhase.Playing,"Round "+round+" starts after countdown");
                for(int i=1;i<10;i++){Step(s,2);s.Kill("p"+i,"p0");}
                Step(s,.2f);Check(s.Phase==RoundPhase.Victory&&s.Snapshot().survivorsLeft==1,"Round "+round+" last survivor wins");
                Step(s,5.2f);Check(s.Phase==RoundPhase.Results,"Round "+round+" reaches results");
            }
            s.StartRound();Step(s,4);var late=s.Register("late",Color.blue,false,false);
            Check(!late.frame.alive&&late.frame.role=="spectator","Late survival joins spectate until a new round");
        }
        static void Capture()
        {
            var s=Populate(GameModeKind.BlockControl,3);s.Find("p0").frame.team=0;s.Find("p1").frame.team=1;s.Find("p2").frame.team=0;
            var r=s.Rules.Copy();r.claimSeconds=1;r.reclaimSeconds=2;r.claimDecaySeconds=2;s.Configure(r);
            s.Find("p0").frame.team=0;s.Find("p1").frame.team=1;s.Find("p2").frame.team=0;s.StartRound();Step(s,2.2f);
            s.Observe("p0",Vector3.zero,Quaternion.identity,3,1,true,5);s.Observe("p2",Vector3.zero,Quaternion.identity,3,1,true,5);Step(s,.6f);
            Check(s.OwnerOf(5)==0,"Two allies capture twice as quickly");
            s.Observe("p0",Vector3.zero,Quaternion.identity,3,1,false,5);s.Observe("p2",Vector3.zero,Quaternion.identity,3,1,false,5);s.Observe("p1",Vector3.zero,Quaternion.identity,3,1,true,5);Step(s,1);
            Check(s.OwnerOf(5)==0,"Enemy recapture respects its separate longer duration");Step(s,1.2f);Check(s.OwnerOf(5)==1,"Enemy can finish recapture");
            s.Observe("p0",Vector3.zero,Quaternion.identity,3,1,true,5);Step(s,1);Check(s.OwnerOf(5)==1,"Equal opposing occupants stall progress");
            s.Observe("p0",Vector3.zero,Quaternion.identity,3,1,false,5);s.Observe("p1",Vector3.zero,Quaternion.identity,3,1,false,5);Step(s,2.2f);Check(s.OwnerOf(5)==-1,"Unoccupied ownership returns to neutral");
            var f=s.Snapshot();Check(f.teams[0].points>0&&f.teams[1].points>0,"Both teams accumulate ownership-seconds");
            var multi=Populate(GameModeKind.BlockControl,4);r=multi.Rules.Copy();r.teamCount=3;r.claimSeconds=1;multi.Configure(r);for(int i=0;i<4;i++)multi.Find("p"+i).frame.team=i%3;multi.StartRound();Step(multi,2.2f);for(int i=0;i<4;i++)multi.Observe("p"+i,Vector3.zero,Quaternion.identity,3,1,true,0);Step(multi,2);
            Check(multi.OwnerOf(0)==-1,"Three-team two-versus-one-versus-one stays contested");
        }
        static void Flags()
        {
            var s=Populate(GameModeKind.CaptureFlag,2);s.Find("p0").frame.team=0;s.Find("p1").frame.team=1;
            s.StartRound();s.SetFlagHomes(new[]{Vector3.zero,new Vector3(10,0,0)});
            s.Observe("p1",new Vector3(10,0,0),Quaternion.identity,1,1,true,-1);Step(s,2.2f);
            for(int i=0;i<3;i++)
            {
                s.Observe("p1",new Vector3(30,0,0),Quaternion.identity,1,1,true,-1);
                s.Observe("p0",new Vector3(10,0,0),Quaternion.identity,1,1,true,-1);Step(s,.2f);
                Check(s.CarriesFlag("p0"),"CTF round-trip "+i+" picks up enemy flag");
                Check(s.Shielded("p0",Vector3.back)&&!s.Shielded("p0",Vector3.forward),"CTF front shield excludes attacks from behind");
                s.Observe("p0",Vector3.zero,Quaternion.identity,1,1,true,-1);Step(s,.2f);
            }
            Check(s.Phase==RoundPhase.Victory&&s.Snapshot().teams[0].captures==3,"Three clear captures end a default CTF round");
            Step(s,6);Check(s.Phase==RoundPhase.Results,"CTF reaches results after victory hold");
            var timeout=s.Rules.Copy();timeout.flagTarget=1;timeout.roundSeconds=10;s.Configure(timeout);s.StartRound();s.SetFlagHomes(new[]{Vector3.zero,new Vector3(10,0,0)});Step(s,15);
            Check(s.Phase==RoundPhase.Victory&&s.Winner=="Draw","Explicit CTF timeout produces a draw on tied captures");
        }
        static void FlagRecovery()
        {
            var roleGoals=new List<BotObjective>{new BotObjective{id="guard-home",kind=BotObjectiveKind.Guard},new BotObjective{id="flag:1",kind=BotObjectiveKind.Travel},new BotObjective{id="carrier:1",kind=BotObjectiveKind.Escort},new BotObjective{id="recover-base:0",kind=BotObjectiveKind.Travel}};
            ModeCtfRecovery.FilterRoleGoals(false,roleGoals);
            Check(roleGoals.Count==3&&roleGoals[0].id=="flag:1"&&roleGoals[1].kind==BotObjectiveKind.Escort&&roleGoals[2].id=="recover-base:0","Offensive CTF roles exclude passive home guarding while retaining flag, escort and recovery choices");
            roleGoals.Add(new BotObjective{id="guard-home",kind=BotObjectiveKind.Guard});ModeCtfRecovery.FilterRoleGoals(true,roleGoals);
            Check(roleGoals.Count==4,"Defensive CTF roles retain home guarding alongside other legitimate choices");
            var flags=new[]{new FlagFrame{team=0,home=Vector3.zero,carrier="thief",position=Vector3.right*70},new FlagFrame{team=1,home=Vector3.right*80},new FlagFrame{team=2,home=Vector3.forward*80}};
            var search=new List<Vector3>();ModeCtfRecovery.PublicSearch(0,true,false,flags,search);
            Check(search.Count==2&&search.Contains(flags[1].home)&&search.Contains(flags[2].home),"Recovery searches public opposing bases when stolen home flag is out of sight, including three teams");
            var prior=search.ToArray();flags[0].position=Vector3.up*400;ModeCtfRecovery.PublicSearch(0,true,false,flags,search);
            Check(search.Count==prior.Length&&search[0]==prior[0]&&search[1]==prior[1],"Invisible thief movement never changes public recovery search destinations");
            ModeCtfRecovery.PublicSearch(0,true,true,flags,search);Check(search.Count==0,"Seeing the thief supersedes speculative base recovery search");
            var s=Populate(GameModeKind.CaptureFlag,2);s.Find("p0").frame.team=0;s.Find("p1").frame.team=1;s.StartRound();s.SetFlagHomes(new[]{Vector3.up*20,new Vector3(10,20,0)});
            s.Observe("p0",new Vector3(10,20,0),Quaternion.identity,20,1,true,-1);s.Observe("p1",Vector3.up*20,Quaternion.identity,20,1,true,-1);Step(s,3.2f);
            Check(s.CarriesFlag("p0")&&s.CarriesFlag("p1"),"Mutual theft scenario establishes both carried flags");
            s.UpdateFlagSupport(0,0);s.UpdateFlagSupport(1,0);var snapshot=s.Snapshot();
            Check(snapshot.flags[0].home.y==0&&snapshot.flags[1].home.y==0&&s.CarriesFlag("p0"),"Home support can descend during theft without resetting carried flag ownership");
            s.Kill("p1",null);s.Find("p1").frame.dying=true;
            var helper=s.Register("helper",Color.red,true,false);helper.frame.team=0;s.Observe("helper",Vector3.up*20,Quaternion.identity,20,1,true,-1);Step(s,.2f);
            Check(s.CarriesFlag("helper"),"A teammate can physically recover the dropped home flag after mutual theft");
            s.Observe("helper",Vector3.zero,Quaternion.identity,0,1,true,-1);Step(s,.2f);s.Observe("p0",Vector3.zero,Quaternion.identity,0,1,true,-1);Step(s,.2f);
            Check(s.Snapshot().teams[0].captures==1,"Returning recovered flag to lowered home enables a capture after tower collapse");
        }
        static void Infection()
        {
            var s=Populate(GameModeKind.Infection,10);var r=s.Rules.Copy();r.starterInfected=true;s.Configure(r);s.StartRound();Step(s,2.2f);
            int count=0;string starter=null;foreach(var p in s.Participants)if(p.frame.role=="infected"){count++;starter=p.frame.id;}
            Check(count==1,"Optional starter infects exactly one player");
            foreach(var p in s.Participants)if(p.frame.role!="infected"){Step(s,1);s.Kill(p.frame.id,starter);}
            Step(s,.2f);Check(s.Phase==RoundPhase.Victory&&s.Winner=="Infected","Full infection round ends when every survivor converts");
            Check(s.Find(starter).frame.kills==9,"Starter receives authoritative infected kills");
        }
        static void InfectionColors()
        {
            var s=new ModeSimulation(ModeSimulation.Defaults(GameModeKind.Demo));
            s.Register("red",ModeNames.Colors[0],false,true);s.Register("green",ModeNames.Colors[7],true,false);
            s.Configure(ModeSimulation.Defaults(GameModeKind.Infection));
            Check(s.Find("green").frame.color==ModeNames.Colors[1]&&s.Find("green").frame.name.StartsWith("Blue"),"Existing green actors receive a non-green identity when entering Infection");
            string originalName=s.Find("red").frame.name;
            s.StartRound();Step(s,3.2f);s.Kill("red","green");
            Check(s.Find("red").frame.role=="infected"&&s.Find("red").frame.color.g>.9f,"Infection conversion uses distinct neon green");
            var migrated=new ModeSimulation(new ModeRules());migrated.Restore(s.Snapshot());migrated.StartRound();
            Check(migrated.Find("red").frame.role=="survivor"&&migrated.Find("red").frame.team==-1&&migrated.Find("red").frame.color==ModeNames.Colors[0]&&migrated.Find("red").frame.name==originalName,"Restart after migration restores survivor color, neutral team and stable identity");
            Check(migrated.Find("green").frame.color==ModeNames.Colors[1],"Restart still excludes green from Infection survivors");
            migrated.Configure(ModeSimulation.Defaults(GameModeKind.Demo));
            Check(migrated.Find("green").frame.color==ModeNames.Colors[7]&&migrated.Find("green").frame.name.StartsWith("Green"),"Leaving Infection restores original palette color after migration");
            migrated.Configure(ModeSimulation.Defaults(GameModeKind.CaptureFlag));migrated.Configure(ModeSimulation.Defaults(GameModeKind.Demo));
            Check(migrated.Find("red").frame.color==ModeNames.Colors[0]&&migrated.Find("green").frame.color==ModeNames.Colors[7],"Team colors never overwrite individual survivor palette");
        }
        static void SearchGeometry()
        {
            var supports=new List<Vector3>{new Vector3(4,4,0),new Vector3(-4,6,0),new Vector3(0,8,4),new Vector3(0,96,0),new Vector3(40,4,0),Vector3.zero};
            var result=new List<Vector3>();ModeSearchGeometry.Select(Vector3.zero,supports,false,result);
            Check(result.Count==3&&result.Contains(supports[0])&&result.Contains(supports[1]),"An infected search without a visible victim emits diverse local climbing stages");
            Check(!result.Contains(new Vector3(0,96,0))&&!result.Contains(new Vector3(40,4,0)),"Search uses local support geometry rather than distant hidden victim or unreachable summit");
            var first=result.ToArray();Vector3 invisibleVictim=new Vector3(200,96,200);invisibleVictim+=Vector3.right*100;
            ModeSearchGeometry.Select(Vector3.zero,supports,false,result);bool same=result.Count==first.Length;for(int i=0;i<first.Length;i++)same&=first[i]==result[i];
            Check(same&&!result.Contains(invisibleVictim),"Moving an unobserved victim cannot change geometry-only search targets");
            ModeSearchGeometry.Select(Vector3.zero,supports,true,result);Check(result.Count==0,"Visible attack targets suppress search fallback");
            ModeSearchGeometry.Select(new Vector3(4,4,0),supports,false,result);Check(result.Count>0&&!result.Contains(supports[0]),"Reaching a search stage advances to higher known platforms");
            var midfield=new List<Vector3>{new Vector3(-25,4,0),new Vector3(-5,8,2),new Vector3(3,8,-2),new Vector3(-45,4,0)};
            ModeSearchGeometry.Midfield(new Vector3(-40,0,0),Vector3.zero,midfield,false,result);
            Check(result.Count>0&&!result.Contains(midfield[3]),"Separated ticket teams without a visible enemy receive public centerward battle objectives");
            ModeSearchGeometry.Midfield(Vector3.zero,Vector3.zero,midfield,false,result);bool central=result.Count>0;foreach(var p in result)central&=Vector3.ProjectOnPlane(p,Vector3.up).magnitude<=12;
            Check(central,"Ticket patrol remains around public midfield after arriving instead of seeking hidden players");
            ModeSearchGeometry.Midfield(new Vector3(-40,0,0),Vector3.zero,midfield,true,result);Check(result.Count==0,"Visible ticket combat suppresses midfield fallback");
        }
        static void HazardTactics()
        {
            var rain=new List<DisasterFrame>{new DisasterFrame{kind="AcidRain",warning=2}};
            var waves=new List<WaveFrame>();var points=new List<Vector3>{Vector3.right*4,Vector3.left*3,Vector3.up*96};var safe=new List<Vector3>();
            ModeHazardTactics.Select(Vector3.zero,1.7f,points,rain,waves,p=>p.x>2,safe);
            Check(safe.Count==1&&safe[0]==points[0],"Warned acid rain selects nearby standing shelter over exposed or unreachable platforms");
            Check(ModeHazardTactics.Risk(Vector3.zero,1.7f,false,rain,waves)>ModeHazardTactics.Risk(Vector3.zero,1.7f,true,rain,waves),"Actual shelter removes acid-rain exposure risk");
            var storms=new List<DisasterFrame>{new DisasterFrame{kind="Tornado",center=Vector3.zero,values=new[]{0f,0,0,10}}};
            ModeHazardTactics.Select(Vector3.zero,1.7f,points,storms,waves,p=>false,safe);Check(safe.Count==1&&safe[0]==points[0],"Tornado escape prefers a local support farther from the funnel");
            storms.Clear();storms.Add(new DisasterFrame{kind="MeteorShower",points=new[]{new DisasterPoint{position=new Vector3(0,10,0),velocity=Vector3.down*18,size=2}}});
            Check(ModeHazardTactics.Risk(Vector3.zero,1.7f,false,storms,waves)>1,"Meteor risk anticipates approaching trajectory rather than current visual position only");
            storms.Clear();waves.Add(new WaveFrame{active=true,center=Vector3.zero,direction=Vector3.right,width=8,depth=2,height=4,speed=2});
            points=new List<Vector3>{Vector3.up*6,Vector3.forward*8,Vector3.right*2};ModeHazardTactics.Select(Vector3.zero,1.7f,points,storms,waves,p=>false,safe);
            Check(safe.Count==1&&safe[0]!=points[2],"Approaching wave permits higher or lateral refuge instead of walking farther into its corridor");
        }
        static void Tickets()
        {
            var s=Populate(GameModeKind.TeamTickets,2);var r=s.Rules.Copy();r.tickets=2;s.Configure(r);s.Find("p0").frame.team=0;s.Find("p1").frame.team=1;s.StartRound();Step(s,2.2f);
            Check(s.Kill("p1","p0")==EliminationOutcome.Respawn,"First team ticket respawns");Check(s.Kill("p1","p0")==EliminationOutcome.Respawn,"Last available ticket still respawns");Check(s.Kill("p1","p0")==EliminationOutcome.Spectator,"Next death after ticket exhaustion spectates");Step(s,.2f);
            Check(s.Phase==RoundPhase.Victory&&s.WinnerTeam==0,"Last remaining team wins a full ticket round");
        }
        static void Hill()
        {
            var s=Populate(GameModeKind.KingOfHill,2);var r=s.Rules.Copy();r.roundSeconds=10;s.Configure(r);s.StartRound();s.Hill=Vector3.up*10;Step(s,2.2f);
            s.Observe("p0",s.Hill,Quaternion.identity,10,1,true,-1);s.Observe("p1",Vector3.zero,Quaternion.identity,0,1,true,-1);Step(s,12);
            Check(s.Phase==RoundPhase.Victory&&s.Winner==s.Find("p0").frame.name,"Highest-region occupation wins a full timed hill round");
            Check(s.Find("p0").frame.metric>s.Find("p1").frame.metric,"Airborne or distant players do not earn hill time");
        }
        static void Migration()
        {
            var s=Populate(GameModeKind.BlockControl,10);s.StartRound();Step(s,4);var frame=s.Snapshot();var adopted=new ModeSimulation(new ModeRules());adopted.Restore(frame);var copy=adopted.Snapshot();
            Check(copy.phase==frame.phase&&Mathf.Abs(copy.elapsed-frame.elapsed)<.001f&&copy.players.Length==frame.players.Length,"New authority restores phase, elapsed time and roster");
            Check(copy.players[0].name==frame.players[0].name,"Migration preserves generated identity");
            var power=Populate(GameModeKind.Demo,3);power.StartRound();power.HandPower("p0",7);Step(power,3);
            var pending=power.Find("p1").frame;pending.dying=true;pending.deathProgress=.4f;pending.deathRemaining=.54f;pending.pendingOutcome="Respawn";pending.sinking=true;pending.sinkProgress=.3f;pending.health=.4f;
            var transferred=power.Snapshot();var newHost=new ModeSimulation(new ModeRules());int duplicatePhase=0;newHost.PhaseChanged+=phase=>duplicatePhase++;newHost.Restore(transferred);
            Check(duplicatePhase==0,"Migration restore does not duplicate round-start events");
            Check(newHost.Find("p0").frame.role=="hand"&&transferred.players[0].handPowerRemaining>3.9f,"Migration transfers remaining temporary hand duration");
            int expired=0;newHost.PowerFinished+=id=>expired++;Step(newHost,3.8f);Check(newHost.Find("p0").frame.role=="hand","Migrated temporary hand does not expire early");Step(newHost,.3f);
            Check(newHost.Find("p0").frame.role=="survivor"&&expired==1,"Migrated temporary hand returns to original role exactly once");
            var restored=newHost.Find("p1").frame;Check(restored.dying&&restored.pendingOutcome=="Respawn"&&Mathf.Approximately(restored.deathRemaining,.54f)&&restored.sinking,"Migration preserves pending death outcome, delay and sinking state");
            var ledger=new RemoteHealthLedger();ledger.Seed(restored.id,restored.health,false);ledger.Damage(restored.id,10,true);Check(Mathf.Approximately(ledger.Read(restored.id),.3f),"Migrated remote damage continues from prior health without healing");
            var history=Populate(GameModeKind.Demo,2);history.StartRound();history.Observe("p0",Vector3.up*10,Quaternion.identity,10,1,true,-1);Step(history,2);
            var historyCopy=new ModeSimulation(new ModeRules());historyCopy.Restore(history.Snapshot());history.Observe("p0",Vector3.up*20,Quaternion.identity,20,1,true,-1);historyCopy.Observe("p0",Vector3.up*20,Quaternion.identity,20,1,true,-1);Step(history,1);Step(historyCopy,1);
            Check(Mathf.Abs(history.Find("p0").frame.averageHeight-historyCopy.Find("p0").frame.averageHeight)<.001f,"Migrated average height retains historical weighting");
            var spectators=Populate(GameModeKind.Survival,12);spectators.StartRound();Step(spectators,3);for(int i=1;i<9;i++)spectators.Kill("p"+i,null);Step(spectators,.1f);var targets=new HashSet<string>();for(int i=1;i<9;i++)targets.Add(spectators.Find("p"+i).frame.spectating);
            Check(targets.Count>1,"Eliminated bots choose varied stable spectator targets");
            var restoredNames=new HashSet<string>();foreach(var participant in newHost.Participants)restoredNames.Add(participant.frame.name);bool unique=true;for(int i=3;i<100;i++){var joined=newHost.Register("new"+i,Color.red,false,false);unique&=restoredNames.Add(joined.frame.name);}Check(unique,"Joining after migration never duplicates restored names");
            foreach(var mode in new[]{GameModeKind.Survival,GameModeKind.Infection})
            {var original=Populate(mode,4);original.StartRound();Step(original,4);int repeated=0;original.Eliminated+=(id,outcome)=>repeated++;original.Restore(original.Snapshot());Check(repeated==0,"Restoring live "+mode+" does not replay latejoin eliminations");}
            var restoredClock=new ModeDisasterClock();restoredClock.Restore(100,"hazard",3,0);int launches=0;restoredClock.Tick(ModeSimulation.Defaults(GameModeKind.Survival),102,0,()=>"other",id=>{launches++;return true;});Check(launches==0&&restoredClock.Pending=="hazard","Migration preserves an existing disaster warning countdown");restoredClock.Tick(ModeSimulation.Defaults(GameModeKind.Survival),103,0,()=>"other",id=>{launches++;return true;});Check(launches==1,"Migrated warned disaster launches once after remaining warning");
            var old=new ModeSimulation(ModeSimulation.Defaults(GameModeKind.CaptureFlag));old.Register("departed-host",Color.red,false,true);old.Register("reconnecting",Color.blue,false,false);old.Register("bot:stable",Color.yellow,true,false);
            old.Find("departed-host").frame.team=0;old.Find("reconnecting").frame.team=1;old.StartRound();old.SetFlagHomes(new[]{Vector3.zero,Vector3.right*10});old.Observe("departed-host",Vector3.right*10,Quaternion.identity,0,1,true,-1);Step(old,3.2f);
            var promoted=new ModeSimulation(new ModeRules());promoted.Restore(old.Snapshot());int events=0;promoted.Eliminated+=(id,outcome)=>events++;
            var removed=promoted.ReconcileHumans(new[]{"reconnecting"});
            Check(removed.Length==1&&removed[0]=="departed-host"&&promoted.Snapshot().players.Length==2,"Migration removes departed host but retains reconnecting human and stable GUID bot");
            Check(!promoted.CarriesFlag("departed-host")&&events==0,"Departed host releases carried flag without replaying an elimination");


        }
        static void ObjectiveEligibility()
        {
            foreach(string role in new[]{"dying","hand","spectator"})
            {
                var claims=Populate(GameModeKind.BlockControl,2);claims.Rules.claimSeconds=.2f;claims.StartRound();Step(claims,3);
                var player=claims.Find("p0");player.frame.team=0;player.frame.role=role=="dying"?"survivor":role;player.frame.dying=role=="dying";
                claims.Observe("p0",Vector3.zero,Quaternion.identity,0,1,true,0);Step(claims,1);
                Check(claims.OwnerOf(0)==-1,role+" actor cannot claim using stale body contact");
                var hill=Populate(GameModeKind.KingOfHill,2);hill.StartRound();Step(hill,3);hill.Hill=Vector3.zero;
                player=hill.Find("p0");player.frame.role=role=="dying"?"survivor":role;player.frame.dying=role=="dying";hill.Observe("p0",Vector3.zero,Quaternion.identity,0,1,true,-1);Step(hill,.5f);
                Check(hill.Find("p0").frame.metric==0,role+" actor cannot score hill time from inactive body");
            }
            var flag=Populate(GameModeKind.CaptureFlag,2);flag.StartRound();Step(flag,3);flag.Find("p0").frame.team=0;flag.Find("p1").frame.team=1;
            flag.SetFlagHomes(new[]{Vector3.left*10,Vector3.right*10});flag.Observe("p0",Vector3.right*10,Quaternion.identity,0,1,true,-1);Step(flag,.1f);
            var enemy=new List<FlagFrame>(flag.Flags)[1];Check(enemy.carrier=="p0","Live player can take enemy flag before death test");
            flag.Eliminated+=(id,outcome)=>flag.Find(id).frame.dying=true;flag.Kill("p0",null);Step(flag,.5f);
            Check(enemy.carrier==null&&!enemy.atHome,"Dying carrier drops flag and cannot immediately reacquire it");
            flag.Find("p0").frame.dying=false;flag.Find("p0").frame.role="hand";Step(flag,.5f);Check(enemy.carrier==null,"Temporary hand cannot pick up dropped flag through inactive survivor body");
        }
        static void Schedules()
        {
            var r=ModeSimulation.Defaults(GameModeKind.Survival);var clock=new ModeDisasterClock(5);clock.Reset(r,0);float warningAt=-1,first=-1,last=-1;int launched=0;
            for(int t=0;t<1800;t++)
            {
                clock.Tick(r,t,0,()=>"hazard",id=>{if(first<0)first=t;last=t;launched++;return true;});
                if(clock.Pending.Length>0&&warningAt<0)warningAt=t;
            }
            Check(warningAt>=90&&warningAt<=120,"First scheduled disaster begins warning after 90–120 seconds");
            Check(first-warningAt>=r.disasterWarningSeconds,"Warnings precede launch by configured time");
            Check(launched>=15&&last>1700,"Full 30-minute simulation continues bounded late-round disaster pacing");
            int blocked=0;clock.Reset(r,0);for(int t=0;t<500;t++)clock.Tick(r,t,2,()=>"hazard",id=>{blocked++;return true;});Check(blocked==0,"Concurrent disaster cap prevents additional launches");
            r.mode=GameModeKind.Demo;clock.Reset(r,0);for(int t=0;t<500;t++)clock.Tick(r,t,0,()=>"hazard",id=>{blocked++;return true;});Check(blocked==0,"Demo never auto-launches disasters");
        }
        static void Performance()
        {
            var s=Populate(GameModeKind.Demo,100);var r=s.Rules.Copy();r.metric=ScoreMetric.CurrentHeight;s.Configure(r);
            for(int i=0;i<100;i++)s.Observe("p"+i,Vector3.up*i,Quaternion.identity,i,1,true,-1);
            var watch=Stopwatch.StartNew();for(int i=0;i<1000;i++)s.TopTen();watch.Stop();var f=s.Snapshot();int ranks=0;foreach(var row in f.players)if(row.rank>0)ranks++;
            Check(f.topTen.Length==10&&f.topTen[0].id=="p99"&&ranks==10,"100-player selection ranks only the top ten");
            results.Add("MEASURED desktop 100-player top-ten selection: "+(watch.Elapsed.TotalMilliseconds/1000).ToString("F4")+" ms; excludes Unity physics, rendering and network transport.");
        }
        static void TuningAndHealth()
        {
            var s=Populate(GameModeKind.BlockControl,2);s.StartRound();Step(s,4);string name=s.Find("p0").frame.name;float elapsed=s.Elapsed;
            var rules=s.Rules.Copy();rules.punchDistance=6;s.Tune(rules);
            Check(s.Phase==RoundPhase.Playing&&s.Elapsed==elapsed&&s.Find("p0").frame.name==name,"Runtime tuning preserves round, timer and generated identity");
            rules=s.Rules.Copy();rules.teamCount=3;s.Tune(rules);Check(s.Phase==RoundPhase.Waiting,"Structural team changes wait for an explicit new round");
            var health=new RemoteHealthLedger();Check(!health.Damage("remote",40,true)&&Mathf.Approximately(health.Read("remote"),.6f),"Host ledger applies remote hazard damage");
            Check(health.Damage("remote",60,true)&&!health.Damage("remote",1,true),"Remote lethal damage eliminates exactly once");
            health.Reset("remote");Check(!health.Damage("remote",1000,false)&&health.Read("remote")==0,"Demo health-zero persistence does not report a kill");
            Check(health.Damage("remote",1,true),"Re-enabling death permits elimination from zero health");
            health.Reset("remote");Check(health.Read("remote")==1,"Remote respawn resets authoritative health");
            var power=Populate(GameModeKind.Demo,2);power.StartRound();int expired=0;power.PowerFinished+=id=>expired++;power.HandPower("p0",1);Check(power.Find("p0").frame.role=="hand","Temporary hand power changes role");Step(power,1.2f);Check(power.Find("p0").frame.role=="survivor"&&expired==1&&!power.Find("p0").frame.dying,"Hand power expires without a death transition");
            power.Find("p0").frame.dying=true;power.StartRound();Check(!power.Find("p0").frame.dying,"Fresh rounds clear unfinished death transitions");
            var f=s.Snapshot();Check(ModeFrameValidation.Frame(f,0),"Valid bounded mode snapshot passes validation");
            f.players[1].id=f.players[0].id;Check(!ModeFrameValidation.Frame(f,0),"Duplicate participant IDs are rejected");
            f=s.Snapshot();f.players[0].metric=float.NaN;Check(!ModeFrameValidation.Frame(f,0),"Non-finite snapshot scores are rejected");
        }
    }
}
