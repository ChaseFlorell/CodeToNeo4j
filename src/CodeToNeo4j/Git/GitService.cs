using System.Diagnostics;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using CodeToNeo4j.FileSystem;

namespace CodeToNeo4j.Git;

public class GitService(IFileService fileService, IFileSystem fileSystem, ILogger<GitService> logger) : IGitService
{
    public async Task<HashSet<string>> GetChangedCsFilesAsync(string diffBase, string workingDirectory)
    {
        var repoRoot = await GetGitRootAsync(workingDirectory);
        logger.LogDebug("Running git diff against {DiffBase} in {RepoRoot}...", diffBase, repoRoot);
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"diff --name-only {diffBase}...HEAD",
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

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var rel = line.Trim();
            if (!rel.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                continue;

            var fullPath = fileService.NormalizePath(fileSystem.Path.Combine(repoRoot, rel));
            set.Add(fullPath);
        }

        logger.LogDebug("Found {Count} changed .cs files.", set.Count);
        return set;
    }

    private async Task<string> GetGitRootAsync(string workingDirectory)
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
