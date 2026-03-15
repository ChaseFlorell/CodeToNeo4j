using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class RazorRoslynTests
{
    [Fact]
    public async Task GivenRazorWithGeneratedCode_WhenHandleCalled_ThenExtractsMembersViaRoslyn()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var symbolMapper = new SymbolMapper();
        var symbolProcessor = new RoslynSymbolProcessor(symbolMapper);
        var sut = new RazorHandler(symbolProcessor, fileSystem, new TextSymbolMapper());
        
        var razorFilePath = "Pages/Index.razor";
        var razorContent = @"@page ""/""
@namespace MyProject.Pages
@code {
    public void MyMethod() { }
}";
        fileSystem.AddFile(razorFilePath, new MockFileData(razorContent));

        // Simulate generated C# code from Razor
        var generatedCode = @"
namespace MyProject.Pages
{
#line 4 ""Pages/Index.razor""
    public class Index : Microsoft.AspNetCore.Components.ComponentBase
    {
#line 4 ""Pages/Index.razor""
        public void MyMethod() { }
#line default
    }
}";
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        
        // Add the generated document to the compilation
        var generatedDoc = workspace.AddDocument(project.Id, "Index.razor.g.cs", SourceText.From(generatedCode));
        var compilation = await generatedDoc.Project.GetCompilationAsync();

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null,
            compilation: compilation,
            repoKey: "test-repo",
            fileKey: "Index.razor",
            filePath: razorFilePath,
            relativePath: razorFilePath,
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        // Should find the class and the method
        var classSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "Index" && s.Kind == "NamedType");
        classSymbol.ShouldNotBeNull();
        classSymbol.Namespace.ShouldBe("MyProject.Pages");
        classSymbol.StartLine.ShouldBe(4); // Mapped line

        var methodSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "MyMethod" && s.Kind == "Method");
        methodSymbol.ShouldNotBeNull();
        methodSymbol.StartLine.ShouldBe(4); // Mapped line
        
        relBuffer.ShouldContain(r => r.FromKey == classSymbol.Key && r.ToKey == methodSymbol.Key && r.RelType == "CONTAINS");
    }
}
