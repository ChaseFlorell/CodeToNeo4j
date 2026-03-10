using System.IO.Abstractions;
using System.Xml.Linq;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeToNeo4j.FileHandlers;

public class XamlHandler(
    IRoslynSymbolProcessor symbolProcessor,
    IFileSystem fileSystem)
    : DocumentHandlerBase(fileSystem)
{
    public override string FileExtension => ".xaml";

    protected override async Task<string?> HandleFile(
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
        string? fileNamespace = null;

        try
        {
            var xdoc = XDocument.Parse(content, LoadOptions.SetLineInfo);
            if (xdoc.Root == null) return null;

            var xNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
            var xClass = xdoc.Root.Attribute(XName.Get("Class", xNamespace))?.Value;
            fileNamespace = xClass != null && xClass.Contains('.') 
                ? xClass.Substring(0, xClass.LastIndexOf('.')) 
                : null;

            // Extract XML elements via traditional parsing
            ProcessElement(xdoc.Root, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);
        }
        catch (Exception)
        {
            // Ignore XML parse errors
        }

        // Use Roslyn to extract members from generated code
        if (compilation is not null)
        {
            foreach (var tree in compilation.SyntaxTrees)
            {
                // XAML generated files (.g.cs) also map back to the .xaml file.
                // We'll check if any type declared in this tree maps back to our file.
                var isMappedToThisFile = string.Equals(tree.FilePath, filePath, StringComparison.OrdinalIgnoreCase);
                if (!isMappedToThisFile)
                {
                    var root = tree.GetRoot();
                    var firstType = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
                    if (firstType != null)
                    {
                        var mappedSpan = firstType.GetLocation().GetMappedLineSpan();
                        isMappedToThisFile = mappedSpan.IsValid && string.Equals(mappedSpan.Path, filePath, StringComparison.OrdinalIgnoreCase);
                    }
                }

                if (isMappedToThisFile)
                {
                    var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
                    symbolProcessor.ProcessSyntaxTree(tree, semanticModel, repoKey, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);
                }
            }
        }

        return fileNamespace;
    }

    private void ProcessElement(XElement element, string fileKey, string relativePath, string? fileNamespace, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, Accessibility minAccessibility)
    {
        var name = element.Name.LocalName;
        var keySuffix = "";

        // Look for x:Key or x:Name
        var xNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
        var xNameAttr = element.Attribute(XName.Get("Name", xNamespace)) ?? element.Attribute("Name");
        var xKeyAttr = element.Attribute(XName.Get("Key", xNamespace));

        if (xNameAttr != null)
        {
            keySuffix = $":{xNameAttr.Value}";
        }
        else if (xKeyAttr != null)
        {
            keySuffix = $":{xKeyAttr.Value}";
        }

        System.Xml.IXmlLineInfo lineInfo = element;
        var startLine = lineInfo.HasLineInfo() ? lineInfo.LineNumber : -1;

        var symbolKey = $"{fileKey}:{name}{keySuffix}:{startLine}";
        if (Accessibility.Public >= minAccessibility)
        {
            var record = new Symbol(
                Key: symbolKey,
                Name: xNameAttr?.Value ?? xKeyAttr?.Value ?? name,
                Kind: "XamlElement",
                Fqn: $"{name}{keySuffix}",
                Accessibility: "Public",
                FileKey: fileKey,
                RelativePath: relativePath,
                StartLine: startLine,
                EndLine: startLine,
                Documentation: null,
                Comments: null,
                Namespace: fileNamespace
            );

            symbolBuffer.Add(record);
            relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: symbolKey, RelType: "CONTAINS"));
        }

        // Extract potential event handlers
        foreach (var attr in element.Attributes())
        {
            if (XamlHandler.IsEventHandler(attr.Name.LocalName))
            {
                if (Accessibility.Private >= minAccessibility)
                {
                    var handlerKey = $"{fileKey}:EventHandler:{attr.Value}";
                    var handlerRecord = new Symbol(
                        Key: handlerKey,
                        Name: attr.Value,
                        Kind: "XamlEventHandler",
                        Fqn: attr.Value,
                        Accessibility: "Private",
                        FileKey: fileKey,
                        RelativePath: relativePath,
                        StartLine: startLine,
                        EndLine: startLine,
                        Documentation: null,
                        Comments: null,
                        Namespace: fileNamespace
                    );
                    symbolBuffer.Add(handlerRecord);
                    relBuffer.Add(new Relationship(FromKey: symbolKey, ToKey: handlerKey, RelType: "BINDS_TO"));
                }
            }
        }

        foreach (var child in element.Elements())
        {
            ProcessElement(child, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);
        }
    }

    private static bool IsEventHandler(string attrName)
    {
        // Common event naming patterns in XAML
        return attrName.EndsWith("Click") ||
               attrName.EndsWith("Changed") ||
               attrName.EndsWith("Loaded") ||
               attrName.EndsWith("Pressed") ||
               attrName.EndsWith("Released") ||
               attrName == "Command";
    }
}