using System.IO.Abstractions;
using System.Text.RegularExpressions;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public partial class TypeScriptHandler(IFileSystem fileSystem, ITextSymbolMapper textSymbolMapper, IConfigurationService configurationService)
	: JsHandlerBase(fileSystem, textSymbolMapper, configurationService)
{
	protected override void ExtractAdditionalSymbols(
		string content,
		string fileKey,
		string relativePath,
		string? fileNamespace,
		ICollection<Symbol> symbolBuffer,
		ICollection<Relationship> relBuffer,
		Accessibility minAccessibility)
	{
		if (!IsPublicAccessible(minAccessibility))
		{
			return;
		}

		ExtractInterfaces(content, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer);
		ExtractTypeAliases(content, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer);
		ExtractEnums(content, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer);
	}

	private void ExtractInterfaces(string content, string fileKey, string relativePath, string? fileNamespace, ICollection<Symbol> symbolBuffer,
		ICollection<Relationship> relBuffer)
	{
		foreach (Match match in InterfaceRegex().Matches(content))
		{
			var name = match.Groups[1].Value;
			var startLine = GetLineNumber(content, match.Index);
			var key = TextSymbolMapper.BuildKey(fileKey, "Interface", name, startLine);

			symbolBuffer.Add(TextSymbolMapper.CreateSymbol(
				key,
				name,
				"TypeScriptInterface",
				"interface",
				name,
				fileKey,
				relativePath,
				fileNamespace,
				startLine));

			relBuffer.Add(new(fileKey, key, "CONTAINS"));
		}
	}

	private void ExtractTypeAliases(string content, string fileKey, string relativePath, string? fileNamespace, ICollection<Symbol> symbolBuffer,
		ICollection<Relationship> relBuffer)
	{
		foreach (Match match in TypeAliasRegex().Matches(content))
		{
			var name = match.Groups[1].Value;
			var startLine = GetLineNumber(content, match.Index);
			var key = TextSymbolMapper.BuildKey(fileKey, "TypeAlias", name, startLine);

			symbolBuffer.Add(TextSymbolMapper.CreateSymbol(
				key,
				name,
				"TypeScriptTypeAlias",
				"type",
				name,
				fileKey,
				relativePath,
				fileNamespace,
				startLine));

			relBuffer.Add(new(fileKey, key, "CONTAINS"));
		}
	}

	private void ExtractEnums(string content, string fileKey, string relativePath, string? fileNamespace, ICollection<Symbol> symbolBuffer,
		ICollection<Relationship> relBuffer)
	{
		foreach (Match match in EnumRegex().Matches(content))
		{
			var name = match.Groups[1].Value;
			var startLine = GetLineNumber(content, match.Index);
			var key = TextSymbolMapper.BuildKey(fileKey, "Enum", name, startLine);

			symbolBuffer.Add(TextSymbolMapper.CreateSymbol(
				key,
				name,
				"TypeScriptEnum",
				"enum",
				name,
				fileKey,
				relativePath,
				fileNamespace,
				startLine));

			relBuffer.Add(new(fileKey, key, "CONTAINS"));
		}
	}

	[GeneratedRegex(@"(?:^|\s)interface\s+([a-zA-Z0-9_$]+)", RegexOptions.Multiline)]
	private static partial Regex InterfaceRegex();

	[GeneratedRegex(@"(?:^|\s)type\s+([a-zA-Z0-9_$]+)(?:<[^>]*>)?\s*=", RegexOptions.Multiline)]
	private static partial Regex TypeAliasRegex();

	[GeneratedRegex(@"(?:^|\s)(?:const\s+)?enum\s+([a-zA-Z0-9_$]+)", RegexOptions.Multiline)]
	private static partial Regex EnumRegex();
}
