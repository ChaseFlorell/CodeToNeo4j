namespace CodeToNeo4j.VersionControl;

public record FileMetadata(DateTimeOffset Created, DateTimeOffset LastModified, IEnumerable<AuthorMetadata> Authors, IEnumerable<string> Commits, IEnumerable<string> Tags);