using System.Reflection;

namespace CodeToNeo4j.Cypher;

public class CypherService : ICypherService
{
    public string GetCypher(string name)
    {
        using var stream = _executingAssembly.GetManifestResourceStream($"CodeToNeo4j.Cypher.{name}.cypher");
        if (stream == null)
        {
            throw new FileNotFoundException($"Cypher resource {name} not found.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private readonly Assembly _executingAssembly = Assembly.GetExecutingAssembly();
}