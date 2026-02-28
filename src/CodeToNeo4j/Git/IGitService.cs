namespace CodeToNeo4j.Git;

public interface IGitService
{
    ValueTask<HashSet<string>> GetChangedCsFiles(string diffBase, string workingDirectory);
}