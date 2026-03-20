using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;
using CodeToNeo4j.Tests.Configuration;

namespace CodeToNeo4j.Tests.FileHandlers;

public class XmlHandlerTests
{
	[Fact]
	public async Task GivenXmlWithElements_WhenHandleCalled_ThenAddsSymbolsAndRelationships()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		XmlHandler sut = new(fileSystem, new TextSymbolMapper(), NullLogger<XmlHandler>.Instance, ConfigurationServiceFactory.Create());
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
}
