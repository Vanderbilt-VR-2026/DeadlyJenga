using System;

namespace DeadlyJenga.CastleSocial
{
    internal static class GroundedDialogue
    {
        internal static bool Accepts(ObservedSpeech turn, string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 140 || text.IndexOf(':') >= 0) return false;
            string line = text.ToLowerInvariant();
            string previous = turn.HeardText?.ToLowerInvariant();
            if (previous != null && (line == previous || line.Contains(previous))) return false;
            if (turn.HeardOnly && (line.Contains("i saw") || line.Contains("i watched") || line.Contains("in front of me")
                || !line.Contains("hear") && !line.Contains("heard") && !line.Contains("?"))) return false;
            if (turn.Kind != SocialEventKind.Collapse) return true;

            // A block-break signal carries a location and no responsible actor.
            if (line.Contains("pulled") || line.Contains("caused") || line.Contains("pushed") || line.Contains("sabotage"))
                return false;
            if (previous != null)
                return line.Contains("block") || line.Contains("crash") || line.Contains("fall")
                    || line.Contains("left") || line.Contains("right") || line.Contains("above") || line.Contains("below");
            if (turn.HeardOnly) return line.Contains("crash") || line.Contains("blocks");
            string fact = turn.Fact.ToLowerInvariant();
            foreach (string direction in new[] { "left", "right", "above", "below" })
                if (fact.Contains(direction) && !line.Contains(direction)) return false;
            return (line.Contains("block") || line.Contains("tower")) &&
                (line.Contains("broke") || line.Contains("break") || line.Contains("crack") || line.Contains("fall") ||
                 line.Contains("crash") || line.Contains("collapse") || line.Contains("moving"));
        }
    }
}
