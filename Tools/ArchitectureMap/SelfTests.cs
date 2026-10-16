using System.Text.Json;

namespace ArchitectureMap;

public static class SelfTests
{
    public static int Run()
    {
        int passed = 0, failed = 0;
        void Test(string name, Action test) { try { test(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); } }
        void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
        var root = Path.Combine(Path.GetTempPath(), "architecture-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path.Combine(root, "Assets"));
        void Write(string path, string text) { Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, path))!); File.WriteAllText(Path.Combine(root, path), text); }
        void Script(string path, string code, string guid) { Write("Assets/" + path, code); Write("Assets/" + path + ".meta", "guid: " + guid); }
        string Normal(Graph g) => JsonSerializer.Serialize(new { g.Nodes, g.Edges }, Data.Json);
        try
        {
            Script("A.cs", "public interface I { void Run(); } public class A : I { public event System.Action E; public virtual void Run() { B.Go(); E += B.Go; } } public static class B { public static void Go() {} }", new string('a', 32));
            Test("Roslyn calls, inheritance and event subscriptions", () => { var g = new Discovery(root).Scan(); Assert(g.Edges.Any(e => e.Kind == "calls")); Assert(g.Edges.Any(e => e.Kind == "inherits")); Assert(g.Edges.Any(e => e.Kind == "subscribes")); Assert(g.Edges.Where(e => e.Method == "Roslyn semantic").All(e => e.Status == "inferred")); });
            Test("Verified semantic context binds overloads, construction and field types", () =>
            {
                var exact = Path.Combine(root,"Exact"); Directory.CreateDirectory(Path.Combine(exact,"Assets"));
                File.WriteAllText(Path.Combine(exact,"Assets/A.cs"),"public class A { B field; public void Run() { field=new B(); field.Go(1); } } public class B { public void Go(int x) {} public void Go(string x) {} }");
                File.WriteAllText(Path.Combine(exact,"Exact.csproj"),"<Project><ItemGroup><ProjectCapability Include=\"Unity\"/><Compile Include=\"Assets/A.cs\"/><Reference Include=\"Core\"><HintPath>"+System.Security.SecurityElement.Escape(typeof(object).Assembly.Location)+"</HintPath></Reference></ItemGroup><PropertyGroup><AssemblyName>Exact</AssemblyName></PropertyGroup></Project>");
                var g=new Discovery(exact).Scan(); var call=g.Edges.Single(e=>e.Kind=="calls"); Assert(call.Status=="verified"); Assert(g.Nodes.Single(n=>n.Id==call.Target).Label.Contains("int")); Assert(g.Edges.Any(e=>e.Kind=="instantiates"&&e.Status=="verified")); Assert(g.Edges.Any(e=>e.Kind=="depends"));
            });
            Test("Unity serialized ownership, nested prefab and distinct references", () =>
            {
                Write("Assets/P.prefab.meta", "guid: " + new string('b', 32));
                Write("Assets/P.prefab", "%YAML 1.1\n--- !u!1 &1\nGameObject:\n  m_Name: Player\n--- !u!114 &2\nMonoBehaviour:\n  m_GameObject: {fileID: 1}\n  m_Script: {fileID: 11500000, guid: " + new string('a',32) + ", type: 3}\n  other: {fileID: 1}\n--- !u!1001 &3\nPrefabInstance:\n  m_SourcePrefab: {fileID: 100100000, guid: " + new string('c',32) + ", type: 3}\n");
                var g = new Discovery(root).Scan(); Assert(g.Nodes.Any(n => n.Label == "Player")); Assert(g.Edges.Any(e => e.Kind == "contains" && e.Source.EndsWith("#1") && e.Target.EndsWith("#2"))); Assert(g.Edges.Any(e => e.Kind == "instantiates" && e.Status == "unknown")); Assert(g.Issues.Any(i => i.Code == "unresolved-reference"));
            });
            Test("GUID move preserves identity and incremental/full equivalence", () =>
            {
                var d = new Discovery(root); var before = d.Scan(); Directory.CreateDirectory(Path.Combine(root,"Assets/Moved")); File.Move(Path.Combine(root,"Assets/A.cs"),Path.Combine(root,"Assets/Moved/A.cs")); File.Move(Path.Combine(root,"Assets/A.cs.meta"),Path.Combine(root,"Assets/Moved/A.cs.meta"));
                var after = d.Scan(); Assert(before.Nodes.Where(n => n.Kind == "method").Select(n => n.Id).SequenceEqual(after.Nodes.Where(n => n.Kind == "method").Select(n => n.Id))); Assert(Normal(after) == Normal(new Discovery(root).Scan(true)));
            });
            Test("Seed 731: randomized edits, deletion, duplicate events and clean reconstruction", () =>
            {
                var random = new Random(731); var d = new Discovery(root); d.Scan();
                for (var i=0;i<30;i++) { var number = random.Next(5); var file = "Assets/R"+number+".cs"; if (random.Next(3)==0) File.Delete(Path.Combine(root,file)); else Write(file,"public class R"+number+" { public int M"+random.Next(6)+"() => "+random.Next(100)+"; }"); var inc=d.Scan(); Assert(Normal(inc)==Normal(new Discovery(root).Scan(true)), "seed 731 iteration "+i); Assert(Normal(d.Scan())==Normal(inc), "duplicate event"); }
            });
            Test("Corrupt cache recovers", () => { Write(".architecture/local/cache.json", "{broken"); var d = new Discovery(root); Assert(d.Scan().Nodes.Count > 0); Assert(d.Health.Contains("Corrupt")); });
            Test("Malformed cache entry recovers without disappearing files", () => { Write(".architecture/local/cache.json", "{\"Assets/Moved/A.cs\":null}"); Assert(new Discovery(root).Scan().Nodes.Any(n=>n.Path=="Assets/Moved/A.cs")); });
            Test("Signature, inheritance and assembly metadata invalidation", () => { var d = new Discovery(root); d.Scan(); Write("Assets/Moved/A.cs", "public class A : System.IDisposable { public void Dispose() {} public void Run(int x) {} }"); Write("Assets/Test.asmdef", "{\"name\":\"Test\"}"); Assert(Normal(d.Scan()) == Normal(new Discovery(root).Scan(true))); });
            Test("Duplicate GUID is explicit", () => { Write("Assets/R0.cs.meta", "guid: " + new string('a',32)); Write("Assets/R0.cs", "class R0 {}"); var g=new Discovery(root).Scan(); Assert(g.Issues.Any(i=>i.Code=="duplicate-guid")); Assert(g.Nodes.Select(n=>n.Id).Distinct().Count()==g.Nodes.Count); });
            var graph = new Graph([new("g","Group","group"),new("a","A","type","Assets/A.cs",Parent:"g"),new("b","B","type","Assets/B.cs")], [new("a","b","calls","Assets/A.cs",1,"call","test"),new("b","a","calls","Assets/B.cs",1,"call","test")], []);
            Test("Inherited scope and explicit override", () => { var intent = new Intent(new() { ["g"]="protected", ["b"]="editable" }, []); Assert(Policy.Check(graph,intent,["Assets/A.cs"]).Any(i=>i.Code=="scope:a")); intent.Scopes["a"]="editable"; Assert(Policy.Check(graph,intent,["Assets/A.cs"]).Count==0); });
            Test("Deleted protected nodes retain scope evidence", () => { var intent = new Intent(new() { ["g"]="protected" }, []); Policy.Anchor(intent,graph); var removed=new Graph(graph.Nodes.Where(n=>n.Id!="a").ToList(),[],[]); Assert(Policy.Check(removed,intent,["Assets/A.cs"]).Any(i=>i.Code=="scope:a")); intent.Scopes.Clear(); Assert(Policy.Check(removed,intent,["Assets/A.cs"]).Count==0); });
            Test("Prohibited, required, direction and configured cycles", () => { Assert(Policy.Check(graph,Intent.Empty).Count==0); var intent = new Intent(new(), [new("p","a","b","calls","prohibited"),new("d","a","b","calls","direction"),new("c","a","b","calls","acyclic"),new("r","a","b","references","required")]); Assert(Policy.Check(graph,intent).Count==4); Assert(Policy.Check(graph,intent).Single(i=>i.Code=="r").Severity=="warning"); });
            Test("Impact traversal terminates on cycles and honors depth", () => { Assert(Policy.Impact("a", graph.Edges,1,false).SetEquals(["b"])); Assert(Policy.Impact("a", graph.Edges,100,true).SetEquals(["a","b"])); });
            Test("Path traversal and invalid policy rejected", () => { Assert(!Data.SafePath(root,"../secret",out _)); Assert(!Data.SafePath(root,Path.GetFullPath("/secret"),out _)); Assert(Data.SafePath(root,"Assets/A.cs",out _)); try { Policy.Validate(new(new(){["a"]="delete"},[])); throw new Exception("accepted"); } catch(ArgumentException) {} });
            Test("Presentation persists separately from actual graph", () => { var before=Normal(graph); Write(".architecture/presentation.json","{\"nodes\":{\"a\":{\"x\":8,\"note\":\"<script>bad</script>\"}}}"); Assert(Normal(graph)==before); Assert(Data.Read<JsonElement>(Path.Combine(root,".architecture/presentation.json")).GetProperty("nodes").GetProperty("a").GetProperty("x").GetInt32()==8); });
            var git = new GitService(root);
            void Git(params string[] a) { var r=git.Run(a); if(r.Code!=0) throw new Exception(r.Text); }
            Test("Git clean, staged, unstaged, untracked and rename", () => { Git("init","-b","main"); Git("config","user.name","Architecture Tests"); Git("config","user.email","test@example.invalid"); Write(".gitignore",".architecture/local/\n"); Git("add","."); Git("commit","-m","initial"); Assert(git.Status().Changes.Count==0); Write("file.txt","first\n"); Assert(git.Status().Changes.Any(c=>c.Index=="?")); Git("add","file.txt"); Assert(git.Status().Changes.Any(c=>c.Index=="A")); Write("file.txt","base\n"); Assert(git.Status().Changes.Any(c=>c.Worktree=="M")); Git("add","."); Git("commit","-m","base"); Git("mv","file.txt","renamed.txt"); Assert(git.Status().Changes.Any(c=>c.OldPath=="file.txt")); Git("commit","-am","rename"); });
            Test("Real divergent branches, merge base and confirmed merge conflict", () => { Git("checkout","-b","left"); Write("renamed.txt","left\n"); Git("commit","-am","left"); Git("checkout","main"); Git("checkout","-b","right"); Write("renamed.txt","right\n"); Git("commit","-am","right"); Assert(git.Compare("left","right").Files.Contains("renamed.txt")); var before=git.Status(); var result=JsonSerializer.SerializeToElement(git.Preview("left","right",graph),Data.Json); Assert(result.GetProperty("confirmedTextConflict").GetBoolean()); Assert(git.Status().Branch==before.Branch && git.Status().Changes.Count==0); Assert(File.ReadAllText(Path.Combine(root,"renamed.txt"))=="right\n"); });
            Test("Git malicious ref rejected and missing baseline reported", () => { Assert(git.Compare("--output=evil","main").Error!=null); Assert(git.Compare("absent","main").Error!=null); });
            Test("Missing repository degrades gracefully", () => { var sub=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString()); Directory.CreateDirectory(sub); try { Assert(new GitService(sub).Status().Error!=null); } finally { Directory.Delete(sub); } });
        }
        finally
        {
            if (root.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(root).StartsWith("architecture-tests-", StringComparison.Ordinal))
            {
                try { foreach (var file in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)) File.SetAttributes(file,FileAttributes.Normal); Directory.Delete(root,true); }
                catch(Exception ex) when (ex is IOException or UnauthorizedAccessException) { Console.WriteLine("Test cleanup deferred: " + root); }
            }
        }
        Console.WriteLine($"{passed} passed; {failed} failed."); return failed == 0 ? 0 : 1;
    }
}
