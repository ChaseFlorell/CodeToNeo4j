namespace CodeToNeo4j.Git;

public interface IGitService
{
    ValueTask<HashSet<string>> GetChangedFiles(string diffBase, string workingDirectory);
}