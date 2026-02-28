namespace CodeToNeo4j.VersionControl;

public interface IVersionControlService
{
    ValueTask<DiffResult> GetChangedFiles(string diffBase, string workingDirectory, IEnumerable<string> includeExtensions);
    ValueTask<FileMetadata> GetFileMetadata(string filePath, string workingDirectory);
}