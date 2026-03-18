using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Progress;

/// <summary>
/// Progress reporting for Azure DevOps using VSO logging commands.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Thin CI-environment console output wrapper; behaviour only meaningful inside Azure DevOps")]
public class AzureDevOpsProgressService(ILogger<AzureDevOpsProgressService> logger) : IProgressService
{
    public void ReportProgress(int current, int total, string filePath)
    {
        var progress = (double)current / total;
        // Azure DevOps specific progress command.
        // ##vso[task.setprogress value=50;]
        Console.WriteLine($"##vso[task.setprogress value={progress:0P};]Processing {filePath}");
        logger.LogInformation("[Progress: {Percentage:p2}] Processing {FilePath} ({Current}/{Total})", progress, filePath, current, total);
    }

    public void ProgressComplete() { }
}
