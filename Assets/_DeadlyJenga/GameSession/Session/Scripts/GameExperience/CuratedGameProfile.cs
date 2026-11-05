using System;
using System.Collections.Generic;

namespace DeadlyJenga.CastlePrototype
{
    [Serializable]
    public sealed class CuratedHazardParameter { public string id = ""; public float value; }
    [Serializable]
    public sealed class CuratedHazard
    {
        public string id = "";
        public CuratedHazardParameter[] parameters = Array.Empty<CuratedHazardParameter>();
    }
    [Serializable]
    public sealed class CuratedGameProfile
    {
        public string id = "", mapId = "", modeId = "", layoutSignature = "", evidence = "";
        public bool enabled, productionApproved;
        public int minimumPopulation, maximumPopulation, targetPopulation, hands;
        public CuratedHazard[] hazards = Array.Empty<CuratedHazard>();
    }

    public static class CuratedGamePolicy
    {
        public static bool CanUseDevelopmentCommands(bool demo, bool worldAuthority, bool localPermission)
            => demo && worldAuthority && localPermission;

        public static bool CanRequestDevelopmentCommands(bool demo, bool member, bool leader, bool shared)
            => demo && member && (leader || shared);

        // Catalog compatibility and the exact layout signature are checked again before launch.
        public static bool Eligible(CuratedGameProfile p, string actualLayoutSignature, int humans, bool prototype,
            Func<string, string, bool> supports, out string error)
        {
            error = "Profile is unavailable.";
            if (p == null || !p.enabled || (!p.productionApproved && !prototype)) return false;
            if (string.IsNullOrEmpty(p.id) || p.id.Length > 128 || string.IsNullOrEmpty(p.mapId) ||
                string.IsNullOrEmpty(p.modeId) || p.modeId == "demo" || supports == null || !supports(p.mapId, p.modeId))
            { error = "Map and mode are not a curated compatible pair."; return false; }
            if (string.IsNullOrEmpty(p.layoutSignature) || p.layoutSignature != actualLayoutSignature || string.IsNullOrEmpty(p.evidence))
            { error = "Profile needs evidence for this exact map layout."; return false; }
            if (p.minimumPopulation < 1 || p.maximumPopulation > 100 || p.maximumPopulation < p.minimumPopulation ||
                p.targetPopulation < p.minimumPopulation || p.targetPopulation > p.maximumPopulation || p.hands < 0 || p.hands > 10)
            { error = "Profile population bounds are invalid."; return false; }
            if (humans < 1 || humans > p.maximumPopulation || humans > 10 || humans + p.hands > p.maximumPopulation)
            { error = "Current human roster does not fit this profile."; return false; }
            if (p.hazards == null || p.hazards.Length > 32) { error = "Invalid hazard allowlist."; return false; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var h in p.hazards)
            {
                if (h == null || string.IsNullOrEmpty(h.id) || !ids.Add(h.id) || h.parameters == null || h.parameters.Length > 64)
                { error = "Invalid or duplicate hazard."; return false; }
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var v in h.parameters)
                    if (v == null || string.IsNullOrEmpty(v.id) || !names.Add(v.id) || float.IsNaN(v.value) || float.IsInfinity(v.value))
                    { error = "Invalid or duplicate hazard parameter."; return false; }
            }
            error = ""; return true;
        }

        public static int BotCount(CuratedGameProfile p, int humans)
        {
            if (p == null || humans < 1 || humans > p.maximumPopulation || p.hands < 0 || humans + p.hands > p.maximumPopulation) return 0;
            return Math.Max(0, Math.Min(p.maximumPopulation, p.targetPopulation) - humans - p.hands);
        }

        // Never silently clamp or fall back to a prefab default when the schema has changed.
        public static bool ResolveHazard(CuratedHazard h, string[] ids, float[] minimum, float[] maximum, bool[] whole, out float[] values)
        {
            values = Array.Empty<float>();
            if (h == null || h.parameters == null || ids == null || minimum == null || maximum == null || whole == null ||
                ids.Length != minimum.Length || ids.Length != maximum.Length || ids.Length != whole.Length || ids.Length != h.parameters.Length || ids.Length > 64) return false;
            var supplied = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var p in h.parameters)
            {
                if (p == null || string.IsNullOrEmpty(p.id) || float.IsNaN(p.value) || float.IsInfinity(p.value) || supplied.ContainsKey(p.id)) return false;
                supplied.Add(p.id, p.value);
            }
            var resolved = new float[ids.Length]; var schema = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.IsNullOrEmpty(ids[i]) || !schema.Add(ids[i]) || float.IsNaN(minimum[i]) || float.IsInfinity(minimum[i]) ||
                    float.IsNaN(maximum[i]) || float.IsInfinity(maximum[i]) || minimum[i] > maximum[i] ||
                    !supplied.TryGetValue(ids[i], out var v) || v < minimum[i] || v > maximum[i] || whole[i] && Math.Round(v)!=v) return false;
                resolved[i] = v;
            }
            values = resolved; return true;
        }
    }
}
