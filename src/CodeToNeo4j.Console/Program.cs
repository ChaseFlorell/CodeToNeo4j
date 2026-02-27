using System.CommandLine;
using Microsoft.Build.Locator;
using Neo4j.Driver;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Security.Cryptography;
using System.Reflection;

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

    private static string GetCypher(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"CodeToNeo4j.Console.Cypher.{name}.cypher");
        if (stream == null) throw new FileNotFoundException($"Cypher resource {name} not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static async Task VerifyNeo4JVersionAsync(IDriver driver)
    {
        await using var session = driver.AsyncSession();
        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(GetCypher("GetNeo4jVersion"));
            return await cursor.SingleAsync();
        });

        var versionString = result["version"].As<string>();
        if (string.IsNullOrWhiteSpace(versionString))
        {
            throw new NotSupportedException("Could not determine Neo4j version.");
        }

        if (Version.TryParse(versionString.Split('-')[0], out var version))
        {
            if (version.Major < 5)
            {
                throw new NotSupportedException($"Neo4j version {versionString} is not supported. Minimum required version is 5.0.");
            }
        }
        else
        {
            // Fallback for cases where version string might be unusual, but starts with a number
            if (char.IsDigit(versionString[0]) && int.TryParse(versionString[0].ToString(), out var major) && major < 5)
            {
                throw new NotSupportedException($"Neo4j version {versionString} is not supported. Minimum required version is 5.0.");
            }
        }
    }

    private static async Task EnsureNeo4JSchemaAsync(IDriver driver, string databaseName)
    {
        // Neo4j schema operations are idempotent with IF NOT EXISTS
        // and are safe to run every time at startup.
        await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));

        var schema = GetCypher("Schema");
        var statements = schema.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
        await VerifyNeo4JVersionAsync(driver);
        await EnsureNeo4JSchemaAsync(driver, databaseName);

        await using var session = driver.AsyncSession();
        // Create or update Project
        await session.ExecuteWriteAsync(async tx => { await tx.RunAsync(GetCypher("UpsertProject"), new { key = repoKey, name = repoKey }); });

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
                await session.ExecuteWriteAsync(async tx => { await tx.RunAsync(GetCypher("UpsertFile"), new { fileKey, path = filePath, hash = fileHash, repoKey }); });

                // Delete prior symbols declared in this file (safe incremental)
                await session.ExecuteWriteAsync(async tx => { await tx.RunAsync(GetCypher("DeletePriorSymbols"), new { fileKey }); });

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
            await tx.RunAsync(GetCypher("UpsertSymbols"), new { symbols = symbolBatch });

            await tx.RunAsync(GetCypher("MergeRelationships"), new { rels = relBatch });
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
}