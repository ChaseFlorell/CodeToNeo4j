namespace CodeToNeo4j.VersionControl;

public interface IVersionControlService
{
    ValueTask<GitDiffResult> GetChangedFiles(string diffBase, string workingDirectory, IEnumerable<string> includeExtensions);
    ValueTask<FileMetadata> GetFileMetadata(string filePath, string workingDirectory);
}

public record GitDiffResult(HashSet<string> ModifiedFiles, HashSet<string> DeletedFiles);

public record AuthorMetadata(string Name, DateTimeOffset FirstCommit, DateTimeOffset LastCommit, int CommitCount);

public record FileMetadata(DateTimeOffset Created, DateTimeOffset LastModified, IEnumerable<AuthorMetadata> Authors, IEnumerable<string> Commits, IEnumerable<string> Tags);