using System.IO.Abstractions;
using System.Text.Json;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.FileHandlers;

public class PackageJsonHandler(IFileSystem fileSystem, ITextSymbolMapper textSymbolMapper, ILogger<PackageJsonHandler> logger)
    : PackageDependencyHandlerBase(fileSystem, textSymbolMapper)
{
    public override string FileExtension => "package.json";

    public override bool CanHandle(string filePath)
        => _fileSystem.Path.GetFileName(filePath).Equals("package.json", StringComparison.OrdinalIgnoreCase);

    internal static string? NormalizeRepositoryUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        url = url.Trim();

        // Apply the first matching prefix normalisation
        foreach (var (from, to) in RepositoryPrefixNormalizations)
        {
            if (url.StartsWith(from, StringComparison.OrdinalIgnoreCase))
            {
                url = to + url[from.Length..];
                break;
            }
        }

        // Strip embedded credentials (e.g. https://org@dev.azure.com/… or https://user:token@host/…)
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.UserInfo))
        {
            url = url.Replace($"{uri.UserInfo}@", string.Empty, StringComparison.Ordinal);
        }

        // Strip trailing .git
        if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            url = url[..^4];
        }

        return url;
    }

    protected override async Task<FileResult> HandleFile(
        TextDocument? document,
        Compilation? compilation,
        string? repoKey,
        string fileKey,
        string filePath,
        string relativePath,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Accessibility minAccessibility)
    {
        var fileNamespace = _fileSystem.Path.GetDirectoryName(relativePath)?.Replace('\\', '/');

        if (Accessibility.Public < minAccessibility)
        {
            return new FileResult(fileNamespace, fileKey);
        }

        var content = await GetContent(document, filePath).ConfigureAwait(false);
        var packageDir = _fileSystem.Path.GetDirectoryName(filePath) ?? string.Empty;
        var urlNodes = new List<UrlNode>();

        try
        {
            using var jsonDoc = JsonDocument.Parse(content);
            var root = jsonDoc.RootElement;

            await ExtractDependencySection(root, "dependencies", fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, packageDir, urlNodes).ConfigureAwait(false);
            await ExtractDependencySection(root, "devDependencies", fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, packageDir, urlNodes).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            logger.LogWarning("Failed to parse package.json: {FilePath}", filePath);
        }

        return new FileResult(fileNamespace, fileKey, urlNodes.Count > 0 ? urlNodes : null);
    }

    private readonly IFileSystem _fileSystem = fileSystem;

    private async Task ExtractDependencySection(
        JsonElement root,
        string sectionName,
        string fileKey,
        string relativePath,
        string? fileNamespace,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        string packageDir,
        List<UrlNode> urlNodes)
    {
        if (!root.TryGetProperty(sectionName, out var section) || section.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var prop in section.EnumerateObject())
        {
            var name = prop.Name;
            var version = prop.Value.GetString();

            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            AddDependency(name, version, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer);
            await CollectNpmUrls(name, packageDir, urlNodes).ConfigureAwait(false);
        }
    }

    private async Task CollectNpmUrls(string name, string packageDir, List<UrlNode> urlNodes)
    {
        var metaPath = ResolvePackageMetadataPath(name, packageDir);
        if (metaPath is null)
        {
            return;
        }

        try
        {
            var content = await _fileSystem.File.ReadAllTextAsync(metaPath).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var depKey = $"pkg:{name}";

            if (root.TryGetProperty("homepage", out var homepage))
            {
                var url = homepage.GetString()?.Trim();
                if (!string.IsNullOrEmpty(url))
                {
                    urlNodes.Add(new UrlNode(depKey, $"url:{url}", url));
                }
            }

            if (root.TryGetProperty("repository", out var repository))
            {
                var repoUrl = repository.ValueKind == JsonValueKind.String
                    ? NormalizeRepositoryUrl(repository.GetString())
                    : repository.ValueKind == JsonValueKind.Object && repository.TryGetProperty("url", out var urlProp)
                        ? NormalizeRepositoryUrl(urlProp.GetString())
                        : null;

                if (!string.IsNullOrEmpty(repoUrl))
                {
                    urlNodes.Add(new UrlNode(depKey, $"url:{repoUrl}", repoUrl));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read npm package metadata for: {PackageName}", name);
        }
    }

    private string? ResolvePackageMetadataPath(string name, string packageDir)
    {
        // npm / yarn v1: node_modules/<pkg>/package.json
        var npmPath = _fileSystem.Path.Combine(packageDir, "node_modules", name, "package.json");
        if (_fileSystem.File.Exists(npmPath))
        {
            return npmPath;
        }

        // pnpm virtual store: node_modules/.pnpm/<pkg>@<version>/node_modules/<pkg>/package.json
        // Scoped packages (e.g. @types/node) use '+' instead of '/' in the .pnpm entry name
        var pnpmStoreDir = _fileSystem.Path.Combine(packageDir, "node_modules", ".pnpm");
        if (_fileSystem.Directory.Exists(pnpmStoreDir))
        {
            var pnpmEntryName = name.Replace("/", "+", StringComparison.Ordinal);
            var prefix = $"{pnpmEntryName}@";
            var match = _fileSystem.Directory.EnumerateDirectories(pnpmStoreDir)
                .FirstOrDefault(d => _fileSystem.Path.GetFileName(d).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                var pnpmPath = _fileSystem.Path.Combine(match, "node_modules", name, "package.json");
                if (_fileSystem.File.Exists(pnpmPath))
                {
                    return pnpmPath;
                }
            }
        }

        return null;
    }

    // Ordered prefix replacements: first match wins.
    // SSH and shorthand entries are listed before the generic git+ / git:// fallbacks.
    private static readonly (string From, string To)[] RepositoryPrefixNormalizations =
    [
        // Shorthand notations  (e.g. "github:user/repo")
        ("github:",    "https://github.com/"),
        ("gitlab:",    "https://gitlab.com/"),
        ("bitbucket:", "https://bitbucket.org/"),

        // git+ssh variants  (e.g. "git+ssh://git@github.com/user/repo.git")
        ("git+ssh://git@github.com/",       "https://github.com/"),
        ("git+ssh://git@gitlab.com/",       "https://gitlab.com/"),
        ("git+ssh://git@bitbucket.org/",    "https://bitbucket.org/"),
        ("git+ssh://git@dev.azure.com/",    "https://dev.azure.com/"),

        // Plain ssh variants  (e.g. "ssh://git@github.com/user/repo.git")
        ("ssh://git@github.com/",           "https://github.com/"),
        ("ssh://git@gitlab.com/",           "https://gitlab.com/"),
        ("ssh://git@bitbucket.org/",        "https://bitbucket.org/"),
        ("ssh://git@dev.azure.com/",        "https://dev.azure.com/"),

        // git+ wrapper over HTTPS / HTTP  (e.g. "git+https://github.com/…" or Azure DevOps tokens)
        ("git+https://", "https://"),
        ("git+http://",  "http://"),

        // Bare git:// protocol  (e.g. "git://github.com/user/repo.git")
        ("git://", "https://"),
    ];
}
