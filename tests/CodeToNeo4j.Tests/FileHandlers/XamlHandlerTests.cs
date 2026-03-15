using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class XamlHandlerTests
{
    [Fact]
    public async Task GivenXamlWithElementsAndEvents_WhenHandleCalled_ThenAddsSymbolsAndRelationships()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var symbolMapper = new SymbolMapper();
        var symbolProcessor = new RoslynSymbolProcessor(symbolMapper);
        var sut = new XamlHandler(symbolProcessor, fileSystem);
        var content = @"
<Window x:Class=""MyApp.MainWindow""
        xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <StackPanel x:Name=""MainPanel"">
        <Button x:Name=""SubmitButton"" Click=""SubmitButton_Click"" Content=""Submit"" />
    </StackPanel>
</Window>";
        var filePath = "test.xaml";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null,
            compilation: null,
            repoKey: "test-repo",
            fileKey: "test-file",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        var windowSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "Window");
        windowSymbol.ShouldNotBeNull();

        var panelSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "MainPanel");
        panelSymbol.ShouldNotBeNull();

        var buttonSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "SubmitButton");
        buttonSymbol.ShouldNotBeNull();

        var handlerSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "SubmitButton_Click");
        handlerSymbol.ShouldNotBeNull();
        handlerSymbol.Kind.ShouldBe("XamlEventHandler");

        relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == windowSymbol.Key && r.RelType == "CONTAINS");
        relBuffer.ShouldContain(r => r.FromKey == buttonSymbol.Key && r.ToKey == handlerSymbol.Key && r.RelType == "BINDS_TO");
    }
}
