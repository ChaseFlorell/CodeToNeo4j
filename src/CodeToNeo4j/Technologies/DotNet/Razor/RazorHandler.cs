using System.IO.Abstractions;
using System.Text.RegularExpressions;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Graph.Mapping;
using CodeToNeo4j.Graph.Models;
using CodeToNeo4j.Technologies.DotNet.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace CodeToNeo4j.Technologies.DotNet.Razor;

public partial class RazorHandler(
	IRoslynSymbolProcessor symbolProcessor,
	IFileSystem fileSystem,
	ITextSymbolMapper textSymbolMapper,
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
		var fileNamespace = ExtractNamespace(content);

		// Use Roslyn to extract members from generated code
		if (compilation is not null)
		{
			foreach (var tree in compilation.SyntaxTrees)
			{
				// Razor generated files use #line directives to map back to the .razor file.
				// We'll check if any type declared in this tree maps back to our file.
				var isMappedToThisFile = string.Equals(tree.FilePath, filePath, StringComparison.OrdinalIgnoreCase);
				if (!isMappedToThisFile)
				{
					var root = await tree.GetRootAsync();
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
					var root = await tree.GetRootAsync().ConfigureAwait(false);
					var firstType = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
					if (firstType != null)
					{
						var symbol = semanticModel.GetDeclaredSymbol(firstType);
						var fqn = symbol?.ToDisplayString();
						if (!string.IsNullOrEmpty(fqn))
						{
							fileKey = fqn;
						}

						var ns = symbol?.ContainingNamespace?.ToDisplayString();
						if (!string.IsNullOrEmpty(ns))
						{
							fileNamespace = ns;
						}
					}

					symbolProcessor.ProcessSyntaxTree(tree, semanticModel, repoKey, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer,
						minAccessibility, Language, Technology);
				}
			}
		}

		// Extract directives via Regex as a fallback/complement
		ExtractDirectives(content, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);

		return new(fileNamespace, fileKey);
	}

	private static string? ExtractNamespace(string content)
	{
		var match = NamespaceRegex().Match(content);
		return match.Success ? match.Groups[1].Value.Trim() : null;
	}

	private void ExtractDirectives(string content, string fileKey, string relativePath, string? fileNamespace, ICollection<Symbol> symbolBuffer,
		ICollection<Relationship> relBuffer, Accessibility minAccessibility)
	{
		if (!IsPublicAccessible(minAccessibility))
		{
			return;
		}

		// Simple regex-based extraction for common Razor directives
		var matches = DirectivesRegex().Matches(content);

		foreach (Match match in matches)
		{
			var line = match.Value.Trim();
			var kind = line.StartsWith("@using") ? "UsingDirective" :
				line.StartsWith("@inject") ? "InjectDirective" :
				line.StartsWith("@model") ? "ModelDirective" : "InheritsDirective";

			var name = match.Groups[1].Value.Trim();
			var key = textSymbolMapper.BuildKey(fileKey, kind, name);
			var startLine = GetLineNumber(content, match.Index);

			var record = textSymbolMapper.CreateSymbol(
				key,
				name,
				kind,
				"component",
				name,
				fileKey,
				relativePath,
				fileNamespace,
				startLine,
				language: Language, technology: Technology);

			symbolBuffer.Add(record);
			relBuffer.Add(new(fileKey, key, "CONTAINS"));
		}
	}

	[GeneratedRegex(@"^@namespace\s+(.+)$", RegexOptions.Multiline)]
	private static partial Regex NamespaceRegex();

	[GeneratedRegex(@"^@(?:using|inject|model|inherits)\s+(.+)$", RegexOptions.Multiline)]
	private static partial Regex DirectivesRegex();
}
