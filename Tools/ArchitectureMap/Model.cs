using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArchitectureMap;

public record Node(string Id, string Label, string Kind, string Path = "", int Line = 0, string? Parent = null, string Group = "Unassigned / Review", string Status = "verified");
public record Edge(string Source, string Target, string Kind, string Path, int Line, string Evidence, string Method, string Status = "verified", int Column = 0);
public record Issue(string Code, string Message, string? Source = null, string? Target = null, string Severity = "warning", Edge? Evidence = null);
public record Graph(List<Node> Nodes, List<Edge> Edges, List<Issue> Issues)
{
    public int Version { get; init; } = 1;
    public DateTimeOffset ScannedAt { get; init; } = DateTimeOffset.UtcNow;
    public double DurationMs { get; set; }
    public int ChangedFiles { get; set; }
    public string Analysis { get; set; } = "partial";
}
public record Rule(string Id, string Source, string Target, string Kind, string Mode);
public record Intent(Dictionary<string, string> Scopes, List<Rule> Rules)
{
    public static Intent Empty => new(new(), new());
    public Dictionary<string, ScopeAnchor> Anchors { get; init; } = new();
}
public record ScopeAnchor(string Path, string RuleNode);
public static class Data
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static T? Read<T>(string path) => File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) : default;
    public static void Write(string path, object value)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Json));
        File.Move(temp, path, true);
    }
    public static string Relative(string root, string path) => System.IO.Path.GetRelativePath(root, path).Replace('\\', '/');
    public static bool SafePath(string root, string relative, out string full)
    {
        full = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relative));
        if (System.IO.Path.IsPathRooted(relative) || !full.StartsWith(System.IO.Path.GetFullPath(root) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
        for (var p = full; p != root && !string.IsNullOrEmpty(p); p = System.IO.Path.GetDirectoryName(p)!)
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) return false;
        return true;
    }
}
