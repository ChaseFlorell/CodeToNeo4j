using System.Diagnostics;
using System.IO.Abstractions;
using CodeToNeo4j.FileSystem;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.VersionControl;

public class GitService(IFileService fileService, IFileSystem fileSystem, ILogger<GitService> logger) : IVersionControlService
{
    public async ValueTask<GitDiffResult> GetChangedFiles(string diffBase, string workingDirectory, IEnumerable<string> includeExtensions)
    {
        var repoRoot = await GetGitRoot(workingDirectory);
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
        var output = await p.StandardOutput.ReadToEndAsync();
        var err = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();

        if (p.ExitCode != 0)
        {
            logger.LogError("git diff failed: {Error}", err);
            throw new Exception($"git diff failed: {err}");
        }

        var modifiedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deletedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            var status = parts[0];
            var rel = parts[1].Trim();

            if (!includeExtensions.Any(ext => rel.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
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
        return new GitDiffResult(modifiedSet, deletedSet);
    }

    public async ValueTask<FileMetadata> GetFileMetadata(string filePath, string workingDirectory)
    {
        var repoRoot = await GetGitRoot(workingDirectory);
        var relPath = fileSystem.Path.GetRelativePath(repoRoot, filePath);

        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"log --format=\"%an <%ae>|%aI|%H|%D\" -- \"{relPath}\"",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var p = Process.Start(psi) ?? throw new Exception($"Failed to start git log for {filePath}");
        var output = await p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync();

        if (p.ExitCode != 0)
        {
            return new FileMetadata(DateTimeOffset.MinValue, DateTimeOffset.MinValue, Array.Empty<AuthorMetadata>(), Array.Empty<string>(), Array.Empty<string>());
        }

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
        {
            return new FileMetadata(DateTimeOffset.MinValue, DateTimeOffset.MinValue, Array.Empty<AuthorMetadata>(), Array.Empty<string>(), Array.Empty<string>());
        }

        var authorMap = new Dictionary<string, (DateTimeOffset first, DateTimeOffset last, int count)>();
        DateTimeOffset created = DateTimeOffset.MaxValue;
        DateTimeOffset lastModified = DateTimeOffset.MinValue;
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

        return new FileMetadata(created, lastModified, authors, commits, tags);
    }

    private async ValueTask<string> GetGitRoot(string workingDirectory)
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
        var output = await p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync();

        if (p.ExitCode != 0)
        {
            throw new Exception("Could not find git repository root.");
        }

        return output.Trim();
    }
}
