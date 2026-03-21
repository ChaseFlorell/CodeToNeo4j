using CodeToNeo4j.Graph;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Graph;

public class NamespaceTagParserTests
{
	private readonly NamespaceTagParser _sut = new();

	[Theory]
	[InlineData("Microsoft.DotNet.Cli", new[] { "Microsoft", "DotNet", "Cli" })]
	[InlineData("SomeApp.SomeFeature.BDC", new[] { "Some_App", "Some_Feature", "BDC" })]
	[InlineData("MyApp.HTTPClient.Core", new[] { "My_App", "HTTP_Client", "Core" })]
	public void GivenMultiSegmentNamespace_WhenParseTags_ThenCorrectTagsReturned(string @namespace, string[] expected)
	{
		var result = _sut.ParseTags(@namespace);
		result.ShouldBe(expected);
	}

	[Fact]
	public void GivenAllLowercaseSegment_WhenParseTags_ThenSegmentReturnedAsIs()
	{
		var result = _sut.ParseTags("myapp.somefeature");
		result.ShouldBe(["myapp", "somefeature"]);
	}

	[Fact]
	public void GivenPascalCaseSegment_WhenParseTags_ThenWordsSeparatedByUnderscore()
	{
		var result = _sut.ParseTags("SomeFeature");
		result.ShouldBe(["Some_Feature"]);
	}

	[Fact]
	public void GivenAllCapsAcronym_WhenParseTags_ThenAcronymKeptTogether()
	{
		var result = _sut.ParseTags("BDC");
		result.ShouldBe(["BDC"]);
	}

	[Fact]
	public void GivenMixedAcronymAndPascal_WhenParseTags_ThenAcronymAndWordSplitCorrectly()
	{
		var result = _sut.ParseTags("HTTPClient");
		result.ShouldBe(["HTTP_Client"]);
	}

	[Fact]
	public void GivenSingleSegmentNamespace_WhenParseTags_ThenSingleTagReturned()
	{
		var result = _sut.ParseTags("Microsoft");
		result.ShouldBe(["Microsoft"]);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void GivenNullOrWhitespaceNamespace_WhenParseTags_ThenEmptyListReturned(string? @namespace)
	{
		var result = _sut.ParseTags(@namespace);
		result.ShouldBeEmpty();
	}

	[Fact]
	public void GivenSingleUppercaseLetter_WhenParseTags_ThenSingleLetterTagReturned()
	{
		var result = _sut.ParseTags("A");
		result.ShouldBe(["A"]);
	}

	[Fact]
	public void GivenLeadingAcronymThenPascal_WhenParseTags_ThenSplitCorrectly()
	{
		// e.g. XMLParser → XML_Parser
		var result = _sut.ParseTags("XMLParser");
		result.ShouldBe(["XML_Parser"]);
	}

	[Fact]
	public void GiveniOSSegment_WhenParseTags_ThenTagPreservedExactly()
	{
		var result = _sut.ParseTags("iOS");
		result.ShouldBe(["iOS"]);
	}

	[Fact]
	public void GivenNamespaceContainingiOS_WhenParseTags_ThenOnlyiOSSegmentPreserved()
	{
		var result = _sut.ParseTags("MyApp.iOS.Views");
		result.ShouldBe(["My_App", "iOS", "Views"]);
	}

	[Fact]
	public void GivenWhitelist_ThenContainsiOS() => NamespaceTagParser.Whitelist.ShouldContain("iOS");

	[Fact]
	public void GivenWhitelist_ThenContainsDotNet() => NamespaceTagParser.Whitelist.ShouldContain("DotNet");

	[Fact]
	public void GivenDotNetSegment_WhenParseTags_ThenTagPreservedExactly()
	{
		var result = _sut.ParseTags("DotNet");
		result.ShouldBe(["DotNet"]);
	}

	[Fact]
	public void GivenEmptySegment_WhenParseTags_ThenSkipsEmptySegment()
	{
		var result = _sut.ParseTags("MyApp..Views");
		result.ShouldBe(["My_App", "Views"]);
	}
}
