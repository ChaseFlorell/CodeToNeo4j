namespace CodeToNeo4j.Console.Progress;

/// <summary>
/// Interface for reporting progress of the ingestion process.
/// </summary>
public interface IProgressService
{
    /// <summary>
    /// Reports progress for a specific file being processed.
    /// </summary>
    /// <param name="current">The current file count being processed.</param>
    /// <param name="total">The total number of files to be processed.</param>
    /// <param name="filePath">The path of the file being processed.</param>
    void ReportProgress(int current, int total, string filePath);
}
