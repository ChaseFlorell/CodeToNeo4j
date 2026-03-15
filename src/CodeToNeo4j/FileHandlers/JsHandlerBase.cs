using System.IO.Abstractions;
using System.Text.RegularExpressions;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

/// <summary>
/// Shared base handler for JavaScript and TypeScript files.
/// Provides common function, import, and call-graph extraction logic.
/// </summary>
public abstract partial class JsHandlerBase(IFileSystem fileSystem, ITextSymbolMapper textSymbolMapper) : DocumentHandlerBase(fileSystem)
{
    protected abstract string KindPrefix { get; }
    protected ITextSymbolMapper TextSymbolMapper { get; } = textSymbolMapper;

    protected override async Task<FileResult> HandleFile(
        TextDocument? document,
        Compilation? compilation,
        string? repoKey,
        string fileKey,
        string filePath,
        string relativePath,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Accessibility minAccessibility)
    {
        var content = await GetContent(document, filePath).ConfigureAwait(false);
        var fileNamespace = Path.GetDirectoryName(relativePath)?.Replace('\\', '/');

        ExtractFunctions(content, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);
        ExtractImportsExports(content, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);
        ExtractAdditionalSymbols(content, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);

        return new FileResult(fileNamespace, fileKey);
    }

    /// <summary>
    /// Extension point for derived classes to extract language-specific symbols beyond functions and imports.
    /// </summary>
    protected virtual void ExtractAdditionalSymbols(
        string content,
        string fileKey,
        string relativePath,
        string? fileNamespace,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Accessibility minAccessibility)
    {
    }

    private record FunctionDef(string Name, string Key, int BodyStart, int BodyEnd);

    private static readonly HashSet<string> JsKeywords = new(StringComparer.Ordinal)
    {
        "if", "for", "while", "switch", "catch", "function", "typeof", "instanceof",
        "return", "new", "delete", "void", "throw", "case", "in", "of", "do", "else"
    };

    private void ExtractFunctions(string content, string fileKey, string relativePath, string? fileNamespace, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, Accessibility minAccessibility)
    {
        if (Accessibility.Public < minAccessibility)
            return;

        var functionRegex = FunctionRegex();
        var matches = functionRegex.Matches(content);
        var functionDefs = new List<FunctionDef>();

        foreach (Match match in matches)
        {
            var name = match.Groups[1].Value;
            if (string.IsNullOrEmpty(name)) name = match.Groups[2].Value;
            if (string.IsNullOrEmpty(name)) name = match.Groups[3].Value;

            if (string.IsNullOrEmpty(name)) continue;

            var startLine = content[..match.Index].Count(c => c == '\n') + 1;
            var key = TextSymbolMapper.BuildKey(fileKey, "Function", name, startLine);

            var record = TextSymbolMapper.CreateSymbol(
                key: key,
                name: name,
                kind: $"{KindPrefix}Function",
                @class: "function",
                fqn: name,
                fileKey: fileKey,
                relativePath: relativePath,
                fileNamespace: fileNamespace,
                startLine: startLine);

            symbolBuffer.Add(record);
            relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "CONTAINS"));

            var (bodyStart, bodyEnd) = FindFunctionBody(content, match.Index + match.Length);
            if (bodyStart >= 0)
                functionDefs.Add(new FunctionDef(name, key, bodyStart, bodyEnd));
        }

        ExtractFunctionCallRelationships(content, functionDefs, relBuffer);
    }

    private static (int bodyStart, int bodyEnd) FindFunctionBody(string content, int searchFrom)
    {
        var braceIndex = content.IndexOf('{', searchFrom);
        if (braceIndex < 0) return (-1, -1);

        var depth = 0;
        for (var i = braceIndex; i < content.Length; i++)
        {
            if (content[i] == '{') depth++;
            else if (content[i] == '}')
            {
                depth--;
                if (depth == 0) return (braceIndex + 1, i);
            }
        }

        return (-1, -1);
    }

    private static void ExtractFunctionCallRelationships(string content, List<FunctionDef> functionDefs, ICollection<Relationship> relBuffer)
    {
        if (functionDefs.Count == 0) return;

        var functionLookup = functionDefs
            .GroupBy(f => f.Name)
            .ToDictionary(g => g.Key, g => g.First().Key);
        var callRegex = FunctionCallRegex();

        foreach (var caller in functionDefs)
        {
            var body = content[caller.BodyStart..caller.BodyEnd];
            var seen = new HashSet<string>();

            foreach (Match match in callRegex.Matches(body))
            {
                var calledName = match.Groups[1].Value;
                if (JsKeywords.Contains(calledName)) continue;
                if (functionLookup.TryGetValue(calledName, out var calleeKey) && seen.Add(calleeKey))
                    relBuffer.Add(new Relationship(FromKey: caller.Key, ToKey: calleeKey, RelType: "INVOKES"));
            }
        }
    }

    private void ExtractImportsExports(string content, string fileKey, string relativePath, string? fileNamespace, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, Accessibility minAccessibility)
    {
        if (Accessibility.Public < minAccessibility) return;

        var importRegex = ImportRegex();
        foreach (Match match in importRegex.Matches(content))
        {
            var module = match.Groups[1].Value;
            var startLine = content[..match.Index].Count(c => c == '\n') + 1;
            var key = TextSymbolMapper.BuildKey(fileKey, "Import", module, startLine);

            var record = TextSymbolMapper.CreateSymbol(
                key: key,
                name: module,
                kind: $"{KindPrefix}Import",
                @class: "module",
                fqn: module,
                fileKey: fileKey,
                relativePath: relativePath,
                fileNamespace: fileNamespace,
                startLine: startLine);

            symbolBuffer.Add(record);
            relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "DEPENDS_ON"));
        }
    }

    // Handles: function name(...), const/let/var name = (...) => (with optional TS return type), name: function(...)
    [GeneratedRegex(@"(?:function\s+([a-zA-Z0-9_$]+)|(?:const|let|var)\s+([a-zA-Z0-9_$]+)\s*=\s*(?:async\s*)?\(.*?\)(?:\s*:\s*[\w<>[\]|&. ?,]+)?\s*=>|([a-zA-Z0-9_$]+)\s*:\s*function)", RegexOptions.Multiline)]
    private static partial Regex FunctionRegex();
    [GeneratedRegex(@"import\s+.*?\s+from\s+['""](.*?)['""]", RegexOptions.Multiline)]
    private static partial Regex ImportRegex();
    [GeneratedRegex(@"\b([a-zA-Z_$][a-zA-Z0-9_$]*)\s*\(", RegexOptions.Multiline)]
    private static partial Regex FunctionCallRegex();
}
