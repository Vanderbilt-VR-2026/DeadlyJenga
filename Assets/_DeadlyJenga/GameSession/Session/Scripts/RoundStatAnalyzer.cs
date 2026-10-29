using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class MetricAnalysis
    {
        public StatDefinition Definition { get; }
        public double ObservedSeconds { get; }
        public double TrueSeconds { get; }
        public double? Average { get; }
        public double? Minimum { get; }
        public double? Maximum { get; }
        internal MetricAnalysis(StatDefinition definition, double seconds, double trueSeconds,
            double integral, double? minimum, double? maximum)
        {
            Definition = definition; ObservedSeconds = seconds; TrueSeconds = trueSeconds;
            Average = seconds > 0 ? integral / seconds : (double?)null;
            Minimum = minimum; Maximum = maximum;
        }
    }

    public sealed class ParticipantAnalysis
    {
        public string Id { get; }
        public bool IsBot { get; }
        public double ObservedSeconds { get; }
        public IReadOnlyDictionary<string, MetricAnalysis> Metrics { get; }
        public double AliveSeconds => Metrics.TryGetValue(SessionStats.Alive, out var value) ? value.TrueSeconds : 0;
        internal ParticipantAnalysis(string id, bool bot, double seconds, Dictionary<string, MetricAnalysis> metrics)
        { Id = id; IsBot = bot; ObservedSeconds = seconds; Metrics = new ReadOnlyDictionary<string, MetricAnalysis>(metrics); }
    }

    public sealed class RoundAnalysis
    {
        public double StartedAt { get; }
        public double EndedAt { get; }
        public double Duration => EndedAt - StartedAt;
        public IReadOnlyList<ParticipantAnalysis> Participants { get; }
        internal RoundAnalysis(double start, double end, ParticipantAnalysis[] participants)
        { StartedAt = start; EndedAt = end; Participants = Array.AsReadOnly(participants); }
    }

    // Integrates time between samples; numeric stats are measured while alive.
    public sealed class RoundStatAnalyzer
    {
        public const double MaximumSampleGap = 1;
        sealed class Metric
        {
            public StatDefinition definition;
            public double seconds, trueSeconds, integral;
            public double? minimum, maximum;
            public void Point(double value)
            {
                minimum = minimum.HasValue ? Math.Min(minimum.Value, value) : value;
                maximum = maximum.HasValue ? Math.Max(maximum.Value, value) : value;
            }
            public MetricAnalysis Finish() => new MetricAnalysis(definition, seconds, trueSeconds, integral, minimum, maximum);
        }
        sealed class Participant
        {
            public bool bot;
            public double seconds;
            public readonly Dictionary<string, Metric> metrics = new Dictionary<string, Metric>();
        }
        readonly Dictionary<string, Participant> participants = new Dictionary<string, Participant>();
        readonly Dictionary<string, ParticipantStats> previousRows = new Dictionary<string, ParticipantStats>();
        readonly Dictionary<string, int> previousColumns = new Dictionary<string, int>();
        readonly double startedAt;
        SessionStatsSnapshot previous;
        RoundAnalysis finished;

        public RoundStatAnalyzer(double start)
        {
            if (!SessionStats.Finite(start) || start < 0) throw new ArgumentException("Invalid round start.");
            startedAt = start;
        }

        public bool Observe(SessionStatsSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (finished != null || snapshot.SampledAt < startedAt || previous != null &&
                (snapshot.Revision <= previous.Revision || snapshot.SampledAt < previous.SampledAt)) return false;
            double dt = previous == null ? 0 : snapshot.SampledAt - previous.SampledAt;
            if (dt > MaximumSampleGap) dt = 0;
            foreach (var row in snapshot.Participants)
            {
                if (!participants.TryGetValue(row.Id, out var participant))
                {
                    if (participants.Count >= SessionStats.MaxParticipants) continue;
                    participants.Add(row.Id, participant = new Participant { bot = row.IsBot });
                }
                bool continuous = previousRows.TryGetValue(row.Id, out var last) && last.IsBot == row.IsBot;
                bool alive = snapshot.TryGetAlive(row.Id, out bool currentAlive) && currentAlive;
                bool previouslyAlive = continuous && previous.TryGetAlive(row.Id, out bool lastAlive) && lastAlive;
                if (continuous) participant.seconds += dt;
                for (int i = 0; i < snapshot.Definitions.Count; i++)
                {
                    var definition = snapshot.Definitions[i];
                    if (!participant.metrics.TryGetValue(definition.Id, out var metric))
                    {
                        if (participant.metrics.Count >= SessionStats.MaxMetrics) continue;
                        participant.metrics.Add(definition.Id, metric = new Metric { definition = definition });
                    }
                    if (metric.definition.Kind != definition.Kind || metric.definition.Unit != definition.Unit)
                        throw new ArgumentException("Metric definition changed during a round: " + definition.Id);
                    bool boolean = definition.Kind == StatKind.Boolean;
                    if (boolean || alive) metric.Point(row.Values[i]);
                    if (continuous && dt > 0 && (boolean || previouslyAlive) && previousColumns.TryGetValue(definition.Id, out int column))
                    {
                        double value = last.Values[column];
                        metric.seconds += dt;
                        metric.integral += value * dt;
                        if (boolean && value == 1) metric.trueSeconds += dt;
                    }
                }
            }
            previousRows.Clear(); previousColumns.Clear();
            foreach (var row in snapshot.Participants) previousRows[row.Id] = row;
            for (int i = 0; i < snapshot.Definitions.Count; i++) previousColumns[snapshot.Definitions[i].Id] = i;
            previous = snapshot;
            return true;
        }

        public RoundAnalysis Finish(double endedAt)
        {
            if (finished != null) return finished;
            if (!SessionStats.Finite(endedAt) || endedAt < startedAt || previous != null && endedAt < previous.SampledAt)
                throw new ArgumentException("Invalid round end.");
            var result = new List<ParticipantAnalysis>(participants.Count);
            foreach (var pair in participants)
            {
                var metrics = new Dictionary<string, MetricAnalysis>();
                foreach (var metric in pair.Value.metrics) metrics.Add(metric.Key, metric.Value.Finish());
                result.Add(new ParticipantAnalysis(pair.Key, pair.Value.bot, pair.Value.seconds, metrics));
            }
            result.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return finished = new RoundAnalysis(startedAt, endedAt, result.ToArray());
        }
    }
}
