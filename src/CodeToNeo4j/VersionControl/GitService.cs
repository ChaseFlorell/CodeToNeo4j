using System.Diagnostics;
using System.IO.Abstractions;
using CodeToNeo4j.FileSystem;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.VersionControl;

public class GitService(
    IFileService fileService,
    IFileSystem fileSystem,
    IGitLogParser logParser,
    IGitMetadataCache metadataCache,
    ILogger<GitService> logger) : IVersionControlService
{
    public async Task LoadMetadata(string workingDirectory, IEnumerable<string> includeExtensions)
    {
        var repoRoot = await GetGitRoot(workingDirectory).ConfigureAwait(false);
        var includedExtensionsSet = includeExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        logger.LogInformation("Pre-fetching git metadata for all files in the repository...");
        metadataCache.Clear();

        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "log HEAD --format=\"COMMIT|%an <%ae>|%aI|%H|%D\" --name-only",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var p = Process.Start(psi) ?? throw new Exception("Failed to start git log for metadata pre-fetch.");

        var fileHistory = new Dictionary<string, List<(string Author, DateTimeOffset Date, string Hash, string? Refs)>>(StringComparer.OrdinalIgnoreCase);

        string? currentAuthor = null;
        DateTimeOffset currentDate = default;
        string? currentHash = null;
        string? currentRefs = null;

        while (await p.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            if (line.StartsWith("COMMIT|"))
            {
                var parts = line.Split('|');
                if (parts.Length >= 5)
                {
                    currentAuthor = parts[1];
                    if (DateTimeOffset.TryParse(parts[2], out var d)) currentDate = d;
                    currentHash = parts[3];
                    currentRefs = string.IsNullOrWhiteSpace(parts[4]) ? null : parts[4];
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(line) || currentAuthor == null)
            {
                continue;
            }

            var relPath = line.Trim();
            if (!includedExtensionsSet.Any(ext => relPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var fullPath = fileService.NormalizePath(fileSystem.Path.Combine(repoRoot, relPath));
            if (!fileHistory.TryGetValue(fullPath, out var history))
            {
                history = [];
                fileHistory[fullPath] = history;
            }

            history.Add((currentAuthor, currentDate, currentHash!, currentRefs));
        }

        await p.WaitForExitAsync().ConfigureAwait(false);

        foreach (var kvp in fileHistory)
        {
            var metadata = logParser.BuildFileMetadata(kvp.Value);
            metadataCache.Set(kvp.Key, metadata);
        }

        logger.LogInformation("Loaded git metadata for {Count} files.", metadataCache.Count);
    }

    public async Task<DiffResult> GetChangedFiles(string diffBase, string workingDirectory, IEnumerable<string> includedExtensions)
    {
        var repoRoot = await GetGitRoot(workingDirectory).ConfigureAwait(false);
        var range = GetRange(diffBase);
        logger.LogDebug("Running git diff against {Range} in {RepoRoot}...", range, repoRoot);
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"diff --name-status {range}",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var p = Process.Start(psi) ?? throw new Exception("Failed to start git process.");
        var output = await p.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var err = await p.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await p.WaitForExitAsync().ConfigureAwait(false);

        if (p.ExitCode != 0)
        {
            logger.LogError("git diff failed: {Error}", err);
            throw new Exception($"git diff failed: {err}");
        }

        var modifiedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deletedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var includedExtensionsSet = includedExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            var status = parts[0];
            var rel = parts[1].Trim();

            if (!includedExtensionsSet.Any(ext => rel.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var fullPath = fileService.NormalizePath(fileSystem.Path.Combine(repoRoot, rel));

            if (status.StartsWith("D"))
            {
                deletedSet.Add(fullPath);
            }
            else
            {
                modifiedSet.Add(fullPath);
            }
        }

        logger.LogDebug("Found {ModifiedCount} modified and {DeletedCount} deleted files.", modifiedSet.Count, deletedSet.Count);

        return new DiffResult(modifiedSet, deletedSet, []);
    }

    public async Task<int> GetCommitCount(string range, string workingDirectory)
    {
        var repoRoot = await GetGitRoot(workingDirectory).ConfigureAwait(false);
        var diffRange = GetRange(range);
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"rev-list --count {diffRange}",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var p = Process.Start(psi) ?? throw new Exception("Failed to start git rev-list.");
        var output = await p.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        await p.WaitForExitAsync().ConfigureAwait(false);

        if (p.ExitCode != 0) return 0;
        return int.TryParse(output.Trim(), out var count) ? count : 0;
    }

    public async IAsyncEnumerable<IEnumerable<CommitMetadata>> GetCommitsBatched(string range, string workingDirectory, int batchSize)
    {
        var totalCount = await GetCommitCount(range, workingDirectory).ConfigureAwait(false);

        for (int skip = 0; skip < totalCount; skip += batchSize)
        {
            yield return await GetCommitBatch(range, workingDirectory, batchSize, skip).ConfigureAwait(false);
        }
    }

    public async Task<IEnumerable<CommitMetadata>> GetCommitBatch(string range, string workingDirectory, int batchSize, int skip)
    {
        var repoRoot = await GetGitRoot(workingDirectory).ConfigureAwait(false);
        var diffRange = GetRange(range);
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"log {diffRange} --max-count={batchSize} --skip={skip} --format=\"COMMIT|%H|#|%an|#|%ae|#|%aI|#|%s\" --name-status",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var p = Process.Start(psi) ?? throw new Exception("Failed to start git log batch.");
        var output = await p.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        await p.WaitForExitAsync().ConfigureAwait(false);

        if (p.ExitCode != 0) return [];

        return logParser.ParseCommits(output, repoRoot);
    }

    public async Task<FileMetadata> GetFileMetadata(string filePath, string workingDirectory)
    {
        if (metadataCache.TryGet(filePath, out var cached))
        {
            return cached;
        }

        var repoRoot = await GetGitRoot(workingDirectory).ConfigureAwait(false);
        var relPath = fileSystem.Path.GetRelativePath(repoRoot, filePath);

        logger.LogDebug("Cache miss for {FilePath}, running git log manually...", filePath);

        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"log HEAD --format=\"%an <%ae>|%aI|%H|%D\" -- \"{relPath}\"",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var p = Process.Start(psi) ?? throw new Exception($"Failed to start git log for {filePath}");
        var output = await p.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        await p.WaitForExitAsync().ConfigureAwait(false);

        if (p.ExitCode != 0)
        {
            return new FileMetadata(DateTimeOffset.MinValue, DateTimeOffset.MinValue, [], [], []);
        }

        var result = logParser.ParseSingleFileLog(output);
        metadataCache.Set(filePath, result);
        return result;
    }

    private static string GetRange(string diffBase) => diffBase.Contains("..") ? diffBase : $"{diffBase}...HEAD";

    private static async Task<string> GetGitRoot(string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "rev-parse --show-toplevel",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var p = Process.Start(psi) ?? throw new Exception("Failed to start git process to find repo root.");
        var output = await p.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        await p.WaitForExitAsync().ConfigureAwait(false);

        if (p.ExitCode != 0)
        {
            throw new Exception("Could not find git repository root.");
        }

        return output.Trim();
    }
}
