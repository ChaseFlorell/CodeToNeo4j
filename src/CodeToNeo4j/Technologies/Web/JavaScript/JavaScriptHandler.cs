using System.IO.Abstractions;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Graph.Mapping;

namespace CodeToNeo4j.Technologies.Web.JavaScript;

public class JavaScriptHandler(IFileSystem fileSystem, ITextSymbolMapper textSymbolMapper, IConfigurationService configurationService)
	: JsHandlerBase(fileSystem, textSymbolMapper, configurationService)
{
}
