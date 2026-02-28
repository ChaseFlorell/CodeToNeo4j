namespace CodeToNeo4j.VersionControl;

public record CommitMetadata(string Hash, string AuthorName, string AuthorEmail, DateTimeOffset Date, string Message, IEnumerable<string> ChangedFiles);