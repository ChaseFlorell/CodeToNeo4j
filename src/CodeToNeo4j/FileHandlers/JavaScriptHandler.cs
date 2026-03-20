using System.IO.Abstractions;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Graph;

namespace CodeToNeo4j.FileHandlers;

public class JavaScriptHandler(IFileSystem fileSystem, ITextSymbolMapper textSymbolMapper, IConfigurationService configurationService)
	: JsHandlerBase(fileSystem, textSymbolMapper, configurationService)
{
}
