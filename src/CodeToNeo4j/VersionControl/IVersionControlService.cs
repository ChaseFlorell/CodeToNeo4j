namespace CodeToNeo4j.VersionControl;

public interface IVersionControlService
{
	Task<DiffResult> GetChangedFiles(string diffBase, string workingDirectory, IEnumerable<string> includeExtensions);
	Task<FileMetadata> GetFileMetadata(string filePath, string workingDirectory);
	Task LoadMetadata(string workingDirectory, IEnumerable<string> includeExtensions);
	Task<int> GetCommitCount(string range, string workingDirectory);
	IAsyncEnumerable<IEnumerable<CommitMetadata>> GetCommitsBatched(string range, string workingDirectory, int batchSize);
	Task<IEnumerable<CommitMetadata>> GetCommitBatch(string range, string workingDirectory, int batchSize, int skip);
}
