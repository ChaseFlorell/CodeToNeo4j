namespace CodeToNeo4j.Graph;

public interface ITextSymbolMapper
{
    string BuildKey(string fileKey, string kindToken, string name, int? startLine = null);

    Symbol CreateSymbol(
        string key,
        string name,
        string kind,
        string @class,
        string fqn,
        string fileKey,
        string relativePath,
        string? fileNamespace,
        int startLine,
        string accessibility = "Public",
        string? documentation = null,
        string? version = null);
}
