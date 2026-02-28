namespace CodeToNeo4j.VersionControl;

public record DiffResult(HashSet<string> ModifiedFiles, HashSet<string> DeletedFiles, IEnumerable<CommitMetadata> Commits);