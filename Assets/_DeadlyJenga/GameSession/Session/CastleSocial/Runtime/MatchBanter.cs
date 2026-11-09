using System;

namespace DeadlyJenga.CastleSocial
{
    internal static class MatchBanter
    {
        static readonly string[][] Tags =
        {
            new[] { "Gravity, undefeated.", "The tower is shedding.", "That block quit." },
            new[] { "Very normal flooring.", "Love a moving floor.", "Solid is a suggestion." },
            new[] { "Nailed the part after jumping.", "Totally meant that.", "Put it on the reel." },
            new[] { "The forecast has hands.", "Great. Weather with a grudge.", "So much for a quiet round." },
            new[] { "New toy energy.", "Please read the manual.", "Fancy." },
            new[] { "Put it on the board.", "A rare win for planning.", "We take those." },
            new[] { "Okay. Deep breath.", "The tower can clock out too.", "Somehow, still standing." },
            new[] { "Love the commitment.", "Strong visual argument.", "I'll allow it." },
            new[] { "Gravity read the patch notes.", "That felt different.", "Well, that's new." },
            new[] { "Time is having a day.", "I preferred that take.", "Continuity is optional." }
        };

        internal static string Compose(ObservedSpeech turn, int variant, bool playful)
        {
            if (!string.IsNullOrEmpty(turn.HeardText)) return Reply(turn, variant, playful);

            string fact = FirstSentence(turn.Fact);
            const string warning = "The public disaster warning reads: ";
            if (fact.StartsWith(warning, StringComparison.Ordinal)) fact = "Heads up. " + fact.Substring(warning.Length);
            else if (fact == "The public match display says the round has started.") fact = "Round just started.";
            else if (fact == "The public match display says the round is over.") fact = "Round's over.";
            else if (fact == "I scored a confirmed flag capture.") fact = "Flag scored.";
            else if (fact == "I got a confirmed elimination.") fact = "That elimination counted.";
            if (fact.StartsWith("This happened to me: someone ", StringComparison.Ordinal))
            {
                string action = fact.Substring("This happened to me: someone ".Length);
                if (turn.Kind == SocialEventKind.Damage) fact = "I took a hit.";
                else if (turn.Kind == SocialEventKind.Collision) fact = "I got knocked back.";
                else if (turn.Kind == SocialEventKind.Effect) fact = "I " + action;
            }
            if (!playful || turn.HeardOnly || turn.Kind == SocialEventKind.Damage ||
                turn.Kind == SocialEventKind.Elimination || turn.Kind == SocialEventKind.Combat ||
                turn.Kind == SocialEventKind.NearMiss || turn.Kind == SocialEventKind.Collision)
                return fact;

            int index = TagIndex(turn.Kind);
            return index < 0 ? fact : fact + " " + Pick(Tags[index], variant);
        }

        static string Reply(ObservedSpeech turn, int variant, bool playful)
        {
            string words = turn.HeardText.ToLowerInvariant();
            if (turn.Kind == SocialEventKind.Collapse || turn.Kind == SocialEventKind.Wobble)
            {
                string direction = words.Contains("on my right") ? "your right" :
                    words.Contains("on my left") ? "your left" :
                    words.Contains("above me") ? "above you" :
                    words.Contains("below me") ? "below you" : "";
                if (direction.Length > 0)
                {
                    string where = direction.StartsWith("your", StringComparison.Ordinal) ? "on " + direction : direction;
                    if (!playful) return "How close was it " + where + "?";
                    return variant % 3 == 0 ? "Was it close " + where + " or just loud?" :
                        "How close " + where + "? Asking for my knees.";
                }
                return turn.HeardOnly ? "I heard that. Which block went?" : "Which part did you see move?";
            }

            switch (turn.Kind)
            {
                case SocialEventKind.Damage:
                case SocialEventKind.Collision:
                case SocialEventKind.NearMiss: return Pick(Checks, variant);
                case SocialEventKind.Elimination: return "Rough. Anyone still up there?";
                case SocialEventKind.Hazard: return "How close is it?";
                case SocialEventKind.Combat: return "Which way did that come from?";
                case SocialEventKind.Equipment: return words.Contains("dropped")
                    ? (playful ? "Loot delivery. No contact, please." : "Where did they drop it?")
                    : (playful ? "New toy? Please read the manual." : "What did they pick up?");
                case SocialEventKind.Objective: return words.Contains("flag") ? "Flag play! Nicely done." : "Okay, put that on the highlight reel.";
                case SocialEventKind.Success: return playful ? "The landing judge gives it a nine." : "Nice landing.";
                case SocialEventKind.Round: return words.Contains("started")
                    ? (playful ? "Okay, everyone look competent." : "Ready.")
                    : (playful ? "Did the tower win too?" : "Well played.");
                case SocialEventKind.Rewind: return playful ? "Does that count as two mistakes?" : "We're back where we started?";
                case SocialEventKind.Effect: return "How long does that last?";
                case SocialEventKind.Joke: return playful ? "I'd clap, but I need both hands." : "Fair enough.";
                default: return "What happened next?";
            }
        }

        static readonly string[] Checks = { "You okay?", "Still with us?", "Did you keep your footing?" };
        static string Pick(string[] values, int variant) => values[(int)((uint)variant % values.Length)];

        static string FirstSentence(string fact)
        {
            if (string.IsNullOrWhiteSpace(fact)) return "";
            int separator = fact.IndexOf(". ", StringComparison.Ordinal);
            return separator < 0 ? fact : fact.Substring(0, separator + 1);
        }

        static int TagIndex(SocialEventKind kind) => kind switch
        {
            SocialEventKind.Collapse => 0, SocialEventKind.Wobble => 1, SocialEventKind.Success => 2,
            SocialEventKind.Hazard => 3, SocialEventKind.Equipment => 4, SocialEventKind.Objective => 5,
            SocialEventKind.Round => 6, SocialEventKind.Joke => 7, SocialEventKind.Effect => 8,
            SocialEventKind.Rewind => 9, _ => -1
        };
    }
}
