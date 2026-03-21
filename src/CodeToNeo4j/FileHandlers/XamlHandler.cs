using System.IO.Abstractions;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.FileHandlers;

public partial class XamlHandler(
	IRoslynSymbolProcessor symbolProcessor,
	IFileSystem fileSystem,
	ITextSymbolMapper textSymbolMapper,
	ILogger<XamlHandler> logger,
	IConfigurationService configurationService)
	: DocumentHandlerBase(fileSystem, configurationService)
{

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
		string? fileNamespace = null;

		try
		{
			XDocument xdoc = XDocument.Parse(content, LoadOptions.SetLineInfo);
			if (xdoc.Root != null)
			{
				var xClass = GetXamlAttribute(xdoc.Root, "Class")?.Value;
				if (!string.IsNullOrEmpty(xClass))
				{
					fileNamespace = xClass.Contains('.')
						? xClass.Substring(0, xClass.LastIndexOf('.'))
						: null;
				}

				// Extract XML elements via traditional parsing
				ProcessElement(xdoc.Root, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to parse XAML file: {FilePath}", filePath);
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
					var semanticModel = compilation.GetSemanticModel(tree, true);
					symbolProcessor.ProcessSyntaxTree(tree, semanticModel, repoKey, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer,
						minAccessibility, Language, Technology);
				}
			}
		}

		return new(fileNamespace, fileKey);
	}

	private void ProcessElement(XElement element, string fileKey, string relativePath, string? fileNamespace, ICollection<Symbol> symbolBuffer,
		ICollection<Relationship> relBuffer, Accessibility minAccessibility)
	{
		var name = element.Name.LocalName;
		var keySuffix = "";

		// Look for x:Key or x:Name
		var xNameAttr = GetXamlAttribute(element, "Name");
		var xKeyAttr = GetXamlAttribute(element, "Key");

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

		// XAML element keys embed the optional x:Name/x:Key suffix directly in the key
		var symbolKey = $"{fileKey}:{name}{keySuffix}:{startLine}";
		if (Accessibility.Public >= minAccessibility)
		{
			var record = textSymbolMapper.CreateSymbol(
				symbolKey,
				xNameAttr?.Value ?? xKeyAttr?.Value ?? name,
				"XamlElement",
				"element",
				$"{name}{keySuffix}",
				fileKey,
				relativePath,
				fileNamespace,
				startLine,
				language: Language, technology: Technology);

			symbolBuffer.Add(record);
			relBuffer.Add(new(fileKey, symbolKey, "CONTAINS"));
		}

		// Extract potential event handlers and property attributes
		foreach (var attr in element.Attributes())
		{
			if (IsEventHandler(attr.Name.LocalName))
			{
				if (Accessibility.Private >= minAccessibility)
				{
					var handlerKey = textSymbolMapper.BuildKey(fileKey, "EventHandler", attr.Value);

					var handlerRecord = textSymbolMapper.CreateSymbol(
						handlerKey,
						attr.Value,
						"XamlEventHandler",
						"event-handler",
						attr.Value,
						fileKey,
						relativePath,
						fileNamespace,
						startLine,
						"Private",
						language: Language, technology: Technology);

					symbolBuffer.Add(handlerRecord);
					relBuffer.Add(new(symbolKey, handlerKey, "BINDS_TO"));
				}
			}
			else if (IsPropertyAttribute(attr))
			{
				if (Accessibility.Public >= minAccessibility)
				{
					var attrName = attr.Name.LocalName;
					var attrValue = attr.Value;
					var bindingPath = ExtractBindingPath(attrValue);
					var attrKey = textSymbolMapper.BuildKey(fileKey, "XamlAttribute", $"{name}.{attrName}", startLine);

					var attrRecord = textSymbolMapper.CreateSymbol(
						attrKey,
						attrName,
						"XamlAttribute",
						"attribute",
						$"{name}.{attrName}={attrValue}",
						fileKey,
						relativePath,
						fileNamespace,
						startLine,
						documentation: attrValue,
						comments: bindingPath,
						language: Language, technology: Technology);

					symbolBuffer.Add(attrRecord);
					relBuffer.Add(new(symbolKey, attrKey, "SETS_PROPERTY"));
				}
			}
		}

		foreach (var child in element.Elements())
		{
			ProcessElement(child, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);
		}
	}

	private static XAttribute? GetXamlAttribute(XElement element, string localName)
	{
		return element.Attributes().FirstOrDefault(a =>
			a.Name.LocalName == localName &&
			(a.Name.NamespaceName == string.Empty || _xamlNamespaces.Contains(a.Name.NamespaceName))
		);
	}

	private static bool IsPropertyAttribute(XAttribute attr)
	{
		// Skip namespace declarations (xmlns:x="..." or xmlns="...")
		if (attr.IsNamespaceDeclaration)
		{
			return false;
		}

		// Skip x:-prefixed attributes (x:Class, x:Name, x:Key, etc.)
		if (_xamlNamespaces.Contains(attr.Name.NamespaceName))
		{
			return false;
		}

		return true;
	}

	internal static string? ExtractBindingPath(string value)
	{
		var match = BindingRegex().Match(value);
		if (!match.Success)
		{
			return null;
		}

		var path = match.Groups[1].Value;

		// Handle named Path= syntax: {Binding Path=Items.Count}
		if (path.StartsWith("Path=", StringComparison.Ordinal))
		{
			path = path.Substring(5);
		}

		return path;
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

	[GeneratedRegex(@"^\{Binding\s+(\S+?)(?:\s*,.*)?}$")]
	private static partial Regex BindingRegex();

	private static readonly string[] _xamlNamespaces =
	[
		"http://schemas.microsoft.com/winfx/2009/xaml",
		"http://schemas.microsoft.com/winfx/2006/xaml",
		"http://schemas.microsoft.com/dotnet/2021/maui",
		"http://schemas.microsoft.com/winfx/2006/xaml/presentation",
		"http://xamarin.com/schemas/2014/forms",
		"http://schemas.microsoft.com/client/2007",
		"https://github.com/avaloniaui"
	];
}
