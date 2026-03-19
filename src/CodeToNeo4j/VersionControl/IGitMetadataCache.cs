namespace CodeToNeo4j.VersionControl;

public interface IGitMetadataCache
{
	bool TryGet(string filePath, out FileMetadata metadata);
	void Set(string filePath, FileMetadata metadata);
	void Clear();
	int Count { get; }
}
