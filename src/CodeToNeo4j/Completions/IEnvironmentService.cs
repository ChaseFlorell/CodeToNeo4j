namespace CodeToNeo4j.Completions;

public interface IEnvironmentService
{
    string? GetEnvironmentVariable(string variable);
    string GetFolderPath(Environment.SpecialFolder folder);
}