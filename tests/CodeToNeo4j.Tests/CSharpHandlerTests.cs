using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using NSubstitute;
using System.IO.Abstractions;
using Xunit;

namespace CodeToNeo4j.Tests;

public class CSharpHandlerTests
{
    [Fact]
    public async Task Handle_ConstructorInjectedDependency_AddsDependsOnRelationship()
    {
        // Arrange
        var fileSystem = Substitute.For<IFileSystem>();
        var symbolMapper = new SymbolMapper();
        var handler = new CSharpHandler(symbolMapper, fileSystem);

        var code = @"
public interface IBarService { }
public class Foo
{
    public Foo(IBarService barService)
    {
    }
}";
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        var document = workspace.AddDocument(project.Id, "Foo.cs", SourceText.From(code));
        var compilation = await document.Project.GetCompilationAsync();

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await handler.Handle(
            document,
            compilation,
            repoKey: "test-repo",
            fileKey: "test-file",
            filePath: "Foo.cs",
            symbolBuffer,
            relBuffer,
            databaseName: "neo4j",
            minAccessibility: Accessibility.Private);

        // Assert
        // Log symbols for debugging
        foreach (var s in symbolBuffer)
        {
            Console.WriteLine($"[DEBUG_LOG] Symbol: {s.Name}, Kind: {s.Kind}");
        }

        // Find Foo symbol
        var fooSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "Foo" && s.Kind == "NamedType");
        fooSymbol.Should().NotBeNull();

        // Find IBarService symbol
        var barServiceSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "IBarService" && s.Kind == "NamedType");
        barServiceSymbol.Should().NotBeNull();

        // Check for DEPENDS_ON relationship
        relBuffer.Should().Contain(r => 
            r.FromKey == fooSymbol.Key && 
            r.ToKey == barServiceSymbol.Key && 
            r.RelType == "DEPENDS_ON");
    }
}
