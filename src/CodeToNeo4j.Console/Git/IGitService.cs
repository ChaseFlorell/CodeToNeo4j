namespace CodeToNeo4j.Console.Git;

public interface IGitService
{
    Task<HashSet<string>> GetChangedCsFilesAsync(string diffBase, string workingDirectory);
}