namespace CodeToNeo4j.Console.FileSystem;

public interface IFileService
{
    string NormalizePath(string path);
    string ComputeSha256(byte[] bytes);
}