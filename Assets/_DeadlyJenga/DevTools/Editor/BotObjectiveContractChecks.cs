using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastleBots;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class BotObjectiveContractChecks
    {
        sealed class Facts : IBotModeFacts
        {
            public bool emergency, completed, carrying, delivered, invalid, contact;
            public Vector3 destination;
            public float homeUtility;
            public bool SupportsQuery(string name) => new[] { "home", "attack", "safety", "parcel", "delivery", "close-threats" }.Contains(name);
            public bool SupportsCondition(string name) => new[] { "completed", "carrying", "delivered" }.Contains(name);
            public bool SupportsInteraction(string name) => name == "collect-parcel" || name == "deliver-parcel";
            public bool Test(string name, BotDirectorActor actor, BotDirectorTarget target) => name == "completed" ? completed : name == "carrying" ? carrying : name == "delivered" && delivered;
            public void Query(string name, BotDirectorActor actor, List<BotDirectorTarget> results)
            {
                if (name == "safety" && !emergency || name == "parcel" && carrying || name == "delivery" && !carrying || name=="close-threats"&&!contact) return;
                results.Add(new BotDirectorTarget { id = name, utility = name=="home"?homeUtility:0, position = invalid ? new Vector3(float.NaN, 0, 0) : destination });
            }
        }
        static int assertions;
        public static bool LastPassed { get; private set; }
        static readonly List<string> lines = new List<string>();
        static string ReportPath => ProjectPath.Report("BotObjectiveContracts.txt");
        static BotObjectiveContractChecks() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string request = ProjectPath.Report("BotObjectiveContracts.request");
            if (!File.Exists(request)) return;
            File.Delete(request); Run();
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Bot Objective Contracts")]
        public static void Run()
        {
            assertions = 0; lines.Clear(); LastPassed = false;
            try
            {
                Validation(); Allocation(); Lifecycle(); Blockage(); Holding(); Delivery(); Tactics(); Combat(); Contact(); TargetUtility(); CtfStaffing(); Assets();
                lines.Add("PASS " + assertions + " assertions. These are director/contract tests, not motor, Quest or multiplayer tests.");
                LastPassed = true;
            }
            catch (Exception e) { lines.Add("FAIL " + e); }
            File.WriteAllLines(ReportPath, lines);
            Debug.Log(string.Join("\n", lines));
        }
        static void Check(bool condition, string message)
        { assertions++; if (!condition) throw new Exception(message); }
        static BotObjectiveTemplate Template(string id, string query, int minimum = 0, int maximum = 100, float utility = 1) =>
            new BotObjectiveTemplate { id = id, query = query, minimum = minimum, maximum = maximum, utility = utility };
        static BotModeDefinition Definition(params BotObjectiveTemplate[] templates) => new BotModeDefinition { id = "contract-fixture", description="Deterministic objective contract fixture", objectives = templates };
        static List<BotDirectorActor> Actors(int count, int team = 0) => Enumerable.Range(0, count).Select(i => new BotDirectorActor { id = "bot-" + i, team = team }).ToList();
        static void Validation()
        {
            foreach(var json in new[] { "{\"id\":\"a\",\"schemaVersion\":1,\"objectives\":[],\"typo\":1}", "{\"id\":\"a\",\"id\":\"b\",\"schemaVersion\":1,\"objectives\":[]}", "{\"id\":\"a\",\"schemaVersion\":1,\"objectives\":[{\"id\":\"x\",\"query\":\"home\",\"skill\":\"Travel\",\"maximun\":2}]}" })
            { bool rejected=false; try{BotModeDefinition.FromJson(json);}catch(ArgumentException){rejected=true;} Check(rejected,"Malformed/unknown JSON fields accepted"); }
            var f = new Facts(); var t = Template("a", "home"); var d = Definition(t);
            Check(BotModeValidation.Validate(d, f).Count == 0, "Valid definition rejected");
            d.schemaVersion = 2; Check(BotModeValidation.Validate(d, f).Count > 0, "Future schema accepted"); d.schemaVersion = 1;
            t.query = "typo"; Check(BotModeValidation.Validate(d, f).Count > 0, "Unknown query accepted"); t.query = "home";
            t.skill = "Teleport"; Check(BotModeValidation.Validate(d, f).Count > 0, "Unknown skill accepted"); t.skill = "Travel";
            t.minimum = 5; t.maximum = 2; Check(BotModeValidation.Validate(d, f).Count > 0, "Impossible caps accepted"); t.minimum = 0; t.maximum = 100;
            t.require = new[] { "carrying" }; t.exclude = new[] { "carrying" }; Check(BotModeValidation.Validate(d, f).Count > 0, "Contradictory conditions accepted");
            t.require = new[] { "unknown" }; t.exclude = Array.Empty<string>(); Check(BotModeValidation.Validate(d, f).Count > 0, "Unknown condition accepted"); t.require = Array.Empty<string>();
            t.utility = float.NaN; Check(BotModeValidation.Validate(d, f).Count > 0, "NaN accepted"); t.utility = 1;
            t.interaction = "missing"; Check(BotModeValidation.Validate(d, f).Count > 0, "Missing interaction accepted");
            lines.Add("PASS invalid schema, queries, skills, limits, conditions, numbers and interactions rejected.");
        }
        static void Allocation()
        {
            var f = new Facts(); var guard = Template("guard", "home", 1, 2, .5f); var attack = Template("attack", "attack", 0, 100, 2);
            var safety = Template("safety", "safety", 0, 1, 3); safety.tier = 3; safety.personal = true;
            var director = new BotObjectiveDirector(Definition(guard, attack, safety), f); var actors = Actors(5);
            director.Tick(0, actors);
            int guards = 0;
            foreach (var a in actors) { Check(director.TryGet(a.id, out var goal, out _), "Unassigned eligible actor"); if (goal.id.StartsWith("guard/")) guards++; }
            var region=new BotObjective{kind=BotObjectiveKind.Contest,radius=2.5f};
            var slot=CastleBotPopulation.ApplyAssignedPosition(region,Vector3.right*1.6f);
            var outside=CastleBotPopulation.ApplyAssignedPosition(region,Vector3.right*2.4f);
            Check(guards >= 1 && guards <= 2 && slot.position.magnitude+slot.radius<=region.radius+.001f && outside.position==region.position, "Staffing limits or assigned region boundary violated");
            director.TryGet(actors[0].id, out _, out var before);
            f.emergency = true; director.Tick(1, actors);
            foreach (var a in actors) { director.TryGet(a.id, out var goal, out _); Check(goal.id.StartsWith("safety/"), "Emergency failed to preempt"); }
            Check(director.ActiveCount == 5, "Personal capacities incorrectly shared");
            f.emergency = false; director.Tick(2, actors); director.TryGet(actors[0].id, out var current, out var policy);
            director.Report(actors[0].id, new BotExecutionFeedback { objectiveId = current.id, assignment = before.assignment, status = BotExecutionStatus.Failed });
            Check(director.TryGet(actors[0].id, out _, out _), "Stale feedback cancelled new assignment");
            f.destination = Vector3.right * 10; director.Tick(2.5f, actors); director.TryGet(actors[0].id, out current, out var moving);
            Check(moving.assignment == policy.assignment && current.position == f.destination, "Moving target reset token or stayed stale");
            actors[0] = new BotDirectorActor { id = actors[0].id, team = 1 }; director.Tick(3, actors);
            director.TryGet(actors[0].id, out _, out var switched); Check(switched.assignment != policy.assignment, "Team switch retained old assignment");
            actors.RemoveAt(0); director.Tick(4, actors); Check(director.ActiveCount == 4, "Removed actor retained assignment");
            lines.Add("PASS staffing limits, personal emergencies, stale feedback, moving targets, team changes and departure.");
        }
        static void Lifecycle()
        {
            var f = new Facts(); var t = Template("work", "home"); t.completeWhen = "completed"; t.timeout = 5; t.retry = 10;
            var director = new BotObjectiveDirector(Definition(t), f); var actors = Actors(1);
            director.Tick(0, actors); director.TryGet("bot-0", out var goal, out var policy);
            director.Report("bot-0", new BotExecutionFeedback { objectiveId = goal.id, assignment = policy.assignment, status = BotExecutionStatus.Arrived });
            Check(!director.History.Any(e => e.outcome == BotAssignmentOutcome.Succeeded), "Arrival fabricated success");
            f.completed = true; director.Tick(1, actors);
            Check(director.History.Count(e => e.outcome == BotAssignmentOutcome.Succeeded) == 1 && director.ActiveCount == 0, "Mode completion ignored or reassigned completed work");
            f.completed = false; director.Tick(2, actors); director.Tick(7, actors);
            Check(director.ActiveCount == 0 && director.History.Any(e => e.outcome == BotAssignmentOutcome.Failed), "Timeout failed to cool down");
            director.Tick(8, actors); Check(director.ActiveCount == 0, "Failed target immediately retried");
            director.Tick(18, actors); Check(director.ActiveCount == 1, "Cooldown never expired");
            director.TryGet("bot-0", out goal, out policy); director.Reset(19, "Round reset"); director.Tick(20, actors);
            director.TryGet("bot-0", out _, out var restarted); Check(restarted.assignment > policy.assignment, "Round reset reused token");
            var replacement=new BotObjectiveDirector(Definition(t),f);replacement.Tick(20,actors);replacement.TryGet("bot-0",out _,out var replaced);
            Check(replaced.assignment>restarted.assignment,"New director reused an obsolete token");
            f.invalid = true; bool rejected = false; try { director.Tick(21, actors); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Invalid runtime target accepted");
            lines.Add("PASS arrival versus success, authoritative completion, timeout, cooldown, reset tokens and invalid facts.");
        }
        static void Delivery()
        {
            var f = new Facts(); var pickup = Template("collect", "parcel"); pickup.completeWhen = "carrying"; pickup.interaction = "collect-parcel";
            var deliver = Template("deliver", "delivery"); deliver.require = new[] { "carrying" }; deliver.completeWhen = "delivered"; deliver.interaction = "deliver-parcel";
            var director = new BotObjectiveDirector(Definition(pickup, deliver), f); var actors = Actors(1);
            director.Tick(0, actors); director.TryGet("bot-0", out var goal, out var policy);
            Check(goal.id == "collect/parcel" && policy.interaction == "collect-parcel", "Pickup not assigned");
            f.carrying = true; director.Tick(1, actors); director.TryGet("bot-0", out goal, out policy);
            Check(goal.id == "deliver/delivery" && policy.interaction == "deliver-parcel", "Delivery did not follow pickup");
            f.delivered = true; director.Tick(2, actors);
            Check(director.ActiveCount == 0 && director.History.Count(e => e.outcome == BotAssignmentOutcome.Succeeded) == 2, "Delivery chain did not complete");
            lines.Add("PASS independent parcel-delivery fixture uses new facts/definitions and unchanged director/Characters.");
        }
        static void Blockage()
        {
            var f=new Facts();var work=Template("work","home",0,100,2);var fallback=Template("fallback","attack",0,100,.1f);
            var director=new BotObjectiveDirector(Definition(work,fallback),f);var actors=Actors(1);
            director.Tick(0,actors);director.TryGet("bot-0",out var goal,out var policy);
            var feedback=new BotExecutionFeedback{objectiveId=goal.id,assignment=policy.assignment,status=BotExecutionStatus.Blocked};
            director.Report("bot-0",feedback);director.Tick(5,actors);director.Report("bot-0",feedback);
            Check(director.ActiveCount==0,"Sustained blockage did not fail");
            director.Tick(5.5f,actors);director.TryGet("bot-0",out goal,out _);Check(goal.id.StartsWith("fallback/"),"Failed route prevented alternative assignment");
            bool rejected=false;try{director.Tick(6,Actors(101));}catch(ArgumentException){rejected=true;}Check(rejected,"Unbounded actor count accepted");
            lines.Add("PASS sustained blockage, alternative objectives and bounded actor population.");
        }
        static void Holding()
        {
            var f=new Facts();var hold=Template("hold","home");hold.skill="Guard";hold.timeout=1;
            var director=new BotObjectiveDirector(Definition(hold),f);var actors=Actors(1);director.Tick(0,actors);
            director.TryGet("bot-0",out var goal,out var policy);
            var feedback=new BotExecutionFeedback{objectiveId=goal.id,assignment=policy.assignment,status=BotExecutionStatus.Arrived};
            director.Report("bot-0",feedback);
            foreach(float time in new[]{.5f,1.2f,1.8f}){director.Tick(time,actors);director.Report("bot-0",feedback);}
            Check(director.ActiveCount==1&&!director.History.Any(e=>e.outcome==BotAssignmentOutcome.Failed),"Legitimate ongoing hold timed out");
            director.Tick(3,actors);Check(director.ActiveCount==0,"Ongoing lease without execution never expired");
            lines.Add("PASS legitimate holding renews its ongoing lease; abandoned execution still times out.");
        }
        sealed class CtfStaffingFacts:IBotModeFacts
        {
            public bool SupportsQuery(string name)=>true;
            public bool SupportsCondition(string name)=>true;
            public bool SupportsInteraction(string name)=>false;
            public bool Test(string name,BotDirectorActor actor,BotDirectorTarget target)=>false;
            public void Query(string name,BotDirectorActor actor,List<BotDirectorTarget> results)
            {
                if(name=="home")results.Add(new BotDirectorTarget{id="home",position=Vector3.zero});
                if(name=="enemy-flags")results.Add(new BotDirectorTarget{id="enemy",position=Vector3.right*80});
                if(name=="patrol")results.Add(new BotDirectorTarget{id="near",position=Vector3.right*3});
            }
        }
        static void CtfStaffing()
        {
            var asset=Resources.Load<TextAsset>("BotModes/CaptureFlag");
            var director=new BotObjectiveDirector(BotModeDefinition.FromJson(asset.text),new CtfStaffingFacts());
            var actors=Actors(5);director.Tick(0,actors);int attackers=0,guards=0;
            foreach(var actor in actors)
            {
                Check(director.TryGet(actor.id,out var goal,out _),"CTF actor remained unassigned");
                if(goal.id.StartsWith("flag/"))attackers++;if(goal.id.StartsWith("guard-home/"))guards++;
            }
            Check(attackers>=3&&guards>=1,"Nearby optional patrols starved cross-map flag offense or home defense");
            lines.Add("PASS shipped CTF profile keeps a defender and sends raiders across an 80m map despite nearby patrol opportunities.");
        }
        static void TargetUtility()
        {
            var facts=new Facts{homeUtility=2};var home=Template("home","home",0,100,.2f);var other=Template("other","attack");
            var director=new BotObjectiveDirector(Definition(home,other),facts);var actors=Actors(1);
            director.Tick(0,actors);director.TryGet("bot-0",out var first,out _);
            Check(first.id.StartsWith("home/"),"Query-specific utility was discarded");
            facts.homeUtility=-2;director.Tick(4,actors);director.TryGet("bot-0",out var second,out _);
            Check(second.id.StartsWith("other/"),"Utility changes could not displace stale commitment");
            facts.homeUtility=float.NaN;bool rejected=false;try{director.Tick(5,actors);}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"Nonfinite target utility accepted");
            facts.homeUtility=11;rejected=false;try{director.Tick(6,actors);}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"Unbounded target utility accepted");
            lines.Add("PASS query-specific utility ranking, reassignment and finite bounded facts.");
        }
        static void Contact()
        {
            var f=new Facts();var repel=Template("repel","close-threats",0,1,3.5f);repel.skill="Attack";repel.tier=2;repel.personal=true;
            var safety=Template("safety","safety",0,1,4);safety.tier=3;safety.personal=true;
            var director=new BotObjectiveDirector(Definition(Template("travel","home"),repel,safety),f);var actors=Actors(1);
            director.Tick(0,actors);Check(director.TryGet("bot-0",out var goal,out _)&&goal.id.StartsWith("travel/"),"Contact response activated without a permitted close target");
            f.contact=true;director.Tick(1,actors);Check(director.TryGet("bot-0",out goal,out _)&&goal.kind==BotObjectiveKind.Attack&&goal.id.StartsWith("repel/"),"Immediate contact failed to interrupt ordinary travel");
            f.emergency=true;director.Tick(2,actors);Check(director.TryGet("bot-0",out goal,out _)&&goal.id.StartsWith("safety/"),"Contact attack displaced immediate hazard safety");
            f.emergency=f.contact=false;director.Tick(3,actors);Check(director.TryGet("bot-0",out goal,out _)&&goal.id.StartsWith("travel/"),"Travel did not resume after contact ended");
            f.contact=true;director.Tick(4,actors);Check(director.TryGet("bot-0",out goal,out _)&&goal.id.StartsWith("repel/"),"Normal contact cancellation incorrectly imposed failure cooldown");
            lines.Add("PASS bounded personal contact response interrupts travel, yields to immediate safety and resumes normal work.");
        }
        static void Combat()
        {
            Check(BotCombatPolicy.InReach(new Vector3(0,1.7f,0),new Vector3(1.3f,.6f,0)),"Ordinary torso contact rejected");
            Check(!BotCombatPolicy.InReach(Vector3.zero,Vector3.forward*2.2f),"Bot exceeded authoritative punch reach");
            Check(!BotCombatPolicy.InReach(Vector3.up*4,Vector3.right),"Scale invented extra punch reach");
            Check(BotCombatPolicy.Facing(Vector3.forward,new Vector3(0,-1,1)),"Bot yaw alignment incorrectly requires unavailable head pitch");
            Check(!BotCombatPolicy.Facing(Vector3.back,Vector3.forward),"Punch allowed while facing away");
            Check(!BotCombatPolicy.Facing(Vector3.forward,Vector3.up),"Vertical-only target supplied a facing direction");
            Check(BotCombatPolicy.ShieldFront(Vector3.forward,Vector3.zero,Vector3.forward),"Carrier's front shield ignored");
            Check(!BotCombatPolicy.ShieldFront(Vector3.back,Vector3.zero,Vector3.forward),"Carrier's back classified as shielded");
            Check(BotCombatPolicy.Lead(Vector3.right*100).magnitude<=.601f,"Observed velocity created unbounded prediction");
            Check(BotCombatPolicy.Lead(Vector3.up*100)==Vector3.zero,"Falling velocity invented a vertical combat destination");
            Check(ModeBotTactics.CombatUtility(5,false,.1f,true)==ModeBotTactics.CombatUtility(5,false,1,false),"Hidden health or flag status influenced remembered target ranking");
            Check(ModeBotTactics.CombatUtility(5,true,.2f,false)>ModeBotTactics.CombatUtility(5,true,1,false),"Visible weakened opponent ignored");
            Check(ModeBotTactics.CombatUtility(5,true,1,true)>ModeBotTactics.CombatUtility(5,true,1,false),"Visible flag carrier ignored");
            Check(ModeBotTactics.CombatUtility(2,true,1,false)>ModeBotTactics.CombatUtility(18,true,1,false),"Nearby visible threat ranked below distant equivalent");
            lines.Add("PASS fixed punch reach, horizontal facing, shield approach and bounded observed-motion prediction.");
        }
        static void Tactics()
        {
            Check(!ModeBotTactics.Retreat(.1f,true,0,0),"Retreat without a known threat");
            Check(ModeBotTactics.Retreat(.2f,false,1,2),"Critical health ignored");
            Check(!ModeBotTactics.Retreat(.8f,true,3,0),"Healthy bot abandoned combat");
            Check(ModeBotTactics.Retreat(.5f,true,3,0),"Last-life numerical disadvantage ignored");
            Check(!ModeBotTactics.Retreat(.5f,true,1,3),"Allied support ignored");
            Check(!CastleBotNavigation.ShouldStageAscent(Vector3.zero,new Vector3(80,4,0),false),"Remote destination caused ascent at the departure base");
            Check(CastleBotNavigation.ShouldStageAscent(Vector3.zero,new Vector3(12,30,0),false),"Nearby tall objective lost ascent recovery");
            Check(!CastleBotNavigation.ShouldStageAscent(Vector3.zero,new Vector3(1,8,0),true),"Descending transit was reversed into ascent");
            Check(ModeBotTactics.BlockRefreshed(true,5,5,0,0,0),"Actual grounded ownership refresh rejected");
            Check(!ModeBotTactics.BlockRefreshed(false,5,5,0,0,0),"Airborne proximity fabricated maintenance");
            Check(!ModeBotTactics.BlockRefreshed(true,4,5,0,0,0),"Adjacent block fabricated maintenance");
            Check(!ModeBotTactics.BlockRefreshed(true,5,5,1,0,0),"Enemy ownership counted as friendly maintenance");
            Check(!ModeBotTactics.BlockRefreshed(true,5,5,-1,-1,0),"Unassigned actor claimed an unowned block");
            Check(!ModeBotTactics.BlockRefreshed(true,5,5,0,0,1),"Stale decay state fabricated refresh");
            var watchdog=new BotTraversalProgress();watchdog.Observe(Vector3.zero,false,0,.1f);
            for(int i=1;i<=6;i++)watchdog.Observe(Vector3.zero,false,i*.5f,.1f);
            Check(watchdog.StalledFor>=2.5f,"Stationary airborne traversal was reported as progress");
            watchdog.Observe(Vector3.down,false,3.5f,.1f);Check(watchdog.StalledFor==0,"Real falling progress was classified as a stall");
            watchdog.Observe(Vector3.down,true,20,.1f);Check(watchdog.StalledFor==0,"Grounded tactical hold became a traversal failure");
            watchdog.Observe(Vector3.down,false,21,.1f);watchdog.Observe(Vector3.down,false,22,.1f);watchdog.Reset();
            Check(watchdog.StalledFor==0,"Respawn retained traversal blockage");
            watchdog.Observe(Vector3.zero,false,10,.1f);watchdog.Observe(Vector3.zero,false,11,.1f);watchdog.Observe(Vector3.zero,false,1,.1f);
            Check(watchdog.StalledFor==0,"Clock reset retained traversal blockage");
            var threats=new List<Vector3>{Vector3.left*2};
            Check(ModeBotTactics.EscapeUtility(Vector3.zero,Vector3.right*4,threats)>0,"Escape did not increase threat distance");
            Check(ModeBotTactics.EscapeUtility(Vector3.zero,Vector3.left,threats)<0,"Moving toward threat counted as escape");
            Check(ModeBotTactics.SupportUtility(Vector3.zero,Vector3.up*4,0)>ModeBotTactics.SupportUtility(Vector3.zero,Vector3.right*20,0),"Safe elevation not ranked above long flat detour");
            Check(!ModeBotTactics.MysteryHostile(MysteryRole.Sheriff,false)&&!ModeBotTactics.MysteryHostile(MysteryRole.Innocent,false),"Hidden role used as attack authority");
            Check(ModeBotTactics.MysteryHostile(MysteryRole.Murderer,false)&&ModeBotTactics.MysteryHostile(MysteryRole.Sheriff,true),"Own role or witnessed strike ignored");
            lines.Add("PASS retreat policy, safe escape direction, elevation utility and witnessed-only Mystery hostility.");
        }
        static void Assets()
        {
            // Check the same adapter registry used by production, without creating a scene.
            foreach (var mode in Enum.GetValues(typeof(GameModeKind)).Cast<GameModeKind>())
            {
                var asset = Resources.Load<TextAsset>("BotModes/" + mode); Check(asset, "Missing definition: " + mode);
                var errors = MatchModeRuntime.ValidateBotDefinition(BotModeDefinition.FromJson(asset.text));
                Check(errors.Count == 0, mode + ": " + string.Join("; ", errors));
                var contact=BotModeDefinition.FromJson(asset.text).objectives.FirstOrDefault(o=>o.id=="repel");
                if(mode!=GameModeKind.Demo)Check(contact!=null&&contact.query=="close-threats"&&contact.skill=="Attack"&&contact.personal&&contact.maximum==1&&contact.tier==2,"Missing bounded mode contact response: "+mode);
            }
            lines.Add("PASS every shipped mode definition validates against the production facts registry.");
        }
    }

    public sealed class BotObjectiveBuildGate : UnityEditor.Build.IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
        {
            BotObjectiveContractChecks.Run();
            if (!BotObjectiveContractChecks.LastPassed) throw new UnityEditor.Build.BuildFailedException("Bot objective contracts failed. See Logs/Validation/BotObjectiveContracts.txt.");
        }
    }
}
