namespace CodeToNeo4j.Completions;

public class EnvironmentService : IEnvironmentService
{
    public string? GetEnvironmentVariable(string variable) => Environment.GetEnvironmentVariable(variable);
    public string GetFolderPath(Environment.SpecialFolder folder) => Environment.GetFolderPath(folder);
}