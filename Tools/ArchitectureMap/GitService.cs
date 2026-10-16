using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArchitectureMap;

public record Change(string Path, string Index, string Worktree, string? OldPath = null);
public record BranchChange(string Name, List<string> Files, string? Error = null);
public record GitState(string Branch, List<Change> Changes, string[] Branches, string? AheadBehind, string? Error);
public sealed class GitService(string root)
{
    public (int Code, string Text) Run(params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) start.ArgumentList.Add(a);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(15000)) { process.Kill(true); throw new IOException("Git timed out."); }
        Task.WaitAll(output, errors);
        return (process.ExitCode, process.ExitCode == 0 ? output.Result : errors.Result);
    }
    public string Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('-') || name.Length > 250) throw new ArgumentException("Invalid Git reference.");
        var r = Run("rev-parse", "--verify", "--end-of-options", name + "^{commit}");
        if (r.Code != 0 || !Regex.IsMatch(r.Text.Trim(), "^[a-f0-9]{40,64}$")) throw new ArgumentException("Reference unavailable locally.");
        return r.Text.Trim();
    }
    public GitState Status()
    {
        try
        {
            var r = Run("status", "--porcelain=v1", "-z", "--untracked-files=normal");
            if (r.Code != 0) return new("unavailable", [], [], null, "Git repository unavailable.");
            var rows = r.Text.Split('\0', StringSplitOptions.RemoveEmptyEntries); var changes = new List<Change>();
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i]; if (row.Length < 4) continue;
                var old = (row[..2].Contains('R') || row[..2].Contains('C')) && i + 1 < rows.Length ? rows[++i] : null;
                changes.Add(new(row[3..], row[0].ToString(), row[1].ToString(), old));
            }
            var ahead = Run("rev-list", "--left-right", "--count", "HEAD...@{upstream}");
            return new(Run("branch", "--show-current").Text.Trim(), changes, Run("for-each-ref", "--format=%(refname:short)", "refs/heads", "refs/remotes").Text.Split('\n', StringSplitOptions.RemoveEmptyEntries), ahead.Code == 0 ? ahead.Text.Trim() : null, null);
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception) { return new("unavailable", [], [], null, "Git executable or repository unavailable."); }
    }
    public BranchChange Compare(string branch, string baseline)
    {
        try
        {
            var head = Resolve(branch); var baseId = Resolve(baseline); var merge = Run("merge-base", head, baseId);
            if (merge.Code != 0) return new(branch, [], "No common ancestor available.");
            var diff = Run("diff", "--name-only", "-z", merge.Text.Trim(), head, "--");
            return new(branch, diff.Text.Split('\0', StringSplitOptions.RemoveEmptyEntries).ToList(), diff.Code == 0 ? null : "Comparison failed.");
        }
        catch (ArgumentException ex) { return new(branch, [], ex.Message); }
    }
    public object Preview(string left, string right, Graph graph)
    {
        var a = Resolve(left); var b = Resolve(right); var common = Run("merge-base", a, b);
        if (common.Code != 0) throw new ArgumentException("No common ancestor.");
        var first = Compare(a, common.Text.Trim()); var second = Compare(b, common.Text.Trim());
        var overlap = first.Files.Intersect(second.Files).ToArray();
        var one = graph.Nodes.Where(n => first.Files.Contains(n.Path)).Select(n => n.Id).ToHashSet();
        var two = graph.Nodes.Where(n => second.Files.Contains(n.Path)).Select(n => n.Id).ToHashSet();
        var dependencies = graph.Edges.Where(e => e.Kind != "contains" && (one.Contains(e.Source) && two.Contains(e.Target) || two.Contains(e.Source) && one.Contains(e.Target))).Take(500).ToList();
        // merge-tree creates Git objects only; it never updates the index or worktree.
        var merge = Run("merge-tree", "--write-tree", "--messages", a, b);
        return new { left, right, overlap, dependencies, confirmedTextConflict = merge.Code == 1, mergeAvailable = merge.Code is 0 or 1, preview = "Estimate: dependencies use the current local graph; merged content is not reanalyzed.", details = merge.Text.Length > 12000 ? merge.Text[..12000] : merge.Text };
    }
    public string Diff(string path, string? branch = null, string? baseline = null)
    {
        if (!Data.SafePath(root, path, out _)) throw new ArgumentException("Invalid path.");
        var args = new List<string> { "diff", "--no-ext-diff", "--no-textconv", "--no-color" };
        if (branch != null && baseline != null)
        { var head = Resolve(branch); var merge = Run("merge-base", Resolve(baseline), head); if (merge.Code != 0) throw new ArgumentException("No merge base."); args.Add(merge.Text.Trim()); args.Add(head); }
        else if (Run("rev-parse", "--verify", "HEAD").Code == 0) args.Add("HEAD");
        args.Add("--"); args.Add(path); var result = Run(args.ToArray()); return result.Text.Length > 200000 ? result.Text[..200000] + "\n[diff truncated]" : result.Text;
    }
    public async Task<object> PullRequests()
    {
        var remote = Run("remote", "get-url", "origin");
        var match = Regex.Match(remote.Text.Trim(), @"^(?:https://github\.com/|git@github\.com:)([\w.-]+/[\w.-]+?)(?:\.git)?$");
        if (!match.Success) return new { status = "unavailable", message = "Origin is not a supported github.com URL." };
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 4 * 1024 * 1024 };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("DeadlyJenga-ArchitectureMap/1.0");
        var token = Environment.GetEnvironmentVariable("GH_TOKEN") ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(token)) http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        try
        {
            var repo = match.Groups[1].Value; var prs = new List<object>();
            for (var page = 1; page <= 5; page++)
            {
                using var response = await http.GetAsync($"https://api.github.com/repos/{repo}/pulls?state=open&per_page=100&page={page}");
                if (!response.IsSuccessStatusCode) return new { status = "unavailable", message = "GitHub returned HTTP " + (int)response.StatusCode + ". Local analysis remains available." };
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                foreach (var p in json.RootElement.EnumerateArray()) prs.Add(new { number = p.GetProperty("number").GetInt32(), title = p.GetProperty("title").GetString(), url = p.GetProperty("html_url").GetString(), author = p.GetProperty("user").GetProperty("login").GetString(), head = p.GetProperty("head").GetProperty("sha").GetString(), baseline = p.GetProperty("base").GetProperty("sha").GetString() });
                if (json.RootElement.GetArrayLength() < 100) return new { status = "current", fetchedAt = DateTimeOffset.UtcNow, pullRequests = prs };
            }
            return new { status = "partial", message = "Limited to 500 open pull requests.", pullRequests = prs };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException)
        { return new { status = "unavailable", message = "GitHub response unavailable or malformed. No remote information is assumed current." }; }
    }
}
