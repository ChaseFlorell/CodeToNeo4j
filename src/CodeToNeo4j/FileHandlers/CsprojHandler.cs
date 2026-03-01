using System.Xml.Linq;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public class CsprojHandler : DocumentHandlerBase
{
    public override string FileExtension => ".csproj";

    public override async ValueTask HandleAsync(
        TextDocument? document,
        Compilation? compilation,
        string repoKey,
        string fileKey,
        string filePath,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        string databaseName,
        Accessibility minAccessibility)
    {
        await base.HandleAsync(document, compilation, repoKey, fileKey, filePath, symbolBuffer, relBuffer, databaseName, minAccessibility);
        string content = await GetContent(document, filePath);

        try
        {
            var xdoc = XDocument.Parse(content, LoadOptions.SetLineInfo);
            if (xdoc.Root == null) return;

            ProcessProject(xdoc.Root, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility);
        }
        catch (Exception)
        {
            // Fail gracefully
        }
    }

    private void ProcessProject(XElement project, string fileKey, string filePath, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, Accessibility minAccessibility)
    {
        if (Accessibility.Public < minAccessibility) return;

        // Extract PropertyGroups
        var propertyGroups = project.Elements().Where(e => e.Name.LocalName == "PropertyGroup");
        foreach (var group in propertyGroups)
        {
            var properties = group.Elements();
            foreach (var property in properties)
            {
                var name = property.Name.LocalName;
                var value = property.Value;
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value)) continue;

                var lineInfo = (System.Xml.IXmlLineInfo)property;
                var startLine = lineInfo.HasLineInfo() ? lineInfo.LineNumber : -1;
                var key = $"{fileKey}:Property:{name}:{startLine}";

                var record = new Symbol(
                    Key: key,
                    Name: name,
                    Kind: "ProjectProperty",
                    Fqn: $"{name}: {value}",
                    Accessibility: "Public",
                    FileKey: fileKey,
                    FilePath: filePath,
                    StartLine: startLine,
                    EndLine: startLine,
                    Documentation: value,
                    Comments: null
                );

                symbolBuffer.Add(record);
                relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "HAS_PROPERTY"));
            }
        }

        // Extract PackageReferences
        var packageRefs = project.Descendants().Where(e => e.Name.LocalName == "PackageReference");
        foreach (var packageRef in packageRefs)
        {
            var include = packageRef.Attribute("Include")?.Value;
            var version = packageRef.Attribute("Version")?.Value ?? packageRef.Element(packageRef.Name.Namespace + "Version")?.Value;

            if (string.IsNullOrEmpty(include)) continue;

            var lineInfo = (System.Xml.IXmlLineInfo)packageRef;
            var startLine = lineInfo.HasLineInfo() ? lineInfo.LineNumber : -1;
            var key = $"{fileKey}:PackageReference:{include}:{startLine}";

            var record = new Symbol(
                Key: key,
                Name: include,
                Kind: "PackageReference",
                Fqn: $"{include} ({version})",
                Accessibility: "Public",
                FileKey: fileKey,
                FilePath: filePath,
                StartLine: startLine,
                EndLine: startLine,
                Documentation: version,
                Comments: null
            );

            symbolBuffer.Add(record);
            relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "DEPENDS_ON"));
        }

        // Extract ProjectReferences
        var projectRefs = project.Descendants().Where(e => e.Name.LocalName == "ProjectReference");
        foreach (var projectRef in projectRefs)
        {
            var include = projectRef.Attribute("Include")?.Value;
            if (string.IsNullOrEmpty(include)) continue;

            var lineInfo = (System.Xml.IXmlLineInfo)projectRef;
            var startLine = lineInfo.HasLineInfo() ? lineInfo.LineNumber : -1;
            var key = $"{fileKey}:ProjectReference:{include}:{startLine}";

            var record = new Symbol(
                Key: key,
                Name: include,
                Kind: "ProjectReference",
                Fqn: include,
                Accessibility: "Public",
                FileKey: fileKey,
                FilePath: filePath,
                StartLine: startLine,
                EndLine: startLine,
                Documentation: null,
                Comments: null
            );

            symbolBuffer.Add(record);
            relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "DEPENDS_ON"));
        }
    }
}
