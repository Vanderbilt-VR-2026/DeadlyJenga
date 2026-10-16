using ArchitectureMap;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;

var arguments = args.ToList();
string Option(string key, string fallback) { var i = arguments.IndexOf(key); return i >= 0 && i + 1 < arguments.Count ? arguments[i + 1] : fallback; }
var root = Path.GetFullPath(Option("--root", Directory.GetCurrentDirectory()));
if (args.Contains("--test")) { Environment.ExitCode = SelfTests.Run(); return; }
if (args.Contains("--benchmark-fixtures")) { Benchmarks.Run(root); return; }
if (!Directory.Exists(Path.Combine(root, "Assets"))) throw new ArgumentException("Select a Unity repository with --root.");
var discovery = new Discovery(root); var git = new GitService(root);
var local = Path.Combine(root, ".architecture/local");
if (!Data.SafePath(root, ".architecture/local/actual.json", out _) || !Data.SafePath(root, ".architecture/intent.json", out _)) throw new ArgumentException("Architecture storage must not use symlink directories.");
Directory.CreateDirectory(local);
Intent ReadIntent() { try { var value = Data.Read<Intent>(Path.Combine(root, ".architecture/intent.json")) ?? Intent.Empty; Policy.Validate(value); return value; } catch { throw new ArgumentException("Intended architecture is invalid. Repair .architecture/intent.json; the file has been preserved."); } }
string? presentationError = null;
JsonElement? ReadPresentation(string path)
{
    try { var value = Data.Read<JsonElement?>(path); if (value != null && value.Value.ValueKind != JsonValueKind.Object) throw new JsonException("Layout must be an object."); return value; }
    catch (Exception ex) when (ex is JsonException or IOException)
    {
        presentationError = "A saved layout could not be read. Defaults are shown; the original file is preserved.";
        return null;
    }
}
var repositoryName = new DirectoryInfo(root).Name;
var graph = discovery.Scan(args.Contains("--rebuild"));
if (args.Contains("--export") || args.Contains("--check") || args.Contains("--benchmark"))
{
    var changedPaths = git.Status().Changes.Select(c => c.Path).ToList();
    if (args.Contains("--base"))
    {
        var comparison = git.Compare("HEAD", Option("--base", "HEAD"));
        if (comparison.Error != null) { Console.WriteLine("Baseline unavailable: " + comparison.Error); if (args.Contains("--check")) Environment.ExitCode = 2; }
        else changedPaths.AddRange(comparison.Files);
    }
    var policy = Policy.Check(graph, ReadIntent(), changedPaths);
    if (args.Contains("--export"))
    {
        var output = Path.GetFullPath(Option("--export", Path.Combine(local, "export"))); Directory.CreateDirectory(output);
        foreach (var path in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "web"))) File.Copy(path, Path.Combine(output, Path.GetFileName(path)), true);
        Data.Write(Path.Combine(output, "snapshot.json"), new { repositoryName, graph, intent = ReadIntent(), issues = policy, git = git.Status(), presentation = ReadPresentation(Path.Combine(root, ".architecture/presentation.json")) });
    }
    if (args.Contains("--benchmark"))
    {
        var cold = graph.DurationMs; var warm = discovery.Scan();
        var result = new { nodes = graph.Nodes.Count, edges = graph.Edges.Count, coldMs = cold, warmMs = warm.DurationMs, warmChanged = warm.ChangedFiles, memoryBytes = GC.GetTotalMemory(false), runtime = Environment.Version.ToString(), measuredAt = DateTimeOffset.UtcNow };
        Data.Write(Path.Combine(local, "benchmark.json"), result); Console.WriteLine(JsonSerializer.Serialize(result, Data.Json));
    }
    Console.WriteLine($"Architecture: {graph.Nodes.Count} nodes, {graph.Edges.Count} edges, {policy.Count} policy findings, {graph.DurationMs:F0} ms.");
    if (args.Contains("--check") && policy.Any(i => i.Severity == "error")) Environment.ExitCode = 1;
    return;
}
var port = int.Parse(Option("--port", "47831"));
var origin = $"http://127.0.0.1:{port}";
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ContentRootPath = AppContext.BaseDirectory });
builder.WebHost.UseUrls(origin); builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 1024 * 1024);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 1024 * 1024);
builder.Services.AddResponseCompression(o => o.EnableForHttps = false);
builder.Services.ConfigureHttpJsonOptions(o => { o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; });
var app = builder.Build(); var gate = new SemaphoreSlim(1); long revision = 1; var lastSeen = DateTimeOffset.UtcNow; string? scanError = null; long dirty = 0; int scanning = 0;
app.UseResponseCompression();
app.Use(async (context, next) =>
{
    if (context.Request.Host.Value != $"127.0.0.1:{port}" || (context.Request.Headers.Origin.Count > 0 && context.Request.Headers.Origin != origin) || context.Request.Headers["Sec-Fetch-Site"] == "cross-site") { context.Response.StatusCode = 403; return; }
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; object-src 'none'; frame-ancestors 'none'";
    context.Response.Headers.XContentTypeOptions = "nosniff"; context.Response.Headers.CacheControl = "no-store";
    if (context.Request.Method == "POST" && context.Request.Headers["X-Architecture-Client"] != "local") { context.Response.StatusCode = 403; return; }
    if (context.Request.ContentLength > 1024 * 1024) { context.Response.StatusCode = 413; return; }
    try { await next(context); }
    catch (Exception ex) when (ex is ArgumentException or JsonException or IOException) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { error = ex.Message }); }
});
app.MapGet("/", () => Results.File(Path.Combine(AppContext.BaseDirectory, "web/index.html"), "text/html"));
foreach (var name in new[] { "app.js", "layout.mjs", "elk-layout.mjs", "elk-api.js", "elk-worker.min.js", "style.css" }) app.MapGet("/" + name, () => Results.File(Path.Combine(AppContext.BaseDirectory, "web", name), name.EndsWith(".css") ? "text/css" : "text/javascript"));
foreach (var asset in new[] { "status-live.mp4", "status-error.png", "status-stale.png" }) app.MapGet("/" + asset, () => Results.File(Path.Combine(AppContext.BaseDirectory, "web", asset), asset.EndsWith(".mp4") ? "video/mp4" : "image/png", enableRangeProcessing: true));
app.MapGet("/api/heartbeat", () => { lastSeen = DateTimeOffset.UtcNow; Data.Write(Path.Combine(local, "lease.json"), new { lastSeen }); return new { revision, error = scanError, refreshing = Volatile.Read(ref scanning) > 0, needsRefresh = Interlocked.Read(ref dirty) > 0 }; });
app.MapGet("/api/state", () =>
{
    var status = git.Status(); var intent = ReadIntent();
    var shared = ReadPresentation(Path.Combine(root, ".architecture/presentation.json"));
    var personal = ReadPresentation(Path.Combine(local, "presentation.json"));
    return new { repositoryName, refreshing = Volatile.Read(ref scanning) > 0, needsRefresh = Interlocked.Read(ref dirty) > 0, graph, intent, issues = Policy.Check(graph, intent, status.Changes.Select(c => c.Path)), git = status, revision, health = discovery.Health, error = scanError ?? presentationError,
        presentation = shared, personal };
});
app.MapGet("/api/git", () => git.Status());
app.MapGet("/api/compare", (string branch, string baseline) => git.Compare(branch, baseline));
app.MapGet("/api/preview", (string left, string right) => git.Preview(left, right, graph));
app.MapGet("/api/pulls", () => git.PullRequests());
app.MapGet("/api/diff", (string path, string? branch, string? baseline) => Results.Text(git.Diff(path, branch, baseline)));
app.MapGet("/api/source", (string path) =>
{
    if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !Data.SafePath(root, path, out var full) || !File.Exists(full)) return Results.BadRequest();
    if (new FileInfo(full).Length > 2 * 1024 * 1024) return Results.BadRequest("File is too large for source preview.");
    if (Path.GetExtension(full) is not (".cs" or ".unity" or ".prefab" or ".asset" or ".asmdef" or ".mat")) return Results.BadRequest();
    return Results.Text(File.ReadAllText(full));
});
app.MapPost("/api/intent", async (HttpContext c) =>
{
    var intent = await c.Request.ReadFromJsonAsync<Intent>() ?? throw new ArgumentException("Missing intent."); Policy.Validate(intent); Policy.Anchor(intent, graph);
    await gate.WaitAsync(); try { Data.Write(Path.Combine(root, ".architecture/intent.json"), intent); Interlocked.Increment(ref revision); } finally { gate.Release(); }
    return Results.Ok();
});
app.MapPost("/api/presentation/{layer}", async (HttpContext c, string layer) =>
{
    if (layer is not ("personal" or "shared")) return Results.BadRequest();
    var value = await c.Request.ReadFromJsonAsync<JsonElement>(); if (value.ValueKind != JsonValueKind.Object) return Results.BadRequest();
    await gate.WaitAsync(); try
    {
        var path = Path.Combine(layer == "personal" ? local : Path.Combine(root, ".architecture"), "presentation.json");
        try { Data.Read<JsonElement?>(path); } catch (JsonException) { File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.Ticks); }
        Data.Write(path, value); presentationError = null;
    } finally { gate.Release(); }
    return Results.Ok();
});
async Task Refresh(bool full)
{
    Interlocked.Increment(ref scanning);
    await gate.WaitAsync();
    try
    {
        var updated = await Task.Run(() => discovery.Scan(full));
        var changed = scanError != null || updated.ScannedAt != graph.ScannedAt;
        graph = updated; scanError = null;
        if (changed) Interlocked.Increment(ref revision);
    }
    catch (Exception ex) { scanError = "Analysis is stale: " + ex.Message; Interlocked.Increment(ref revision); }
    finally { Interlocked.Decrement(ref scanning); gate.Release(); }
}
app.MapPost("/api/refresh", async (bool? full) => { await Refresh(full ?? false); return Results.Ok(); });
app.MapPost("/api/stop", () => { app.Lifetime.StopApplication(); return Results.Ok(); });
using var watcher = new FileSystemWatcher(Path.Combine(root, "Assets")) { IncludeSubdirectories = true, EnableRaisingEvents = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName };
void Dirty(object? sender, FileSystemEventArgs e) => Interlocked.Exchange(ref dirty, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
watcher.Changed += Dirty; watcher.Created += Dirty; watcher.Deleted += Dirty; watcher.Renamed += (s, e) => Dirty(s, e); watcher.Error += (_, _) => Interlocked.Exchange(ref dirty, 1);
_ = Task.Run(async () =>
{
    var reconcile = DateTimeOffset.UtcNow;
    while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
    {
        await Task.Delay(1000);
        if (DateTimeOffset.UtcNow - lastSeen > TimeSpan.FromSeconds(90)) { app.Lifetime.StopApplication(); break; }
        var stamp = Interlocked.Read(ref dirty);
        if (stamp > 0 && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - stamp > 750 || DateTimeOffset.UtcNow - reconcile > TimeSpan.FromSeconds(60))
        { Interlocked.Exchange(ref dirty, 0); await Refresh(false); reconcile = DateTimeOffset.UtcNow; }
    }
});
Console.WriteLine("Architecture Map: " + origin + " (closes after 90 seconds without a dashboard heartbeat)");
if (args.Contains("--open")) _ = Task.Run(async () => { await Task.Delay(1500); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(origin) { UseShellExecute = true }); });
await app.RunAsync();
