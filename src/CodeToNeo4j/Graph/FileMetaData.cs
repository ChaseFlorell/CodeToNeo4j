using CodeToNeo4j.VersionControl;

namespace CodeToNeo4j.Graph;

public record FileMetaData(
    string FileKey,
    string FileName,
    string RelativePath,
    string FileHash,
    FileMetadata Metadata,
    string? RepoKey,
    string? Namespace
);
