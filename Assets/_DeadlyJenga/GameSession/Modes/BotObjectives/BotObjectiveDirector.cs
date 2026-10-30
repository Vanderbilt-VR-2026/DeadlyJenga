using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public enum BotAssignmentOutcome { Assigned, Running, Arrived, Succeeded, Failed, Cancelled }

    public struct BotAssignmentEvent
    {
        public float time;
        public string actor, objective, reason;
        public long assignment;
        public BotAssignmentOutcome outcome;
    }

    // Pure allocation and lifecycle code. No character classes or mode-specific branches.
    public sealed class BotObjectiveDirector
    {
        sealed class Candidate
        {
            public BotDirectorActor actor;
            public BotDirectorTarget target;
            public BotObjectiveTemplate template;
            public BotObjectiveKind kind;
            public string key, group;
            public float score;
            public int desired;
            public Candidate Snapshot() => (Candidate)MemberwiseClone();
            public void Update(Candidate c)
            {actor=c.actor;target=c.target;template=c.template;kind=c.kind;key=c.key;group=c.group;score=c.score;desired=c.desired;}
        }
        sealed class Assignment
        {
            public Candidate candidate;
            public BotDirectorTarget initialTarget;
            public long token;
            public float started, feedbackAt, lastArrivedAt, blockedAt = -1;
            public bool arrived;
        }
        struct Failure { public float until, touched; public int count; }
        readonly BotModeDefinition definition;
        readonly IBotModeFacts facts;
        readonly Dictionary<string, Assignment> assignments = new Dictionary<string, Assignment>(StringComparer.Ordinal);
        readonly Dictionary<string, Failure> failures = new Dictionary<string, Failure>(StringComparer.Ordinal);
        readonly Dictionary<string, Candidate> chosen = new Dictionary<string, Candidate>(StringComparer.Ordinal);
        readonly Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly Dictionary<int, int> populations = new Dictionary<int, int>();
        readonly HashSet<string> actors = new HashSet<string>(StringComparer.Ordinal);
        readonly List<Candidate> candidates = new List<Candidate>();
        readonly List<Candidate> candidatePool = new List<Candidate>();
        readonly Dictionary<BotObjectiveTemplate, BotObjectiveKind> skills = new Dictionary<BotObjectiveTemplate, BotObjectiveKind>();
        readonly HashSet<string> targetIds = new HashSet<string>(StringComparer.Ordinal);
        readonly List<BotDirectorTarget> targets = new List<BotDirectorTarget>(32);
        readonly List<string> remove = new List<string>();
        readonly Queue<BotAssignmentEvent> history = new Queue<BotAssignmentEvent>();
        static long sequence;
        float now;
        public int ActiveCount => assignments.Count;
        public int CandidateCount => candidates.Count;
        public IEnumerable<BotAssignmentEvent> History => history;
        public string ModeId => definition.id;
        public double LastMilliseconds { get; private set; }
        public double PeakMilliseconds { get; private set; }

        public BotObjectiveDirector(BotModeDefinition definition, IBotModeFacts facts)
        {
            var errors = BotModeValidation.Validate(definition, facts);
            if (errors.Count > 0) throw new ArgumentException(string.Join("\n", errors));
            // Own the configuration: editing a caller's definition cannot bypass validation.
            this.definition = BotModeDefinition.FromJson(JsonUtility.ToJson(definition));
            this.facts = facts;
            foreach(var t in this.definition.objectives)skills[t]=(BotObjectiveKind)Enum.Parse(typeof(BotObjectiveKind),t.skill);
        }

        public void Reset(float time, string reason)
        {
            now = time;
            foreach (var a in assignments.Values) Record(a, BotAssignmentOutcome.Cancelled, reason);
            assignments.Clear(); failures.Clear(); candidates.Clear(); chosen.Clear(); counts.Clear();
            // Tokens deliberately do not reset; stale feedback cannot match a new round.
        }

        public void Tick(float time, IReadOnlyList<BotDirectorActor> members)
        {
            long tickStarted=System.Diagnostics.Stopwatch.GetTimestamp();
            if (!BotModeValidation.Finite(time)) throw new ArgumentException("Nonfinite director clock.");
            if(members==null||members.Count>100)throw new ArgumentException("Director supports at most 100 eligible bots.");
            if (time < now) Reset(time, "Clock reset");
            now = time; actors.Clear(); populations.Clear();
            foreach (var actor in members)
            {
                if (string.IsNullOrEmpty(actor.id) || !BotModeValidation.Finite(actor.position) || !actors.Add(actor.id)) throw new ArgumentException("Invalid or duplicate bot actor.");
                populations.TryGetValue(actor.team, out int n); populations[actor.team] = n + 1;
            }
            remove.Clear();
            foreach (var pair in assignments)
            {
                var a = pair.Value;
                if (!actors.Contains(pair.Key)) { Record(a, BotAssignmentOutcome.Cancelled, "Actor unavailable"); remove.Add(pair.Key); continue; }
                BotDirectorActor current = default;
                foreach (var actor in members) if (actor.id == pair.Key) { current = actor; break; }
                if (current.team != a.candidate.actor.team) { Record(a, BotAssignmentOutcome.Cancelled, "Team changed"); remove.Add(pair.Key); continue; }
                if (!string.IsNullOrEmpty(a.candidate.template.completeWhen) && facts.Test(a.candidate.template.completeWhen, current, a.initialTarget))
                { Record(a, BotAssignmentOutcome.Succeeded, "Mode completion condition"); remove.Add(pair.Key); continue; }
                if (now - Mathf.Max(a.started,a.lastArrivedAt) >= a.candidate.template.timeout)
                { Fail(a, "Objective deadline"); remove.Add(pair.Key); }
            }
            foreach (var id in remove) assignments.Remove(id);
            PruneFailures(); candidates.Clear();
            foreach (var actor in members)
            {
                int emitted = 0;
                foreach (var template in definition.objectives)
                {
                    targets.Clear(); facts.Query(template.query, actor, targets);
                    if (targets.Count > 32) throw new InvalidOperationException(template.query + " exceeded 32 targets; bound and rank the query.");
                    targetIds.Clear();
                    foreach (var target in targets)
                    {
                        if (string.IsNullOrEmpty(target.id) || !BotModeValidation.Finite(target.position) || !BotModeValidation.Finite(target.value) || !BotModeValidation.Finite(target.utility) || Mathf.Abs(target.utility)>10 || !BotModeValidation.Finite(target.radius) || target.radius < 0 || !targetIds.Add(target.id)) throw new InvalidOperationException(template.query + " returned invalid or duplicate targets.");
                        if (!Eligible(template, actor, target)) continue;
                        string key = template.id + "/" + target.id;
                        string failureKey = actor.id + "/" + key;
                        if (failures.TryGetValue(failureKey, out var failure) && now < failure.until) continue;
                        float score = template.utility + target.utility - Mathf.Min(3, Vector3.Distance(actor.position, target.position) * .015f);
                        if (assignments.TryGetValue(actor.id, out var previous) && previous.candidate.key == key)
                            score += now - previous.started < template.commitment ? .6f : .15f;
                        string group = actor.team + "/" + key + (template.personal ? "/" + actor.id : "");
                        if(candidates.Count==candidatePool.Count)candidatePool.Add(new Candidate());
                        var candidate=candidatePool[candidates.Count];
                        candidate.actor=actor;candidate.target=target;candidate.template=template;candidate.kind=skills[template];candidate.key=key;candidate.group=group;candidate.score=score;
                        candidate.desired=Mathf.Clamp(Mathf.CeilToInt(populations[actor.team]*template.fraction),template.minimum,template.maximum);
                        candidates.Add(candidate);
                        if (++emitted >= 128) throw new InvalidOperationException("More than 127 eligible candidates for " + actor.id + "; narrow the definition.");
                    }
                }
            }
            candidates.Sort((a, b) =>
            {
                int c = b.template.tier.CompareTo(a.template.tier); if (c != 0) return c;
                c = b.score.CompareTo(a.score); if (c != 0) return c;
                c = string.CompareOrdinal(a.actor.id, b.actor.id); return c != 0 ? c : string.CompareOrdinal(a.key, b.key);
            });
            chosen.Clear(); counts.Clear();
            // Mandatory personal tasks and emergencies outrank lower-tier staffing needs.
            for (int tier = 3; tier >= 0; tier--)
                for (int pass = 0; pass < 3; pass++)
                    foreach (var c in candidates)
                    {
                        if (c.template.tier != tier || chosen.ContainsKey(c.actor.id)) continue;
                        counts.TryGetValue(c.group, out int used);
                        int limit = pass == 0 ? c.template.minimum : pass == 1 ? c.desired : c.template.maximum;
                        if (used >= limit) continue;
                        chosen.Add(c.actor.id, c); counts[c.group] = used + 1;
                    }
            remove.Clear();
            foreach (var pair in assignments)
                if (!chosen.TryGetValue(pair.Key, out var selected) || selected.key != pair.Value.candidate.key)
                { Record(pair.Value, BotAssignmentOutcome.Cancelled, "Opportunity ended or reassigned"); remove.Add(pair.Key); }
            foreach (var id in remove) assignments.Remove(id);
            foreach (var pair in chosen)
            {
                if (assignments.TryGetValue(pair.Key, out var a)) { a.candidate.Update(pair.Value); continue; }
                a = new Assignment { candidate = pair.Value.Snapshot(), initialTarget = pair.Value.target, token = System.Threading.Interlocked.Increment(ref sequence), started = now, feedbackAt = now };
                assignments.Add(pair.Key, a); Record(a, BotAssignmentOutcome.Assigned, "Capacity and utility allocation");
            }
            LastMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-tickStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency;
            PeakMilliseconds=Math.Max(PeakMilliseconds,LastMilliseconds);
        }

        bool Eligible(BotObjectiveTemplate t, BotDirectorActor actor, BotDirectorTarget target)
        {
            foreach (var name in t.require ?? Array.Empty<string>()) if (!facts.Test(name, actor, target)) return false;
            foreach (var name in t.exclude ?? Array.Empty<string>()) if (facts.Test(name, actor, target)) return false;
            return string.IsNullOrEmpty(t.completeWhen) || !facts.Test(t.completeWhen, actor, target);
        }

        public bool TryGet(string actor, out BotObjective objective, out BotExecutionPolicy policy)
        {
            objective = default; policy = default;
            if(string.IsNullOrEmpty(actor))return false;
            if (!assignments.TryGetValue(actor, out var a)) return false;
            var c = a.candidate;
            objective = new BotObjective { id = c.key, kind = c.kind, position = c.target.position,
                subject = c.target.subject, radius = c.target.radius > 0 ? c.target.radius : c.template.radius, priority = c.template.utility + c.template.tier * 10, expiresAt = now + 2 };
            policy = new BotExecutionPolicy { assignment = a.token, interaction = c.template.interaction };
            return true;
        }

        public void Report(string actor, BotExecutionFeedback feedback)
        {
            if(string.IsNullOrEmpty(actor))return;
            if (!assignments.TryGetValue(actor, out var a) || a.token != feedback.assignment || a.candidate.key != feedback.objectiveId) return;
            if (!Enum.IsDefined(typeof(BotExecutionStatus), feedback.status) || !BotModeValidation.Finite(feedback.position)) return;
            if (feedback.status == BotExecutionStatus.Interrupted)
            { Record(a, BotAssignmentOutcome.Cancelled, feedback.reason); assignments.Remove(actor); return; }
            if (feedback.status == BotExecutionStatus.Failed || feedback.status == BotExecutionStatus.Unavailable)
            { Fail(a, feedback.reason); assignments.Remove(actor); return; }
            if (feedback.status == BotExecutionStatus.Blocked)
            {
                if (a.blockedAt < 0) a.blockedAt = now;
                if (now - a.blockedAt >= 5) { Fail(a, "Sustained blockage: " + feedback.reason); assignments.Remove(actor); }
                return;
            }
            a.blockedAt = -1;
            if (feedback.status == BotExecutionStatus.Arrived)
            {
                if(string.IsNullOrEmpty(a.candidate.template.completeWhen))a.lastArrivedAt=now;
                if (!a.arrived) { a.arrived = true; Record(a, BotAssignmentOutcome.Arrived, "Within execution radius; mode still owns success"); }
            }
            else if (now - a.feedbackAt >= 5) { Record(a, BotAssignmentOutcome.Running, feedback.reason); a.feedbackAt = now; }
        }
        void Fail(Assignment a, string reason)
        {
            string key = a.candidate.actor.id + "/" + a.candidate.key;
            failures.TryGetValue(key, out var f); f.count = Mathf.Min(4, f.count + 1); f.touched = now;
            f.until = now + Mathf.Min(120, a.candidate.template.retry * f.count); failures[key] = f;
            Record(a, BotAssignmentOutcome.Failed, reason);
        }
        void PruneFailures()
        {
            remove.Clear(); foreach (var pair in failures) if (now - pair.Value.touched > 180) remove.Add(pair.Key);
            foreach (var key in remove) failures.Remove(key);
            if (failures.Count > 4096) failures.Clear();
        }
        void Record(Assignment a, BotAssignmentOutcome outcome, string reason)
        {
            if (history.Count >= 256) history.Dequeue();
            history.Enqueue(new BotAssignmentEvent { time = now, actor = a.candidate.actor.id, objective = a.candidate.key, assignment = a.token, outcome = outcome, reason = reason });
        }
    }
}
