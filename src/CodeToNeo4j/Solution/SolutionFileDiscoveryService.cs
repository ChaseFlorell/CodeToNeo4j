using System.IO.Abstractions;
using System.Text.RegularExpressions;
using CodeToNeo4j.FileSystem;

namespace CodeToNeo4j.Solution;

public partial class SolutionFileDiscoveryService(
    IFileService fileService,
    IFileSystem fileSystem) : ISolutionFileDiscoveryService
{
    public IEnumerable<ProcessedFile> GetFilesToProcess(FileInfo sln,
        Microsoft.CodeAnalysis.Solution solution,
        IEnumerable<string> includeExtensions)
    {
        var extensions = includeExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var solutionFiles = new Dictionary<string, ProcessedFile>(StringComparer.OrdinalIgnoreCase);

        // 1. Get all documents from MSBuild
        foreach (var project in solution.Projects)
        {
            // Skip outer multi-target wrapper projects (they have 0 documents and no useful data)
            if (!project.Documents.Any() && !project.AdditionalDocuments.Any())
            {
                continue;
            }

            var tfm = ExtractTargetFramework(project.Name);

            // Regular Documents
            foreach (var doc in project.Documents)
            {
                var path = fileService.NormalizePath(doc.FilePath!);
                if (string.IsNullOrEmpty(path)) continue;
                if (!extensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) continue;

                if (solutionFiles.TryGetValue(path, out var existing))
                {
                    solutionFiles[path] = existing with { TargetFrameworks = MergeTfm(existing.TargetFrameworks, tfm) };
                }
                else
                {
                    solutionFiles[path] = new ProcessedFile(path, project.Id, doc.Id, MergeTfm(null, tfm));
                }
            }

            // Additional Documents
            foreach (var doc in project.AdditionalDocuments)
            {
                var path = fileService.NormalizePath(doc.FilePath!);
                if (string.IsNullOrEmpty(path)) continue;
                if (!extensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) continue;

                if (solutionFiles.TryGetValue(path, out var existing))
                {
                    solutionFiles[path] = existing with { TargetFrameworks = MergeTfm(existing.TargetFrameworks, tfm) };
                }
                else
                {
                    solutionFiles[path] = new ProcessedFile(path, project.Id, doc.Id, MergeTfm(null, tfm));
                }
            }
        }

        // 2. File system fallback for other files in the solution directory
        var solutionDir = sln.DirectoryName!;
        var allFilesOnDisk = fileSystem.Directory.EnumerateFiles(solutionDir, "*.*", SearchOption.AllDirectories);
        foreach (var fileOnDisk in allFilesOnDisk)
        {
            var normalizedPath = fileService.NormalizePath(fileOnDisk);
            if (IsExcluded(normalizedPath)) continue;
            if (!extensions.Any(ext => normalizedPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) continue;

            if (!solutionFiles.ContainsKey(normalizedPath))
            {
                solutionFiles[normalizedPath] = new ProcessedFile(normalizedPath, null, null);
            }
        }

        return solutionFiles.Values;
    }

    internal static string? ExtractTargetFramework(string projectName)
    {
        var match = TfmRegex().Match(projectName);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static IReadOnlySet<string>? MergeTfm(IReadOnlySet<string>? existing, string? tfm)
    {
        if (tfm is null) return existing;

        var set = existing is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        set.Add(tfm);
        return set;
    }

    private static bool IsExcluded(string path) =>
        path.Split('/')
            .Any(p => p.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                      p.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                      p.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                      p.Equals(".idea", StringComparison.OrdinalIgnoreCase) ||
                      p.Equals("node_modules", StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"\(([^)]+)\)$")]
    private static partial Regex TfmRegex();
}