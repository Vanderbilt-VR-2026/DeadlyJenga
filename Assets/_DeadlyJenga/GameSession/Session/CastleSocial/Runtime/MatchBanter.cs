using System;

namespace DeadlyJenga.CastleSocial
{
    internal static class MatchBanter
    {
        internal const string BotIdentity = "Yeah, I'm a bot.";

        // Keep the first callout clean. A playful bot can add an aside on a later beat.
        internal static bool CanQuip(ObservedSpeech turn, int variant, double playfulness) =>
            !turn.HeardOnly && string.IsNullOrEmpty(turn.HeardText) && playfulness > .6 &&
            (uint)variant % 5 == 2;

        internal static bool IsBotQuestion(string speech, string name)
        {
            var words = QuestionText(speech, name);
            return words is "are you a bot" or "are you ai" or "are you real"
                or "are you a real person" or "are you human" or "are you a human";
        }

        static string QuestionText(string speech, string name)
        {
            var words = speech.Trim().TrimEnd('?', '.', '!', ' ');
            if (words.StartsWith("hey ", StringComparison.OrdinalIgnoreCase)) words = words.Substring(4);
            if (!string.IsNullOrEmpty(name) && words.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                words = words.Substring(name.Length).TrimStart(',', ' ');
            return words.ToLowerInvariant();
        }

        internal static string DirectPlayerReply(string speech, string name, string fact)
        {
            if (IsBotQuestion(speech, name)) return BotIdentity;
            var words = QuestionText(speech, name);
            var lastSentence = words.Substring(words.LastIndexOf('.') + 1).TrimStart();
            if (lastSentence is "one more round" or "wanna run it back" or "want to run it back")
                return "Yeah, run it back.";
            if (fact != "This happened to me: someone took a hit.") return null;
            return words switch
            {
                "did you get hit" or "did you get hit just then" or "did you get hit there" => "Yep, felt that.",
                _ => null
            };
        }

        internal static bool IsPlayerReply(string answer, string speech)
        {
            string words = answer.Trim().TrimEnd('?', '.', '!');
            if (words.Length == 0 || answer.IndexOfAny(new[]{'[',']','*'}) >= 0) return false;
            return !ContainsWords(answer, "I'm a bot") && !ContainsWords(answer, "I am a bot") &&
                !ContainsWords(answer, "as a bot") && !ContainsWords(answer, "fragments are fine") &&
                !ContainsWords(speech, words);
        }

        internal static bool IsQuestion(string speech)
        {
            if (speech.TrimEnd().EndsWith("?", StringComparison.Ordinal)) return true;
            if (speech.StartsWith("wait,", StringComparison.OrdinalIgnoreCase)) speech = speech.Substring(5).TrimStart();
            else if (speech.StartsWith("wait ", StringComparison.OrdinalIgnoreCase)) speech = speech.Substring(5);
            foreach (var start in QuestionStarts)
                if (speech.StartsWith(start, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static readonly string[] QuestionStarts = {
            "where ", "what ", "which ", "how ", "who ", "why ", "can you ", "are you ", "did you ", "do you ", "should we "
        };

        internal static bool ContainsWords(string speech, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            for (int start = 0; start < speech.Length;)
            {
                int at = speech.IndexOf(name, start, StringComparison.OrdinalIgnoreCase);
                if (at < 0) return false;
                int end = at + name.Length;
                if ((at == 0 || !char.IsLetterOrDigit(speech[at - 1])) &&
                    (end == speech.Length || !char.IsLetterOrDigit(speech[end]))) return true;
                start = at + 1;
            }
            return false;
        }

        internal static string Compose(ObservedSpeech turn, int variant, bool playful)
        {
            if (!string.IsNullOrEmpty(turn.HeardText)) return Reply(turn, variant);

            string fact = FirstSentence(turn.Fact);
            const string warning = "The public disaster warning reads: ";
            if (fact.StartsWith(warning, StringComparison.Ordinal)) fact = "Heads up, " + fact.Substring(warning.Length);
            else if (fact.StartsWith("A tower block broke apart", StringComparison.Ordinal))
                fact = "A block just broke" + fact.Substring("A tower block broke apart".Length);
            else if (fact == "The public match display says the round has started.") fact = "Round just started.";
            else if (fact == "The public match display says the round is over.") fact = "Round's over.";
            else if (fact == "I scored a confirmed flag capture.") fact = "Flag scored.";
            else if (fact == "I got a confirmed elimination.") fact = "That elimination counted.";
            if (fact.StartsWith("This happened to me: someone ", StringComparison.Ordinal))
            {
                string action = fact.Substring("This happened to me: someone ".Length);
                if (turn.Kind == SocialEventKind.Damage) fact = Pick(SelfHits, variant);
                else if (turn.Kind == SocialEventKind.Collision) fact = Pick(SelfShoves, variant);
                else if (turn.Kind == SocialEventKind.Effect) fact = "I " + action;
            }
            if (turn.HeardOnly && turn.Kind == SocialEventKind.Collapse)
                return Pick(HeardCrashes, variant);
            if (!playful || turn.HeardOnly) return fact;
            switch (turn.Kind)
            {
                case SocialEventKind.Collapse: return fact + " Okay, I liked that block.";
                case SocialEventKind.Success: return fact + " I would've missed that.";
                case SocialEventKind.Rewind: return fact + " Wait, does that count as a do-over?";
                case SocialEventKind.Round when fact == "Round's over.": return fact + " One more? I can do worse.";
                default: return fact;
            }
        }

        static string Reply(ObservedSpeech turn, int variant)
        {
            string words = turn.HeardText.ToLowerInvariant();
            if (turn.Kind == SocialEventKind.Collapse || turn.Kind == SocialEventKind.Wobble)
            {
                string direction = ContainsWords(words, "on my right") ? "your right" :
                    ContainsWords(words, "on my left") ? "your left" :
                    ContainsWords(words, "above me") ? "above you" :
                    ContainsWords(words, "below me") ? "below you" : "";
                if (direction.Length > 0)
                {
                    string where = direction.StartsWith("your", StringComparison.Ordinal) ? "on " + direction : direction;
                    return ((uint)variant % 4) switch
                    {
                        0 or 1 => "Wait, " + where + "?",
                        2 => "How close " + where + "?",
                        _ => "You mean " + where + "?"
                    };
                }
                return turn.HeardOnly ? "Wait, which block?" : "Which one moved?";
            }

            switch (turn.Kind)
            {
                case SocialEventKind.Damage:
                case SocialEventKind.Collision:
                case SocialEventKind.NearMiss: return Pick(Checks, variant);
                case SocialEventKind.Elimination: return Pick(EliminationReplies, variant);
                case SocialEventKind.Hazard: return Pick(HazardReplies, variant);
                case SocialEventKind.Combat: return Pick(CombatReplies, variant);
                case SocialEventKind.Equipment: return words.Contains("dropped")
                    ? "Where did they drop it?"
                    : "What did they pick up?";
                case SocialEventKind.Objective: return words.Contains("flag") ? "Nice, the flag!" : "Oh, nice.";
                case SocialEventKind.Success: return "Nice landing.";
                case SocialEventKind.Round: return words.Contains("started")
                    ? "Ready."
                    : "Good game.";
                case SocialEventKind.Rewind: return "Wait, we get another go?";
                case SocialEventKind.Effect: return "How long does that last?";
                case SocialEventKind.Joke: return "Okay, that got me.";
                default: return "What happened next?";
            }
        }

        static readonly string[] SelfHits = { "I took a hit.", "That hit me.", "Yep, felt that.", "I just got tagged." };
        static readonly string[] SelfShoves = { "I got knocked back.", "That sent me backwards.", "So much for standing still.", "I just got shoved." };
        static readonly string[] HeardCrashes = { "Whoa, what was that?", "I heard a crash nearby.", "That was loud. What was it?", "Did you hear that?" };
        static readonly string[] EliminationReplies = { "Rough. Anyone still up there?", "Anyone left on that side?", "That was rough. You still up there?" };
        static readonly string[] HazardReplies = { "How close is it?", "Is it moving this way?", "Which side is it on?" };
        static readonly string[] CombatReplies = { "Which way did that come from?", "Was that close to you?", "Where was the shot?" };
        static readonly string[] Checks = { "You okay?", "Still with us?", "Did you keep your footing?" };
        static string Pick(string[] values, int variant) => values[(int)((uint)variant % values.Length)];

        static string FirstSentence(string fact)
        {
            if (string.IsNullOrWhiteSpace(fact)) return "";
            int separator = fact.IndexOf(". ", StringComparison.Ordinal);
            return separator < 0 ? fact : fact.Substring(0, separator + 1);
        }
    }
}
