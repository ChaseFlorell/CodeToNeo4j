using System.IO.Abstractions;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Graph.Mapping;
using CodeToNeo4j.Graph.Models;
using CodeToNeo4j.Technologies.DotNet.CSharp;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Technologies.DotNet.CSharp;

public class RoslynSymbolProcessorTests
{
	private static IConfigurationService CreateConfigService()
	{
		IConfigurationService fake = A.Fake<IConfigurationService>();
		A.CallTo(() => fake.GetHandlerConfiguration(A<string>._))
			.Returns(new HandlerConfiguration([".cs"], "csharp"));
		return fake;
	}

	[Theory]
	[InlineData("Dictionary<string, List<int>>", "Items")]
	[InlineData("List<string>", "Names")]
	public async Task GivenGenericTypeProperty_WhenProcessed_ThenExtractsPropertySymbol(string genericType, string propName)
	{
		// Arrange
		var code = $$"""
		             using System.Collections.Generic;
		             public class Container
		             {
		                 public {{genericType}} {{propName}} { get; set; }
		             }
		             """;

		var (symbols, rels) = await ProcessCode(code, addCollectionsReference: true);

		// Assert
		symbols.ShouldContain(s => s.Name == "Container" && s.Kind == "NamedType");
		symbols.ShouldContain(s => s.Name == propName && s.Kind == "Property");
		rels.ShouldContain(r => r.RelType == "CONTAINS" && r.ToKey.Contains(propName));
	}

	[Fact]
	public async Task GivenNullableReferenceTypeAnnotation_WhenProcessed_ThenExtractsPropertySymbol()
	{
		// Arrange
		var code = """
		           #nullable enable
		           public class Foo
		           {
		               public string? NullableName { get; set; }
		               public string NonNullName { get; set; } = "";
		           }
		           """;

		var (symbols, _) = await ProcessCode(code);

		// Assert
		symbols.ShouldContain(s => s.Name == "NullableName");
		symbols.ShouldContain(s => s.Name == "NonNullName");
	}

	[Fact]
	public async Task GivenNestedType_WhenProcessed_ThenExtractsBothOuterAndInnerTypes()
	{
		// Arrange
		var code = """
		           public class Outer
		           {
		               public class Inner
		               {
		                   public void InnerMethod() { }
		               }
		           }
		           """;

		var (symbols, rels) = await ProcessCode(code);

		// Assert
		symbols.ShouldContain(s => s.Name == "Outer" && s.Kind == "NamedType");
		symbols.ShouldContain(s => s.Name == "Inner" && s.Kind == "NamedType");
		symbols.ShouldContain(s => s.Name == "InnerMethod");
		rels.ShouldContain(r => r.RelType == "CONTAINS" && r.ToKey.Contains("Inner"));
	}

	[Theory]
	[InlineData("public partial class Widget { public void PartA() { } }")]
	[InlineData("public partial class Widget { public void PartB() { } }")]
	public async Task GivenPartialClass_WhenProcessed_ThenExtractsTypeAndMembers(string code)
	{
		// Arrange & Act
		var (symbols, _) = await ProcessCode(code);

		// Assert
		symbols.ShouldContain(s => s.Name == "Widget" && s.Kind == "NamedType");
		symbols.Count(s => s.Kind == "Method").ShouldBe(1);
	}

	[Fact]
	public async Task GivenRecordTypeWithPrimaryConstructor_WhenProcessed_ThenExtractsRecordType()
	{
		// Arrange
		var code = """
		           public record Person(string FirstName, string LastName)
		           {
		               public string FullName => FirstName + " " + LastName;
		           }
		           """;

		var (symbols, rels) = await ProcessCode(code);

		// Assert
		symbols.ShouldContain(s => s.Name == "Person" && s.Kind == "NamedType");
		symbols.ShouldContain(s => s.Name == "FullName");
		rels.ShouldContain(r => r.RelType == "CONTAINS" && r.ToKey.Contains("FullName"));
	}

