namespace CodeToNeo4j.Neo4j;

public interface INeo4jSchemaService
{
	Task Initialize(string? repoKey, string databaseName);
}
