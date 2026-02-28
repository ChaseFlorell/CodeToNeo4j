using System.Diagnostics;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using CodeToNeo4j.FileSystem;

namespace CodeToNeo4j.Git;

public class GitService(IFileService fileService, IFileSystem fileSystem, ILogger<GitService> logger) : IGitService
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
