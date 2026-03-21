using System.IO.Abstractions;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Dart.Bridge;
using CodeToNeo4j.Dart.Models;
using CodeToNeo4j.Dart.Yaml;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.FileHandlers;

public class DartHandler(
	IFileSystem fileSystem,
	ITextSymbolMapper textSymbolMapper,
	IDartBridgeService dartBridgeService,
	ILogger<DartHandler> logger,
	IConfigurationService configurationService) : DocumentHandlerBase(fileSystem, configurationService)
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
		var fileNamespace = _fileSystem.Path.GetDirectoryName(relativePath)?.Replace('\\', '/');

		// Find the project root by walking up to find pubspec.yaml
		var projectRoot = FindProjectRoot(filePath, _fileSystem);
		if (projectRoot is null)
		{
			logger.LogDebug("No pubspec.yaml found for {FilePath}, skipping Dart analysis", filePath);
			return new(fileNamespace, fileKey);
		}

		var analysisResult = await dartBridgeService.AnalyzeProject(projectRoot).ConfigureAwait(false);
		if (analysisResult is null)
		{
			return new(fileNamespace, fileKey);
		}

		// Find this file's results in the analysis
		var normalizedRelativePath = relativePath.Replace('\\', '/');
		DartFileResult? fileResult = null;

		foreach (var (key, value) in analysisResult.Files)
		{
			if (key.Replace('\\', '/').Equals(normalizedRelativePath, StringComparison.OrdinalIgnoreCase))
			{
				fileResult = value;
				break;
			}
		}

		if (fileResult is null)
		{
			logger.LogDebug("No analysis results found for {FilePath}", filePath);
			return new(fileNamespace, fileKey);
		}

		foreach (var symbolInfo in fileResult.Symbols)
		{
			if (!ShouldInclude(symbolInfo.Accessibility, minAccessibility))
			{
				continue;
			}

			var symbolKey = textSymbolMapper.BuildKey(fileKey, symbolInfo.Kind, symbolInfo.Name, symbolInfo.StartLine);
			var symbol = textSymbolMapper.CreateSymbol(
				symbolKey,
				symbolInfo.Name,
				symbolInfo.Kind,
				symbolInfo.Class,
				symbolInfo.Fqn,
				fileKey,
				relativePath,
				symbolInfo.Namespace ?? fileNamespace,
				symbolInfo.StartLine,
				symbolInfo.Accessibility,
				symbolInfo.Documentation,
				language: Language, technology: Technology);

			symbolBuffer.Add(symbol);
		}

		foreach (var rel in fileResult.Relationships)
		{
			var fromKey = textSymbolMapper.BuildKey(fileKey, rel.FromKind, rel.FromSymbol, rel.FromLine);
			var toKey = textSymbolMapper.BuildKey(fileKey, rel.ToKind, rel.ToSymbol, rel.ToLine);

			relBuffer.Add(new(fromKey, toKey, rel.RelType));
		}

		return new(fileNamespace, fileKey, TargetFrameworks: GetDartSdkConstraint(projectRoot));
	}

	private HashSet<string>? GetDartSdkConstraint(string projectRoot)
	{
		var pubspecPath = _fileSystem.Path.Combine(projectRoot, "pubspec.yaml");
		if (!_fileSystem.File.Exists(pubspecPath))
		{
			return null;
		}

		try
		{
			var content = _fileSystem.File.ReadAllText(pubspecPath);
			var pubspec = PubspecParser.Parse(content);
			if (!string.IsNullOrEmpty(pubspec.SdkConstraint))
			{
				return new HashSet<string>(StringComparer.Ordinal) { pubspec.SdkConstraint };
			}
		}
		catch
		{
			// ignore parse errors
		}

		return null;
	}

	private readonly IFileSystem _fileSystem = fileSystem;

	private static string? FindProjectRoot(string filePath, IFileSystem fs)
	{
		var dir = fs.Path.GetDirectoryName(filePath);
		while (!string.IsNullOrEmpty(dir))
		{
			if (fs.File.Exists(fs.Path.Combine(dir, "pubspec.yaml")))
			{
				return dir;
			}

			dir = fs.Path.GetDirectoryName(dir);
		}

		return null;
	}

	private static bool ShouldInclude(string accessibility, Accessibility minAccessibility)
	{
		if (minAccessibility == Accessibility.NotApplicable)
		{
			return true;
		}

		var mapped = accessibility switch
		{
			"Public" => Accessibility.Public,
			"Private" => Accessibility.Private,
			"Protected" => Accessibility.Protected,
			"Internal" => Accessibility.Internal,
			_ => Accessibility.Public
		};

		return mapped >= minAccessibility;
	}
}
