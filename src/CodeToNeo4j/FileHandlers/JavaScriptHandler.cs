using System.IO.Abstractions;
using System.Text.RegularExpressions;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public partial class JavaScriptHandler (IFileSystem fileSystem) : DocumentHandlerBase(fileSystem)
{
    public override string FileExtension => ".js";

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

        // Extract function definitions (basic)
        ExtractFunctions(content, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);
        
        // Extract imports/exports (basic)
        ExtractImportsExports(content, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);

        return new FileResult(fileNamespace, fileKey);
    }

    private record FunctionDef(string Name, string Key, int BodyStart, int BodyEnd);

    private static readonly HashSet<string> JsKeywords = new(StringComparer.Ordinal)
    {
        "if", "for", "while", "switch", "catch", "function", "typeof", "instanceof",
        "return", "new", "delete", "void", "throw", "case", "in", "of", "do", "else"
    };

    private static void ExtractFunctions(string content, string fileKey, string relativePath, string? fileNamespace, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, Accessibility minAccessibility)
    {
        if (Accessibility.Public < minAccessibility)
        {
            return;
        }

        // Regex for: function name(...) or const name = (...) => or name: function(...)
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
            var key = $"{fileKey}:Function:{name}:{startLine}";

            var record = new Symbol(
                Key: key,
                Name: name,
                Kind: "JavaScriptFunction",
                Class: "function",
                Fqn: name,
                Accessibility: "Public",
                FileKey: fileKey,
                RelativePath: relativePath,
                StartLine: startLine,
                EndLine: startLine,
                Documentation: null,
                Comments: null,
                Namespace: fileNamespace,
                Version: null
            );

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

        var functionLookup = functionDefs.ToDictionary(f => f.Name, f => f.Key);
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

    private static void ExtractImportsExports(string content, string fileKey, string relativePath, string? fileNamespace, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, Accessibility minAccessibility)
    {
        if (Accessibility.Public < minAccessibility) return;

        // Extract imports
        var importRegex = ImportRegex();
        foreach (Match match in importRegex.Matches(content))
        {
            var module = match.Groups[1].Value;
            var startLine = content[..match.Index].Count(c => c == '\n') + 1;
            var key = $"{fileKey}:Import:{module}:{startLine}";

            var record = new Symbol(
                Key: key,
                Name: module,
                Kind: "JavaScriptImport",
                Class: "module",
                Fqn: module,
                Accessibility: "Public",
                FileKey: fileKey,
                RelativePath: relativePath,
                StartLine: startLine,
                EndLine: startLine,
                Documentation: null,
                Comments: null,
                Namespace: fileNamespace,
                Version: null
            );

            symbolBuffer.Add(record);
            relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "DEPENDS_ON"));
        }
    }

    [GeneratedRegex(@"(?:function\s+([a-zA-Z0-9_$]+)|(?:const|let|var)\s+([a-zA-Z0-9_$]+)\s*=\s*(?:async\s*)?\(.*?\)\s*=>|([a-zA-Z0-9_$]+)\s*:\s*function)", RegexOptions.Multiline)]
    private static partial Regex FunctionRegex();
    [GeneratedRegex(@"import\s+.*?\s+from\s+['""](.*?)['""]", RegexOptions.Multiline)]
    private static partial Regex ImportRegex();
    [GeneratedRegex(@"\b([a-zA-Z_$][a-zA-Z0-9_$]*)\s*\(", RegexOptions.Multiline)]
    private static partial Regex FunctionCallRegex();
}
