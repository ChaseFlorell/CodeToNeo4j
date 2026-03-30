using System.IO.Abstractions;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Graph;
using CodeToNeo4j.Graph.Mapping;
using CodeToNeo4j.Graph.Models;
using CodeToNeo4j.Technologies.DotNet.CSharp;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace CodeToNeo4j.Tests.Technologies.DotNet.CSharp;

public class CSharpHandlerTests
{
	private readonly ITestOutputHelper _testOutputHelper;

	private static IConfigurationService CreateConfigService()
	{
		IConfigurationService fake = A.Fake<IConfigurationService>();
		A.CallTo(() => fake.GetHandlerConfiguration(A<string>._))
			.Returns(new HandlerConfiguration([".cs"], "csharp"));
		return fake;
	}

	public CSharpHandlerTests(ITestOutputHelper testOutputHelper)
	{
		_testOutputHelper = testOutputHelper;
	}

	[Fact]
	public async Task GivenExplicitInterfaceImplementation_WhenHandleCalled_ThenShouldIngestMember()
	{
		// Arrange
		var fileSystem = A.Fake<IFileSystem>();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor, new AccessibilityFilter());
		CSharpHandler sut = new(symbolProcessor, fileSystem, CreateConfigService());

		var code = @"
public interface IBar
{
    void DoSomething();
}

public class Foo : IBar
{
    void IBar.DoSomething() { }
}";
		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
		var document = workspace.AddDocument(project.Id, "Foo.cs", SourceText.From(code));
		var compilation = await document.Project.GetCompilationAsync();

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		// Explicit interface implementations are usually considered private/internal in terms of accessibility,
		// but they are technically 'NotApplicable' or 'Private' depending on how Roslyn sees them.
		// We want to see if they are captured when minAccessibility is Public or Internal.
		await sut.Handle(
			document,
			compilation,
			"test-repo",
			"test-file",
			"Foo.cs", "Foo.cs",
			symbolBuffer,
			relBuffer,
			Accessibility.Public);

		// Assert
		var explicitMember = symbolBuffer.FirstOrDefault(s => s.Name.Contains("IBar.DoSomething"));

		// If it's missing, it means it's filtered out by accessibility check.
		explicitMember.ShouldNotBeNull("Explicit interface implementation should be ingested even if minAccessibility is Public");

		// Let's log what the accessibility actually is
		_testOutputHelper.WriteLine($"[DEBUG_LOG] Accessibility: {explicitMember.Accessibility}");
		_testOutputHelper.WriteLine($"[DEBUG_LOG] Name: {explicitMember.Name}");
	}

	[Fact]
	public async Task GivenExplicitPropertyImplementation_WhenHandleCalled_ThenShouldIngestMember()
	{
		// Arrange
		var fileSystem = A.Fake<IFileSystem>();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor, new AccessibilityFilter());
		CSharpHandler sut = new(symbolProcessor, fileSystem, CreateConfigService());

		var code = @"
public interface IBar
{
    int Value { get; set; }
}

public class Foo : IBar
{
    int IBar.Value { get; set; }
}";
		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
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
			Accessibility.Public);

		// Assert
		var explicitMember = symbolBuffer.FirstOrDefault(s => s.Name.Contains("IBar.Value"));
		explicitMember.ShouldNotBeNull("Explicit property implementation should be ingested even if minAccessibility is Public");
	}

	[Fact]
	public async Task GivenConstructorInjectedDependency_WhenHandleCalled_ThenAddsDependsOnRelationship()
	{
		// Arrange
		var fileSystem = A.Fake<IFileSystem>();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor, new AccessibilityFilter());
		CSharpHandler sut = new(symbolProcessor, fileSystem, CreateConfigService());

		var code = @"
