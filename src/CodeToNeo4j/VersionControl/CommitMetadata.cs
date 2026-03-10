namespace CodeToNeo4j.VersionControl;

public record CommitMetadata(
    string Hash,
    string AuthorName,
    string AuthorEmail,
    DateTimeOffset Date,
    string Message,
    IEnumerable<FileStatus> ChangedFiles);

public record FileStatus(string Path, bool IsDeleted);