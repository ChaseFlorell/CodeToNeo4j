using System.Reflection;

namespace CodeToNeo4j.Cypher;

public class CypherService : ICypherService
{
	public string GetCypher(string name)
	{
		using var stream = _executingAssembly.GetManifestResourceStream($"CodeToNeo4j.Cypher.{name}.cypher")
			?? throw new FileNotFoundException($"Cypher resource {name} not found.");

		using StreamReader reader = new(stream);
		return reader.ReadToEnd();
	}

	private readonly Assembly _executingAssembly = Assembly.GetExecutingAssembly();
}
