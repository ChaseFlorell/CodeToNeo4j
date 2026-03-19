using CodeToNeo4j.Graph;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Graph;

public class TextSymbolMapperTests
{
	private readonly TextSymbolMapper _sut = new();

	// BuildKey

	[Fact]
	public void GivenStartLine_WhenBuildKey_ThenIncludesLineInKey()
	{
		var key = _sut.BuildKey("file.js", "Function", "myFunc", 42);
		key.ShouldBe("file.js:Function:myFunc:42");
	}

	[Fact]
	public void GivenNoStartLine_WhenBuildKey_ThenOmitsLineFromKey()
	{
		var key = _sut.BuildKey("file.js", "Import", "./bar");
		key.ShouldBe("file.js:Import:./bar");
	}

	[Fact]
	public void GivenNullStartLine_WhenBuildKey_ThenOmitsLineFromKey()
	{
		var key = _sut.BuildKey("file.js", "Kind", "name", null);
		key.ShouldBe("file.js:Kind:name");
	}

	// CreateSymbol — enforced invariants

	[Fact]
	public void GivenStartLine_WhenCreateSymbol_ThenEndLineEqualsStartLine()
	{
		var symbol = BuildSymbol(7);
		symbol.EndLine.ShouldBe(symbol.StartLine);
	}

	[Fact]
	public void WhenCreateSymbol_ThenCommentsIsAlwaysNull()
	{
		var symbol = BuildSymbol();
		symbol.Comments.ShouldBeNull();
	}

	[Fact]
	public void GivenNoAccessibility_WhenCreateSymbol_ThenDefaultsToPublic()
	{
		var symbol = BuildSymbol();
		symbol.Accessibility.ShouldBe("Public");
	}

	[Fact]
	public void GivenExplicitAccessibility_WhenCreateSymbol_ThenUsesProvidedValue()
	{
		var symbol = BuildSymbol(accessibility: "Private");
		symbol.Accessibility.ShouldBe("Private");
	}

	[Fact]
	public void GivenDocumentation_WhenCreateSymbol_ThenDocumentationIsSet()
	{
		var symbol = BuildSymbol(documentation: "some docs");
		symbol.Documentation.ShouldBe("some docs");
	}

	[Fact]
	public void GivenNoDocumentation_WhenCreateSymbol_ThenDocumentationIsNull()
	{
		var symbol = BuildSymbol();
		symbol.Documentation.ShouldBeNull();
	}

	[Fact]
	public void GivenVersion_WhenCreateSymbol_ThenVersionIsSet()
	{
		var symbol = BuildSymbol(version: "1.2.3");
		symbol.Version.ShouldBe("1.2.3");
	}

	[Fact]
	public void GivenAllCoreFields_WhenCreateSymbol_ThenFieldsAreSetCorrectly()
	{
		var symbol = _sut.CreateSymbol(
			"file.js:Function:foo:5",
			"foo",
			"JavaScriptFunction",
			"function",
			"foo",
			"file.js",
			"src/file.js",
			"src",
			5);

		symbol.Key.ShouldBe("file.js:Function:foo:5");
		symbol.Name.ShouldBe("foo");
		symbol.Kind.ShouldBe("JavaScriptFunction");
		symbol.Class.ShouldBe("function");
		symbol.Fqn.ShouldBe("foo");
		symbol.FileKey.ShouldBe("file.js");
		symbol.RelativePath.ShouldBe("src/file.js");
		symbol.Namespace.ShouldBe("src");
		symbol.StartLine.ShouldBe(5);
		symbol.EndLine.ShouldBe(5);
	}

	private Symbol BuildSymbol(
		int startLine = 1,
		string accessibility = "Public",
		string? documentation = null,
		string? version = null)
		=> _sut.CreateSymbol(
			"file:Kind:name:1",
			"name",
			"Kind",
			"class",
			"name",
			"file",
			"file",
			null,
			startLine,
			accessibility,
			documentation,
			version);
}
