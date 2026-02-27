using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Progress;

/// <summary>
/// Progress reporting for Azure DevOps using VSO logging commands.
/// </summary>
public class AzureDevOpsProgressService(ILogger<AzureDevOpsProgressService> logger) : IProgressService
{
    public void ReportProgress(int current, int total, string filePath)
    {
        double progressPercentage = (double)current / total * 100;
        // Azure DevOps specific progress command.
        // ##vso[task.setprogress value=50;]
        System.Console.WriteLine($"##vso[task.setprogress value={progressPercentage:0};]Processing {filePath}");
        logger.LogInformation("[Progress: {Percentage:p2}] Processing {FilePath} ({Current}/{Total})", (double)current / total, filePath, current, total);
    }
}
