using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class RazorHandlerTests
{
	private static IConfigurationService CreateConfigService()
	{
		IConfigurationService fake = A.Fake<IConfigurationService>();
		A.CallTo(() => fake.GetHandlerConfiguration(A<string>._))
			.Returns(new HandlerConfiguration([".razor"], "csharp"));
		return fake;
	}

	[Fact]
	public async Task GivenRazorWithNamespace_WhenHandleCalled_ThenCapturesNamespace()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor, new AccessibilityFilter());
		RazorHandler sut = new(symbolProcessor, fileSystem, new TextSymbolMapper(), CreateConfigService());
		var content = @"@namespace MyProject.Pages
<h1>Hello</h1>";
		var filePath = "test.razor";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var result = await sut.Handle(
			null,
			null,
			"test-repo",
			"test.razor",
			filePath, filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		result.Namespace.ShouldBe("MyProject.Pages");
	}

	[Fact]
	public async Task GivenRazorWithDirectives_WhenHandleCalled_ThenAddsSymbolsAndRelationships()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor, new AccessibilityFilter());
		RazorHandler sut = new(symbolProcessor, fileSystem, new TextSymbolMapper(), CreateConfigService());
		var content = @"
@using System.Text
@inject IMyService MyService
@model MyViewModel
@inherits MyBasePage
<h1>Hello</h1>";
		var filePath = "test.razor";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"test.razor",
			filePath, filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		symbolBuffer.Any(s => s is { Kind: "UsingDirective", Name: "System.Text" }).ShouldBeTrue();
		symbolBuffer.Any(s => s is { Kind: "InjectDirective", Name: "IMyService MyService" }).ShouldBeTrue();
		symbolBuffer.Any(s => s is { Kind: "ModelDirective", Name: "MyViewModel" }).ShouldBeTrue();
		symbolBuffer.Any(s => s is { Kind: "InheritsDirective", Name: "MyBasePage" }).ShouldBeTrue();

		relBuffer.Count.ShouldBe(4);
	}
}