public interface IBarService { }
public class Foo
{
    public Foo(IBarService barService)
    {
    }
}";
		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
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
		// Find Foo symbol
		var fooSymbol = symbolBuffer.FirstOrDefault(s => s is { Name: "Foo", Kind: "NamedType" });
		fooSymbol.ShouldNotBeNull();

		// Find IBarService symbol
		var barServiceSymbol = symbolBuffer.FirstOrDefault(s => s is { Name: "IBarService", Kind: "NamedType" });
		barServiceSymbol.ShouldNotBeNull();

		// Check for DEPENDS_ON relationship
		relBuffer.ShouldContain(r =>
			r.FromKey == fooSymbol.Key &&
			r.ToKey == barServiceSymbol.Key &&
			r.RelType == GraphSchema.Relationships.DependsOn);
	}

	[Fact]
	public async Task GivenOperators_WhenHandleCalled_ThenShouldIngestOperators()
	{
		// Arrange
		var fileSystem = A.Fake<IFileSystem>();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor, new AccessibilityFilter());
		CSharpHandler sut = new(symbolProcessor, fileSystem, CreateConfigService());

		var code = @"
public class Foo
{
    public static bool operator ==(Foo a, Foo b) => true;
    public static bool operator !=(Foo a, Foo b) => false;
    public static implicit operator string(Foo a) => ""foo"";
    public static explicit operator int(Foo a) => 1;
}";
		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
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
			Accessibility.Public);

		// Assert
		_ = symbolBuffer.Where(s => s.Kind == "Method" && (s.Name.Contains("op_") || s.Name.Contains("operator"))).ToList();

		// Roslyn names operators as op_Equality, op_Inequality, op_Implicit, op_Explicit
		symbolBuffer.ShouldContain(s => s.Name.Contains("op_Equality"));
		symbolBuffer.ShouldContain(s => s.Name.Contains("op_Inequality"));
		symbolBuffer.ShouldContain(s => s.Name.Contains("op_Implicit"));
		symbolBuffer.ShouldContain(s => s.Name.Contains("op_Explicit"));

		// Check dependencies for operator parameters
		// For 'operator ==(Foo a, Foo b)', Foo should be a dependency
		relBuffer.ShouldContain(r => r.FromKey == "test-repo:Foo" && r.ToKey == "test-repo:Foo" && r.RelType == GraphSchema.Relationships.DependsOn);
	}

	[Fact]
	public async Task GivenEvents_WhenHandleCalled_ThenShouldIngestEvents()
	{
		// Arrange
		var fileSystem = A.Fake<IFileSystem>();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor, new AccessibilityFilter());
		CSharpHandler sut = new(symbolProcessor, fileSystem, CreateConfigService());

		var code = @"
public class Foo
{
    public delegate void MyHandler(string s);
    public event MyHandler MyEvent;
    public event MyHandler OtherEvent { add { } remove { } }
}";
		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
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
			Accessibility.Public);

		// Assert
		List<Symbol> events = symbolBuffer.Where(s => s.Kind == "Event").ToList();
		events.Count.ShouldBe(2);

		symbolBuffer.ShouldContain(s => s.Name == "MyEvent");
		symbolBuffer.ShouldContain(s => s.Name == "OtherEvent");

		// Check dependencies for event types
		// MyEvent depends on MyHandler
		relBuffer.ShouldContain(r => r.FromKey == "test-repo:Foo" && r.ToKey == "test-repo:Foo.MyHandler" && r.RelType == GraphSchema.Relationships.DependsOn);

		// OtherEvent depends on MyHandler
		relBuffer.ShouldContain(r => r.FromKey == "test-repo:Foo" && r.ToKey == "test-repo:Foo.MyHandler" && r.RelType == GraphSchema.Relationships.DependsOn);
	}

	[Fact]
	public async Task GivenNullableGenericEvent_WhenHandleCalled_ThenShouldIngestEvent()
	{
		// Arrange
		var fileSystem = A.Fake<IFileSystem>();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor, new AccessibilityFilter());
		CSharpHandler sut = new(symbolProcessor, fileSystem, CreateConfigService());

		var code = @"
