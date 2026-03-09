using System.IO.Abstractions;
using CodeToNeo4j.FileSystem;

namespace CodeToNeo4j.Solution;

public class SolutionFileDiscoveryService(
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
            // Regular Documents
            foreach (var doc in project.Documents)
            {
                var path = fileService.NormalizePath(doc.FilePath!);
                if (string.IsNullOrEmpty(path)) continue;
                if (!extensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) continue;

                if (!solutionFiles.ContainsKey(path))
                {
                    solutionFiles[path] = new ProcessedFile(path, project.Id, doc.Id);
                }
            }

            // Additional Documents
            foreach (var doc in project.AdditionalDocuments)
            {
                var path = fileService.NormalizePath(doc.FilePath!);
                if (string.IsNullOrEmpty(path)) continue;
                if (!extensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) continue;

                if (!solutionFiles.ContainsKey(path))
                {
                    solutionFiles[path] = new ProcessedFile(path, project.Id, doc.Id);
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

    private static bool IsExcluded(string path) =>
        path.Split('/')
            .Any(p => p.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                      p.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                      p.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                      p.Equals(".idea", StringComparison.OrdinalIgnoreCase) ||
                      p.Equals("node_modules", StringComparison.OrdinalIgnoreCase));
}