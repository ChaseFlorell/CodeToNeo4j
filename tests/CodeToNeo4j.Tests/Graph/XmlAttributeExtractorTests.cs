using System.Xml.Linq;
using CodeToNeo4j.Graph;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Graph;

public class XmlAttributeExtractorTests
{
	[Fact]
	public void GivenElementWithAttributes_WhenExtractAttributes_ThenCreatesSymbolsAndRelationships()
	{
		// Arrange
		XmlAttributeExtractor sut = new();
		TextSymbolMapper mapper = new();
		XElement element = XElement.Parse(@"<Entry Keyboard=""Email"" Placeholder=""Enter email"" />", LoadOptions.SetLineInfo);
		List<Symbol> symbols = [];
		List<Relationship> rels = [];

		// Act
		sut.ExtractAttributes(
			element, "Entry", "parent-key", 5,
			"file-key", "path.xaml", "ns",
			mapper, symbols, rels,
			"TestAttribute", "TEST_REL",
			skipPredicate: null, commentExtractor: null,
			"xaml", "dotnet");

		// Assert
		symbols.Count.ShouldBe(2);

		Symbol keyboardSymbol = symbols.First(s => s.Name == "Keyboard");
		keyboardSymbol.Kind.ShouldBe("TestAttribute");
		keyboardSymbol.Class.ShouldBe("attribute");
		keyboardSymbol.Documentation.ShouldBe("Email");
		keyboardSymbol.Fqn.ShouldBe("Entry.Keyboard=Email");
		keyboardSymbol.Comments.ShouldBeNull();
		keyboardSymbol.Language.ShouldBe("xaml");
		keyboardSymbol.Technology.ShouldBe("dotnet");

		Symbol placeholderSymbol = symbols.First(s => s.Name == "Placeholder");
		placeholderSymbol.Documentation.ShouldBe("Enter email");

		rels.Count.ShouldBe(2);
		rels.ShouldAllBe(r => r.FromKey == "parent-key" && r.RelType == "TEST_REL");
	}

	[Fact]
	public void GivenSkipPredicate_WhenExtractAttributes_ThenSkipsMatchingAttributes()
	{
		// Arrange
		XmlAttributeExtractor sut = new();
		TextSymbolMapper mapper = new();
		XElement element = XElement.Parse(@"<Item skip=""yes"" keep=""no"" />", LoadOptions.SetLineInfo);
		List<Symbol> symbols = [];
		List<Relationship> rels = [];

		// Act
		sut.ExtractAttributes(
			element, "Item", "parent-key", 1,
			"file-key", "path.xml", null,
			mapper, symbols, rels,
			"XmlAttribute", "HAS_ATTRIBUTE",
			skipPredicate: a => a.Name.LocalName == "skip", commentExtractor: null,
			"xml", "unknown");

		// Assert
		symbols.Count.ShouldBe(1);
		symbols[0].Name.ShouldBe("keep");
	}

	[Fact]
	public void GivenCommentExtractor_WhenExtractAttributes_ThenAppliesExtractorToValue()
	{
		// Arrange
		XmlAttributeExtractor sut = new();
		TextSymbolMapper mapper = new();
		XElement element = XElement.Parse(@"<Label Text=""bound-value"" />", LoadOptions.SetLineInfo);
		List<Symbol> symbols = [];
		List<Relationship> rels = [];

		// Act
		sut.ExtractAttributes(
			element, "Label", "parent-key", 1,
			"file-key", "path.xaml", null,
			mapper, symbols, rels,
			"XamlAttribute", "SETS_PROPERTY",
			skipPredicate: null, commentExtractor: v => $"extracted:{v}",
			"xaml", "dotnet");

		// Assert
		symbols.Count.ShouldBe(1);
		symbols[0].Comments.ShouldBe("extracted:bound-value");
	}

	[Fact]
	public void GivenElementWithNoAttributes_WhenExtractAttributes_ThenNoSymbolsOrRelationships()
	{
		// Arrange
		XmlAttributeExtractor sut = new();
		TextSymbolMapper mapper = new();
		XElement element = XElement.Parse(@"<Empty />", LoadOptions.SetLineInfo);
		List<Symbol> symbols = [];
		List<Relationship> rels = [];

		// Act
		sut.ExtractAttributes(
			element, "Empty", "parent-key", 1,
			"file-key", "path.xml", null,
			mapper, symbols, rels,
			"XmlAttribute", "HAS_ATTRIBUTE",
			skipPredicate: null, commentExtractor: null,
			"xml", "unknown");

		// Assert
		symbols.ShouldBeEmpty();
		rels.ShouldBeEmpty();
	}
}
