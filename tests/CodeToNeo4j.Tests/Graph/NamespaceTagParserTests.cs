using CodeToNeo4j.Graph;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Graph;

public class NamespaceTagParserTests
{
    [Theory]
    [InlineData("Microsoft.DotNet.Cli", new[] { "Microsoft", "Dot_Net", "Cli" })]
    [InlineData("SomeApp.SomeFeature.BDC", new[] { "Some_App", "Some_Feature", "BDC" })]
    [InlineData("MyApp.HTTPClient.Core", new[] { "My_App", "HTTP_Client", "Core" })]
    public void GivenMultiSegmentNamespace_WhenParseTags_ThenCorrectTagsReturned(string @namespace, string[] expected)
    {
        var result = NamespaceTagParser.ParseTags(@namespace);
        result.ShouldBe(expected);
    }

    [Fact]
    public void GivenAllLowercaseSegment_WhenParseTags_ThenSegmentReturnedAsIs()
    {
        var result = NamespaceTagParser.ParseTags("myapp.somefeature");
        result.ShouldBe(["myapp", "somefeature"]);
    }

    [Fact]
    public void GivenPascalCaseSegment_WhenParseTags_ThenWordsSeparatedByUnderscore()
    {
        var result = NamespaceTagParser.ParseTags("SomeFeature");
        result.ShouldBe(["Some_Feature"]);
    }

    [Fact]
    public void GivenAllCapsAcronym_WhenParseTags_ThenAcronymKeptTogether()
    {
        var result = NamespaceTagParser.ParseTags("BDC");
        result.ShouldBe(["BDC"]);
    }

    [Fact]
    public void GivenMixedAcronymAndPascal_WhenParseTags_ThenAcronymAndWordSplitCorrectly()
    {
        var result = NamespaceTagParser.ParseTags("HTTPClient");
        result.ShouldBe(["HTTP_Client"]);
    }

    [Fact]
    public void GivenSingleSegmentNamespace_WhenParseTags_ThenSingleTagReturned()
    {
        var result = NamespaceTagParser.ParseTags("Microsoft");
        result.ShouldBe(["Microsoft"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenNullOrWhitespaceNamespace_WhenParseTags_ThenEmptyListReturned(string? @namespace)
    {
        var result = NamespaceTagParser.ParseTags(@namespace);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void GivenSingleUppercaseLetter_WhenParseTags_ThenSingleLetterTagReturned()
    {
        var result = NamespaceTagParser.ParseTags("A");
        result.ShouldBe(["A"]);
    }

    [Fact]
    public void GivenLeadingAcronymThenPascal_WhenParseTags_ThenSplitCorrectly()
    {
        // e.g. XMLParser → XML_Parser
        var result = NamespaceTagParser.ParseTags("XMLParser");
        result.ShouldBe(["XML_Parser"]);
    }
}
