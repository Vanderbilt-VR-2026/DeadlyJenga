using System;
using System.Collections.Generic;
using System.Globalization;

namespace DeadlyJenga.Modes
{
    // JsonUtility ignores unknown fields. Check the authoring contract before deserializing.
    static class BotModeJson
    {
        public static void Validate(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 131072) throw new ArgumentException("Bot JSON must be nonempty and at most 128 KiB.");
            var reader = new Reader(json); reader.Object(false); reader.Space();
            if (reader.Index != json.Length) throw new ArgumentException("Trailing content in bot definition.");
        }
        sealed class Reader
        {
            readonly string text;
            public int Index;
            public Reader(string text) { this.text = text; }
            public void Space() { while (Index < text.Length && char.IsWhiteSpace(text[Index])) Index++; }
            bool Take(char c) { Space(); if (Index < text.Length && text[Index] == c) { Index++; return true; } return false; }
            void Expect(char c) { if (!Take(c)) throw new ArgumentException("Expected '" + c + "' at " + Index); }
            string String()
            {
                Expect('"'); var value = new System.Text.StringBuilder();
                while (Index < text.Length)
                {
                    char c = text[Index++]; if (c == '"') return value.ToString();
                    if (c < 32) throw new ArgumentException("Control character in bot JSON string.");
                    if (c == '\\')
                    {
                        if (Index >= text.Length) break;
                        char escaped = text[Index++];
                        if (escaped == 'u')
                        {
                            if (Index + 4 > text.Length || !ushort.TryParse(text.Substring(Index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code)) throw new ArgumentException("Invalid Unicode escape.");
                            value.Append((char)code); Index += 4; continue;
                        }
                        if ("\"\\/bfnrt".IndexOf(escaped) < 0) throw new ArgumentException("Invalid JSON escape.");
                        // Field names may not hide behind escapes; values are decoded by JsonUtility.
                        value.Append(escaped); continue;
                    }
                    value.Append(c);
                }
                throw new ArgumentException("Unterminated JSON string.");
            }
            public void Object(bool template)
            {
                Expect('{'); var keys = new HashSet<string>(StringComparer.Ordinal);
                if (Take('}')) throw new ArgumentException("Empty bot definition object.");
                do
                {
                    string key = String(); if (!keys.Add(key)) throw new ArgumentException("Duplicate field: " + key); Expect(':');
                    if (!template)
                    {
                        switch (key)
                        {
                            case "schemaVersion": Number(true); break;
                            case "id": case "description": String(); break;
                            case "objectives":
                                Expect('['); if (!Take(']')) { do { Object(true); } while (Take(',')); Expect(']'); } break;
                            default: throw new ArgumentException("Unknown mode field: " + key);
                        }
                    }
                    else
                    {
                        switch (key)
                        {
                            case "id": case "query": case "skill": case "completeWhen": case "interaction": String(); break;
                            case "require": case "exclude": Expect('['); if (!Take(']')) { do { String(); } while (Take(',')); Expect(']'); } break;
                            case "tier": case "minimum": case "maximum": Number(true); break;
                            case "fraction": case "utility": case "radius": case "timeout": case "retry": case "commitment": Number(false); break;
                            case "personal":
                                Space(); if (text.Substring(Index).StartsWith("true", StringComparison.Ordinal)) Index += 4;
                                else if (text.Substring(Index).StartsWith("false", StringComparison.Ordinal)) Index += 5;
                                else throw new ArgumentException("personal must be true or false."); break;
                            default: throw new ArgumentException("Unknown objective field: " + key);
                        }
                    }
                } while (Take(','));
                Expect('}');
                if (!keys.Contains("id") || template && (!keys.Contains("query") || !keys.Contains("skill")) || !template && (!keys.Contains("schemaVersion") || !keys.Contains("objectives")))
                    throw new ArgumentException("Missing required bot definition fields.");
            }
            void Number(bool integer)
            {
                Space(); int start = Index;
                if (Index < text.Length && text[Index] == '-') Index++;
                int digits = Index;
                while (Index < text.Length && text[Index] >= '0' && text[Index] <= '9') Index++;
                if (Index == digits || Index - digits > 1 && text[digits] == '0') throw new ArgumentException("Invalid JSON number.");
                if (Index < text.Length && text[Index] == '.')
                { Index++; int fraction = Index; while (Index < text.Length && char.IsDigit(text[Index])) Index++; if (fraction == Index) throw new ArgumentException("Invalid fraction."); }
                if (Index < text.Length && (text[Index] == 'e' || text[Index] == 'E'))
                { Index++; if (Index < text.Length && (text[Index] == '+' || text[Index] == '-')) Index++; int exponent = Index; while (Index < text.Length && char.IsDigit(text[Index])) Index++; if (exponent == Index) throw new ArgumentException("Invalid exponent."); }
                string number = text.Substring(start, Index - start);
                if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || double.IsInfinity(value) || integer && (!int.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _)))
                    throw new ArgumentException("Invalid numeric field: " + number);
            }
        }
    }
}
