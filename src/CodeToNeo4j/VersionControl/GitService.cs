using System.Diagnostics;
using System.IO.Abstractions;
using CodeToNeo4j.FileSystem;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.VersionControl;

public class GitService(IFileService fileService, IFileSystem fileSystem, ILogger<GitService> logger) : IVersionControlService
{
    private readonly Dictionary<string, FileMetadata> _metadataCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task LoadMetadata(string workingDirectory, IEnumerable<string> includeExtensions)
    {
        var repoRoot = await GetGitRoot(workingDirectory).ConfigureAwait(false);
        var includedExtensionsSet = includeExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        logger.LogInformation("Pre-fetching git metadata for all files in the repository...");
        _metadataCache.Clear();

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

        // Temporary data structure for raw history
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

            if (string.IsNullOrWhiteSpace(line) || currentAuthor == null) continue;

            // This is a file name
            var relPath = line.Trim();
            if (!includedExtensionsSet.Any(ext => relPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) continue;

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
            var filePath = kvp.Key;
            var history = kvp.Value;

            var authorMap = new Dictionary<string, (DateTimeOffset first, DateTimeOffset last, int count)>();
            var created = DateTimeOffset.MaxValue;
            var lastModified = DateTimeOffset.MinValue;
            var hashes = new List<string>();
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var commit in history)
            {
                if (commit.Date < created) created = commit.Date;
                if (commit.Date > lastModified) lastModified = commit.Date;

                if (authorMap.TryGetValue(commit.Author, out var stats))
                {
                    var newFirst = commit.Date < stats.first ? commit.Date : stats.first;
                    var newLast = commit.Date > stats.last ? commit.Date : stats.last;
                    authorMap[commit.Author] = (newFirst, newLast, stats.count + 1);
                }
                else
                {
                    authorMap[commit.Author] = (commit.Date, commit.Date, 1);
                }

                hashes.Add(commit.Hash);

                if (commit.Refs != null)
                {
                    var refs = commit.Refs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var r in refs)
                    {
                        if (r.StartsWith("tag:"))
                        {
                            tags.Add(r[4..].Trim());
                        }
                    }
                }
            }

            var authors = authorMap.Select(m => new AuthorMetadata(m.Key, m.Value.first, m.Value.last, m.Value.count)).ToArray();
            _metadataCache[filePath] = new FileMetadata(created, lastModified, authors, [.. hashes], [.. tags]);
        }

        logger.LogInformation("Loaded git metadata for {Count} files.", _metadataCache.Count);
    }

    public async Task<DiffResult> GetChangedFiles(string diffBase, string workingDirectory, IEnumerable<string> includedExtensions)
    {
        var repoRoot = await GetGitRoot(workingDirectory).ConfigureAwait(false);
        logger.LogDebug("Running git diff against {DiffBase} in {RepoRoot}...", diffBase, repoRoot);
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"diff --name-status {diffBase}...HEAD",
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
                continue;

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

        var commits = new List<CommitMetadata>();
        if (!string.IsNullOrWhiteSpace(diffBase))
        {
            logger.LogDebug("Fetching commit details between {DiffBase} and HEAD...", diffBase);
            var commitPsi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"log {diffBase}...HEAD --format=\"%H|%an|%ae|%aI|%s\" --name-only",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var cp = Process.Start(commitPsi) ?? throw new Exception("Failed to start git log process.");
            var commitOutput = await cp.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await cp.WaitForExitAsync().ConfigureAwait(false);

            if (cp.ExitCode == 0)
            {
                var commitLines = commitOutput.Split('\n', StringSplitOptions.TrimEntries);
                CommitMetadata? currentCommit = null;
                var changedFiles = new List<string>();

                foreach (var line in commitLines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        if (currentCommit != null)
                        {
                            commits.Add(currentCommit with { ChangedFiles = [.. changedFiles] });
                            currentCommit = null;
                            changedFiles = [];
                        }
                        continue;
                    }

                    if (line.Contains('|'))
                    {
                        var parts = line.Split('|');
                        if (parts.Length >= 5)
                        {
                            if (currentCommit != null)
                            {
                                commits.Add(currentCommit with { ChangedFiles = [.. changedFiles] });
                                changedFiles = [];
                            }

                            if (DateTimeOffset.TryParse(parts[3], out var date))
                            {
                                currentCommit = new CommitMetadata(parts[0], parts[1], parts[2], date, parts[4], []);
                            }
                        }
                    }
                    else
                    {
                        if (currentCommit != null)
                        {
                            changedFiles.Add(fileService.NormalizePath(fileSystem.Path.Combine(repoRoot, line)));
                        }
                    }
                }

                if (currentCommit != null)
                {
                    commits.Add(currentCommit with { ChangedFiles = [.. changedFiles] });
                }
            }
        }

        return new DiffResult(modifiedSet, deletedSet, commits);
    }

    public async Task<FileMetadata> GetFileMetadata(string filePath, string workingDirectory)
    {
        if (_metadataCache.TryGetValue(filePath, out var cached))
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

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
        {
            return new FileMetadata(DateTimeOffset.MinValue, DateTimeOffset.MinValue, [], [], []);
        }

        var authorMap = new Dictionary<string, (DateTimeOffset first, DateTimeOffset last, int count)>();
        var created = DateTimeOffset.MaxValue;
        var lastModified = DateTimeOffset.MinValue;
        var commits = new List<string>();
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var parts = line.Split('|');
            if (parts.Length < 2) continue;

            var authorName = parts[0];
            if (!DateTimeOffset.TryParse(parts[1], out var commitDate)) continue;

            if (commitDate < created) created = commitDate;
            if (commitDate > lastModified) lastModified = commitDate;

            if (authorMap.TryGetValue(authorName, out var stats))
            {
                var newFirst = commitDate < stats.first ? commitDate : stats.first;
                var newLast = commitDate > stats.last ? commitDate : stats.last;
                authorMap[authorName] = (newFirst, newLast, stats.count + 1);
            }
            else
            {
                authorMap[authorName] = (commitDate, commitDate, 1);
            }

            if (parts.Length >= 3)
            {
                commits.Add(parts[2]);
            }

            if (parts.Length >= 4 && !string.IsNullOrWhiteSpace(parts[3]))
            {
                var refs = parts[3].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var r in refs)
                {
                    if (r.StartsWith("tag:"))
                    {
                        tags.Add(r[4..].Trim());
                    }
                }
            }
        }

        var authors = authorMap.Select(kvp => new AuthorMetadata(kvp.Key, kvp.Value.first, kvp.Value.last, kvp.Value.count)).ToArray();

        var result = new FileMetadata(created, lastModified, authors, [.. commits], [.. tags]);
        _metadataCache[filePath] = result;
        return result;
    }

    private async Task<string> GetGitRoot(string workingDirectory)
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
