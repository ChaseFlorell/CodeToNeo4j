using System.Diagnostics;

namespace CodeToNeo4j.Console;

public interface IGitService
{
    Task<HashSet<string>> GetChangedCsFilesAsync(string diffBase, string repoRoot);
}

public class GitService(IFileService fileService) : IGitService
{
    public async Task<HashSet<string>> GetChangedCsFilesAsync(string diffBase, string repoRoot)
    {
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
            throw new Exception($"git diff failed: {err}");

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var rel = line.Trim();
            if (!rel.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                continue;

            var fullPath = fileService.NormalizePath(Path.Combine(repoRoot, rel));
            set.Add(fullPath);
        }

        return set;
    }
}
