using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
namespace ArchitectureMap;

public record RelationshipDelta(Edge Edge, string Change, int Count);
public record RelationshipComparison(string Name, string Baseline, string Head, List<RelationshipDelta> Relationships, List<Node> Nodes, List<string> Warnings, string? Error = null);

// Revision contents are data only: no checkout, build, project imports, scripts or Unity execution.
public sealed class RelationshipCompare(string root)
{
    readonly SemaphoreSlim gate = new(1);
    readonly Dictionary<string, Graph> cache = new();
    public async Task<RelationshipComparison> Compare(string branch, string baseline, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        var token = timeout.Token; var entered = false;
        try
        {
            await gate.WaitAsync(token); entered = true;
            var git = new GitService(root); var head = git.Resolve(branch); var start = git.Resolve(baseline);
            var merge = git.Run("merge-base", start, head); if (merge.Code != 0) throw new ArgumentException("No common ancestor available.");
            var ancestor = merge.Text.Trim();
            token.ThrowIfCancellationRequested();
            // Tooling/documentation-only commits cannot change the analyzed Assets graph.
            var unchanged = git.Run("diff", "--quiet", ancestor, head, "--", "Assets");
            if (unchanged.Code == 0) return new(branch, ancestor, head, [], [], ["No changes in Assets between these revisions; no relationship scan needed."]);
            if (unchanged.Code != 1) throw new IOException("Could not compare revision asset trees.");
            var before = await Snapshot(ancestor, token); var after = await Snapshot(head, token);
            return Difference(branch, ancestor, head, before, after);
        }
        catch (Exception e) when (e is IOException or ArgumentException or OperationCanceledException or InvalidOperationException)
        { return new(branch, baseline, "", [], [], [], e is OperationCanceledException ? "Relationship comparison cancelled or reached its two-minute limit. No partial result is shown. Compare fewer branches or retry once the server is idle." : "Relationship comparison unavailable: " + e.Message); }
        finally { if (entered) gate.Release(); }
    }
    public static RelationshipComparison Difference(string name, string baseline, string head, Graph before, Graph after)
    {
        // Ignore source-line shifts and evidence text; compare endpoint/type multiplicity.
        string Key(Edge e) => System.Text.Json.JsonSerializer.Serialize(new[] { e.Source, e.Target, e.Kind });
        var a = before.Edges.GroupBy(Key).ToDictionary(g => g.Key, g => g.ToList());
        var b = after.Edges.GroupBy(Key).ToDictionary(g => g.Key, g => g.ToList());
        var delta = new List<RelationshipDelta>();
        foreach (var key in a.Keys.Union(b.Keys).Order())
        { var old = a.GetValueOrDefault(key) ?? []; var next = b.GetValueOrDefault(key) ?? []; var count = next.Count - old.Count;
          if (count != 0) delta.Add(new(count > 0 ? next[0] : old[0], count > 0 ? "added" : "removed", Math.Abs(count))); }
        var ids = delta.SelectMany(d => new[] { d.Edge.Source, d.Edge.Target }).ToHashSet();
        var nodes = after.Nodes.Concat(before.Nodes).Where(n => ids.Contains(n.Id)).DistinctBy(n => n.Id).ToList();
        var warnings = new List<string> { "Static revision analysis only. Unity AssetDatabase exports and compiled Unity/package assemblies are not available for historical revisions; unresolved calls may not be detected. No change means no detected change, not proof of identical behavior." };
        return new(name, baseline, head, delta, nodes, warnings);
    }
    async Task<Graph> Snapshot(string sha, CancellationToken token)
    {
        if (cache.TryGetValue(sha, out var graph)) return graph;
        var listing = new GitService(root).Run("ls-tree", "-r", "-z", sha, "--", "Assets");
        if (listing.Code != 0) throw new IOException("Could not read revision tree.");
        var dir = Path.Combine(Path.GetTempPath(), "architecture-revision-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Assets"));

        var start = new ProcessStartInfo("git") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("cat-file"); start.ArgumentList.Add("--batch");
        using var process = Process.Start(start)!; var errors = process.StandardError.ReadToEndAsync();
        try
        {
            long total = 0; int files = 0;
            foreach (var entry in listing.Text.Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                token.ThrowIfCancellationRequested();
                var tab = entry.IndexOf('\t'); if (tab < 0) continue;
                var header = entry[..tab].Split(' '); var path = entry[(tab + 1)..];
                if (header[0] is not ("100644" or "100755")) throw new IOException("Revision contains symlinks or submodules; comparison requires ordinary asset files.");
                if (++files > 60000 || !Data.SafePath(dir, path, out var target)) throw new IOException("Revision exceeds safe inventory limits or contains an unsupported path.");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext is not (".cs" or ".meta" or ".unity" or ".prefab" or ".asset" or ".mat" or ".asmdef")) { File.WriteAllText(target, ""); continue; }
                await process.StandardInput.WriteLineAsync(header[2]); await process.StandardInput.FlushAsync();
                var stream = process.StandardOutput.BaseStream; var line = new StringBuilder(); var one = new byte[1];
                while (true) { await stream.ReadExactlyAsync(one, token); if (one[0] == 10) break; if (line.Length > 200) throw new IOException("Invalid Git blob header."); line.Append((char)one[0]); }
                var info = line.ToString().Split(' '); if (info.Length != 3 || info[1] != "blob" || !int.TryParse(info[2], out var size) || size > 32 * 1024 * 1024 || (total += size) > 256 * 1024 * 1024) throw new IOException("Revision exceeds the 32 MB/file or 256 MB analysis limit.");
                var bytes = new byte[size]; await stream.ReadExactlyAsync(bytes, token); await stream.ReadExactlyAsync(one, token);
                var prefix = Encoding.UTF8.GetString(bytes, 0, Math.Min(size, 150));
                if (prefix.StartsWith("version https://git-lfs.github.com/spec/"))
                { if (path.EndsWith("LightingData.asset", StringComparison.OrdinalIgnoreCase)) { File.WriteAllText(target, ""); continue; } throw new IOException("Historical source is an LFS pointer: " + path + ". Its relationships cannot be compared accurately."); }
                await File.WriteAllBytesAsync(target, bytes, token);
            }
            process.StandardInput.Close(); await process.WaitForExitAsync(token);
            if (process.ExitCode != 0) throw new IOException("Git could not read revision blobs: " + await errors);
            graph = await Task.Run(() => new Discovery(dir).Scan(cancellationToken: token), token);
            if (cache.Count >= 4) cache.Remove(cache.Keys.First()); cache[sha] = graph; return graph;
        }
        finally { if (!process.HasExited) process.Kill(true); Directory.Delete(dir, true); }
    }
}
