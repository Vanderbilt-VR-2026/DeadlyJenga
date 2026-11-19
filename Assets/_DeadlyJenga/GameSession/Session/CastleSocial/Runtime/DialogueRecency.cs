using System;
using System.Collections.Generic;
using System.Text;

namespace DeadlyJenga.CastleSocial
{
    internal sealed class DialogueRecency
    {
        const int MatchLimit = 24;
        const int SpeakerLimit = 8;
        const float MemorySeconds = 65;
        readonly Queue<Entry> match = new Queue<Entry>();
        readonly Dictionary<string, Queue<Entry>> speakers = new Dictionary<string, Queue<Entry>>(StringComparer.Ordinal);

        readonly struct Entry
        {
            internal readonly string Key;
            internal readonly float At;
            internal Entry(string key, float at) { Key = key; At = at; }
        }

        internal string[] Snapshot(string speaker, float now, bool shared)
        {
            var keys = new List<string>(MatchLimit + SpeakerLimit);
            if (shared) CopyRecent(match, now, keys);
            if (speakers.TryGetValue(speaker, out var own)) CopyRecent(own, now, keys);
            return keys.ToArray();
        }

        internal void Remember(string speaker, string line, float now, bool shared)
        {
            string key = Normalize(line);
            if (key.Length == 0) return;
            if (!speakers.TryGetValue(speaker, out var own))
                speakers.Add(speaker, own = new Queue<Entry>());
            Add(own, key, now, SpeakerLimit);
            if (shared) Add(match, key, now, MatchLimit);
        }

        internal void Remove(string speaker) => speakers.Remove(speaker);
        internal void Clear() { match.Clear(); speakers.Clear(); }

        internal static bool Repeats(string line, string[] recent, bool paraphrases)
        {
            string key = Normalize(line);
            if (key.Length == 0) return true;
            foreach (string earlier in recent)
            {
                if (key == earlier) return true;
                if (paraphrases && NearCopy(key, earlier)) return true;
            }
            return false;
        }

        static readonly string[] FactCues = { "left", "right", "above", "below", "not", "never", "no", "didnt" };

        static bool NearCopy(string a, string b)
        {
            var words = new HashSet<string>(a.Split(' '), StringComparer.Ordinal);
            var other = new HashSet<string>(b.Split(' '), StringComparer.Ordinal);
            if (words.Count < 5 || other.Count < 5) return false;
            // A changed direction or negation can carry a different fact.
            foreach (string cue in FactCues)
                if (words.Contains(cue) != other.Contains(cue)) return false;
            int shared = 0;
            foreach (string word in words) if (other.Contains(word)) shared++;
            return shared * 10 >= (words.Count + other.Count - shared) * 8;
        }

        static string Normalize(string line)
        {
            var key = new StringBuilder();
            bool space = false;
            foreach (char c in line ?? "")
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (space && key.Length != 0) key.Append(' ');
                    key.Append(char.ToLowerInvariant(c));
                    space = false;
                }
                else if (c != '\'' && c != '’') space = true;
            }
            return key.ToString();
        }

        static void CopyRecent(Queue<Entry> source, float now, List<string> target)
        {
            foreach (var entry in source) if (now - entry.At <= MemorySeconds) target.Add(entry.Key);
        }

        static void Add(Queue<Entry> target, string key, float now, int limit)
        {
            target.Enqueue(new Entry(key, now));
            while (target.Count > limit) target.Dequeue();
        }
    }
}
