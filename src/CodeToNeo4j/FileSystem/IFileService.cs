namespace CodeToNeo4j.FileSystem;

public interface IFileService
{
    string NormalizePath(string path);
    string GetRelativePath(string relativeTo, string path);
    string ComputeSha256(byte[] bytes);
}