using CodeToNeo4j.VersionControl;

namespace CodeToNeo4j.Graph;

public record FileMetaData(
    string FileKey,
    string FilePath,
    string FileHash,
    FileMetadata Metadata,
    string RepoKey
);
