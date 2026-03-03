namespace CodeToNeo4j.VersionControl;

public interface IVersionControlService
{
    Task<DiffResult> GetChangedFiles(string diffBase, string workingDirectory, IEnumerable<string> includeExtensions);
    Task<FileMetadata> GetFileMetadata(string filePath, string workingDirectory);
}