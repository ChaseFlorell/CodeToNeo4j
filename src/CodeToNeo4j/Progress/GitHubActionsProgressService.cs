using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Progress;

/// <summary>
/// Progress reporting for GitHub Actions using workflow commands.
/// </summary>
public class GitHubActionsProgressService(ILogger<GitHubActionsProgressService> logger) : IProgressService
{
    public void ReportProgress(int current, int total, string filePath)
    {
        double progress = (double)current / total;
        // GitHub Actions doesn't have a direct progress bar command, but we can print specifically formatted lines.
        // Some users use notice commands, but we'll stick to clear info logs for now.
        logger.LogInformation("::notice::[Progress: {Percentage:p2}] Processing {FilePath} ({Current}/{Total})", progress, filePath, current, total);
    }
}
