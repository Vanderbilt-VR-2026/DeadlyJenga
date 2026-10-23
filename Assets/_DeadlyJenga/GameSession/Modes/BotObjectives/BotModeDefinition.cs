using System;
using System.Collections.Generic;
using System.Globalization;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    [Serializable]
    public sealed class BotModeDefinition
    {
        public int schemaVersion = 1;
        public string id, description;
        public BotObjectiveTemplate[] objectives = Array.Empty<BotObjectiveTemplate>();

        public static BotModeDefinition FromJson(string json)
        { BotModeJson.Validate(json); return JsonUtility.FromJson<BotModeDefinition>(json); }
    }

    [Serializable]
    public sealed class BotObjectiveTemplate
    {
        public string id, query, skill = "Travel", completeWhen, interaction;
        public string[] require = Array.Empty<string>(), exclude = Array.Empty<string>();
        public int tier, minimum, maximum = 50;
        public float fraction = 1, utility = 1, radius = 1, timeout = 120, retry = 10, commitment = 3;
        public bool personal;
    }

    public struct BotDirectorActor
    {
        public string id;
        public int team;
        public Vector3 position;
        public GameObject body;
    }

    public struct BotDirectorTarget
    {
        public string id;
        public Vector3 position;
        public GameObject subject;
        public float value, radius, utility;
    }

    public interface IBotModeFacts
    {
        bool SupportsQuery(string name);
        bool SupportsCondition(string name);
        bool SupportsInteraction(string name);
        void Query(string name, BotDirectorActor actor, List<BotDirectorTarget> results);
        bool Test(string name, BotDirectorActor actor, BotDirectorTarget target);
    }

    public static class BotModeValidation
    {
        public static List<string> Validate(BotModeDefinition definition, IBotModeFacts facts)
        {
            var errors = new List<string>();
            if (definition == null) { errors.Add("Definition is null."); return errors; }
            if (definition.schemaVersion != 1) errors.Add("Unsupported schemaVersion; expected 1.");
            if (string.IsNullOrWhiteSpace(definition.id)) errors.Add("A stable mode id is required.");
            if(string.IsNullOrWhiteSpace(definition.description))errors.Add("Describe the mode's objective intent.");
            if (facts == null) { errors.Add("A facts adapter is required."); return errors; }
            if (definition.objectives == null || definition.objectives.Length == 0 || definition.objectives.Length > 32)
            { errors.Add("Use between 1 and 32 objective templates."); return errors; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in definition.objectives)
            {
                if (t == null) { errors.Add("Null objective template."); continue; }
                string label = t.id ?? "<missing id>";
                if (string.IsNullOrWhiteSpace(t.id) || t.id.Contains("/") || !ids.Add(t.id)) errors.Add(label + ": unique id without '/' required.");
                if (!facts.SupportsQuery(t.query)) errors.Add(label + ": unknown query '" + t.query + "'.");
                if (!Enum.TryParse(t.skill, false, out BotObjectiveKind kind) || !Enum.IsDefined(typeof(BotObjectiveKind), kind)) errors.Add(label + ": unknown skill '" + t.skill + "'.");
                if (t.tier < 0 || t.tier > 3) errors.Add(label + ": tier must be 0..3.");
                if (t.minimum < 0 || t.maximum < 1 || t.maximum > 100 || t.minimum > t.maximum) errors.Add(label + ": invalid participant limits.");
                if (!Finite(t.fraction) || t.fraction < 0 || t.fraction > 1 || !Finite(t.utility) || t.utility < 0 || t.utility > 10) errors.Add(label + ": invalid fraction or utility.");
                if (!Positive(t.radius, 20) || !Positive(t.timeout, 600) || !Positive(t.retry, 120) || !Positive(t.commitment, 60)) errors.Add(label + ": invalid radius or timing.");
                var required = new HashSet<string>(StringComparer.Ordinal);
                if ((t.require?.Length ?? 0) + (t.exclude?.Length ?? 0) > 32) errors.Add(label + ": too many conditions.");
                foreach (var name in t.require ?? Array.Empty<string>()) { CheckCondition(name, label, facts, errors); required.Add(name ?? ""); }
                foreach (var name in t.exclude ?? Array.Empty<string>())
                { CheckCondition(name, label, facts, errors); if (required.Contains(name ?? "")) errors.Add(label + ": condition both required and excluded: " + name); }
                if (!string.IsNullOrEmpty(t.completeWhen)) CheckCondition(t.completeWhen, label, facts, errors);
                if (!string.IsNullOrEmpty(t.interaction) && !facts.SupportsInteraction(t.interaction)) errors.Add(label + ": unsupported interaction '" + t.interaction + "'.");
                if(!string.IsNullOrEmpty(t.interaction)&&string.IsNullOrEmpty(t.completeWhen))errors.Add(label+": interactions require an authoritative completion condition.");
            }
            return errors;
        }
        static void CheckCondition(string name, string label, IBotModeFacts facts, List<string> errors)
        { if (string.IsNullOrEmpty(name) || !facts.SupportsCondition(name)) errors.Add(label + ": unknown condition '" + name + "'."); }
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Positive(float value, float limit) => Finite(value) && value > 0 && value <= limit;
    }
}
