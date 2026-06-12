using System.IO.Abstractions;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Graph.Mapping;
using CodeToNeo4j.TypeScript.Bridge;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Technologies.Web.JavaScript;

public class JavaScriptHandler(
	IFileSystem fileSystem,
	ITextSymbolMapper textSymbolMapper,
	ITypeScriptBridgeService bridgeService,
	ILogger<JavaScriptHandler> logger,
	IConfigurationService configurationService)
	: TypeScriptHandlerBase(fileSystem, textSymbolMapper, bridgeService, logger, configurationService)
{
}
