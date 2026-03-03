using System.IO.Abstractions;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class CSharpHandlerTests
{
    [Fact]
    public async Task GivenConstructorInjectedDependency_WhenHandleCalled_ThenAddsDependsOnRelationship()
    {
        // Arrange
        var fileSystem = A.Fake<IFileSystem>();
        var symbolMapper = new SymbolMapper();
        var sut = new CSharpHandler(symbolMapper, fileSystem);

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
        await sut.Handle(
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
        // Find Foo symbol
        var fooSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "Foo" && s.Kind == "NamedType");
        fooSymbol.ShouldNotBeNull();

        // Find IBarService symbol
        var barServiceSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "IBarService" && s.Kind == "NamedType");
        barServiceSymbol.ShouldNotBeNull();

        // Check for DEPENDS_ON relationship
        relBuffer.ShouldContain(r => 
            r.FromKey == fooSymbol.Key && 
            r.ToKey == barServiceSymbol.Key && 
            r.RelType == "DEPENDS_ON");
    }
}
