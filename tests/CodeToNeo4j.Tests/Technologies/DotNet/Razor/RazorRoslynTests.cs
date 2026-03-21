using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Graph.Mapping;
using CodeToNeo4j.Graph.Models;
using CodeToNeo4j.Technologies.DotNet.CSharp;
using CodeToNeo4j.Technologies.DotNet.Razor;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Technologies.DotNet.Razor;

public class RazorRoslynTests
{
	private static IConfigurationService CreateConfigService()
	{
		IConfigurationService fake = A.Fake<IConfigurationService>();
		A.CallTo(() => fake.GetHandlerConfiguration(A<string>._))
			.Returns(new HandlerConfiguration([".razor"], "csharp"));
		return fake;
	}

	[Fact]
	public async Task GivenRazorWithGeneratedCode_WhenHandleCalled_ThenExtractsMembersViaRoslyn()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor, new AccessibilityFilter());
		RazorHandler sut = new(symbolProcessor, fileSystem, new TextSymbolMapper(), CreateConfigService());

		var razorFilePath = "Pages/Index.razor";
		var razorContent = @"@page ""/""
@namespace MyProject.Pages
@code {
    public void MyMethod() { }
}";
		fileSystem.AddFile(razorFilePath, new(razorContent));

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
		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

		// Add the generated document to the compilation
		var generatedDoc = workspace.AddDocument(project.Id, "Index.razor.g.cs", SourceText.From(generatedCode));
		var compilation = await generatedDoc.Project.GetCompilationAsync();

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			compilation,
			"test-repo",
			"Index.razor",
			razorFilePath,
			razorFilePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		// Should find the class and the method
		var classSymbol = symbolBuffer.FirstOrDefault(s => s is { Name: "Index", Kind: "NamedType" });
		classSymbol.ShouldNotBeNull();
		classSymbol.Namespace.ShouldBe("MyProject.Pages");
		classSymbol.StartLine.ShouldBe(4); // Mapped line

		var methodSymbol = symbolBuffer.FirstOrDefault(s => s is { Name: "MyMethod", Kind: "Method" });
		methodSymbol.ShouldNotBeNull();
		methodSymbol.StartLine.ShouldBe(4); // Mapped line

		relBuffer.ShouldContain(r => r.FromKey == classSymbol.Key && r.ToKey == methodSymbol.Key && r.RelType == "CONTAINS");
	}
}
