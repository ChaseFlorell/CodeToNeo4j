namespace CodeToNeo4j.Completions;

public interface IProcessRunner
{
    Task<string> RunCommand(string command, string arguments);
}
