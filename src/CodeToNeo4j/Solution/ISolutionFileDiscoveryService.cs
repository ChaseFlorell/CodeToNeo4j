namespace CodeToNeo4j.Solution;

public interface ISolutionFileDiscoveryService
{
    IEnumerable<ProcessedFile> GetFilesToProcess(FileInfo sln,
        Microsoft.CodeAnalysis.Solution solution,
        IEnumerable<string> includeExtensions);
}
