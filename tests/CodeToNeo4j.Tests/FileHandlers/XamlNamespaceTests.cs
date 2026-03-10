using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class XamlNamespaceTests
{
    [Theory]
    [InlineData("http://schemas.microsoft.com/winfx/2006/xaml")]
    [InlineData("http://schemas.microsoft.com/winfx/2009/xaml")]
    public async Task GivenXamlWithDifferentXNamespaces_WhenHandleCalled_ThenCorrectNamespaceExtracted(string xNamespace)
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var symbolMapper = new SymbolMapper();
        var symbolProcessor = new RoslynSymbolProcessor(symbolMapper);
        var sut = new XamlHandler(symbolProcessor, fileSystem);
        var content = $@"
<Window x:Class=""MyApp.MainWindow""
        xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""{xNamespace}"">
    <StackPanel x:Name=""MainPanel"">
    </StackPanel>
</Window>";
        var filePath = "test.xaml";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        var resultNamespace = await sut.Handle(
            document: null,
            compilation: null,
            repoKey: "test-repo",
            fileKey: "test-file",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        resultNamespace.ShouldBe("MyApp");
        var panelSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "MainPanel");
        panelSymbol.ShouldNotBeNull();
    }

    [Fact]
    public async Task GivenMauiXaml_WhenHandleCalled_ThenCorrectNamespaceExtracted()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var symbolMapper = new SymbolMapper();
        var symbolProcessor = new RoslynSymbolProcessor(symbolMapper);
        var sut = new XamlHandler(symbolProcessor, fileSystem);
        var content = @"
<ContentPage xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
             xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml""
             x:Class=""MauiApp.MainPage"">
    <Label x:Name=""WelcomeLabel"" Text=""Welcome to .NET MAUI!"" />
</ContentPage>";
        var filePath = "MainPage.xaml";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        var resultNamespace = await sut.Handle(
            document: null,
            compilation: null,
            repoKey: "test-repo",
            fileKey: "test-file",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        resultNamespace.ShouldBe("MauiApp");
        var labelSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "WelcomeLabel");
        labelSymbol.ShouldNotBeNull();
    }

    [Fact]
    public async Task GivenXamarinFormsXaml_WhenHandleCalled_ThenCorrectNamespaceExtracted()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var symbolMapper = new SymbolMapper();
        var symbolProcessor = new RoslynSymbolProcessor(symbolMapper);
        var sut = new XamlHandler(symbolProcessor, fileSystem);
        var content = @"
<ContentPage xmlns=""http://xamarin.com/schemas/2014/forms""
             xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml""
             x:Class=""FormsApp.MainPage"">
    <Button x:Name=""ClickMe"" />
</ContentPage>";
        var filePath = "MainPage.xaml";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        var resultNamespace = await sut.Handle(
            document: null,
            compilation: null,
            repoKey: "test-repo",
            fileKey: "test-file",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        resultNamespace.ShouldBe("FormsApp");
        var buttonSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "ClickMe");
        buttonSymbol.ShouldNotBeNull();
    }

    [Fact]
    public async Task GivenXamlWithUnprefixedName_WhenHandleCalled_ThenCorrectSymbolNameExtracted()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var symbolMapper = new SymbolMapper();
        var symbolProcessor = new RoslynSymbolProcessor(symbolMapper);
        var sut = new XamlHandler(symbolProcessor, fileSystem);
        var content = @"
<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
    <Button Name=""UnprefixedButton"" />
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
        var buttonSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "UnprefixedButton");
        buttonSymbol.ShouldNotBeNull();
    }
}
