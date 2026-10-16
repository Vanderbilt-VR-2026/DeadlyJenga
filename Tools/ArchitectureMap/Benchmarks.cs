using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ArchitectureMap;

public static class Benchmarks
{
    public static void Run(string outputRoot)
    {
        var root = Path.Combine(Path.GetTempPath(), "architecture-benchmark-" + Guid.NewGuid().ToString("N"));
        var results = new List<object>();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Assets"));
            foreach (var count in new[] { 100, 1000, 5000 })
            {
                var yaml = new StringBuilder("%YAML 1.1\n");
                for (var i = 1; i <= count; i++) yaml.Append($"--- !u!1 &{i}\nGameObject:\n  m_Name: Node {i}\n  reference: {{fileID: {(i == count ? 1 : i + 1)}}}\n");
                var path = Path.Combine(root,"Assets/Benchmark.prefab"); File.WriteAllText(path,yaml.ToString());
                File.WriteAllText(path+".meta", "guid: 12345678901234567890123456789012");
                var engine = new Discovery(root); var cold = engine.Scan(true); var warm = engine.Scan();
                File.AppendAllText(path,"  extraReference: {fileID: 1}\n"); var incremental=engine.Scan(); var full=new Discovery(root).Scan(true);
                var equal = JsonSerializer.Serialize(new { incremental.Nodes, incremental.Edges }, Data.Json) == JsonSerializer.Serialize(new { full.Nodes, full.Edges }, Data.Json);
                if (!equal) throw new InvalidOperationException("Benchmark incremental/full mismatch.");
                var search = Stopwatch.StartNew(); for(var i=0;i<100;i++) _=full.Nodes.Where(n=>n.Label.Contains("Node 9",StringComparison.Ordinal)).ToArray(); search.Stop();
                results.Add(new { inputObjects=count, nodes=full.Nodes.Count, edges=full.Edges.Count, coldMs=cold.DurationMs, warmMs=warm.DurationMs, incrementalMs=incremental.DurationMs, fullMs=full.DurationMs, search100Ms=search.Elapsed.TotalMilliseconds, incrementalEqualsFull=equal, managedBytes=GC.GetTotalMemory(false) });
            }
            var report=new { measuredAt=DateTimeOffset.UtcNow, runtime=Environment.Version.ToString(), fixtures=results };
            Data.Write(Path.Combine(outputRoot,".architecture/local/fixture-benchmark.json"),report); Console.WriteLine(JsonSerializer.Serialize(report,Data.Json));
        }
        finally { if (root.StartsWith(Path.GetTempPath(),StringComparison.OrdinalIgnoreCase) && Path.GetFileName(root).StartsWith("architecture-benchmark-",StringComparison.Ordinal)) Directory.Delete(root,true); }
    }
}