using System;
public class Foo
{
    public event EventHandler<EventArgs>? MyEvent;
}";
		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location))
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(EventArgs).Assembly.Location));
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
			Accessibility.Public);

		// Assert
		var eventSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "MyEvent");
		eventSymbol.ShouldNotBeNull();
		eventSymbol.Kind.ShouldBe("Event");

		// Check for dependency on EventHandler<EventArgs>
		// Since it's an ErrorType in this limited test context (due to missing dependencies in AdhocWorkspace),
		// but we now allow ErrorTypes to be processed if they are Nullable<T>, it should be there.
		relBuffer.ShouldContain(r =>
			r.FromKey == "test-repo:Foo" &&
			r.RelType == GraphSchema.Relationships.DependsOn &&
			r.ToKey.Contains("EventHandler"));
	}

	[Fact]
	public async Task GivenNonNullableEvent_WhenHandleCalled_ThenShouldIngestEvent()
	{
		// Arrange
		var fileSystem = A.Fake<IFileSystem>();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor, new AccessibilityFilter());
		CSharpHandler sut = new(symbolProcessor, fileSystem, CreateConfigService());

		var code = @"
using System;
public class Foo
{
    public event EventHandler MyEvent;
}";
		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location))
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(EventHandler).Assembly.Location));
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
			Accessibility.Public);

		// Assert
		var eventSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "MyEvent");
		eventSymbol.ShouldNotBeNull();
		eventSymbol.Kind.ShouldBe("Event");

		// Check for dependency on EventHandler
		relBuffer.ShouldContain(r =>
			r.FromKey == "test-repo:Foo" &&
			r.RelType == GraphSchema.Relationships.DependsOn &&
			r.ToKey.Contains("EventHandler"));
	}

	[Fact]
	public async Task GivenNonNullableGenericEvent_WhenHandleCalled_ThenShouldIngestEvent()
	{
		// Arrange
		var fileSystem = A.Fake<IFileSystem>();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor, new AccessibilityFilter());
		CSharpHandler sut = new(symbolProcessor, fileSystem, CreateConfigService());

		var code = @"
using System;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Tests.Configuration;
public class MyEventArgs : EventArgs { }
public class Foo
{
    public event EventHandler<MyEventArgs> MyEvent;
}";
		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location))
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(EventArgs).Assembly.Location));
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
			Accessibility.Public);

		// Assert
		var eventSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "MyEvent");
		eventSymbol.ShouldNotBeNull();
		eventSymbol.Kind.ShouldBe("Event");

		// Check for dependency on EventHandler<MyEventArgs>
		// Depending on how SymbolMapper builds the key, it should at least contain EventHandler and MyEventArgs
		relBuffer.ShouldContain(r =>
			r.FromKey == "test-repo:Foo" &&
			r.RelType == GraphSchema.Relationships.DependsOn &&
			r.ToKey.Contains("EventHandler") &&
			r.ToKey.Contains("MyEventArgs"));
	}

	[Fact]
	public async Task GivenMethodThatCallsAnotherMethod_WhenHandleCalled_ThenAddsInvokesRelationship()
	{
		// Arrange
		var fileSystem = A.Fake<IFileSystem>();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor, new AccessibilityFilter());
		CSharpHandler sut = new(symbolProcessor, fileSystem, CreateConfigService());

		var code = @"
public class OrderService
{
    public void ProcessOrder()
    {
        Validate();
        Save();
    }

