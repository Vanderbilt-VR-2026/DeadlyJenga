using System;
using System.Collections.Generic;

namespace DeadlyJenga.Core
{
    public sealed class PostGameColumn
    {
        public string Id { get; }
        public string Title { get; }
        public string Unit { get; }
        public PostGameColumn(string id, string title, string unit)
        { Id = id; Title = title; Unit = unit; }
    }

    public sealed class PostGameRow
    {
        public string ParticipantId { get; }
        public string DisplayName { get; }
        public bool IsBot { get; }
        public int Place { get; }
        // Values match the report's column order. Null means insufficient data.
        public IReadOnlyList<double?> Values { get; }
        public PostGameRow(string participantId, string displayName, bool isBot, int place, double?[] values)
        {
            ParticipantId = participantId; DisplayName = displayName; IsBot = isBot; Place = place;
            Values = Array.AsReadOnly((double?[])values.Clone());
        }
    }

    public sealed class PostGameReport
    {
        public string Title { get; }
        public string Description { get; }
        public double DurationSeconds { get; }
        public IReadOnlyList<PostGameColumn> Columns { get; }
        public IReadOnlyList<PostGameRow> Rows { get; }
        public PostGameReport(string title, string description, double durationSeconds, PostGameColumn[] columns, PostGameRow[] rows)
        {
            Title = title; Description = description; DurationSeconds = durationSeconds;
            Columns = Array.AsReadOnly((PostGameColumn[])columns.Clone());
            Rows = Array.AsReadOnly((PostGameRow[])rows.Clone());
        }
    }
}
