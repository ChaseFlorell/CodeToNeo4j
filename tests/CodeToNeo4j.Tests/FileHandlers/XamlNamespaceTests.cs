using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class XamlNamespaceTests
{
	private static IConfigurationService CreateConfigService()
	{
		IConfigurationService fake = A.Fake<IConfigurationService>();
		A.CallTo(() => fake.GetHandlerConfiguration(A<string>._))
			.Returns(new HandlerConfiguration([".xaml"], "xaml"));
		return fake;
	}

	[Theory]
	[InlineData("http://schemas.microsoft.com/winfx/2006/xaml")]
	[InlineData("http://schemas.microsoft.com/winfx/2009/xaml")]
	public async Task GivenXamlWithDifferentXNamespaces_WhenHandleCalled_ThenCorrectNamespaceExtracted(string xNamespace)
	{
		// Arrange
		MockFileSystem fileSystem = new();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor);
		XamlHandler sut = new(symbolProcessor, fileSystem, new TextSymbolMapper(), NullLogger<XamlHandler>.Instance, CreateConfigService());
		var content = $@"
<Window x:Class=""MyApp.MainWindow""
        xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""{xNamespace}"">
    <StackPanel x:Name=""MainPanel"">
    </StackPanel>
</Window>";
		const string filePath = "test.xaml";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var resultNamespace = await sut.Handle(
			null,
			null,
			"test-repo",
			"test-file",
			filePath, filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		resultNamespace.Namespace.ShouldBe("MyApp");
		var panelSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "MainPanel");
		panelSymbol.ShouldNotBeNull();
	}

	[Fact]
	public async Task GivenMauiXaml_WhenHandleCalled_ThenCorrectNamespaceExtracted()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor);
		XamlHandler sut = new(symbolProcessor, fileSystem, new TextSymbolMapper(), NullLogger<XamlHandler>.Instance, CreateConfigService());
		var content = @"
<ContentPage xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
             xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml""
             x:Class=""MauiApp.MainPage"">
    <Label x:Name=""WelcomeLabel"" Text=""Welcome to .NET MAUI!"" />
</ContentPage>";
		const string filePath = "MainPage.xaml";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var resultNamespace = await sut.Handle(
			null,
			null,
			"test-repo",
			"test-file",
			filePath, filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		resultNamespace.Namespace.ShouldBe("MauiApp");
		var labelSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "WelcomeLabel");
		labelSymbol.ShouldNotBeNull();
	}

	[Fact]
	public async Task GivenXamarinFormsXaml_WhenHandleCalled_ThenCorrectNamespaceExtracted()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor);
		XamlHandler sut = new(symbolProcessor, fileSystem, new TextSymbolMapper(), NullLogger<XamlHandler>.Instance, CreateConfigService());
		var content = @"
<ContentPage xmlns=""http://xamarin.com/schemas/2014/forms""
             xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml""
             x:Class=""FormsApp.MainPage"">
    <Button x:Name=""ClickMe"" />
</ContentPage>";
		const string filePath = "MainPage.xaml";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var resultNamespace = await sut.Handle(
			null,
			null,
			"test-repo",
			"test-file",
			filePath, filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		resultNamespace.Namespace.ShouldBe("FormsApp");
		var buttonSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "ClickMe");
		buttonSymbol.ShouldNotBeNull();
	}

	[Fact]
	public async Task GivenXamlWithUnprefixedName_WhenHandleCalled_ThenCorrectSymbolNameExtracted()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor);
		XamlHandler sut = new(symbolProcessor, fileSystem, new TextSymbolMapper(), NullLogger<XamlHandler>.Instance, CreateConfigService());
		var content = @"
<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
    <Button Name=""UnprefixedButton"" />
</Window>";
		const string filePath = "test.xaml";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"test-file",
			filePath, filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		var buttonSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "UnprefixedButton");
		buttonSymbol.ShouldNotBeNull();
	}
}
