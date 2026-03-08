using System.IO.Abstractions;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace CodeToNeo4j.Tests.FileHandlers;

public class CSharpHandlerTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public CSharpHandlerTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task GivenExplicitInterfaceImplementation_WhenHandleCalled_ThenShouldIngestMember()
    {
        // Arrange
        var fileSystem = A.Fake<IFileSystem>();
        var symbolMapper = new SymbolMapper();
        var sut = new CSharpHandler(symbolMapper, fileSystem);

        var code = @"
public interface IBar
{
    void DoSomething();
}

public class Foo : IBar
{
    void IBar.DoSomething() { }
}";
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        var document = workspace.AddDocument(project.Id, "Foo.cs", SourceText.From(code));
        var compilation = await document.Project.GetCompilationAsync();

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        // Explicit interface implementations are usually considered private/internal in terms of accessibility,
        // but they are technically 'NotApplicable' or 'Private' depending on how Roslyn sees them.
        // We want to see if they are captured when minAccessibility is Public or Internal.
        await sut.Handle(
            document,
            compilation,
            repoKey: "test-repo",
            fileKey: "test-file",
            filePath: "Foo.cs",
            symbolBuffer,
            relBuffer,
            databaseName: "neo4j",
            minAccessibility: Accessibility.Public);

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
        var symbolMapper = new SymbolMapper();
        var sut = new CSharpHandler(symbolMapper, fileSystem);

        var code = @"
public interface IBar
{
    int Value { get; set; }
}

public class Foo : IBar
{
    int IBar.Value { get; set; }
}";
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        var document = workspace.AddDocument(project.Id, "Foo.cs", SourceText.From(code));
        var compilation = await document.Project.GetCompilationAsync();

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document,
            compilation,
            repoKey: "test-repo",
            fileKey: "test-file",
            filePath: "Foo.cs",
            symbolBuffer,
            relBuffer,
            databaseName: "neo4j",
            minAccessibility: Accessibility.Public);

        // Assert
        var explicitMember = symbolBuffer.FirstOrDefault(s => s.Name.Contains("IBar.Value"));
        explicitMember.ShouldNotBeNull("Explicit property implementation should be ingested even if minAccessibility is Public");
    }

    [Fact]
    public async Task GivenConstructorInjectedDependency_WhenHandleCalled_ThenAddsDependsOnRelationship()
    {
        // Arrange
        var fileSystem = A.Fake<IFileSystem>();
        var symbolMapper = new SymbolMapper();
        var sut = new CSharpHandler(symbolMapper, fileSystem);

        var code = @"
public interface IBarService { }
public class Foo
{
    public Foo(IBarService barService)
    {
    }
}";
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        var document = workspace.AddDocument(project.Id, "Foo.cs", SourceText.From(code));
        var compilation = await document.Project.GetCompilationAsync();

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document,
            compilation,
            repoKey: "test-repo",
            fileKey: "test-file",
            filePath: "Foo.cs",
            symbolBuffer,
            relBuffer,
            databaseName: "neo4j",
            minAccessibility: Accessibility.Private);

        // Assert
        // Find Foo symbol
        var fooSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "Foo" && s.Kind == "NamedType");
        fooSymbol.ShouldNotBeNull();

        // Find IBarService symbol
        var barServiceSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "IBarService" && s.Kind == "NamedType");
        barServiceSymbol.ShouldNotBeNull();

        // Check for DEPENDS_ON relationship
        relBuffer.ShouldContain(r =>
            r.FromKey == fooSymbol.Key &&
            r.ToKey == barServiceSymbol.Key &&
            r.RelType == "DEPENDS_ON");
    }

    [Fact]
    public async Task GivenOperators_WhenHandleCalled_ThenShouldIngestOperators()
    {
        // Arrange
        var fileSystem = A.Fake<IFileSystem>();
        var symbolMapper = new SymbolMapper();
        var sut = new CSharpHandler(symbolMapper, fileSystem);

        var code = @"
public class Foo
{
    public static bool operator ==(Foo a, Foo b) => true;
    public static bool operator !=(Foo a, Foo b) => false;
    public static implicit operator string(Foo a) => ""foo"";
    public static explicit operator int(Foo a) => 1;
}";
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        var document = workspace.AddDocument(project.Id, "Foo.cs", SourceText.From(code));
        var compilation = await document.Project.GetCompilationAsync();

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document,
            compilation,
            repoKey: "test-repo",
            fileKey: "test-file",
            filePath: "Foo.cs",
            symbolBuffer,
            relBuffer,
            databaseName: "neo4j",
            minAccessibility: Accessibility.Public);

        // Assert
        _ = symbolBuffer.Where(s => s.Kind == "Method" && (s.Name.Contains("op_") || s.Name.Contains("operator"))).ToList();

        // Roslyn names operators as op_Equality, op_Inequality, op_Implicit, op_Explicit
        symbolBuffer.ShouldContain(s => s.Name.Contains("op_Equality"));
        symbolBuffer.ShouldContain(s => s.Name.Contains("op_Inequality"));
        symbolBuffer.ShouldContain(s => s.Name.Contains("op_Implicit"));
        symbolBuffer.ShouldContain(s => s.Name.Contains("op_Explicit"));

        // Check dependencies for operator parameters
        // For 'operator ==(Foo a, Foo b)', Foo should be a dependency
        relBuffer.ShouldContain(r => r.FromKey == "test-repo:Foo" && r.ToKey == "test-repo:Foo" && r.RelType == "DEPENDS_ON");
    }

    [Fact]
    public async Task GivenEvents_WhenHandleCalled_ThenShouldIngestEvents()
    {
        // Arrange
        var fileSystem = A.Fake<IFileSystem>();
        var symbolMapper = new SymbolMapper();
        var sut = new CSharpHandler(symbolMapper, fileSystem);

        var code = @"
public class Foo
{
    public delegate void MyHandler(string s);
    public event MyHandler MyEvent;
    public event MyHandler OtherEvent { add { } remove { } }
}";
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        var document = workspace.AddDocument(project.Id, "Foo.cs", SourceText.From(code));
        var compilation = await document.Project.GetCompilationAsync();

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document,
            compilation,
            repoKey: "test-repo",
            fileKey: "test-file",
            filePath: "Foo.cs",
            symbolBuffer,
            relBuffer,
            databaseName: "neo4j",
            minAccessibility: Accessibility.Public);

        // Assert
        var events = symbolBuffer.Where(s => s.Kind == "Event").ToList();
        events.Count.ShouldBe(2);

        symbolBuffer.ShouldContain(s => s.Name == "MyEvent");
        symbolBuffer.ShouldContain(s => s.Name == "OtherEvent");

        // Check dependencies for event types
        // MyEvent depends on MyHandler
        relBuffer.ShouldContain(r => r.FromKey == "test-repo:Foo" && r.ToKey == "test-repo:Foo.MyHandler" && r.RelType == "DEPENDS_ON");

        // OtherEvent depends on MyHandler
        relBuffer.ShouldContain(r => r.FromKey == "test-repo:Foo" && r.ToKey == "test-repo:Foo.MyHandler" && r.RelType == "DEPENDS_ON");
    }

    [Fact]
    public async Task GivenNullableGenericEvent_WhenHandleCalled_ThenShouldIngestEvent()
    {
        // Arrange
        var fileSystem = A.Fake<IFileSystem>();
        var symbolMapper = new SymbolMapper();
        var sut = new CSharpHandler(symbolMapper, fileSystem);

        var code = @"
using System;
public class Foo
{
    public event EventHandler<EventArgs>? MyEvent;
}";
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(EventArgs).Assembly.Location));
        var document = workspace.AddDocument(project.Id, "Foo.cs", SourceText.From(code));
        var compilation = await document.Project.GetCompilationAsync();

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document,
            compilation,
            repoKey: "test-repo",
            fileKey: "test-file",
            filePath: "Foo.cs",
            symbolBuffer,
            relBuffer,
            databaseName: "neo4j",
            minAccessibility: Accessibility.Public);

        // Assert
        var eventSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "MyEvent");
        eventSymbol.ShouldNotBeNull();
        eventSymbol.Kind.ShouldBe("Event");

        // Check for dependency on EventHandler<EventArgs>
        // Since it's an ErrorType in this limited test context (due to missing dependencies in AdhocWorkspace),
        // but we now allow ErrorTypes to be processed if they are Nullable<T>, it should be there.
        relBuffer.ShouldContain(r =>
            r.FromKey == "test-repo:Foo" &&
            r.RelType == "DEPENDS_ON" &&
            r.ToKey.Contains("EventHandler"));
    }

    [Fact]
    public async Task GivenNonNullableEvent_WhenHandleCalled_ThenShouldIngestEvent()
    {
        // Arrange
        var fileSystem = A.Fake<IFileSystem>();
        var symbolMapper = new SymbolMapper();
        var sut = new CSharpHandler(symbolMapper, fileSystem);

        var code = @"
using System;
public class Foo
{
    public event EventHandler MyEvent;
}";
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(EventHandler).Assembly.Location));
        var document = workspace.AddDocument(project.Id, "Foo.cs", SourceText.From(code));
        var compilation = await document.Project.GetCompilationAsync();

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document,
            compilation,
            repoKey: "test-repo",
            fileKey: "test-file",
            filePath: "Foo.cs",
            symbolBuffer,
            relBuffer,
            databaseName: "neo4j",
            minAccessibility: Accessibility.Public);

        // Assert
        var eventSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "MyEvent");
        eventSymbol.ShouldNotBeNull();
        eventSymbol.Kind.ShouldBe("Event");

        // Check for dependency on EventHandler
        relBuffer.ShouldContain(r =>
            r.FromKey == "test-repo:Foo" &&
            r.RelType == "DEPENDS_ON" &&
            r.ToKey.Contains("EventHandler"));
    }

    [Fact]
    public async Task GivenNonNullableGenericEvent_WhenHandleCalled_ThenShouldIngestEvent()
    {
        // Arrange
        var fileSystem = A.Fake<IFileSystem>();
        var symbolMapper = new SymbolMapper();
        var sut = new CSharpHandler(symbolMapper, fileSystem);

        var code = @"
using System;
public class MyEventArgs : EventArgs { }
public class Foo
{
    public event EventHandler<MyEventArgs> MyEvent;
}";
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(EventArgs).Assembly.Location));
        var document = workspace.AddDocument(project.Id, "Foo.cs", SourceText.From(code));
        var compilation = await document.Project.GetCompilationAsync();

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document,
            compilation,
            repoKey: "test-repo",
            fileKey: "test-file",
            filePath: "Foo.cs",
            symbolBuffer,
            relBuffer,
            databaseName: "neo4j",
            minAccessibility: Accessibility.Public);

        // Assert
        var eventSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "MyEvent");
        eventSymbol.ShouldNotBeNull();
        eventSymbol.Kind.ShouldBe("Event");

        // Check for dependency on EventHandler<MyEventArgs>
        // Depending on how SymbolMapper builds the key, it should at least contain EventHandler and MyEventArgs
        relBuffer.ShouldContain(r =>
            r.FromKey == "test-repo:Foo" &&
            r.RelType == "DEPENDS_ON" &&
            r.ToKey.Contains("EventHandler") &&
            r.ToKey.Contains("MyEventArgs"));
    }
}