	[Fact]
	public async Task GivenExtensionMethod_WhenProcessed_ThenExtractsMethodSymbol()
	{
		// Arrange
		var code = """
		           public static class StringExtensions
		           {
		               public static string Reverse(this string input) => new string(input.ToCharArray().Reverse().ToArray());
		           }
		           """;

		var (symbols, rels) = await ProcessCode(code, addLinqReference: true);

		// Assert
		symbols.ShouldContain(s => s.Name == "StringExtensions" && s.Kind == "NamedType");
		symbols.ShouldContain(s => s.Name == "Reverse" && s.Kind == "Method");
		rels.ShouldContain(r => r.RelType == "CONTAINS" && r.ToKey.Contains("Reverse"));
	}

	[Fact]
	public async Task GivenEnumWithMembers_WhenProcessed_ThenExtractsEnumAndMembers()
	{
		// Arrange
		var code = """
		           public enum Color
		           {
		               Red,
		               Green,
		               Blue
		           }
		           """;

		var (symbols, rels) = await ProcessCode(code);

		// Assert
		symbols.ShouldContain(s => s.Name == "Color" && s.Kind == "NamedType");
		symbols.ShouldContain(s => s.Name == "Red");
		symbols.ShouldContain(s => s.Name == "Green");
		symbols.ShouldContain(s => s.Name == "Blue");
		rels.Count(r => r.RelType == "CONTAINS").ShouldBeGreaterThanOrEqualTo(3);
	}

	[Fact]
	public async Task GivenInterfaceWithGenericConstraints_WhenProcessed_ThenExtractsInterfaceAndMethod()
	{
		// Arrange
		var code = """
		           public interface IRepository<T> where T : class
		           {
		               T GetById(int id);
		               void Save(T entity);
		           }
		           """;

		var (symbols, rels) = await ProcessCode(code);

		// Assert
		symbols.ShouldContain(s => s.Name == "IRepository" && s.Kind == "NamedType");
		symbols.ShouldContain(s => s.Name == "GetById");
		symbols.ShouldContain(s => s.Name == "Save");
		rels.Count(r => r.RelType == "CONTAINS").ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public async Task GivenMinAccessibilityPublic_WhenProcessed_ThenSkipsPrivateMembers()
	{
		// Arrange
		var code = """
		           public class Foo
		           {
		               public void PublicMethod() { }
		               private void PrivateMethod() { }
		               internal void InternalMethod() { }
		           }
		           """;

		var (symbols, _) = await ProcessCode(code, Accessibility.Public, accessibilityFilter: new AccessibilityFilter());

		// Assert
		symbols.ShouldContain(s => s.Name == "PublicMethod");
		symbols.ShouldNotContain(s => s.Name == "PrivateMethod");
		symbols.ShouldNotContain(s => s.Name == "InternalMethod");
	}

	private static async Task<(List<Symbol> Symbols, List<Relationship> Rels)> ProcessCode(
		string code,
		Accessibility minAccessibility = Accessibility.Private,
		bool addLinqReference = false,
		bool addCollectionsReference = false,
		IAccessibilityFilter? accessibilityFilter = null)
	{
		var fileSystem = A.Fake<IFileSystem>();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor sut = new(symbolMapper, dependencyExtractor, accessibilityFilter ?? A.Fake<IAccessibilityFilter>());
		CSharpHandler handler = new(sut, fileSystem, CreateConfigService());

		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

		if (addLinqReference)
		{
			project = project.AddMetadataReference(
				MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location));
		}

		if (addCollectionsReference)
		{
			project = project.AddMetadataReference(
				MetadataReference.CreateFromFile(typeof(Dictionary<,>).Assembly.Location));
		}

		var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(code));
		var compilation = await document.Project.GetCompilationAsync();

		List<Symbol> symbols = [];
		List<Relationship> rels = [];

		await handler.Handle(
			document,
			compilation,
			"test-repo",
			"test-file",
			"Test.cs",
			"Test.cs",
			symbols,
			rels,
			minAccessibility);

		return (symbols, rels);
	}
}
