using System.IO.Abstractions;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class CSharpUsingDependencyTests
{
	private static IConfigurationService CreateConfigService()
	{
		IConfigurationService fake = A.Fake<IConfigurationService>();
		A.CallTo(() => fake.GetHandlerConfiguration(A<string>._))
			.Returns(new HandlerConfiguration([".cs"], "csharp"));
		return fake;
	}

	[Fact]
	public async Task GivenThirdPartyUsing_WhenHandleCalled_ThenAddsDependsOnRelationshipToDependency()
	{
		// Arrange
		var fileSystem = A.Fake<IFileSystem>();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor, new AccessibilityFilter());
		CSharpHandler sut = new(symbolProcessor, fileSystem, CreateConfigService());

		// We use Microsoft.CodeAnalysis as an external dependency
		var code = @"
using Microsoft.CodeAnalysis;

public class Foo
{
}";
		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(SyntaxTree).Assembly.Location));

		var document = workspace.AddDocument(project.Id, "Foo.cs", SourceText.From(code));
		var compilation = await document.Project.GetCompilationAsync();

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			document,
			compilation,
			"test-repo",
			"test-file",
			"Foo.cs", "Foo.cs",
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

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
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor, new AccessibilityFilter());
		CSharpHandler sut = new(symbolProcessor, fileSystem, CreateConfigService());

		// We use Microsoft.CodeAnalysis.CSharp.SyntaxKind as a static using
		var code = @"
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Tests.Configuration;

public class Foo
{
}";
		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(Microsoft.CodeAnalysis.CSharp.SyntaxKind).Assembly.Location));

		var document = workspace.AddDocument(project.Id, "Foo.cs", SourceText.From(code));
		var compilation = await document.Project.GetCompilationAsync();

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			document,
			compilation,
			"test-repo",
			"test-file",
			"Foo.cs", "Foo.cs",
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

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
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor, new AccessibilityFilter());
		CSharpHandler sut = new(symbolProcessor, fileSystem, CreateConfigService());

		// We use Microsoft.CodeAnalysis as a global using
		var code = @"
global using Microsoft.CodeAnalysis;

public class Foo
{
}";
		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(SyntaxTree).Assembly.Location));

		var document = workspace.AddDocument(project.Id, "Foo.cs", SourceText.From(code));
		var compilation = await document.Project.GetCompilationAsync();

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			document,
			compilation,
			"test-repo",
			"test-file",
			"Foo.cs", "Foo.cs",
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

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
		const string ExpectedFileKey = "test-file";
		var fileSystem = A.Fake<IFileSystem>();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor, new AccessibilityFilter());
		CSharpHandler sut = new(symbolProcessor, fileSystem, CreateConfigService());

		// File 1 has the global using
		var globalUsingCode = "global using Microsoft.CodeAnalysis;";
		// File 2 uses the namespace but doesn't have the using
		var code = @"
public class Foo
{
    public SyntaxTree? Tree { get; set; }
}";
		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(SyntaxTree).Assembly.Location));

		_ = workspace.AddDocument(project.Id, "GlobalUsings.cs", SourceText.From(globalUsingCode));
		var document = workspace.AddDocument(project.Id, "Foo.cs", SourceText.From(code));
		project = workspace.CurrentSolution.GetProject(project.Id)!;
		document = project.GetDocument(document.Id)!;
		var compilation = await project.GetCompilationAsync();

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			document,
			compilation,
			"test-repo",
			"test-file",
			"Foo.cs", "Foo.cs",
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		relBuffer.ShouldContain(r =>
			r.FromKey == ExpectedFileKey &&
			r.ToKey.Contains("Microsoft.CodeAnalysis") &&
			r.RelType == "DEPENDS_ON");
	}
}
