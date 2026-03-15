using System.IO.Abstractions;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace CodeToNeo4j.Tests.FileHandlers;

public class CSharpUsingDependencyTests
{
    [Fact]
    public async Task GivenThirdPartyUsing_WhenHandleCalled_ThenAddsDependsOnRelationshipToDependency()
    {
        // Arrange
        var fileSystem = A.Fake<IFileSystem>();
        var symbolMapper = new SymbolMapper();
        var symbolProcessor = new RoslynSymbolProcessor(symbolMapper);
        var sut = new CSharpHandler(symbolProcessor, fileSystem);

        // We use Microsoft.CodeAnalysis as an external dependency
        var code = @"
using Microsoft.CodeAnalysis;

public class Foo
{
}";
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Microsoft.CodeAnalysis.SyntaxTree).Assembly.Location));

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
            filePath: "Foo.cs", relativePath: "Foo.cs",
            symbolBuffer,
            relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        var expectedFileKey = "test-file";

        // Check for DEPENDS_ON relationship from expectedFileKey to Microsoft.CodeAnalysis
        relBuffer.ShouldContain(r =>
            r.FromKey == expectedFileKey &&
            r.ToKey.Contains("Microsoft.CodeAnalysis") &&
            r.RelType == "DEPENDS_ON");
    }
    [Fact]
    public async Task GivenStaticUsing_WhenHandleCalled_ThenAddsDependsOnRelationshipToDependency()
    {
        // Arrange
        var fileSystem = A.Fake<IFileSystem>();
        var symbolMapper = new SymbolMapper();
        var symbolProcessor = new RoslynSymbolProcessor(symbolMapper);
        var sut = new CSharpHandler(symbolProcessor, fileSystem);

        // We use Microsoft.CodeAnalysis.CSharp.SyntaxKind as a static using
        var code = @"
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;

public class Foo
{
}";
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Microsoft.CodeAnalysis.CSharp.SyntaxKind).Assembly.Location));

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
            filePath: "Foo.cs", relativePath: "Foo.cs",
            symbolBuffer,
            relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        var expectedFileKey = "test-file";

        // Check for DEPENDS_ON relationship from expectedFileKey to Microsoft.CodeAnalysis.CSharp.SyntaxKind
        relBuffer.ShouldContain(r =>
            r.FromKey == expectedFileKey &&
            r.ToKey.Contains("Microsoft.CodeAnalysis.CSharp.SyntaxKind") &&
            r.RelType == "DEPENDS_ON");
    }

    [Fact]
    public async Task GivenGlobalUsing_WhenHandleCalled_ThenAddsDependsOnRelationshipToDependency()
    {
        // Arrange
        var fileSystem = A.Fake<IFileSystem>();
        var symbolMapper = new SymbolMapper();
        var symbolProcessor = new RoslynSymbolProcessor(symbolMapper);
        var sut = new CSharpHandler(symbolProcessor, fileSystem);

        // We use Microsoft.CodeAnalysis as a global using
        var code = @"
global using Microsoft.CodeAnalysis;

public class Foo
{
}";
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Microsoft.CodeAnalysis.SyntaxTree).Assembly.Location));

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
            filePath: "Foo.cs", relativePath: "Foo.cs",
            symbolBuffer,
            relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        var expectedFileKey = "test-file";

        // Check for DEPENDS_ON relationship from expectedFileKey to Microsoft.CodeAnalysis
        relBuffer.ShouldContain(r =>
            r.FromKey == expectedFileKey &&
            r.ToKey.Contains("Microsoft.CodeAnalysis") &&
            r.RelType == "DEPENDS_ON");
    }
    [Fact]
    public async Task GivenGlobalUsingInAnotherFile_WhenHandleCalled_ThenAddsDependsOnRelationshipToDependency()
    {
        // Arrange
        var fileSystem = A.Fake<IFileSystem>();
        var symbolMapper = new SymbolMapper();
        var symbolProcessor = new RoslynSymbolProcessor(symbolMapper);
        var sut = new CSharpHandler(symbolProcessor, fileSystem);

        // File 1 has the global using
        var globalUsingCode = "global using Microsoft.CodeAnalysis;";
        // File 2 uses the namespace but doesn't have the using
        var code = @"
public class Foo
{
    public SyntaxTree? Tree { get; set; }
}";
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Microsoft.CodeAnalysis.SyntaxTree).Assembly.Location));

        _ = workspace.AddDocument(project.Id, "GlobalUsings.cs", SourceText.From(globalUsingCode));
        var document = workspace.AddDocument(project.Id, "Foo.cs", SourceText.From(code));
        project = workspace.CurrentSolution.GetProject(project.Id)!;
        document = project.GetDocument(document.Id)!;
        var compilation = await project.GetCompilationAsync();

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document,
            compilation,
            repoKey: "test-repo",
            fileKey: "test-file",
            filePath: "Foo.cs", relativePath: "Foo.cs",
            symbolBuffer,
            relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        var expectedFileKey = "test-file";

        // This currently FAILS because we only look at using directives in the current syntax tree.
        relBuffer.ShouldContain(r =>
            r.FromKey == expectedFileKey &&
            r.ToKey.Contains("Microsoft.CodeAnalysis") &&
            r.RelType == "DEPENDS_ON");
    }
}
