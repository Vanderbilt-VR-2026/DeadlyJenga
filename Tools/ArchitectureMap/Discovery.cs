using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace ArchitectureMap;

public record Fragment(string Hash, List<Node> Nodes, List<Edge> Edges);
public sealed class Discovery(string root)
{
    const string CacheVersion = "v4";
    Dictionary<string, Fragment> cache = new();
    Graph? last;
    string inventory = "";
    DateTimeOffset lastAudit;
    public string Health { get; private set; } = "ready";
    public string Root => root;
    public static IEnumerable<string> Files(string dir)
    {
        if (!Directory.Exists(dir)) yield break;
        foreach (var file in Directory.EnumerateFiles(dir)) if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0) yield return file;
        foreach (var sub in Directory.EnumerateDirectories(dir)) if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) == 0) foreach (var file in Files(sub)) yield return file;
    }
    public Graph Scan(bool full = false)
    {
        var timer = Stopwatch.StartNew();
        var files = Files(Path.Combine(root, "Assets")).ToArray();
        var context = Semantic.ContextHash(root);
        var signature = Data.Hash(string.Join("|", files.Order().Select(p => { var info = new FileInfo(p); return p + ":" + info.Length + ":" + info.LastWriteTimeUtc.Ticks; })) + context + File.GetLastWriteTimeUtc(Path.Combine(root, ".architecture/local/unity.json")).Ticks);
        if (!full && last != null && inventory == signature && DateTimeOffset.UtcNow - lastAudit < TimeSpan.FromMinutes(5))
            return last with { DurationMs = timer.Elapsed.TotalMilliseconds, ChangedFiles = 0 };
        var cachePath = Path.Combine(root, ".architecture/local/cache.json");
        if (full) cache.Clear();
        else if (cache.Count == 0) try { cache = Data.Read<Dictionary<string, Fragment>>(cachePath) ?? new(); } catch { Health = "Corrupt cache discarded; rebuilding."; cache.Clear(); }
        var next = new Dictionary<string, Fragment>(); var issues = new List<Issue>();
        var guids = new Dictionary<string, string>();
        foreach (var meta in files.Where(f => f.EndsWith(".meta")))
        {
            var match = Regex.Match(File.ReadAllText(meta), @"(?m)^guid:\s*([a-fA-F0-9]{32})\s*$");
            if (match.Success) guids[Data.Relative(root, meta[..^5])] = match.Groups[1].Value.ToLowerInvariant();
        }
        var duplicates = guids.GroupBy(x => x.Value).Where(g => g.Count() > 1).ToList();
        foreach (var d in duplicates) issues.Add(new("duplicate-guid", "Duplicate Unity GUID: " + string.Join(", ", d.Select(x => x.Key)), Severity: "error"));
        var changed = 0;
        foreach (var file in files.Where(f => !f.EndsWith(".meta")))
        {
            var path = Data.Relative(root, file); var ext = Path.GetExtension(file).ToLowerInvariant();
            var id = "asset:" + (guids.GetValueOrDefault(path) ?? "path:" + path);
            if (duplicates.Any(g => g.Key == guids.GetValueOrDefault(path))) id += ":" + path;
            var text = ext is ".cs" or ".unity" or ".prefab" or ".asset" or ".mat" or ".asmdef" ? File.ReadAllText(file) : "";
            var hash = Data.Hash(CacheVersion + path + id + text);
            if (!cache.TryGetValue(path, out var fragment) || fragment == null || fragment.Hash != hash || fragment.Nodes == null || fragment.Edges == null || fragment.Nodes.Any(n => n == null || string.IsNullOrEmpty(n.Id)))
            { fragment = Asset(path, id, ext, text, hash); changed++; }
            next[path] = fragment;
        }
        changed += cache.Keys.Except(next.Keys).Count();
        var codeChanged = full || last == null || next.Where(x => x.Key.EndsWith(".cs") || x.Key.EndsWith(".asmdef")).Any(x => !cache.TryGetValue(x.Key, out var old) || old.Hash != x.Value.Hash)
            || cache.Keys.Except(next.Keys).Any(x => x.EndsWith(".cs") || x.EndsWith(".asmdef")) || context != previousContext;
        var nodes = next.Values.SelectMany(f => f.Nodes).ToList(); var edges = next.Values.SelectMany(f => f.Edges).ToList();
        if (codeChanged) code = Semantic.Analyze(root, nodes.Where(n => n.Kind == "script").ToList());
        nodes.AddRange(code.Nodes); edges.AddRange(code.Edges); issues.AddRange(code.Issues);
        try
        {
            var unity = Data.Read<UnitySnapshot>(Path.Combine(root, ".architecture/local/unity.json"));
            if (unity != null) foreach (var entry in unity.Assets)
            {
                if (!Data.SafePath(root, entry.Path, out var fullPath) || !File.Exists(fullPath)) continue;
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(fullPath)));
                if (hash != entry.Hash) { issues.Add(new("unity-stale", "Unity dependency export is stale for " + entry.Path, "asset:" + entry.Guid, Severity: "info")); continue; }
                foreach (var target in entry.Dependencies) edges.Add(new("asset:" + entry.Guid, "asset:" + target, "depends", entry.Path, 0, "Direct AssetDatabase dependency", "Unity AssetDatabase"));
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException) { issues.Add(new("unity-export", "Unity export unavailable; serialized-file analysis is still available.")); }
        previousContext = context;
        var known = nodes.Select(n => n.Id).ToHashSet();
        var assetIndex = nodes.Where(n => n.Id.StartsWith("asset:") && !n.Id.Contains('#')).DistinctBy(n => n.Id).ToDictionary(n => n.Id);
        // Serialized GUID+fileID references are retained exactly. An external subobject is not assumed missing without Unity's database.
        foreach (var e in edges.ToArray()) if (!known.Contains(e.Target))
        {
            var assetId = e.Target.Split('#')[0];
            var existing = assetIndex.GetValueOrDefault(assetId);
            var builtin = assetId.StartsWith("asset:0000000000000000");
            var status = existing != null || builtin ? "unknown" : "unresolved";
            nodes.Add(new(e.Target, existing == null ? e.Target : existing.Label + " · object " + e.Target.Split('#').Last(), "unresolved", existing?.Path ?? "", Status: status)); known.Add(e.Target);
            if (status == "unresolved") issues.Add(new("unresolved-reference", "Serialized reference could not be resolved locally; it may belong to a package or a missing asset.", e.Source, e.Target, "warning", e));
        }
        var unresolved = nodes.Where(n => n.Kind == "unresolved").Select(n => n.Id).ToHashSet();
        for (var i = 0; i < edges.Count; i++) if (unresolved.Contains(edges[i].Target)) edges[i] = edges[i] with { Status = "unknown" };
        var groups = nodes.Where(n => n.Kind is not ("external" or "unresolved")).Select(n => n.Group).Distinct().ToArray();
        foreach (var group in groups) nodes.Add(new("group:" + group, group, "group"));
        for (var i = 0; i < nodes.Count; i++) if (nodes[i].Parent == null && nodes[i].Kind is not ("group" or "external" or "unresolved")) nodes[i] = nodes[i] with { Parent = "group:" + nodes[i].Group };
        var graph = new Graph(nodes.DistinctBy(n => n.Id).OrderBy(n => n.Id).ToList(), edges.Distinct().OrderBy(e => e.Source).ThenBy(e => e.Target).ThenBy(e => e.Line).ToList(), issues);
        graph.DurationMs = timer.Elapsed.TotalMilliseconds; graph.ChangedFiles = changed;
        cache = next; last = graph; inventory = signature; lastAudit = DateTimeOffset.UtcNow;
        Data.Write(cachePath, cache); Data.Write(Path.Combine(root, ".architecture/local/actual.json"), graph);
        return graph;
    }
    string previousContext = "";
    Graph code = new([], [], []);
    public record UnityEntry(string Path, string Hash, string Guid, string[] Dependencies);
    public record UnitySnapshot(string ExportedAt, UnityEntry[] Assets);
    static Fragment Asset(string path, string id, string ext, string text, string hash)
    {
        var group = path.Split('/').Length > 2 ? path.Split('/')[1] : "Project";
        var kind = ext switch { ".cs" => "script", ".unity" => "scene", ".prefab" => "prefab", ".asmdef" => "assembly", _ => "asset" };
        var nodes = new List<Node> { new(id, Path.GetFileName(path), kind, path, Group: group) }; var edges = new List<Edge>();
        if (!text.StartsWith("%YAML")) return new(hash, nodes, edges);
        string current = id; var lines = text.Split('\n');
        var owners = new Dictionary<string, string>(); var fathers = new Dictionary<string, (string Target, int Line)>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i]; var header = Regex.Match(line, @"^--- !u!(\d+) &(-?\d+)");
            if (header.Success)
            {
                current = id + "#" + header.Groups[2].Value;
                var objectKind = header.Groups[1].Value switch { "1" => "gameObject", "1001" => "prefabInstance", "114" => "component", _ => "object" };
                nodes.Add(new(current, objectKind + " " + header.Groups[2].Value, objectKind, path, i + 1, id, group));
                edges.Add(new(id, current, "contains", path, i + 1, "Serialized object document", "Unity YAML"));
            }
            if (line.StartsWith("  m_Name: ") && current != id)
            { var j = nodes.FindIndex(n => n.Id == current); nodes[j] = nodes[j] with { Label = line[10..].Trim() }; }
            foreach (Match m in Regex.Matches(line, @"\{fileID:\s*(-?\d+)(?:,\s*guid:\s*([a-fA-F0-9]{32}),\s*type:\s*\d+)?\}"))
            {
                if (m.Groups[1].Value == "0") continue;
                var target = m.Groups[2].Success ? "asset:" + m.Groups[2].Value.ToLowerInvariant() + (line.Contains("m_Script:") || line.Contains("m_SourcePrefab:") ? "" : "#" + m.Groups[1].Value) : id + "#" + m.Groups[1].Value;
                var relation = line.Contains("m_SourcePrefab:") ? "instantiates" : "references";
                if (line.Contains("m_GameObject:") && current != id)
                {
                    owners[current] = target;
                    edges.Add(new(target, current, "contains", path, i + 1, "m_GameObject owner", "Unity YAML"));
                    var j = nodes.FindIndex(n => n.Id == current); nodes[j] = nodes[j] with { Parent = target };
                }
                else
                {
                    if (line.Contains("m_Father:")) fathers[current] = (target, i + 1);
                    edges.Add(new(current, target, relation, path, i + 1, line.Trim().Split(':')[0] + " serialized reference", "Unity YAML"));
                }
            }
        }
        foreach (var (transform, father) in fathers)
        {
            if (!owners.TryGetValue(transform, out var child) || !owners.TryGetValue(father.Target, out var parent)) continue;
            var index = nodes.FindIndex(n => n.Id == child);
            if (index < 0 || child == parent) continue;
            nodes[index] = nodes[index] with { Parent = parent };
            edges.RemoveAll(e => e.Kind == "contains" && e.Source == id && e.Target == child);
            edges.Add(new(parent, child, "contains", path, father.Line, "Transform.m_Father and m_GameObject owners", "Unity YAML"));
        }
        return new(hash, nodes, edges);
    }
}
