namespace ArchitectureMap;

public static class Policy
{
    public static void Validate(Intent intent)
    {
        if (intent.Scopes == null || intent.Rules == null || intent.Anchors == null || intent.Scopes.Count > 100000 || intent.Rules.Count > 10000 || intent.Anchors.Count > 100000) throw new ArgumentException("Invalid policy size.");
        if (intent.Anchors.Values.Any(a => a == null || string.IsNullOrWhiteSpace(a.Path) || string.IsNullOrWhiteSpace(a.RuleNode))) throw new ArgumentException("Invalid scope anchor.");
        if (intent.Scopes.Values.Any(v => v is not ("editable" or "protected" or "watch"))) throw new ArgumentException("Unknown scope.");
        if (intent.Rules.Any(r => r == null || string.IsNullOrWhiteSpace(r.Id) || r.Source == null || r.Target == null || r.Mode is not ("required" or "prohibited" or "direction" or "acyclic") || string.IsNullOrWhiteSpace(r.Kind))) throw new ArgumentException("Invalid architecture rule.");
        if (intent.Rules.Select(r => r.Id).Distinct().Count() != intent.Rules.Count) throw new ArgumentException("Duplicate rule ID.");
    }
    public static string? Scope(Node node, IReadOnlyDictionary<string, Node> nodes, Intent intent)
    {
        var visited = new HashSet<string>();
        while (visited.Add(node.Id))
        {
            if (intent.Scopes.TryGetValue(node.Id, out var value)) return value;
            if (node.Parent == null || !nodes.TryGetValue(node.Parent, out node!)) break;
        }
        return null;
    }
    public static List<Issue> Check(Graph graph, Intent intent, IEnumerable<string>? changed = null)
    {
        Validate(intent);
        var issues = new List<Issue>();
        var nodes = graph.Nodes.ToDictionary(n => n.Id);
        foreach (var edge in graph.Edges)
            if (!nodes.ContainsKey(edge.Source) || !nodes.ContainsKey(edge.Target)) issues.Add(new("dangling", "Reference endpoint is unavailable.", edge.Source, edge.Target, "warning", edge));
        foreach (var rule in intent.Rules)
        {
            var edges = graph.Edges.Where(e => e.Kind == rule.Kind && e.Status == "verified").ToList();
            var matches = edges.Where(e => e.Source == rule.Source && e.Target == rule.Target).ToList();
            if (rule.Mode == "required" && matches.Count == 0)
                issues.Add(new(rule.Id, "Required relationship was not observed; incomplete analysis may hide it.", rule.Source, rule.Target, "warning"));
            if (rule.Mode == "prohibited") foreach (var e in matches) issues.Add(new(rule.Id, "Detected a relationship prohibited by this rule.", e.Source, e.Target, "error", e));
            if (rule.Mode == "direction") foreach (var e in edges.Where(e => e.Source == rule.Target && e.Target == rule.Source)) issues.Add(new(rule.Id, "Detected direction is opposite to the explicitly expected direction.", e.Source, e.Target, "error", e));
            if (rule.Mode == "acyclic")
            {
                var reachable = Impact(rule.Target, edges, 10000, false);
                if (matches.Count > 0 && reachable.Contains(rule.Source)) issues.Add(new(rule.Id, "This relationship participates in a cycle forbidden by this rule.", rule.Source, rule.Target, "error", matches[0]));
            }
        }
        var paths = (changed ?? []).ToHashSet(StringComparer.Ordinal);
        foreach (var n in graph.Nodes.Where(n => paths.Contains(n.Path)))
        {
            var scope = Scope(n, nodes, intent);
            if (scope is "protected" or "watch") issues.Add(new("scope:" + n.Id, scope == "protected" ? "Git reports changes in protected scope." : "Watched content changed; review the diff.", n.Id, Severity: scope == "protected" ? "error" : "info"));
        }
        foreach (var (id, anchor) in intent.Anchors.Where(a => !nodes.ContainsKey(a.Key) && paths.Contains(a.Value.Path)))
            if (intent.Scopes.TryGetValue(anchor.RuleNode, out var scope) && scope is "protected" or "watch")
                issues.Add(new("scope:" + id, "Previously scoped content was removed or changed: " + anchor.Path, id, Severity: scope == "protected" ? "error" : "info"));
        return issues;
    }
    public static void Anchor(Intent intent, Graph graph)
    {
        var nodes = graph.Nodes.ToDictionary(n => n.Id);
        var recordedPaths = new HashSet<string>();
        foreach (var original in graph.Nodes.Where(n => n.Path.Length > 0))
        {
            var n = original; var seen = new HashSet<string>();
            while (seen.Add(n.Id))
            {
                if (intent.Scopes.ContainsKey(n.Id))
                {
                    if (intent.Scopes.ContainsKey(original.Id) || recordedPaths.Add(original.Path + "|" + n.Id)) intent.Anchors[original.Id] = new(original.Path, n.Id);
                    else intent.Anchors.Remove(original.Id);
                    break;
                }
                if (n.Parent == null || !nodes.TryGetValue(n.Parent, out n!)) { intent.Anchors.Remove(original.Id); break; }
            }
        }
    }
    public static HashSet<string> Impact(string id, IEnumerable<Edge> edges, int depth, bool incoming)
    {
        var index = edges.Where(e => e.Kind != "contains").GroupBy(e => incoming ? e.Target : e.Source).ToDictionary(g => g.Key, g => g.Select(e => incoming ? e.Source : e.Target).ToArray());
        var seen = new HashSet<string>(); var frontier = new HashSet<string> { id };
        for (var i = 0; i < depth && frontier.Count > 0; i++)
        {
            var next = new HashSet<string>();
            foreach (var x in frontier) if (index.TryGetValue(x, out var targets)) foreach (var t in targets) if (seen.Add(t)) next.Add(t);
            frontier = next;
        }
        return seen;
    }
}
