using System.IO.Abstractions;
using System.Xml.Linq;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.FileHandlers;

public class XmlHandler(IFileSystem fileSystem, ITextSymbolMapper textSymbolMapper, ILogger<XmlHandler> logger)
    : DocumentHandlerBase(fileSystem)
{
    public override string FileExtension => ".xml";

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
        var fileNamespace = _fileSystem.Path.GetDirectoryName(relativePath)?.Replace('\\', '/');

        try
        {
            var xdoc = XDocument.Parse(content, LoadOptions.SetLineInfo);
            if (xdoc.Root != null)
            {
                ProcessElement(xdoc.Root, fileKey, relativePath, fileNamespace ?? string.Empty, symbolBuffer, relBuffer, minAccessibility);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse XML file: {FilePath}", filePath);
        }

        return new FileResult(fileNamespace, fileKey);
    }

    private void ProcessElement(XElement element, string fileKey, string relativePath, string fileNamespace, ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer, Accessibility minAccessibility)
    {
        if (Accessibility.Public < minAccessibility)
        {
            return;
        }

        var name = element.Name.LocalName;
        System.Xml.IXmlLineInfo lineInfo = element;
        var startLine = lineInfo.HasLineInfo() ? lineInfo.LineNumber : -1;

        var key = textSymbolMapper.BuildKey(fileKey, "XmlElement", name, startLine);

        var record = textSymbolMapper.CreateSymbol(
            key: key,
            name: name,
            kind: "XmlElement",
            @class: "element",
            fqn: name,
            fileKey: fileKey,
            relativePath: relativePath,
            fileNamespace: fileNamespace,
            startLine: startLine);

        symbolBuffer.Add(record);
        relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "CONTAINS"));

        foreach (var child in element.Elements())
        {
            ProcessElement(child, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);
        }
    }

    private readonly IFileSystem _fileSystem = fileSystem;
}
