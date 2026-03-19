namespace CodeToNeo4j.VersionControl;

public interface IGitLogParser
{
	IEnumerable<CommitMetadata> ParseCommits(string output, string repoRoot);

	FileMetadata BuildFileMetadata(
		IList<(string Author, DateTimeOffset Date, string Hash, string? Refs)> history);

	FileMetadata ParseSingleFileLog(string output);
}
