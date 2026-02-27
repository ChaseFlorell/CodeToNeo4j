using System.Reflection;

namespace CodeToNeo4j.Console;

public class CypherService
{
    public string GetCypher(string name)
    {
        using var stream = _executingAssembly.GetManifestResourceStream($"CodeToNeo4j.Console.Cypher.{name}.cypher");
        if (stream == null)
        {
            throw new FileNotFoundException($"Cypher resource {name} not found.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private readonly Assembly _executingAssembly = Assembly.GetExecutingAssembly();
}