    private void Validate() { }
    private void Save() { }
}";
		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
		var document = workspace.AddDocument(project.Id, "OrderService.cs", SourceText.From(code));
		var compilation = await document.Project.GetCompilationAsync();

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			document,
			compilation,
			"test-repo",
			"test-file",
			"OrderService.cs", "OrderService.cs",
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		var processOrder = symbolBuffer.FirstOrDefault(s => s.Name == "ProcessOrder");
		processOrder.ShouldNotBeNull();

		relBuffer.ShouldContain(r =>
			r.FromKey == processOrder.Key &&
			r.RelType == GraphSchema.Relationships.Invokes &&
			r.ToKey.Contains("Validate"));

		relBuffer.ShouldContain(r =>
			r.FromKey == processOrder.Key &&
			r.RelType == GraphSchema.Relationships.Invokes &&
			r.ToKey.Contains("Save"));
	}

	[Fact]
	public async Task GivenMethodThatUsesNewExpression_WhenHandleCalled_ThenAddsInvokesRelationshipForConstructor()
	{
		// Arrange
		var fileSystem = A.Fake<IFileSystem>();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor, new AccessibilityFilter());
		CSharpHandler sut = new(symbolProcessor, fileSystem, CreateConfigService());

		var code = @"
public class Widget
{
    public Widget() { }
}
public class Factory
{
    public Widget Create()
    {
        return new Widget();
    }
}";
		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
		var document = workspace.AddDocument(project.Id, "Factory.cs", SourceText.From(code));
		var compilation = await document.Project.GetCompilationAsync();

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			document,
			compilation,
			"test-repo",
			"test-file",
			"Factory.cs", "Factory.cs",
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		var createMethod = symbolBuffer.FirstOrDefault(s => s.Name == "Create");
		createMethod.ShouldNotBeNull();

		relBuffer.ShouldContain(r =>
			r.FromKey == createMethod.Key &&
			r.RelType == GraphSchema.Relationships.Invokes &&
			r.ToKey.Contains("Widget"));
	}

	[Theory]
	[InlineData("Program.cs", true)]
	[InlineData("Service.CS", true)]
	[InlineData("file.ts", false)]
	[InlineData("file.razor", false)]
	public void GivenFilePath_WhenCanHandleCalled_ThenMatchesCsExtensionOnly(string path, bool expected)
	{
		CSharpHandler sut = new(A.Fake<IRoslynSymbolProcessor>(), A.Fake<IFileSystem>(), CreateConfigService());
		sut.CanHandle(path).ShouldBe(expected);
		sut.FileExtension.ShouldBe(".cs");
	}

	[Fact]
	public async Task GivenMethodWithNoCallsToLocalMethods_WhenHandleCalled_ThenNoInvokesRelationship()
	{
		// Arrange
		var fileSystem = A.Fake<IFileSystem>();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor, new AccessibilityFilter());
		CSharpHandler sut = new(symbolProcessor, fileSystem, CreateConfigService());

		var code = @"
public class Pure
{
    public int Add(int a, int b) => a + b;
}";
		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
		var document = workspace.AddDocument(project.Id, "Pure.cs", SourceText.From(code));
		var compilation = await document.Project.GetCompilationAsync();

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			document,
			compilation,
			"test-repo",
			"test-file",
			"Pure.cs", "Pure.cs",
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		relBuffer.ShouldNotContain(r => r.RelType == GraphSchema.Relationships.Invokes);
	}

	[Fact]
	public async Task GivenCSharpHandler_WhenHandleCalled_ThenAllSymbolsHaveLanguageCsharp()
	{
		// Arrange
		var fileSystem = A.Fake<IFileSystem>();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor, new AccessibilityFilter());
		CSharpHandler sut = new(symbolProcessor, fileSystem, CreateConfigService());

		var code = """
			public class Foo
			{
			    public void Bar() { }
			}
			""";
		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
		var document = workspace.AddDocument(project.Id, "Foo.cs", SourceText.From(code));
		var compilation = await document.Project.GetCompilationAsync();

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(document, compilation, null, "file-key", "Foo.cs", "Foo.cs", symbolBuffer, relBuffer, Accessibility.Public);

		// Assert
		symbolBuffer.ShouldNotBeEmpty();
		symbolBuffer.ShouldAllBe(s => s.Language == "csharp");
	}
}
