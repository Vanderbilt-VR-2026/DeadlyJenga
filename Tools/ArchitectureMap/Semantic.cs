using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Xml.Linq;

namespace ArchitectureMap;

public static class Semantic
{
    static readonly Lazy<List<MetadataReference>> FallbackReferences = new(() => ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "").Split(Path.PathSeparator).Where(File.Exists).Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList());
    public static string ContextHash(string root) => Data.Hash(string.Join("|", Directory.EnumerateFiles(root, "*.csproj").Order().Select(p => File.ReadAllText(p))) + string.Join("|", Discovery.Files(Path.Combine(root, "Library/ScriptAssemblies")).Where(p => p.EndsWith(".dll")).Select(p => p + File.GetLastWriteTimeUtc(p).Ticks)));
    public static Graph Analyze(string root, List<Node> scripts, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (scripts.Count == 0) return new([], [], []);
        var nodes = new Dictionary<string, Node>(); var edges = new List<Edge>(); var issues = new List<Issue>();
        var byPath = scripts.ToDictionary(n => n.Path); var assignments = new Dictionary<string, (string Assembly, string[] Defines, List<MetadataReference> Refs)>();
        foreach (var project in Directory.EnumerateFiles(root, "*.csproj"))
        {
            try
            {
                var xml = XDocument.Load(project); var elements = xml.Descendants().ToList();
                if (!elements.Any(e => e.Name.LocalName == "ProjectCapability" && (string?)e.Attribute("Include") == "Unity")) continue;
                var name = elements.FirstOrDefault(e => e.Name.LocalName == "AssemblyName")?.Value ?? Path.GetFileNameWithoutExtension(project);
                var defines = (elements.FirstOrDefault(e => e.Name.LocalName == "DefineConstants")?.Value ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries);
                var refs = new List<MetadataReference>();
                foreach (var hint in elements.Where(e => e.Name.LocalName == "HintPath"))
                {
                    var p = Path.GetFullPath(Path.Combine(root, hint.Value.Replace('\\', Path.DirectorySeparatorChar)));
                    // Read metadata only; never execute project imports, analyzers or generators.
                    if (File.Exists(p) && p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) try { refs.Add(MetadataReference.CreateFromFile(p)); } catch (BadImageFormatException) { }
                }
                foreach (var file in elements.Where(e => e.Name.LocalName == "Compile"))
                { var path = ((string?)file.Attribute("Include"))?.Replace('\\', '/'); if (path != null && byPath.ContainsKey(path)) assignments[path] = (name, defines, refs); }
            }
            catch (Exception ex) when (ex is System.Xml.XmlException or IOException) { issues.Add(new("project-context", "Could not read Unity project metadata: " + Path.GetFileName(project))); }
        }
        var fallback = ((string)"Unassigned", new[] { "UNITY_EDITOR", "UNITY_6000_3_OR_NEWER" }, FallbackReferences.Value);
        foreach (var s in scripts) assignments.TryAdd(s.Path, fallback);
        var compilations = new List<(CSharpCompilation C, bool Exact)>();
        foreach (var group in scripts.GroupBy(s => assignments[s.Path].Assembly))
        {
            var config = assignments[group.First().Path];
            var trees = group.Select(n => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, n.Path)), new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: config.Defines), n.Path)).ToList();
            var compilation = CSharpCompilation.Create(group.Key, trees, config.Refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            compilations.Add((compilation, group.Key != "Unassigned"));
        }
        var symbolIds = new Dictionary<string, string>();
        string Key(ISymbol symbol) => symbol.ContainingAssembly?.Name + ":" + (symbol.OriginalDefinition.GetDocumentationCommentId() ?? symbol.ToDisplayString());
        foreach (var (compilation, exact) in compilations)
        foreach (var tree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var model = compilation.GetSemanticModel(tree); var asset = byPath[tree.FilePath];
            foreach (var syntax in tree.GetRoot(cancellationToken).DescendantNodes().Where(n => n is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax or BaseMethodDeclarationSyntax or PropertyDeclarationSyntax or EventDeclarationSyntax || n is VariableDeclaratorSyntax v && v.Parent?.Parent is EventFieldDeclarationSyntax))
            {
                var symbol = model.GetDeclaredSymbol(syntax, cancellationToken); if (symbol == null || symbol.IsImplicitlyDeclared) continue;
                var key = Key(symbol); var anchors = symbol.DeclaringSyntaxReferences.Select(r => byPath.GetValueOrDefault(r.SyntaxTree.FilePath)?.Id).Where(x => x != null).Order().ToArray();
                var id = anchors.FirstOrDefault() + ":" + key;
                symbolIds[key] = id;
                var kind = symbol is INamedTypeSymbol ? "type" : symbol is IMethodSymbol ? "method" : symbol is IEventSymbol ? "event" : "property";
                nodes.TryAdd(id, new(id, symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), kind, tree.FilePath, syntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1, asset.Id, asset.Group, exact ? "verified" : "inferred"));
                edges.Add(new(asset.Id, id, "contains", tree.FilePath, nodes[id].Line, "C# declaration", "Roslyn syntax"));
            }
        }
        string Target(ISymbol symbol)
        {
            var key = Key(symbol); if (symbolIds.TryGetValue(key, out var id)) return id;
            id = "external:" + key;
            nodes.TryAdd(id, new(id, symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), "external", Status: "unknown")); return id;
        }
        foreach (var (compilation, exact) in compilations)
        {
            var diagnostics = compilation.GetDiagnostics(cancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            if (diagnostics.Length > 0) issues.Add(new("semantic-context", $"{compilation.AssemblyName}: {diagnostics.Length} compiler diagnostics; affected bindings are not verified."));
            if (!exact) issues.Add(new("fallback-context", "Unity assembly metadata unavailable for some scripts; fallback semantic relationships are inferred."));
            foreach (var tree in compilation.SyntaxTrees)
            {
                cancellationToken.ThrowIfCancellationRequested();
            var model = compilation.GetSemanticModel(tree); var asset = byPath[tree.FilePath];
                foreach (var syntax in tree.GetRoot(cancellationToken).DescendantNodes())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var kind = syntax switch
                    {
                        InvocationExpressionSyntax => "calls",
                        ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax => "instantiates",
                        BaseTypeSyntax => "inherits",
                        AssignmentExpressionSyntax a when a.IsKind(SyntaxKind.AddAssignmentExpression) => "subscribes",
                        AssignmentExpressionSyntax a when a.IsKind(SyntaxKind.SubtractAssignmentExpression) => "unsubscribes",
                        TypeSyntax t when t.Parent is VariableDeclarationSyntax v && v.Type == t || t.Parent is ParameterSyntax p && p.Type == t || t.Parent is PropertyDeclarationSyntax prop && prop.Type == t || t.Parent is MethodDeclarationSyntax method && method.ReturnType == t => "depends",
                        MemberAccessExpressionSyntax access when access.Parent is not InvocationExpressionSyntax => "references",
                        _ => null
                    };
                    if (kind == null) continue;
                    var bind = syntax is BaseTypeSyntax b ? b.Type : syntax is AssignmentExpressionSyntax assignment ? assignment.Left : syntax;
                    var symbol = model.GetSymbolInfo(bind, cancellationToken).Symbol;
                    var line = syntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    if (symbol == null || symbol is ITypeSymbol errorType && errorType.TypeKind == TypeKind.Error)
                    { issues.Add(new("unresolved-symbol", "Symbol could not be bound at " + tree.FilePath + ":" + line, asset.Id, Severity: "info")); continue; }
                    if (kind is "subscribes" or "unsubscribes" && symbol is not IEventSymbol) continue;
                    if (kind == "references" && symbol is not (IFieldSymbol or IPropertySymbol or IEventSymbol)) continue;
                    var caller = model.GetEnclosingSymbol(syntax.SpanStart);
                    while (caller != null && !symbolIds.ContainsKey(Key(caller))) caller = caller.ContainingSymbol;
                    var source = caller == null ? asset.Id : symbolIds[Key(caller)];
                    var target = Target(symbol);
                    var hasError = diagnostics.Any(d => d.Location.SourceTree == tree && d.Location.SourceSpan.IntersectsWith(syntax.Span));
                    var status = exact && !hasError ? "verified" : "inferred";
                    var evidence = kind == "calls" && symbol is IMethodSymbol m && (m.IsVirtual || m.IsAbstract || m.ContainingType.TypeKind == TypeKind.Interface) ? "Compile-time target; runtime dispatch may differ" : "Statically bound " + symbol.Kind;
                    edges.Add(new(source, target, kind, tree.FilePath, line, evidence, "Roslyn semantic", status, syntax.GetLocation().GetLineSpan().StartLinePosition.Character + 1));
                    if (symbol.ContainingType != null && caller?.ContainingType != null)
                        edges.Add(new(Target(caller.ContainingType), Target(symbol.ContainingType), "depends", tree.FilePath, line, evidence, "Roslyn semantic", status, syntax.GetLocation().GetLineSpan().StartLinePosition.Character + 1));
                }
                foreach (var node in nodes.Values.Where(n => n.Path == tree.FilePath && n.Kind is "method" or "property" or "event").ToArray())
                {
                    var syntax = tree.GetRoot(cancellationToken).FindToken(tree.GetText().Lines[node.Line - 1].Start).Parent;
                    var type = syntax?.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
                    var symbol = type == null ? null : model.GetDeclaredSymbol(type);
                    if (symbol != null) nodes[node.Id] = node with { Parent = Target(symbol) };
                }
            }
        }
        issues.Add(new("static-limit", "Reflection, runtime registration, dynamic loading, network behavior and UnityEvent callback names are not resolved as calls.", Severity: "info"));
        return new(nodes.Values.ToList(), edges, issues);
    }
}
