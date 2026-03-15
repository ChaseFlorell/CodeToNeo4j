using System.IO.Abstractions;
using CodeToNeo4j.Graph;

namespace CodeToNeo4j.FileHandlers;

public class JavaScriptHandler(IFileSystem fileSystem, ITextSymbolMapper textSymbolMapper) : JsHandlerBase(fileSystem, textSymbolMapper)
{
    public override string FileExtension => ".js";
    protected override string KindPrefix => "JavaScript";
}
