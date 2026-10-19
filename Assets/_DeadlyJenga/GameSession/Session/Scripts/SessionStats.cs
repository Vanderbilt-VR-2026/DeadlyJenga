using System;
using System.Collections.Generic;

namespace DeadlyJenga.CastlePrototype
{
    public enum StatKind : byte { Number, Boolean }

    public sealed class StatDefinition
    {
        public string Id { get; }
        public string Unit { get; }
        public StatKind Kind { get; }
        public StatDefinition(string id, StatKind kind, string unit = "")
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || unit == null || unit.Length > 32 ||
                !Enum.IsDefined(typeof(StatKind), kind)) throw new ArgumentException("Invalid stat definition.");
            Id = id; Kind = kind; Unit = unit;
        }
    }

    public readonly struct SurvivorStatSample
    {
        public readonly string Id;
        public readonly bool Alive, IsBot;
        public readonly double Altitude;
        public SurvivorStatSample(string id, bool alive, bool isBot, double altitude)
        { Id = id; Alive = alive; IsBot = isBot; Altitude = altitude; }
    }

    public sealed class ParticipantStats
    {
        public string Id { get; }
        public bool IsBot { get; }
        public IReadOnlyList<double> Values { get; }
        public ParticipantStats(string id, bool isBot, double[] values)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128) throw new ArgumentException("Invalid participant ID.");
            Id = id; IsBot = isBot;
            Values = Array.AsReadOnly((double[])values.Clone());
        }
    }

    public sealed class SessionStatsSnapshot
    {
        public string DefiningMetricId { get; }
        public int Revision { get; }
        public double SampledAt { get; }
        public IReadOnlyList<StatDefinition> Definitions { get; }
        public IReadOnlyList<ParticipantStats> Participants { get; }
        public SessionStatsSnapshot(int revision, double sampledAt, StatDefinition[] definitions, ParticipantStats[] participants, string definingMetricId = "alive")
        {
            Revision = revision; SampledAt = sampledAt; DefiningMetricId = definingMetricId;
            Definitions = Array.AsReadOnly((StatDefinition[])definitions.Clone());
            Participants = Array.AsReadOnly((ParticipantStats[])participants.Clone());
        }
        public bool TryGet(string participantId, string metricId, out double value)
        {
            value = 0;
            for (int i = 0; i < Definitions.Count; i++)
                if (Definitions[i].Id == metricId)
                    foreach (var participant in Participants)
                        if (participant.Id == participantId) { value = participant.Values[i]; return true; }
            return false;
        }
        public bool TryGetAlive(string participantId, out bool alive)
        {
            bool found = TryGet(participantId, SessionStats.Alive, out double value);
            alive = found && value == 1;
            return found;
        }
    }

    // Definitions and readers are shared by every mode; no scoring rules live here.
    public sealed class SessionStats
    {
        public string DefiningMetricId {get;set;} = Alive;
        public const string Alive = "alive", Altitude = "altitude";
        public const int MaxMetrics = 32, MaxParticipants = 256;
        readonly List<StatDefinition> definitions = new List<StatDefinition>();
        readonly List<Func<SurvivorStatSample, double>> readers = new List<Func<SurvivorStatSample, double>>();
        int revision;
        public SessionStats()
        {
            Register(new StatDefinition(Alive, StatKind.Boolean), s => s.Alive ? 1 : 0);
            Register(new StatDefinition(Altitude, StatKind.Number, "m above water"), s => s.Altitude);
        }
        public void Register(StatDefinition definition, Func<SurvivorStatSample, double> read)
        {
            if (definition == null || read == null) throw new ArgumentNullException();
            if (definitions.Count >= MaxMetrics || definitions.Exists(d => d.Id == definition.Id))
                throw new ArgumentException("Duplicate stat ID or too many metrics.");
            definitions.Add(definition); readers.Add(read);
        }
        public SessionStatsSnapshot Sample(IReadOnlyList<SurvivorStatSample> survivors, double time)
        {
            if (!Finite(time) || time < 0 || survivors.Count > MaxParticipants) throw new ArgumentException("Invalid stat sample.");
            var ids = new HashSet<string>();
            var rows = new ParticipantStats[survivors.Count];
            for (int p = 0; p < survivors.Count; p++)
            {
                var source = survivors[p];
                if (!ids.Add(source.Id)) throw new ArgumentException("Duplicate participant ID.");
                var values = new double[definitions.Count];
                for (int m = 0; m < values.Length; m++)
                {
                    values[m] = readers[m](source);
                    ValidateValue(definitions[m], values[m]);
                }
                rows[p] = new ParticipantStats(source.Id, source.IsBot, values);
            }
            return new SessionStatsSnapshot(++revision, time, definitions.ToArray(), rows, DefiningMetricId);
        }
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        internal static void ValidateValue(StatDefinition definition, double value)
        {
            if (!Finite(value) || definition.Kind == StatKind.Boolean && value != 0 && value != 1)
                throw new ArgumentException("Invalid metric value: " + definition.Id);
        }
    }
}
