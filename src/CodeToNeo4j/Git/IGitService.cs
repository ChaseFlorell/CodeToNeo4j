namespace CodeToNeo4j.Git;

public interface IGitService
{
    Task<HashSet<string>> GetChangedCsFilesAsync(string diffBase, string workingDirectory);
}