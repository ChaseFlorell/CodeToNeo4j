using System.IO.Abstractions;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Graph.Mapping;
using CodeToNeo4j.TypeScript.Bridge;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Technologies.Web.TypeScript;

public class TypeScriptHandler(
	IFileSystem fileSystem,
	ITextSymbolMapper textSymbolMapper,
	ITypeScriptBridgeService bridgeService,
	ILogger<TypeScriptHandler> logger,
	IConfigurationService configurationService)
	: TypeScriptHandlerBase(fileSystem, textSymbolMapper, bridgeService, logger, configurationService)
{
}
