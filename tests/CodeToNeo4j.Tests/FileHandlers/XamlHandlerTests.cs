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

public class XamlHandlerTests
{
	private static IConfigurationService CreateConfigService()
	{
		IConfigurationService fake = A.Fake<IConfigurationService>();
		A.CallTo(() => fake.GetHandlerConfiguration(A<string>._))
			.Returns(new HandlerConfiguration([".xaml"], "xaml"));
		return fake;
	}

	[Fact]
	public async Task GivenXamlWithElementsAndEvents_WhenHandleCalled_ThenAddsSymbolsAndRelationships()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor);
		XamlHandler sut = new(symbolProcessor, fileSystem, new TextSymbolMapper(), NullLogger<XamlHandler>.Instance, CreateConfigService());
		var content = @"
<Window x:Class=""MyApp.MainWindow""
        xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <StackPanel x:Name=""MainPanel"">
        <Button x:Name=""SubmitButton"" Click=""SubmitButton_Click"" Content=""Submit"" />
    </StackPanel>
</Window>";
		var filePath = "test.xaml";
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
