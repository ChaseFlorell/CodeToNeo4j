namespace CodeToNeo4j.Solution;

public interface ISolutionFileDiscoveryService
{
    IEnumerable<ProcessedFile> GetFilesToProcess(string rootDirectory,
        Microsoft.CodeAnalysis.Solution? solution,
        IEnumerable<string> includeExtensions);
}
