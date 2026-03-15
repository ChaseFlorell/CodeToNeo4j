using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class XamlRoslynTests
{
    [Fact]
    public async Task GivenXamlWithGeneratedCode_WhenHandleCalled_ThenExtractsMembersViaRoslyn()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var symbolMapper = new SymbolMapper();
        var dependencyExtractor = new MemberDependencyExtractor(symbolMapper);
        var symbolProcessor = new RoslynSymbolProcessor(symbolMapper, dependencyExtractor);
        var sut = new XamlHandler(symbolProcessor, fileSystem, new TextSymbolMapper(), NullLogger<XamlHandler>.Instance);
        
        var xamlFilePath = "MainWindow.xaml";
        var xamlContent = @"<Window x:Class=""MyApp.MainWindow""
        xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <Button x:Name=""SubmitButton"" />
</Window>";
        fileSystem.AddFile(xamlFilePath, new MockFileData(xamlContent));

        // Simulate generated C# code from XAML (simplified)
        var generatedCode = @"
namespace MyApp
{
#line 1 ""MainWindow.xaml""
    public partial class MainWindow : System.Windows.Window, System.Windows.Markup.IComponentConnector
    {
#line 4 ""MainWindow.xaml""
        internal System.Windows.Controls.Button SubmitButton;
#line default
    }
}";
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        
        var generatedDoc = workspace.AddDocument(project.Id, "MainWindow.g.cs", SourceText.From(generatedCode));
        var compilation = await generatedDoc.Project.GetCompilationAsync();

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null,
            compilation: compilation,
            repoKey: "test-repo",
            fileKey: "MainWindow.xaml",
            filePath: xamlFilePath,
            relativePath: xamlFilePath,
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        // Should find the class and the generated field
        var classSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "MainWindow" && s.Kind == "NamedType");
        classSymbol.ShouldNotBeNull();
        
        var fieldSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "SubmitButton" && s.Kind == "Field");
        fieldSymbol.ShouldNotBeNull();
        fieldSymbol.StartLine.ShouldBe(4); // Mapped line
    }
}
