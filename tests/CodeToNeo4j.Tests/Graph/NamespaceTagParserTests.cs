using CodeToNeo4j.Graph;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Graph;

public class NamespaceTagParserTests
{
	[Theory]
	[InlineData("Microsoft.DotNet.Cli", new[] { "Microsoft", "DotNet", "Cli" })]
	[InlineData("SomeApp.SomeFeature.BDC", new[] { "Some_App", "Some_Feature", "BDC" })]
	[InlineData("MyApp.HTTPClient.Core", new[] { "My_App", "HTTP_Client", "Core" })]
	public void GivenMultiSegmentNamespace_WhenParseTags_ThenCorrectTagsReturned(string @namespace, string[] expected)
	{
		NamespaceTagParser sut = new();
		var result = sut.ParseTags(@namespace);
		result.ShouldBe(expected);
	}

	[Fact]
	public void GivenAllLowercaseSegment_WhenParseTags_ThenSegmentReturnedAsIs()
	{
		NamespaceTagParser sut = new();
		var result = sut.ParseTags("myapp.somefeature");
		result.ShouldBe(["myapp", "somefeature"]);
	}

	[Fact]
	public void GivenPascalCaseSegment_WhenParseTags_ThenWordsSeparatedByUnderscore()
	{
		NamespaceTagParser sut = new();
		var result = sut.ParseTags("SomeFeature");
		result.ShouldBe(["Some_Feature"]);
	}

	[Fact]
	public void GivenAllCapsAcronym_WhenParseTags_ThenAcronymKeptTogether()
	{
		NamespaceTagParser sut = new();
		var result = sut.ParseTags("BDC");
		result.ShouldBe(["BDC"]);
	}

	[Fact]
	public void GivenMixedAcronymAndPascal_WhenParseTags_ThenAcronymAndWordSplitCorrectly()
	{
		NamespaceTagParser sut = new();
		var result = sut.ParseTags("HTTPClient");
		result.ShouldBe(["HTTP_Client"]);
	}

	[Fact]
	public void GivenSingleSegmentNamespace_WhenParseTags_ThenSingleTagReturned()
	{
		NamespaceTagParser sut = new();
		var result = sut.ParseTags("Microsoft");
		result.ShouldBe(["Microsoft"]);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void GivenNullOrWhitespaceNamespace_WhenParseTags_ThenEmptyListReturned(string? @namespace)
	{
		NamespaceTagParser sut = new();
		var result = sut.ParseTags(@namespace);
		result.ShouldBeEmpty();
	}

	[Fact]
	public void GivenSingleUppercaseLetter_WhenParseTags_ThenSingleLetterTagReturned()
	{
		NamespaceTagParser sut = new();
		var result = sut.ParseTags("A");
		result.ShouldBe(["A"]);
	}

	[Fact]
	public void GivenLeadingAcronymThenPascal_WhenParseTags_ThenSplitCorrectly()
	{
		// e.g. XMLParser → XML_Parser
		NamespaceTagParser sut = new();
		var result = sut.ParseTags("XMLParser");
		result.ShouldBe(["XML_Parser"]);
	}

	[Fact]
	public void GiveniOSSegment_WhenParseTags_ThenTagPreservedExactly()
	{
		NamespaceTagParser sut = new();
		var result = sut.ParseTags("iOS");
		result.ShouldBe(["iOS"]);
	}

	[Fact]
	public void GivenNamespaceContainingiOS_WhenParseTags_ThenOnlyiOSSegmentPreserved()
	{
		NamespaceTagParser sut = new();
		var result = sut.ParseTags("MyApp.iOS.Views");
		result.ShouldBe(["My_App", "iOS", "Views"]);
	}

	[Fact]
	public void GivenWhitelist_ThenContainsiOS() => NamespaceTagParser.Whitelist.ShouldContain("iOS");

	[Fact]
	public void GivenWhitelist_ThenContainsDotNet() => NamespaceTagParser.Whitelist.ShouldContain("DotNet");

	[Fact]
	public void GivenDotNetSegment_WhenParseTags_ThenTagPreservedExactly()
	{
		NamespaceTagParser sut = new();
		var result = sut.ParseTags("DotNet");
		result.ShouldBe(["DotNet"]);
	}

	[Fact]
	public void GivenEmptySegment_WhenParseTags_ThenSkipsEmptySegment()
	{
		NamespaceTagParser sut = new();
		var result = sut.ParseTags("MyApp..Views");
		result.ShouldBe(["My_App", "Views"]);
	}
}
