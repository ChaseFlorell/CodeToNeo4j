using System.CommandLine;
using Microsoft.Build.Locator;
using Neo4j.Driver;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Security.Cryptography;

namespace CodeToNeo4j.Console;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var slnOption = new Option<FileInfo>("--sln") { IsRequired = true };
        var neo4JOption = new Option<string>("--neo4j", () => "bolt://localhost:7687");
        var userOption = new Option<string>("--user", () => "neo4j");
        var databaseOption = new Option<string>("--database", () => "neo4j");
        var passOption = new Option<string>("--pass") { IsRequired = true };
        var repoKeyOption = new Option<string>("--repoKey") { IsRequired = true };
        var diffBaseOption = new Option<string?>("--diffBase", description: "Optional git base ref for incremental indexing, e.g. origin/main");
        var batchSizeOption = new Option<int>("--batchSize", () => 500);

        var root = new RootCommand("Index C# solution into Neo4j via Roslyn")
        {
            slnOption, neo4JOption, userOption, passOption, repoKeyOption, diffBaseOption, batchSizeOption, databaseOption
        };

        root.SetHandler((sln, neo4J, user, pass, repoKey, diffBase, batchSize, databaseName) => Handle(sln, neo4J, user, pass, repoKey, diffBase, databaseName, batchSize),
            slnOption,
            neo4JOption,
            userOption,
            passOption,
            repoKeyOption,
            diffBaseOption,
            batchSizeOption,
            databaseOption);

        return await root.InvokeAsync(args);
    }

    private static async Task EnsureNeo4JSchemaAsync(IDriver driver, string databaseName)
    {
        // Neo4j schema operations are idempotent with IF NOT EXISTS
        // and are safe to run every time at startup.
        await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));

        var statements = new[]
        {
            // Constraints
            """
            CREATE CONSTRAINT project_key IF NOT EXISTS
            FOR (p:Project) REQUIRE p.key IS UNIQUE
            """,
            """
            CREATE CONSTRAINT file_key IF NOT EXISTS
            FOR (f:File) REQUIRE f.key IS UNIQUE
            """,
            """
            CREATE CONSTRAINT symbol_key IF NOT EXISTS
            FOR (s:Symbol) REQUIRE s.key IS UNIQUE
            """,

            // Indexes
            """
            CREATE INDEX symbol_name IF NOT EXISTS
            FOR (s:Symbol) ON (s.name)
            """,
            """
            CREATE INDEX symbol_kind IF NOT EXISTS
            FOR (s:Symbol) ON (s.kind)
            """,
            """
            CREATE INDEX file_path IF NOT EXISTS
            FOR (f:File) ON (f.path)
            """,
            """
            CREATE INDEX symbol_fqn IF NOT EXISTS
            FOR (s:Symbol) ON (s.fqn)
            """,
            """
            CREATE INDEX symbol_fileKey IF NOT EXISTS
            FOR (s:Symbol) ON (s.fileKey)
            """
        };

        foreach (var cypher in statements)
        {
            await session.RunAsync(cypher);
        }
    }

    private static async Task Handle(FileInfo sln, string neo4J, string user, string pass, string repoKey, string? diffBase, string databaseName, int batchSize)
    {
        // Ensure MSBuild is available
        if (!MSBuildLocator.IsRegistered) MSBuildLocator.RegisterDefaults();

        var changedFiles = diffBase is null
            ? null
            : await GetChangedCsFilesAsync(diffBase, Directory.GetCurrentDirectory());

        await using var driver = GraphDatabase.Driver(new Uri(neo4J), AuthTokens.Basic(user, pass));
        await EnsureNeo4JSchemaAsync(driver, databaseName);
        
        await using var session = driver.AsyncSession();
        // Create or update Project
        await session.ExecuteWriteAsync(async tx => { await tx.RunAsync("MERGE (p:Project {key:$key}) SET p.name=$name, p.updatedAt=datetime()", new { key = repoKey, name = repoKey }); });

        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(e => { System.Console.Error.WriteLine($"Workspace warning: {e.Diagnostic.Message}"); });

        var solution = await workspace.OpenSolutionAsync(sln.FullName);

        var symbolBuffer = new List<SymbolRecord>(batchSize);
        var relBuffer = new List<RelRecord>(batchSize);

        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation is null) continue;

            foreach (var document in project.Documents)
            {
                if (!document.FilePath?.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ?? true) continue;

                var filePath = NormalizePath(document.FilePath!);
                if (changedFiles is not null && !changedFiles.Contains(filePath)) continue;

                var syntaxTree = await document.GetSyntaxTreeAsync();
                if (syntaxTree is null) continue;

                var rootNode = await syntaxTree.GetRootAsync();
                var semanticModel = compilation.GetSemanticModel(syntaxTree, ignoreAccessibility: true);

                var fileKey = $"{repoKey}:{filePath}";
                var fileHash = ComputeSha256(await File.ReadAllBytesAsync(filePath));

                // Upsert file node and link to project
                await session.ExecuteWriteAsync(async tx =>
                {
                    await tx.RunAsync(@"
MERGE (f:File {key:$fileKey})
SET f.path=$path, f.hash=$hash, f.updatedAt=datetime()
WITH f
MATCH (p:Project {key:$repoKey})
MERGE (p)-[:HAS_FILE]->(f)
", new { fileKey, path = filePath, hash = fileHash, repoKey });
                });

                // Delete prior symbols declared in this file (safe incremental)
                await session.ExecuteWriteAsync(async tx =>
                {
                    await tx.RunAsync(@"
MATCH (f:File {key:$fileKey})-[:DECLARES]->(s:Symbol)
DETACH DELETE s
", new { fileKey });
                });

                // Extract types and members
                foreach (var typeDecl in rootNode.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
                {
                    var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                    if (typeSymbol is null) continue;

                    var typeRec = ToSymbolRecord(repoKey, fileKey, filePath, typeSymbol, typeDecl.GetLocation());
                    symbolBuffer.Add(typeRec);

                    if (typeDecl is TypeDeclarationSyntax tds)
                    {
                        foreach (var member in tds.Members)
                        {
                            var memberSymbol = semanticModel.GetDeclaredSymbol(member);
                            if (memberSymbol is null) continue;

                            var memberRec = ToSymbolRecord(repoKey, fileKey, filePath, memberSymbol, member.GetLocation());
                            symbolBuffer.Add(memberRec);

                            relBuffer.Add(new RelRecord(FromKey: typeRec.Key, ToKey: memberRec.Key, RelType: "CONTAINS"));
                        }
                    }
                    else if (typeDecl is EnumDeclarationSyntax eds)
                    {
                        foreach (var member in eds.Members)
                        {
                            var memberSymbol = semanticModel.GetDeclaredSymbol(member);
                            if (memberSymbol is null) continue;

                            var memberRec = ToSymbolRecord(repoKey, fileKey, filePath, memberSymbol, member.GetLocation());
                            symbolBuffer.Add(memberRec);

                            relBuffer.Add(new RelRecord(FromKey: typeRec.Key, ToKey: memberRec.Key, RelType: "CONTAINS"));
                        }
                    }
                }

                // Flush in batches
                if (symbolBuffer.Count >= batchSize) await FlushAsync(session, repoKey, fileKey, symbolBuffer, relBuffer);

                System.Console.WriteLine($"Indexed {filePath}");
            }
        }

        // Flush remainder
        if (symbolBuffer.Count > 0) await FlushAsync(session, repoKey, fileKey: null, symbolBuffer, relBuffer);

        System.Console.WriteLine("Done.");
    }

    private static async Task FlushAsync(IAsyncSession session, string repoKey, string? fileKey, List<SymbolRecord> symbols, List<RelRecord> rels)
    {
        var symbolBatch = symbols.ToArray();
        var relBatch = rels.ToArray();
        symbols.Clear();
        rels.Clear();

        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(@"
UNWIND $symbols AS s
MERGE (n:Symbol {key:s.key})
SET n.name = s.name,
    n.kind = s.kind,
    n.fqn = s.fqn,
    n.accessibility = s.accessibility,
    n.fileKey = s.fileKey,
    n.filePath = s.filePath,
    n.startLine = s.startLine,
    n.endLine = s.endLine,
    n.updatedAt = datetime()
WITH n, s
MATCH (f:File {key:s.fileKey})
MERGE (f)-[:DECLARES]->(n)
", new { symbols = symbolBatch });

            await tx.RunAsync(@"
UNWIND $rels AS r
MATCH (a:Symbol {key:r.fromKey})
MATCH (b:Symbol {key:r.toKey})
CALL {
  WITH a, b, r
  // Relationship type is fixed in v1, safe to switch later
  MERGE (a)-[:CONTAINS]->(b)
}
RETURN count(*) AS created
", new { rels = relBatch });
        });
    }

    private static SymbolRecord ToSymbolRecord(string repoKey, string fileKey, string filePath, ISymbol symbol, Location loc)
    {
        var kind = symbol.Kind.ToString();
        var name = symbol.Name;

        var fqn = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var key = BuildStableSymbolKey(repoKey, symbol);

        var (startLine, endLine) = GetLineSpan(loc);

        return new SymbolRecord(
            Key: key,
            Name: name,
            Kind: kind,
            Fqn: fqn,
            Accessibility: symbol.DeclaredAccessibility.ToString(),
            FileKey: fileKey,
            FilePath: filePath,
            StartLine: startLine,
            EndLine: endLine
        );
    }

    private static string BuildStableSymbolKey(string repoKey, ISymbol symbol)
    {
        // This is stable enough for most solutions:
        // fully qualified name + signature-ish display, scoped to repoKey.
        var display = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        return $"{repoKey}:{display}";
    }

    private static (int startLine, int endLine) GetLineSpan(Location loc)
    {
        if (!loc.IsInSource) return (-1, -1);
        var span = loc.GetLineSpan();
        return (span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1);
    }

    private static string NormalizePath(string path)
    {
        // Normalize separators so keys match across platforms.
        var full = Path.GetFullPath(path);
        return full.Replace('\\', '/');
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<HashSet<string>> GetChangedCsFilesAsync(string diffBase, string repoRoot)
    {
        // Uses git to compute changed files. Works on macOS/Linux/Windows if git is installed.
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"diff --name-only {diffBase}...HEAD",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var p = System.Diagnostics.Process.Start(psi)!;
        var output = await p.StandardOutput.ReadToEndAsync();
        var err = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();

        if (p.ExitCode != 0)
            throw new Exception($"git diff failed: {err}");

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var rel = line.Trim();
            if (!rel.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                continue;

            var fullPath = NormalizePath(Path.Combine(repoRoot, rel));
            set.Add(fullPath);
        }

        return set;
    }

    private record SymbolRecord(
        string Key,
        string Name,
        string Kind,
        string Fqn,
        string Accessibility,
        string FileKey,
        string FilePath,
        int StartLine,
        int EndLine
    );

    private record RelRecord(string FromKey, string ToKey, string RelType);
}