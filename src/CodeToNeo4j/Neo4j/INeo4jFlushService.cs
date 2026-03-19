using CodeToNeo4j.Graph;

namespace CodeToNeo4j.Neo4j;

public interface INeo4jFlushService
{
	Task FlushFiles(IEnumerable<FileMetaData> files, string databaseName);
	Task FlushSymbols(IEnumerable<Symbol> symbols, IEnumerable<Relationship> relationships, string databaseName);
	Task UpsertDependencyUrls(IEnumerable<UrlNode> urlNodes, string databaseName);
	Task FlushTargetFrameworks(IEnumerable<TargetFrameworkBatch> batches, string databaseName);
}
