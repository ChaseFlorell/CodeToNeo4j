namespace CodeToNeo4j.VersionControl;

public record AuthorMetadata(string Name, DateTimeOffset FirstCommit, DateTimeOffset LastCommit, int CommitCount);