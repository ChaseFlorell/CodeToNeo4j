using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Console.Progress;

/// <summary>
/// Default progress reporting for local command line environments.
/// </summary>
public class ConsoleProgressService(ILogger<ConsoleProgressService> logger) : IProgressService
{
    public void ReportProgress(int current, int total, string filePath)
    {
        double progress = (double)current / total;
        logger.LogInformation("[Progress: {Percentage:p2}] Processing {FilePath} ({Current}/{Total})", progress, filePath, current, total);
    }
}
