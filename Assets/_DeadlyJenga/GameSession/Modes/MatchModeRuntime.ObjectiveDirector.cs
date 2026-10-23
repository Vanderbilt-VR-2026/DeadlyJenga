using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        BotObjectiveDirector botDirector;
        BotObjectiveDirector enemyDirector;
        MatchBotFacts enemyFacts;
        readonly List<BotDirectorActor> enemyDirectorActors=new List<BotDirectorActor>();
        MatchBotFacts botFacts;
        GameModeKind directorMode;
        float nextBotDirector;
        bool botDefinitionFailed;
        readonly List<BotDirectorActor> directorActors = new List<BotDirectorActor>();
        public BotObjectiveDirector ObjectiveDirector => botDirector;
        public static List<string> ValidateBotDefinition(BotModeDefinition definition) => BotModeValidation.Validate(definition, new MatchBotFacts(null));

        public void Collect(GameObject bot, List<BotObjective> goals)
        {
            if (!Authority || !bot || goals == null || simulation == null || simulation.Phase != RoundPhase.Playing) return;
            RefreshBotDirector();
            var director=bot.GetComponent<IEnemyActor>()?.IsEnemy==true?enemyDirector:botDirector;
            if (actorIds.TryGetValue(bot, out var id) && director != null && director.TryGet(id, out var objective, out _)) goals.Add(objective);
        }

        void ResetObjectiveDirector(string reason)
        {
            botDirector?.Reset(Time.time, reason);
            enemyDirector?.Reset(Time.time,reason);enemyDirector=null;enemyFacts=null;
            botDirector = null; botFacts = null; nextBotDirector = 0; botDefinitionFailed = false;
        }

        void RefreshBotDirector()
        {
            if(!Authority||simulation==null||simulation.Phase!=RoundPhase.Playing)return;
            if (botDirector != null && directorMode != Rules.mode) ResetObjectiveDirector("Mode changed");
            if (botDefinitionFailed) return;
            try
            {
                if (botDirector == null)
                {
                    directorMode = Rules.mode; botFacts = new MatchBotFacts(this);
                    var asset = Resources.Load<TextAsset>("BotModes/" + directorMode);
                    if (!asset) throw new InvalidOperationException("Missing bot definition for " + directorMode + ". Create a validated BotModes TextAsset; no legacy fallback is applied.");
                    botDirector = new BotObjectiveDirector(BotModeDefinition.FromJson(asset.text), botFacts);
                }
                if (Time.time < nextBotDirector) return;
                nextBotDirector = Time.time + .5f; directorActors.Clear();
                foreach (var p in simulation.Participants)
                {
                    var f = p.frame;
                    if (!p.present || !f.bot || !f.alive || f.dying || f.role == "hand" || f.role == "spectator" || f.role == "downed") continue;
                    var actor = FindActor(f.id); if (actor == null) continue;
                    directorActors.Add(new BotDirectorActor { id = f.id, team = f.team, body = actor.gameObject, position = f.position });
                }
                directorActors.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
                botFacts.BeginSample(); botDirector.Tick(Time.time, directorActors);
                if(enemyDirector==null)
                {
                    var infection=Resources.Load<TextAsset>("BotModes/Infection");if(!infection)throw new InvalidOperationException("Missing infected behavior for enemies");
                    enemyFacts=new MatchBotFacts(this);enemyDirector=new BotObjectiveDirector(BotModeDefinition.FromJson(infection.text),enemyFacts);
                }
                enemyDirectorActors.Clear();
                foreach(var p in simulation.Enemies)
                {
                    var actor=FindActor(p.frame.id);if(actor==null||!p.frame.alive||p.frame.dying)continue;
                    enemyDirectorActors.Add(new BotDirectorActor{id=p.frame.id,team=-2,body=actor.gameObject,position=actor.transform.position});
                }
                enemyFacts.BeginSample();enemyDirector.Tick(Time.time,enemyDirectorActors);
            }
            catch (Exception e)
            {
                botDirector?.Reset(Time.time, "Definition or facts failure"); botDirector = null; botDefinitionFailed = true;
                Debug.LogError("Bot objective contract failed: " + e.Message, this);
            }
        }

        public bool TryGetPolicy(GameObject actor, string objectiveId, out BotExecutionPolicy policy)
        {
            policy = default;
            var director=actor&&actor.GetComponent<IEnemyActor>()?.IsEnemy==true?enemyDirector:botDirector;
            return Authority && actor && actorIds.TryGetValue(actor, out var id) && director != null &&
                director.TryGet(id, out var objective, out policy) && objective.id == objectiveId;
        }
        public void Report(GameObject actor, BotExecutionFeedback feedback)
        {
            var director=actor&&actor.GetComponent<IEnemyActor>()?.IsEnemy==true?enemyDirector:botDirector;
            if (Authority && actor && actorIds.TryGetValue(actor, out var id)) director?.Report(id, feedback);
        }

        // This adapter translates authoritative mode state into the director's small vocabulary.
        // Queries may reveal only information allowed by the mode's public/observed rules.
        sealed partial class MatchBotFacts : IBotModeFacts
        {
            readonly MatchModeRuntime owner;
            MysteryPublicFrame publicMystery;
            readonly List<BotObjective> legacy = new List<BotObjective>();
            readonly Dictionary<string, List<BotObjective>> hazards = new Dictionary<string, List<BotObjective>>();
            readonly Dictionary<string, List<BotObjective>> mysteries = new Dictionary<string, List<BotObjective>>();
            readonly Dictionary<string, List<BotObjective>> hostileCache = new Dictionary<string, List<BotObjective>>();
            static readonly HashSet<string> queries = new HashSet<string>(StringComparer.Ordinal)
            { "hazard-refuge", "hill", "unowned-blocks", "high-ground", "midfield", "infection-search", "hostiles", "home", "enemy-bases", "enemy-flags", "own-dropped-flag", "return-enemy-flag", "return-own-flag", "friendly-carriers", "hostile-carriers", "stolen-flag-search", "mystery-travel", "mystery-guard", "mystery-attack", "mystery-evade", "mystery-revive", "mystery-bodies", "mystery-badge" };
            static readonly HashSet<string> conditions = new HashSet<string>(StringComparer.Ordinal)
            { "carrying", "infected", "own-flag-missing", "has-hostile", "carrying-enemy", "carrying-own", "scored", "own-flag-home", "block-owned", "target-dead" };
            public MatchBotFacts(MatchModeRuntime owner) { this.owner = owner; }
            public void BeginSample() { hazards.Clear(); mysteries.Clear(); hostileCache.Clear(); PruneTacticalMemory(); publicMystery=owner!=null&&owner.MysteryActive?owner.mystery.Public():null; }
            public bool SupportsQuery(string name) => name != null && (queries.Contains(name)||Array.IndexOf(tacticQueries,name)>=0);
            public bool SupportsCondition(string name) => name != null && (conditions.Contains(name)||Array.IndexOf(tacticConditions,name)>=0);
            public bool SupportsInteraction(string name) => false;
            ModeSimulation Sim => owner.simulation;
            FlagFrame Own(int team) { foreach (var f in Sim.Flags) if (f.team == team) return f; return null; }
            public bool Test(string name, BotDirectorActor actor, BotDirectorTarget target)
            {
                var self = Sim.Find(actor.id); if (self == null) return false;
                switch (name)
                {
                    case "carrying": return Sim.CarriesFlag(actor.id);
                    case "infected": return self.frame.role == "infected";
                    case "own-flag-missing": return Own(actor.team)?.atHome == false;
                    case "own-flag-home": return Own(actor.team)?.atHome == true;
                    case "carrying-enemy": foreach (var f in Sim.Flags) if (f.carrier == actor.id && f.team != actor.team) return true; return false;
                    case "carrying-own": foreach (var f in Sim.Flags) if (f.carrier == actor.id && f.team == actor.team) return true; return false;
                    case "scored": return Sim.CapturesBy(actor.id) > target.value;
                    case "has-hostile": legacy.Clear(); Hostiles(actor, legacy); return legacy.Count > 0;
                    case "target-dead": var p = Sim.Find(TargetPerson(target.id)); return p != null && p.present && (!p.frame.alive || p.frame.dying);
                    case "block-owned": return actor.team>=0&&int.TryParse(target.id,out int block)&&Sim.OwnerOf(block)==actor.team;
                    default: return TestTactic(name,actor,target);
                }
            }
            public void Query(string name, BotDirectorActor actor, List<BotDirectorTarget> results)
            {
                var self = Sim.Find(actor.id); if (self == null) return;
                var home = Own(actor.team);
                switch (name)
                {
                    case "hazard-refuge":
                        if (!hazards.TryGetValue(actor.id, out var refuge)) { refuge = new List<BotObjective>(); owner.CollectHazardTactics(owner.FindActor(actor.id), refuge); hazards[actor.id] = refuge; }
                        AddLegacy(refuge, results); return;
                    case "hill": Add(results, "hill", Sim.Hill, null, 0, owner.Rules.hillRadius); return;
                    case "home": if (home != null) Add(results, home.team.ToString(), home.home); return;
                    case "return-enemy-flag": case "return-own-flag":
                        if (home == null) return;
                        foreach (var f in Sim.Flags)
                            if (f.carrier == actor.id && (name == "return-own-flag") == (f.team == actor.team))
                            {
                                Add(results, f.team.ToString(), home.home, null, Sim.CapturesBy(actor.id));
                            }
                        return;
                    case "enemy-bases": foreach (var f in Sim.Flags) if (f.team != actor.team) Add(results, f.team.ToString(), f.home); return;
                    case "enemy-flags": case "own-dropped-flag":
                        foreach (var f in Sim.Flags)
                            if (f.carrier == null && (name == "enemy-flags" ? f.team != actor.team : f.team == actor.team && !f.atHome)) Add(results, f.team.ToString(), f.position);
                        return;
                    case "friendly-carriers": case "hostile-carriers":
                        foreach (var f in Sim.Flags)
                        {
                            if (f.carrier == null || f.carrier == actor.id) continue;
                            var p = Sim.Find(f.carrier); if (p == null || !p.frame.alive || p.frame.dying) continue;
                            bool friendly = p.frame.team == actor.team;
                            if ((name == "friendly-carriers") != friendly) continue;
                            Vector3 point = p.frame.position;
                            if (!friendly && !owner.ObserveOpponent(self.frame, p.frame, out point)) continue;
                            // Memory supplies a position, never a live enemy transform.
                            var body = friendly || owner.VisibleToActor(self.frame, p.frame) ? owner.FindActor(p.frame.id)?.gameObject : null;
                            Add(results, p.frame.id, point, body);
                        }
                        return;
                    case "stolen-flag-search":
                        if (home == null || home.atHome || home.carrier == null) return;
                        var thief = Sim.Find(home.carrier);
                        if (thief == null || thief.frame.team == actor.team || owner.ObserveOpponent(self.frame, thief.frame, out _)) return;
                        if(owner.botSightings.TryGetValue(actor.id+"/"+thief.frame.id,out var last)&&Time.time-last.seen<30)
                        {Add(results,"last-seen:"+thief.frame.id,last.position);var item=results[results.Count-1];item.utility=.5f;results[results.Count-1]=item;}
                        foreach (var f in Sim.Flags) if (f.team != actor.team) Add(results, f.team.ToString(), f.home);
                        owner.RefreshSupportGoals();
                        if(Services.World!=null){ModeSearchGeometry.Midfield(actor.position,Services.World.Center,owner.supportCandidates,false,owner.searchStages);for(int i=0;i<Mathf.Min(4,owner.searchStages.Count);i++)Add(results,PointId(owner.searchStages[i]),owner.searchStages[i]);}
                        return;
                    case "hostiles": if(!owner.RulesFor(actor.body).punchEnabled)return; legacy.Clear(); Hostiles(actor, legacy); AddLegacy(legacy, results); return;
                    case "high-ground": SafeSupports(actor,results,false); return;
                    case "midfield": case "infection-search":
                        owner.RefreshSupportGoals();
                        if (Services.World == null) return;
                        if (name == "midfield") ModeSearchGeometry.Midfield(actor.position, Services.World.Center, owner.supportCandidates, false, owner.searchStages);
                        else ModeSearchGeometry.Select(actor.position, owner.supportCandidates, false, owner.searchStages);
                        for (int i = 0; i < owner.searchStages.Count; i++) Add(results, PointId(owner.searchStages[i]), owner.searchStages[i]);
                        if(results.Count==0)SafeSupports(actor,results,true); return;
                    case "unowned-blocks":
                        foreach (var pair in owner.blockIds)
                        {
                            if (!pair.Key || Sim.OwnerOf(pair.Value) == actor.team) continue;
                            var collider = pair.Key.GetComponent<Collider>(); if (!collider) continue;
                            if(!BlockPoint(actor,collider,out var point))continue;
                            Add(results, pair.Value.ToString(), point, pair.Key.gameObject,0,.25f);
                            var target=results[results.Count-1];target.utility=Sim.ProgressOf(pair.Value)*.7f+(self.frame.standingBlock==pair.Value?.8f:0);results[results.Count-1]=target;
                            if (results.Count >= 24) break;
                        }
                        return;
                    default:
                        if(Array.IndexOf(tacticQueries,name)>=0){QueryTactic(name,actor,results);return;}
                        if(!name.StartsWith("mystery-",StringComparison.Ordinal)){QueryTactic(name,actor,results);return;}
                        if(name=="mystery-travel"){SafeSupports(actor,results,true);return;}
                        if (!mysteries.TryGetValue(actor.id, out var goals)) { goals = new List<BotObjective>(); owner.CollectMystery(actor.id, goals,publicMystery); mysteries[actor.id] = goals; }
                        var kind = name == "mystery-attack" ? BotObjectiveKind.Attack : name == "mystery-evade" ? BotObjectiveKind.Evade : BotObjectiveKind.Guard;
                        string prefix=name=="mystery-revive"?"revive:":name=="mystery-bodies"?"investigate:":name=="mystery-badge"?"badge:":"";
                        foreach (var g in goals) if (g.kind == kind && g.id.StartsWith(prefix,StringComparison.Ordinal) && results.Count < 24)
                        {Add(results,g.id,g.position,g.subject,0,g.radius);var item=results[results.Count-1];item.utility=g.priority-1;results[results.Count-1]=item;}
                        return;
                }
            }
            void Hostiles(BotDirectorActor actor, List<BotObjective> results)
            {
                if(hostileCache.TryGetValue(actor.id,out var cached)){results.AddRange(cached);return;}
                var self = Sim.Find(actor.id); if (self == null) return;
                int survivors = 0; foreach (var p in Sim.Participants) if (p.frame.alive && p.frame.role != "infected") survivors++;
                foreach (var p in Sim.CombatParticipants)
                {
                    if (p == self || !p.present || !p.frame.alive || p.frame.dying || p.frame.role == "hand" || p.frame.role == "spectator" || p.frame.role == "downed") continue;
                    bool infection = owner.Rules.mode == GameModeKind.Infection||owner.FindActor(actor.id)?.gameObject.GetComponent<IEnemyActor>()?.IsEnemy==true;
                    if (infection ? (p.frame.role == "infected") == (self.frame.role == "infected") : ModeSimulation.TeamMode(owner.Rules.mode)&&p.frame.team == actor.team) continue;
                    bool publicBlip = infection && (self.frame.role != "infected" || survivors <= 2);
                    Vector3 point = p.frame.position;
                    if (!publicBlip && !owner.ObserveOpponent(self.frame, p.frame, out point)) continue;
                    bool visible=owner.VisibleToActor(self.frame,p.frame);
                    var body = publicBlip || visible ? owner.FindActor(p.frame.id)?.gameObject : null;
                    float utility=ModeBotTactics.CombatUtility(Vector3.Distance(actor.position,point),visible,p.frame.health,visible&&Sim.CarriesFlag(p.frame.id));
                    var candidate=new BotObjective { id = p.frame.id, position = point, subject = body, priority=utility };
                    int at=0;while(at<results.Count&&(results[at].priority>utility||results[at].priority==utility&&string.CompareOrdinal(results[at].id,candidate.id)<0))at++;
                    if(at<24){results.Insert(at,candidate);if(results.Count>24)results.RemoveAt(24);}
                }
                hostileCache[actor.id]=new List<BotObjective>(results);
            }
            readonly Dictionary<Vector3Int,string> pointIds=new Dictionary<Vector3Int,string>();
            string PointId(Vector3 p)
            {
                var key=new Vector3Int(Mathf.RoundToInt(p.x*10),Mathf.RoundToInt(p.y*10),Mathf.RoundToInt(p.z*10));
                if(pointIds.TryGetValue(key,out var id))return id;
                if(pointIds.Count>=4096)pointIds.Clear();
                return pointIds[key]=key.x+":"+key.y+":"+key.z;
            }
            static void Add(List<BotDirectorTarget> results, string id, Vector3 position, GameObject body = null, float value = 0, float radius = 0)
            { results.Add(new BotDirectorTarget { id = id, position = position, subject = body, value = value, radius = radius }); }
            static void AddLegacy(List<BotObjective> goals, List<BotDirectorTarget> results)
            { foreach (var g in goals) { if(results.Count>=32)break; Add(results, g.id, g.position, g.subject,0,g.radius);var item=results[results.Count-1];item.utility=Mathf.Clamp(g.priority,-5,5);results[results.Count-1]=item; } }
        }
        bool VisibleToActor(MatchPlayerFrame observer, MatchPlayerFrame target)
        {
            var delta = target.position - observer.position;
            return (delta.sqrMagnitude < 16 || Vector3.Dot(observer.viewRotation * Vector3.forward, delta.normalized) > -.15f) && VisibleTo(observer.position, target.position);
        }
    }
}
