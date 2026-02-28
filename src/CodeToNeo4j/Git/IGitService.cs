namespace CodeToNeo4j.Git;

public interface IGitService
{
    ValueTask<GitDiffResult> GetChangedFiles(string diffBase, string workingDirectory, IEnumerable<string> includeExtensions);
}

public record GitDiffResult(HashSet<string> ModifiedFiles, HashSet<string> DeletedFiles);