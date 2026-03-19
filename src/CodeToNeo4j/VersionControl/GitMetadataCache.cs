namespace CodeToNeo4j.VersionControl;

public class GitMetadataCache : IGitMetadataCache
{
	public bool TryGet(string filePath, out FileMetadata metadata) =>
		_cache.TryGetValue(filePath, out metadata!);

	public void Set(string filePath, FileMetadata metadata) =>
		_cache[filePath] = metadata;

	public void Clear() =>
		_cache.Clear();

	public int Count => _cache.Count;

	private readonly Dictionary<string, FileMetadata> _cache = new(StringComparer.OrdinalIgnoreCase);
}
