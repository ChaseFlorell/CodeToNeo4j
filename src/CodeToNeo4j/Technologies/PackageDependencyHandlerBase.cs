using System.IO.Abstractions;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Graph;
using CodeToNeo4j.Graph.Mapping;
using CodeToNeo4j.Graph.Models;

namespace CodeToNeo4j.Technologies;

/// <summary>
/// Base handler for package manifest files (e.g. .csproj, package.json).
/// Provides shared infrastructure for producing Dependency nodes keyed as
/// <c>pkg:{packageName}</c> with <c>DEPENDS_ON</c> relationships.
/// </summary>
public abstract class PackageDependencyHandlerBase(IFileSystem fileSystem, ITextSymbolMapper textSymbolMapper, IConfigurationService configurationService)
	: DocumentHandlerBase(fileSystem, configurationService)
{
	protected ITextSymbolMapper SymbolMapper { get; } = textSymbolMapper;

	protected void AddDependency(
		string name,
		string? version,
		string fileKey,
		string relativePath,
		string? fileNamespace,
		ICollection<Symbol> symbolBuffer,
		ICollection<Relationship> relBuffer)
	{
		var key = $"pkg:{name}";

		var record = SymbolMapper.CreateSymbol(
			key,
			name,
			"Dependency",
			name,
			version is not null ? $"{name} ({version})" : name,
			fileKey,
			relativePath,
			fileNamespace,
			-1,
			documentation: version,
			version: version,
			language: Language, technology: Technology);

		symbolBuffer.Add(record);
		relBuffer.Add(new(fileKey, key, GraphSchema.Relationships.DependsOn));
	}
}
