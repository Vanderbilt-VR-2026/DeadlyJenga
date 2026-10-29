using System;
using System.Collections.Generic;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class RoundReportBuilder
    {
        readonly List<PostGameColumn> columns = new List<PostGameColumn>();
        readonly List<Func<ParticipantAnalysis, double?>> readers = new List<Func<ParticipantAnalysis, double?>>();
        public RoundReportBuilder()
        {
            AddColumn(new PostGameColumn("alive_seconds", "Time alive", "s"), p => p.ObservedSeconds > 0 ? p.AliveSeconds : (double?)null);
            AddColumn(new PostGameColumn("average_altitude", "Avg altitude", "m"), p => Altitude(p)?.Average);
            AddColumn(new PostGameColumn("peak_altitude", "Peak altitude", "m"), p => Altitude(p)?.Maximum);
            AddColumn(new PostGameColumn("observed_seconds", "Observed", "s"), p => p.ObservedSeconds);
        }
        public void AddColumn(PostGameColumn column, Func<ParticipantAnalysis, double?> read)
        {
            if (column == null || read == null || string.IsNullOrWhiteSpace(column.Id) ||
                columns.Count >= 16 || columns.Exists(c => c.Id == column.Id)) throw new ArgumentException("Invalid result column.");
            columns.Add(column); readers.Add(read);
        }
        public PostGameReport Build(RoundAnalysis analysis, string title = "Round summary",
            Comparison<ParticipantAnalysis> order = null, Func<ParticipantAnalysis, string> name = null,
            string description = "Ordered by time alive, then average altitude. Altitude is feet above water; averages include alive time only. Sampled estimates exclude missing data.")
        {
            var sorted = new List<ParticipantAnalysis>(analysis.Participants);
            order ??= CompareSurvival;
            sorted.Sort((a, b) => { int result = order(a, b); return result != 0 ? result : string.CompareOrdinal(a.Id, b.Id); });
            var rows = new PostGameRow[sorted.Count];
            int place = 0;
            for (int i = 0; i < sorted.Count; i++)
            {
                var participant = sorted[i];
                if (i == 0 || order(sorted[i - 1], participant) != 0) place = i + 1;
                var values = new double?[columns.Count];
                for (int c = 0; c < values.Length; c++)
                {
                    values[c] = readers[c](participant);
                    if (values[c].HasValue && !SessionStats.Finite(values[c].Value)) throw new ArgumentException("Nonfinite result.");
                }
                rows[i] = new PostGameRow(participant.Id, name?.Invoke(participant) ?? participant.Id,
                    participant.IsBot, participant.ObservedSeconds > 0 ? place : 0, values);
            }
            return new PostGameReport(title, description, analysis.Duration, columns.ToArray(), rows);
        }
        static MetricAnalysis Altitude(ParticipantAnalysis p) => p.Metrics.TryGetValue(SessionStats.Altitude, out var value) ? value : null;
        static int CompareSurvival(ParticipantAnalysis a, ParticipantAnalysis b)
        {
            int observed = (b.ObservedSeconds > 0).CompareTo(a.ObservedSeconds > 0);
            if (observed != 0) return observed;
            int alive = b.AliveSeconds.CompareTo(a.AliveSeconds);
            return alive != 0 ? alive : Nullable.Compare(Altitude(b)?.Average, Altitude(a)?.Average);
        }
    }
}
