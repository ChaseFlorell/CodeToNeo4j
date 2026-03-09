namespace CodeToNeo4j.FileSystem;

public interface IFileService
{
    string NormalizePath(string path);
    string GetRelativePath(string relativeTo, string path);
    Task<string> ComputeSha256(string filePath);
}