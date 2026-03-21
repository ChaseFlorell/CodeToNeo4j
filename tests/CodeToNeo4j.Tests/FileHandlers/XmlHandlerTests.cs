using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class XmlHandlerTests
{
	[Fact]
	public async Task GivenXmlWithElements_WhenHandleCalled_ThenAddsSymbolsAndRelationships()
	{
		// Arrange
		var (sut, fileSystem) = CreateSut();
		var content = @"<root><child>value</child></root>";
		var filePath = "test.xml";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"test-file",
			filePath, filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		var rootSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "root");
		rootSymbol.ShouldNotBeNull();
		rootSymbol.Kind.ShouldBe("XmlElement");

		var childSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "child");
		childSymbol.ShouldNotBeNull();
		childSymbol.Kind.ShouldBe("XmlElement");

		relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == rootSymbol.Key && r.RelType == "CONTAINS");
		relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == childSymbol.Key && r.RelType == "CONTAINS");
	}

	[Fact]
	public async Task GivenXmlWithSingleAttribute_WhenHandleCalled_ThenCapturesAsXmlAttribute()
	{
		// Arrange
		var (sut, fileSystem) = CreateSut();
		var content = @"<root><item name=""foo"" /></root>";
		var filePath = "test.xml";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(null, null, "test-repo", "test-file", filePath, filePath, symbolBuffer, relBuffer, Accessibility.Private);

		// Assert
		var attrSymbol = symbolBuffer.FirstOrDefault(s => s.Kind == "XmlAttribute" && s.Name == "name");
		attrSymbol.ShouldNotBeNull();
		attrSymbol.Documentation.ShouldBe("foo");
		attrSymbol.Fqn.ShouldBe("item.name=foo");

		var itemSymbol = symbolBuffer.First(s => s.Kind == "XmlElement" && s.Name == "item");
		relBuffer.ShouldContain(r => r.FromKey == itemSymbol.Key && r.ToKey == attrSymbol.Key && r.RelType == "HAS_ATTRIBUTE");
	}

	[Fact]
	public async Task GivenXmlWithMultipleAttributes_WhenHandleCalled_ThenCapturesAll()
	{
		// Arrange
		var (sut, fileSystem) = CreateSut();
		var content = @"<root><PackageReference Include=""Newtonsoft.Json"" Version=""13.0.3"" /></root>";
		var filePath = "test.xml";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(null, null, "test-repo", "test-file", filePath, filePath, symbolBuffer, relBuffer, Accessibility.Private);

		// Assert
		var includeAttr = symbolBuffer.FirstOrDefault(s => s.Kind == "XmlAttribute" && s.Name == "Include");
		includeAttr.ShouldNotBeNull();
		includeAttr.Documentation.ShouldBe("Newtonsoft.Json");

		var versionAttr = symbolBuffer.FirstOrDefault(s => s.Kind == "XmlAttribute" && s.Name == "Version");
		versionAttr.ShouldNotBeNull();
		versionAttr.Documentation.ShouldBe("13.0.3");

		var pkgRefSymbol = symbolBuffer.First(s => s.Kind == "XmlElement" && s.Name == "PackageReference");
		relBuffer.ShouldContain(r => r.FromKey == pkgRefSymbol.Key && r.ToKey == includeAttr.Key && r.RelType == "HAS_ATTRIBUTE");
		relBuffer.ShouldContain(r => r.FromKey == pkgRefSymbol.Key && r.ToKey == versionAttr.Key && r.RelType == "HAS_ATTRIBUTE");
	}

	[Fact]
	public async Task GivenXmlWithNoAttributes_WhenHandleCalled_ThenNoXmlAttributeSymbols()
	{
		// Arrange
		var (sut, fileSystem) = CreateSut();
		var content = @"<root><item>value</item></root>";
		var filePath = "test.xml";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(null, null, "test-repo", "test-file", filePath, filePath, symbolBuffer, relBuffer, Accessibility.Private);

		// Assert
		symbolBuffer.ShouldNotContain(s => s.Kind == "XmlAttribute");
		relBuffer.ShouldNotContain(r => r.RelType == "HAS_ATTRIBUTE");
	}

	private static IConfigurationService CreateConfigService()
	{
		IConfigurationService fake = A.Fake<IConfigurationService>();
		A.CallTo(() => fake.GetHandlerConfiguration(A<string>._))
			.Returns(new HandlerConfiguration([".xml"], "xml"));
		return fake;
	}

	private static (XmlHandler sut, MockFileSystem fileSystem) CreateSut()
	{
		MockFileSystem fileSystem = new();
		XmlHandler sut = new(fileSystem, new TextSymbolMapper(), new XmlAttributeExtractor(), NullLogger<XmlHandler>.Instance, CreateConfigService());
		return (sut, fileSystem);
	}
}
